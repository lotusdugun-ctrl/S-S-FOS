// Procedural, natural-toned audio: wind bed, stone friction, footsteps.
// No external assets; everything is synthesized with the WebAudio API.

// Ancient Greek flavoured music: a slow lyre melody in D (Dorian/Phrygian
// flavour) over a D–A drone, echoing like a mythic kithara.
const MELODY: number[] = [
  293.66, 0, 349.23, 392, 440, 392, 349.23, 329.63,
  293.66, 0, 261.63, 293.66, 329.63, 349.23, 329.63, 0,
  293.66, 220, 349.23, 440, 392, 349.23, 329.63, 293.66,
  261.63, 293.66, 329.63, 261.63, 220, 0, 293.66, 0,
];

// bass figure (D2 / A2 / G2) every other step
const BASS: Record<number, number> = {
  0: 73.42, 2: 73.42, 4: 110, 6: 110,
  8: 73.42, 10: 73.42, 12: 98, 14: 98,
  16: 73.42, 18: 73.42, 20: 110, 22: 110,
  24: 73.42, 26: 73.42, 28: 98, 30: 98,
};

export class GameAudio {
  private ctx: AudioContext | null = null;
  private master: GainNode | null = null;
  private windGain: GainNode | null = null;
  private frictionGain: GainNode | null = null;
  private frictionFilter: BiquadFilterNode | null = null;
  private noiseBuffer: AudioBuffer | null = null;
  private started = false;
  muted = false;

  private musicGain: GainNode | null = null;
  private musicTimer: number | null = null;
  private drone: OscillatorNode[] = [];
  private musicStep = 0;
  private nextNoteTime = 0;

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
      (window as unknown as { webkitAudioContext: typeof AudioContext })
        .webkitAudioContext;
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
    windFilter.frequency.value = 420;
    windFilter.Q.value = 0.6;
    this.windGain = ctx.createGain();
    this.windGain.gain.value = 0.05;
    wind.connect(windFilter).connect(this.windGain).connect(this.master);
    wind.start();

    // slow gust modulation
    const lfo = ctx.createOscillator();
    lfo.frequency.value = 0.07;
    const lfoGain = ctx.createGain();
    lfoGain.gain.value = 0.035;
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
    fric
      .connect(this.frictionFilter)
      .connect(this.frictionGain)
      .connect(this.master);
    fric.start();

    this.startMusic();
  }

  /** soft echo bus so the lyre rings out into the dusk */
  private startMusic() {
    const ctx = this.ctx;
    if (!ctx || !this.master) return;

    this.musicGain = ctx.createGain();
    this.musicGain.gain.value = 0.32;
    this.musicGain.connect(this.master);

    // echo (delay + feedback)
    const delay = ctx.createDelay(1.5);
    delay.delayTime.value = 0.4;
    const fb = ctx.createGain();
    fb.gain.value = 0.32;
    const wet = ctx.createGain();
    wet.gain.value = 0.3;
    this.musicGain.connect(delay);
    delay.connect(fb).connect(delay);
    delay.connect(wet).connect(this.master);

    // D2–A2 drone, breathing slowly
    const dg = ctx.createGain();
    dg.gain.value = 0.05;
    const d1 = ctx.createOscillator();
    d1.type = "sine";
    d1.frequency.value = 73.42;
    const d2 = ctx.createOscillator();
    d2.type = "sine";
    d2.frequency.value = 110;
    d1.connect(dg);
    d2.connect(dg);
    dg.connect(this.musicGain);
    d1.start();
    d2.start();
    this.drone = [d1, d2];

    const lfo = ctx.createOscillator();
    lfo.frequency.value = 0.08;
    const lg = ctx.createGain();
    lg.gain.value = 0.02;
    lfo.connect(lg).connect(dg.gain);
    lfo.start();

    this.musicStep = 0;
    this.nextNoteTime = ctx.currentTime + 0.15;
    this.musicTimer = window.setInterval(() => this.scheduleMusic(), 120);
  }

  private scheduleMusic() {
    const ctx = this.ctx;
    if (!ctx || !this.musicGain) return;
    const spb = 0.4; // eighth note at ~75 bpm
    while (this.nextNoteTime < ctx.currentTime + 0.4) {
      this.playStep(this.musicStep, this.nextNoteTime, spb);
      this.nextNoteTime += spb;
      this.musicStep = (this.musicStep + 1) % MELODY.length;
    }
  }

  private playStep(i: number, when: number, spb: number) {
    const m = MELODY[i]!;
    if (m > 0) this.pluck(m, when, spb * 1.8, 0.16, "triangle");
    const b = BASS[i] ?? 0;
    if (b > 0) this.pluck(b, when, spb * 4.5, 0.2, "sine");
  }

  /** plucked-string synthesis */
  private pluck(
    freq: number,
    when: number,
    dur: number,
    vel: number,
    type: OscillatorType,
  ) {
    const ctx = this.ctx;
    if (!ctx || !this.musicGain) return;
    const g = ctx.createGain();
    g.gain.setValueAtTime(0.0001, when);
    g.gain.exponentialRampToValueAtTime(vel, when + 0.008);
    g.gain.exponentialRampToValueAtTime(0.0001, when + dur);
    const filt = ctx.createBiquadFilter();
    filt.type = "lowpass";
    filt.frequency.value = Math.min(6500, freq * 6);
    filt.Q.value = 1.1;
    const o = ctx.createOscillator();
    o.type = type;
    o.frequency.value = freq;
    const o2 = ctx.createOscillator();
    o2.type = "sine";
    o2.frequency.value = freq * 2;
    const g2 = ctx.createGain();
    g2.gain.value = 0.22;
    o.connect(filt);
    o2.connect(g2).connect(filt);
    filt.connect(g).connect(this.musicGain);
    o.start(when);
    o.stop(when + dur + 0.05);
    o2.start(when);
    o2.stop(when + dur + 0.05);
  }

  private stopMusic() {
    if (this.musicTimer !== null) {
      clearInterval(this.musicTimer);
      this.musicTimer = null;
    }
    this.drone.forEach((o) => {
      try {
        o.stop();
      } catch {
        /* already stopped */
      }
    });
    this.drone = [];
    const g = this.musicGain;
    if (g) {
      g.gain.setTargetAtTime(0, this.ctx?.currentTime ?? 0, 0.05);
    }
  }

  resume() {
    void this.ctx?.resume();
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
    this.frictionFilter.frequency.setTargetAtTime(
      500 + speed * (rolling ? 2200 : 700),
      t,
      0.1,
    );
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

  /** low toll used for the summit cinematic */
  toll() {
    const ctx = this.ctx;
    if (!ctx || !this.master) return;
    const t = ctx.currentTime;
    [55, 82.5, 110].forEach((f, i) => {
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
