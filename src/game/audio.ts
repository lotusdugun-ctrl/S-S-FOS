// Procedural, natural-toned audio: wind bed, stone friction, footsteps.
// No external assets; everything is synthesized with the WebAudio API.

/*
 * The music.
 *
 * Broken chords on a plucked string, over the drone that was already here.
 *
 * There was a melody before — a pipe playing a line over a plucked
 * accompaniment — and twice it was made slower, quieter and more consonant and
 * twice it was still the wrong thing, because the problem was never the notes.
 * A melody is a line, a line goes somewhere, and something going somewhere is
 * something to follow. On a screen where the whole point is that nothing is ever
 * finished, having a tune to keep up with works against the game.
 *
 * So there is no line now. There is a chord, arpeggiated slowly enough that no
 * two notes belong to the same gesture, and every note rings for five or six
 * seconds — which means four or five of them are always sounding at once. What
 * the ear gets is not a sequence but a texture that keeps being refreshed from
 * underneath. There is constant movement and no direction, which is the thing
 * the last two versions were reaching for and could not get to while there was
 * still a melody in the way.
 */

/**
 * A Aeolian, from A2 to E5 — the range one pair of hands covers on a harp.
 *
 * Wider than the pentatonic that was here, because an arpeggio needs octaves to
 * move through where a melody needed a comfortable middle. The F and the G are
 * back: over a fixed A drone they are what separates the four chords below from
 * each other, and in a chord they are consonant in a way they never were as a
 * melody note held against the drone.
 */
const SCALE = [
  110.0, // 0  A2
  130.81, // 1  C3
  146.83, // 2  D3
  164.81, // 3  E3
  174.61, // 4  F3
  196.0, // 5  G3
  220.0, // 6  A3
  261.63, // 7  C4
  293.66, // 8  D4
  329.63, // 9  E4
  349.23, // 10 F4
  392.0, // 11 G4
  440.0, // 12 A4
  523.25, // 13 C5
  587.33, // 14 D5
  659.25, // 15 E5
];

/**
 * The unit the plucks are spaced in.
 *
 * It is no longer a note length — a plucked string decides its own length by
 * decaying, and the ring is set in `pluck` rather than here. This is only how
 * long it is until the next note is picked.
 */
const BEAT = 0.4;

/**
 * Four chords, each voiced as the degrees of SCALE the arpeggio picks from.
 *
 * Am, C, F, Dm. No dominant and no leading tone anywhere, so nothing pulls
 * towards a resolution and the sequence can turn over forever without ever
 * arriving. Each shares two notes with the one before it, which is why the
 * change lands as a colour shift rather than as an event.
 *
 * All four work over the fixed A1–E2–A2 drone, and that is what lets the drone
 * stay put instead of following the chords: A over C makes it Am7, the drone's E
 * over F makes it Fmaj7, over Dm it is a ninth. A moving bass under a fixed
 * drone would have collided with it — F2 against the drone's E2 is a semitone in
 * the register least able to carry one — so there is no bass part at all. The
 * drone is the bass.
 */
const CHORDS: number[][] = [
  [6, 7, 9, 12, 13, 15], // Am — A C E
  [7, 9, 11, 13, 15], //    C  — the relative major
  [4, 6, 7, 10, 12, 13], // F  — the flat sixth, the most consoling chord here
  [2, 4, 6, 8, 10, 12], //  Dm — the minor fourth
];

/** how many plucks each chord is held for; four chords make the loop */
const CHORD_LENGTH = 16;

/**
 * The order the notes of a chord are picked in.
 *
 * Deliberately not a rising run. A run is a figure, a figure is recognisable,
 * and once the ear has recognised it it starts predicting it — which is
 * attention, and attention is what this is trying not to ask for. This wanders:
 * it moves up more than down, never covers the chord in order, and never lands
 * on the same note twice running.
 */
const PATTERN = [0, 2, 1, 4, 3, 5, 2, 4, 1, 3, 0, 4, 2, 5, 3, 1];

/**
 * Beats between one pluck and the next. Uneven, and no two adjacent gaps equal,
 * so there is no pulse to lock onto — the notes arrive between eight tenths of a
 * second and two seconds apart, which is slow enough that each is heard as its
 * own event rather than as part of a run.
 */
const GAPS = [3, 2, 4, 2, 3, 5, 2, 3, 4, 2, 3, 3, 5, 2, 4, 3];

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
  /** `step` counts plucks through the whole four-chord loop; `at` is when the
   *  next one is due */
  private arp: { step: number; at: number } | null = null;

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
     * Calm is very largely a question of how much high frequency is asking for
     * attention, and this is the single control that decides that.
     *
     * It sits higher than it did for the pipe. A breathy wind instrument had
     * nothing above two kilohertz but hiss, so it could be shut down hard; a
     * plucked string keeps its identity up there, and cutting it that far turns
     * the arpeggio into a series of dull thumps. Dull is not the same as calm.
     */
    const air = ctx.createBiquadFilter();
    air.type = "lowpass";
    air.frequency.value = 2800;
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

    this.arp = { step: 0, at: ctx.currentTime + 0.2 };
    this.musicTimer = window.setInterval(() => this.scheduleMusic(), 120);
  }

  private scheduleMusic() {
    const ctx = this.ctx;
    const arp = this.arp;
    if (!ctx || !this.musicGain || !arp) return;
    const horizon = ctx.currentTime + 0.5;
    const loop = CHORD_LENGTH * CHORDS.length;

    // a part that has fallen behind (a backgrounded tab) catches up rather than
    // scheduling a burst of notes all in the past
    if (arp.at < ctx.currentTime) arp.at = ctx.currentTime + 0.05;

    while (arp.at < horizon) {
      const chordIdx = Math.floor(arp.step / CHORD_LENGTH);
      const chord = CHORDS[chordIdx]!;
      const i = arp.step % CHORD_LENGTH;

      /*
       * Both the order and the spacing are rotated by a different amount per
       * chord, and by amounts that share no factor with the pattern length. The
       * four chords therefore never pick their notes in the same order or at the
       * same moments, so the twenty seconds each one lasts do not read as a bar
       * being repeated four times.
       */
      const note = PATTERN[(i + chordIdx * 5) % PATTERN.length]! % chord.length;
      this.pluck(SCALE[chord[note]!]!, arp.at);

      arp.at += GAPS[(i + chordIdx * 3) % GAPS.length]! * BEAT;
      arp.step = (arp.step + 1) % loop;
    }
  }

  /**
   * One string, picked and left alone.
   *
   * Warm rather than bright: a triangle fundamental with an octave and a soft
   * twelfth over it, each partial dying sooner than the one below, which is what
   * a real string does and what makes the tone go from ringing to woody as it
   * fades. The attack is twelve milliseconds — a pluck genuinely is a transient
   * and softening it any further stops it being a string at all — but there is
   * no separate pick noise, because with a note starting every second or so a
   * click on each one becomes a rhythm section.
   *
   * Ring and level both fall with pitch. On any real instrument a high string is
   * shorter and quieter than a low one; without that the top of the arpeggio
   * sits on top of everything, and the top of the arpeggio is exactly where the
   * ear goes looking for a melody.
   */
  private pluck(freq: number, when: number) {
    const ctx = this.ctx;
    if (!ctx || !this.musicGain) return;
    const ring = Math.max(2.6, 7.5 - freq / 110);
    const level = 0.075 * Math.min(1, 300 / freq);

    const g = ctx.createGain();
    g.gain.setValueAtTime(0.0001, when);
    g.gain.exponentialRampToValueAtTime(level, when + 0.012);
    g.gain.exponentialRampToValueAtTime(0.0001, when + ring);

    const filt = ctx.createBiquadFilter();
    filt.type = "lowpass";
    filt.frequency.setValueAtTime(Math.min(3200, freq * 6), when);
    // the string goes dull as it decays, where a synth tone would stay bright
    filt.frequency.exponentialRampToValueAtTime(Math.min(1200, freq * 2), when + ring * 0.5);
    filt.Q.value = 0.7;
    filt.connect(g).connect(this.musicGain);

    const o = ctx.createOscillator();
    o.type = "triangle";
    o.frequency.value = freq;
    o.connect(filt);
    o.start(when);
    o.stop(when + ring + 0.05);

    // octave and twelfth: the body of the note for its first second, gone well
    // before the fundamental is
    for (const [mult, amp, life] of [
      [2, 0.3, 0.4],
      [3, 0.12, 0.22],
    ]) {
      const p = ctx.createOscillator();
      p.type = "sine";
      p.frequency.value = freq * mult!;
      const pg = ctx.createGain();
      pg.gain.setValueAtTime(amp!, when);
      pg.gain.exponentialRampToValueAtTime(0.0001, when + ring * life!);
      p.connect(pg).connect(filt);
      p.start(when);
      p.stop(when + ring + 0.05);
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
    this.arp = null;
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
