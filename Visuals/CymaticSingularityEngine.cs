using System.Threading.Tasks;
using SkiaSharp;
using SonicDeck.Audio;

namespace SonicDeck.Visuals;

/// <summary>
/// Flagship 4-Layer Audio Visualization Engine ("Cymatic Singularity"):
/// 1. Real-Time Chladni Standing-Wave & Ferrofluid Field (Fused Multithreaded Pixel Kernel)
/// 2. MilkDrop-Style Ping-Pong Optical Feedback with Gravitational Lensing & Micro-RGB Chromatic Dispersion
/// 3. Physical Cymatic Sand / Bioluminescent Nodal Particles Advected by Analytical Chladni Gradients
/// 4. 12-Semitone Circle-of-Fifths Harmonic Constellation + True Stereo Mid/Side Lissajous Phase Ribbons
/// </summary>
public sealed class CymaticSingularityEngine : IDisposable
{
    private const int MaxParticles = 760;
    private const int MaxShockwaves = 3;
    private const float Pi = 3.14159265f;

    // Circle of Fifths pitch-class indices into AudioAnalysisSnapshot.Chroma (0=C .. 11=B)
    private static readonly int[] CircleOfFifthsIndices = { 0, 7, 2, 9, 4, 11, 6, 1, 8, 3, 10, 5 };
    private static readonly string[] CircleOfFifthsLabels = { "C", "G", "D", "A", "E", "B", "F#", "C#", "G#", "D#", "A#", "F" };

    private struct NodalParticle
    {
        public float X;      // Normalized cymatic space [-1.15, 1.15]
        public float Y;
        public float Vx;
        public float Vy;
        public float Size;
        public float Phase;
        public float SpectrumPos;
    }

    private struct LensingShockwave
    {
        public float Radius;   // Normalized radial distance 0..1.45
        public float Strength; // 0..1 decay
    }

    private struct HatSparkle
    {
        public float Angle;
        public float Radius;
        public float Alpha;
        public float Size;
    }

    private readonly NodalParticle[] _particles = new NodalParticle[MaxParticles];
    private readonly LensingShockwave[] _shockwaves = new LensingShockwave[MaxShockwaves];
    private readonly HatSparkle[] _hatSparkles = new HatSparkle[48];
    private readonly Random _rng = new(42069);

    // Ping-pong offscreen pixel buffers for liquid optical feedback + cymatic standing-wave field
    private SKBitmap? _fbA;
    private SKBitmap? _fbB;
    private SKCanvas? _fbCanvasA;
    private SKCanvas? _fbCanvasB;
    private int _fbWidth;
    private int _fbHeight;
    private bool _fbInitialized;

    // Continuous visual phase state
    private float _time;
    private float _swirlPhase;
    private float _coreBreath;
    private float _constellationRotation;
    private float _smoothedEnergy;

    private static readonly SKTypeface NoteTypeface = SKTypeface.FromFamilyName(
        "Consolas",
        SKFontStyleWeight.Bold,
        SKFontStyleWidth.Normal,
        SKFontStyleSlant.Upright);

    public CymaticSingularityEngine()
    {
        for (int i = 0; i < MaxParticles; i++)
        {
            _particles[i] = CreateParticle();
        }
        for (int i = 0; i < _hatSparkles.Length; i++)
        {
            _hatSparkles[i] = new HatSparkle
            {
                Angle = (float)(_rng.NextDouble() * Math.PI * 2.0),
                Radius = (float)(0.54 + _rng.NextDouble() * 0.44),
                Alpha = 0f,
                Size = (float)(1.3 + _rng.NextDouble() * 2.3)
            };
        }
    }

    private NodalParticle CreateParticle()
    {
        double angle = _rng.NextDouble() * Math.PI * 2.0;
        double r = Math.Sqrt(_rng.NextDouble()) * 0.96;
        return new NodalParticle
        {
            X = (float)(Math.Cos(angle) * r),
            Y = (float)(Math.Sin(angle) * r),
            Vx = 0f,
            Vy = 0f,
            Size = (float)(1.15 + _rng.NextDouble() * 2.15),
            Phase = (float)(_rng.NextDouble() * Math.PI * 2.0),
            SpectrumPos = (float)Math.Clamp(r, 0.0, 1.0)
        };
    }

    private void EnsureFeedbackBuffers(int canvasWidth, int canvasHeight)
    {
        // Target ~340px max dimension for silky fluid softness & ultra-fast 60+ FPS parallel rendering
        const float maxDim = 340f;
        float scale = Math.Min(1f, maxDim / Math.Max(canvasWidth, canvasHeight));
        int targetW = Math.Clamp((int)(canvasWidth * scale), 160, 360);
        int targetH = Math.Clamp((int)(canvasHeight * scale), 96, 360);

        if (_fbA != null && _fbB != null && _fbWidth == targetW && _fbHeight == targetH)
            return;

        DisposeBuffers();

        var info = new SKImageInfo(targetW, targetH, SKColorType.Bgra8888, SKAlphaType.Premul);
        _fbA = new SKBitmap(info);
        _fbB = new SKBitmap(info);
        _fbCanvasA = new SKCanvas(_fbA);
        _fbCanvasB = new SKCanvas(_fbB);
        _fbWidth = targetW;
        _fbHeight = targetH;
        _fbInitialized = false;
    }

    public void Render(
        SKCanvas canvas,
        int width,
        int height,
        AudioAnalysisSnapshot snap,
        ThemePalette pal,
        float glowIntensity,
        float beatFlashIntensity,
        int bandCount,
        bool showMirrorShimmer)
    {
        if (width <= 16 || height <= 16)
            return;

        EnsureFeedbackBuffers(width, height);

        float energy = snap.BassEnergy * 0.45f + snap.MidEnergy * 0.35f + snap.HighEnergy * 0.20f;
        _smoothedEnergy += (energy - _smoothedEnergy) * 0.15f;
        _time += 0.016f + energy * 0.022f;
        _swirlPhase += 0.012f + snap.StereoWidth * 0.024f;
        _constellationRotation += 0.0022f + snap.MidEnergy * 0.005f;
        _coreBreath += (snap.BassEnergy * 0.45f + snap.KickPulse * 0.40f - _coreBreath) * 0.22f;

        // Spawn refractive gravitational-lensing shockwaves on Kick onset
        if (snap.IsKickFrame)
        {
            SpawnShockwave(Math.Clamp(0.55f + snap.KickPulse * 0.45f * beatFlashIntensity, 0.35f, 1.0f));
        }

        // Advance expanding shockwaves
        for (int i = 0; i < MaxShockwaves; i++)
        {
            if (_shockwaves[i].Strength > 0.01f)
            {
                _shockwaves[i].Radius += 0.022f + snap.BassEnergy * 0.018f;
                _shockwaves[i].Strength *= 0.945f;
                if (_shockwaves[i].Radius > 1.45f)
                    _shockwaves[i].Strength = 0f;
            }
        }

        // Trigger outer hi-hat caustic sparkles
        if (snap.IsHatFrame || snap.HatPulse > 0.40f)
        {
            int burst = snap.IsHatFrame ? 7 : 2;
            for (int k = 0; k < burst; k++)
            {
                int idx = _rng.Next(_hatSparkles.Length);
                _hatSparkles[idx].Angle = (float)(_rng.NextDouble() * Math.PI * 2.0);
                _hatSparkles[idx].Radius = (float)(0.50 + _rng.NextDouble() * 0.48);
                _hatSparkles[idx].Alpha = Math.Clamp(0.55f + snap.HatPulse * 0.55f, 0.4f, 1.0f);
                _hatSparkles[idx].Size = (float)(1.3 + _rng.NextDouble() * 2.6);
            }
        }

        // Layer 1 & 2: Fused Multithreaded Optical Feedback + Chladni Cymatic Standing-Wave Field
        RenderFeedbackAndCymaticsPass(canvas, width, height, snap, pal, glowIntensity, beatFlashIntensity, showMirrorShimmer);

        float cx = width * 0.5f;
        float cy = height * 0.5f;
        float minDim = Math.Min(width, height);

        // Layer 3: Full-Resolution Cymatic Sand / Bioluminescent Nodal Particles
        int activeParticles = Math.Clamp(bandCount * 6, 220, MaxParticles);
        UpdateAndDrawNodalParticles(canvas, cx, cy, minDim, snap, pal, glowIntensity, activeParticles);

        // Layer 4a: Mid/Side Dual Harmonic Halo + Outer Hi-Hat Caustic Sparkles
        DrawMidSideHarmonicHalo(canvas, cx, cy, minDim, snap, pal, glowIntensity);
        DrawHatSparkles(canvas, cx, cy, minDim, pal, glowIntensity);

        // Layer 4b: 12-Semitone Circle-of-Fifths Harmonic Constellation & Snare Lightning Arcs
        DrawHarmonicConstellation(canvas, cx, cy, minDim, snap, pal, glowIntensity);

        // Layer 4c: True Stereo Lissajous Phase Ribbons + Refractive Shockwaves + Singularity Core
        DrawStereoLissajousRibbons(canvas, cx, cy, minDim, snap, pal, glowIntensity, isFeedbackPass: false);
        DrawRefractiveShockwaveRings(canvas, cx, cy, minDim, pal, glowIntensity);
        DrawSingularityCore(canvas, cx, cy, minDim, snap, pal, glowIntensity);
    }

    private void SpawnShockwave(float strength)
    {
        int slot = 0;
        float lowest = float.MaxValue;
        for (int i = 0; i < MaxShockwaves; i++)
        {
            if (_shockwaves[i].Strength < lowest)
            {
                lowest = _shockwaves[i].Strength;
                slot = i;
            }
        }
        _shockwaves[slot] = new LensingShockwave { Radius = 0.06f, Strength = strength };
    }

    private unsafe void RenderFeedbackAndCymaticsPass(
        SKCanvas mainCanvas,
        int mainW,
        int mainH,
        AudioAnalysisSnapshot snap,
        ThemePalette pal,
        float glowIntensity,
        float beatFlashIntensity,
        bool showMirrorShimmer)
    {
        if (_fbA == null || _fbB == null || _fbCanvasA == null || _fbCanvasB == null)
            return;

        if (!_fbInitialized)
        {
            _fbCanvasA.Clear(pal.BackgroundBottom);
            _fbCanvasB.Clear(pal.BackgroundBottom);
            _fbInitialized = true;
        }

        int w = _fbWidth;
        int h = _fbHeight;
        float halfW = w * 0.5f;
        float halfH = h * 0.5f;
        float minDim = MathF.Max(1f, MathF.Min(w, h));
        float invMinDim = 1f / minDim;
        float invCymScale = 1f / (minDim * 0.52f);

        // Capture scalar uniforms once for zero-allocation parallel scanlines
        float time = _time;
        float bass = Math.Clamp(snap.BassEnergy, 0f, 1.3f);
        float mid = Math.Clamp(snap.MidEnergy, 0f, 1.3f);
        float high = Math.Clamp(snap.HighEnergy, 0f, 1.3f);
        float kick = Math.Clamp(snap.KickPulse * beatFlashIntensity, 0f, 1.3f);
        float snare = Math.Clamp(snap.SnarePulse * beatFlashIntensity, 0f, 1.3f);
        float centroid = Math.Clamp(snap.SpectralCentroid, 0f, 1f);
        float stereo = Math.Clamp(snap.StereoWidth, 0f, 1f);
        float modeM = snap.HarmonicModeM;
        float modeN = snap.HarmonicModeN;
        float mPi = modeM * Pi * 0.62f;
        float nPi = modeN * Pi * 0.62f;
        float symOrder = MathF.Floor(Math.Clamp(modeM + modeN, 4f, 10f));

        float zoom = 1f - (0.011f + bass * 0.015f + kick * 0.016f);
        float decay = Math.Clamp(0.835f + (showMirrorShimmer ? 0.025f : 0f) - kick * 0.022f, 0.78f, 0.875f);
        float dispPx = (0.0020f + snare * 0.0072f + kick * 0.0046f + stereo * 0.0028f) * minDim;
        float curlAmp = 0.0018f + bass * 0.0045f;

        float rot = time * 0.14f + stereo * 0.25f * MathF.Sin(time * 0.3f);
        float cosRot = MathF.Cos(rot);
        float sinRot = MathF.Sin(rot);

        float polarFreq = 7.5f + bass * 3.5f;
        float polarTime = time * (1.8f + mid * 2.2f);
        float polarPhase = MathF.Sin(time * 0.6f) * 0.8f;
        float polarBlend = 0.36f + stereo * 0.24f;
        float sharpness = 5.4f + centroid * 6.5f + high * 4.2f;
        // Calibrated per-frame injection so multi-frame feedback stays rich & jewel-saturated, never blown-out white
        float cymBlendAlpha = Math.Clamp(0.115f + glowIntensity * 0.095f, 0.095f, 0.22f);
        float activity = Math.Clamp(0.28f + (bass + mid + high) * 0.52f + kick * 0.30f, 0.28f, 1.05f);
        float coreFalloff = 7.5f - bass * 1.6f;
        float coreAmp = 0.14f + kick * 0.22f + bass * 0.14f;
        float causticAmp = (0.55f + bass * 0.50f + mid * 0.38f) * (0.65f + glowIntensity * 0.48f);
        float secAmp = 0.22f + stereo * 0.32f + high * 0.22f;
        float sheenAmp = (0.20f + centroid * 0.52f) * 0.42f;

        float s0R = _shockwaves[0].Radius, s0S = _shockwaves[0].Strength;
        float s1R = _shockwaves[1].Radius, s1S = _shockwaves[1].Strength;
        float s2R = _shockwaves[2].Radius, s2S = _shockwaves[2].Strength;

        // Theme RGB floats [0..1]
        float lowR = pal.LowColor.Red / 255f, lowG = pal.LowColor.Green / 255f, lowB = pal.LowColor.Blue / 255f;
        float midR = pal.MidColor.Red / 255f, midG = pal.MidColor.Green / 255f, midB = pal.MidColor.Blue / 255f;
        float hiR = pal.HighColor.Red / 255f, hiG = pal.HighColor.Green / 255f, hiB = pal.HighColor.Blue / 255f;
        float accR = pal.AccentGlow.Red / 255f, accG = pal.AccentGlow.Green / 255f, accB = pal.AccentGlow.Blue / 255f;
        float bgR = (pal.BackgroundBottom.Red / 255f) * 0.92f;
        float bgG = (pal.BackgroundBottom.Green / 255f) * 0.92f;
        float bgB = (pal.BackgroundBottom.Blue / 255f) * 0.92f;

        float sheenTintR = midR + (hiR - midR) * centroid;
        float sheenTintG = midG + (hiG - midG) * centroid;
        float sheenTintB = midB + (hiB - midB) * centroid;
        float coreTintR = lowR + (hiR - lowR) * 0.65f;
        float coreTintG = lowG + (hiG - lowG) * 0.65f;
        float coreTintB = lowB + (hiB - lowB) * 0.65f;

        IntPtr srcPtr = _fbA.GetPixels();
        IntPtr dstPtr = _fbB.GetPixels();
        int maxX = w - 2;
        int maxY = h - 2;

        // Single fused parallel pass over all rows: Optical Feedback + Lensing + Dispersion + Chladni Cymatics!
        Parallel.For(0, h, y =>
        {
            uint* prevBase = (uint*)srcPtr;
            uint* dstRow = (uint*)dstPtr + y * w;

            float dy = (y + 0.5f) - halfH;
            float uvY = dy * invMinDim;
            float cymY = dy * invCymScale;
            float curlXTerm = MathF.Sin(uvY * 5.2f + time * 0.9f) * curlAmp;

            for (int x = 0; x < w; x++)
            {
                float dx = (x + 0.5f) - halfW;
                float uvX = dx * invMinDim;
                float r = MathF.Sqrt(uvX * uvX + uvY * uvY);
                float invR = r > 0.0001f ? 1f / r : 0f;
                float dirX = uvX * invR;
                float dirY = uvY * invR;

                // 1. MilkDrop optical feedback swirl + outward drift
                float swirlAng = (0.0055f + stereo * 0.014f) / (0.35f + r * 1.4f) * MathF.Sin(time * 0.37f + r * 2.1f);
                float ca = MathF.Cos(swirlAng);
                float sa = MathF.Sin(swirlAng);
                float warpX = (uvX * ca - uvY * sa) * zoom + curlXTerm;
                float warpY = (uvX * sa + uvY * ca) * zoom + MathF.Cos(uvX * 5.2f - time * 0.85f) * curlAmp;

                // Gravitational lensing from active kick shockwaves
                float lens = 0f;
                if (s0S > 0.01f)
                {
                    float d0 = r - s0R;
                    if (MathF.Abs(d0) < 0.25f)
                        lens += MathF.Sin(d0 * 34f) * MathF.Exp(-MathF.Abs(d0) * 20f) * s0S * 0.028f;
                }
                if (s1S > 0.01f)
                {
                    float d1 = r - s1R;
                    if (MathF.Abs(d1) < 0.25f)
                        lens += MathF.Sin(d1 * 34f) * MathF.Exp(-MathF.Abs(d1) * 20f) * s1S * 0.028f;
                }
                if (s2S > 0.01f)
                {
                    float d2 = r - s2R;
                    if (MathF.Abs(d2) < 0.25f)
                        lens += MathF.Sin(d2 * 34f) * MathF.Exp(-MathF.Abs(d2) * 20f) * s2S * 0.028f;
                }

                warpX -= dirX * lens;
                warpY -= dirY * lens;

                float basePxX = halfW + warpX * minDim;
                float basePxY = halfH + warpY * minDim;

                // Micro-RGB chromatic dispersion sampling from previous frame (BGRA8888)
                // Gentle extra decay inside the inner singularity well so harmonic geometry & ribbons stay razor-crisp
                float wellMask = SmoothStep(0.08f, 0.38f, r);
                float localDecay = decay * (0.86f + 0.14f * wellMask);
                float pr = SampleChannelBilinear(prevBase, w, maxX, maxY, basePxX + dirX * dispPx, basePxY + dirY * dispPx, 16) * localDecay;
                float pg = SampleChannelBilinear(prevBase, w, maxX, maxY, basePxX, basePxY, 8) * localDecay;
                float pb = SampleChannelBilinear(prevBase, w, maxX, maxY, basePxX - dirX * dispPx, basePxY - dirY * dispPx, 0) * localDecay;

                if (pr < bgR) pr = bgR;
                if (pg < bgG) pg = bgG;
                if (pb < bgB) pb = bgB;

                // 2. Real-time Chladni Square + Polar Standing-Wave & Ferrofluid Caustics
                float cymX = dx * invCymScale;
                float cr = MathF.Sqrt(cymX * cymX + cymY * cymY);
                float vignette = SmoothStep(1.62f, 0.16f, cr);

                if (vignette > 0.002f)
                {
                    float ang = MathF.Atan2(cymY, cymX);
                    float gravWarp = 1f / (1f + bass * 0.24f * MathF.Exp(-cr * 1.35f));
                    float qx = (cymX * cosRot - cymY * sinRot) * gravWarp;
                    float qy = (cymX * sinRot + cymY * cosRot) * gravWarp;

                    float cosNx = MathF.Cos(nPi * qx);
                    float sinNx = MathF.Sin(nPi * qx);
                    float cosMy = MathF.Cos(mPi * qy);
                    float sinMy = MathF.Sin(mPi * qy);
                    float cosMx = MathF.Cos(mPi * qx);
                    float sinMx = MathF.Sin(mPi * qx);
                    float cosNy = MathF.Cos(nPi * qy);
                    float sinNy = MathF.Sin(nPi * qy);

                    float cSquare = cosNx * cosMy - cosMx * cosNy;
                    float cPolar = MathF.Cos(cr * polarFreq - polarTime) * MathF.Cos(symOrder * ang + polarPhase);
                    float field = cSquare + (cPolar - cSquare) * polarBlend;

                    // Analytical slope magnitude for liquid ferrofluid metallic specular highlights
                    float dfx = -nPi * sinNx * cosMy + mPi * sinMx * cosNy;
                    float dfy = -mPi * cosNx * sinMy + nPi * cosMx * sinNy;
                    float slope = MathF.Min(1f, MathF.Sqrt(dfx * dfx + dfy * dfy) * 0.085f);

                    float nodalLine = MathF.Exp(-MathF.Abs(field) * sharpness);
                    float secondaryLine = MathF.Exp(-MathF.Abs(cPolar) * (sharpness * 0.72f));

                    float caustic = nodalLine * MathF.Sqrt(nodalLine) * causticAmp;
                    float sheen = slope * slope * (1f - nodalLine * 0.4f) * sheenAmp;
                    float coreHalo = MathF.Exp(-cr * cr * coreFalloff) * coreAmp;

                    // Modulate inner well gently so outer Chladni waves blaze while inner singularity stays velvety deep
                    float innerRelief = 0.36f + 0.64f * SmoothStep(0.18f, 0.62f, cr);

                    float tCol = Math.Clamp(cr * 0.65f + centroid * 0.28f + 0.12f * MathF.Sin(ang * 2f + time), 0f, 1f);
                    float gR, gG, gB;
                    if (tCol < 0.5f)
                    {
                        float f = tCol * 2f;
                        gR = lowR + (midR - lowR) * f;
                        gG = lowG + (midG - lowG) * f;
                        gB = lowB + (midB - lowB) * f;
                    }
                    else
                    {
                        float f = (tCol - 0.5f) * 2f;
                        gR = midR + (hiR - midR) * f;
                        gG = midG + (hiG - midG) * f;
                        gB = midB + (hiB - midB) * f;
                    }

                    float scaleVis = vignette * activity * cymBlendAlpha * innerRelief;
                    pr += (gR * caustic + accR * secondaryLine * secAmp + sheenTintR * sheen + coreTintR * coreHalo) * scaleVis;
                    pg += (gG * caustic + accG * secondaryLine * secAmp + sheenTintG * sheen + coreTintG * coreHalo) * scaleVis;
                    pb += (gB * caustic + accB * secondaryLine * secAmp + sheenTintB * sheen + coreTintB * coreHalo) * scaleVis;
                }

                // Soft filmic HDR shoulder keeps highlights jewel-saturated instead of clipping to flat white
                float maxCh = MathF.Max(pr, MathF.Max(pg, pb));
                if (maxCh > 0.78f)
                {
                    float compressed = 0.78f + (maxCh - 0.78f) / (1f + (maxCh - 0.78f) * 3.8f);
                    float ratio = compressed / maxCh;
                    pr *= ratio;
                    pg *= ratio;
                    pb *= ratio;
                }

                uint outR = (uint)(pr >= 1f ? 255 : (pr <= 0f ? 0 : (int)(pr * 255f)));
                uint outG = (uint)(pg >= 1f ? 255 : (pg <= 0f ? 0 : (int)(pg * 255f)));
                uint outB = (uint)(pb >= 1f ? 255 : (pb <= 0f ? 0 : (int)(pb * 255f)));
                dstRow[x] = 0xFF000000u | (outR << 16) | (outG << 8) | outB;
            }
        });

        _fbB.NotifyPixelsChanged();

        // Pass C: Inject live Stereo Lissajous Ribbon into the feedback canvas so stereo whips leave chromatic liquid trails
        DrawStereoLissajousRibbons(
            _fbCanvasB,
            halfW,
            halfH,
            minDim,
            snap,
            pal,
            glowIntensity,
            isFeedbackPass: true);

        // Swap ping-pong buffers (_fbB becomes newest _fbA)
        (_fbA, _fbB) = (_fbB, _fbA);
        (_fbCanvasA, _fbCanvasB) = (_fbCanvasB, _fbCanvasA);

        // Smooth bilinear upsample to full-resolution main canvas
        var destRect = new SKRect(0, 0, mainW, mainH);
        using var blitPaint = new SKPaint
        {
            IsAntialias = false,
            FilterQuality = SKFilterQuality.Low
        };
        mainCanvas.DrawBitmap(_fbA, destRect, blitPaint);
    }

    private static unsafe float SampleChannelBilinear(
        uint* pixels,
        int width,
        int maxX,
        int maxY,
        float px,
        float py,
        int shift)
    {
        if (px < 0f) px = 0f;
        else if (px > maxX) px = maxX;
        if (py < 0f) py = 0f;
        else if (py > maxY) py = maxY;

        int x0 = (int)px;
        int y0 = (int)py;
        float fx = px - x0;
        float fy = py - y0;

        int idx = y0 * width + x0;
        float c00 = (pixels[idx] >> shift) & 0xFF;
        float c10 = (pixels[idx + 1] >> shift) & 0xFF;
        float c01 = (pixels[idx + width] >> shift) & 0xFF;
        float c11 = (pixels[idx + width + 1] >> shift) & 0xFF;

        float top = c00 + (c10 - c00) * fx;
        float bot = c01 + (c11 - c01) * fx;
        return (top + (bot - top) * fy) * (1f / 255f);
    }

    private static float SmoothStep(float edge0, float edge1, float x)
    {
        float t = Math.Clamp((x - edge0) / (edge1 - edge0), 0f, 1f);
        return t * t * (3f - 2f * t);
    }

    private static SKPath BuildSmoothClosedPath(ReadOnlySpan<SKPoint> pts)
    {
        var path = new SKPath();
        int n = pts.Length;
        if (n < 3) return path;

        SKPoint startMid = new((pts[n - 1].X + pts[0].X) * 0.5f, (pts[n - 1].Y + pts[0].Y) * 0.5f);
        path.MoveTo(startMid);
        for (int i = 0; i < n; i++)
        {
            SKPoint curr = pts[i];
            SKPoint next = pts[(i + 1) % n];
            path.QuadTo(curr.X, curr.Y, (curr.X + next.X) * 0.5f, (curr.Y + next.Y) * 0.5f);
        }
        path.Close();
        return path;
    }

    private void UpdateAndDrawNodalParticles(
        SKCanvas canvas,
        float cx,
        float cy,
        float minDim,
        AudioAnalysisSnapshot snap,
        ThemePalette pal,
        float glowIntensity,
        int count)
    {
        float m = snap.HarmonicModeM;
        float n = snap.HarmonicModeN;
        float mPi = m * Pi * 0.62f;
        float nPi = n * Pi * 0.62f;

        float scale = minDim * 0.46f;
        float attractForce = 0.0028f + snap.MidEnergy * 0.0045f;
        float swirlSpeed = 0.0018f + snap.StereoWidth * 0.0055f;
        float kickBurst = snap.IsKickFrame ? (0.022f + snap.KickPulse * 0.028f) : 0f;
        float snareScatter = snap.IsSnareFrame ? (0.018f + snap.SnarePulse * 0.026f) : 0f;

        using var pPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            BlendMode = SKBlendMode.Plus
        };

        for (int i = 0; i < count; i++)
        {
            ref var p = ref _particles[i];

            float cosNx = MathF.Cos(nPi * p.X);
            float sinNx = MathF.Sin(nPi * p.X);
            float cosMy = MathF.Cos(mPi * p.Y);
            float sinMy = MathF.Sin(mPi * p.Y);

            float cosMx = MathF.Cos(mPi * p.X);
            float sinMx = MathF.Sin(mPi * p.X);
            float cosNy = MathF.Cos(nPi * p.Y);
            float sinNy = MathF.Sin(nPi * p.Y);

            float val = cosNx * cosMy - cosMx * cosNy;
            float dValDx = -nPi * sinNx * cosMy + mPi * sinMx * cosNy;
            float dValDy = -mPi * cosNx * sinMy + nPi * cosMx * sinNy;

            // Pull particle toward physical Chladni nodal line (val == 0)
            p.Vx -= val * dValDx * attractForce;
            p.Vy -= val * dValDy * attractForce;

            float r = MathF.Sqrt(p.X * p.X + p.Y * p.Y + 0.0001f);

            // Stereo rotational swirl
            p.Vx += (-p.Y / r) * swirlSpeed;
            p.Vy += (p.X / r) * swirlSpeed;

            if (r > 0.98f)
            {
                p.Vx -= (p.X / r) * 0.008f;
                p.Vy -= (p.Y / r) * 0.008f;
            }

            // Kick radial blast & Snare crystalline scatter
            if (kickBurst > 0f && r < 0.72f)
            {
                float impulse = kickBurst * (1f - r * 0.85f);
                p.Vx += (p.X / r) * impulse;
                p.Vy += (p.Y / r) * impulse;
            }
            if (snareScatter > 0f)
            {
                float jitterAng = p.Phase + _time * 13.7f;
                p.Vx += MathF.Cos(jitterAng) * snareScatter;
                p.Vy += MathF.Sin(jitterAng) * snareScatter;
            }

            float damping = 0.88f + snap.SpectralCentroid * 0.045f;
            p.Vx *= damping;
            p.Vy *= damping;

            p.X += p.Vx;
            p.Y += p.Vy;

            if (MathF.Abs(p.X) > 1.32f || MathF.Abs(p.Y) > 1.32f)
            {
                p = CreateParticle();
            }

            float sx = cx + p.X * scale;
            float sy = cy + p.Y * scale;

            float nodalClarity = MathF.Exp(-MathF.Abs(val) * 3.8f);
            float alpha = Math.Clamp((0.18f + nodalClarity * 0.65f + snap.HighEnergy * 0.25f) * (0.65f + glowIntensity * 0.5f), 0.12f, 0.96f);

            SKColor col = pal.SampleSpectrum(Math.Clamp(r * 0.82f + snap.SpectralCentroid * 0.18f, 0f, 1f));
            pPaint.Color = col.WithAlpha((byte)(alpha * 225));

            float drawRadius = p.Size * (0.72f + nodalClarity * 0.48f + snap.HatPulse * 0.35f);
            canvas.DrawCircle(sx, sy, drawRadius, pPaint);
        }
    }

    private void DrawMidSideHarmonicHalo(
        SKCanvas canvas,
        float cx,
        float cy,
        float minDim,
        AudioAnalysisSnapshot snap,
        ThemePalette pal,
        float glowIntensity)
    {
        float ringR = minDim * (0.29f + _coreBreath * 0.032f);
        const int pts = 96;
        int binLen = snap.SmoothedSpectrum.Length;

        Span<SKPoint> midPts = stackalloc SKPoint[pts];
        Span<SKPoint> sidePts = stackalloc SKPoint[pts];

        for (int i = 0; i < pts; i++)
        {
            int half = pts / 2;
            int mirrored = i < half ? i : pts - i;
            int bin = Math.Clamp(mirrored * (binLen - 1) / half, 0, binLen - 1);

            float midVal = Math.Clamp(snap.SmoothedSpectrum[bin], 0f, 1.15f);
            float sideVal = Math.Clamp(snap.SideSpectrum[bin], 0f, 1.15f);

            float angle = (i * 2f * Pi / pts) - Pi * 0.5f + _constellationRotation;
            float cos = MathF.Cos(angle);
            float sin = MathF.Sin(angle);

            float rMid = ringR - midVal * (minDim * 0.065f);
            float rSide = ringR + sideVal * (minDim * (0.065f + snap.StereoWidth * 0.095f));

            midPts[i] = new SKPoint(cx + cos * rMid, cy + sin * rMid);
            sidePts[i] = new SKPoint(cx + cos * rSide, cy + sin * rSide);
        }

        using var midPath = BuildSmoothClosedPath(midPts);
        using var sidePath = BuildSmoothClosedPath(sidePts);

        if (glowIntensity > 0.05f)
        {
            using var sideGlowOuter = new SKPaint
            {
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 8.5f,
                BlendMode = SKBlendMode.Plus,
                Color = pal.HighColor.WithAlpha((byte)(34 * glowIntensity))
            };
            using var sideGlowInner = new SKPaint
            {
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 4.2f,
                BlendMode = SKBlendMode.Plus,
                Color = pal.HighColor.WithAlpha((byte)(68 * glowIntensity))
            };
            canvas.DrawPath(sidePath, sideGlowOuter);
            canvas.DrawPath(sidePath, sideGlowInner);
        }

        using var sideStroke = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1.45f,
            BlendMode = SKBlendMode.Plus,
            Color = pal.HighColor.WithAlpha((byte)Math.Clamp(115 + snap.StereoWidth * 110, 90, 235))
        };
        canvas.DrawPath(sidePath, sideStroke);

        using var midStroke = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1.6f,
            BlendMode = SKBlendMode.Plus,
            Color = pal.LowColor.WithAlpha((byte)Math.Clamp(120 + snap.BassEnergy * 95, 95, 225))
        };
        canvas.DrawPath(midPath, midStroke);
    }

    private void DrawHarmonicConstellation(
        SKCanvas canvas,
        float cx,
        float cy,
        float minDim,
        AudioAnalysisSnapshot snap,
        ThemePalette pal,
        float glowIntensity)
    {
        float ringRadius = minDim * (0.31f + _coreBreath * 0.032f);
        Span<SKPoint> nodes = stackalloc SKPoint[12];
        Span<float> energies = stackalloc float[12];

        for (int i = 0; i < 12; i++)
        {
            float angle = (i * 2f * Pi / 12f) - Pi * 0.5f + _constellationRotation;
            nodes[i] = new SKPoint(
                cx + MathF.Cos(angle) * ringRadius,
                cy + MathF.Sin(angle) * ringRadius);
            energies[i] = snap.Chroma[CircleOfFifthsIndices[i]];
        }

        using (var ringPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1.0f,
            Color = pal.GridLine.WithAlpha(115)
        })
        {
            canvas.DrawCircle(cx, cy, ringRadius, ringPaint);
        }

        using var arcGlowPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            BlendMode = SKBlendMode.Plus,
            StrokeCap = SKStrokeCap.Round
        };
        using var arcCorePaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            BlendMode = SKBlendMode.Plus,
            StrokeCap = SKStrokeCap.Round
        };

        for (int i = 0; i < 12; i++)
        {
            if (energies[i] < 0.24f) continue;

            for (int j = i + 1; j < 12; j++)
            {
                if (energies[j] < 0.24f) continue;

                float pairStrength = energies[i] * energies[j];
                if (pairStrength < 0.09f) continue;

                SKPoint a = nodes[i];
                SKPoint b = nodes[j];

                float pull = 0.56f - snap.StereoWidth * 0.14f;
                float ctrlX = cx + ((a.X + b.X) * 0.5f - cx) * pull;
                float ctrlY = cy + ((a.Y + b.Y) * 0.5f - cy) * pull;

                SKColor chordCol = pal.SampleSpectrum((i + j) / 22f);
                byte alpha = (byte)Math.Clamp(pairStrength * 235f, 35, 235);

                using var path = new SKPath();
                path.MoveTo(a);
                if (snap.SnarePulse > 0.38f && pairStrength > 0.22f)
                {
                    // Snare transient snaps crisp crystalline lightning across active chord!
                    float midX = (a.X + b.X + 2f * ctrlX) * 0.25f;
                    float midY = (a.Y + b.Y + 2f * ctrlY) * 0.25f;
                    float jitter = snap.SnarePulse * minDim * 0.024f;
                    float jx = MathF.Sin(_time * 47.0f + i * 3.1f + j * 1.7f) * jitter;
                    float jy = MathF.Cos(_time * 53.0f + i * 2.3f - j * 2.9f) * jitter;
                    path.LineTo(midX + jx, midY + jy);
                    path.LineTo(b);
                }
                else
                {
                    path.QuadTo(ctrlX, ctrlY, b.X, b.Y);
                }

                if (glowIntensity > 0.05f)
                {
                    arcGlowPaint.Color = chordCol.WithAlpha((byte)(alpha * 0.24f * glowIntensity));
                    arcGlowPaint.StrokeWidth = 6.5f + pairStrength * 8.0f;
                    canvas.DrawPath(path, arcGlowPaint);

                    arcGlowPaint.Color = chordCol.WithAlpha((byte)(alpha * 0.48f * glowIntensity));
                    arcGlowPaint.StrokeWidth = 3.0f + pairStrength * 4.2f;
                    canvas.DrawPath(path, arcGlowPaint);
                }

                arcCorePaint.Color = VisualizerPalettes.BlendColor(chordCol, pal.PeakCapColor, 0.42f).WithAlpha(alpha);
                arcCorePaint.StrokeWidth = 0.95f + pairStrength * 2.0f;
                canvas.DrawPath(path, arcCorePaint);
            }
        }

        using var nodePaint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill, BlendMode = SKBlendMode.Plus };
        using var haloPaint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill, BlendMode = SKBlendMode.Plus };
        using var textPaint = new SKPaint
        {
            IsAntialias = true,
            TextSize = Math.Clamp(minDim * 0.0175f, 9.5f, 13.5f),
            Typeface = NoteTypeface,
            TextAlign = SKTextAlign.Center
        };

        float labelRadius = ringRadius + Math.Clamp(minDim * 0.036f, 16f, 28f);

        for (int i = 0; i < 12; i++)
        {
            float e = energies[i];
            SKPoint pt = nodes[i];
            SKColor col = pal.SampleSpectrum(i / 11f);

            if (e > 0.14f && glowIntensity > 0.05f)
            {
                float haloR = 11f + e * 16f;
                using var radGrad = SKShader.CreateRadialGradient(
                    pt,
                    haloR,
                    new[] { col.WithAlpha((byte)Math.Clamp(e * 195f * glowIntensity, 20, 210)), SKColors.Transparent },
                    null,
                    SKShaderTileMode.Clamp);
                haloPaint.Shader = radGrad;
                canvas.DrawCircle(pt, haloR, haloPaint);
                haloPaint.Shader = null;
            }

            nodePaint.Color = VisualizerPalettes.BlendColor(col, pal.PeakCapColor, e * 0.65f)
                .WithAlpha((byte)Math.Clamp(85 + e * 170, 85, 255));
            canvas.DrawCircle(pt, 2.4f + e * 4.2f, nodePaint);

            float angle = (i * 2f * Pi / 12f) - Pi * 0.5f + _constellationRotation;
            float lx = cx + MathF.Cos(angle) * labelRadius;
            float ly = cy + MathF.Sin(angle) * labelRadius + textPaint.TextSize * 0.35f;

            textPaint.Color = e > 0.30f
                ? VisualizerPalettes.BlendColor(col, pal.PeakCapColor, 0.55f).WithAlpha(240)
                : new SKColor(148, 163, 184, 120);
            canvas.DrawText(CircleOfFifthsLabels[i], lx, ly, textPaint);
        }
    }

    private void DrawStereoLissajousRibbons(
        SKCanvas canvas,
        float cx,
        float cy,
        float minDim,
        AudioAnalysisSnapshot snap,
        ThemePalette pal,
        float glowIntensity,
        bool isFeedbackPass)
    {
        int len = snap.WaveformLeft.Length;
        if (len < 8) return;

        float ampRadius = minDim * (0.20f + snap.StereoWidth * 0.13f + snap.BassEnergy * 0.05f);
        float energyBoost = 1.0f + snap.KickPulse * 0.25f;

        using var path = new SKPath();
        int step = isFeedbackPass ? 3 : 2;
        bool started = false;

        float rot = _swirlPhase * 0.35f;
        float cosR = MathF.Cos(rot);
        float sinR = MathF.Sin(rot);

        for (int i = 0; i < len; i += step)
        {
            float l = Math.Clamp(snap.WaveformLeft[i], -1.1f, 1.1f);
            float r = Math.Clamp(snap.WaveformRight[i], -1.1f, 1.1f);

            float side = (l - r) * 0.7071f * (1.15f + snap.StereoWidth * 0.65f);
            float mid = (l + r) * 0.7071f;

            float rx = (side * cosR - mid * sinR) * ampRadius * energyBoost;
            float ry = -(side * sinR + mid * cosR) * ampRadius * energyBoost;

            float px = cx + rx;
            float py = cy + ry;

            if (!started)
            {
                path.MoveTo(px, py);
                started = true;
            }
            else
            {
                path.LineTo(px, py);
            }
        }

        if (!started) return;

        var stops = pal.SpectrumStops;
        using var gradShader = SKShader.CreateSweepGradient(
            new SKPoint(cx, cy),
            stops,
            null);

        if (isFeedbackPass)
        {
            using var fbRibbonPaint = new SKPaint
            {
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 1.4f + snap.BassEnergy * 1.4f,
                BlendMode = SKBlendMode.Plus,
                Shader = gradShader,
                Color = new SKColor(255, 255, 255, (byte)Math.Clamp(34 + glowIntensity * 30, 26, 72))
            };
            canvas.DrawPath(path, fbRibbonPaint);
            return;
        }

        if (glowIntensity > 0.05f)
        {
            using var outerAuraPaint = new SKPaint
            {
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 9.5f + snap.BassEnergy * 6.0f,
                BlendMode = SKBlendMode.Plus,
                Shader = gradShader,
                Color = new SKColor(255, 255, 255, (byte)Math.Clamp(42 * glowIntensity, 12, 72))
            };
            using var innerAuraPaint = new SKPaint
            {
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 4.6f + snap.BassEnergy * 3.0f,
                BlendMode = SKBlendMode.Plus,
                Shader = gradShader,
                Color = new SKColor(255, 255, 255, (byte)Math.Clamp(95 * glowIntensity, 22, 145))
            };
            canvas.DrawPath(path, outerAuraPaint);
            canvas.DrawPath(path, innerAuraPaint);
        }

        using var corePaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1.65f + snap.SnarePulse * 1.4f,
            BlendMode = SKBlendMode.Plus,
            Color = VisualizerPalettes.BlendColor(pal.AccentGlow, pal.PeakCapColor, 0.55f + snap.SnarePulse * 0.35f)
                .WithAlpha((byte)Math.Clamp(160 + _smoothedEnergy * 95, 130, 250))
        };
        canvas.DrawPath(path, corePaint);
    }

    private void DrawRefractiveShockwaveRings(
        SKCanvas canvas,
        float cx,
        float cy,
        float minDim,
        ThemePalette pal,
        float glowIntensity)
    {
        using var ringPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            BlendMode = SKBlendMode.Plus
        };

        for (int i = 0; i < MaxShockwaves; i++)
        {
            var sw = _shockwaves[i];
            if (sw.Strength <= 0.03f) continue;

            float r = sw.Radius * minDim * 0.52f;
            byte a = (byte)Math.Clamp(sw.Strength * 195f * (0.6f + glowIntensity * 0.5f), 0, 220);
            ringPaint.Color = pal.HighColor.WithAlpha(a);
            ringPaint.StrokeWidth = 1.4f + sw.Strength * 2.8f;
            canvas.DrawCircle(cx, cy, r, ringPaint);
        }
    }

    private void DrawHatSparkles(
        SKCanvas canvas,
        float cx,
        float cy,
        float minDim,
        ThemePalette pal,
        float glowIntensity)
    {
        using var sparkPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeCap = SKStrokeCap.Round,
            BlendMode = SKBlendMode.Plus
        };

        for (int i = 0; i < _hatSparkles.Length; i++)
        {
            ref var s = ref _hatSparkles[i];
            if (s.Alpha <= 0.02f) continue;

            s.Alpha *= 0.87f;
            s.Radius += 0.0045f;

            float rad = s.Radius * minDim * 0.49f;
            float x = cx + MathF.Cos(s.Angle) * rad;
            float y = cy + MathF.Sin(s.Angle) * rad;
            float arm = s.Size * (1.2f + glowIntensity);

            sparkPaint.StrokeWidth = 1.2f;
            sparkPaint.Color = VisualizerPalettes.BlendColor(pal.HighColor, pal.PeakCapColor, 0.7f)
                .WithAlpha((byte)(s.Alpha * 235));

            canvas.DrawLine(x - arm, y, x + arm, y, sparkPaint);
            canvas.DrawLine(x, y - arm, x, y + arm, sparkPaint);
        }
    }

    private void DrawSingularityCore(
        SKCanvas canvas,
        float cx,
        float cy,
        float minDim,
        AudioAnalysisSnapshot snap,
        ThemePalette pal,
        float glowIntensity)
    {
        float coreR = minDim * (0.048f + _coreBreath * 0.022f);

        // Deep obsidian event-horizon core disk so live chord text & central constellation remain razor-legible
        using (var wellPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Shader = SKShader.CreateRadialGradient(
                new SKPoint(cx, cy),
                coreR * 1.45f,
                new[]
                {
                    pal.BackgroundBottom.WithAlpha(215),
                    pal.BackgroundTop.WithAlpha(130),
                    SKColors.Transparent
                },
                new[] { 0f, 0.62f, 1f },
                SKShaderTileMode.Clamp)
        })
        {
            canvas.DrawCircle(cx, cy, coreR * 1.45f, wellPaint);
        }

        // Luminous event-horizon halo ring around the core
        using var orbRimPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1.6f + snap.KickPulse * 1.8f,
            BlendMode = SKBlendMode.Plus,
            Color = VisualizerPalettes.BlendColor(pal.AccentGlow, pal.PeakCapColor, 0.45f)
                .WithAlpha((byte)Math.Clamp(135 + snap.KickPulse * 110, 110, 245))
        };
        canvas.DrawCircle(cx, cy, coreR * 0.92f, orbRimPaint);

        using var orbGlowPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            BlendMode = SKBlendMode.Plus,
            Shader = SKShader.CreateRadialGradient(
                new SKPoint(cx, cy),
                coreR * 2.4f,
                new[]
                {
                    SKColors.Transparent,
                    pal.AccentGlow.WithAlpha((byte)Math.Clamp(52 + glowIntensity * 55 + snap.KickPulse * 55, 30, 155)),
                    pal.LowColor.WithAlpha(28),
                    SKColors.Transparent
                },
                new[] { 0.25f, 0.42f, 0.72f, 1f },
                SKShaderTileMode.Clamp)
        };
        canvas.DrawCircle(cx, cy, coreR * 2.4f, orbGlowPaint);

        if (!string.IsNullOrEmpty(snap.DetectedChordLabel))
        {
            using var chordPaint = new SKPaint
            {
                IsAntialias = true,
                Color = pal.PeakCapColor.WithAlpha(240),
                TextSize = Math.Clamp(minDim * 0.023f, 11f, 19f),
                Typeface = NoteTypeface,
                TextAlign = SKTextAlign.Center
            };
            canvas.DrawText(snap.DetectedChordLabel, cx, cy + chordPaint.TextSize * 0.34f, chordPaint);
        }
    }

    private void DisposeBuffers()
    {
        _fbCanvasA?.Dispose();
        _fbCanvasB?.Dispose();
        _fbA?.Dispose();
        _fbB?.Dispose();
        _fbCanvasA = null;
        _fbCanvasB = null;
        _fbA = null;
        _fbB = null;
    }

    public void Dispose()
    {
        DisposeBuffers();
    }
}
