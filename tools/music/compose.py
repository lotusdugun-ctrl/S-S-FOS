# Composes and renders the game's score (unity/Assets/Resources/Audio/music.mp3, public/music.mp3):
#   python3 compose.py score.wav && ffmpeg -i score.wav -b:a 192k music.mp3
# An original score for Sisyphus, synthesized from scratch (no samples, no recordings).
# D minor, 72 bpm, 36 bars (120 s), rendered as a seamless loop.
import numpy as np, sys
from scipy.signal import lfilter, fftconvolve, butter, sosfilt, resample_poly

SR = 44100
BPM = 72
BEAT = 60 / BPM
BAR = 4 * BEAT
BARS = 36
LEN = BARS * BAR
TAIL = 10.0
N = int((LEN + TAIL) * SR)
rng = np.random.default_rng(7)

def hz(n):  # midi -> hz
    return 440.0 * 2 ** ((n - 69) / 12)

NOTE = {'C':0,'C#':1,'Db':1,'D':2,'D#':3,'Eb':3,'E':4,'F':5,'F#':6,'Gb':6,'G':7,'G#':8,'Ab':8,'A':9,'A#':10,'Bb':10,'B':11}
def m(s):
    name = s[:-1]; octv = int(s[-1])
    return 12 * (octv + 1) + NOTE[name]

L = np.zeros(N); R = np.zeros(N)
busses = {k: (np.zeros(N), np.zeros(N)) for k in ['piano','harp','strings','cello','perc','choir']}

def place(bus, sig, t, pan=0.0, gain=1.0):
    i = int(t * SR)
    if i >= N: return
    sig = sig[: N - i]
    l = np.cos((pan + 1) * np.pi / 4); r = np.sin((pan + 1) * np.pi / 4)
    busses[bus][0][i:i+len(sig)] += sig * gain * l
    busses[bus][1][i:i+len(sig)] += sig * gain * r

def env_adsr(n, a, d, s, rel, dur):
    t = np.arange(n) / SR
    e = np.ones(n) * s
    e[t < a] = t[t < a] / a
    m_ = (t >= a) & (t < a + d)
    e[m_] = 1 - (1 - s) * (t[m_] - a) / d
    off = t >= dur
    e[off] = s * np.exp(-(t[off] - dur) / rel)
    return e

# ---------------------------------------------------------------- instruments
def piano(note, dur, vel=0.7):
    f = hz(note); n = int((dur + 3.0) * SR); t = np.arange(n) / SR
    B = 0.0003
    out = np.zeros(n)
    for k in range(1, 12):
        fk = k * f * np.sqrt(1 + B * k * k)
        if fk > 12000: break
        amp = (1 / k ** 1.3) * (1 + 0.6 * vel * (k > 3))
        dec = np.exp(-t * (0.45 + 0.35 * k) * (f / 260) ** 0.4)
        out += amp * dec * np.sin(2 * np.pi * fk * t + rng.random() * 6.28)
    # second string, slightly detuned: the beating of a real piano
    out += 0.35 * np.sin(2 * np.pi * f * 1.0012 * t) * np.exp(-t * 0.5)
    att = np.minimum(1, t / 0.004)
    rel = np.where(t > dur, np.exp(-(t - dur) / 0.35), 1)
    hammer = rng.standard_normal(n) * np.exp(-t / 0.006) * 0.15
    s = (out * att * rel + hammer) * vel
    return s / 3.0

def harp(note, vel=0.6, ring=2.5):
    # Karplus-Strong at twice the rate (finer tuning), then down
    sr2 = SR * 2
    f = hz(note); Nd = sr2 / f
    D = int(np.floor(Nd - 0.5)); n = int(ring * sr2)
    x = np.zeros(n); burst = rng.standard_normal(D + 1)
    burst = lfilter([0.6, 0.4], [1], burst)
    x[:D+1] = burst
    g = 0.9985 - 0.0008 * (note - 60) / 12
    a = np.zeros(D + 2); a[0] = 1; a[D] = -g * 0.5; a[D + 1] = -g * 0.5
    y = lfilter([1], a, x)
    y = resample_poly(y, 1, 2)
    t = np.arange(len(y)) / SR
    y *= np.minimum(1, t / 0.002)
    return y * vel * 0.5

def saw_add(f, t, maxf=6000, phase=None):
    out = np.zeros_like(t)
    K = int(maxf // f)
    for k in range(1, max(2, K) + 1):
        out += np.sin(2 * np.pi * k * f * t + (0 if phase is None else phase * k)) / k
    return out

def strings(note, dur, vel=0.5, att=1.2, rel=1.8, voices=5, maxf=5000, vib=0.0):
    f = hz(note); n = int((dur + rel * 3) * SR); t = np.arange(n) / SR
    out = np.zeros(n)
    for v in range(voices):
        det = 1 + (v - (voices - 1) / 2) * 0.0035
        vibr = 1 + vib * np.sin(2 * np.pi * (5 + 0.4 * v) * t + v) * np.minimum(1, t / 1.5)
        ph = np.cumsum(2 * np.pi * f * det * vibr / SR)
        K = int(maxf // f)
        sig = np.zeros(n)
        for k in range(1, max(2, K) + 1):
            sig += np.sin(k * ph + rng.random() * 6.28) / k
        out += sig
    e = env_adsr(n, att, 0.5, 0.85, rel, dur)
    sos = butter(2, min(0.95, 2600 / (SR / 2)), output='sos')
    out = sosfilt(sos, out)
    return out * e * vel / voices * 0.5

def choir(note, dur, vel=0.4):
    # vowel 'ah': stacked partials shaped by formants
    f = hz(note); n = int((dur + 3) * SR); t = np.arange(n) / SR
    out = np.zeros(n)
    form = [(700, 110), (1220, 120), (2600, 160)]
    for v in range(4):
        det = 1 + (v - 1.5) * 0.004
        vib = 1 + 0.004 * np.sin(2 * np.pi * (4.6 + 0.3 * v) * t + v)
        ph = np.cumsum(2 * np.pi * f * det * vib / SR)
        for k in range(1, int(4000 // f) + 1):
            fk = k * f
            a = sum(np.exp(-((fk - c) ** 2) / (2 * bw ** 2)) * w for (c, bw), w in zip(form, [1, 0.5, 0.25])) + 0.05 / k
            out += a * np.sin(k * ph + rng.random() * 6.28)
    e = env_adsr(n, 1.8, 0.6, 0.9, 2.0, dur)
    return out * e * vel / 4 * 0.35

def timpani(note, vel=0.6):
    f = hz(note); n = int(3.5 * SR); t = np.arange(n) / SR
    fr = f * (1 + 0.08 * np.exp(-t / 0.05))
    ph = np.cumsum(2 * np.pi * fr / SR)
    s = (np.sin(ph) + 0.5 * np.sin(1.5 * ph) * np.exp(-t * 3) + 0.3 * np.sin(2 * ph) * np.exp(-t * 4)) * np.exp(-t * 1.1)
    s += rng.standard_normal(n) * np.exp(-t / 0.02) * 0.2
    sos = butter(2, 900 / (SR / 2), output='sos')
    return sosfilt(sos, s) * vel * 0.6

# ---------------------------------------------------------------- the piece
# chords per bar (root-position voicings in the strings, bass in the cello)
prog_A = [('D', ['D3','F3','A3','D4']), ('Bb', ['Bb2','D3','F3','Bb3']), ('F', ['F2','A2','C3','F3']), ('C', ['C3','E3','G3','C4'])]
prog_B = [('Gm', ['G2','Bb2','D3','G3']), ('D', ['D3','F3','A3','D4']), ('Bb', ['Bb2','D3','F3','Bb3']), ('A', ['A2','C#3','E3','A3'])]
plan = []  # (section, chord)
for _ in range(1): plan += [('intro', c) for c in prog_A]          # 1-4
for _ in range(2): plan += [('A', c) for c in prog_A]              # 5-12
for _ in range(2): plan += [('B', c) for c in prog_B]              # 13-20
for _ in range(2): plan += [('A2', c) for c in prog_A]             # 21-28
plan += [('outro', c) for c in prog_A]                              # 29-32
plan += [('outro2', c) for c in [prog_A[0], prog_A[1], prog_B[0], prog_B[3]]]  # 33-36, A leads home to Dm
assert len(plan) == BARS

# melody (bar index -> list of (beat, note, beats))
mel_A = [
    [(0,'A4',1.5),(1.5,'D5',0.5),(2,'C5',1),(3,'A4',1)],
    [(0,'Bb4',2),(2,'A4',1),(3,'F4',1)],
    [(0,'A4',1.5),(1.5,'G4',0.5),(2,'F4',1),(3,'E4',1)],
    [(0,'G4',3),(3,'E4',1)],
    [(0,'F4',1),(1,'A4',1),(2,'D5',1.5),(3.5,'E5',0.5)],
    [(0,'F5',2),(2,'D5',1),(3,'Bb4',1)],
    [(0,'C5',1.5),(1.5,'A4',0.5),(2,'F4',1),(3,'G4',1)],
    [(0,'E4',4)],
]
mel_B = [
    [(0,'D5',2),(2,'Bb4',1),(3,'G4',1)],
    [(0,'A4',3),(3,'F4',1)],
    [(0,'D5',1),(1,'F5',1),(2,'E5',1),(3,'D5',1)],
    [(0,'C#5',4)],
    [(0,'D5',1.5),(1.5,'E5',0.5),(2,'F5',1),(3,'G5',1)],
    [(0,'A5',2),(2,'F5',1),(3,'D5',1)],
    [(0,'F5',1.5),(1.5,'E5',0.5),(2,'D5',1),(3,'Bb4',1)],
    [(0,'A4',2),(2,'C#5',2)],
]

for bi, (sec, (cname, chord)) in enumerate(plan):
    t0 = bi * BAR
    notes = [m(x) for x in chord]
    bass = notes[0] - 12
    # strings pad: everywhere, louder in B
    padv = {'intro':0.35,'A':0.45,'B':0.7,'A2':0.6,'outro':0.45,'outro2':0.35}[sec]
    for k, nn in enumerate(notes[1:]):
        place('strings', strings(nn, BAR * 0.98, padv, att=1.0, rel=1.4, vib=0.003), t0, pan=-0.5 + 0.5 * k, gain=1)
    # cello bass
    cv = {'intro':0.0,'A':0.55,'B':0.8,'A2':0.75,'outro':0.5,'outro2':0.45}[sec]
    if cv > 0:
        place('cello', strings(bass, BAR * 0.95, cv, att=0.4, rel=1.2, voices=3, maxf=2500, vib=0.004), t0, pan=-0.15)
    # harp arpeggio, eighths
    hv = {'intro':0.0,'A':0.5,'B':0.55,'A2':0.6,'outro':0.45,'outro2':0.3}[sec]
    if hv > 0:
        arp = [notes[0]+12, notes[1]+12, notes[2]+12, notes[3]+12, notes[2]+12, notes[1]+12, notes[3]+12, notes[2]+12]
        for e in range(8):
            if sec == 'outro2' and e % 2: continue
            place('harp', harp(arp[e], hv * (1.0 if e % 2 == 0 else 0.8)), t0 + e * BEAT / 2 + rng.normal(0, 0.006), pan=0.35)
    # piano: chord on beat 1 in the intro and outros, melody in A sections
    if sec in ('intro', 'outro', 'outro2'):
        for k, nn in enumerate(notes):
            place('piano', piano(nn + 12, BAR * 0.9, 0.45), t0 + k * 0.045, pan=0.1)
        if sec == 'intro' and bi % 2 == 1:
            place('piano', piano(notes[3] + 24, BEAT * 2, 0.35), t0 + 2 * BEAT, pan=0.2)
    if sec in ('A', 'A2'):
        idx = (bi - (4 if sec == 'A' else 20)) % 8
        for beat, nn, ln in mel_A[idx]:
            place('piano', piano(m(nn), ln * BEAT, 0.62 if sec == 'A' else 0.55), t0 + beat * BEAT + rng.normal(0, 0.004), pan=0.15)
        if sec == 'A2':  # the violins take the tune an octave up, softly
            for beat, nn, ln in mel_A[idx]:
                place('strings', strings(m(nn) + 12, ln * BEAT, 0.28, att=0.25, rel=0.6, voices=4, maxf=7000, vib=0.006), t0 + beat * BEAT, pan=0.3)
    if sec == 'B':
        idx = (bi - 12) % 8
        for beat, nn, ln in mel_B[idx]:
            place('strings', strings(m(nn), ln * BEAT, 0.55, att=0.18, rel=0.7, voices=5, maxf=7000, vib=0.007), t0 + beat * BEAT, pan=0.2)
        for nn in notes[1:3]:
            place('choir', choir(nn + 12, BAR * 0.95, 0.5), t0, pan=-0.3)
    # timpani on section starts and the climax
    if bi in (4, 12, 16, 20):
        place('perc', timpani(m('D2') if cname != 'Gm' else m('G2'), 0.7), t0, pan=0)
    if sec == 'B' and bi % 2 == 1:
        place('perc', timpani(bass + 12 if bass < 40 else bass, 0.35), t0 + 3 * BEAT, pan=0)

# ---------------------------------------------------------------- mix
mix = {'piano':0.9,'harp':0.55,'strings':0.85,'cello':0.8,'perc':0.6,'choir':0.6}
for k, (bl, br) in busses.items():
    L += bl * mix[k]; R += br * mix[k]

# reverb: a hall from decaying noise, decorrelated per side
def ir(sec_len=3.2, seed=1):
    r = np.random.default_rng(seed)
    n = int(sec_len * SR); t = np.arange(n) / SR
    x = r.standard_normal(n) * np.exp(-t * 6.9 / sec_len)
    sos = butter(1, 6000 / (SR / 2), output='sos')
    x = sosfilt(sos, x)
    x[: int(0.02 * SR)] *= np.linspace(0, 1, int(0.02 * SR))
    return x / np.sqrt(np.sum(x ** 2))
wetL = fftconvolve(L, ir(3.2, 1))[:N]; wetR = fftconvolve(R, ir(3.2, 2))[:N]
L = L * 0.75 + wetL * 0.55; R = R * 0.75 + wetR * 0.55

# a gentle low cut and high shelf
sos = butter(2, 35 / (SR / 2), btype='high', output='sos')
L = sosfilt(sos, L); R = sosfilt(sos, R)

# seamless loop: what rings past the end is folded onto the beginning
n0 = int(LEN * SR)
L[:N - n0] += L[n0:]; R[:N - n0] += R[n0:]
L = L[:n0]; R = R[:n0]

st = np.stack([L, R], 1)
st /= np.max(np.abs(st)) / 0.89
# soft limiter
st = np.tanh(st * 1.1) / np.tanh(1.1)
from scipy.io import wavfile
wavfile.write(sys.argv[1], SR, (st * 32767).astype(np.int16))
print('ok', st.shape[0] / SR)
