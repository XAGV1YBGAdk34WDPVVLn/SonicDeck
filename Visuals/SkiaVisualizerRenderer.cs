using SkiaSharp;
using SonicDeck.Audio;
using SonicDeck.Audio.Dsp;
using SonicDeck.Models;

namespace SonicDeck.Visuals;

public class SkiaVisualizerRenderer
{
    private struct Particle
    {
        public float Angle;
        public float Radius;
        public float Speed;
        public float Size;
        public float Alpha;
    }

    private struct ShockwaveRing
    {
        public float Radius;
        public float Alpha;
    }

    private readonly Particle[] _particles = new Particle[90];
    private readonly List<ShockwaveRing> _shockwaves = new();
    private readonly Random _rng = new(1337);
    private float _phaseTime;
    private float _gridScroll;

    public SkiaVisualizerRenderer()
    {
        for (int i = 0; i < _particles.Length; i++)
        {
            _particles[i] = CreateParticle(randomRadius: true);
        }
    }

    public VisualizerMode Mode { get; set; } = VisualizerMode.StudioRtaEq;
    public RtaDisplayStyle RtaStyle { get; set; } = RtaDisplayStyle.MidSideWaterfall;
    public VisualizerTheme Theme { get; set; } = VisualizerTheme.CyberpunkNeon;
    public int BandCount { get; set; } = 64; // 16, 32, 64, 128
    public float GlowIntensity { get; set; } = 0.65f;
    public float BeatFlashIntensity { get; set; } = 0.75f;
    public bool ShowEqCurveOverlay { get; set; } = true;
    public bool ShowPeakHoldCaps { get; set; } = true;
    public bool ShowMirrorReflection { get; set; } = true;
    public bool ShowPreEqGhost { get; set; } = true;

    public int HoveredBandIndex { get; set; } = -1;
    public SKPoint? CursorSkiaPoint { get; set; }

    private Particle CreateParticle(bool randomRadius)
    {
        return new Particle
        {
            Angle = (float)(_rng.NextDouble() * Math.PI * 2.0),
            Radius = randomRadius ? (float)(_rng.NextDouble() * 320.0 + 30.0) : 45f,
            Speed = (float)(_rng.NextDouble() * 1.8 + 0.4),
            Size = (float)(_rng.NextDouble() * 2.8 + 1.2),
            Alpha = (float)(_rng.NextDouble() * 0.75 + 0.25)
        };
    }

    public void Render(
        SKCanvas canvas,
        int width,
        int height,
        AudioAnalysisSnapshot snap,
        BiquadEqChain eqChain,
        IReadOnlyList<EqBand> bands)
    {
        if (width <= 10 || height <= 10)
            return;

        ThemePalette pal = VisualizerPalettes.GetPalette(Theme);
        _phaseTime += 0.016f + snap.BassEnergy * 0.035f;
        _gridScroll = (_gridScroll + 1.8f + snap.BassEnergy * 9.5f) % 40f;

        if (snap.IsBeatFrame && _shockwaves.Count < 8)
        {
            _shockwaves.Add(new ShockwaveRing { Radius = 58f, Alpha = 0.85f });
        }

        DrawBackground(canvas, width, height, pal, snap);

        float padLeft = 48f;
        float padRight = 24f;
        float padTop = 32f;
        float padBottom = 34f;
        SKRect plotRect = new(padLeft, padTop, width - padRight, height - padBottom);

        switch (Mode)
        {
            case VisualizerMode.StudioRtaEq:
                DrawFrequencyGrid(canvas, plotRect, pal);
                DrawStudioRtaPro(canvas, plotRect, snap, pal);
                if (ShowEqCurveOverlay)
                {
                    DrawIndividualBandCurves(canvas, plotRect, eqChain, bands, pal);
                    DrawInteractiveEqCurve(canvas, plotRect, eqChain, bands, pal);
                }
                DrawDominantPeakCallout(canvas, plotRect, snap, pal);
                DrawStudioCrosshair(canvas, plotRect, snap, eqChain, pal);
                break;

            case VisualizerMode.WinampClassicLed:
                DrawFrequencyGrid(canvas, plotRect, pal);
                DrawWinampLedBars(canvas, plotRect, snap, pal);
                if (ShowEqCurveOverlay)
                {
                    DrawIndividualBandCurves(canvas, plotRect, eqChain, bands, pal);
                    DrawInteractiveEqCurve(canvas, plotRect, eqChain, bands, pal);
                }
                DrawStudioCrosshair(canvas, plotRect, snap, eqChain, pal);
                break;

            case VisualizerMode.HarmonicOscilloscope:
                DrawFrequencyGrid(canvas, plotRect, pal, isOscilloscope: true);
                DrawHarmonicOscilloscope(canvas, plotRect, snap, pal);
                if (ShowEqCurveOverlay)
                    DrawInteractiveEqCurve(canvas, plotRect, eqChain, bands, pal);
                break;

            case VisualizerMode.RadialBassReactor:
                DrawRadialReactor(canvas, width, height, snap, pal);
                if (ShowEqCurveOverlay)
                    DrawInteractiveEqCurve(canvas, plotRect, eqChain, bands, pal);
                break;

            case VisualizerMode.CyberHorizonGrid:
                DrawCyberHorizon(canvas, width, height, snap, pal);
                if (ShowEqCurveOverlay)
                    DrawInteractiveEqCurve(canvas, plotRect, eqChain, bands, pal);
                break;
        }

        DrawHudOverlay(canvas, width, height, snap, eqChain, pal);
    }

    private void DrawBackground(SKCanvas canvas, int w, int h, ThemePalette pal, AudioAnalysisSnapshot snap)
    {
        float beatBoost = snap.BeatPulse * BeatFlashIntensity * 0.22f;
        SKColor top = BlendColor(pal.BackgroundTop, pal.AccentGlow, beatBoost);

        using var bgPaint = new SKPaint
        {
            Shader = SKShader.CreateLinearGradient(
                new SKPoint(0, 0),
                new SKPoint(0, h),
                new[] { top, pal.BackgroundBottom },
                null,
                SKShaderTileMode.Clamp)
        };
        canvas.DrawRect(0, 0, w, h, bgPaint);
    }

    private void DrawFrequencyGrid(SKCanvas canvas, SKRect rect, ThemePalette pal, bool isOscilloscope = false)
    {
        using var linePaint = new SKPaint
        {
            Color = pal.GridLine.WithAlpha(135),
            StrokeWidth = 1f,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke
        };

        using var textPaint = new SKPaint
        {
            Color = new SKColor(148, 163, 184, 185),
            TextSize = 10.5f,
            IsAntialias = true,
            Typeface = SKTypeface.FromFamilyName("Consolas", SKFontStyleWeight.Bold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright)
        };

        string[] dbLabels = isOscilloscope
            ? new[] { "+1.0", "+0.5", " 0.0", "-0.5", "-1.0" }
            : new[] { "+15dB", " +7dB", "  0dB", " -7dB", "-15dB" };

        for (int i = 0; i < 5; i++)
        {
            float y = rect.Top + (rect.Height * i / 4f);
            canvas.DrawLine(rect.Left, y, rect.Right, y, linePaint);
            canvas.DrawText(dbLabels[i], 6f, y + 3.5f, textPaint);
        }

        double[] isoFreqs = { 31, 62, 125, 250, 500, 1000, 2000, 4000, 8000, 16000 };
        string[] isoLabels = { "31", "62", "125", "250", "500", "1k", "2k", "4k", "8k", "16k" };

        for (int i = 0; i < isoFreqs.Length; i++)
        {
            float x = FreqToX(isoFreqs[i], rect);
            canvas.DrawLine(x, rect.Top, x, rect.Bottom, linePaint);
            float tw = textPaint.MeasureText(isoLabels[i]);
            canvas.DrawText(isoLabels[i], x - tw * 0.5f, rect.Bottom + 15f, textPaint);
        }
    }

    private void DrawStudioRtaPro(SKCanvas canvas, SKRect rect, AudioAnalysisSnapshot snap, ThemePalette pal)
    {
        int n = snap.SmoothedSpectrum.Length;
        if (n < 4) return;

        bool hasWaterfall = RtaStyle is RtaDisplayStyle.MidSideWaterfall
            or RtaDisplayStyle.HybridBarsAndSpline
            or RtaDisplayStyle.FullSpectrogramMatrix;

        float waterfallRatio = RtaStyle == RtaDisplayStyle.FullSpectrogramMatrix
            ? 0.34f
            : hasWaterfall
                ? 0.17f
                : ShowMirrorReflection ? 0.14f : 0.0f;

        float baseLine = rect.Bottom - rect.Height * waterfallRatio;
        float usableHeight = baseLine - rect.Top;

        // 1. Optional Pre-EQ Ghost trace
        if (ShowPreEqGhost)
        {
            using var ghostPath = BuildSmoothSplinePath(snap.RawSpectrum, rect.Left, rect.Width, baseLine, usableHeight * 0.88f, closeFill: false);
            using var ghostPaint = new SKPaint
            {
                Color = new SKColor(148, 163, 184, 75),
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 1.25f,
                IsAntialias = true,
                PathEffect = SKPathEffect.CreateDash(new[] { 4f, 4f }, 0)
            };
            canvas.DrawPath(ghostPath, ghostPaint);
        }

        // 2. Stereo Side (Stereo Width) Layer in MidSideWaterfall mode
        if (RtaStyle == RtaDisplayStyle.MidSideWaterfall)
        {
            using var sideFillPath = BuildSmoothSplinePath(snap.SideSpectrum, rect.Left, rect.Width, baseLine, usableHeight * 0.84f, closeFill: true);
            using var sideStrokePath = BuildSmoothSplinePath(snap.SideSpectrum, rect.Left, rect.Width, baseLine, usableHeight * 0.84f, closeFill: false);

            using var sideFillPaint = new SKPaint
            {
                IsAntialias = true,
                Style = SKPaintStyle.Fill,
                Color = pal.MidColor.WithAlpha(48)
            };
            using var sideLinePaint = new SKPaint
            {
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 1.35f,
                Color = pal.MidColor.WithAlpha(155)
            };
            canvas.DrawPath(sideFillPath, sideFillPaint);
            canvas.DrawPath(sideStrokePath, sideLinePaint);
        }

        // 3. Optional Micro-Spectrum Bars underneath the liquid spline (HybridBarsAndSpline mode)
        if (RtaStyle == RtaDisplayStyle.HybridBarsAndSpline)
        {
            float slotW = rect.Width / n;
            float barW = Math.Max(1.5f, slotW * 0.68f);
            using var microPaint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill };

            for (int i = 0; i < n; i++)
            {
                float val = Math.Clamp(snap.SmoothedSpectrum[i], 0f, 1.12f);
                float bh = val * usableHeight * 0.88f;
                float x = rect.Left + i * slotW;
                float ratio = (float)i / (n - 1);
                microPaint.Color = BlendColor(pal.LowColor, pal.HighColor, ratio).WithAlpha(95);
                canvas.DrawRect(x, baseLine - bh, barW, bh, microPaint);
            }
        }

        // 4. Main Catmull-Rom Cubic Spline RTA Curve & Gradient Fill
        using var fillPath = BuildSmoothSplinePath(snap.SmoothedSpectrum, rect.Left, rect.Width, baseLine, usableHeight * 0.88f, closeFill: true);
        using var strokePath = BuildSmoothSplinePath(snap.SmoothedSpectrum, rect.Left, rect.Width, baseLine, usableHeight * 0.88f, closeFill: false);

        using (var fillPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Shader = SKShader.CreateLinearGradient(
                new SKPoint(rect.Left, rect.Top),
                new SKPoint(rect.Left, baseLine),
                new[]
                {
                    pal.HighColor.WithAlpha(160),
                    pal.MidColor.WithAlpha(105),
                    pal.LowColor.WithAlpha(22)
                },
                new[] { 0.0f, 0.45f, 1.0f },
                SKShaderTileMode.Clamp)
        })
        {
            canvas.DrawPath(fillPath, fillPaint);
        }

        if (GlowIntensity > 0.05f)
        {
            using var glowPaint = new SKPaint
            {
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 4.5f,
                Color = pal.AccentGlow.WithAlpha((byte)(GlowIntensity * 150)),
                MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 4.5f * GlowIntensity)
            };
            canvas.DrawPath(strokePath, glowPaint);
        }

        using (var linePaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2.3f,
            Shader = SKShader.CreateLinearGradient(
                new SKPoint(rect.Left, 0),
                new SKPoint(rect.Right, 0),
                new[] { pal.LowColor, pal.MidColor, pal.HighColor },
                new[] { 0f, 0.5f, 1f },
                SKShaderTileMode.Clamp)
        })
        {
            canvas.DrawPath(strokePath, linePaint);
        }

        // 5. Smooth Peak-Hold Spline Trace
        if (ShowPeakHoldCaps)
        {
            using var peakPath = BuildSmoothSplinePath(snap.PeakHoldSpectrum, rect.Left, rect.Width, baseLine, usableHeight * 0.88f, closeFill: false);
            using var peakPaint = new SKPaint
            {
                Color = pal.PeakCapColor.WithAlpha(155),
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 1.25f,
                IsAntialias = true
            };
            canvas.DrawPath(peakPath, peakPaint);
        }

        // 6. Rolling Waterfall Spectrogram Heat Strip (or Water Reflection) below baseline
        if (hasWaterfall && rect.Bottom - baseLine > 8f)
        {
            SKRect wfRect = new(rect.Left, baseLine + 2f, rect.Right, rect.Bottom);
            DrawWaterfallSpectrogramStrip(canvas, wfRect, snap, pal);
        }
        else if (ShowMirrorReflection && rect.Bottom - baseLine > 6f)
        {
            float reflectH = rect.Bottom - baseLine;
            using var refPath = new SKPath();
            refPath.MoveTo(rect.Left, baseLine);
            for (int i = 0; i < n; i++)
            {
                float x = rect.Left + (rect.Width * i / (n - 1f));
                float val = Math.Clamp(snap.SmoothedSpectrum[i], 0f, 1.0f);
                float y = baseLine + val * reflectH * 0.78f;
                refPath.LineTo(x, y);
            }
            refPath.LineTo(rect.Right, baseLine);
            refPath.Close();

            using var refPaint = new SKPaint
            {
                IsAntialias = true,
                Style = SKPaintStyle.Fill,
                Color = pal.LowColor.WithAlpha(35)
            };
            canvas.DrawPath(refPath, refPaint);
        }
    }

    private static SKPath BuildSmoothSplinePath(
        float[] values,
        float startX,
        float width,
        float baseLine,
        float scaleHeight,
        bool closeFill)
    {
        int n = values.Length;
        var pts = new SKPoint[n];
        for (int i = 0; i < n; i++)
        {
            float x = startX + (width * i / (n - 1f));
            float val = Math.Clamp(values[i], 0f, 1.14f);
            float y = baseLine - val * scaleHeight;
            pts[i] = new SKPoint(x, y);
        }

        var path = new SKPath();
        if (closeFill)
        {
            path.MoveTo(startX, baseLine);
            path.LineTo(pts[0]);
        }
        else
        {
            path.MoveTo(pts[0]);
        }

        // Catmull-Rom to Cubic Bezier conversion for liquid-smooth audio curves
        for (int i = 0; i < n - 1; i++)
        {
            SKPoint p0 = i == 0 ? pts[0] : pts[i - 1];
            SKPoint p1 = pts[i];
            SKPoint p2 = pts[i + 1];
            SKPoint p3 = i + 2 < n ? pts[i + 2] : pts[n - 1];

            SKPoint cp1 = new(
                p1.X + (p2.X - p0.X) / 6f,
                Math.Clamp(p1.Y + (p2.Y - p0.Y) / 6f, baseLine - scaleHeight * 1.2f, baseLine));
            SKPoint cp2 = new(
                p2.X - (p3.X - p1.X) / 6f,
                Math.Clamp(p2.Y - (p3.Y - p1.Y) / 6f, baseLine - scaleHeight * 1.2f, baseLine));

            path.CubicTo(cp1, cp2, p2);
        }

        if (closeFill)
        {
            path.LineTo(startX + width, baseLine);
            path.Close();
        }

        return path;
    }

    private static void DrawWaterfallSpectrogramStrip(
        SKCanvas canvas,
        SKRect wfRect,
        AudioAnalysisSnapshot snap,
        ThemePalette pal)
    {
        int rows = AudioAnalysisSnapshot.WaterfallRows;
        int cols = AudioAnalysisSnapshot.BinCount;
        float cellW = wfRect.Width / cols;
        float cellH = wfRect.Height / rows;

        using var cellPaint = new SKPaint { Style = SKPaintStyle.Fill, IsAntialias = false };

        for (int r = 0; r < rows; r++)
        {
            float y = wfRect.Top + r * cellH;
            float rowFade = 1.0f - (float)r / rows * 0.55f;

            for (int c = 0; c < cols; c += 2)
            {
                float v = Math.Max(snap.WaterfallHistory[r, c], c + 1 < cols ? snap.WaterfallHistory[r, c + 1] : 0f);
                if (v < 0.04f) continue;

                float t = Math.Clamp(v, 0f, 1f);
                SKColor heat = t < 0.45f
                    ? BlendColor(pal.BackgroundBottom, pal.LowColor, t / 0.45f)
                    : t < 0.80f
                        ? BlendColor(pal.LowColor, pal.MidColor, (t - 0.45f) / 0.35f)
                        : BlendColor(pal.MidColor, pal.HighColor, (t - 0.80f) / 0.20f);

                cellPaint.Color = heat.WithAlpha((byte)(Math.Clamp(t * rowFade * 235f, 15f, 240f)));
                canvas.DrawRect(wfRect.Left + c * cellW, y, cellW * 2.15f, cellH + 0.8f, cellPaint);
            }
        }

        // Separator hairline at top of waterfall
        using var sepPaint = new SKPaint { Color = pal.GridLine, StrokeWidth = 1f };
        canvas.DrawLine(wfRect.Left, wfRect.Top, wfRect.Right, wfRect.Top, sepPaint);
    }

    /// <summary>
    /// Draws FabFilter Pro-Q style individual shaded bell/shelf curves for each EQ band,
    /// highlighting the hovered or dragged band with its Q/Bandwidth envelope.
    /// </summary>
    private void DrawIndividualBandCurves(
        SKCanvas canvas,
        SKRect rect,
        BiquadEqChain eqChain,
        IReadOnlyList<EqBand> bands,
        ThemePalette pal)
    {
        float zeroDbY = rect.MidY;
        const float dbRange = 18.0f;
        int steps = 110;

        for (int bIdx = 0; bIdx < bands.Count; bIdx++)
        {
            var band = bands[bIdx];
            bool isHovered = bIdx == HoveredBandIndex;
            if (!isHovered && Math.Abs(band.GainDb) < 0.15)
                continue;

            using var fillPath = new SKPath();
            using var strokePath = new SKPath();

            fillPath.MoveTo(rect.Left, zeroDbY);
            for (int s = 0; s <= steps; s++)
            {
                float x = rect.Left + (rect.Width * s / steps);
                double freq = XToFreq(x, rect);
                double db = Math.Clamp(eqChain.EvaluateSingleBandResponseDb(bIdx, freq), -dbRange, dbRange);
                float y = zeroDbY - (float)(db / dbRange) * (rect.Height * 0.5f);

                fillPath.LineTo(x, y);
                if (s == 0) strokePath.MoveTo(x, y);
                else strokePath.LineTo(x, y);
            }
            fillPath.LineTo(rect.Right, zeroDbY);
            fillPath.Close();

            SKColor bandColor = band.GainDb >= 0 ? pal.LowColor : pal.HighColor;
            if (isHovered)
            {
                bandColor = pal.EqCurveColor;
            }

            using var fillPaint = new SKPaint
            {
                Color = bandColor.WithAlpha((byte)(isHovered ? 62 : 22)),
                Style = SKPaintStyle.Fill,
                IsAntialias = true
            };
            using var linePaint = new SKPaint
            {
                Color = bandColor.WithAlpha((byte)(isHovered ? 195 : 70)),
                Style = SKPaintStyle.Stroke,
                StrokeWidth = isHovered ? 1.7f : 1.0f,
                IsAntialias = true
            };

            canvas.DrawPath(fillPath, fillPaint);
            canvas.DrawPath(strokePath, linePaint);
        }
    }

    private void DrawInteractiveEqCurve(
        SKCanvas canvas,
        SKRect rect,
        BiquadEqChain eqChain,
        IReadOnlyList<EqBand> bands,
        ThemePalette pal)
    {
        float zeroDbY = rect.MidY;
        float dbRange = 18.0f; // +/- 18dB full vertical span

        using var curvePath = new SKPath();
        using var deltaFillPath = new SKPath();
        deltaFillPath.MoveTo(rect.Left, zeroDbY);

        int steps = 160;
        for (int i = 0; i <= steps; i++)
        {
            float x = rect.Left + (rect.Width * i / steps);
            double freq = XToFreq(x, rect);
            double db = Math.Clamp(eqChain.EvaluateResponseDb(freq), -dbRange, dbRange);
            float y = zeroDbY - (float)(db / dbRange) * (rect.Height * 0.5f);

            if (i == 0) curvePath.MoveTo(x, y);
            else curvePath.LineTo(x, y);
            deltaFillPath.LineTo(x, y);
        }
        deltaFillPath.LineTo(rect.Right, zeroDbY);
        deltaFillPath.Close();

        // 1. Boost / Cut Energy Delta Fill between 0dB and the EQ curve
        using (var deltaPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Shader = SKShader.CreateLinearGradient(
                new SKPoint(rect.Left, rect.Top),
                new SKPoint(rect.Left, rect.Bottom),
                new[]
                {
                    pal.EqCurveColor.WithAlpha(55),
                    pal.EqCurveColor.WithAlpha(8),
                    pal.HighColor.WithAlpha(55)
                },
                new[] { 0f, 0.5f, 1f },
                SKShaderTileMode.Clamp)
        })
        {
            canvas.DrawPath(deltaFillPath, deltaPaint);
        }

        // 2. Zero dB reference line
        using var zeroPaint = new SKPaint
        {
            Color = pal.EqCurveColor.WithAlpha(75),
            StrokeWidth = 1f,
            Style = SKPaintStyle.Stroke,
            PathEffect = SKPathEffect.CreateDash(new[] { 6f, 4f }, 0)
        };
        canvas.DrawLine(rect.Left, zeroDbY, rect.Right, zeroDbY, zeroPaint);

        // 3. Glow behind EQ curve
        using var eqGlow = new SKPaint
        {
            Color = pal.EqCurveColor.WithAlpha(115),
            StrokeWidth = 5f,
            Style = SKPaintStyle.Stroke,
            IsAntialias = true,
            MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 3.5f)
        };
        canvas.DrawPath(curvePath, eqGlow);

        // 4. Crisp EQ transfer function curve
        using var eqStroke = new SKPaint
        {
            Color = pal.EqCurveColor,
            StrokeWidth = 2.3f,
            Style = SKPaintStyle.Stroke,
            IsAntialias = true
        };
        canvas.DrawPath(curvePath, eqStroke);

        // 5. Interactive Band Control Nodes (2D Drag = Freq/Gain, Scroll Wheel = Q, Shift+Drag = Solo, Right-Click = Reset)
        using var nodeFill = new SKPaint { Color = pal.EqCurveColor, Style = SKPaintStyle.Fill, IsAntialias = true };
        using var nodeRing = new SKPaint { Color = SKColors.Black, Style = SKPaintStyle.Stroke, StrokeWidth = 1.8f, IsAntialias = true };
        using var labelPaint = new SKPaint
        {
            Color = pal.EqCurveColor,
            TextSize = 10.5f,
            IsAntialias = true,
            Typeface = SKTypeface.FromFamilyName("Consolas", SKFontStyleWeight.Bold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright)
        };

        for (int i = 0; i < bands.Count; i++)
        {
            var b = bands[i];
            float bx = FreqToX(b.Frequency, rect);
            double totalDbAtBand = Math.Clamp(eqChain.EvaluateResponseDb(b.Frequency), -dbRange, dbRange);
            float by = zeroDbY - (float)(totalDbAtBand / dbRange) * (rect.Height * 0.5f);

            bool hovered = i == HoveredBandIndex;
            float r = hovered ? 8.2f : 5.4f;

            if (hovered)
            {
                using var halo = new SKPaint
                {
                    Color = pal.EqCurveColor.WithAlpha(95),
                    Style = SKPaintStyle.Fill,
                    IsAntialias = true
                };
                canvas.DrawCircle(bx, by, 14f, halo);
            }

            canvas.DrawCircle(bx, by, r, nodeFill);
            canvas.DrawCircle(bx, by, r, nodeRing);

            if (hovered)
            {
                // Detailed FabFilter-style floating node inspector pill
                string info = $"B{i + 1}: {b.Frequency:0}Hz ({AudioAnalysisEngine.FrequencyToMusicalNote(b.Frequency)})  {b.GainFormatted}dB  Q:{b.QFactor:0.00}";
                DrawFloatingPill(canvas, bx, by - 22f, info, pal.EqCurveColor, rect);
            }
            else if (Math.Abs(b.GainDb) >= 0.4)
            {
                string txt = $"{b.GainFormatted}";
                float tw = labelPaint.MeasureText(txt);
                canvas.DrawText(txt, bx - tw * 0.5f, by - 9f, labelPaint);
            }
        }
    }

    private static void DrawDominantPeakCallout(
        SKCanvas canvas,
        SKRect rect,
        AudioAnalysisSnapshot snap,
        ThemePalette pal)
    {
        if (snap.DominantLevel < 0.14f || snap.DominantFrequencyHz < 20.0)
            return;

        float px = FreqToX(snap.DominantFrequencyHz, rect);
        float py = rect.Top + 18f;
        string text = $"▲ PEAK {snap.DominantFrequencyHz:0} Hz ({snap.DominantMusicalNote})";
        DrawFloatingPill(canvas, px, py, text, pal.HighColor, rect);
    }

    private void DrawStudioCrosshair(
        SKCanvas canvas,
        SKRect rect,
        AudioAnalysisSnapshot snap,
        BiquadEqChain eqChain,
        ThemePalette pal)
    {
        if (CursorSkiaPoint is not SKPoint pt || !rect.Contains(pt))
            return;

        using var hairPaint = new SKPaint
        {
            Color = new SKColor(226, 232, 240, 90),
            StrokeWidth = 1f,
            IsAntialias = true,
            PathEffect = SKPathEffect.CreateDash(new[] { 4f, 4f }, 0)
        };

        canvas.DrawLine(pt.X, rect.Top, pt.X, rect.Bottom, hairPaint);
        canvas.DrawLine(rect.Left, pt.Y, rect.Right, pt.Y, hairPaint);

        double freq = XToFreq(pt.X, rect);
        string note = AudioAnalysisEngine.FrequencyToMusicalNote(freq);
        double eqDb = eqChain.EvaluateResponseDb(freq);

        // Interpolate RTA level at cursor X
        int bin = Math.Clamp((int)Math.Round((pt.X - rect.Left) / Math.Max(1f, rect.Width) * (snap.SmoothedSpectrum.Length - 1)), 0, snap.SmoothedSpectrum.Length - 1);
        float rtaNorm = snap.SmoothedSpectrum[bin];
        float rtaDb = (rtaNorm * 52f) - 52f;

        string freqStr = freq >= 1000 ? $"{freq / 1000.0:0.00} kHz" : $"{freq:0} Hz";
        string readout = $"{freqStr} ({note})   RTA: {rtaDb:0.0} dB   EQ: {eqDb:+0.0;-0.0;0.0} dB";
        DrawFloatingPill(canvas, pt.X, Math.Max(rect.Top + 18f, pt.Y - 16f), readout, pal.LowColor, rect);
    }

    private static void DrawFloatingPill(
        SKCanvas canvas,
        float centerX,
        float centerY,
        string text,
        SKColor borderColor,
        SKRect bounds)
    {
        using var txtPaint = new SKPaint
        {
            Color = new SKColor(248, 250, 252),
            TextSize = 10.5f,
            IsAntialias = true,
            Typeface = SKTypeface.FromFamilyName("Consolas", SKFontStyleWeight.Bold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright)
        };

        float tw = txtPaint.MeasureText(text);
        float padX = 8f;
        float pillW = tw + padX * 2f;
        float pillH = 19f;

        float left = Math.Clamp(centerX - pillW * 0.5f, bounds.Left + 4f, bounds.Right - pillW - 4f);
        float top = Math.Clamp(centerY - pillH * 0.5f, bounds.Top + 2f, bounds.Bottom - pillH - 2f);
        SKRect pillRect = new(left, top, left + pillW, top + pillH);

        using var bgPaint = new SKPaint
        {
            Color = new SKColor(9, 13, 24, 225),
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };
        using var borderPaint = new SKPaint
        {
            Color = borderColor.WithAlpha(210),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1.2f,
            IsAntialias = true
        };

        canvas.DrawRoundRect(pillRect, 4.5f, 4.5f, bgPaint);
        canvas.DrawRoundRect(pillRect, 4.5f, 4.5f, borderPaint);
        canvas.DrawText(text, left + padX, top + 13.2f, txtPaint);
    }

    private void DrawWinampLedBars(SKCanvas canvas, SKRect rect, AudioAnalysisSnapshot snap, ThemePalette pal)
    {
        int bands = Math.Clamp(BandCount, 16, 128);
        int srcLen = snap.SmoothedSpectrum.Length;

        float baseLine = ShowMirrorReflection ? rect.Bottom - rect.Height * 0.14f : rect.Bottom;
        float maxBarH = (baseLine - rect.Top) * 0.90f;

        float slotW = rect.Width / bands;
        float gap = Math.Max(1.5f, slotW * 0.22f);
        float barW = Math.Max(2.0f, slotW - gap);

        int ledSegments = 24;
        float segStep = maxBarH / ledSegments;
        float segH = Math.Max(2.0f, segStep - 1.6f);

        using var barPaint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill };
        using var capPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Color = pal.PeakCapColor
        };

        for (int b = 0; b < bands; b++)
        {
            int idxStart = b * srcLen / bands;
            int idxEnd = Math.Max(idxStart + 1, (b + 1) * srcLen / bands);

            float val = 0f;
            float peak = 0f;
            for (int k = idxStart; k < idxEnd && k < srcLen; k++)
            {
                if (snap.SmoothedSpectrum[k] > val) val = snap.SmoothedSpectrum[k];
                if (snap.PeakHoldSpectrum[k] > peak) peak = snap.PeakHoldSpectrum[k];
            }

            float x = rect.Left + b * slotW + gap * 0.5f;
            int litSegments = Math.Clamp((int)Math.Round(val * ledSegments), 0, ledSegments);

            for (int s = 0; s < litSegments; s++)
            {
                float ratio = (float)s / (ledSegments - 1);
                barPaint.Color = ratio < 0.6f
                    ? BlendColor(pal.LowColor, pal.MidColor, ratio / 0.6f)
                    : BlendColor(pal.MidColor, pal.HighColor, (ratio - 0.6f) / 0.4f);

                float y = baseLine - (s + 1) * segStep;
                canvas.DrawRoundRect(x, y, barW, segH, 1.5f, 1.5f, barPaint);
            }

            if (ShowPeakHoldCaps && peak > 0.02f)
            {
                int peakSeg = Math.Clamp((int)Math.Round(peak * ledSegments), 0, ledSegments);
                float capY = baseLine - (peakSeg + 1) * segStep;
                canvas.DrawRoundRect(x, capY, barW, segH, 1.5f, 1.5f, capPaint);
            }

            if (ShowMirrorReflection && litSegments > 0)
            {
                float refH = (rect.Bottom - baseLine) * Math.Clamp(val, 0f, 1f) * 0.75f;
                barPaint.Color = pal.LowColor.WithAlpha(42);
                canvas.DrawRect(x, baseLine + 2f, barW, refH, barPaint);
            }
        }
    }

    private void DrawHarmonicOscilloscope(SKCanvas canvas, SKRect rect, AudioAnalysisSnapshot snap, ThemePalette pal)
    {
        float midY = rect.MidY;
        float ampScale = rect.Height * 0.42f;
        int len = snap.WaveformLeft.Length;

        using var ribbonPath = new SKPath();
        for (int i = 0; i < len; i += 2)
        {
            float t = (float)i / (len - 1);
            float x = rect.Left + t * rect.Width;
            float env = (float)Math.Sin(t * Math.PI);
            float y = midY + (float)Math.Sin(t * Math.PI * 6.0 + _phaseTime * 2.5f) * snap.BassEnergy * ampScale * 0.65f * env;
            if (i == 0) ribbonPath.MoveTo(x, y);
            else ribbonPath.LineTo(x, y);
        }

        using (var ribbonPaint = new SKPaint
        {
            Color = pal.MidColor.WithAlpha(85),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 5f,
            IsAntialias = true,
            MaskFilter = GlowIntensity > 0.05f ? SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 5f * GlowIntensity) : null
        })
        {
            canvas.DrawPath(ribbonPath, ribbonPaint);
        }

        using var leftPath = new SKPath();
        using var rightPath = new SKPath();

        for (int i = 0; i < len; i++)
        {
            float x = rect.Left + (rect.Width * i / (len - 1f));
            float yl = midY - Math.Clamp(snap.WaveformLeft[i], -1.1f, 1.1f) * ampScale;
            float yr = midY - Math.Clamp(snap.WaveformRight[i], -1.1f, 1.1f) * ampScale;

            if (i == 0)
            {
                leftPath.MoveTo(x, yl);
                rightPath.MoveTo(x, yr);
            }
            else
            {
                leftPath.LineTo(x, yl);
                rightPath.LineTo(x, yr);
            }
        }

        if (GlowIntensity > 0.05f)
        {
            using var glowL = new SKPaint
            {
                Color = pal.LowColor.WithAlpha((byte)(145 * GlowIntensity)),
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 5f,
                IsAntialias = true,
                MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 4f * GlowIntensity)
            };
            canvas.DrawPath(leftPath, glowL);
        }

        using var paintL = new SKPaint
        {
            Color = pal.LowColor,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2.2f,
            IsAntialias = true
        };
        using var paintR = new SKPaint
        {
            Color = pal.HighColor.WithAlpha(210),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1.8f,
            IsAntialias = true
        };

        canvas.DrawPath(rightPath, paintR);
        canvas.DrawPath(leftPath, paintL);

        float scopeSize = Math.Min(rect.Width, rect.Height) * 0.26f;
        SKRect scopeRect = new(rect.Right - scopeSize - 12f, rect.Bottom - scopeSize - 10f, rect.Right - 12f, rect.Bottom - 10f);
        using var scopeBg = new SKPaint { Color = new SKColor(8, 12, 22, 190), Style = SKPaintStyle.Fill, IsAntialias = true };
        using var scopeBorder = new SKPaint { Color = pal.GridLine, Style = SKPaintStyle.Stroke, StrokeWidth = 1f, IsAntialias = true };
        canvas.DrawRoundRect(scopeRect, 6f, 6f, scopeBg);
        canvas.DrawRoundRect(scopeRect, 6f, 6f, scopeBorder);

        float cx = scopeRect.MidX;
        float cy = scopeRect.MidY;
        float rad = scopeSize * 0.44f;
        using var xyPath = new SKPath();
        for (int i = 0; i < len; i += 2)
        {
            float l = Math.Clamp(snap.WaveformLeft[i], -1f, 1f);
            float r = Math.Clamp(snap.WaveformRight[i], -1f, 1f);
            float px = cx + (l - r) * 0.707f * rad;
            float py = cy - (l + r) * 0.707f * rad;
            if (i == 0) xyPath.MoveTo(px, py);
            else xyPath.LineTo(px, py);
        }
        using var xyPaint = new SKPaint
        {
            Color = pal.PeakCapColor.WithAlpha(185),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1.2f,
            IsAntialias = true
        };
        canvas.DrawPath(xyPath, xyPaint);
    }

    private void DrawRadialReactor(SKCanvas canvas, int w, int h, AudioAnalysisSnapshot snap, ThemePalette pal)
    {
        float cx = w * 0.5f;
        float cy = h * 0.5f;
        float minDim = Math.Min(w, h);
        float baseRadius = minDim * 0.19f * (1.0f + snap.BassEnergy * 0.28f + snap.BeatPulse * 0.12f);

        using var partPaint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill };
        float speedMult = 1.0f + snap.BassEnergy * 4.2f;
        for (int i = 0; i < _particles.Length; i++)
        {
            ref var p = ref _particles[i];
            p.Radius += p.Speed * speedMult;
            p.Angle += 0.004f;
            if (p.Radius > minDim * 0.65f)
            {
                p = CreateParticle(randomRadius: false);
            }

            float px = cx + (float)Math.Cos(p.Angle) * p.Radius;
            float py = cy + (float)Math.Sin(p.Angle) * p.Radius;
            partPaint.Color = pal.AccentGlow.WithAlpha((byte)(p.Alpha * 190));
            canvas.DrawCircle(px, py, p.Size * (1f + snap.HighEnergy), partPaint);
        }

        using var shockPaint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2.2f };
        for (int i = _shockwaves.Count - 1; i >= 0; i--)
        {
            var sw = _shockwaves[i];
            sw.Radius += 5.5f + snap.BassEnergy * 6f;
            sw.Alpha *= 0.94f;
            if (sw.Alpha < 0.03f)
            {
                _shockwaves.RemoveAt(i);
                continue;
            }
            _shockwaves[i] = sw;
            shockPaint.Color = pal.HighColor.WithAlpha((byte)(sw.Alpha * 220));
            canvas.DrawCircle(cx, cy, sw.Radius, shockPaint);
        }

        int blades = Math.Clamp(BandCount, 32, 128);
        int srcLen = snap.SmoothedSpectrum.Length;
        using var bladePaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeCap = SKStrokeCap.Round,
            StrokeWidth = Math.Max(2.2f, (float)(Math.PI * 2.0 * baseRadius / blades * 0.58f))
        };

        for (int i = 0; i < blades; i++)
        {
            int bin = (i < blades / 2 ? i : blades - 1 - i) * (srcLen - 1) / (blades / 2);
            float val = Math.Clamp(snap.SmoothedSpectrum[bin], 0f, 1.15f);
            float barLen = 6f + val * minDim * 0.25f;

            float angle = (float)(i * Math.PI * 2.0 / blades) - (float)(Math.PI * 0.5) + _phaseTime * 0.15f;
            float cos = (float)Math.Cos(angle);
            float sin = (float)Math.Sin(angle);

            float x1 = cx + cos * baseRadius;
            float y1 = cy + sin * baseRadius;
            float x2 = cx + cos * (baseRadius + barLen);
            float y2 = cy + sin * (baseRadius + barLen);

            float ratio = (float)bin / srcLen;
            bladePaint.Color = ratio < 0.5f
                ? BlendColor(pal.LowColor, pal.MidColor, ratio * 2f)
                : BlendColor(pal.MidColor, pal.HighColor, (ratio - 0.5f) * 2f);

            canvas.DrawLine(x1, y1, x2, y2, bladePaint);
        }

        using var corePaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Shader = SKShader.CreateRadialGradient(
                new SKPoint(cx, cy),
                baseRadius,
                new[]
                {
                    pal.HighColor.WithAlpha(200),
                    pal.MidColor.WithAlpha(110),
                    pal.BackgroundBottom.WithAlpha(230)
                },
                new[] { 0f, 0.55f, 1f },
                SKShaderTileMode.Clamp)
        };
        canvas.DrawCircle(cx, cy, baseRadius * 0.92f, corePaint);
    }

    private void DrawCyberHorizon(SKCanvas canvas, int w, int h, AudioAnalysisSnapshot snap, ThemePalette pal)
    {
        float horizonY = h * 0.54f;
        float sunRadius = Math.Min(w, h) * (0.16f + snap.BassEnergy * 0.05f);
        float sunX = w * 0.5f;
        float sunY = horizonY - sunRadius * 0.35f;

        using (var sunPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Shader = SKShader.CreateLinearGradient(
                new SKPoint(sunX, sunY - sunRadius),
                new SKPoint(sunX, sunY + sunRadius),
                new[] { pal.HighColor, pal.MidColor, pal.LowColor },
                null,
                SKShaderTileMode.Clamp)
        })
        {
            canvas.DrawCircle(sunX, sunY, sunRadius, sunPaint);
        }

        using var gridPaint = new SKPaint
        {
            Color = pal.LowColor.WithAlpha(135),
            StrokeWidth = 1.25f,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke
        };

        int radialLines = 18;
        for (int i = -radialLines; i <= radialLines; i++)
        {
            float topX = sunX + i * (w * 0.025f);
            float botX = sunX + i * (w * 0.16f);
            canvas.DrawLine(topX, horizonY, botX, h, gridPaint);
        }

        float floorH = h - horizonY;
        for (int row = 1; row <= 12; row++)
        {
            float t = ((row + (_gridScroll / 40f)) / 12f);
            float perspectiveY = horizonY + (t * t) * floorH;
            if (perspectiveY <= h)
            {
                canvas.DrawLine(0, perspectiveY, w, perspectiveY, gridPaint);
            }
        }

        int n = snap.SmoothedSpectrum.Length;
        using var skylinePath = new SKPath();
        skylinePath.MoveTo(0, horizonY);
        for (int i = 0; i < n; i++)
        {
            float x = w * i / (n - 1f);
            float val = Math.Clamp(snap.SmoothedSpectrum[i], 0f, 1.15f);
            float y = horizonY - val * (horizonY * 0.72f);
            skylinePath.LineTo(x, y);
        }
        skylinePath.LineTo(w, horizonY);
        skylinePath.Close();

        using var mountFill = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Color = pal.BackgroundBottom.WithAlpha(225)
        };
        using var mountEdge = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2.4f,
            Color = pal.PeakCapColor
        };
        canvas.DrawPath(skylinePath, mountFill);
        canvas.DrawPath(skylinePath, mountEdge);
    }

    private static void DrawHudOverlay(
        SKCanvas canvas,
        int w,
        int h,
        AudioAnalysisSnapshot snap,
        BiquadEqChain eqChain,
        ThemePalette pal)
    {
        using var labelPaint = new SKPaint
        {
            Color = new SKColor(203, 213, 225, 220),
            TextSize = 11f,
            IsAntialias = true,
            Typeface = SKTypeface.FromFamilyName("Consolas", SKFontStyleWeight.Bold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright)
        };

        string noteInfo = snap.DominantLevel > 0.1f
            ? $"   NOTE {snap.DominantMusicalNote} ({snap.DominantFrequencyHz:0}Hz)"
            : "";

        string metrics = $"BASS {snap.BassEnergy * 100:00}%   MID {snap.MidEnergy * 100:00}%   HIGH {snap.HighEnergy * 100:00}%{noteInfo}";
        canvas.DrawText(metrics, 50f, 20f, labelPaint);

        if (eqChain.SoloBandIndex >= 0)
        {
            using var soloPaint = new SKPaint
            {
                Color = new SKColor(250, 204, 21),
                TextSize = 11.5f,
                IsAntialias = true,
                Typeface = SKTypeface.FromFamilyName("Consolas", SKFontStyleWeight.Bold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright)
            };
            canvas.DrawText($"[SOLO AUDITION: BAND {eqChain.SoloBandIndex + 1}]", w * 0.5f - 95f, 20f, soloPaint);
        }

        float meterW = 90f;
        float meterX = w - meterW - 26f;
        DrawMiniMeter(canvas, meterX, 10f, meterW, 5f, snap.LeftPeak, "L", pal);
        DrawMiniMeter(canvas, meterX, 19f, meterW, 5f, snap.RightPeak, "R", pal);
    }

    private static void DrawMiniMeter(SKCanvas canvas, float x, float y, float w, float h, float val, string ch, ThemePalette pal)
    {
        using var bg = new SKPaint { Color = new SKColor(30, 41, 59, 190), Style = SKPaintStyle.Fill };
        using var fg = new SKPaint
        {
            Color = val > 0.85f ? new SKColor(244, 63, 94) : pal.LowColor,
            Style = SKPaintStyle.Fill
        };
        using var txt = new SKPaint { Color = new SKColor(148, 163, 184), TextSize = 9.5f, IsAntialias = true };

        canvas.DrawText(ch, x - 11f, y + h, txt);
        canvas.DrawRoundRect(x, y, w, h, 2f, 2f, bg);
        canvas.DrawRoundRect(x, y, w * Math.Clamp(val, 0f, 1f), h, 2f, 2f, fg);
    }

    public static float FreqToX(double freq, SKRect rect)
    {
        double minLog = Math.Log10(22.0);
        double maxLog = Math.Log10(19500.0);
        double t = (Math.Log10(Math.Clamp(freq, 22.0, 19500.0)) - minLog) / (maxLog - minLog);
        return rect.Left + (float)t * rect.Width;
    }

    public static double XToFreq(float x, SKRect rect)
    {
        double minLog = Math.Log10(22.0);
        double maxLog = Math.Log10(19500.0);
        double t = Math.Clamp((x - rect.Left) / Math.Max(1f, rect.Width), 0f, 1f);
        return Math.Pow(10.0, minLog + t * (maxLog - minLog));
    }

    private static SKColor BlendColor(SKColor a, SKColor b, float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return new SKColor(
            (byte)(a.Red + (b.Red - a.Red) * t),
            (byte)(a.Green + (b.Green - a.Green) * t),
            (byte)(a.Blue + (b.Blue - a.Blue) * t),
            (byte)(a.Alpha + (b.Alpha - a.Alpha) * t));
    }
}
