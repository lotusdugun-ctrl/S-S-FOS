// Procedural, natural-toned audio: wind bed, stone friction, footsteps.
// No external assets; everything is synthesized with the WebAudio API.

/*
 * The music: a music box.
 *
 * Everything before this was written to be atmosphere — a pipe, then broken
 * chords, both of them trying to have no melody and no pulse so that nothing
 * would ask to be listened to. That is one way to score a hillside and it kept
 * not working, so this goes the other way entirely.
 *
 * A music box has a tune, and a waltz pulse, and it is bright where all of that
 * was dark. What makes it right for this game is not the sound, it is the
 * mechanism: a cylinder turns, the pins pluck the same teeth in the same order,
 * it reaches the end, and it starts again having got nowhere. It is thirty
 * seconds long and it will play until the tab is closed. Sisyphus is a man
 * doing the same thing forever, and a music box is the only instrument that is
 * also that.
 *
 * The tune is a simple minor waltz that circles back to the note it started on,
 * and the box is deliberately imperfect — the timing wanders a few
 * milliseconds, the teeth are not all struck equally hard — because a mechanism
 * that is exactly even sounds like a sequencer, and one that is slightly out
 * sounds like an object with springs in it.
 */

/**
 * A natural minor, A3 to E6.
 *
 * High and narrow, because that is where a comb lives. The melody sits in the
 * top octave and a half of this, the counterpoint in the bottom fifth of it, and
 * the gap between them is what makes a music box sound like a music box rather
 * than like a celeste playing chords.
 */
const SCALE = [
  220.0, // 0  A3
  246.94, // 1  B3
  261.63, // 2  C4
  293.66, // 3  D4
  329.63, // 4  E4
  349.23, // 5  F4
  392.0, // 6  G4
  440.0, // 7  A4
  493.88, // 8  B4
  523.25, // 9  C5
  587.33, // 10 D5
  659.25, // 11 E5
  698.46, // 12 F5
  783.99, // 13 G5
  880.0, // 14 A5
  987.77, // 15 B5
  1046.5, // 16 C6
  1174.66, // 17 D6
  1318.51, // 18 E6
];

/** `[degree in SCALE, length in beats]`; a degree of -1 is a rest */
type Figure = [number, number];

/**
 * One beat of the waltz — a crotchet, three to the bar.
 *
 * Slower than a music box really turns. A real cylinder is wound to something
 * near a hundred and twenty; at that speed this tune is a jaunty little thing,
 * and jaunty is wrong over a man pushing a rock up a hill. At ninety-odd it
 * becomes what a box sounds like when the spring is nearly run down, which is
 * the same tune and an entirely different feeling.
 */
const BEAT = 0.62;

/**
 * The tune. Sixteen bars of three, and the sixteenth is a rest — the pause where
 * you can hear the cylinder still turning before the first pin comes round
 * again.
 *
 * It is built out of one four-bar phrase and three answers to it, and it ends
 * on the A it began on, having gone up a sixth in the middle and come back
 * down. That shape is the point: it is a tune with an arc that arrives exactly
 * where it started, sixteen bars later, forever.
 */
// The line breaks are the four-bar phrases; reflowing them loses the shape.
// prettier-ignore
const MELODY: Figure[] = [
  [14, 3], [16, 1], [15, 1], [14, 1], [15, 2], [13, 1], [14, 3],
  [11, 1], [13, 1], [14, 1], [15, 2], [16, 1], [15, 1], [14, 1], [13, 1], [11, 3],
  [17, 3], [16, 1], [15, 1], [14, 1], [13, 2], [14, 1], [15, 3],
  [16, 1], [15, 1], [13, 1], [14, 2], [11, 1], [14, 3], [-1, 3],
];

/**
 * The teeth at the low end of the comb, one every two bars.
 *
 * Not a bass line — a music box has no bass, the longest tooth on a comb is
 * still well up in the treble. It is one note left to ring underneath, which is
 * enough to say which chord the bar is in: Am, then Em, Am, Am, Dm, G, and Am
 * to the end.
 *
 * Dm–G–Am across the last six bars is the only proper cadence in the piece, and
 * it is what makes the loop sound like it has finished rather than like it has
 * been cut off — which matters when the whole point is that it then starts
 * again. The G is a low tooth against the tune's G, A and B: an octave, a ninth
 * and a tenth, all of them wide and none of them a semitone.
 */
// prettier-ignore
const COMB: Figure[] = [
  [0, 6], [4, 6], [0, 6], [0, 6],
  [3, 6], [6, 6], [0, 6], [0, 6],
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
     *
     * It has been walked up twice now and this is the last of it. At two
     * kilohertz it was taking the hiss off a breathy pipe, which is what that
     * instrument needed; a music box is a chime at six or seven kilohertz and
     * everything below that is only the note it hangs on, so cutting anywhere
     * near the old figure removes the instrument and leaves a sine wave.
     *
     * It stays in the graph rather than coming out, because there is still
     * something to do up at the very top: the pin noise and the third mode of
     * the shorter teeth run past ten kilohertz, and that is the register that
     * makes a synthesised chime sound glassy rather than metal.
     */
    const air = ctx.createBiquadFilter();
    air.type = "lowpass";
    air.frequency.value = 9000;
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
     * A held drone under everything: A1–E2–A2, the octave and the fifth and
     * nothing else. These are the exact pitches the summit bell tolls, so when
     * Zeus arrives the toll lands inside the drone instead of beside it, and
     * that is the reason it survives a change of instrument.
     *
     * Half of what it was, though. It was written to be a voice — the thing the
     * old melody leaned on and resolved into — and a music box does not lean on
     * anything; it is an object sitting in a room. Down here it stops being part
     * of the music and becomes the room, which is what is wanted now.
     */
    const dg = ctx.createGain();
    dg.gain.value = 0.03;
    this.drone = [];
    for (const f of [55, 82.41, 110]) {
      const o = ctx.createOscillator();
      o.type = "sine";
      o.frequency.value = f;
      o.connect(dg);
      o.start();
      this.drone.push(o);
    }
    /*
     * A fourth voice a hair sharp, so the drone breathes instead of sitting dead.
     *
     * It was seven cents out, which beats about three times a second — fast
     * enough to hear as a wobble, and a wobble is a thing the ear keeps checking
     * on. At four cents the two go in and out of phase over roughly four
     * seconds, which is slow enough to read as one warm note rather than as two
     * arguing ones. It is the same trick a shruti box lives on.
     */
    const shimmer = ctx.createOscillator();
    shimmer.type = "sine";
    shimmer.frequency.value = 110;
    shimmer.detune.value = 4;
    const sg = ctx.createGain();
    sg.gain.value = 0.4;
    shimmer.connect(sg).connect(dg);
    shimmer.start();
    this.drone.push(shimmer);
    dg.connect(this.musicGain);

    // the drone swells and falls on its own, slower than a breath
    const lfo = ctx.createOscillator();
    lfo.frequency.value = 0.055;
    const lg = ctx.createGain();
    lg.gain.value = 0.018;
    lfo.connect(lg).connect(dg.gain);
    lfo.start();

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
        if (degree >= 0) {
          /*
           * The wobble that makes it a mechanism rather than a sequencer.
           *
           * A cylinder is a machine with slack in it: no two pins arrive exactly
           * on time and no two teeth are struck exactly as hard. Twelve
           * milliseconds and a fifth of the level is far too little to hear as
           * an effect and just enough that the box never lands twice in exactly
           * the same place, which is the whole difference between an object and
           * a grid.
           */
          const jitter = (Math.random() - 0.5) * 0.024;
          this.chime(SCALE[degree]!, voice.at + jitter, level * (0.9 + Math.random() * 0.2));
        }
        voice.at += len * BEAT;
        voice.idx = (voice.idx + 1) % part.length;
      }
    };

    const [melody, comb] = this.voices;
    if (melody) run(melody, MELODY, 0.13);
    // the low teeth are struck by the same pins and ring longer, so they need to
    // be well under the tune or they become the thing you follow
    if (comb) run(comb, COMB, 0.075);
  }

  /**
   * One tooth of the comb, plucked by a pin on the cylinder.
   *
   * A comb tooth is a cantilever — a metal bar clamped at one end — and that is
   * the whole synthesis. A clamped bar's modes are not harmonic: the second sits
   * at 6.27 times the fundamental rather than at 2, which is why a music box
   * rings rather than sounding like a note. That mode is also very short-lived,
   * so what the ear gets is a metallic chime for a tenth of a second and then a
   * decaying sine, and that pair is the entire character of the instrument.
   *
   * Everything before this was made calm by taking the top off it. A music box
   * is nothing without its top — the chime lives at six or seven kilohertz — so
   * the filter over the music bus is open to nine, and it is the tune and the
   * silence around it that have to do the work instead.
   */
  private chime(freq: number, when: number, level: number) {
    const ctx = this.ctx;
    if (!ctx || !this.musicGain) return;
    // short teeth ring less than long ones, so the top of the tune sparkles and
    // the bottom of it hums
    const ring = Math.max(1.3, 3.6 - freq / 620);

    const g = ctx.createGain();
    g.gain.setValueAtTime(0.0001, when);
    // a pin releasing a tooth is about as close to instantaneous as this gets
    g.gain.exponentialRampToValueAtTime(level, when + 0.004);
    g.gain.exponentialRampToValueAtTime(0.0001, when + ring);
    g.connect(this.musicGain);

    const o = ctx.createOscillator();
    o.type = "sine";
    o.frequency.value = freq;
    o.connect(g);
    o.start(when);
    o.stop(when + ring + 0.05);

    /*
     * The bar's second and third modes. 6.27 and 17.5 are the real ratios for a
     * clamped-free bar, and using the real ones rather than octaves is what
     * stops this being a bell or a glockenspiel; both are gone inside a tenth of
     * a second, which is what stops it being a gong.
     */
    for (const [ratio, amp, life] of [
      [6.27, 0.5, 0.085],
      [17.5, 0.16, 0.035],
    ]) {
      if (freq * ratio! > 15000) continue; // above hearing, and it would alias
      const p = ctx.createOscillator();
      p.type = "sine";
      p.frequency.value = freq * ratio!;
      const pg = ctx.createGain();
      pg.gain.setValueAtTime(level * amp!, when);
      pg.gain.exponentialRampToValueAtTime(0.0001, when + life!);
      p.connect(pg).connect(this.musicGain);
      p.start(when);
      p.stop(when + life! + 0.02);
    }

    // the pin itself, dragging off the tooth. Almost inaudible on its own and
    // the difference between a music box and a sine wave with an envelope
    if (this.noiseBuffer) {
      const pin = ctx.createBufferSource();
      pin.buffer = this.noiseBuffer;
      pin.playbackRate.value = 2;
      const bp = ctx.createBiquadFilter();
      bp.type = "bandpass";
      bp.frequency.value = 4200;
      bp.Q.value = 0.9;
      const ng = ctx.createGain();
      ng.gain.setValueAtTime(level * 0.28, when);
      ng.gain.exponentialRampToValueAtTime(0.0001, when + 0.025);
      pin.connect(bp).connect(ng).connect(this.musicGain);
      pin.start(when);
      pin.stop(when + 0.04);
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
