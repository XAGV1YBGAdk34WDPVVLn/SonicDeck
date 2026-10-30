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
    WinampClassic,
    StudioProAmber,
    ArcticIce,
    SunsetSynthwave,
    MatrixPhosphor
}

public record VisualizerModeItem(VisualizerMode Mode, string DisplayName);
public record RtaStyleItem(RtaDisplayStyle Style, string DisplayName);

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
    SKColor AccentGlow);

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

    public static ThemePalette GetPalette(VisualizerTheme theme) => theme switch
    {
        VisualizerTheme.WinampClassic => new ThemePalette(
            "Winamp Classic LED",
            new SKColor(10, 14, 12),
            new SKColor(4, 6, 5),
            new SKColor(28, 46, 32),
            new SKColor(0, 230, 64),     // Classic green bottom
            new SKColor(245, 215, 0),    // Classic yellow mid
            new SKColor(255, 45, 30),    // Classic red top clip
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
            "Arctic Ice",
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
            new SKColor(6, 182, 212),    // Cyan
            new SKColor(139, 92, 246),   // Violet
            new SKColor(244, 63, 94),    // Neon Rose
            new SKColor(255, 255, 255),
            new SKColor(250, 204, 21),   // Gold EQ curve
            new SKColor(34, 211, 238))
    };
}
