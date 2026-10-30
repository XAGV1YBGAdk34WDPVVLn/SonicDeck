using System.Numerics;
using SonicDeck.Models;

namespace SonicDeck.Audio.Dsp;

public struct BiquadCoeffs
{
    public double B0, B1, B2, A1, A2;

    public static BiquadCoeffs Identity => new() { B0 = 1.0, B1 = 0.0, B2 = 0.0, A1 = 0.0, A2 = 0.0 };

    public static BiquadCoeffs CreatePeaking(double sampleRate, double freq, double q, double gainDb)
    {
        if (Math.Abs(gainDb) < 0.01 || freq <= 0 || freq >= sampleRate * 0.49)
            return Identity;

        double a = Math.Pow(10.0, gainDb / 40.0);
        double w0 = 2.0 * Math.PI * freq / sampleRate;
        double cosW0 = Math.Cos(w0);
        double alpha = Math.Sin(w0) / (2.0 * Math.Max(0.1, q));

        double b0 = 1.0 + alpha * a;
        double b1 = -2.0 * cosW0;
        double b2 = 1.0 - alpha * a;
        double a0 = 1.0 + alpha / a;
        double a1 = -2.0 * cosW0;
        double a2 = 1.0 - alpha / a;

        return new BiquadCoeffs
        {
            B0 = b0 / a0,
            B1 = b1 / a0,
            B2 = b2 / a0,
            A1 = a1 / a0,
            A2 = a2 / a0
        };
    }

    public static BiquadCoeffs CreateLowShelf(double sampleRate, double freq, double q, double gainDb)
    {
        if (Math.Abs(gainDb) < 0.01 || freq <= 0 || freq >= sampleRate * 0.49)
            return Identity;

        double a = Math.Pow(10.0, gainDb / 40.0);
        double w0 = 2.0 * Math.PI * freq / sampleRate;
        double cosW0 = Math.Cos(w0);
        double sinW0 = Math.Sin(w0);
        double alpha = sinW0 / (2.0 * Math.Max(0.1, q));
        double twoSqrtAAlpha = 2.0 * Math.Sqrt(a) * alpha;

        double b0 = a * ((a + 1.0) - (a - 1.0) * cosW0 + twoSqrtAAlpha);
        double b1 = 2.0 * a * ((a - 1.0) - (a + 1.0) * cosW0);
        double b2 = a * ((a + 1.0) - (a - 1.0) * cosW0 - twoSqrtAAlpha);
        double a0 = (a + 1.0) + (a - 1.0) * cosW0 + twoSqrtAAlpha;
        double a1 = -2.0 * ((a - 1.0) + (a + 1.0) * cosW0);
        double a2 = (a + 1.0) + (a - 1.0) * cosW0 - twoSqrtAAlpha;

        return new BiquadCoeffs
        {
            B0 = b0 / a0,
            B1 = b1 / a0,
            B2 = b2 / a0,
            A1 = a1 / a0,
            A2 = a2 / a0
        };
    }

    public static BiquadCoeffs CreateHighShelf(double sampleRate, double freq, double q, double gainDb)
    {
        if (Math.Abs(gainDb) < 0.01 || freq <= 0 || freq >= sampleRate * 0.49)
            return Identity;

        double a = Math.Pow(10.0, gainDb / 40.0);
        double w0 = 2.0 * Math.PI * freq / sampleRate;
        double cosW0 = Math.Cos(w0);
        double sinW0 = Math.Sin(w0);
        double alpha = sinW0 / (2.0 * Math.Max(0.1, q));
        double twoSqrtAAlpha = 2.0 * Math.Sqrt(a) * alpha;

        double b0 = a * ((a + 1.0) + (a - 1.0) * cosW0 + twoSqrtAAlpha);
        double b1 = -2.0 * a * ((a - 1.0) + (a + 1.0) * cosW0);
        double b2 = a * ((a + 1.0) + (a - 1.0) * cosW0 - twoSqrtAAlpha);
        double a0 = (a + 1.0) - (a - 1.0) * cosW0 + twoSqrtAAlpha;
        double a1 = 2.0 * ((a - 1.0) - (a + 1.0) * cosW0);
        double a2 = (a + 1.0) - (a - 1.0) * cosW0 - twoSqrtAAlpha;

        return new BiquadCoeffs
        {
            B0 = b0 / a0,
            B1 = b1 / a0,
            B2 = b2 / a0,
            A1 = a1 / a0,
            A2 = a2 / a0
        };
    }

    public static BiquadCoeffs CreateBandPass(double sampleRate, double freq, double q)
    {
        if (freq <= 0 || freq >= sampleRate * 0.49)
            return Identity;

        double w0 = 2.0 * Math.PI * freq / sampleRate;
        double cosW0 = Math.Cos(w0);
        double sinW0 = Math.Sin(w0);
        double alpha = sinW0 / (2.0 * Math.Max(0.15, q));

        double b0 = alpha;
        double b1 = 0.0;
        double b2 = -alpha;
        double a0 = 1.0 + alpha;
        double a1 = -2.0 * cosW0;
        double a2 = 1.0 - alpha;

        return new BiquadCoeffs
        {
            B0 = b0 / a0,
            B1 = b1 / a0,
            B2 = b2 / a0,
            A1 = a1 / a0,
            A2 = a2 / a0
        };
    }

    public double EvaluateMagnitudeLinear(double freq, double sampleRate)
    {
        double w = 2.0 * Math.PI * freq / sampleRate;
        Complex z1 = Complex.FromPolarCoordinates(1.0, -w);
        Complex z2 = Complex.FromPolarCoordinates(1.0, -2.0 * w);
        Complex num = B0 + B1 * z1 + B2 * z2;
        Complex den = 1.0 + A1 * z1 + A2 * z2;
        double mag = (num / den).Magnitude;
        return double.IsNaN(mag) || double.IsInfinity(mag) ? 1.0 : mag;
    }
}

public class BiquadEqChain
{
    private const int TotalStages = 12; // 10 bands + 1 sub-bass shelf + 1 treble tilt shelf
    private readonly BiquadCoeffs[] _coeffs = new BiquadCoeffs[TotalStages];
    private readonly double[,] _z1 = new double[2, TotalStages];
    private readonly double[,] _z2 = new double[2, TotalStages];

    // Solo / Audition Band-Pass Filter state (Shift + Drag on an EQ node)
    private BiquadCoeffs _soloCoeffs = BiquadCoeffs.Identity;
    private readonly double[] _soloZ1 = new double[2];
    private readonly double[] _soloZ2 = new double[2];
    private int _soloBandIndex = -1;

    private readonly object _lock = new();

    private double _sampleRate = 48000;
    private bool _isEnabled = true;
    private double _preampLinear = 1.0;
    private double _preampDb = 0.0;
    private double _balance = 0.0; // -1.0 L to +1.0 R
    private bool _softLimiter = true;

    public BiquadEqChain()
    {
        for (int i = 0; i < TotalStages; i++)
            _coeffs[i] = BiquadCoeffs.Identity;
    }

    public bool IsEnabled
    {
        get => _isEnabled;
        set => _isEnabled = value;
    }

    public int SoloBandIndex => _soloBandIndex;

    public void SetSoloBand(int bandIndex, double freq = 1000.0, double q = 2.0)
    {
        lock (_lock)
        {
            _soloBandIndex = bandIndex;
            if (bandIndex >= 0)
            {
                _soloCoeffs = BiquadCoeffs.CreateBandPass(_sampleRate, freq, Math.Max(0.6, q));
            }
            else
            {
                _soloCoeffs = BiquadCoeffs.Identity;
                Array.Clear(_soloZ1, 0, _soloZ1.Length);
                Array.Clear(_soloZ2, 0, _soloZ2.Length);
            }
        }
    }

    public void UpdateParameters(
        double sampleRate,
        bool isEnabled,
        double preampDb,
        double bassBoostDb,
        double trebleTiltDb,
        double globalQ,
        double balance,
        bool softLimiter,
        IReadOnlyList<EqBand> bands)
    {
        lock (_lock)
        {
            _sampleRate = Math.Max(8000, sampleRate);
            _isEnabled = isEnabled;
            _preampDb = preampDb;
            _preampLinear = Math.Pow(10.0, preampDb / 20.0);
            _balance = Math.Clamp(balance, -1.0, 1.0);
            _softLimiter = softLimiter;

            for (int i = 0; i < Math.Min(10, bands.Count); i++)
            {
                var band = bands[i];
                double q = band.QFactor * globalQ;
                _coeffs[i] = i == 0
                    ? BiquadCoeffs.CreateLowShelf(_sampleRate, band.Frequency, Math.Max(0.4, q * 0.7), band.GainDb)
                    : i == 9
                        ? BiquadCoeffs.CreateHighShelf(_sampleRate, band.Frequency, Math.Max(0.4, q * 0.7), band.GainDb)
                        : BiquadCoeffs.CreatePeaking(_sampleRate, band.Frequency, q, band.GainDb);
            }

            // Stage 10: Dedicated Sub-Bass Punch Shelf (75 Hz)
            _coeffs[10] = BiquadCoeffs.CreateLowShelf(_sampleRate, 75.0, 0.75, bassBoostDb);

            // Stage 11: Dedicated Air / Treble Tilt Shelf (6500 Hz)
            _coeffs[11] = BiquadCoeffs.CreateHighShelf(_sampleRate, 6500.0, 0.707, trebleTiltDb);

            if (_soloBandIndex >= 0 && _soloBandIndex < bands.Count)
            {
                var sb = bands[_soloBandIndex];
                _soloCoeffs = BiquadCoeffs.CreateBandPass(_sampleRate, sb.Frequency, Math.Max(0.6, sb.QFactor * globalQ));
            }
        }
    }

    public void ProcessStereoBuffer(Span<float> buffer, int channels)
    {
        if (!_isEnabled && _soloBandIndex < 0)
            return;

        lock (_lock)
        {
            double leftGain = _preampLinear * (_balance > 0 ? 1.0 - _balance : 1.0);
            double rightGain = _preampLinear * (_balance < 0 ? 1.0 + _balance : 1.0);

            int step = Math.Max(1, channels);
            for (int i = 0; i <= buffer.Length - step; i += step)
            {
                for (int ch = 0; ch < Math.Min(2, channels); ch++)
                {
                    double x = buffer[i + ch];

                    if (_soloBandIndex >= 0)
                    {
                        // Solo / Audition Band-Pass filter mode
                        double y = _soloCoeffs.B0 * x + _soloZ1[ch];
                        _soloZ1[ch] = _soloCoeffs.B1 * x - _soloCoeffs.A1 * y + _soloZ2[ch];
                        _soloZ2[ch] = _soloCoeffs.B2 * x - _soloCoeffs.A2 * y;
                        x = y * 1.45;
                    }
                    else
                    {
                        x *= (ch == 0 ? leftGain : rightGain);
                        for (int s = 0; s < TotalStages; s++)
                        {
                            ref BiquadCoeffs c = ref _coeffs[s];
                            double y = c.B0 * x + _z1[ch, s];
                            _z1[ch, s] = c.B1 * x - c.A1 * y + _z2[ch, s];
                            _z2[ch, s] = c.B2 * x - c.A2 * y;
                            x = y;
                        }
                    }

                    if (_softLimiter)
                    {
                        if (Math.Abs(x) > 0.85)
                        {
                            x = Math.Tanh(x);
                        }
                    }
                    else
                    {
                        x = Math.Clamp(x, -1.0, 1.0);
                    }

                    buffer[i + ch] = (float)x;
                }
            }
        }
    }

    public double EvaluateResponseDb(double freq)
    {
        lock (_lock)
        {
            if (!_isEnabled)
                return 0.0;

            double totalMag = 1.0;
            for (int s = 0; s < TotalStages; s++)
            {
                totalMag *= _coeffs[s].EvaluateMagnitudeLinear(freq, _sampleRate);
            }

            double filterDb = 20.0 * Math.Log10(Math.Max(1e-6, totalMag));
            return _preampDb + filterDb;
        }
    }

    public double EvaluateSingleBandResponseDb(int bandIndex, double freq)
    {
        lock (_lock)
        {
            if (bandIndex < 0 || bandIndex >= 10)
                return 0.0;

            double mag = _coeffs[bandIndex].EvaluateMagnitudeLinear(freq, _sampleRate);
            return 20.0 * Math.Log10(Math.Max(1e-6, mag));
        }
    }

    public double EvaluateFilterOnlyLinear(double freq)
    {
        lock (_lock)
        {
            if (_soloBandIndex >= 0)
            {
                return _soloCoeffs.EvaluateMagnitudeLinear(freq, _sampleRate) * 1.35;
            }

            if (!_isEnabled)
                return 1.0;

            double totalMag = _preampLinear;
            for (int s = 0; s < TotalStages; s++)
            {
                totalMag *= _coeffs[s].EvaluateMagnitudeLinear(freq, _sampleRate);
            }
            return totalMag;
        }
    }
}
