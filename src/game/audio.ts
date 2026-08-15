// Procedural, natural-toned audio: wind bed, stone friction, footsteps.
// No external assets; everything is synthesized with the WebAudio API.

/*
 * The music.
 *
 * A syrinx over a plucked kithara and an ison drone, in the Dorian octave
 * species, on a metre made of long and short syllables.
 *
 * It was an aulos before, and an aulos is the wrong Greek instrument for this
 * hillside. It is a double reed: penetrating, nasal, built to be heard across a
 * crowd, and it belonged to Dionysos and to the drama — an instrument that
 * insists. Pan's pipes are the other half of the same tradition and the
 * pastoral one: breath through a stopped tube, nearly a sine wave, as much air
 * as tone. A shepherd on a mountain at dusk is playing the syrinx.
 *
 * Everything else follows from wanting stillness rather than drive. The
 * tympanon is gone entirely — a drum is a pulse, and a pulse is the opposite of
 * calm however sparse you make it. The beat is half again as long, the notes run
 * two to four seconds each, and the rests between phrases are as long as the
 * phrases. The line stays inside the middle of its range instead of climbing to
 * the octave and falling back down, and it comes to rest on the notes the drone
 * is already holding.
 */

/**
 * An anhemitonic pentatonic on A: A C D E G.
 *
 * What was here was the Dorian octave species, which has a flattened second —
 * B♭ over an A drone. That interval is the most recognisable thing about the
 * ancient sound and it is also the most restless one available; held or passed
 * through, it is a semitone grinding against a note that never goes away. The F
 * did the same thing against the E of the drone. Between them they were most of
 * why the piece read as uneasy rather than as still.
 *
 * This scale has no semitone in it at all — that is what anhemitonic means, and
 * it is why some version of these five notes is what nearly every culture
 * reaches for when it wants music to be calm. Against the A–E drone every
 * degree lands as a third, a fourth, a fifth or a seventh. There is no interval
 * left that wants to resolve, so nothing in the melody can create tension the
 * drone then has to hold.
 *
 * It stays rooted on A, so the drone still lands on the same A1–E2–A2 the summit
 * bell tolls and the two fuse rather than collide.
 */
const SCALE = [
  110.0, // 0  A2
  130.81, // 1  C3
  146.83, // 2  D3
  164.81, // 3  E3
  196.0, // 4  G3
  220.0, // 5  A3
  261.63, // 6  C4
  293.66, // 7  D4
  329.63, // 8  E4
  392.0, // 9  G4
  440.0, // 10 A4
];

/** `[degree in SCALE, length in short beats]`; a degree of -1 is a rest */
type Figure = [number, number];

/**
 * The shortest note in the metre — the chronos protos, the unit every other
 * length is a whole multiple of.
 *
 * Greek music took its rhythm from the metre of the verse it set rather than
 * from a bar line, so nothing here sits on an even grid; the phrases breathe at
 * lengths that do not divide into each other. It runs slower again — notes of
 * three to six seconds, where a held note stops being a note you are following
 * and becomes something the room is simply doing.
 */
const BEAT = 0.4;

/**
 * The syrinx line.
 *
 * Three rules make it calm, and all of them are about restraint rather than
 * about notes. It stays between E3 and E4 — the middle of the register, where a
 * pipe is warm — instead of climbing to the octave and dropping the whole way
 * back; a wide arc is a story, and a story is something to follow. It moves by
 * one degree at a time almost everywhere, so no interval ever arrives as an
 * event. And it comes to rest on A, D and E, which is what the drone is already
 * holding, so a held note settles into the drone instead of leaning against it.
 *
 * The rests run four to six and a half seconds, close to twice what they were
 * in real time. That is not padding; on a piece with no pulse, the silence is
 * what the ear rests on, and a phrase should feel like it has been waited for
 * rather than kept up with.
 */
// The line breaks below are the phrases; reflowing them loses the metre.
// prettier-ignore
const SYRINX: Figure[] = [
  [5, 10], [6, 8], [7, 12], [-1, 12],
  [8, 12], [7, 6], [6, 8], [5, 14], [-1, 14],
  [6, 8], [5, 10], [4, 8], [3, 14], [-1, 14],
  [3, 8], [5, 10], [6, 8], [5, 16], [-1, 16],
];

/**
 * The kithara underneath it: single notes left to ring, not a countermelody.
 * Same total length as the syrinx line (208 beats, about eighty-three seconds)
 * so the two stay locked, and long enough that the loop does not announce
 * itself.
 *
 * Every note is A, E or C — the drone's own pitches and the third above them.
 * Nothing here is trying to move the harmony anywhere, because there is nowhere
 * it needs to go. There is more silence than string now: one note every ten
 * seconds or so, which is sparse enough that each is heard as a single event
 * decaying rather than as a part being played.
 */
// prettier-ignore
const LYRE: Figure[] = [
  [5, 14], [-1, 12], [3, 12], [-1, 14],
  [0, 16], [-1, 12], [5, 12], [-1, 12],
  [3, 14], [-1, 12], [6, 12], [-1, 14],
  [0, 16], [-1, 12], [3, 12], [-1, 12],
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

  /** soft echo bus so the lyre rings out into the dusk */
  private startMusic() {
    const ctx = this.ctx;
    if (!ctx || !this.master) return;

    this.musicGain = ctx.createGain();
    this.musicGain.gain.value = 0.32;

    /*
     * One filter across the whole of the music, before anything else touches it.
     *
     * Nothing in this piece has anything to say above about two kilohertz — the
     * top end was breath hiss, string attack and the harsh edge of the echo
     * repeats, which is to say it was all the parts that made it tiring. Rolling
     * it off is the single change that does the most, because "calm" is very
     * largely a question of how much high frequency is asking for attention.
     */
    const air = ctx.createBiquadFilter();
    air.type = "lowpass";
    air.frequency.value = 2000;
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

    const [syrinx, lyre] = this.voices;
    if (syrinx) run(syrinx, SYRINX, (d, when, dur) => this.syrinx(SCALE[d]!, when, dur));
    if (lyre) run(lyre, LYRE, (d, when, dur) => this.lyre(SCALE[d]!, when, dur));
  }

  /**
   * The syrinx: breath across the lip of a stopped tube. Where the aulos had a
   * reed beating between the player and the pipe, here nothing vibrates but the
   * air itself, and the whole character of the instrument follows from that.
   *
   * A stopped tube sounds odd harmonics only, and weakly — so the tone is a sine
   * with a soft third above it and nothing else, which is as close to a pure
   * pipe as this gets without sounding like a test tone. The breath is not an
   * attack transient but a voice: it arrives before the pitch does and stays
   * audible under the whole note, because on a pipe this quiet the air *is* half
   * the sound. And it speaks slowly — a reed cracks into life, a lip tone has to
   * be found, so the tone takes a good tenth of a second to arrive under the
   * breath that is already there.
   */
  private syrinx(freq: number, when: number, dur: number) {
    const ctx = this.ctx;
    if (!ctx || !this.musicGain) return;
    const end = when + dur;

    // tone and breath share one envelope, so the pipe speaks and stops as one
    // thing rather than as two sources that happen to overlap
    const g = ctx.createGain();
    g.gain.setValueAtTime(0.0001, when);
    // slower in and much slower out. A note that fades over most of a second
    // has no moment at which it stops, and a sound with no ending is not
    // something the ear has to keep track of
    g.gain.exponentialRampToValueAtTime(0.13, when + 0.22);
    // eases back off the first push, the way a held breath does
    g.gain.setTargetAtTime(0.09, when + 0.3, 0.7);
    g.gain.setTargetAtTime(0.0001, Math.max(when + 0.4, end - 0.55), 0.2);
    g.connect(this.musicGain);

    /*
     * Vibrato, and much later and shallower than the aulos had it. On these
     * notes — two to four seconds each — a player would let the note sit before
     * touching it at all, and a pipe vibrato is a waver in the breath rather
     * than a lip bending the pitch, so it stays under half the depth.
     */
    const vib = ctx.createOscillator();
    vib.frequency.value = 4.6;
    const vibAmt = ctx.createGain();
    vibAmt.gain.setValueAtTime(0, when);
    vibAmt.gain.setTargetAtTime(freq * 0.0035, when + 0.55, 0.7);
    vib.connect(vibAmt);
    vib.start(when);
    vib.stop(end + 0.3);

    const o = ctx.createOscillator();
    o.type = "sine";
    o.frequency.value = freq;
    vibAmt.connect(o.frequency);
    o.connect(g);
    o.start(when);
    o.stop(end + 0.3);

    // the odd harmonic a stopped pipe gives: quiet, late, and fading back as the
    // breath settles — it is what keeps the sine from reading as a synth
    const third = ctx.createOscillator();
    third.type = "sine";
    third.frequency.value = freq * 3;
    vibAmt.connect(third.frequency);
    const tg = ctx.createGain();
    tg.gain.setValueAtTime(0.0001, when);
    tg.gain.exponentialRampToValueAtTime(0.045, when + 0.2);
    tg.gain.setTargetAtTime(0.014, when + 0.4, 1.1);
    third.connect(tg).connect(g);
    third.start(when);
    third.stop(end + 0.3);

    /*
     * The breath: noise around the second partial, held for the length of the
     * note. It comes up in half the time the tone does, so every note begins as
     * air and only then finds its pitch — which is the one thing that separates
     * a pipe from an organ.
     *
     * At a third of the level it had. Breath is what makes this a pipe, but a
     * loud continuous hiss is also just a loud continuous hiss, and there is
     * already a wind bed underneath doing that job for the whole scene. It needs
     * to be present, not audible on its own.
     */
    if (this.noiseBuffer) {
      const breath = ctx.createBufferSource();
      breath.buffer = this.noiseBuffer;
      breath.loop = true;
      breath.playbackRate.value = 1.1;
      const bp = ctx.createBiquadFilter();
      bp.type = "bandpass";
      bp.frequency.value = Math.min(6000, freq * 2);
      // wide enough to stay air rather than becoming a second, whistling pitch
      bp.Q.value = 1.5;
      const ag = ctx.createGain();
      ag.gain.setValueAtTime(0.0001, when);
      ag.gain.exponentialRampToValueAtTime(0.038, when + 0.1);
      // and drops back once the tone is there, without ever leaving
      ag.gain.setTargetAtTime(0.016, when + 0.22, 0.6);
      breath.connect(bp).connect(ag).connect(g);
      breath.start(when);
      breath.stop(end + 0.3);
    }
  }

  /**
   * The kithara under it. A gut string picked with a plektron: a body that is
   * mostly fundamental with a ringing octave over it, decaying long.
   *
   * Played softly and let ring, rather than picked. A hard pick is a transient,
   * a transient is an event, and an event is a thing that arrives — which is
   * most of what a plucked string can do to disturb a piece like this. So the
   * attack is thirty milliseconds instead of six, the string is dull from the
   * start rather than bright and decaying to dull, and it rings for at least
   * three seconds however short its written length.
   */
  private lyre(freq: number, when: number, dur: number) {
    const ctx = this.ctx;
    if (!ctx || !this.musicGain) return;
    const ring = Math.max(dur, 3);

    const g = ctx.createGain();
    g.gain.setValueAtTime(0.0001, when);
    g.gain.exponentialRampToValueAtTime(0.1, when + 0.03);
    g.gain.exponentialRampToValueAtTime(0.0001, when + ring);
    const filt = ctx.createBiquadFilter();
    filt.type = "lowpass";
    filt.frequency.setValueAtTime(Math.min(2600, freq * 5), when);
    // gut goes dull as it decays, where a synth tone would stay bright
    filt.frequency.exponentialRampToValueAtTime(Math.min(1100, freq * 2), when + ring * 0.6);
    filt.Q.value = 0.8;
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

    // the plektron on the string — barely there, and low enough that it reads as
    // the string taking hold rather than as a click on top of it
    if (this.noiseBuffer) {
      const pick = ctx.createBufferSource();
      pick.buffer = this.noiseBuffer;
      pick.playbackRate.value = 1.2;
      const bp = ctx.createBiquadFilter();
      bp.type = "bandpass";
      bp.frequency.value = Math.min(1800, freq * 3);
      bp.Q.value = 0.8;
      const pg = ctx.createGain();
      pg.gain.setValueAtTime(0.018, when);
      pg.gain.exponentialRampToValueAtTime(0.0001, when + 0.05);
      pick.connect(bp).connect(pg).connect(this.musicGain);
      pick.start(when);
      pick.stop(when + 0.07);
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
