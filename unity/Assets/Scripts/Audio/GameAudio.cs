// Procedural sound effects — wind bed, stone friction, footsteps, the bell —
// all synthesized with the WebAudio API. The music is the one thing that is not.
// (Port of src/game/audio.ts on top of Sisifos.Audio.AudioCtx.)

/*
 * The music.
 *
 * Four instruments were written for this hill and synthesized from oscillators:
 * a pipe, broken chords on a string, a music box, and a violin over a cello.
 * None of them held, and the ceiling was never the composition — it was that
 * everything here is built out of sawtooths and filtered noise, and past a
 * certain point that is audible no matter what it plays.
 *
 * So the score is a recording: "Inspiring Cinematic Music" by Tunetank, from
 * Pixabay (pixabay.com/music), used under the Pixabay Content License — free
 * for commercial use, no attribution required, not to be redistributed on its
 * own. The silent lead-in and tail are trimmed so it loops without a gap. The
 * synthesis stays for everything else, because a footstep and a rockfall want
 * to follow the physics frame by frame and a fixed recording cannot.
 */

using System;

namespace Sisifos.Audio
{
    public class GameAudio
    {
        /// <summary>
        /// The track, served from public/. If it is missing the game simply runs without
        /// music rather than failing — every sound effect is independent of it.
        /// PORT: the host owns the file; it hands GameAudio an IMusicPlayer instead.
        /// </summary>
        public const string MUSIC_URL = "/music.mp3";

        private AudioCtx ctx = null;
        private GainNode master = null;
        private GainNode windGain = null;
        private GainNode frictionGain = null;
        private BiquadFilterNode frictionFilter = null;
        private BiquadFilterNode windFilter = null;
        private AudioBuffer noiseBuffer = null;
        private GainNode rainGain = null;
        private double rainLevel = 0;
        private bool started = false;
        public bool muted = false;

        private GainNode musicGain = null;
        private IMusicPlayer musicEl = null;

        /// <summary>PORT: `new Audio(MUSIC_URL)`. Set by the host before start(); null = no music.</summary>
        public IMusicPlayer musicPlayer;

        /// <summary>PORT: the output rate (Unity: AudioSettings.outputSampleRate). Set before start().</summary>
        public double sampleRate = 48000;

        /// <summary>PORT: the context, once started (tests, diagnostics).</summary>
        public AudioCtx context { get { return ctx; } }

        /// <summary>PORT: the effects' audio callback (OnAudioFilterRead of the synth source).</summary>
        public void render(float[] data, int channels)
        {
            var c = ctx;
            if (c == null) { Array.Clear(data, 0, data.Length); return; }
            c.Render(data, channels);
        }

        /// <summary>PORT: the music source's audio callback: applies musicGain x master to the music samples.</summary>
        public void renderMusic(float[] data, int channels)
        {
            var c = ctx;
            if (c == null) { Array.Clear(data, 0, data.Length); return; }
            c.ApplyMediaGain(data, channels);
        }

        private AudioBuffer makeNoise(AudioCtx ctx)
        {
            var buf = ctx.createBuffer(1, (int)(ctx.sampleRate * 2), ctx.sampleRate);
            var data = buf.getChannelData(0);
            for (int i = 0; i < data.Length; i++) data[i] = (float)(Js.random() * 2 - 1);
            return buf;
        }

        public void start()
        {
            if (this.started) return;
            var ctx = new AudioCtx(sampleRate);
            this.ctx = ctx;
            this.started = true;
            this.noiseBuffer = this.makeNoise(ctx);

            this.master = ctx.createGain();
            this.master.gain.value = this.muted ? 0 : 0.9;
            this.master.connect(ctx.destination);

            // --- wind bed
            var wind = ctx.createBufferSource();
            wind.buffer = this.noiseBuffer;
            wind.loop = true;
            var windFilter = ctx.createBiquadFilter();
            windFilter.type = "bandpass";
            // lower and quieter than it was: at 420 Hz it sat right where the breath of
            // the pipe does and the two added up into hiss
            windFilter.frequency.value = 300;
            windFilter.Q.value = 0.6;
            this.windFilter = windFilter;
            this.windGain = ctx.createGain();
            this.windGain.gain.value = 0.035;
            wind.connect(windFilter).connect(this.windGain).connect(this.master);
            wind.start();

            // slow gust modulation
            var lfo = ctx.createOscillator();
            lfo.frequency.value = 0.055;
            var lfoGain = ctx.createGain();
            lfoGain.gain.value = 0.022;
            lfo.connect(lfoGain).connect(this.windGain.gain);
            lfo.start();

            // --- stone friction (gain driven by speed)
            var fric = ctx.createBufferSource();
            fric.buffer = this.noiseBuffer;
            fric.loop = true;
            this.frictionFilter = ctx.createBiquadFilter();
            this.frictionFilter.type = "lowpass";
            this.frictionFilter.frequency.value = 900;
            this.frictionGain = ctx.createGain();
            this.frictionGain.gain.value = 0;
            fric.connect(this.frictionFilter).connect(this.frictionGain).connect(this.master);
            fric.start();

            // --- rain: two bands of noise, the hiss of the drops and the patter under it
            var rain = ctx.createBufferSource();
            rain.buffer = this.noiseBuffer;
            rain.loop = true;
            rain.playbackRate.value = 0.93;
            var hiss = ctx.createBiquadFilter();
            hiss.type = "highpass";
            hiss.frequency.value = 2400;
            var patter = ctx.createBiquadFilter();
            patter.type = "bandpass";
            patter.frequency.value = 900;
            patter.Q.value = 0.8;
            this.rainGain = ctx.createGain();
            this.rainGain.gain.value = this.rainLevel;
            rain.connect(hiss).connect(this.rainGain);
            rain.connect(patter).connect(this.rainGain);
            this.rainGain.connect(this.master);
            rain.start();

            this.startMusic();
        }

        /// <summary>
        /// Hangs the recording off the same master gain as everything else, so the mute
        /// button and the tab-suspend logic keep working on it without knowing that the
        /// music stopped being synthetic. (The element streams and loops the track itself.)
        /// </summary>
        private void startMusic()
        {
            var ctx = this.ctx;
            if (ctx == null || this.master == null) return;

            this.musicGain = ctx.createGain();
            // a produced track arrives already mastered, so this sits well below the
            // level the synthesized score needed — it is a bed for the effects, not a
            // peer of them
            this.musicGain.gain.value = 0.45;
            this.musicGain.connect(this.master);

            var el = this.musicPlayer;
            // PORT: no player is the missing-file case: the load error takes the music down
            if (el == null) { this.stopMusic(); return; }
            el.loop = true;
            el.preload = "auto";
            /*
             * A load error is the only thing that takes the music down for good: the
             * file is absent or the device cannot decode it, and asking again will not
             * change either. The game then runs on its sound effects.
             */
            el.addErrorListener(() => this.stopMusic());
            this.musicEl = el;

            ctx.createMediaElementSource(el).connect(this.musicGain);
            this.playMusic();
        }

        /// <summary>
        /// Asks the element to play, and deliberately does nothing if it refuses: a
        /// refusal is recoverable, and resume() asks again on the next gesture.
        /// </summary>
        private void playMusic()
        {
            if (this.musicEl != null) this.musicEl.play(); // not started yet? resume() will ask again
        }

        private void stopMusic()
        {
            var el = this.musicEl;
            if (el != null)
            {
                el.pause();
                this.musicEl = null;
            }
            var g = this.musicGain;
            if (g != null) g.gain.setTargetAtTime(0, this.ctx != null ? this.ctx.currentTime : 0, 0.05);
        }

        /// <summary>
        /// The app went to the background (a store build): stop every sound.
        /// </summary>
        public void suspend()
        {
            if (this.musicEl != null) this.musicEl.pause();
            if (this.ctx != null) this.ctx.suspend();
        }

        /// <summary>called on every gesture that starts or returns to the game</summary>
        public void resume()
        {
            if (this.ctx != null) this.ctx.resume();
            // the element is paused by backgrounding and by a refused autoplay, and
            // this is where both recover. Muting runs on the master gain rather than on
            // the element, so a muted game still keeps the track rolling underneath and
            // unmuting lands where the music actually is rather than where it stopped.
            this.playMusic();
        }

        /// <summary>sound is actually coming out (a platform may have refused it)</summary>
        public bool playing
        {
            get { return this.ctx != null && this.ctx.state == "running" && this.musicEl != null && !this.musicEl.paused; }
        }

        public void setMuted(bool m)
        {
            this.muted = m;
            if (this.master != null && this.ctx != null)
                this.master.gain.setTargetAtTime(m ? 0 : 0.9, this.ctx.currentTime, 0.05);
        }

        /// <summary>speed: 0..1 magnitude of stone movement, rolling: falling downhill</summary>
        public void updateFriction(double speed, bool rolling)
        {
            if (this.ctx == null || this.frictionGain == null || this.frictionFilter == null) return;
            var t = this.ctx.currentTime;
            var target = Math.Min(0.35, speed * (rolling ? 0.42 : 0.2));
            this.frictionGain.gain.setTargetAtTime(target, t, 0.08);
            this.frictionFilter.frequency.setTargetAtTime(500 + speed * (rolling ? 2200 : 700), t, 0.1);
        }

        public void step()
        {
            var ctx = this.ctx;
            if (ctx == null || this.master == null || this.noiseBuffer == null) return;
            var src = ctx.createBufferSource();
            src.buffer = this.noiseBuffer;
            src.playbackRate.value = 0.8 + Js.random() * 0.4;
            var filt = ctx.createBiquadFilter();
            filt.type = "bandpass";
            filt.frequency.value = 180 + Js.random() * 120;
            filt.Q.value = 1.2;
            var g = ctx.createGain();
            var t = ctx.currentTime;
            g.gain.setValueAtTime(0.0001, t);
            g.gain.exponentialRampToValueAtTime(0.22, t + 0.01);
            g.gain.exponentialRampToValueAtTime(0.0001, t + 0.16);
            src.connect(filt).connect(g).connect(this.master);
            src.start(t);
            src.stop(t + 0.2);
        }

        /// <summary>low straining grunt while pushing the boulder</summary>
        public void strain(bool intense = false)
        {
            var ctx = this.ctx;
            if (ctx == null || this.master == null) return;
            var t = ctx.currentTime;
            var dur = intense ? 0.3 : 0.22;
            double @base = intense ? 130 : 105;
            var osc = ctx.createOscillator();
            osc.type = "sawtooth";
            osc.frequency.setValueAtTime(@base + Js.random() * 12, t);
            osc.frequency.linearRampToValueAtTime(@base * 0.78, t + dur);
            var filt = ctx.createBiquadFilter();
            filt.type = "bandpass";
            filt.frequency.value = 260 + Js.random() * 60;
            filt.Q.value = 2.2;
            var g = ctx.createGain();
            var peak = intense ? 0.16 : 0.1;
            g.gain.setValueAtTime(0.0001, t);
            g.gain.exponentialRampToValueAtTime(peak, t + 0.025);
            g.gain.exponentialRampToValueAtTime(0.0001, t + dur);
            osc.connect(filt).connect(g).connect(this.master);
            osc.start(t);
            osc.stop(t + dur + 0.05);
        }

        static readonly double[] heaveSteps = { 1, 9.0 / 8, 5.0 / 4, 3.0 / 2, 5.0 / 3, 2 };

        /// <summary>
        /// A heave landed on the beat. A short plucked fifth whose brightness says how
        /// well it was timed — the ear learns the window faster than the eye does.
        /// quality: "perfect" | "good" | "miss"
        /// </summary>
        public void heave(string quality, int streak = 0)
        {
            var ctx = this.ctx;
            if (ctx == null || this.master == null) return;
            if (quality == "miss")
            {
                this.strain(false);
                return;
            }
            var t = ctx.currentTime;
            // a streak climbs a pentatonic step per link, so a good run sounds like one
            var steps = heaveSteps;
            var lift = quality == "perfect" ? steps[Math.Min(steps.Length - 1, Math.Max(0, streak - 1))] : 1;
            var root = (quality == "perfect" ? 293.66 : 220) * lift; // D4 / A3
            var notes = quality == "perfect" ? new[] { root, root * 1.5, root * 2 } : new[] { root, root * 1.5 };
            for (int i = 0; i < notes.Length; i++)
            {
                var f = notes[i];
                var o = ctx.createOscillator();
                o.type = "triangle";
                o.frequency.value = f;
                var g = ctx.createGain();
                var peak = (quality == "perfect" ? 0.07 : 0.045) / (i + 1);
                g.gain.setValueAtTime(0.0001, t);
                g.gain.exponentialRampToValueAtTime(peak, t + 0.008);
                g.gain.exponentialRampToValueAtTime(0.0001, t + 0.45);
                o.connect(g).connect(this.master);
                o.start(t);
                o.stop(t + 0.5);
            }
        }

        /// <summary>out of breath: a ragged exhale as the grip goes</summary>
        public void gasp()
        {
            var ctx = this.ctx;
            if (ctx == null || this.master == null || this.noiseBuffer == null) return;
            var t = ctx.currentTime;
            var src = ctx.createBufferSource();
            src.buffer = this.noiseBuffer;
            var filt = ctx.createBiquadFilter();
            filt.type = "bandpass";
            filt.frequency.setValueAtTime(1400, t);
            filt.frequency.exponentialRampToValueAtTime(500, t + 0.7);
            filt.Q.value = 0.9;
            var g = ctx.createGain();
            g.gain.setValueAtTime(0.0001, t);
            g.gain.exponentialRampToValueAtTime(0.2, t + 0.06);
            g.gain.exponentialRampToValueAtTime(0.0001, t + 0.75);
            src.connect(filt).connect(g).connect(this.master);
            src.start(t);
            src.stop(t + 0.8);
            this.strain(true);
        }

        static readonly double[] checkpointFreqs = { 440, 660, 880 };

        /// <summary>a ledge is crossed: one clear, high bell — the hill concedes a little</summary>
        public void checkpoint()
        {
            var ctx = this.ctx;
            if (ctx == null || this.master == null) return;
            var t = ctx.currentTime;
            for (int i = 0; i < checkpointFreqs.Length; i++)
            {
                var f = checkpointFreqs[i];
                var o = ctx.createOscillator();
                o.type = "sine";
                o.frequency.value = f;
                var g = ctx.createGain();
                g.gain.setValueAtTime(0.0001, t + i * 0.06);
                g.gain.exponentialRampToValueAtTime(0.06 / (i + 1), t + i * 0.06 + 0.01);
                g.gain.exponentialRampToValueAtTime(0.0001, t + i * 0.06 + 1.6);
                o.connect(g).connect(this.master);
                o.start(t + i * 0.06);
                o.stop(t + i * 0.06 + 1.7);
            }
        }

        /// <summary>rain on the hill: 0 (dry) to 1 (a downpour)</summary>
        public void setRain(double level)
        {
            this.rainLevel = level * 0.09;
            var ctx = this.ctx;
            if (ctx != null && this.rainGain != null)
                this.rainGain.gain.setTargetAtTime(this.rainLevel, ctx.currentTime, 0.8);
        }

        /// <summary>
        /// The wind, following what the grass is doing on screen: when a gust leans
        /// it over, the wind gets louder and brighter. `g` is 0 (calm) to 1.
        /// </summary>
        public void setWind(double g)
        {
            var ctx = this.ctx;
            if (ctx == null || this.windGain == null || this.windFilter == null) return;
            var t = ctx.currentTime;
            this.windGain.gain.setTargetAtTime(0.03 + g * 0.1, t, 0.4);
            this.windFilter.frequency.setTargetAtTime(260 + g * 520, t, 0.5);
        }

        /// <summary>
        /// One breath, in or out. Filtered noise shaped like air through a mouth:
        /// the in-breath short, higher and sharper, the out-breath longer and lower.
        /// `force` is how hard he is working, 0..1.
        /// </summary>
        public void breathe(bool inhale, double force)
        {
            var ctx = this.ctx;
            if (ctx == null || this.master == null || this.noiseBuffer == null) return;
            var t = ctx.currentTime;
            var dur = inhale ? 0.22 + (1 - force) * 0.18 : 0.32 + (1 - force) * 0.25;
            var src = ctx.createBufferSource();
            src.buffer = this.noiseBuffer;
            src.playbackRate.value = 0.9 + Js.random() * 0.2;
            var f = ctx.createBiquadFilter();
            f.type = "bandpass";
            f.frequency.setValueAtTime(inhale ? 1500 : 900, t);
            f.frequency.linearRampToValueAtTime(inhale ? 1900 : 650, t + dur);
            f.Q.value = inhale ? 1.4 : 0.9;
            // a voiced edge on hard out-breaths: the grunt in the exhale
            var g = ctx.createGain();
            var peak = (inhale ? 0.035 : 0.05) * (0.4 + force * 0.8);
            g.gain.setValueAtTime(0.0001, t);
            g.gain.exponentialRampToValueAtTime(peak, t + dur * 0.3);
            g.gain.exponentialRampToValueAtTime(0.0001, t + dur);
            src.connect(f).connect(g).connect(this.master);
            src.start(t);
            src.stop(t + dur + 0.05);
            if (!inhale && force > 0.7)
            {
                var o = ctx.createOscillator();
                o.type = "sawtooth";
                o.frequency.setValueAtTime(110 + Js.random() * 15, t);
                o.frequency.linearRampToValueAtTime(90, t + dur * 0.6);
                var of = ctx.createBiquadFilter();
                of.type = "lowpass";
                of.frequency.value = 500;
                var og = ctx.createGain();
                og.gain.setValueAtTime(0.0001, t);
                og.gain.exponentialRampToValueAtTime(0.025 * force, t + 0.04);
                og.gain.exponentialRampToValueAtTime(0.0001, t + dur * 0.6);
                o.connect(of).connect(og).connect(this.master);
                o.start(t);
                o.stop(t + dur);
            }
        }

        /// <summary>a grain of gravel cracking under the stone</summary>
        public void grit(double force)
        {
            var ctx = this.ctx;
            if (ctx == null || this.master == null || this.noiseBuffer == null) return;
            var t = ctx.currentTime;
            var dur = 0.015 + Js.random() * 0.03;
            var src = ctx.createBufferSource();
            src.buffer = this.noiseBuffer;
            src.playbackRate.value = 0.6 + Js.random() * 0.8;
            var f = ctx.createBiquadFilter();
            f.type = "bandpass";
            f.frequency.value = 1400 + Js.random() * 2600;
            f.Q.value = 2.5;
            var g = ctx.createGain();
            g.gain.setValueAtTime(0.0001, t);
            g.gain.exponentialRampToValueAtTime(0.03 + force * 0.07 * Js.random(), t + 0.003);
            g.gain.exponentialRampToValueAtTime(0.0001, t + dur);
            src.connect(f).connect(g).connect(this.master);
            src.start(t, Js.random() * 1.5);
            src.stop(t + dur + 0.02);
        }

        /// <summary>ambient bird chirp</summary>
        public void chirp(double mult = 1, bool @double = false)
        {
            var ctx = this.ctx;
            if (ctx == null || this.master == null) return;
            var master = this.master;
            var t = ctx.currentTime;
            void pip(double start, double f0, double f1, double dur)
            {
                var o = ctx.createOscillator();
                o.type = "sine";
                o.frequency.setValueAtTime(f0, t + start);
                o.frequency.exponentialRampToValueAtTime(f1, t + start + dur);
                var g = ctx.createGain();
                g.gain.setValueAtTime(0.0001, t + start);
                g.gain.exponentialRampToValueAtTime(0.045, t + start + 0.012);
                g.gain.exponentialRampToValueAtTime(0.0001, t + start + dur);
                o.connect(g).connect(master);
                o.start(t + start);
                o.stop(t + start + dur + 0.02);
            }
            var @base = 2200 + Js.random() * 900;
            pip(0, @base * mult, @base * 0.8 * mult, 0.09);
            pip(0.13, @base * 1.25 * mult, @base * 0.9 * mult, 0.12);
            if (@double) pip(0.28, @base * 0.9 * mult, @base * 1.15 * mult, 0.1);
        }

        static readonly double[] tollFreqs = { 55, 82.41, 110 };

        /// <summary>low toll for the summit cinematic: A1–E2–A2, an open fifth and its octave</summary>
        public void toll()
        {
            var ctx = this.ctx;
            if (ctx == null || this.master == null) return;
            var t = ctx.currentTime;
            for (int i = 0; i < tollFreqs.Length; i++)
            {
                var f = tollFreqs[i];
                var osc = ctx.createOscillator();
                osc.type = "sine";
                osc.frequency.value = f;
                var g = ctx.createGain();
                g.gain.setValueAtTime(0.0001, t);
                g.gain.exponentialRampToValueAtTime(0.18 / (i + 1), t + 0.06);
                g.gain.exponentialRampToValueAtTime(0.0001, t + 3.2);
                osc.connect(g).connect(this.master);
                osc.start(t);
                osc.stop(t + 3.4);
            }
        }

        static readonly double[] swellFreqs = { 73.42, 146.83, 220, 293.66, 369.99, 440 };
        static readonly double[] swellDetunes = { -4, 4 };

        /// <summary>
        /// The ending: the music lifts, and under it a slow chord opens — D major,
        /// low strings imagined as sines and soft triangles through a warm filter —
        /// swelling over seven seconds and fading over seven more.
        /// </summary>
        public void swell()
        {
            var ctx = this.ctx;
            if (ctx == null || this.master == null) return;
            var t = ctx.currentTime;
            if (this.musicGain != null)
            {
                this.musicGain.gain.setTargetAtTime(0.75, t, 1.5);
                this.musicGain.gain.setTargetAtTime(0.45, t + 12, 3);
            }
            var lp = ctx.createBiquadFilter();
            lp.type = "lowpass";
            lp.frequency.value = 1400;
            lp.connect(this.master);
            for (int i = 0; i < swellFreqs.Length; i++)
            {
                var f = swellFreqs[i];
                foreach (var det in swellDetunes)
                {
                    var osc = ctx.createOscillator();
                    osc.type = i < 2 ? "sine" : "triangle";
                    osc.frequency.value = f;
                    osc.detune.value = det;
                    var g = ctx.createGain();
                    var peak = (i < 2 ? 0.05 : 0.026) / (1 + i * 0.15);
                    g.gain.setValueAtTime(0.0001, t);
                    g.gain.exponentialRampToValueAtTime(peak, t + 6 + i * 0.4);
                    g.gain.setValueAtTime(peak, t + 8);
                    g.gain.exponentialRampToValueAtTime(0.0001, t + 16);
                    osc.connect(g).connect(lp);
                    osc.start(t + i * 0.35);
                    osc.stop(t + 16.5);
                }
            }
        }

        /// <summary>solid thud when the character kicks the boulder loose</summary>
        public void kick()
        {
            var ctx = this.ctx;
            if (ctx == null || this.master == null || this.noiseBuffer == null) return;
            var t = ctx.currentTime;
            // low boom from the impact
            var o = ctx.createOscillator();
            o.type = "sine";
            o.frequency.setValueAtTime(170, t);
            o.frequency.exponentialRampToValueAtTime(42, t + 0.26);
            var og = ctx.createGain();
            og.gain.setValueAtTime(0.5, t);
            og.gain.exponentialRampToValueAtTime(0.0001, t + 0.3);
            o.connect(og).connect(this.master);
            o.start(t);
            o.stop(t + 0.32);
            // gritty slap of rock breaking loose
            var src = ctx.createBufferSource();
            src.buffer = this.noiseBuffer;
            src.playbackRate.value = 0.7;
            var filt = ctx.createBiquadFilter();
            filt.type = "bandpass";
            filt.frequency.setValueAtTime(900, t);
            filt.frequency.exponentialRampToValueAtTime(180, t + 0.25);
            var g = ctx.createGain();
            g.gain.setValueAtTime(0.0001, t);
            g.gain.exponentialRampToValueAtTime(0.35, t + 0.02);
            g.gain.exponentialRampToValueAtTime(0.0001, t + 0.28);
            src.connect(filt).connect(g).connect(this.master);
            src.start(t);
            src.stop(t + 0.3);
        }

        public void impact()
        {
            var ctx = this.ctx;
            if (ctx == null || this.master == null || this.noiseBuffer == null) return;
            var t = ctx.currentTime;
            var src = ctx.createBufferSource();
            src.buffer = this.noiseBuffer;
            var filt = ctx.createBiquadFilter();
            filt.type = "lowpass";
            filt.frequency.setValueAtTime(1400, t);
            filt.frequency.exponentialRampToValueAtTime(120, t + 0.6);
            var g = ctx.createGain();
            g.gain.setValueAtTime(0.5, t);
            g.gain.exponentialRampToValueAtTime(0.0001, t + 0.8);
            src.connect(filt).connect(g).connect(this.master);
            src.start(t);
            src.stop(t + 0.9);
        }

        /// <summary>deep thunder + crackle for Zeus' lightning strike</summary>
        public void thunder()
        {
            var ctx = this.ctx;
            if (ctx == null || this.master == null || this.noiseBuffer == null) return;
            var t = ctx.currentTime;
            void mk(double start, double freq, double len)
            {
                var osc = ctx.createOscillator();
                osc.type = "sine";
                osc.frequency.setValueAtTime(freq, t + start);
                osc.frequency.exponentialRampToValueAtTime(freq * 0.45, t + start + len);
                var g = ctx.createGain();
                g.gain.setValueAtTime(0.0001, t + start);
                g.gain.exponentialRampToValueAtTime(0.55, t + start + 0.03);
                g.gain.exponentialRampToValueAtTime(0.0001, t + start + len);
                osc.connect(g).connect(this.master);
                osc.start(t + start);
                osc.stop(t + start + len + 0.05);
            }
            mk(0, 90, 2.2);
            mk(0.08, 60, 2.8);
            mk(0.14, 140, 1.4);
            var crackle = ctx.createBufferSource();
            crackle.buffer = this.noiseBuffer;
            var cp = ctx.createBiquadFilter();
            cp.type = "highpass";
            cp.frequency.value = 900;
            var cg = ctx.createGain();
            cg.gain.setValueAtTime(0.0001, t);
            cg.gain.exponentialRampToValueAtTime(0.45, t + 0.02);
            cg.gain.exponentialRampToValueAtTime(0.0001, t + 0.5);
            crackle.connect(cp).connect(cg).connect(this.master);
            crackle.start(t);
            crackle.stop(t + 0.55);
        }

        public void dispose()
        {
            this.stopMusic();
            if (this.ctx != null) this.ctx.close();
            this.ctx = null;
            this.started = false;
        }
    }
}
