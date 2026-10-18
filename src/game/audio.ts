// Procedural sound effects — wind bed, stone friction, footsteps, the bell —
// all synthesized with the WebAudio API. The music is the one thing that is not.

/*
 * The music.
 *
 * Four instruments were written for this hill and synthesized from oscillators:
 * a pipe, broken chords on a string, a music box, and a violin over a cello.
 * None of them held, and the ceiling was never the composition — it was that
 * everything here is built out of sawtooths and filtered noise, and past a
 * certain point that is audible no matter what it plays.
 *
 * So the score is a recording: an orchestral piece made for this game (a
 * generated track — check that its licence covers commercial use before
 * shipping to a store). The synthesis stays for everything else, because a
 * footstep and a rockfall want to follow the physics
 * frame by frame and a fixed recording cannot.
 */

/**
 * The track, served from public/. If it is missing the game simply runs without
 * music rather than failing — every sound effect is independent of it.
 */
const MUSIC_URL = "/music.mp3";

export class GameAudio {
  private ctx: AudioContext | null = null;
  private master: GainNode | null = null;
  private windGain: GainNode | null = null;
  private frictionGain: GainNode | null = null;
  private frictionFilter: BiquadFilterNode | null = null;
  private windFilter: BiquadFilterNode | null = null;
  private noiseBuffer: AudioBuffer | null = null;
  private started = false;
  muted = false;

  private musicGain: GainNode | null = null;
  private musicEl: HTMLAudioElement | null = null;

  private makeNoise(ctx: AudioContext) {
    const buf = ctx.createBuffer(1, ctx.sampleRate * 2, ctx.sampleRate);
    const data = buf.getChannelData(0);
    for (let i = 0; i < data.length; i++) data[i] = Math.random() * 2 - 1;
    return buf;
  }

  start() {
    if (this.started) return;
    const Ctor =
      window.AudioContext ||
      (window as unknown as { webkitAudioContext: typeof AudioContext }).webkitAudioContext;
    if (!Ctor) return;
    const ctx = new Ctor();
    this.ctx = ctx;
    this.started = true;
    this.noiseBuffer = this.makeNoise(ctx);

    this.master = ctx.createGain();
    this.master.gain.value = this.muted ? 0 : 0.9;
    this.master.connect(ctx.destination);

    // --- wind bed
    const wind = ctx.createBufferSource();
    wind.buffer = this.noiseBuffer;
    wind.loop = true;
    const windFilter = ctx.createBiquadFilter();
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
    const lfo = ctx.createOscillator();
    lfo.frequency.value = 0.055;
    const lfoGain = ctx.createGain();
    lfoGain.gain.value = 0.022;
    lfo.connect(lfoGain).connect(this.windGain.gain);
    lfo.start();

    // --- stone friction (gain driven by speed)
    const fric = ctx.createBufferSource();
    fric.buffer = this.noiseBuffer;
    fric.loop = true;
    this.frictionFilter = ctx.createBiquadFilter();
    this.frictionFilter.type = "lowpass";
    this.frictionFilter.frequency.value = 900;
    this.frictionGain = ctx.createGain();
    this.frictionGain.gain.value = 0;
    fric.connect(this.frictionFilter).connect(this.frictionGain).connect(this.master);
    fric.start();

    this.startMusic();
  }

  /**
   * Hangs the recording off the same master gain as everything else, so the mute
   * button and the tab-suspend logic keep working on it without knowing that the
   * music stopped being synthetic.
   *
   * A MediaElementSource rather than a decoded buffer: the track is three and a
   * half minutes, and decoding it up front would mean holding tens of megabytes
   * of PCM and a delay on the first note. An element streams it and loops it
   * itself.
   */
  private startMusic() {
    const ctx = this.ctx;
    if (!ctx || !this.master) return;

    this.musicGain = ctx.createGain();
    // a produced track arrives already mastered, so this sits well below the
    // level the synthesized score needed — it is a bed for the effects, not a
    // peer of them
    this.musicGain.gain.value = 0.45;
    this.musicGain.connect(this.master);

    const el = new Audio(MUSIC_URL);
    el.loop = true;
    el.preload = "auto";
    /*
     * A load error is the only thing that takes the music down for good: the
     * file is absent or the browser cannot decode it, and asking again will not
     * change either. The game then runs on its sound effects, which are
     * independent of this.
     */
    el.addEventListener("error", () => this.stopMusic());
    this.musicEl = el;

    ctx.createMediaElementSource(el).connect(this.musicGain);
    this.playMusic();
  }

  /**
   * Asks the element to play, and deliberately does nothing if it refuses.
   *
   * A refusal here is almost never fatal. The autoplay policy rejects a play()
   * that the browser does not consider gestured — which can happen on the very
   * first call depending on how the click is dispatched — and a backgrounded tab
   * pauses the element on its own. Tearing the music down on either would make a
   * recoverable situation permanent, so both are left to `resume`, which runs on
   * the next gesture.
   */
  private playMusic() {
    void this.musicEl?.play().catch(() => {
      /* not started yet; resume() will ask again */
    });
  }

  private stopMusic() {
    const el = this.musicEl;
    if (el) {
      el.pause();
      this.musicEl = null;
    }
    const g = this.musicGain;
    if (g) g.gain.setTargetAtTime(0, this.ctx?.currentTime ?? 0, 0.05);
  }

  /** called on every gesture that starts or returns to the game */
  resume() {
    void this.ctx?.resume();
    // the element is paused by a backgrounded tab and by a refused autoplay, and
    // this is where both recover. Muting runs on the master gain rather than on
    // the element, so a muted game still keeps the track rolling underneath and
    // unmuting lands where the music actually is rather than where it stopped.
    this.playMusic();
  }

  setMuted(m: boolean) {
    this.muted = m;
    if (this.master && this.ctx)
      this.master.gain.setTargetAtTime(m ? 0 : 0.9, this.ctx.currentTime, 0.05);
  }

  /** speed: 0..1 magnitude of stone movement, rolling: falling downhill */
  updateFriction(speed: number, rolling: boolean) {
    if (!this.ctx || !this.frictionGain || !this.frictionFilter) return;
    const t = this.ctx.currentTime;
    const target = Math.min(0.35, speed * (rolling ? 0.42 : 0.2));
    this.frictionGain.gain.setTargetAtTime(target, t, 0.08);
    this.frictionFilter.frequency.setTargetAtTime(500 + speed * (rolling ? 2200 : 700), t, 0.1);
  }

  step() {
    const ctx = this.ctx;
    if (!ctx || !this.master || !this.noiseBuffer) return;
    const src = ctx.createBufferSource();
    src.buffer = this.noiseBuffer;
    src.playbackRate.value = 0.8 + Math.random() * 0.4;
    const filt = ctx.createBiquadFilter();
    filt.type = "bandpass";
    filt.frequency.value = 180 + Math.random() * 120;
    filt.Q.value = 1.2;
    const g = ctx.createGain();
    const t = ctx.currentTime;
    g.gain.setValueAtTime(0.0001, t);
    g.gain.exponentialRampToValueAtTime(0.22, t + 0.01);
    g.gain.exponentialRampToValueAtTime(0.0001, t + 0.16);
    src.connect(filt).connect(g).connect(this.master);
    src.start(t);
    src.stop(t + 0.2);
  }

  /** low straining grunt while pushing the boulder */
  strain(intense = false) {
    const ctx = this.ctx;
    if (!ctx || !this.master) return;
    const t = ctx.currentTime;
    const dur = intense ? 0.3 : 0.22;
    const base = intense ? 130 : 105;
    const osc = ctx.createOscillator();
    osc.type = "sawtooth";
    osc.frequency.setValueAtTime(base + Math.random() * 12, t);
    osc.frequency.linearRampToValueAtTime(base * 0.78, t + dur);
    const filt = ctx.createBiquadFilter();
    filt.type = "bandpass";
    filt.frequency.value = 260 + Math.random() * 60;
    filt.Q.value = 2.2;
    const g = ctx.createGain();
    const peak = intense ? 0.16 : 0.1;
    g.gain.setValueAtTime(0.0001, t);
    g.gain.exponentialRampToValueAtTime(peak, t + 0.025);
    g.gain.exponentialRampToValueAtTime(0.0001, t + dur);
    osc.connect(filt).connect(g).connect(this.master);
    osc.start(t);
    osc.stop(t + dur + 0.05);
  }

  /**
   * A heave landed on the beat. A short plucked fifth whose brightness says how
   * well it was timed — the ear learns the window faster than the eye does.
   */
  heave(quality: "perfect" | "good" | "miss", streak = 0) {
    const ctx = this.ctx;
    if (!ctx || !this.master) return;
    if (quality === "miss") {
      this.strain(false);
      return;
    }
    const t = ctx.currentTime;
    // a streak climbs a pentatonic step per link, so a good run sounds like one
    const steps = [1, 9 / 8, 5 / 4, 3 / 2, 5 / 3, 2];
    const lift =
      quality === "perfect" ? steps[Math.min(steps.length - 1, Math.max(0, streak - 1))]! : 1;
    const root = (quality === "perfect" ? 293.66 : 220) * lift; // D4 / A3
    const notes = quality === "perfect" ? [root, root * 1.5, root * 2] : [root, root * 1.5];
    notes.forEach((f, i) => {
      const o = ctx.createOscillator();
      o.type = "triangle";
      o.frequency.value = f;
      const g = ctx.createGain();
      const peak = (quality === "perfect" ? 0.07 : 0.045) / (i + 1);
      g.gain.setValueAtTime(0.0001, t);
      g.gain.exponentialRampToValueAtTime(peak, t + 0.008);
      g.gain.exponentialRampToValueAtTime(0.0001, t + 0.45);
      o.connect(g).connect(this.master!);
      o.start(t);
      o.stop(t + 0.5);
    });
  }

  /** out of breath: a ragged exhale as the grip goes */
  gasp() {
    const ctx = this.ctx;
    if (!ctx || !this.master || !this.noiseBuffer) return;
    const t = ctx.currentTime;
    const src = ctx.createBufferSource();
    src.buffer = this.noiseBuffer;
    const filt = ctx.createBiquadFilter();
    filt.type = "bandpass";
    filt.frequency.setValueAtTime(1400, t);
    filt.frequency.exponentialRampToValueAtTime(500, t + 0.7);
    filt.Q.value = 0.9;
    const g = ctx.createGain();
    g.gain.setValueAtTime(0.0001, t);
    g.gain.exponentialRampToValueAtTime(0.2, t + 0.06);
    g.gain.exponentialRampToValueAtTime(0.0001, t + 0.75);
    src.connect(filt).connect(g).connect(this.master);
    src.start(t);
    src.stop(t + 0.8);
    this.strain(true);
  }

  /** a ledge is crossed: one clear, high bell — the hill concedes a little */
  checkpoint() {
    const ctx = this.ctx;
    if (!ctx || !this.master) return;
    const t = ctx.currentTime;
    [440, 660, 880].forEach((f, i) => {
      const o = ctx.createOscillator();
      o.type = "sine";
      o.frequency.value = f;
      const g = ctx.createGain();
      g.gain.setValueAtTime(0.0001, t + i * 0.06);
      g.gain.exponentialRampToValueAtTime(0.06 / (i + 1), t + i * 0.06 + 0.01);
      g.gain.exponentialRampToValueAtTime(0.0001, t + i * 0.06 + 1.6);
      o.connect(g).connect(this.master!);
      o.start(t + i * 0.06);
      o.stop(t + i * 0.06 + 1.7);
    });
  }

  /**
   * The wind, following what the grass is doing on screen: when a gust leans
   * it over, the wind gets louder and brighter. `g` is 0 (calm) to 1.
   */
  setWind(g: number) {
    const ctx = this.ctx;
    if (!ctx || !this.windGain || !this.windFilter) return;
    const t = ctx.currentTime;
    this.windGain.gain.setTargetAtTime(0.03 + g * 0.1, t, 0.4);
    this.windFilter.frequency.setTargetAtTime(260 + g * 520, t, 0.5);
  }

  /**
   * One breath, in or out. Filtered noise shaped like air through a mouth:
   * the in-breath short, higher and sharper, the out-breath longer and lower.
   * `force` is how hard he is working, 0..1.
   */
  breathe(inhale: boolean, force: number) {
    const ctx = this.ctx;
    if (!ctx || !this.master || !this.noiseBuffer) return;
    const t = ctx.currentTime;
    const dur = inhale ? 0.22 + (1 - force) * 0.18 : 0.32 + (1 - force) * 0.25;
    const src = ctx.createBufferSource();
    src.buffer = this.noiseBuffer;
    src.playbackRate.value = 0.9 + Math.random() * 0.2;
    const f = ctx.createBiquadFilter();
    f.type = "bandpass";
    f.frequency.setValueAtTime(inhale ? 1500 : 900, t);
    f.frequency.linearRampToValueAtTime(inhale ? 1900 : 650, t + dur);
    f.Q.value = inhale ? 1.4 : 0.9;
    // a voiced edge on hard out-breaths: the grunt in the exhale
    const g = ctx.createGain();
    const peak = (inhale ? 0.035 : 0.05) * (0.4 + force * 0.8);
    g.gain.setValueAtTime(0.0001, t);
    g.gain.exponentialRampToValueAtTime(peak, t + dur * 0.3);
    g.gain.exponentialRampToValueAtTime(0.0001, t + dur);
    src.connect(f).connect(g).connect(this.master);
    src.start(t);
    src.stop(t + dur + 0.05);
    if (!inhale && force > 0.7) {
      const o = ctx.createOscillator();
      o.type = "sawtooth";
      o.frequency.setValueAtTime(110 + Math.random() * 15, t);
      o.frequency.linearRampToValueAtTime(90, t + dur * 0.6);
      const of = ctx.createBiquadFilter();
      of.type = "lowpass";
      of.frequency.value = 500;
      const og = ctx.createGain();
      og.gain.setValueAtTime(0.0001, t);
      og.gain.exponentialRampToValueAtTime(0.025 * force, t + 0.04);
      og.gain.exponentialRampToValueAtTime(0.0001, t + dur * 0.6);
      o.connect(of).connect(og).connect(this.master);
      o.start(t);
      o.stop(t + dur);
    }
  }

  /** a grain of gravel cracking under the stone */
  grit(force: number) {
    const ctx = this.ctx;
    if (!ctx || !this.master || !this.noiseBuffer) return;
    const t = ctx.currentTime;
    const dur = 0.015 + Math.random() * 0.03;
    const src = ctx.createBufferSource();
    src.buffer = this.noiseBuffer;
    src.playbackRate.value = 0.6 + Math.random() * 0.8;
    const f = ctx.createBiquadFilter();
    f.type = "bandpass";
    f.frequency.value = 1400 + Math.random() * 2600;
    f.Q.value = 2.5;
    const g = ctx.createGain();
    g.gain.setValueAtTime(0.0001, t);
    g.gain.exponentialRampToValueAtTime(0.03 + force * 0.07 * Math.random(), t + 0.003);
    g.gain.exponentialRampToValueAtTime(0.0001, t + dur);
    src.connect(f).connect(g).connect(this.master);
    src.start(t, Math.random() * 1.5);
    src.stop(t + dur + 0.02);
  }

  /** ambient bird chirp */
  chirp(mult = 1, double = false) {
    const ctx = this.ctx;
    if (!ctx || !this.master) return;
    const master = this.master;
    const t = ctx.currentTime;
    const pip = (start: number, f0: number, f1: number, dur: number) => {
      const o = ctx.createOscillator();
      o.type = "sine";
      o.frequency.setValueAtTime(f0, t + start);
      o.frequency.exponentialRampToValueAtTime(f1, t + start + dur);
      const g = ctx.createGain();
      g.gain.setValueAtTime(0.0001, t + start);
      g.gain.exponentialRampToValueAtTime(0.045, t + start + 0.012);
      g.gain.exponentialRampToValueAtTime(0.0001, t + start + dur);
      o.connect(g).connect(master);
      o.start(t + start);
      o.stop(t + start + dur + 0.02);
    };
    const base = 2200 + Math.random() * 900;
    pip(0, base * mult, base * 0.8 * mult, 0.09);
    pip(0.13, base * 1.25 * mult, base * 0.9 * mult, 0.12);
    if (double) pip(0.28, base * 0.9 * mult, base * 1.15 * mult, 0.1);
  }

  /** low toll for the summit cinematic: A1–E2–A2, an open fifth and its octave */
  toll() {
    const ctx = this.ctx;
    if (!ctx || !this.master) return;
    const t = ctx.currentTime;
    [55, 82.41, 110].forEach((f, i) => {
      const osc = ctx.createOscillator();
      osc.type = "sine";
      osc.frequency.value = f;
      const g = ctx.createGain();
      g.gain.setValueAtTime(0.0001, t);
      g.gain.exponentialRampToValueAtTime(0.18 / (i + 1), t + 0.06);
      g.gain.exponentialRampToValueAtTime(0.0001, t + 3.2);
      osc.connect(g).connect(this.master!);
      osc.start(t);
      osc.stop(t + 3.4);
    });
  }

  /** solid thud when the character kicks the boulder loose */
  kick() {
    const ctx = this.ctx;
    if (!ctx || !this.master || !this.noiseBuffer) return;
    const t = ctx.currentTime;
    // low boom from the impact
    const o = ctx.createOscillator();
    o.type = "sine";
    o.frequency.setValueAtTime(170, t);
    o.frequency.exponentialRampToValueAtTime(42, t + 0.26);
    const og = ctx.createGain();
    og.gain.setValueAtTime(0.5, t);
    og.gain.exponentialRampToValueAtTime(0.0001, t + 0.3);
    o.connect(og).connect(this.master);
    o.start(t);
    o.stop(t + 0.32);
    // gritty slap of rock breaking loose
    const src = ctx.createBufferSource();
    src.buffer = this.noiseBuffer;
    src.playbackRate.value = 0.7;
    const filt = ctx.createBiquadFilter();
    filt.type = "bandpass";
    filt.frequency.setValueAtTime(900, t);
    filt.frequency.exponentialRampToValueAtTime(180, t + 0.25);
    const g = ctx.createGain();
    g.gain.setValueAtTime(0.0001, t);
    g.gain.exponentialRampToValueAtTime(0.35, t + 0.02);
    g.gain.exponentialRampToValueAtTime(0.0001, t + 0.28);
    src.connect(filt).connect(g).connect(this.master);
    src.start(t);
    src.stop(t + 0.3);
  }

  impact() {
    const ctx = this.ctx;
    if (!ctx || !this.master || !this.noiseBuffer) return;
    const t = ctx.currentTime;
    const src = ctx.createBufferSource();
    src.buffer = this.noiseBuffer;
    const filt = ctx.createBiquadFilter();
    filt.type = "lowpass";
    filt.frequency.setValueAtTime(1400, t);
    filt.frequency.exponentialRampToValueAtTime(120, t + 0.6);
    const g = ctx.createGain();
    g.gain.setValueAtTime(0.5, t);
    g.gain.exponentialRampToValueAtTime(0.0001, t + 0.8);
    src.connect(filt).connect(g).connect(this.master);
    src.start(t);
    src.stop(t + 0.9);
  }

  /** deep thunder + crackle for Zeus' lightning strike */
  thunder() {
    const ctx = this.ctx;
    if (!ctx || !this.master || !this.noiseBuffer) return;
    const t = ctx.currentTime;
    const mk = (start: number, freq: number, len: number) => {
      const osc = ctx.createOscillator();
      osc.type = "sine";
      osc.frequency.setValueAtTime(freq, t + start);
      osc.frequency.exponentialRampToValueAtTime(freq * 0.45, t + start + len);
      const g = ctx.createGain();
      g.gain.setValueAtTime(0.0001, t + start);
      g.gain.exponentialRampToValueAtTime(0.55, t + start + 0.03);
      g.gain.exponentialRampToValueAtTime(0.0001, t + start + len);
      osc.connect(g).connect(this.master!);
      osc.start(t + start);
      osc.stop(t + start + len + 0.05);
    };
    mk(0, 90, 2.2);
    mk(0.08, 60, 2.8);
    mk(0.14, 140, 1.4);
    const crackle = ctx.createBufferSource();
    crackle.buffer = this.noiseBuffer;
    const cp = ctx.createBiquadFilter();
    cp.type = "highpass";
    cp.frequency.value = 900;
    const cg = ctx.createGain();
    cg.gain.setValueAtTime(0.0001, t);
    cg.gain.exponentialRampToValueAtTime(0.45, t + 0.02);
    cg.gain.exponentialRampToValueAtTime(0.0001, t + 0.5);
    crackle.connect(cp).connect(cg).connect(this.master);
    crackle.start(t);
    crackle.stop(t + 0.55);
  }

  dispose() {
    this.stopMusic();
    void this.ctx?.close();
    this.ctx = null;
    this.started = false;
  }
}
