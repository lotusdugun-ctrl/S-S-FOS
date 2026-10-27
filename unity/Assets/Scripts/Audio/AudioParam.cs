// A WebAudio AudioParam: intrinsic value, an automation timeline with the spec's
// event types, and audio-rate inputs (a unity-gain summing junction).
//
// The timeline evaluation is a port of Chromium's AudioParamTimeline
// (ValuesForFrameRange / ProcessLinearRamp / ProcessExponentialRamp /
// ProcessSetTarget), including the float arithmetic and the 4-wide SSE
// unrolling Chrome uses on x86, so automation curves land on the same samples
// with the same values as the web build.
//
// Threading: the public methods are called from the game thread and only post
// commands; everything else runs on the audio thread (AudioCtx.Render).

using System;
using System.Collections.Generic;

namespace Sisifos.Audio
{
    public sealed class AudioParam
    {
        internal const int SetValue = 0, LinearRamp = 1, ExpRamp = 2, SetTarget = 3, SetCurve = 4;

        internal sealed class Ev
        {
            public int type;
            public float value;
            public double time;
            public double timeConstant;
            public float[] curve;
            public double duration;
        }

        // Chrome: exp(-kTimeConstantsToConverge), kTimeConstantsToConverge = 10
        const float kTimeConstantsToConverge = 10;
        const float kSetTargetThreshold = 4.539992976248485e-05f;
        const float kSetTargetZeroThreshold = 1e-20f;

        readonly AudioCtx ctx;
        public readonly float defaultValue;
        public readonly float minValue;
        public readonly float maxValue;
        /// <summary>"k-rate" params (AudioBufferSourceNode.playbackRate/detune) take one value per quantum.</summary>
        public readonly bool kRate;

        // ---- audio-thread state
        internal float intrinsic;
        internal readonly List<Ev> events = new List<Ev>();
        internal readonly List<AudioNode> inputs = new List<AudioNode>();
        internal readonly float[] values = new float[AudioCtx.RENDER_QUANTUM];
        readonly float[] one = new float[1];
        long aQuantum = -1, kQuantum = -1, saQuantum = -1;
        bool saValue;
        float kValue;

        // last value computed, for the `value` getter on the game thread
        volatile float shown;

        internal AudioParam(AudioCtx ctx, float defaultValue, float minValue, float maxValue, bool kRate = false)
        {
            this.ctx = ctx;
            this.defaultValue = defaultValue;
            this.minValue = minValue;
            this.maxValue = maxValue;
            this.kRate = kRate;
            intrinsic = defaultValue;
            shown = defaultValue;
        }

        // ------------------------------------------------------------ game thread

        /// <summary>
        /// Setting `value` sets the intrinsic value and, like Chrome, also schedules a
        /// setValueAtTime at the current time so it takes effect even under automation.
        /// </summary>
        public double value
        {
            get { return shown; }
            set
            {
                float v = (float)value;
                double t = ctx.currentTime;
                shown = v;
                ctx.Post(() =>
                {
                    intrinsic = v;
                    Insert(new Ev { type = SetValue, value = v, time = t });
                });
            }
        }

        static void CheckTime(double t, string what)
        {
            if (!(t >= 0) || double.IsInfinity(t)) throw new ArgumentOutOfRangeException(what, "Time must be a finite non-negative number");
        }

        public AudioParam setValueAtTime(double value, double startTime)
        {
            CheckTime(startTime, "startTime");
            var e = new Ev { type = SetValue, value = (float)value, time = startTime };
            ctx.Post(() => Insert(e));
            return this;
        }

        public AudioParam linearRampToValueAtTime(double value, double endTime)
        {
            CheckTime(endTime, "endTime");
            var e = new Ev { type = LinearRamp, value = (float)value, time = endTime };
            ctx.Post(() => Insert(e));
            return this;
        }

        public AudioParam exponentialRampToValueAtTime(double value, double endTime)
        {
            CheckTime(endTime, "endTime");
            if ((float)value == 0) throw new ArgumentOutOfRangeException("value", "exponentialRampToValueAtTime: value must not be 0");
            var e = new Ev { type = ExpRamp, value = (float)value, time = endTime };
            ctx.Post(() => Insert(e));
            return this;
        }

        public AudioParam setTargetAtTime(double target, double startTime, double timeConstant)
        {
            CheckTime(startTime, "startTime");
            if (!(timeConstant >= 0)) throw new ArgumentOutOfRangeException("timeConstant");
            Ev e;
            // a time constant of 0 is an instant jump (Chrome turns it into a setValue)
            if (timeConstant == 0) e = new Ev { type = SetValue, value = (float)target, time = startTime };
            else e = new Ev { type = SetTarget, value = (float)target, time = startTime, timeConstant = timeConstant };
            ctx.Post(() => Insert(e));
            return this;
        }

        public AudioParam setValueCurveAtTime(float[] values, double startTime, double duration)
        {
            CheckTime(startTime, "startTime");
            if (values == null || values.Length < 2) throw new ArgumentException("setValueCurveAtTime needs at least 2 values");
            if (!(duration > 0)) throw new ArgumentOutOfRangeException("duration");
            var copy = (float[])values.Clone();
            var e = new Ev { type = SetCurve, value = copy[copy.Length - 1], time = startTime, curve = copy, duration = duration };
            // Chrome follows a curve with an implicit setValue of its last point at the end
            var tail = new Ev { type = SetValue, value = copy[copy.Length - 1], time = startTime + duration };
            ctx.Post(() => { Insert(e); Insert(tail); });
            return this;
        }

        public AudioParam cancelScheduledValues(double cancelTime)
        {
            CheckTime(cancelTime, "cancelTime");
            ctx.Post(() =>
            {
                for (int i = events.Count - 1; i >= 0; i--)
                    if (events[i].time >= cancelTime) events.RemoveAt(i);
            });
            return this;
        }

        // ------------------------------------------------------------ audio thread

        /// <summary>Chrome's InsertEvent: sorted by time, after equal times; same type at the same time replaces.</summary>
        void Insert(Ev e)
        {
            int i;
            for (i = 0; i < events.Count; i++)
            {
                if (events[i].time == e.time && events[i].type == e.type)
                {
                    events[i] = e;
                    return;
                }
                if (events[i].time > e.time) break;
            }
            events.Insert(i, e);
        }

        /// <summary>Chrome's AudioParamTimeline::HasValues + "has connections": does this quantum need per-sample values?</summary>
        internal bool HasSampleAccurateValues(long q, long startFrame)
        {
            if (kRate) return false;
            if (saQuantum == q) return saValue;
            saQuantum = q;
            saValue = inputs.Count > 0 || HasValues(startFrame);
            return saValue;
        }

        bool HasValues(long currentFrame)
        {
            int n = events.Count;
            if (n == 0) return false;
            double sr = ctx.sampleRate;
            var e0 = events[0];
            if (e0.time > (currentFrame + AudioCtx.RENDER_QUANTUM) / sr)
            {
                switch (e0.type)
                {
                    case SetTarget:
                    case SetValue:
                    case SetCurve:
                        return false;
                    default:
                        return true;
                }
            }
            if (n >= 2) return true;
            switch (e0.type)
            {
                case SetValue:
                    // still needs the sample-accurate path in the quantum it lands in
                    // (measured: an oscillator whose .value was just set renders its
                    // first quantum on the a-rate path)
                    return !(e0.time < currentFrame / sr);
                case LinearRamp:
                case ExpRamp:
                    return !(e0.time < currentFrame / sr);
                case SetCurve:
                    {
                        double ct = currentFrame / sr;
                        return e0.time <= ct && ct < e0.time + e0.duration;
                    }
                case SetTarget:
                    return e0.time <= (currentFrame + AudioCtx.RENDER_QUANTUM) / sr;
            }
            return true;
        }

        /// <summary>a-rate values for quantum q (cached), including audio-rate inputs.</summary>
        internal float[] ComputeA(long q, long startFrame)
        {
            if (aQuantum == q) return values;
            aQuantum = q;
            int n = AudioCtx.RENDER_QUANTUM;
            double sr = ctx.sampleRate;
            intrinsic = Timeline(startFrame, n, intrinsic, values, sr, sr);
            Clip(values, n);
            if (inputs.Count > 0)
            {
                for (int i = 0; i < inputs.Count; i++)
                {
                    var src = inputs[i];
                    src.Pull(q);
                    var b = src.buf;
                    for (int k = 0; k < n; k++) values[k] += b[k];
                }
                DropFinishedInputs();
                for (int k = 0; k < n; k++) if (float.IsNaN(values[k])) values[k] = defaultValue;
                Clip(values, n);
            }
            shown = values[n - 1];
            return values;
        }

        /// <summary>k-rate value for quantum q (cached): Chrome's FinalValue().</summary>
        internal float ComputeK(long q, long startFrame)
        {
            if (kQuantum == q) return kValue;
            kQuantum = q;
            float v = intrinsic;
            double sr = ctx.sampleRate;
            if (events.Count > 0 && !((startFrame + 1) / sr <= events[0].time))
            {
                v = Timeline(startFrame, 1, intrinsic, one, sr, sr / AudioCtx.RENDER_QUANTUM);
                v = one[0];
            }
            intrinsic = v;
            if (v < minValue) v = minValue; else if (v > maxValue) v = maxValue;
            if (inputs.Count > 0)
            {
                for (int i = 0; i < inputs.Count; i++)
                {
                    inputs[i].Pull(q);
                    v += inputs[i].buf[0];
                }
                DropFinishedInputs();
                if (float.IsNaN(v)) v = defaultValue;
                if (v < minValue) v = minValue; else if (v > maxValue) v = maxValue;
            }
            kValue = v;
            shown = v;
            return v;
        }

        void DropFinishedInputs()
        {
            for (int i = inputs.Count - 1; i >= 0; i--)
            {
                var src = inputs[i];
                if (src.finished)
                {
                    inputs.RemoveAt(i);
                    src.outputs.Remove(this);
                }
            }
        }

        void Clip(float[] v, int n)
        {
            for (int k = 0; k < n; k++)
            {
                float x = v[k];
                if (x < minValue) v[k] = minValue;
                else if (x > maxValue) v[k] = maxValue;
            }
        }

        // ---- Chrome's AudioParamTimeline::ValuesForFrameRangeImpl

        float Timeline(long startFrame, int numberOfValues, float defaultValue, float[] values, double sampleRate, double controlRate)
        {
            long endFrame = startFrame + numberOfValues;
            if (events.Count == 0 || endFrame / sampleRate <= events[0].time)
            {
                for (int k = 0; k < numberOfValues; k++) values[k] = defaultValue;
                return defaultValue;
            }

            long currentFrame = startFrame;
            int writeIndex = 0;

            // HandleFirstEvent: fill with the default value up to the first event
            double firstEventTime = events[0].time;
            if (firstEventTime > startFrame / sampleRate)
            {
                long fillToEndFrame = endFrame;
                double firstEventFrame = Math.Ceiling(firstEventTime * sampleRate);
                if (endFrame > firstEventFrame) fillToEndFrame = (long)firstEventFrame;
                int fillTo = (int)Math.Min(fillToEndFrame - startFrame, numberOfValues);
                for (; writeIndex < fillTo; writeIndex++) values[writeIndex] = defaultValue;
                currentFrame += fillTo;
            }

            float value = defaultValue;
            int lastSkippedEventIndex = 0;
            int numberOfEvents = events.Count;

            for (int i = 0; i < numberOfEvents && writeIndex < numberOfValues; i++)
            {
                Ev ev = events[i];
                Ev next = i < numberOfEvents - 1 ? events[i + 1] : null;

                // IsEventCurrent: skip events whose segment is entirely in the past
                if (next != null && next.time < currentFrame / sampleRate)
                {
                    lastSkippedEventIndex = i;
                    continue;
                }

                int nextType = next != null ? next.type : -1;

                float value1 = ev.value;
                double time1 = ev.time;
                float value2 = next != null ? next.value : value1;
                double time2 = next != null ? next.time : endFrame / sampleRate + 1;

                long fillToEndFrame = endFrame;
                if (endFrame > time2 * sampleRate) fillToEndFrame = (long)Math.Ceiling(time2 * sampleRate);
                int fillToFrame = (int)Math.Max(0, Math.Min(fillToEndFrame - startFrame, numberOfValues));

                if (nextType == LinearRamp)
                {
                    // ---- ProcessLinearRamp
                    double deltaTime = time2 - time1;
                    float k = deltaTime > 0 ? (float)(1 / deltaTime) : 0;
                    float valueDelta = value2 - value1;
                    if (fillToFrame > writeIndex)
                    {
                        // x86 SSE path, 4 lanes in float
                        float invSr = (float)(1 / sampleRate);
                        float t0 = (float)(currentFrame / sampleRate - time1);
                        float kd = k * valueDelta;
                        float v0 = (invSr * 0f + t0) * kd + value1;
                        float v1 = (invSr * 1f + t0) * kd + value1;
                        float v2 = (invSr * 2f + t0) * kd + value1;
                        float v3 = (invSr * 3f + t0) * kd + value1;
                        float inc = (float)(4 / sampleRate * k * valueDelta);
                        int trunc = writeIndex + ((fillToFrame - writeIndex) / 4) * 4;
                        currentFrame += trunc - writeIndex;
                        for (; writeIndex < trunc; writeIndex += 4)
                        {
                            values[writeIndex] = v0;
                            values[writeIndex + 1] = v1;
                            values[writeIndex + 2] = v2;
                            values[writeIndex + 3] = v3;
                            v0 += inc; v1 += inc; v2 += inc; v3 += inc;
                        }
                    }
                    if (writeIndex >= 1) value = values[writeIndex - 1];
                    for (; writeIndex < fillToFrame; writeIndex++)
                    {
                        float x = (float)((currentFrame / sampleRate - time1) * k);
                        value = value1 + x * valueDelta;
                        values[writeIndex] = value;
                        currentFrame++;
                    }
                }
                else if (nextType == ExpRamp)
                {
                    // ---- ProcessExponentialRamp
                    if (value1 * value2 <= 0 || time1 >= time2)
                    {
                        value = value1;
                        for (; writeIndex < fillToFrame; writeIndex++) values[writeIndex] = value;
                    }
                    else
                    {
                        double deltaTime = time2 - time1;
                        double numSampleFrames = deltaTime * sampleRate;
                        double multiplier = Math.Pow(value2 / value1, 1 / numSampleFrames);
                        value = (float)(value1 * Math.Pow(value2 / (double)value1, (currentFrame / sampleRate - time1) / deltaTime));
                        for (; writeIndex < fillToFrame; writeIndex++)
                        {
                            values[writeIndex] = value;
                            value = (float)(value * multiplier);
                            currentFrame++;
                        }
                        if (writeIndex >= 1) value = (float)(value / multiplier);
                        if (currentFrame > time2 * sampleRate - 0.5) value = value2;
                    }
                }
                else
                {
                    switch (ev.type)
                    {
                        case SetValue:
                        case LinearRamp:
                            currentFrame = fillToEndFrame;
                            value = ev.value;
                            for (; writeIndex < fillToFrame; writeIndex++) values[writeIndex] = value;
                            break;
                        case ExpRamp:
                            currentFrame = fillToEndFrame;
                            if (i >= 1 && events[i - 1].value * ev.value > 0) value = ev.value;
                            for (; writeIndex < fillToFrame; writeIndex++) values[writeIndex] = value;
                            break;
                        case SetTarget:
                            {
                                // ---- ProcessSetTarget
                                float target = value1;
                                double timeConstant = ev.timeConstant;
                                float discreteTimeConstant = (float)(1 - Math.Exp(-1 / (controlRate * timeConstant)));
                                double rampStartFrame = time1 * sampleRate;
                                if (rampStartFrame <= currentFrame && currentFrame < rampStartFrame + 1)
                                    value = (float)(target + (value - target) * Math.Exp(-(currentFrame / sampleRate - time1) / timeConstant));
                                else
                                    value += (target - value) * discreteTimeConstant;

                                if (HasSetTargetConverged(value, target, currentFrame / sampleRate, time1, timeConstant))
                                {
                                    currentFrame += fillToFrame - writeIndex;
                                    for (; writeIndex < fillToFrame; writeIndex++) values[writeIndex] = target;
                                    value = target;
                                }
                                else
                                {
                                    if (fillToFrame > writeIndex)
                                    {
                                        // x86 SSE path: 4 steps of the recursion at a time
                                        float c0 = discreteTimeConstant;
                                        float c1 = c0 * (2 - c0);
                                        float c2 = c0 * ((c0 - 3) * c0 + 3);
                                        float c3 = c0 * (c0 * ((4 - c0) * c0 - 6) + 4);
                                        int trunc = writeIndex + ((fillToFrame - writeIndex) / 4) * 4;
                                        for (; writeIndex < trunc; writeIndex += 4)
                                        {
                                            float delta = target - value;
                                            values[writeIndex] = value + delta * 0f;
                                            values[writeIndex + 1] = value + delta * c0;
                                            values[writeIndex + 2] = value + delta * c1;
                                            values[writeIndex + 3] = value + delta * c2;
                                            value += delta * c3;
                                        }
                                    }
                                    for (; writeIndex < fillToFrame; writeIndex++)
                                    {
                                        values[writeIndex] = value;
                                        value += (target - value) * discreteTimeConstant;
                                    }
                                    if (writeIndex >= 1) value = values[writeIndex - 1];
                                    currentFrame = startFrame + writeIndex;
                                }
                                break;
                            }
                        case SetCurve:
                            {
                                float[] curve = ev.curve;
                                int len = curve.Length;
                                double dur = ev.duration;
                                double curveEnd = time1 + dur;
                                for (; writeIndex < fillToFrame; writeIndex++)
                                {
                                    double t = currentFrame / sampleRate;
                                    if (t >= curveEnd) value = curve[len - 1];
                                    else
                                    {
                                        double pos = (len - 1) / dur * (t - time1);
                                        if (pos < 0) pos = 0;
                                        int kk = (int)Math.Floor(pos);
                                        if (kk >= len - 1) value = curve[len - 1];
                                        else value = (float)(curve[kk] + (curve[kk + 1] - curve[kk]) * (pos - kk));
                                    }
                                    values[writeIndex] = value;
                                    currentFrame++;
                                }
                                break;
                            }
                    }
                }
            }

            // drop events that are entirely in the past (keep the last skipped one:
            // a ramp needs its start point, and an ended exponential ramp checks it)
            if (lastSkippedEventIndex > 0) events.RemoveRange(0, lastSkippedEventIndex);

            for (; writeIndex < numberOfValues; writeIndex++) values[writeIndex] = value;
            return value;
        }

        static bool HasSetTargetConverged(float value, float target, double currentTime, double startTime, double timeConstant)
        {
            if (currentTime > startTime + kTimeConstantsToConverge * timeConstant) return true;
            if (target == 0) return Math.Abs(value) < kSetTargetZeroThreshold;
            return Math.Abs(value - target) < kSetTargetThreshold * Math.Abs(target);
        }
    }
}
