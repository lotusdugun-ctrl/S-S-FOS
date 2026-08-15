// Procedural, natural-toned audio: wind bed, stone friction, footsteps.
// No external assets; everything is synthesized with the WebAudio API.

/*
 * The music.
 *
 * An aulos carrying the melody over a plucked kithara and an ison drone, in the
 * Dorian octave species, on a metre made of long and short syllables. Every one
 * of those four choices is doing work that a "sad minor tune on a harp" was not.
 */

/**
 * The Dorian octave species — the scale the Greeks themselves called Dorian.
 * Two disjunct tetrachords, each semitone–tone–tone from the bottom:
 * A B♭ C D | E F G A.
 *
 * Confusingly it is what a modern ear files under Phrygian, and that flattened
 * second is the single most recognisable thing about the sound. What was here
 * before was D natural minor, which is not wrong so much as neutral: it reads as
 * "melancholy", not as "old", because it is the scale half of Western music is
 * written in.
 *
 * It is rooted on A so the drone lands on the same A1–E2–A2 the summit bell
 * already tolls. The two then fuse instead of colliding.
 */
const SCALE = [
  110.0, // 0  A2
  116.54, // 1  B♭2
  130.81, // 2  C3
  146.83, // 3  D3
  164.81, // 4  E3
  174.61, // 5  F3
  196.0, // 6  G3
  220.0, // 7  A3
  233.08, // 8  B♭3
  261.63, // 9  C4
  293.66, // 10 D4
  329.63, // 11 E4
  349.23, // 12 F4
  392.0, // 13 G4
  440.0, // 14 A4
];

/** `[degree in SCALE, length in short beats]`; a degree of -1 is a rest */
type Figure = [number, number];

/**
 * The shortest note in the metre — the chronos protos, the unit every other
 * length is a whole multiple of.
 *
 * Greek music took its rhythm from the metre of the verse it set rather than
 * from a bar line, so nothing here sits on an even grid: notes run two, three,
 * four and six units long, and the phrases breathe at different lengths. The old
 * melody was a straight run of equal eighth notes, which is what made it sound
 * like a sequencer rather than like a player.
 */
const BEAT = 0.22;

/**
 * The aulos line, in dactyls and spondees — long-short-short and long-long, the
 * feet Homer walks on. It climbs the lower tetrachord, reaches over into the
 * upper one, touches the octave, falls the whole way back down, and turns
 * upwards again at the end, because that is the story.
 */
// The line breaks below are the phrases; reflowing them loses the metre.
// prettier-ignore
const AULOS: Figure[] = [
  [7, 4], [7, 2], [8, 2], [9, 4], [10, 4], [-1, 2],
  [9, 4], [10, 2], [11, 2], [10, 4], [9, 4], [-1, 2],
  [11, 4], [12, 2], [11, 2], [13, 4], [12, 4], [-1, 2],
  [14, 6], [13, 2], [12, 4], [11, 4], [-1, 4],
  [13, 2], [12, 2], [11, 4], [10, 2], [9, 2], [8, 4],
  [7, 6], [5, 2], [4, 4], [-1, 4],
  [4, 4], [5, 2], [6, 2], [7, 8], [-1, 6],
];

/**
 * The kithara underneath it: slow open intervals rather than a countermelody,
 * which is how a plucked instrument accompanied a wind one. Same total length as
 * the aulos line (128 beats, about twenty-eight seconds) so the two stay locked.
 */
// prettier-ignore
const LYRE: Figure[] = [
  [0, 4], [4, 4], [7, 4], [4, 2], [-1, 4],
  [0, 4], [5, 4], [9, 4], [5, 2], [-1, 4],
  [4, 4], [7, 4], [11, 4], [7, 2], [-1, 4],
  [0, 6], [4, 4], [7, 4], [-1, 6],
  [9, 4], [7, 4], [5, 4], [4, 4],
  [3, 4], [2, 4], [1, 4], [0, 4],
  [0, 4], [4, 4], [7, 6], [-1, 8],
];

/**
 * A tympanon marking the head of each period and nothing else. One struck skin
 * every few seconds: enough to give the climb a footfall, far too sparse to
 * become a beat.
 */
// prettier-ignore
const DRUM: Array<[0 | 1, number]> = [
  [1, 18], [0, 18], [1, 18], [0, 20], [1, 16], [0, 16], [1, 22],
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
  /** each voice walks its own part, so the three keep their own phrase lengths */
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
    fric.connect(this.frictionFilter).connect(this.frictionGain).connect(this.master);
    fric.start();

    this.startMusic();
  }

  /** soft echo bus so the lyre rings out into the dusk */
  private startMusic() {
    const ctx = this.ctx;
    if (!ctx || !this.master) return;

    this.musicGain = ctx.createGain();
    this.musicGain.gain.value = 0.42;
    this.musicGain.connect(this.master);

    /*
     * Echo, as the open air of a hillside rather than as an effect. Two taps at
     * unrelated times, so the repeats never line up into a rhythm of their own —
     * one short slap off the near rock, one long one off the valley.
     */
    for (const [time, feedback, wetness] of [
      [0.27, 0.24, 0.26],
      [0.53, 0.3, 0.2],
    ]) {
      const delay = ctx.createDelay(1.5);
      delay.delayTime.value = time!;
      const fb = ctx.createGain();
      fb.gain.value = feedback!;
      // the far repeats lose their top end, the way distance takes it
      const damp = ctx.createBiquadFilter();
      damp.type = "lowpass";
      damp.frequency.value = 2600;
      const wet = ctx.createGain();
      wet.gain.value = wetness!;
      this.musicGain.connect(delay);
      delay.connect(damp).connect(fb).connect(delay);
      delay.connect(wet).connect(this.master);
    }

    /*
     * The ison: a held drone under everything, which is how this music was sung
     * and is still sung around the same sea. A1–E2–A2, the octave and the fifth
     * and nothing else — a third would make it a chord, and a chord is the one
     * thing this music did not have.
     *
     * These are the exact pitches the summit bell tolls, so when Zeus arrives the
     * toll lands inside the drone instead of beside it.
     */
    const dg = ctx.createGain();
    dg.gain.value = 0.06;
    this.drone = [];
    for (const f of [55, 82.41, 110]) {
      const o = ctx.createOscillator();
      o.type = "sine";
      o.frequency.value = f;
      o.connect(dg);
      o.start();
      this.drone.push(o);
    }
    // a fifth voice a hair sharp, so the drone beats slowly instead of sitting dead
    const shimmer = ctx.createOscillator();
    shimmer.type = "sine";
    shimmer.frequency.value = 110;
    shimmer.detune.value = 7;
    const sg = ctx.createGain();
    sg.gain.value = 0.4;
    shimmer.connect(sg).connect(dg);
    shimmer.start();
    this.drone.push(shimmer);
    dg.connect(this.musicGain);

    const lfo = ctx.createOscillator();
    lfo.frequency.value = 0.08;
    const lg = ctx.createGain();
    lg.gain.value = 0.022;
    lfo.connect(lg).connect(dg.gain);
    lfo.start();

    const at = ctx.currentTime + 0.2;
    this.voices = [
      { idx: 0, at },
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
      play: (degree: number, when: number, dur: number) => void,
    ) => {
      // a voice that has fallen behind (a backgrounded tab) catches up rather
      // than scheduling a burst of notes all in the past
      if (voice.at < ctx.currentTime) voice.at = ctx.currentTime + 0.05;
      while (voice.at < horizon) {
        const [degree, len] = part[voice.idx]!;
        const dur = len * BEAT;
        if (degree >= 0) play(degree, voice.at, dur);
        voice.at += dur;
        voice.idx = (voice.idx + 1) % part.length;
      }
    };

    const [aulos, lyre, drum] = this.voices;
    if (aulos) run(aulos, AULOS, (d, when, dur) => this.aulos(SCALE[d]!, when, dur));
    if (lyre) run(lyre, LYRE, (d, when, dur) => this.lyre(SCALE[d]!, when, dur));
    if (drum) {
      run(drum, DRUM, (hit, when) => {
        if (hit === 1) this.tympanon(when);
      });
    }
  }

  /**
   * The aulos: a double-reed pipe, and the sound most people would name if asked
   * what ancient Greece sounded like. Nothing in the old mix was a wind
   * instrument at all, which is why it read as "harp music" rather than as Greek.
   *
   * Three things make it one. It is reedy rather than pure, so the source is a
   * sawtooth shaped by two formant peaks — the nasal bite of a beating reed lives
   * in that pair of resonances. It speaks rather than strikes, so the envelope
   * comes up over fifty milliseconds on a breath of noise instead of snapping on.
   * And it is genuinely *two* pipes, played at once by one player and never quite
   * in tune with each other; that slow beating between them is not a chorus
   * effect standing in for the instrument, it is the instrument.
   */
  private aulos(freq: number, when: number, dur: number) {
    const ctx = this.ctx;
    if (!ctx || !this.musicGain) return;
    const end = when + dur;

    const g = ctx.createGain();
    g.gain.setValueAtTime(0.0001, when);
    g.gain.exponentialRampToValueAtTime(0.19, when + 0.05);
    // settles back off the initial push, the way a held breath does
    g.gain.setTargetAtTime(0.13, when + 0.07, 0.3);
    g.gain.setTargetAtTime(0.0001, Math.max(when + 0.09, end - 0.09), 0.045);

    const bore = ctx.createBiquadFilter();
    bore.type = "lowpass";
    bore.frequency.value = Math.min(4200, freq * 7);
    bore.Q.value = 0.9;
    // the two formants that make a double reed nasal rather than merely bright
    const f1 = ctx.createBiquadFilter();
    f1.type = "peaking";
    f1.frequency.value = 720;
    f1.Q.value = 2.2;
    f1.gain.value = 8;
    const f2 = ctx.createBiquadFilter();
    f2.type = "peaking";
    f2.frequency.value = 1500;
    f2.Q.value = 2.6;
    f2.gain.value = 5;
    bore.connect(f1).connect(f2).connect(g).connect(this.musicGain);

    // vibrato, held back until the note has spoken — a player does not shake a
    // note they have not landed yet
    const vib = ctx.createOscillator();
    vib.frequency.value = 5.4;
    const vibAmt = ctx.createGain();
    vibAmt.gain.setValueAtTime(0, when);
    vibAmt.gain.setTargetAtTime(freq * 0.008, when + 0.14, 0.22);
    vib.connect(vibAmt);
    vib.start(when);
    vib.stop(end + 0.15);

    for (const detune of [-7, 7]) {
      const o = ctx.createOscillator();
      o.type = "sawtooth";
      o.frequency.value = freq;
      o.detune.value = detune;
      vibAmt.connect(o.frequency);
      o.connect(bore);
      o.start(when);
      o.stop(end + 0.15);
    }

    // the breath itself, arriving just before the tone does
    if (this.noiseBuffer) {
      const air = ctx.createBufferSource();
      air.buffer = this.noiseBuffer;
      air.playbackRate.value = 1.4;
      const hp = ctx.createBiquadFilter();
      hp.type = "bandpass";
      hp.frequency.value = Math.min(5000, freq * 5);
      hp.Q.value = 0.7;
      const ag = ctx.createGain();
      ag.gain.setValueAtTime(0.0001, when);
      ag.gain.exponentialRampToValueAtTime(0.035, when + 0.03);
      ag.gain.exponentialRampToValueAtTime(0.0001, when + 0.16);
      air.connect(hp).connect(ag).connect(this.musicGain);
      air.start(when);
      air.stop(when + 0.2);
    }
  }

  /**
   * The kithara under it. A gut string picked with a plektron: a short bright
   * click as the pick releases, then a body that is mostly fundamental with a
   * ringing octave over it, decaying long.
   */
  private lyre(freq: number, when: number, dur: number) {
    const ctx = this.ctx;
    if (!ctx || !this.musicGain) return;
    const ring = Math.max(dur, 1.1);

    const g = ctx.createGain();
    g.gain.setValueAtTime(0.0001, when);
    g.gain.exponentialRampToValueAtTime(0.15, when + 0.006);
    g.gain.exponentialRampToValueAtTime(0.0001, when + ring);
    const filt = ctx.createBiquadFilter();
    filt.type = "lowpass";
    filt.frequency.setValueAtTime(Math.min(7000, freq * 9), when);
    // gut goes dull as it decays, where a synth tone would stay bright
    filt.frequency.exponentialRampToValueAtTime(Math.min(2200, freq * 3), when + ring * 0.6);
    filt.Q.value = 1.1;
    filt.connect(g).connect(this.musicGain);

    const o = ctx.createOscillator();
    o.type = "triangle";
    o.frequency.value = freq;
    o.connect(filt);
    o.start(when);
    o.stop(when + ring + 0.05);

    const oct = ctx.createOscillator();
    oct.type = "sine";
    oct.frequency.value = freq * 2;
    const og = ctx.createGain();
    og.gain.setValueAtTime(0.26, when);
    // the octave partial dies well before the fundamental, as partials do
    og.gain.exponentialRampToValueAtTime(0.0001, when + ring * 0.45);
    oct.connect(og).connect(filt);
    oct.start(when);
    oct.stop(when + ring + 0.05);

    // the plektron striking the string
    if (this.noiseBuffer) {
      const pick = ctx.createBufferSource();
      pick.buffer = this.noiseBuffer;
      pick.playbackRate.value = 1.8;
      const bp = ctx.createBiquadFilter();
      bp.type = "bandpass";
      bp.frequency.value = Math.min(6000, freq * 8);
      bp.Q.value = 0.8;
      const pg = ctx.createGain();
      pg.gain.setValueAtTime(0.06, when);
      pg.gain.exponentialRampToValueAtTime(0.0001, when + 0.035);
      pick.connect(bp).connect(pg).connect(this.musicGain);
      pick.start(when);
      pick.stop(when + 0.06);
    }
  }

  /** a struck frame drum, marking the head of a period */
  private tympanon(when: number) {
    const ctx = this.ctx;
    if (!ctx || !this.musicGain) return;

    const o = ctx.createOscillator();
    o.type = "sine";
    o.frequency.setValueAtTime(96, when);
    o.frequency.exponentialRampToValueAtTime(52, when + 0.22);
    const g = ctx.createGain();
    g.gain.setValueAtTime(0.0001, when);
    g.gain.exponentialRampToValueAtTime(0.11, when + 0.012);
    g.gain.exponentialRampToValueAtTime(0.0001, when + 0.4);
    o.connect(g).connect(this.musicGain);
    o.start(when);
    o.stop(when + 0.45);

    if (!this.noiseBuffer) return;
    // the slap of the hand on the skin, without which it is just a low sine
    const skin = ctx.createBufferSource();
    skin.buffer = this.noiseBuffer;
    skin.playbackRate.value = 0.9;
    const lp = ctx.createBiquadFilter();
    lp.type = "lowpass";
    lp.frequency.value = 380;
    const sg = ctx.createGain();
    sg.gain.setValueAtTime(0.09, when);
    sg.gain.exponentialRampToValueAtTime(0.0001, when + 0.13);
    skin.connect(lp).connect(sg).connect(this.musicGain);
    skin.start(when);
    skin.stop(when + 0.16);
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
