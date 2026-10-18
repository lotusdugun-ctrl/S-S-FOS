"""master a wav to a target loudness with gentle compression and a limiter
usage: python3 master.py in.wav out.wav [-11]"""
import sys
import numpy as np
import pyloudnorm as pyln
from scipy import signal
from scipy.io import wavfile

sr, x = wavfile.read(sys.argv[1])
x = x.astype(np.float64) / 32768
target = float(sys.argv[3]) if len(sys.argv) > 3 else -11.0

# a slow RMS compressor: evens out the quiet intro and the loud climax
mono = np.sqrt((x ** 2).mean(1))
win = int(0.4 * sr)
env = np.sqrt(signal.fftconvolve(mono ** 2, np.ones(win) / win, mode="same") + 1e-12)
db = 20 * np.log10(env)
thr, ratio = np.percentile(db, 70) - 2, 2.6
gain_db = np.where(db > thr, (thr - db) * (1 - 1 / ratio), 0.0)
# lift what sits well under the threshold, a little, so quiet parts are not lost
gain_db += np.clip((thr - 14 - db) * 0.35, 0, 6)
gain_db = signal.fftconvolve(gain_db, np.ones(win) / win, mode="same")
x = x * (10 ** (gain_db / 20))[:, None]

meter = pyln.Meter(sr)
for _ in range(6):
    lufs = meter.integrated_loudness(x)
    x = x * 10 ** ((target - lufs) / 20)
    # soft limiter at -1 dBFS
    ceil = 10 ** (-1 / 20)
    x = np.tanh(x / ceil) * ceil
print("LUFS", round(meter.integrated_loudness(x), 2), "peak dB", round(20 * np.log10(np.abs(x).max()), 2))
wavfile.write(sys.argv[2], sr, (x * 32767).astype(np.int16))
