using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SonicDeck.Models;

public enum EqEngineMode
{
    Both,
    EqualizerApo,
    InternalDsp
}

public enum BiquadFilterType
{
    LowShelf,
    Peaking,
    HighShelf
}

public class EqBand : INotifyPropertyChanged
{
    private double _frequency;
    private double _gainDb;
    private double _qFactor = 1.41;

    public int Index { get; init; }
    public double DefaultFrequency { get; init; }
    public BiquadFilterType FilterType { get; init; } = BiquadFilterType.Peaking;

    public double Frequency
    {
        get => _frequency;
        set
        {
            double clamped = Math.Clamp(Math.Round(value, 1), 20.0, 19500.0);
            if (Math.Abs(_frequency - clamped) > 0.05)
            {
                _frequency = clamped;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Label));
                Changed?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public string Label => _frequency >= 1000.0
        ? $"{_frequency / 1000.0:0.#}k"
        : $"{_frequency:0}";

    public double GainDb
    {
        get => _gainDb;
        set
        {
            double clamped = Math.Clamp(Math.Round(value, 1), -15.0, 15.0);
            if (Math.Abs(_gainDb - clamped) > 0.001)
            {
                _gainDb = clamped;
                OnPropertyChanged();
                OnPropertyChanged(nameof(GainFormatted));
                Changed?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public double QFactor
    {
        get => _qFactor;
        set
        {
            double clamped = Math.Clamp(Math.Round(value, 2), 0.30, 8.00);
            if (Math.Abs(_qFactor - clamped) > 0.001)
            {
                _qFactor = clamped;
                OnPropertyChanged();
                OnPropertyChanged(nameof(QFormatted));
                Changed?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public string GainFormatted => _gainDb >= 0 ? $"+{_gainDb:0.0}" : $"{_gainDb:0.0}";
    public string QFormatted => $"Q {_qFactor:0.00}";

    public void ResetToDefault()
    {
        Frequency = DefaultFrequency;
        QFactor = 1.41;
        GainDb = 0.0;
    }

    public event EventHandler? Changed;
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public class EqPreset
{
    public string Name { get; init; } = string.Empty;
    public double PreampDb { get; init; }
    public double BassBoostDb { get; init; }
    public double TrebleTiltDb { get; init; }
    public double[] BandGains { get; init; } = new double[10];

    public static IReadOnlyList<EqPreset> BuiltInPresets { get; } = new List<EqPreset>
    {
        new()
        {
            Name = "Flat (Bypass Reference)",
            PreampDb = 0.0, BassBoostDb = 0.0, TrebleTiltDb = 0.0,
            BandGains = new[] { 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0 }
        },
        new()
        {
            Name = "Bass Boost + Punch",
            PreampDb = -3.5, BassBoostDb = 3.0, TrebleTiltDb = 0.5,
            BandGains = new[] { 6.5, 5.5, 3.5, 1.0, -0.5, 0.0, 1.0, 2.0, 2.5, 2.0 }
        },
        new()
        {
            Name = "Rock / Metal Drive",
            PreampDb = -2.5, BassBoostDb = 1.5, TrebleTiltDb = 1.5,
            BandGains = new[] { 4.5, 3.8, 2.0, -1.5, -2.0, 0.5, 2.8, 4.0, 4.5, 4.0 }
        },
        new()
        {
            Name = "Electronic / Sub Club",
            PreampDb = -4.0, BassBoostDb = 4.0, TrebleTiltDb = 1.5,
            BandGains = new[] { 7.5, 6.0, 2.5, -1.0, -1.5, 0.0, 1.5, 3.5, 5.0, 5.5 }
        },
        new()
        {
            Name = "Vocal & Podcast Clarity",
            PreampDb = -1.5, BassBoostDb = -2.0, TrebleTiltDb = 1.0,
            BandGains = new[] { -4.0, -3.0, -1.0, 1.5, 3.5, 4.5, 4.0, 2.5, 1.0, 0.0 }
        },
        new()
        {
            Name = "V-Shape Dynamic (Winamp Classic)",
            PreampDb = -3.0, BassBoostDb = 2.0, TrebleTiltDb = 2.0,
            BandGains = new[] { 6.0, 5.0, 3.0, 0.0, -2.5, -2.0, 1.0, 3.5, 5.5, 6.5 }
        },
        new()
        {
            Name = "Harman / Studio Target",
            PreampDb = -2.0, BassBoostDb = 1.5, TrebleTiltDb = 0.5,
            BandGains = new[] { 3.5, 3.0, 1.5, 0.0, -0.5, 0.5, 2.0, 1.5, 2.5, 3.0 }
        },
        new()
        {
            Name = "Warm Analog Tube",
            PreampDb = -1.5, BassBoostDb = 2.0, TrebleTiltDb = -1.5,
            BandGains = new[] { 2.5, 3.5, 3.0, 2.0, 1.0, 0.0, -0.5, -1.5, -2.0, -3.0 }
        },
        new()
        {
            Name = "Late Night / Quiet Listening",
            PreampDb = -2.0, BassBoostDb = 2.5, TrebleTiltDb = 1.5,
            BandGains = new[] { 4.5, 3.5, 1.0, -1.0, 0.5, 1.5, 1.0, 2.0, 3.5, 4.0 }
        },
        new()
        {
            Name = "FPS Gaming (Footsteps & Cues)",
            PreampDb = -2.0, BassBoostDb = -3.0, TrebleTiltDb = 2.0,
            BandGains = new[] { -5.0, -3.5, -1.0, 1.5, 3.0, 5.0, 6.0, 4.5, 2.5, 1.0 }
        }
    };
}
