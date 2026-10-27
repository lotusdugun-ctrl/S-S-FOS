// The WebAudio nodes GameAudio uses, each a port of what Chromium does when it
// renders them: OscillatorNode (band-limited wavetables, the same interpolation
// and the same k-rate SIMD path), BiquadFilterNode (Chrome's coefficient
// formulas, per-sample coefficients under automation), GainNode,
// AudioBufferSourceNode (linear-interpolating resampler, loop, offset) and a
// MediaElementAudioSourceNode stand-in for the music (see AudioCtx).
//
// The graph is mono: every node GameAudio builds is single-channel, and the
// destination up-mixes mono to the output channels as WebAudio's "speakers"
// rule does.
//
// Threading: public members are game-thread API and only post commands to the
// audio thread; everything internal runs on the audio thread.

using System;
using System.Collections.Generic;

namespace Sisifos.Audio
{
    public abstract class AudioNode
    {
        public readonly AudioCtx context;

        // ---- audio-thread state
        internal readonly List<AudioNode> inputs = new List<AudioNode>();
        internal readonly List<object> outputs = new List<object>();
        internal readonly float[] buf = new float[AudioCtx.RENDER_QUANTUM];
        /// <summary>The music probe: the gain the media element's signal gets on its way out (see AudioCtx).</summary>
        internal float[] probe;
        internal bool hasProbe;
        internal long quantum = -1;
        internal bool hadInput;
        /// <summary>Will output only silence from now on; consumers drop the connection.</summary>
        internal bool finished;

        protected AudioNode(AudioCtx context)
        {
            this.context = context;
        }

        /// <summary>node.connect(dest) — returns dest, for chaining.</summary>
        public T connect<T>(T destination) where T : AudioNode
        {
            if (destination == null) throw new ArgumentNullException("destination");
            var self = this;
            context.Post(() =>
            {
                if (destination.inputs.Contains(self)) return;
                destination.inputs.Add(self);
                destination.hadInput = true;
                self.outputs.Add(destination);
            });
            return destination;
        }

        /// <summary>node.connect(param): an audio-rate modulation input.</summary>
        public void connect(AudioParam destination)
        {
            if (destination == null) throw new ArgumentNullException("destination");
            var self = this;
            context.Post(() =>
            {
                if (destination.inputs.Contains(self)) return;
                destination.inputs.Add(self);
                self.outputs.Add(destination);
            });
        }

        public void disconnect()
        {
            var self = this;
            context.Post(() =>
            {
                foreach (var o in self.outputs)
                {
                    var n = o as AudioNode;
                    if (n != null) n.inputs.Remove(self);
                    else ((AudioParam)o).inputs.Remove(self);
                }
                self.outputs.Clear();
            });
        }

        internal void Pull(long q)
        {
            if (quantum == q) return;
            quantum = q;
            context.nodesThisQuantum++;
            Process(q, context.quantumStartFrame);
        }

        internal abstract void Process(long q, long startFrame);

        /// <summary>Sums the inputs into buf (and their probes into probe), dropping finished ones.</summary>
        protected void SumInputs(long q)
        {
            int n = AudioCtx.RENDER_QUANTUM;
            Array.Clear(buf, 0, n);
            hasProbe = false;
            for (int i = 0; i < inputs.Count; i++)
            {
                var src = inputs[i];
                src.Pull(q);
                var b = src.buf;
                for (int k = 0; k < n; k++) buf[k] += b[k];
                if (src.hasProbe)
                {
                    if (probe == null) probe = new float[n];
                    if (!hasProbe) { Array.Clear(probe, 0, n); hasProbe = true; }
                    var p = src.probe;
                    for (int k = 0; k < n; k++) probe[k] += p[k];
                }
            }
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
    }

    // ======================================================================

    public sealed class AudioDestinationNode : AudioNode
    {
        internal AudioDestinationNode(AudioCtx c) : base(c) { }

        internal override void Process(long q, long startFrame)
        {
            SumInputs(q);
        }
    }

    // ======================================================================

    public sealed class GainNode : AudioNode
    {
        public readonly AudioParam gain;

        internal GainNode(AudioCtx c) : base(c)
        {
            gain = new AudioParam(c, 1, -float.MaxValue, float.MaxValue);
        }

        internal override void Process(long q, long startFrame)
        {
            SumInputs(q);
            int n = AudioCtx.RENDER_QUANTUM;
            if (gain.HasSampleAccurateValues(q, startFrame))
            {
                var g = gain.ComputeA(q, startFrame);
                for (int k = 0; k < n; k++) buf[k] *= g[k];
                if (hasProbe) for (int k = 0; k < n; k++) probe[k] *= g[k];
            }
            else
            {
                float g = gain.ComputeK(q, startFrame);
                if (g == 0)
                {
                    Array.Clear(buf, 0, n);
                    if (hasProbe) Array.Clear(probe, 0, n);
                }
                else
                {
                    for (int k = 0; k < n; k++) buf[k] *= g;
                    if (hasProbe) for (int k = 0; k < n; k++) probe[k] *= g;
                }
            }
            if (hadInput && inputs.Count == 0 && gain.inputs.Count == 0) finished = true;
        }
    }

    // ======================================================================

    public sealed class BiquadFilterNode : AudioNode
    {
        public readonly AudioParam frequency;
        public readonly AudioParam detune;
        public readonly AudioParam Q;
        public readonly AudioParam gain;

        volatile string _type = "lowpass";
        int typeId; // audio thread

        /// <summary>"lowpass", "highpass", "bandpass", "lowshelf", "highshelf", "peaking", "notch", "allpass"</summary>
        public string type
        {
            get { return _type; }
            set
            {
                int id = TypeId(value);
                if (id < 0) return; // WebIDL enum: invalid values are ignored
                _type = value;
                context.Post(() => { typeId = id; dirty = true; });
            }
        }

        static int TypeId(string t)
        {
            switch (t)
            {
                case "lowpass": return 0;
                case "highpass": return 1;
                case "bandpass": return 2;
                case "lowshelf": return 3;
                case "highshelf": return 4;
                case "peaking": return 5;
                case "notch": return 6;
                case "allpass": return 7;
            }
            return -1;
        }

        // coefficients (per sample when automated)
        readonly double[] b0 = new double[AudioCtx.RENDER_QUANTUM], b1 = new double[AudioCtx.RENDER_QUANTUM], b2 = new double[AudioCtx.RENDER_QUANTUM];
        readonly double[] a1 = new double[AudioCtx.RENDER_QUANTUM], a2 = new double[AudioCtx.RENDER_QUANTUM];
        bool sampleAccurateCoefs;
        bool dirty = true;
        float prevF = float.NaN, prevQ = float.NaN, prevG = float.NaN, prevD = float.NaN;
        double x1, x2, y1, y2;

        internal BiquadFilterNode(AudioCtx c) : base(c)
        {
            float nyquist = (float)(c.sampleRate / 2);
            frequency = new AudioParam(c, 350, 0, nyquist);
            Q = new AudioParam(c, 1, -float.MaxValue, float.MaxValue);
            gain = new AudioParam(c, 0, -float.MaxValue, 40 * (float)Math.Log10(float.MaxValue));
            float maxDetune = 1200 * (float)(Math.Log(float.MaxValue) / Math.Log(2));
            detune = new AudioParam(c, 0, -maxDetune, maxDetune);
        }

        internal override void Process(long q, long startFrame)
        {
            SumInputs(q);
            hasProbe = false; // the music probe only passes through gains
            int n = AudioCtx.RENDER_QUANTUM;

            bool sa = frequency.HasSampleAccurateValues(q, startFrame) | Q.HasSampleAccurateValues(q, startFrame)
                      | gain.HasSampleAccurateValues(q, startFrame) | detune.HasSampleAccurateValues(q, startFrame);
            if (sa)
            {
                var f = frequency.ComputeA(q, startFrame);
                var qq = Q.ComputeA(q, startFrame);
                var g = gain.ComputeA(q, startFrame);
                var d = detune.ComputeA(q, startFrame);
                for (int k = 0; k < n; k++) SetCoefficients(k, f[k], qq[k], g[k], d[k]);
                sampleAccurateCoefs = true;
                dirty = true; // when automation ends, recompute from the final values
            }
            else
            {
                float f = frequency.ComputeK(q, startFrame);
                float qq = Q.ComputeK(q, startFrame);
                float g = gain.ComputeK(q, startFrame);
                float d = detune.ComputeK(q, startFrame);
                if (dirty || f != prevF || qq != prevQ || g != prevG || d != prevD)
                {
                    SetCoefficients(0, f, qq, g, d);
                    prevF = f; prevQ = qq; prevG = g; prevD = d;
                    dirty = false;
                }
                sampleAccurateCoefs = false;
            }

            // Biquad::Process: double state, float output
            double X1 = x1, X2 = x2, Y1 = y1, Y2 = y2;
            if (sampleAccurateCoefs)
            {
                for (int k = 0; k < n; k++)
                {
                    float x = buf[k];
                    float y = (float)(b0[k] * x + b1[k] * X1 + b2[k] * X2 - a1[k] * Y1 - a2[k] * Y2);
                    buf[k] = y;
                    X2 = X1; X1 = x; Y2 = Y1; Y1 = y;
                }
            }
            else
            {
                double B0 = b0[0], B1 = b1[0], B2 = b2[0], A1 = a1[0], A2 = a2[0];
                for (int k = 0; k < n; k++)
                {
                    float x = buf[k];
                    float y = (float)(B0 * x + B1 * X1 + B2 * X2 - A1 * Y1 - A2 * Y2);
                    buf[k] = y;
                    X2 = X1; X1 = x; Y2 = Y1; Y1 = y;
                }
            }
            // avoid a stream of subnormals as the tail dies away
            if (X1 == 0 && X2 == 0 && (Y1 != 0 || Y2 != 0) && Math.Abs(Y1) < float.Epsilon * 8388608.0 && Math.Abs(Y2) < float.Epsilon * 8388608.0)
            {
                Y1 = Y2 = 0;
                for (int i = n; i-- > 0;)
                {
                    if (Math.Abs(buf[i]) < 1.17549435e-38f) buf[i] = 0;
                    else break;
                }
            }
            x1 = X1; x2 = X2; y1 = Y1; y2 = Y2;

            // inputs gone: keep ringing out the tail, then die
            if (hadInput && inputs.Count == 0 && Math.Abs(y1) < 1e-9 && Math.Abs(y2) < 1e-9 && x1 == 0 && x2 == 0
                && frequency.inputs.Count == 0 && Q.inputs.Count == 0 && gain.inputs.Count == 0 && detune.inputs.Count == 0)
                finished = true;
        }

        void SetCoefficients(int k, float cutoff, float q, float gainDb, float det)
        {
            double nyquist = 0.5 * context.sampleRate;
            double f = cutoff / nyquist;
            if (det != 0) f *= Math.Pow(2, (float)(det / 1200f));
            switch (typeId)
            {
                case 0: Lowpass(k, f, q); break;
                case 1: Highpass(k, f, q); break;
                case 2: Bandpass(k, f, q); break;
                case 3: Lowshelf(k, f, gainDb); break;
                case 4: Highshelf(k, f, gainDb); break;
                case 5: Peaking(k, f, q, gainDb); break;
                case 6: Notch(k, f, q); break;
                case 7: Allpass(k, f, q); break;
            }
        }

        void Set(int k, double B0, double B1, double B2, double A0, double A1, double A2)
        {
            double inv = 1 / A0;
            b0[k] = B0 * inv; b1[k] = B1 * inv; b2[k] = B2 * inv;
            a1[k] = A1 * inv; a2[k] = A2 * inv;
        }

        static double Clamp01(double v) { return v < 0 ? 0 : (v > 1 ? 1 : v); }

        void Lowpass(int k, double cutoff, double resonance)
        {
            cutoff = Clamp01(cutoff);
            if (cutoff == 1) Set(k, 1, 0, 0, 1, 0, 0);
            else if (cutoff > 0)
            {
                resonance = Math.Pow(10.0, resonance / 20);
                double theta = Math.PI * cutoff;
                double alpha = Math.Sin(theta) / (2 * resonance);
                double cosw = Math.Cos(theta);
                double beta = (1 - cosw) / 2;
                Set(k, beta, 2 * beta, beta, 1 + alpha, -2 * cosw, 1 - alpha);
            }
            else Set(k, 0, 0, 0, 1, 0, 0);
        }

        void Highpass(int k, double cutoff, double resonance)
        {
            cutoff = Clamp01(cutoff);
            if (cutoff == 1) Set(k, 0, 0, 0, 1, 0, 0);
            else if (cutoff > 0)
            {
                resonance = Math.Pow(10.0, resonance / 20);
                double theta = Math.PI * cutoff;
                double alpha = Math.Sin(theta) / (2 * resonance);
                double cosw = Math.Cos(theta);
                double beta = (1 + cosw) / 2;
                Set(k, beta, -2 * beta, beta, 1 + alpha, -2 * cosw, 1 - alpha);
            }
            else Set(k, 1, 0, 0, 1, 0, 0);
        }

        void Bandpass(int k, double frequency, double q)
        {
            frequency = Math.Max(0.0, frequency);
            q = Math.Max(0.0, q);
            if (frequency > 0 && frequency < 1)
            {
                double w0 = Math.PI * frequency;
                if (q > 0)
                {
                    double alpha = Math.Sin(w0) / (2 * q);
                    double c = Math.Cos(w0);
                    Set(k, alpha, 0, -alpha, 1 + alpha, -2 * c, 1 - alpha);
                }
                else Set(k, 1, 0, 0, 1, 0, 0);
            }
            else Set(k, 0, 0, 0, 1, 0, 0);
        }

        void Lowshelf(int k, double frequency, double dbGain)
        {
            frequency = Clamp01(frequency);
            double A = Math.Pow(10.0, dbGain / 40);
            if (frequency == 1) Set(k, A * A, 0, 0, 1, 0, 0);
            else if (frequency > 0)
            {
                double w0 = Math.PI * frequency;
                double S = 1;
                double alpha = 0.5 * Math.Sin(w0) * Math.Sqrt((A + 1 / A) * (1 / S - 1) + 2);
                double c = Math.Cos(w0);
                double k2 = 2 * Math.Sqrt(A) * alpha;
                double ap = A + 1, am = A - 1;
                Set(k, A * (ap - am * c + k2), 2 * A * (am - ap * c), A * (ap - am * c - k2),
                    ap + am * c + k2, -2 * (am + ap * c), ap + am * c - k2);
            }
            else Set(k, 1, 0, 0, 1, 0, 0);
        }

        void Highshelf(int k, double frequency, double dbGain)
        {
            frequency = Clamp01(frequency);
            double A = Math.Pow(10.0, dbGain / 40);
            if (frequency == 1) Set(k, 1, 0, 0, 1, 0, 0);
            else if (frequency > 0)
            {
                double w0 = Math.PI * frequency;
                double S = 1;
                double alpha = 0.5 * Math.Sin(w0) * Math.Sqrt((A + 1 / A) * (1 / S - 1) + 2);
                double c = Math.Cos(w0);
                double k2 = 2 * Math.Sqrt(A) * alpha;
                double ap = A + 1, am = A - 1;
                Set(k, A * (ap + am * c + k2), -2 * A * (am + ap * c), A * (ap + am * c - k2),
                    ap - am * c + k2, 2 * (am - ap * c), ap - am * c - k2);
            }
            else Set(k, A * A, 0, 0, 1, 0, 0);
        }

        void Peaking(int k, double frequency, double q, double dbGain)
        {
            frequency = Clamp01(frequency);
            q = Math.Max(0.0, q);
            double A = Math.Pow(10.0, dbGain / 40);
            if (frequency > 0 && frequency < 1)
            {
                if (q > 0)
                {
                    double w0 = Math.PI * frequency;
                    double alpha = Math.Sin(w0) / (2 * q);
                    double c = Math.Cos(w0);
                    Set(k, 1 + alpha * A, -2 * c, 1 - alpha * A, 1 + alpha / A, -2 * c, 1 - alpha / A);
                }
                else Set(k, A * A, 0, 0, 1, 0, 0);
            }
            else Set(k, 1, 0, 0, 1, 0, 0);
        }

        void Notch(int k, double frequency, double q)
        {
            frequency = Clamp01(frequency);
            q = Math.Max(0.0, q);
            if (frequency > 0 && frequency < 1)
            {
                if (q > 0)
                {
                    double w0 = Math.PI * frequency;
                    double alpha = Math.Sin(w0) / (2 * q);
                    double c = Math.Cos(w0);
                    Set(k, 1, -2 * c, 1, 1 + alpha, -2 * c, 1 - alpha);
                }
                else Set(k, 0, 0, 0, 1, 0, 0);
            }
            else Set(k, 1, 0, 0, 1, 0, 0);
        }

        void Allpass(int k, double frequency, double q)
        {
            frequency = Clamp01(frequency);
            q = Math.Max(0.0, q);
            if (frequency > 0 && frequency < 1)
            {
                if (q > 0)
                {
                    double w0 = Math.PI * frequency;
                    double alpha = Math.Sin(w0) / (2 * q);
                    double c = Math.Cos(w0);
                    Set(k, 1 - alpha, -2 * c, 1 + alpha, 1 + alpha, -2 * c, 1 - alpha);
                }
                else Set(k, -1, 0, 0, 1, 0, 0);
            }
            else Set(k, 1, 0, 0, 1, 0, 0);
        }
    }

    // ======================================================================

    /// <summary>start/stop scheduling shared by the source nodes (Chrome's AudioScheduledSourceHandler).</summary>
    public abstract class AudioScheduledSourceNode : AudioNode
    {
        const int UNSCHEDULED = 0, SCHEDULED = 1, PLAYING = 2, FINISHED = 3;

        bool startCalled; // game thread
        internal double startTime;
        internal double endTime = double.NaN; // NaN: unknown
        int playbackState = UNSCHEDULED;

        /// <summary>Called on the audio thread when the source has finished. Do not touch the graph from it.</summary>
        public Action onended;

        protected AudioScheduledSourceNode(AudioCtx c) : base(c) { }

        protected void StartCommon(double when, Action extra)
        {
            if (startCalled) throw new InvalidOperationException("start() can only be called once");
            if (!(when >= 0)) throw new ArgumentOutOfRangeException("when");
            startCalled = true;
            context.Post(() =>
            {
                if (extra != null) extra();
                startTime = when;
                playbackState = SCHEDULED;
            });
        }

        public void stop(double when = 0)
        {
            if (!startCalled) throw new InvalidOperationException("stop() before start()");
            if (!(when >= 0)) throw new ArgumentOutOfRangeException("when");
            context.Post(() =>
            {
                if (playbackState != FINISHED) endTime = when;
            });
        }

        internal void Finish()
        {
            if (playbackState == FINISHED) return;
            playbackState = FINISHED;
            finished = true;
            var cb = onended;
            if (cb != null) cb();
        }

        /// <summary>
        /// Start/stop time to frame. Oscillators: Chrome's TimeToSampleFrame(kRoundUp), a
        /// plain ceil. Buffer sources (measured against Chrome): a time within 1e-4 frame
        /// above a frame boundary starts on that frame, so round-off in `t + x` does not
        /// delay the source by a sample.
        /// </summary>
        internal virtual long TimeToFrameRoundUp(double time, double sr)
        {
            return (long)Math.Ceiling(time * sr);
        }

        /// <summary>Stop time to frame: as measured against Chrome, round-off just above a frame boundary stops on it.</summary>
        static long EndFrame(double time, double sr)
        {
            double frame = time * sr;
            double fl = Math.Floor(frame);
            if (frame - fl <= 1e-4) return (long)fl;
            return (long)Math.Ceiling(frame);
        }

        /// <summary>
        /// Chrome's UpdateSchedulingInfo. Zeroes buf where the source is silent and
        /// returns false when the whole quantum is silent.
        /// </summary>
        internal bool UpdateSchedulingInfo(long startFrameQ, out int quantumFrameOffset, out int nonSilentFramesToProcess, out double startFrameOffset)
        {
            int size = AudioCtx.RENDER_QUANTUM;
            quantumFrameOffset = 0;
            nonSilentFramesToProcess = 0;
            startFrameOffset = 0;
            double sr = context.sampleRate;
            long quantumStartFrame = startFrameQ;
            long quantumEndFrame = quantumStartFrame + size;
            long startFrame = playbackState == UNSCHEDULED ? 0 : TimeToFrameRoundUp(startTime, sr);
            bool endKnown = !double.IsNaN(endTime);
            long endFrame = endKnown ? EndFrame(endTime, sr) : 0;

            if (endKnown && endFrame <= quantumStartFrame) Finish();

            if (playbackState == UNSCHEDULED || playbackState == FINISHED || startFrame >= quantumEndFrame)
            {
                Array.Clear(buf, 0, size);
                return false;
            }

            if (playbackState == SCHEDULED)
            {
                playbackState = PLAYING;
                startFrameOffset = startTime * sr - startFrame;
            }

            long off = startFrame > quantumStartFrame ? startFrame - quantumStartFrame : 0;
            quantumFrameOffset = (int)Math.Min(off, size);
            nonSilentFramesToProcess = size - quantumFrameOffset;
            if (nonSilentFramesToProcess == 0)
            {
                Array.Clear(buf, 0, size);
                return false;
            }
            if (quantumFrameOffset > 0) Array.Clear(buf, 0, quantumFrameOffset);

            if (endKnown && endFrame >= quantumStartFrame && endFrame < quantumEndFrame)
            {
                int zeroStart = (int)(endFrame - quantumStartFrame);
                int framesToZero = size - zeroStart;
                if (framesToZero > nonSilentFramesToProcess) nonSilentFramesToProcess = 0;
                else nonSilentFramesToProcess -= framesToZero;
                Array.Clear(buf, zeroStart, framesToZero);
                Finish();
            }
            return true;
        }
    }

    // ======================================================================

    public sealed class OscillatorNode : AudioScheduledSourceNode
    {
        // Chrome's interpolation thresholds on the table increment
        const float kInterpolate2Point = 0.3f;
        const float kInterpolate3Point = 0.16f;

        public readonly AudioParam frequency;
        public readonly AudioParam detune;

        volatile string _type = "sine";
        PeriodicWave wave; // audio thread
        double virtualReadIndex;
        readonly float[] phaseIncrements = new float[AudioCtx.RENDER_QUANTUM];
        readonly float[] detuneValues = new float[AudioCtx.RENDER_QUANTUM];

        /// <summary>"sine", "square", "sawtooth", "triangle" (or "custom" after setPeriodicWave)</summary>
        public string type
        {
            get { return _type; }
            set
            {
                if (value != "sine" && value != "square" && value != "sawtooth" && value != "triangle")
                {
                    if (value == "custom") throw new InvalidOperationException("use setPeriodicWave() for a custom wave");
                    return;
                }
                _type = value;
                var w = PeriodicWave.Basic(value, (float)context.sampleRate);
                context.Post(() => wave = w);
            }
        }

        public void setPeriodicWave(PeriodicWave w)
        {
            _type = "custom";
            context.Post(() => wave = w);
        }

        internal OscillatorNode(AudioCtx c) : base(c)
        {
            float nyquist = (float)(c.sampleRate / 2);
            frequency = new AudioParam(c, 440, -nyquist, nyquist);
            float maxDetune = 1200 * (float)(Math.Log(float.MaxValue) / Math.Log(2));
            detune = new AudioParam(c, 0, -maxDetune, maxDetune);
            wave = PeriodicWave.Basic("sine", (float)c.sampleRate);
        }

        public void start(double when = 0) { StartCommon(when, null); }

        static float DetuneToFrequencyMultiplier(float detune)
        {
            return (float)Math.Pow(2, detune / 1200);
        }

        internal override void Process(long q, long startFrame)
        {
            int frames = AudioCtx.RENDER_QUANTUM;
            int quantumFrameOffset, nonSilent;
            double startFrameOffset;
            if (!UpdateSchedulingInfo(startFrame, out quantumFrameOffset, out nonSilent, out startFrameOffset) || nonSilent == 0)
            {
                // keep the automation timelines moving
                if (frequency.HasSampleAccurateValues(q, startFrame)) frequency.ComputeA(q, startFrame); else frequency.ComputeK(q, startFrame);
                if (detune.HasSampleAccurateValues(q, startFrame)) detune.ComputeA(q, startFrame); else detune.ComputeK(q, startFrame);
                Array.Clear(buf, 0, frames);
                return;
            }

            float nyquist = (float)(context.sampleRate / 2);
            int periodicWaveSize = wave.periodicWaveSize;
            float rateScale = wave.rateScale;
            double vri = virtualReadIndex;

            // CalculateSampleAccuratePhaseIncrements
            bool hasSampleAccurateValues = false;
            bool hasFrequencyChanges = false;
            float finalScale = rateScale;
            if (frequency.HasSampleAccurateValues(q, startFrame))
            {
                hasFrequencyChanges = true;
                hasSampleAccurateValues = true;
                Array.Copy(frequency.ComputeA(q, startFrame), phaseIncrements, frames);
            }
            else finalScale *= frequency.ComputeK(q, startFrame);
            if (detune.HasSampleAccurateValues(q, startFrame))
            {
                hasSampleAccurateValues = true;
                float[] dv = hasFrequencyChanges ? detuneValues : phaseIncrements;
                Array.Copy(detune.ComputeA(q, startFrame), dv, frames);
                float kk = (float)(1.0 / 1200);
                for (int i = 0; i < frames; i++) dv[i] = (float)Math.Pow(2, dv[i] * kk);
                if (hasFrequencyChanges) for (int i = 0; i < frames; i++) phaseIncrements[i] *= dv[i];
            }
            else finalScale *= DetuneToFrequencyMultiplier(detune.ComputeK(q, startFrame));
            if (hasSampleAccurateValues)
            {
                for (int i = 0; i < frames; i++)
                {
                    float v = phaseIncrements[i];
                    if (v < -nyquist) v = -nyquist; else if (v > nyquist) v = nyquist;
                    phaseIncrements[i] = v * finalScale;
                }
            }

            float freqK = 0;
            if (!hasSampleAccurateValues)
            {
                freqK = frequency.ComputeK(q, startFrame);
                freqK *= DetuneToFrequencyMultiplier(detune.ComputeK(q, startFrame));
                freqK = ClampFrequency(freqK, nyquist);
            }

            int destOffset = quantumFrameOffset;
            int n = nonSilent;
            // a start between frames advances the phase by the fraction (on the a-rate
            // path Chrome's frequency here is 0, so it starts at phase 0 on the next frame)
            float offsetFrequency = freqK;
            if (startFrameOffset > 0)
            {
                destOffset++;
                n--;
                vri += (1 - startFrameOffset) * offsetFrequency * rateScale;
            }
            else if (startFrameOffset < 0)
            {
                vri = -startFrameOffset * offsetFrequency * rateScale;
            }

            if (hasSampleAccurateValues) vri = ProcessARate(n, destOffset, vri);
            else vri = ProcessKRate(n, destOffset, vri, freqK);
            virtualReadIndex = vri;
        }

        static float ClampFrequency(float f, float nyquist)
        {
            if (f > nyquist) return nyquist;
            if (f < -nyquist) return -nyquist;
            return f;
        }

        double ProcessARate(int n, int destOffset, double vri)
        {
            float rateScale = wave.rateScale;
            float invRateScale = 1 / rateScale;
            int size = wave.periodicWaveSize;
            double invSize = 1.0 / size;
            int mask = size - 1;
            for (int m = 0; m < n; m++)
            {
                // PORT: Chrome reads the increments from the start of the quantum
                // even when the source starts mid-quantum; so do we.
                float incr = phaseIncrements[m];
                float freq = invRateScale * incr;
                float[] lower, higher;
                float tif;
                wave.WaveDataForFundamentalFrequency(freq, out lower, out higher, out tif);
                buf[destOffset + m] = DoInterpolation(vri, Math.Abs(incr), mask, tif, lower, higher);
                vri += incr;
                vri -= Math.Floor(vri * invSize) * size;
            }
            return vri;
        }

        double ProcessKRate(int n, int destOffset, double vri, float frequency)
        {
            int size = wave.periodicWaveSize;
            double invSize = 1.0 / size;
            int mask = size - 1;
            float[] lower, higher;
            float tif;
            wave.WaveDataForFundamentalFrequency(frequency, out lower, out higher, out tif);
            float rateScale = wave.rateScale;
            float incr = frequency * rateScale;

            if (incr >= kInterpolate2Point)
            {
                // ProcessKRateVector: 4 lanes with a float read index
                int nLoops = n / 4;
                float vSize = size;
                float vInvSize = 1.0f / size;
                float fourIncr = 4 * incr;
                float i0 = (float)vri, i1 = (float)(vri + incr), i2 = (float)(vri + 2 * incr), i3 = (float)(vri + 3 * incr);
                i0 = Wrap(i0, vSize, vInvSize); i1 = Wrap(i1, vSize, vInvSize);
                i2 = Wrap(i2, vSize, vInvSize); i3 = Wrap(i3, vSize, vInvSize);
                int o = destOffset;
                for (int k = 0; k < nLoops; k++)
                {
                    buf[o++] = LerpSample(i0, mask, tif, lower, higher);
                    buf[o++] = LerpSample(i1, mask, tif, lower, higher);
                    buf[o++] = LerpSample(i2, mask, tif, lower, higher);
                    buf[o++] = LerpSample(i3, mask, tif, lower, higher);
                    i0 = Wrap(i0 + fourIncr, vSize, vInvSize); i1 = Wrap(i1 + fourIncr, vSize, vInvSize);
                    i2 = Wrap(i2 + fourIncr, vSize, vInvSize); i3 = Wrap(i3 + fourIncr, vSize, vInvSize);
                }
                int done = nLoops * 4;
                double v = vri + done * (double)incr;
                v -= Math.Floor(v * invSize) * size;
                // ProcessKRateScalar for the remainder
                for (int k = done; k < n; k++)
                {
                    int r0 = (int)(uint)v & mask;
                    int r1 = (r0 + 1) & mask;
                    float f = (float)v - r0;
                    float sh = higher[r0] + f * (higher[r1] - higher[r0]);
                    float sl = lower[r0] + f * (lower[r1] - lower[r0]);
                    buf[destOffset + k] = sh + tif * (sl - sh);
                    v += incr;
                    v -= Math.Floor(v * invSize) * size;
                }
                vri += n * (double)incr;
                vri -= Math.Floor(vri * invSize) * size;
            }
            else
            {
                float aincr = Math.Abs(incr);
                for (int k = 0; k < n; k++)
                {
                    buf[destOffset + k] = DoInterpolation(vri, aincr, mask, tif, lower, higher);
                    vri += incr;
                    vri -= Math.Floor(vri * invSize) * size;
                }
            }
            return vri;
        }

        /// <summary>x - floor(x / size) * size in float, as the SSE code does it.</summary>
        static float Wrap(float x, float size, float invSize)
        {
            float r = x * invSize;
            int f = (int)r; // truncation
            if (r < (float)f) f -= 1;
            return x - (float)f * size;
        }

        static float LerpSample(float vIndex, int mask, float tif, float[] lower, float[] higher)
        {
            int r0 = ((int)vIndex) & mask;
            int r1 = (r0 + 1) & mask;
            float f = vIndex - (float)r0;
            float sh = higher[r0] + f * (higher[r1] - higher[r0]);
            float sl = lower[r0] + f * (lower[r1] - lower[r0]);
            return sh + tif * (sl - sh);
        }

        static float DoInterpolation(double virtualReadIndex, float incr, int mask, float tif, float[] lower, float[] higher)
        {
            double sampleLower = 0, sampleHigher = 0;
            uint readIndex0 = (uint)virtualReadIndex;
            if (incr >= kInterpolate2Point)
            {
                int r0 = (int)(readIndex0 & (uint)mask);
                int r2 = (int)((readIndex0 + 1) & (uint)mask);
                double f = (float)virtualReadIndex - r0;
                sampleHigher = (1 - f) * higher[r0] + f * higher[r2];
                sampleLower = (1 - f) * lower[r0] + f * lower[r2];
            }
            else if (incr >= kInterpolate3Point)
            {
                double t = virtualReadIndex - readIndex0;
                double a0 = 0.5 * t * (t - 1);
                double a1 = 1 - t * t;
                double a2 = 0.5 * t * (t + 1);
                int i0 = (int)((readIndex0 - 1) & (uint)mask), i1 = (int)(readIndex0 & (uint)mask), i2 = (int)((readIndex0 + 1) & (uint)mask);
                sampleLower = a0 * lower[i0] + a1 * lower[i1] + a2 * lower[i2];
                sampleHigher = a0 * higher[i0] + a1 * higher[i1] + a2 * higher[i2];
            }
            else
            {
                double t = virtualReadIndex - readIndex0;
                double t2 = t * t;
                double a0 = t * (t2 - 1) * (t - 2) / 24;
                double a1 = -t * (t - 1) * (t2 - 4) / 6;
                double a2 = (t2 - 1) * (t2 - 4) / 4;
                double a3 = -t * (t + 1) * (t2 - 4) / 6;
                double a4 = t * (t2 - 1) * (t + 2) / 24;
                int i0 = (int)((readIndex0 - 2) & (uint)mask), i1 = (int)((readIndex0 - 1) & (uint)mask), i2 = (int)(readIndex0 & (uint)mask);
                int i3 = (int)((readIndex0 + 1) & (uint)mask), i4 = (int)((readIndex0 + 2) & (uint)mask);
                sampleLower = a0 * lower[i0] + a1 * lower[i1] + a2 * lower[i2] + a3 * lower[i3] + a4 * lower[i4];
                sampleHigher = a0 * higher[i0] + a1 * higher[i1] + a2 * higher[i2] + a3 * higher[i3] + a4 * higher[i4];
            }
            return (float)((1 - tif) * sampleHigher + tif * sampleLower);
        }
    }

    // ======================================================================

    public sealed class AudioBuffer
    {
        public readonly int numberOfChannels;
        public readonly int length;
        public readonly float sampleRate;
        readonly float[][] channels;

        public AudioBuffer(int numberOfChannels, int length, double sampleRate)
        {
            if (numberOfChannels < 1 || length < 1) throw new ArgumentOutOfRangeException("numberOfChannels/length");
            this.numberOfChannels = numberOfChannels;
            this.length = length;
            this.sampleRate = (float)sampleRate;
            channels = new float[numberOfChannels][];
            for (int i = 0; i < numberOfChannels; i++) channels[i] = new float[length];
        }

        public double duration { get { return length / (double)sampleRate; } }

        /// <summary>Write it before handing the buffer to a source; the audio thread reads it.</summary>
        public float[] getChannelData(int channel) { return channels[channel]; }
    }

    // ======================================================================

    public sealed class AudioBufferSourceNode : AudioScheduledSourceNode
    {
        const double kMaxRate = 1024;

        public readonly AudioParam playbackRate;
        public readonly AudioParam detune;

        AudioBuffer _buffer;
        volatile bool _loop;
        double _loopStart, _loopEnd;

        // audio thread
        AudioBuffer abuf;
        bool loopA;
        double loopStartA, loopEndA;
        bool isGrain;
        double grainOffset, grainDuration;
        double virtualReadIndex;

        internal AudioBufferSourceNode(AudioCtx c) : base(c)
        {
            playbackRate = new AudioParam(c, 1, -float.MaxValue, float.MaxValue, true);
            float maxDetune = 1200 * (float)(Math.Log(float.MaxValue) / Math.Log(2));
            detune = new AudioParam(c, 0, -maxDetune, maxDetune, true);
        }

        public AudioBuffer buffer
        {
            get { return _buffer; }
            set
            {
                if (_buffer != null && value != null) throw new InvalidOperationException("buffer can only be set once");
                _buffer = value;
                context.Post(() => abuf = value);
            }
        }

        public bool loop
        {
            get { return _loop; }
            set { _loop = value; bool v = value; context.Post(() => loopA = v); }
        }

        public double loopStart
        {
            get { return _loopStart; }
            set { _loopStart = value; double v = value; context.Post(() => loopStartA = v); }
        }

        public double loopEnd
        {
            get { return _loopEnd; }
            set { _loopEnd = value; double v = value; context.Post(() => loopEndA = v); }
        }

        public void start(double when = 0) { StartSource(when, 0, double.NaN, false); }
        public void start(double when, double offset) { StartSource(when, offset, double.NaN, true); }
        public void start(double when, double offset, double duration) { StartSource(when, offset, duration, true); }

        void StartSource(double when, double offset, double duration, bool grain)
        {
            if (!(offset >= 0)) throw new ArgumentOutOfRangeException("offset");
            if (!double.IsNaN(duration) && !(duration >= 0)) throw new ArgumentOutOfRangeException("duration");
            StartCommon(when, () =>
            {
                isGrain = grain;
                if (abuf != null)
                {
                    double bufferDuration = abuf.duration;
                    double go = Math.Min(Math.Max(0.0, offset), bufferDuration);
                    grainOffset = go;
                    double gd = double.IsNaN(duration) ? bufferDuration - go : duration;
                    grainDuration = Math.Max(0, gd);
                    virtualReadIndex = Math.Round(grainOffset * abuf.sampleRate, MidpointRounding.AwayFromZero);
                }
                else
                {
                    grainOffset = offset;
                    grainDuration = double.IsNaN(duration) ? 0 : duration;
                }
            });
        }

        internal override void Process(long q, long startFrame)
        {
            int frames = AudioCtx.RENDER_QUANTUM;
            // k-rate params, once per quantum
            float rate = playbackRate.ComputeK(q, startFrame);
            float det = detune.ComputeK(q, startFrame);

            int quantumFrameOffset, nonSilent;
            double startFrameOffset;
            if (!UpdateSchedulingInfo(startFrame, out quantumFrameOffset, out nonSilent, out startFrameOffset) || nonSilent == 0 || abuf == null)
            {
                Array.Clear(buf, 0, frames);
                return;
            }
            if (!RenderFromBuffer(quantumFrameOffset, nonSilent, rate, det, startFrameOffset)) Array.Clear(buf, 0, frames);
        }

        internal override long TimeToFrameRoundUp(double time, double sr)
        {
            double frame = time * sr;
            double fl = Math.Floor(frame);
            if (frame - fl <= 1e-4) return (long)fl;
            return (long)Math.Ceiling(frame);
        }

        bool RenderSilenceAndFinishIfNotLooping(int index, int framesAvailable)
        {
            if (!loopA)
            {
                if (framesAvailable > 0) Array.Clear(buf, index, framesAvailable);
                Finish();
                return true;
            }
            return false;
        }

        bool RenderFromBuffer(int destinationFrameOffset, int numberOfFrames, float rate, float det, double startFrameOffset)
        {
            var source = abuf.getChannelData(0); // the graph is mono (see file header)
            int bufferLength = abuf.length;
            double bufferSampleRate = abuf.sampleRate;
            int writeIndex = destinationFrameOffset;

            int endFrame = isGrain
                ? (int)Math.Round((grainOffset + grainDuration) * bufferSampleRate, MidpointRounding.AwayFromZero)
                : bufferLength;
            if (endFrame > bufferLength) endFrame = bufferLength;

            double virtualEndFrame = endFrame;
            double virtualDeltaFrames = endFrame;
            if (loopA && (loopStartA != 0 || loopEndA != 0) && loopStartA >= 0 && loopEndA > 0 && loopStartA < loopEndA)
            {
                double loopStartFrame = loopStartA * bufferSampleRate;
                double loopEndFrame = loopEndA * bufferSampleRate;
                virtualEndFrame = Math.Min(loopEndFrame, virtualEndFrame);
                virtualDeltaFrames = virtualEndFrame - loopStartFrame;
            }

            if (loopA && virtualReadIndex >= virtualEndFrame)
            {
                virtualReadIndex = loopStartA < 0 ? 0 : loopStartA * bufferSampleRate;
                virtualReadIndex = Math.Min(virtualReadIndex, bufferLength - 1);
            }

            // TotalPitchRate
            double sampleRateFactor = bufferSampleRate / context.sampleRate;
            double pitchRate = sampleRateFactor * rate;
            pitchRate *= Math.Pow(2, det / 1200f);
            if (!(pitchRate >= 0)) pitchRate = 0;
            if (pitchRate > kMaxRate) pitchRate = kMaxRate;

            if (pitchRate == 0 || pitchRate > virtualDeltaFrames) return false;

            double vri = virtualReadIndex;
            // a start time between frames: begin that fraction of a frame into the buffer
            if (startFrameOffset < 0) vri += -startFrameOffset * pitchRate;
            int framesToProcess = numberOfFrames;

            if (pitchRate == 1 && vri == Math.Floor(vri) && virtualDeltaFrames == Math.Floor(virtualDeltaFrames) && virtualEndFrame == Math.Floor(virtualEndFrame))
            {
                int readIndex = (int)vri;
                int deltaFrames = (int)virtualDeltaFrames;
                int endF = (int)virtualEndFrame;
                while (framesToProcess > 0)
                {
                    int framesToEnd = endF - readIndex;
                    int framesThisTime = Math.Max(0, Math.Min(framesToProcess, framesToEnd));
                    Array.Copy(source, readIndex, buf, writeIndex, framesThisTime);
                    writeIndex += framesThisTime;
                    readIndex += framesThisTime;
                    framesToProcess -= framesThisTime;
                    if (readIndex >= endF)
                    {
                        readIndex -= deltaFrames;
                        if (RenderSilenceAndFinishIfNotLooping(writeIndex, framesToProcess)) break;
                    }
                }
                vri = readIndex;
            }
            else
            {
                while (framesToProcess-- > 0)
                {
                    int readIndex = (int)vri;
                    double interpolationFactor = vri - readIndex;
                    int readIndex2 = readIndex + 1;
                    if (readIndex2 >= bufferLength)
                    {
                        if (loopA) readIndex2 = (int)(vri + 1 - virtualDeltaFrames);
                        else readIndex2 = readIndex;
                    }
                    if (readIndex >= bufferLength || readIndex2 >= bufferLength) break;

                    double sample;
                    if (readIndex == readIndex2 && readIndex >= 1)
                    {
                        double s1 = source[readIndex - 1];
                        double s2 = source[readIndex];
                        sample = s2 + (s2 - s1) * interpolationFactor;
                    }
                    else
                    {
                        double s1 = source[readIndex];
                        double s2 = source[readIndex2];
                        sample = (1.0 - interpolationFactor) * s1 + interpolationFactor * s2;
                    }
                    buf[writeIndex] = (float)sample;
                    writeIndex++;
                    vri += pitchRate;
                    if (vri >= virtualEndFrame)
                    {
                        vri -= virtualDeltaFrames;
                        if (RenderSilenceAndFinishIfNotLooping(writeIndex, framesToProcess)) break;
                    }
                }
            }
            virtualReadIndex = vri;
            return true;
        }
    }

    // ======================================================================

    /// <summary>
    /// ctx.createMediaElementSource(el). The element's audio does not flow through
    /// this graph (it is played by the host's own AudioSource, in stereo, streamed);
    /// instead this node emits a "probe" of 1.0 that every GainNode on its path
    /// multiplies, so the destination knows, sample by sample, the gain the
    /// element's signal must get. AudioCtx.ApplyMediaGain applies it to the
    /// element's samples. Only GainNodes may sit between it and the destination.
    /// </summary>
    public sealed class MediaElementAudioSourceNode : AudioNode
    {
        public readonly IMusicPlayer mediaElement;

        internal MediaElementAudioSourceNode(AudioCtx c, IMusicPlayer el) : base(c)
        {
            mediaElement = el;
            probe = new float[AudioCtx.RENDER_QUANTUM];
            for (int i = 0; i < probe.Length; i++) probe[i] = 1;
            hasProbe = true;
        }

        internal override void Process(long q, long startFrame)
        {
            // buf stays silent; probe stays 1
        }
    }
}
