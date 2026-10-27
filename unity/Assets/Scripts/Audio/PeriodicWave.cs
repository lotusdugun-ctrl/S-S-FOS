// The band-limited wavetables behind OscillatorNode, built the way Chromium's
// PeriodicWave does: the Fourier series of the basic shape, one table per
// third of an octave with the partials above Nyquist culled, all normalised
// by the peak of the fullest table. The oscillator crossfades between the two
// tables around its pitch.

using System;
using System.Collections.Generic;

namespace Sisifos.Audio
{
    public sealed class PeriodicWave
    {
        const int kNumberOfOctaveBands = 3;
        const float kCentsPerRange = 1200f / kNumberOfOctaveBands;

        internal readonly float sampleRate;
        internal readonly int periodicWaveSize;
        internal readonly float rateScale;
        internal readonly float lowestFundamentalFrequency;
        internal readonly int numberOfRanges;
        internal readonly float[][] bandLimitedTables;

        static readonly Dictionary<string, PeriodicWave> cache = new Dictionary<string, PeriodicWave>();

        /// <summary>The shared table set for a basic oscillator type ("sine", "square", "sawtooth", "triangle").</summary>
        internal static PeriodicWave Basic(string type, float sampleRate)
        {
            string key = type + "@" + sampleRate.ToString(System.Globalization.CultureInfo.InvariantCulture);
            lock (cache)
            {
                PeriodicWave w;
                if (cache.TryGetValue(key, out w)) return w;
                int size = SizeFor(sampleRate);
                int half = size / 2;
                var real = new float[half];
                var imag = new float[half];
                for (int n = 1; n < half; n++)
                {
                    float piFactor = 2 / (n * (float)Math.PI);
                    float b;
                    switch (type)
                    {
                        case "square": b = piFactor * ((n & 1) != 0 ? 2 : 0); break;
                        case "sawtooth": b = piFactor * ((n & 1) != 0 ? 1 : -1); break;
                        case "triangle":
                            if ((n & 1) != 0) b = 2 * (piFactor * piFactor) * ((((n - 1) >> 1) & 1) != 0 ? -1 : 1);
                            else b = 0;
                            break;
                        default: b = n == 1 ? 1 : 0; break; // sine
                    }
                    real[n] = 0;
                    imag[n] = b;
                }
                w = new PeriodicWave(sampleRate, real, imag, false);
                cache[key] = w;
                return w;
            }
        }

        static int SizeFor(float sampleRate)
        {
            if (sampleRate <= 24000) return 2048;
            if (sampleRate <= 88200) return 4096;
            return 16384;
        }

        /// <summary>A custom wave (BaseAudioContext.createPeriodicWave).</summary>
        public PeriodicWave(float sampleRate, float[] real, float[] imag, bool disableNormalization)
        {
            this.sampleRate = sampleRate;
            periodicWaveSize = SizeFor(sampleRate);
            float nyquist = 0.5f * sampleRate;
            lowestFundamentalFrequency = nyquist / (periodicWaveSize / 2);
            rateScale = periodicWaveSize / sampleRate;
            numberOfRanges = (int)(0.5f + kNumberOfOctaveBands * (float)(Math.Log(periodicWaveSize) / Math.Log(2)));
            bandLimitedTables = new float[numberOfRanges][];
            CreateBandLimitedTables(real, imag, Math.Min(real.Length, imag.Length), disableNormalization);
        }

        int MaxNumberOfPartials() { return periodicWaveSize / 2; }

        int NumberOfPartialsForRange(int rangeIndex)
        {
            float centsToCull = rangeIndex * kCentsPerRange;
            float cullingScale = (float)Math.Pow(2, -centsToCull / 1200);
            return (int)(cullingScale * MaxNumberOfPartials());
        }

        void CreateBandLimitedTables(float[] realData, float[] imagData, int numberOfComponents, bool disableNormalization)
        {
            float normalizationScale = 0.5f;
            int fftSize = periodicWaveSize;
            int halfSize = fftSize / 2;
            numberOfComponents = Math.Min(numberOfComponents, halfSize);

            var re = new double[fftSize];
            var im = new double[fftSize];
            for (int rangeIndex = 0; rangeIndex < numberOfRanges; rangeIndex++)
            {
                int numberOfPartials = NumberOfPartialsForRange(rangeIndex);
                int last = Math.Min(numberOfComponents, numberOfPartials + 1);
                // x[k] = sum_n a_n cos(2 pi n k / N) + b_n sin(2 pi n k / N), n = 1 .. last-1
                // (DC and the packed Nyquist bin cleared, as Chrome does)
                Array.Clear(re, 0, fftSize);
                Array.Clear(im, 0, fftSize);
                for (int n = 1; n < last; n++)
                {
                    re[n] = realData[n];
                    im[n] = -imagData[n];
                }
                // inverse DFT of the one-sided spectrum: y[k] = sum c[n] e^{+i..}; with c = a - i b,
                // Re(y) = sum a cos + b sin
                InverseFft(re, im);
                var table = new float[fftSize];
                for (int k = 0; k < fftSize; k++) table[k] = (float)re[k];

                if (!disableNormalization && rangeIndex == 0)
                {
                    float maxValue = 0;
                    for (int k = 0; k < fftSize; k++) maxValue = Math.Max(maxValue, Math.Abs(table[k]));
                    if (maxValue != 0) normalizationScale = 1.0f / maxValue;
                }
                for (int k = 0; k < fftSize; k++) table[k] *= normalizationScale;
                bandLimitedTables[rangeIndex] = table;
            }
        }

        /// <summary>In-place unscaled inverse complex FFT (radix 2).</summary>
        static void InverseFft(double[] re, double[] im)
        {
            int n = re.Length;
            for (int i = 1, j = 0; i < n; i++)
            {
                int bit = n >> 1;
                for (; (j & bit) != 0; bit >>= 1) j ^= bit;
                j ^= bit;
                if (i < j)
                {
                    double t = re[i]; re[i] = re[j]; re[j] = t;
                    t = im[i]; im[i] = im[j]; im[j] = t;
                }
            }
            for (int len = 2; len <= n; len <<= 1)
            {
                int halfLen = len >> 1;
                double ang = 2 * Math.PI / len;
                for (int i = 0; i < n; i += len)
                {
                    for (int k = 0; k < halfLen; k++)
                    {
                        double c = Math.Cos(ang * k), s = Math.Sin(ang * k);
                        int a = i + k, b = a + halfLen;
                        double xr = re[b] * c - im[b] * s;
                        double xi = re[b] * s + im[b] * c;
                        re[b] = re[a] - xr; im[b] = im[a] - xi;
                        re[a] += xr; im[a] += xi;
                    }
                }
            }
        }

        internal void WaveDataForFundamentalFrequency(float fundamentalFrequency, out float[] lowerWaveData, out float[] higherWaveData, out float tableInterpolationFactor)
        {
            fundamentalFrequency = Math.Abs(fundamentalFrequency);
            float ratio = fundamentalFrequency > 0 ? fundamentalFrequency / lowestFundamentalFrequency : 0.5f;
            float centsAboveLowestFrequency = (float)(Math.Log(ratio) / Math.Log(2)) * 1200;
            float pitchRange = 1 + centsAboveLowestFrequency / kCentsPerRange;
            pitchRange = Math.Max(pitchRange, 0.0f);
            pitchRange = Math.Min(pitchRange, (float)(numberOfRanges - 1));
            int rangeIndex1 = (int)pitchRange;
            int rangeIndex2 = rangeIndex1 < numberOfRanges - 1 ? rangeIndex1 + 1 : rangeIndex1;
            lowerWaveData = bandLimitedTables[rangeIndex2];
            higherWaveData = bandLimitedTables[rangeIndex1];
            tableInterpolationFactor = pitchRange - rangeIndex1;
        }
    }
}
