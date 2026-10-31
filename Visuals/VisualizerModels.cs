using System.Windows.Media;
using SkiaSharp;

namespace SonicDeck.Visuals;

public enum VisualizerMode
{
    StudioRtaEq,
    WinampClassicLed,
    HarmonicOscilloscope,
    RadialBassReactor,
    CyberHorizonGrid
}

public enum RtaDisplayStyle
{
    MidSideWaterfall,
    HybridBarsAndSpline,
    CleanLiquidSpline,
    FullSpectrogramMatrix
}

public enum VisualizerTheme
{
    CyberpunkNeon,
    FabFilterObsidian,
    PrismRainbow,
    DynamicChameleon,
    TokyoMidnight,
    VolcanicMagma,
    BioluminescentAbyss,
    AuroraBorealis,
    UltravioletLaser,
    SolarFlareGold,
    BloodMoonCrimson,
    VaporwaveAesthetic,
    ToxicRadioactive,
    CosmicSupernova,
    SunsetSynthwave,
    ArcticIce,
    StudioProAmber,
    MatrixPhosphor,
    WinampClassic
}

public record VisualizerModeItem(VisualizerMode Mode, string DisplayName);
public record RtaStyleItem(RtaDisplayStyle Style, string DisplayName);

public record VisualizerThemeItem(
    VisualizerTheme Theme,
    string DisplayName,
    Brush SwatchBrush,
    Brush EqAccentBrush);

public record ThemePalette(
    string Name,
    SKColor BackgroundTop,
    SKColor BackgroundBottom,
    SKColor GridLine,
    SKColor LowColor,
    SKColor MidColor,
    SKColor HighColor,
    SKColor PeakCapColor,
    SKColor EqCurveColor,
    SKColor AccentGlow,
    SKColor[]? CustomSpectrumStops = null)
{
    public SKColor[] SpectrumStops => CustomSpectrumStops is { Length: >= 2 }
        ? CustomSpectrumStops
        : new[] { LowColor, MidColor, HighColor };

    public SKColor SampleSpectrum(float t)
    {
        var stops = SpectrumStops;
        t = Math.Clamp(t, 0f, 1f);
        if (stops.Length == 1)
            return stops[0];

        float scaled = t * (stops.Length - 1);
        int idx = Math.Clamp((int)Math.Floor(scaled), 0, stops.Length - 2);
        float frac = scaled - idx;
        return VisualizerPalettes.BlendColor(stops[idx], stops[idx + 1], frac);
    }
}

public static class VisualizerPalettes
{
    public static IReadOnlyList<VisualizerModeItem> ModeItems { get; } = new List<VisualizerModeItem>
    {
        new(VisualizerMode.StudioRtaEq, "1. Pro Studio RTA + Live EQ (FabFilter Style)"),
        new(VisualizerMode.WinampClassicLed, "2. Winamp Classic Segmented LED Bars"),
        new(VisualizerMode.HarmonicOscilloscope, "3. Harmonic Stereo Oscilloscope + XY Scope"),
        new(VisualizerMode.RadialBassReactor, "4. Radial Bass Reactor + Particle Ring"),
        new(VisualizerMode.CyberHorizonGrid, "5. Cyber Horizon 3D Synthwave Terrain")
    };

    public static IReadOnlyList<RtaStyleItem> RtaStyleItems { get; } = new List<RtaStyleItem>
    {
        new(RtaDisplayStyle.MidSideWaterfall, "Mid/Side Stereo + Waterfall Strip"),
        new(RtaDisplayStyle.HybridBarsAndSpline, "Micro-Bars + Liquid Spline + Waterfall"),
        new(RtaDisplayStyle.CleanLiquidSpline, "Clean Liquid Spline + EQ Curves"),
        new(RtaDisplayStyle.FullSpectrogramMatrix, "Deep Waterfall Spectrogram + RTA")
    };

    public static IReadOnlyList<VisualizerThemeItem> ThemeItems { get; } = BuildThemeItems();

    private static IReadOnlyList<VisualizerThemeItem> BuildThemeItems()
    {
        var list = new List<VisualizerThemeItem>();
        foreach (var theme in Enum.GetValues<VisualizerTheme>())
        {
            var pal = GetPalette(theme, phaseTime: 0.25f);
            var stops = pal.SpectrumStops;
            var grad = new LinearGradientBrush
            {
                StartPoint = new System.Windows.Point(0, 0.5),
                EndPoint = new System.Windows.Point(1, 0.5)
            };

            for (int i = 0; i < stops.Length; i++)
            {
                var c = stops[i];
                double offset = stops.Length == 1 ? 0.0 : (double)i / (stops.Length - 1);
                grad.GradientStops.Add(new GradientStop(Color.FromRgb(c.Red, c.Green, c.Blue), offset));
            }
            grad.Freeze();

            var eqC = pal.EqCurveColor;
            var eqBrush = new SolidColorBrush(Color.FromRgb(eqC.Red, eqC.Green, eqC.Blue));
            eqBrush.Freeze();

            list.Add(new VisualizerThemeItem(theme, pal.Name, grad, eqBrush));
        }
        return list;
    }

    public static ThemePalette GetPalette(VisualizerTheme theme, float phaseTime = 0f) => theme switch
    {
        VisualizerTheme.FabFilterObsidian => new ThemePalette(
            "Pro Studio Mastering",
            new SKColor(12, 16, 24),
            new SKColor(6, 8, 13),
            new SKColor(30, 41, 59),
            new SKColor(37, 99, 235),     // Deep Royal Blue
            new SKColor(56, 189, 248),    // Studio Sky Cyan
            new SKColor(186, 230, 253),   // Ice Mist Highs
            new SKColor(248, 250, 252),
            new SKColor(250, 204, 21),    // Signature Pro-Q Gold EQ Curve
            new SKColor(56, 189, 248),
            new[]
            {
                new SKColor(29, 78, 216),
                new SKColor(37, 99, 235),
                new SKColor(14, 165, 233),
                new SKColor(56, 189, 248),
                new SKColor(186, 230, 253)
            }),

        VisualizerTheme.PrismRainbow => new ThemePalette(
            "Chromatic Prism",
            new SKColor(11, 13, 22),
            new SKColor(5, 6, 12),
            new SKColor(32, 38, 60),
            new SKColor(244, 63, 94),     // Sub-bass Rose-Red
            new SKColor(16, 185, 129),    // Mid Emerald-Cyan
            new SKColor(168, 85, 247),    // Treble Electric Violet
            new SKColor(255, 255, 255),
            new SKColor(255, 255, 255),   // Crisp White EQ Curve against Rainbow
            new SKColor(56, 189, 248),
            new[]
            {
                new SKColor(255, 45, 85),   // Neon Red-Pink
                new SKColor(255, 149, 0),   // Vivid Orange
                new SKColor(255, 214, 10),  // Cyber Yellow
                new SKColor(48, 209, 88),   // Laser Green
                new SKColor(0, 229, 255),   // Electric Cyan
                new SKColor(94, 92, 230),   // Royal Indigo
                new SKColor(217, 70, 239)   // Hyper Fuchsia
            }),

        VisualizerTheme.DynamicChameleon => BuildDynamicChameleonPalette(phaseTime),

        VisualizerTheme.TokyoMidnight => new ThemePalette(
            "Tokyo Midnight",
            new SKColor(15, 10, 32),
            new SKColor(7, 4, 18),
            new SKColor(44, 28, 84),
            new SKColor(99, 102, 241),    // Electric Indigo
            new SKColor(244, 114, 182),   // Neon Sakura Pink
            new SKColor(45, 212, 191),    // Cyber Mint
            new SKColor(253, 242, 248),
            new SKColor(253, 224, 71),    // Warm Neon Yellow EQ
            new SKColor(244, 114, 182),
            new[]
            {
                new SKColor(67, 56, 202),
                new SKColor(139, 92, 246),
                new SKColor(236, 72, 153),
                new SKColor(244, 114, 182),
                new SKColor(45, 212, 191)
            }),

        VisualizerTheme.VolcanicMagma => new ThemePalette(
            "Volcanic Magma",
            new SKColor(24, 10, 10),
            new SKColor(10, 4, 4),
            new SKColor(62, 22, 20),
            new SKColor(185, 28, 28),     // Deep Molten Red
            new SKColor(249, 115, 22),    // Blazing Lava Orange
            new SKColor(254, 240, 138),   // White-Hot Solar Yellow
            new SKColor(255, 255, 255),
            new SKColor(34, 211, 238),    // High-contrast Cyan EQ Curve
            new SKColor(249, 115, 22),
            new[]
            {
                new SKColor(153, 27, 27),
                new SKColor(220, 38, 38),
                new SKColor(249, 115, 22),
                new SKColor(250, 204, 21),
                new SKColor(254, 249, 195)
            }),

        VisualizerTheme.BioluminescentAbyss => new ThemePalette(
            "Bioluminescent Abyss",
            new SKColor(4, 18, 28),
            new SKColor(2, 8, 14),
            new SKColor(14, 48, 66),
            new SKColor(2, 132, 199),     // Deep Ocean Blue
            new SKColor(6, 182, 212),     // Glowing Aqua
            new SKColor(163, 230, 53),    // Bioluminescent Electric Lime
            new SKColor(236, 254, 255),
            new SKColor(244, 114, 182),   // Coral Pink EQ Curve
            new SKColor(34, 211, 238),
            new[]
            {
                new SKColor(3, 105, 161),
                new SKColor(6, 182, 212),
                new SKColor(20, 184, 166),
                new SKColor(52, 211, 153),
                new SKColor(163, 230, 53)
            }),

        VisualizerTheme.AuroraBorealis => new ThemePalette(
            "Northern Lights",
            new SKColor(8, 19, 26),
            new SKColor(4, 9, 14),
            new SKColor(22, 50, 64),
            new SKColor(16, 185, 129),    // Aurora Emerald
            new SKColor(56, 189, 248),    // Polar Cyan
            new SKColor(192, 132, 252),   // Celestial Violet
            new SKColor(240, 253, 244),
            new SKColor(250, 204, 21),    // Starlight Gold EQ Curve
            new SKColor(52, 211, 153),
            new[]
            {
                new SKColor(5, 150, 105),
                new SKColor(16, 185, 129),
                new SKColor(45, 212, 191),
                new SKColor(56, 189, 248),
                new SKColor(168, 85, 247),
                new SKColor(232, 121, 249)
            }),

        VisualizerTheme.UltravioletLaser => new ThemePalette(
            "Ultraviolet Laser",
            new SKColor(17, 8, 33),
            new SKColor(7, 3, 16),
            new SKColor(48, 22, 84),
            new SKColor(124, 58, 237),    // UV Deep Violet
            new SKColor(217, 70, 239),    // Hyper Magenta
            new SKColor(0, 245, 212),     // Laser Mint-Cyan
            new SKColor(255, 255, 255),
            new SKColor(163, 230, 53),    // Acid Lime EQ Curve
            new SKColor(217, 70, 239),
            new[]
            {
                new SKColor(109, 40, 217),
                new SKColor(168, 85, 247),
                new SKColor(217, 70, 239),
                new SKColor(244, 63, 94),
                new SKColor(0, 245, 212)
            }),

        VisualizerTheme.SolarFlareGold => new ThemePalette(
            "Solar Eclipse 24K",
            new SKColor(18, 15, 10),
            new SKColor(8, 6, 4),
            new SKColor(48, 38, 22),
            new SKColor(180, 83, 9),      // Deep Bronze
            new SKColor(245, 158, 11),    // Radiant 24K Gold
            new SKColor(254, 243, 199),   // Platinum Champagne
            new SKColor(255, 255, 255),
            new SKColor(244, 63, 94),     // Ruby Crimson EQ Curve
            new SKColor(251, 191, 36),
            new[]
            {
                new SKColor(146, 64, 14),
                new SKColor(217, 119, 6),
                new SKColor(245, 158, 11),
                new SKColor(251, 191, 36),
                new SKColor(254, 243, 199)
            }),

        VisualizerTheme.BloodMoonCrimson => new ThemePalette(
            "Blood Moon Noir",
            new SKColor(20, 7, 12),
            new SKColor(8, 3, 5),
            new SKColor(56, 18, 28),
            new SKColor(159, 18, 57),     // Dark Blood Crimson
            new SKColor(244, 63, 94),     // Vivid Scarlet Rose
            new SKColor(254, 205, 211),   // Pale Rose Gold
            new SKColor(255, 255, 255),
            new SKColor(56, 189, 248),    // Icy Silver-Blue EQ Curve
            new SKColor(244, 63, 94),
            new[]
            {
                new SKColor(136, 19, 55),
                new SKColor(190, 18, 60),
                new SKColor(244, 63, 94),
                new SKColor(251, 113, 133),
                new SKColor(254, 205, 211)
            }),

        VisualizerTheme.VaporwaveAesthetic => new ThemePalette(
            "Vaporwave 1995",
            new SKColor(22, 12, 38),
            new SKColor(10, 5, 20),
            new SKColor(60, 34, 92),
            new SKColor(255, 113, 206),   // Hot Bubblegum Pink
            new SKColor(185, 103, 255),   // Neon Lavender
            new SKColor(1, 205, 254),     // Helios Turquoise Cyan
            new SKColor(255, 251, 150),
            new SKColor(5, 255, 161),     // Retro Mint Green EQ Curve
            new SKColor(255, 113, 206),
            new[]
            {
                new SKColor(255, 113, 206),
                new SKColor(185, 103, 255),
                new SKColor(1, 205, 254),
                new SKColor(5, 255, 161)
            }),

        VisualizerTheme.ToxicRadioactive => new ThemePalette(
            "Radioactive Hazard",
            new SKColor(10, 18, 8),
            new SKColor(4, 8, 3),
            new SKColor(28, 52, 20),
            new SKColor(22, 163, 74),     // Isotope Green
            new SKColor(132, 204, 22),    // Uranium Lime
            new SKColor(250, 204, 21),    // Hazard Cyber Yellow
            new SKColor(254, 249, 195),
            new SKColor(217, 70, 239),    // Plasma Magenta EQ Curve
            new SKColor(163, 230, 53),
            new[]
            {
                new SKColor(21, 128, 61),
                new SKColor(34, 197, 94),
                new SKColor(132, 204, 22),
                new SKColor(234, 179, 8),
                new SKColor(254, 240, 138)
            }),

        VisualizerTheme.CosmicSupernova => new ThemePalette(
            "Cosmic Supernova",
            new SKColor(10, 10, 28),
            new SKColor(4, 4, 12),
            new SKColor(30, 32, 72),
            new SKColor(79, 70, 229),     // Deep Nebula Indigo
            new SKColor(236, 72, 153),    // Quasar Pink
            new SKColor(34, 211, 238),    // Supernova Cyan
            new SKColor(255, 255, 255),
            new SKColor(251, 191, 36),    // Solar Gold EQ Curve
            new SKColor(168, 85, 247),
            new[]
            {
                new SKColor(49, 46, 129),
                new SKColor(99, 102, 241),
                new SKColor(168, 85, 247),
                new SKColor(236, 72, 153),
                new SKColor(251, 146, 60),
                new SKColor(34, 211, 238)
            }),

        VisualizerTheme.WinampClassic => new ThemePalette(
            "Winamp Classic LED",
            new SKColor(10, 14, 12),
            new SKColor(4, 6, 5),
            new SKColor(28, 46, 32),
            new SKColor(0, 230, 64),      // Classic green bottom
            new SKColor(245, 215, 0),     // Classic yellow mid
            new SKColor(255, 45, 30),     // Classic red top clip
            new SKColor(200, 240, 255),
            new SKColor(0, 255, 180),
            new SKColor(0, 230, 90)),

        VisualizerTheme.StudioProAmber => new ThemePalette(
            "Studio Pro Amber",
            new SKColor(18, 16, 14),
            new SKColor(9, 8, 7),
            new SKColor(45, 38, 32),
            new SKColor(255, 145, 0),
            new SKColor(255, 196, 60),
            new SKColor(255, 240, 180),
            new SKColor(255, 255, 255),
            new SKColor(56, 189, 248),
            new SKColor(255, 170, 30)),

        VisualizerTheme.ArcticIce => new ThemePalette(
            "Arctic Borealis",
            new SKColor(10, 16, 28),
            new SKColor(5, 8, 16),
            new SKColor(26, 42, 68),
            new SKColor(14, 165, 233),
            new SKColor(56, 189, 248),
            new SKColor(224, 242, 254),
            new SKColor(255, 255, 255),
            new SKColor(168, 85, 247),
            new SKColor(56, 189, 248)),

        VisualizerTheme.SunsetSynthwave => new ThemePalette(
            "Sunset Synthwave",
            new SKColor(22, 10, 34),
            new SKColor(8, 4, 15),
            new SKColor(58, 24, 82),
            new SKColor(168, 85, 247),
            new SKColor(236, 72, 153),
            new SKColor(251, 146, 60),
            new SKColor(254, 240, 138),
            new SKColor(34, 211, 238),
            new SKColor(236, 72, 153)),

        VisualizerTheme.MatrixPhosphor => new ThemePalette(
            "Matrix Phosphor",
            new SKColor(6, 16, 8),
            new SKColor(2, 8, 4),
            new SKColor(18, 52, 24),
            new SKColor(16, 185, 129),
            new SKColor(52, 211, 153),
            new SKColor(167, 243, 208),
            new SKColor(236, 253, 245),
            new SKColor(250, 204, 21),
            new SKColor(16, 185, 129)),

        _ => new ThemePalette(
            "Cyberpunk Neon",
            new SKColor(11, 15, 27),
            new SKColor(6, 8, 15),
            new SKColor(28, 37, 60),
            new SKColor(6, 182, 212),     // Cyan
            new SKColor(139, 92, 246),    // Violet
            new SKColor(244, 63, 94),     // Neon Rose
            new SKColor(255, 255, 255),
            new SKColor(250, 204, 21),    // Gold EQ curve
            new SKColor(34, 211, 238),
            new[]
            {
                new SKColor(6, 182, 212),
                new SKColor(59, 130, 246),
                new SKColor(139, 92, 246),
                new SKColor(217, 70, 239),
                new SKColor(244, 63, 94)
            })
    };

    private static ThemePalette BuildDynamicChameleonPalette(float phaseTime)
    {
        float baseHue = (phaseTime * 28f) % 360f;
        if (baseHue < 0) baseHue += 360f;

        SKColor c0 = SKColor.FromHsl(baseHue, 92f, 56f);
        SKColor c1 = SKColor.FromHsl((baseHue + 55f) % 360f, 92f, 58f);
        SKColor c2 = SKColor.FromHsl((baseHue + 115f) % 360f, 94f, 60f);
        SKColor c3 = SKColor.FromHsl((baseHue + 175f) % 360f, 94f, 62f);
        SKColor c4 = SKColor.FromHsl((baseHue + 235f) % 360f, 92f, 64f);
        SKColor eq = SKColor.FromHsl((baseHue + 295f) % 360f, 98f, 66f);

        SKColor bgTop = SKColor.FromHsl(baseHue, 48f, 8f);
        SKColor bgBot = SKColor.FromHsl((baseHue + 60f) % 360f, 52f, 4f);
        SKColor grid = SKColor.FromHsl(baseHue, 38f, 19f);

        return new ThemePalette(
            "RGB Chameleon Flow",
            bgTop,
            bgBot,
            grid,
            c0,
            c2,
            c4,
            new SKColor(255, 255, 255),
            eq,
            c1,
            new[] { c0, c1, c2, c3, c4 });
    }

    public static SKColor BlendColor(SKColor a, SKColor b, float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return new SKColor(
            (byte)(a.Red + (b.Red - a.Red) * t),
            (byte)(a.Green + (b.Green - a.Green) * t),
            (byte)(a.Blue + (b.Blue - a.Blue) * t),
            (byte)(a.Alpha + (b.Alpha - a.Alpha) * t));
    }
}
