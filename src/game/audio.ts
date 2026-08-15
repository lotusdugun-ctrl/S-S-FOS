// Procedural, natural-toned audio: wind bed, stone friction, footsteps.
// No external assets; everything is synthesized with the WebAudio API.

/*
 * The music: a violin over a cello, playing an adagio.
 *
 * This is the fifth instrument on this hill. A pipe, then broken chords, then a
 * music box, and the honest reading of why none of them held is that all three
 * were built out of the same idea — that the score should be an object in the
 * room, something with no line to follow and no feeling to have. Atmosphere.
 * The music box at least had a tune, but a music box is a toy; it chimes, and a
 * chime cannot carry weight.
 *
 * A bowed string is the opposite of all of that, and it is the one thing none of
 * them could do: it can *sustain*, and it can get louder inside a note it is
 * already playing. Everything else here started at its loudest and died from
 * there — a pluck, a strike, a breath — which is why they could only ever set a
 * mood and never mean anything by it. A bow can lean into the middle of a long
 * note, and that swell is the whole reason a violin sounds like it is feeling
 * something.
 *
 * So: an adagio in A minor, sixty-four seconds, one line sung on the violin's
 * warmest string and a cello under it holding the harmony. It ends on the note
 * it started on, and then it goes round again.
 */

/**
 * A natural minor, E3 to A5.
 *
 * The bottom fifth of it is the cello's, everything from A4 up is the violin's,
 * and the violin's part sits almost entirely on what would be its D and A
 * strings — the middle of the instrument, where it is warm and human, rather
 * than the E string, where it is brilliant and starts to sound like a display.
 */
const SCALE = [
  164.81, // 0  E3
  174.61, // 1  F3
  196.0, // 2  G3
  220.0, // 3  A3
  246.94, // 4  B3
  261.63, // 5  C4
  293.66, // 6  D4
  329.63, // 7  E4
  349.23, // 8  F4
  392.0, // 9  G4
  440.0, // 10 A4
  493.88, // 11 B4
  523.25, // 12 C5
  587.33, // 13 D5
  659.25, // 14 E5
  698.46, // 15 F5
  783.99, // 16 G5
  880.0, // 17 A5
];

/** `[degree in SCALE, length in beats]`; a degree of -1 is a rest */
type Figure = [number, number];

/**
 * One beat. Four to the bar, and slow enough that a bow has time to do something
 * inside a single note — which is the entire point of changing to strings, and
 * is lost the moment the notes get shorter than about a second.
 */
const BEAT = 0.5;

/**
 * The violin line: eight phrases of four bars, sixty-four seconds.
 *
 * It is a lament, so it mostly falls. Four of the eight phrases are descents,
 * the rests come where a player would need the bow back, and the one climb — up
 * to the F in the third phrase — is the only time it goes above E5. That F is
 * the flattened sixth of the scale and the saddest note available in a minor
 * key; putting it at the top of the only ascent, and then falling away from it
 * for the rest of the piece, is the shape of the whole thing.
 *
 * The last phrase lands back on A, where the first one was heading all along.
 */
// The line breaks are the four-bar phrases; reflowing them loses the shape.
// prettier-ignore
const VIOLIN: Figure[] = [
  [14, 6], [13, 2], [12, 4], [11, 2], [-1, 2],
  [12, 4], [10, 4], [11, 6], [-1, 2],
  [10, 4], [12, 4], [14, 4], [15, 4],
  [14, 8], [13, 4], [12, 2], [-1, 2],
  [11, 4], [12, 2], [13, 2], [12, 4], [11, 4],
  [10, 6], [9, 2], [10, 8],
  [13, 4], [12, 4], [11, 4], [10, 4],
  [11, 4], [10, 6], [-1, 6],
];

/**
 * The cello under it: one note every two bars, bowed and held for the whole of
 * them, which is what puts a floor under the violin.
 *
 * Am, Em, F, E, Am, Dm, Am, F, G, Em, Dm, E, Dm, Am, E, Am. It is a real
 * progression rather than a static bed — the first four instruments all refused
 * to have one, on the theory that a harmony that moves is a harmony going
 * somewhere, and going nowhere was the point. That was the mistake. A line that
 * is felt has to be leaning on something, and the E under the seventh phrase is
 * what makes the A at the end sound like an arrival instead of a stop.
 */
// prettier-ignore
const CELLO: Figure[] = [
  [3, 8], [0, 8],
  [1, 8], [0, 8],
  [3, 8], [6, 8],
  [3, 8], [1, 8],
  [2, 8], [0, 8],
  [6, 8], [0, 8],
  [6, 8], [3, 8],
  [0, 8], [3, 8],
];

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
  /** the tune and the comb underneath it, each walking its own part */
  private voices: Array<{ idx: number; at: number }> = [];

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

  /** soft echo bus so the strings ring out into the dusk */
  private startMusic() {
    const ctx = this.ctx;
    if (!ctx || !this.master) return;

    this.musicGain = ctx.createGain();
    this.musicGain.gain.value = 0.32;

    /*
     * One filter across the whole of the music, before anything else touches it.
     * It has been moved for every instrument that has stood here and this is
     * where a string section wants it.
     *
     * The bow noise and the top harmonics of a sawtooth both run well past this,
     * and both are the parts that sound like a synthesiser rather than like
     * rosin on a string. Each note has its own rolloff at 4.2 kHz for the body
     * of the instrument; this one is the room the instrument is standing in.
     */
    const air = ctx.createBiquadFilter();
    air.type = "lowpass";
    air.frequency.value = 6000;
    air.Q.value = 0.5;
    this.musicGain.connect(air).connect(this.master);

    /*
     * Echo, as the open air of a hillside rather than as an effect. Two taps at
     * unrelated times, so the repeats never line up into a rhythm of their own —
     * one off the near rock, one off the far side of the valley.
     *
     * Longer, dimmer and with much less feedback than they had. A short bright
     * repeat is heard as a rhythm even when there is no rhythm under it, and a
     * repeat that feeds back hard piles the previous phrase onto the next one
     * until the whole thing is a wash. Two or three quiet returns and gone: that
     * is heard as distance, and distance is the thing this wants.
     */
    for (const [time, feedback, wetness] of [
      [0.61, 0.18, 0.16],
      [1.13, 0.2, 0.13],
    ]) {
      const delay = ctx.createDelay(2);
      delay.delayTime.value = time!;
      const fb = ctx.createGain();
      fb.gain.value = feedback!;
      // the far repeats lose their top end, the way distance takes it
      const damp = ctx.createBiquadFilter();
      damp.type = "lowpass";
      damp.frequency.value = 1100;
      const wet = ctx.createGain();
      wet.gain.value = wetness!;
      // fed from after the rolloff, so a repeat is never brighter than the note
      // that caused it
      air.connect(delay);
      delay.connect(damp).connect(fb).connect(delay);
      delay.connect(wet).connect(this.master);
    }

    /*
     * The drone is gone, and it is the harmony that removed it.
     *
     * It had been here since the first version: a held A1–E2–A2 on the pitches
     * the summit bell tolls, so that Zeus arriving landed inside the sound
     * instead of beside it. That worked because none of the first four pieces
     * ever changed chord — a drone can sit under a static harmony forever.
     *
     * The cello now moves through Am, Em, F, E, G and Dm, and a fixed A
     * underneath is wrong for three of those: a fourth against the E, a ninth
     * against the G, and in the bass register both read as mud rather than as
     * colour. A moving harmony and a drone are alternatives, not layers.
     *
     * The bell still tolls A1–E2–A2, and it now lands on an open A minor at
     * either end of the loop, which is close enough to what the drone was doing
     * for it.
     */
    this.drone = [];

    const at = ctx.currentTime + 0.2;
    this.voices = [
      { idx: 0, at },
      { idx: 0, at },
    ];
    this.musicTimer = window.setInterval(() => this.scheduleMusic(), 120);
  }

  private scheduleMusic() {
    const ctx = this.ctx;
    if (!ctx || !this.musicGain) return;
    const horizon = ctx.currentTime + 0.5;

    const run = (
      voice: { idx: number; at: number },
      part: ReadonlyArray<readonly [number, number]>,
      level: number,
    ) => {
      // a voice that has fallen behind (a backgrounded tab) catches up rather
      // than scheduling a burst of notes all in the past
      if (voice.at < ctx.currentTime) voice.at = ctx.currentTime + 0.05;
      while (voice.at < horizon) {
        const [degree, len] = part[voice.idx]!;
        const dur = len * BEAT;
        if (degree >= 0) {
          // a player is not a grid: the bow lands a few milliseconds off and no
          // two notes are drawn at exactly the same weight
          const jitter = (Math.random() - 0.5) * 0.03;
          this.bow(SCALE[degree]!, voice.at + jitter, dur, level * (0.92 + Math.random() * 0.16));
        }
        voice.at += dur;
        voice.idx = (voice.idx + 1) % part.length;
      }
    };

    const [violin, cello] = this.voices;
    if (violin) run(violin, VIOLIN, 0.1);
    // the cello holds whole phrases, so at equal level it would simply be louder
    // than the line it is supporting
    if (cello) run(cello, CELLO, 0.062);
  }

  /**
   * A bowed string.
   *
   * The string itself is the easy half. A bow pulls the string sideways until it
   * slips, catches it again, and repeats — Helmholtz motion — and the wave that
   * comes off it is very close to a sawtooth. That is one oscillator.
   *
   * The half that makes it a violin rather than a buzz is the box it is glued
   * to. A violin body has fixed resonances that do not move when the pitch does:
   * the air inside the box around 280 Hz, the main wood resonance near 460, a
   * cluster around 700, and the broad "bridge hill" between two and three
   * kilohertz that gives the instrument its carry. Those four peaking filters
   * are the difference between this and a sawtooth with an envelope on it,
   * because they are what makes a low note and a high note sound like the same
   * instrument.
   *
   * The other half of the illusion is that a bow keeps *doing* something for as
   * long as the note lasts: it takes a moment to grip, it swells into the middle
   * of the note, and the vibrato arrives late because a player lands the pitch
   * before shaking it. None of the four instruments before this could do any of
   * that — a pluck, a strike and a breath all start at their loudest and only
   * decay.
   */
  private bow(freq: number, when: number, dur: number, level: number) {
    const ctx = this.ctx;
    if (!ctx || !this.musicGain) return;
    const end = when + dur;

    /*
     * The bow's own shape across the note. It grips over about a sixth of a
     * second — slow, because a hard front on a bowed note is the single clearest
     * sign of a synthesiser — swells to full weight a third of the way in, and
     * comes off the string over a quarter of a second rather than stopping.
     */
    const g = ctx.createGain();
    g.gain.setValueAtTime(0.0001, when);
    g.gain.exponentialRampToValueAtTime(level * 0.72, when + 0.16);
    g.gain.setTargetAtTime(level, when + 0.2, Math.max(0.2, dur * 0.3));
    g.gain.setTargetAtTime(0.0001, Math.max(when + 0.25, end - 0.22), 0.08);
    g.connect(this.musicGain);

    /*
     * The body. These frequencies are properties of the box, not of the note, so
     * they are the same for every pitch — that is exactly why they read as an
     * instrument. The bridge hill is wide and the rest are narrow.
     */
    let head: BiquadFilterNode | null = null;
    let tail: BiquadFilterNode | null = null;
    for (const [f, q, gain] of [
      [280, 2.4, 7],
      [460, 3.2, 9],
      [700, 2.8, 5],
      [2400, 0.9, 6],
    ]) {
      const b = ctx.createBiquadFilter();
      b.type = "peaking";
      b.frequency.value = f!;
      b.Q.value = q!;
      b.gain.value = gain!;
      if (tail) tail.connect(b);
      else head = b;
      tail = b;
    }
    // above the bridge hill a real body falls away; without this the sawtooth's
    // top harmonics stay raw and it buzzes
    const roll = ctx.createBiquadFilter();
    roll.type = "lowpass";
    roll.frequency.value = 4200;
    roll.Q.value = 0.6;
    tail!.connect(roll).connect(g);

    /*
     * Vibrato: five and a half hertz, and it does not start with the note. A
     * player finds the pitch first and only then begins to shake it, so this
     * comes in over the second half of a second and never exceeds about a third
     * of a semitone.
     */
    const vib = ctx.createOscillator();
    vib.frequency.value = 5.5;
    const vibAmt = ctx.createGain();
    vibAmt.gain.setValueAtTime(0, when);
    vibAmt.gain.setTargetAtTime(freq * 0.0075, when + 0.35, 0.45);
    vib.connect(vibAmt);
    vib.start(when);
    vib.stop(end + 0.4);

    /*
     * Two saws a few cents apart. On a solo instrument this is not a chorus
     * effect standing in for an ensemble — a real string is never in perfect
     * tune with itself across its own length, and the slow beating between the
     * two is what stops the tone sitting dead still.
     */
    for (const detune of [-5, 5]) {
      const o = ctx.createOscillator();
      o.type = "sawtooth";
      o.frequency.value = freq;
      o.detune.value = detune;
      vibAmt.connect(o.frequency);
      o.connect(head!);
      o.start(when);
      o.stop(end + 0.4);
    }

    // the bow's own scrape on the string: broadband, loudest as it grips, and
    // never quite gone. It is inaudible alone and the note sounds synthetic
    // without it
    if (this.noiseBuffer) {
      const hair = ctx.createBufferSource();
      hair.buffer = this.noiseBuffer;
      hair.loop = true;
      hair.playbackRate.value = 0.9;
      const bp = ctx.createBiquadFilter();
      bp.type = "bandpass";
      bp.frequency.value = Math.min(5000, freq * 4);
      bp.Q.value = 0.8;
      const ng = ctx.createGain();
      ng.gain.setValueAtTime(0.0001, when);
      ng.gain.exponentialRampToValueAtTime(level * 0.3, when + 0.05);
      ng.gain.setTargetAtTime(level * 0.09, when + 0.12, 0.3);
      hair.connect(bp).connect(ng).connect(g);
      hair.start(when);
      hair.stop(end + 0.4);
    }
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
    this.voices = [];
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

  /** low toll used for the summit cinematic — the same A1–E2–A2 the drone holds */
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
