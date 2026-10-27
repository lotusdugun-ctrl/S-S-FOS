// A small WebAudio AudioContext: the graph GameAudio builds, rendered in
// 128-frame quanta exactly as Chromium renders it.
//
// THREADING
//   Game thread: creates nodes, connects them, schedules automation, starts and
//   stops sources. None of these touch audio-thread state directly: each posts
//   a command (a closure) into a queue under a short lock. `currentTime` is a
//   volatile read of the rendered frame count.
//   Audio thread (Unity's OnAudioFilterRead): Render() swaps the queue out at
//   the start of every quantum, applies the commands, then pulls the graph from
//   the destination. Commands therefore take effect on a quantum boundary, as
//   in Chrome, where the main thread and the render thread also meet there.
//
// LIFETIME
//   Nothing registers nodes globally. A node is referenced by the graph only
//   through its connections. A source past its stop time is "finished"; its
//   consumers drop the connection after summing its last block. A processing
//   node whose inputs have all gone (and whose filter tail has rung out) is
//   finished in turn, so a one-shot chain unhooks itself from the master gain
//   and becomes garbage. `liveNodeCount` is the number of nodes processed in
//   the last quantum.
//
// MUSIC
//   See MediaElementAudioSourceNode: the music's per-sample gain (music gain x
//   master gain, with all their automation) is computed by the graph as a probe
//   signal and kept in a ring beside the effects' output. The host's music
//   AudioSource calls ApplyMediaGain from its own OnAudioFilterRead; both
//   consumers read the same frame range in a given audio tick, whichever runs
//   first renders it.

using System;
using System.Collections.Generic;
using System.Threading;

namespace Sisifos.Audio
{
    public sealed class AudioCtx
    {
        public const int RENDER_QUANTUM = 128;
        const int RING = 1 << 16;
        const int MASK = RING - 1;

        public readonly double sampleRate;
        public readonly AudioDestinationNode destination;

        long frame; // frames rendered; audio thread writes
        volatile string _state = "running";

        // command queue
        readonly object qLock = new object();
        List<Action> pending = new List<Action>();
        List<Action> running = new List<Action>();

        // audio thread
        readonly object renderLock = new object();
        readonly float[] ring = new float[RING];
        readonly float[] probeRing = new float[RING];
        long quantumIndex;
        internal long quantumStartFrame;
        internal int nodesThisQuantum;
        volatile int _liveNodeCount;
        long sfxPos, mediaPos;
        float lastProbe = 1;

        public AudioCtx(double sampleRate)
        {
            this.sampleRate = (float)sampleRate;
            destination = new AudioDestinationNode(this);
        }

        public double currentTime { get { return Volatile.Read(ref frame) / sampleRate; } }

        /// <summary>"running", "suspended" or "closed"</summary>
        public string state { get { return _state; } }

        /// <summary>Nodes processed in the last rendered quantum (graph size; for leak checks).</summary>
        public int liveNodeCount { get { return _liveNodeCount; } }

        /// <summary>Commands posted by the game thread and not yet applied.</summary>
        public int pendingCommandCount { get { lock (qLock) return pending.Count; } }

        public void suspend() { if (_state != "closed") _state = "suspended"; }
        public void resume() { if (_state != "closed") _state = "running"; }
        public void close() { _state = "closed"; }

        internal void Post(Action a)
        {
            lock (qLock) pending.Add(a);
        }

        // ---- factories (game thread)

        public GainNode createGain() { return new GainNode(this); }
        public OscillatorNode createOscillator() { return new OscillatorNode(this); }
        public BiquadFilterNode createBiquadFilter() { return new BiquadFilterNode(this); }
        public AudioBufferSourceNode createBufferSource() { return new AudioBufferSourceNode(this); }
        public AudioBuffer createBuffer(int numberOfChannels, int length, double sampleRate) { return new AudioBuffer(numberOfChannels, length, sampleRate); }
        public MediaElementAudioSourceNode createMediaElementSource(IMusicPlayer el) { return new MediaElementAudioSourceNode(this, el); }
        public PeriodicWave createPeriodicWave(float[] real, float[] imag, bool disableNormalization = false)
        {
            return new PeriodicWave((float)sampleRate, real, imag, disableNormalization);
        }

        // ---- audio thread

        void DrainCommands()
        {
            List<Action> list;
            lock (qLock)
            {
                list = pending;
                pending = running;
                running = list;
            }
            for (int i = 0; i < list.Count; i++) list[i]();
            list.Clear();
        }

        void RenderQuantum()
        {
            DrainCommands();
            long q = quantumIndex++;
            quantumStartFrame = frame;
            nodesThisQuantum = 0;
            destination.Pull(q);
            int at = (int)(frame & MASK);
            var b = destination.buf;
            bool hp = destination.hasProbe;
            var p = destination.probe;
            for (int k = 0; k < RENDER_QUANTUM; k++)
            {
                ring[at + k] = b[k];
                probeRing[at + k] = hp ? p[k] : 0;
            }
            _liveNodeCount = nodesThisQuantum;
            Volatile.Write(ref frame, frame + RENDER_QUANTUM);
        }

        void EnsureRendered(long upTo)
        {
            while (frame < upTo) RenderQuantum();
        }

        /// <summary>
        /// Pulls the next block of the graph's output into `interleaved` (overwriting it).
        /// The graph is mono; it goes to channel 0 and, for 2+ channels, to the first two
        /// (WebAudio's mono→stereo up-mix). Call from the audio thread.
        /// </summary>
        public void Render(float[] interleaved, int channels)
        {
            int n = interleaved.Length / channels;
            lock (renderLock)
            {
                if (_state != "running")
                {
                    DrainCommands();
                    Array.Clear(interleaved, 0, interleaved.Length);
                    return;
                }
                if (sfxPos < mediaPos - n) sfxPos = mediaPos - n;
                if (sfxPos < frame - RING / 2) sfxPos = frame;
                EnsureRendered(sfxPos + n);
                for (int f = 0; f < n; f++)
                {
                    float s = ring[(int)((sfxPos + f) & MASK)];
                    int o = f * channels;
                    interleaved[o] = s;
                    if (channels >= 2) interleaved[o + 1] = s;
                    for (int c = 2; c < channels; c++) interleaved[o + c] = 0;
                }
                sfxPos += n;
            }
        }

        /// <summary>
        /// Multiplies the media element's own samples (`interleaved`, any channel count)
        /// by the gain the graph gives them, frame-aligned with Render. Silence while
        /// the context is not running. Call from the music source's audio callback.
        /// </summary>
        public void ApplyMediaGain(float[] interleaved, int channels)
        {
            int n = interleaved.Length / channels;
            lock (renderLock)
            {
                if (_state != "running")
                {
                    Array.Clear(interleaved, 0, interleaved.Length);
                    return;
                }
                // a paused music source misses ticks: rejoin the effects' clock
                if (mediaPos < sfxPos - n) mediaPos = sfxPos - n;
                if (mediaPos > sfxPos + n) mediaPos = sfxPos;
                EnsureRendered(mediaPos + n);
                for (int f = 0; f < n; f++)
                {
                    float g = probeRing[(int)((mediaPos + f) & MASK)];
                    int o = f * channels;
                    for (int c = 0; c < channels; c++) interleaved[o + c] *= g;
                    lastProbe = g;
                }
                mediaPos += n;
            }
        }
    }
}
