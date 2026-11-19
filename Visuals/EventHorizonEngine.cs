using System.Threading.Tasks;
using SkiaSharp;
using SonicDeck.Audio;

namespace SonicDeck.Visuals;

/// <summary>
/// Flagship Space Visualization Engine ("Event Horizon"):
/// 1. Relativistic Gargantua Black Hole with Schwarzschild Gravitational Lensing,
///    Upper/Lower Einstein Halo Arches, Keplerian Accretion Disk, Doppler Beaming,
///    and Domain-Warped Deep-Space Nebula (Fused Multithreaded Pixel Kernel)
/// 2. 3D Hyperspace Warp Starfield with Gravitational Deflection & Bass Warp Streaks
/// 3. Twin Relativistic Polar Quasar Jets with Helical Stereo Waveform Magnetic Ribbons
/// 4. True 3D Depth-Occluded Orbital Accretion Plasma Embers, Gravitational Ripples & Pulsar Glints
/// </summary>
public sealed class EventHorizonEngine : IDisposable
{
    private const int StarCount = 420;
    private const int EmberCount = 360;
    private const int PulsarCount = 36;
    private const int MaxWaves = 3;
    private const float Pi = 3.14159265f;
    private const float TwoPi = 6.28318530f;

    private struct WarpStar
    {
        public float X;          // 3D space [-2.2, 2.2]
        public float Y;
        public float Z;          // Depth (0.06 .. 1.0)
        public float PrevZ;
        public float Brightness;
        public float TintPos;    // 0..1 star color temperature / palette tint
        public float Size;
    }

    private struct OrbitalEmber
    {
        public float Angle;      // Orbital azimuth
        public float Radius;     // Orbital radius in normalized black hole units (0.30 .. 1.55)
        public float YOffset;    // Vertical thickness perturbation
        public float Speed;      // Base Keplerian multiplier
        public float Size;
        public float Brightness;
    }

    private struct GravWave
    {
        public float Radius;
        public float Strength;
    }

    private struct PulsarGlint
    {
        public float Nx;         // Normalized screen [-0.92, 0.92]
        public float Ny;
        public float Alpha;
        public float Size;
        public float Tint;
    }

    private readonly WarpStar[] _stars = new WarpStar[StarCount];
    private readonly OrbitalEmber[] _embers = new OrbitalEmber[EmberCount];
    private readonly PulsarGlint[] _pulsars = new PulsarGlint[PulsarCount];
    private readonly GravWave[] _waves = new GravWave[MaxWaves];
    private readonly float[] _ringSpectrum = new float[64];
    private readonly Random _rng = new(20141107); // Interstellar release date seed :)

    private SKBitmap? _fieldA;
    private SKBitmap? _fieldB;
    private int _fieldW;
    private int _fieldH;
    private bool _fieldInit;

    private float _time;
    private float _diskPhase;
    private float _tiltRoll = -0.22f;
    private float _warpVelocity;
    private float _horizonPulse;
    private float _jetSurge;

    public EventHorizonEngine()
    {
        for (int i = 0; i < StarCount; i++)
            _stars[i] = CreateStar(randomDepth: true);

        for (int i = 0; i < EmberCount; i++)
            _embers[i] = CreateEmber();

        for (int i = 0; i < PulsarCount; i++)
            _pulsars[i] = new PulsarGlint();
    }

    private WarpStar CreateStar(bool randomDepth)
    {
        // Distribute stars avoiding exact dead-center singularity so they fly dramatically around it
        float ang = (float)(_rng.NextDouble() * TwoPi);
        float rad = (float)(0.18 + Math.Pow(_rng.NextDouble(), 0.72) * 1.95);
        float z = randomDepth ? (float)(0.08 + _rng.NextDouble() * 0.92) : (float)(0.88 + _rng.NextDouble() * 0.14);
        return new WarpStar
        {
            X = MathF.Cos(ang) * rad,
            Y = MathF.Sin(ang) * rad * 0.72f,
            Z = z,
            PrevZ = z,
            Brightness = (float)(0.35 + _rng.NextDouble() * 0.65),
            TintPos = (float)_rng.NextDouble(),
            Size = (float)(0.85 + _rng.NextDouble() * 1.75)
        };
    }

    private OrbitalEmber CreateEmber()
    {
        float r = (float)(0.32 + Math.Pow(_rng.NextDouble(), 1.35) * 1.18);
        return new OrbitalEmber
        {
            Angle = (float)(_rng.NextDouble() * TwoPi),
            Radius = r,
            YOffset = (float)((_rng.NextDouble() - 0.5) * 0.045 * (r + 0.3)),
            Speed = (float)(0.85 + _rng.NextDouble() * 0.35),
            Size = (float)(1.0 + _rng.NextDouble() * 2.1),
            Brightness = (float)(0.45 + _rng.NextDouble() * 0.55)
        };
    }

    private void EnsureFieldBuffers(int canvasW, int canvasH)
    {
        const float maxDim = 360f;
        float scale = Math.Min(1f, maxDim / Math.Max(canvasW, canvasH));
        int targetW = Math.Clamp((int)(canvasW * scale), 160, 380);
        int targetH = Math.Clamp((int)(canvasH * scale), 96, 380);

        if (_fieldA != null && _fieldB != null && _fieldW == targetW && _fieldH == targetH)
            return;

        DisposeBuffers();

        var info = new SKImageInfo(targetW, targetH, SKColorType.Bgra8888, SKAlphaType.Premul);
        _fieldA = new SKBitmap(info);
        _fieldB = new SKBitmap(info);
        _fieldW = targetW;
        _fieldH = targetH;
        _fieldInit = false;
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

        EnsureFieldBuffers(width, height);

        float energy = snap.BassEnergy * 0.45f + snap.MidEnergy * 0.35f + snap.HighEnergy * 0.20f;
        _time += 0.016f + energy * 0.014f;
        _diskPhase += 0.022f + snap.BassEnergy * 0.042f + snap.MidEnergy * 0.024f;

        // Subtle relativistic precession + stereo tilt response
        float targetRoll = -0.22f + MathF.Sin(_time * 0.23f) * 0.09f + (snap.LeftRms - snap.RightRms) * 0.22f;
        _tiltRoll += (targetRoll - _tiltRoll) * 0.06f;

        float targetWarp = 0.0045f + snap.BassEnergy * 0.024f + snap.KickPulse * 0.032f * beatFlashIntensity;
        _warpVelocity += (targetWarp - _warpVelocity) * 0.18f;

        _horizonPulse += (snap.BassEnergy * 0.55f + snap.KickPulse * 0.65f * beatFlashIntensity - _horizonPulse) * 0.24f;
        _jetSurge += (snap.SnarePulse * 0.85f + snap.HighEnergy * 0.55f + snap.MidEnergy * 0.35f - _jetSurge) * 0.22f;

        // Downsample 128-bin spectrum into 64 crisp accretion rings
        int srcLen = snap.SmoothedSpectrum.Length;
        for (int i = 0; i < 64; i++)
        {
            int b = i * (srcLen - 1) / 63;
            _ringSpectrum[i] = Math.Clamp(snap.SmoothedSpectrum[b], 0f, 1.2f);
        }

        // Spawn gravitational lensing wave on Kick
        if (snap.IsKickFrame)
        {
            SpawnGravWave(Math.Clamp(0.55f + snap.KickPulse * 0.45f * beatFlashIntensity, 0.4f, 1.0f));
        }

        for (int i = 0; i < MaxWaves; i++)
        {
            if (_waves[i].Strength > 0.01f)
            {
                _waves[i].Radius += 0.026f + snap.BassEnergy * 0.020f;
                _waves[i].Strength *= 0.942f;
                if (_waves[i].Radius > 1.85f)
                    _waves[i].Strength = 0f;
            }
        }

        // Spawn distant pulsar diffraction flares on Hi-Hat transients
        if (snap.IsHatFrame || snap.HatPulse > 0.42f)
        {
            int burst = snap.IsHatFrame ? 4 : 1;
            for (int k = 0; k < burst; k++)
            {
                int idx = _rng.Next(PulsarCount);
                float px, py;
                // Keep pulsar glints outside the black hole shadow
                do
                {
                    px = (float)(_rng.NextDouble() * 1.84 - 0.92);
                    py = (float)(_rng.NextDouble() * 1.76 - 0.88);
                } while (px * px + py * py < 0.18f);

                _pulsars[idx] = new PulsarGlint
                {
                    Nx = px,
                    Ny = py,
                    Alpha = Math.Clamp(0.65f + snap.HatPulse * 0.45f, 0.5f, 1.0f),
                    Size = (float)(4.5 + _rng.NextDouble() * 8.5),
                    Tint = (float)_rng.NextDouble()
                };
            }
        }

        float cx = width * 0.5f;
        float cy = height * 0.5f;
        float minDim = Math.Min(width, height);
        float scale = minDim * 0.5f;
        float rs = 0.245f + _horizonPulse * 0.026f;
        float cosRoll = MathF.Cos(_tiltRoll);
        float sinRoll = MathF.Sin(_tiltRoll);

        // Layer 1: Gargantua Black Hole + Lensed Accretion Disk + Deep-Space Nebula (Fused Parallel Pixel Kernel)
        RenderBlackHoleAndNebula(canvas, width, height, snap, pal, rs, cosRoll, sinRoll, glowIntensity, beatFlashIntensity, showMirrorShimmer);

        // Layer 2: 3D Hyperspace Warp Starfield (gravitationally bent around the event horizon)
        int activeStars = Math.Clamp(bandCount * 5 + 140, 220, StarCount);
        UpdateAndDrawWarpStars(canvas, cx, cy, width, height, scale, rs, snap, pal, glowIntensity, activeStars);

        // Layer 3a: Distant Supernova Pulsar Glints (Hi-Hat transients)
        DrawPulsarGlints(canvas, cx, cy, width, height, pal, glowIntensity);

        // Layer 3b: Twin Relativistic Polar Quasar Jets + Helical Stereo Waveform Magnetic Ribbons
        DrawRelativisticPolarJets(canvas, cx, cy, scale, rs, cosRoll, sinRoll, snap, pal, glowIntensity);

        // Layer 4: True 3D Depth-Occluded Orbital Accretion Plasma Embers + Gravitational Wave Rings
        int activeEmbers = Math.Clamp(bandCount * 4 + 100, 160, EmberCount);
        UpdateAndDrawOrbitalEmbers(canvas, cx, cy, scale, rs, cosRoll, sinRoll, snap, pal, glowIntensity, activeEmbers);
        DrawGravitationalWaveRings(canvas, cx, cy, scale, pal, glowIntensity);
    }

    private void SpawnGravWave(float strength)
    {
        int slot = 0;
        float lowest = float.MaxValue;
        for (int i = 0; i < MaxWaves; i++)
        {
            if (_waves[i].Strength < lowest)
            {
                lowest = _waves[i].Strength;
                slot = i;
            }
        }
        _waves[slot] = new GravWave { Radius = 0.26f, Strength = strength };
    }

    private unsafe void RenderBlackHoleAndNebula(
        SKCanvas mainCanvas,
        int mainW,
        int mainH,
        AudioAnalysisSnapshot snap,
        ThemePalette pal,
        float rs,
        float cosRoll,
        float sinRoll,
        float glowIntensity,
        float beatFlashIntensity,
        bool showMirrorShimmer)
    {
        if (_fieldA == null || _fieldB == null)
            return;

        int w = _fieldW;
        int h = _fieldH;
        float halfW = w * 0.5f;
        float halfH = h * 0.5f;
        float invScale = 1f / MathF.Max(1f, MathF.Min(w, h) * 0.5f);

        float time = _time;
        float diskPhase = _diskPhase;
        float bass = Math.Clamp(snap.BassEnergy, 0f, 1.3f);
        float mid = Math.Clamp(snap.MidEnergy, 0f, 1.3f);
        float high = Math.Clamp(snap.HighEnergy, 0f, 1.3f);
        float kick = Math.Clamp(snap.KickPulse * beatFlashIntensity, 0f, 1.3f);
        float snare = Math.Clamp(snap.SnarePulse * beatFlashIntensity, 0f, 1.3f);
        float stereo = Math.Clamp(snap.StereoWidth, 0f, 1f);
        float centroid = Math.Clamp(snap.SpectralCentroid, 0f, 1f);

        float rsSq = rs * rs;
        float diskSquash = 4.65f - stereo * 0.55f; // Shallow Interstellar viewing angle
        float nebulaGain = (0.48f + mid * 0.44f + bass * 0.26f) * (0.74f + glowIntensity * 0.46f);
        float diskGain = (0.92f + bass * 0.55f + mid * 0.42f + kick * 0.46f) * (0.70f + glowIntensity * 0.46f);
        float photonGain = (0.92f + kick * 1.05f + bass * 0.48f + snare * 0.40f) * (0.75f + glowIntensity * 0.38f);
        float trailMix = _fieldInit ? (showMirrorShimmer ? 0.42f : 0.28f) : 0f;

        float w0R = _waves[0].Radius, w0S = _waves[0].Strength;
        float w1R = _waves[1].Radius, w1S = _waves[1].Strength;
        float w2R = _waves[2].Radius, w2S = _waves[2].Strength;

        // Palette colors in float RGB [0..1]
        float lowR = pal.LowColor.Red / 255f, lowG = pal.LowColor.Green / 255f, lowB = pal.LowColor.Blue / 255f;
        float midR = pal.MidColor.Red / 255f, midG = pal.MidColor.Green / 255f, midB = pal.MidColor.Blue / 255f;
        float hiR = pal.HighColor.Red / 255f, hiG = pal.HighColor.Green / 255f, hiB = pal.HighColor.Blue / 255f;
        float accR = pal.AccentGlow.Red / 255f, accG = pal.AccentGlow.Green / 255f, accB = pal.AccentGlow.Blue / 255f;
        float hotR = pal.PeakCapColor.Red / 255f, hotG = pal.PeakCapColor.Green / 255f, hotB = pal.PeakCapColor.Blue / 255f;

        // Deep space cosmic void base (darkened version of theme background)
        float spaceR = (pal.BackgroundBottom.Red / 255f) * 0.48f;
        float spaceG = (pal.BackgroundBottom.Green / 255f) * 0.48f;
        float spaceB = (pal.BackgroundBottom.Blue / 255f) * 0.58f;

        float[] spec = _ringSpectrum;
        IntPtr prevPtr = _fieldA.GetPixels();
        IntPtr dstPtr = _fieldB.GetPixels();

        Parallel.For(0, h, y =>
        {
            uint* prevRow = (uint*)prevPtr + y * w;
            uint* dstRow = (uint*)dstPtr + y * w;

            float uy = ((y + 0.5f) - halfH) * invScale;

            for (int x = 0; x < w; x++)
            {
                float ux = ((x + 0.5f) - halfW) * invScale;

                // Rotate into black hole local frame: lx = equatorial disk axis, ly = polar jet axis
                float lx = ux * cosRoll - uy * sinRoll;
                float ly = ux * sinRoll + uy * cosRoll;

                float r2 = lx * lx + ly * ly;
                float r = MathF.Sqrt(r2 + 1e-6f);

                // Gravitational spacetime ripple from kick shockwaves
                float ripple = 0f;
                if (w0S > 0.01f)
                {
                    float d0 = r - w0R;
                    if (MathF.Abs(d0) < 0.28f)
                        ripple += MathF.Sin(d0 * 28f) * MathF.Exp(-MathF.Abs(d0) * 16f) * w0S * 0.055f;
                }
                if (w1S > 0.01f)
                {
                    float d1 = r - w1R;
                    if (MathF.Abs(d1) < 0.28f)
                        ripple += MathF.Sin(d1 * 28f) * MathF.Exp(-MathF.Abs(d1) * 16f) * w1S * 0.055f;
                }
                if (w2S > 0.01f)
                {
                    float d2 = r - w2R;
                    if (MathF.Abs(d2) < 0.28f)
                        ripple += MathF.Sin(d2 * 28f) * MathF.Exp(-MathF.Abs(d2) * 16f) * w2S * 0.055f;
                }

                float rEff = r + ripple;

                // 1. Deep-Space Volumetric Nebula (gravitationally lensed around the black hole!)
                float colR = spaceR;
                float colG = spaceG;
                float colB = spaceB;

                float horizonMask = SmoothStep(rs * 0.96f, rs * 1.03f, rEff);

                if (horizonMask > 0.002f)
                {
                    // Schwarzschild ray bending bends background nebula around the Einstein ring
                    float bend = 1f - (rsSq * 0.85f) / (r2 + rsSq * 0.45f);
                    float nx = ux * bend * 1.65f + time * 0.055f;
                    float ny = uy * bend * 1.65f - time * 0.032f;

                    // Fast 3-octave domain-warped interstellar nebula clouds
                    float w1 = MathF.Sin(nx * 1.9f + MathF.Cos(ny * 2.3f + time * 0.18f) * 1.3f);
                    float w2 = MathF.Cos(ny * 2.7f - MathF.Sin(nx * 2.1f - time * 0.14f) * 1.4f + w1 * 0.8f);
                    float w3 = MathF.Sin((nx + ny) * 3.8f + w2 * 1.2f + time * 0.22f);

                    float nebDensity = MathF.Max(0f, (w1 * 0.45f + w2 * 0.35f + w3 * 0.20f) * 0.55f + 0.25f);
                    float nebDust = nebDensity * nebDensity;
                    float nebCloud = nebDust * SmoothStep(2.35f, 0.32f, r) * nebulaGain;

                    // Ionized nebula ridges (bright filaments inside cosmic dust)
                    float nebRidge = MathF.Exp(-MathF.Abs(w1 + w2 * 0.65f) * 5.2f) * nebDust * nebulaGain * (0.45f + high * 0.55f);

                    float nebMix = Math.Clamp(0.5f + 0.5f * w2, 0f, 1f);
                    float nR = lowR + (midR - lowR) * nebMix;
                    float nG = lowG + (midG - lowG) * nebMix;
                    float nB = lowB + (midB - lowB) * nebMix;

                    colR += (nR * nebCloud + accR * nebRidge) * horizonMask;
                    colG += (nG * nebCloud + accG * nebRidge) * horizonMask;
                    colB += (nB * nebCloud + accB * nebRidge) * horizonMask;
                }

                // 2. Relativistic Doppler Beaming factor (left side approaching -> hotter/brighter)
                float doppler = Math.Clamp(1.05f - 0.52f * (lx / (r + 0.22f)), 0.48f, 1.65f);

                // 3. Gravitationally Lensed Top & Bottom Einstein Halo Arches (Far-side accretion disk bent over & under!)
                if (horizonMask > 0.002f && rEff < rs * 3.05f)
                {
                    float dr = rEff - rs * 1.01f;
                    if (dr > -0.02f)
                    {
                        float normDr = MathF.Max(0f, dr) / (rs * 1.65f);
                        // Primary upper arch (ly < 0) slightly brighter than secondary lower arch (ly > 0)
                        float polarWeight = MathF.Abs(ly) / (r + 0.001f);
                        float archAsym = ly < 0f ? 1.05f : 0.76f;
                        float archEnv = MathF.Exp(-normDr * (3.1f - bass * 0.6f))
                                      * SmoothStep(0.08f, 0.52f, polarWeight)
                                      * archAsym;

                        if (archEnv > 0.004f)
                        {
                            float phiLensed = MathF.Atan2(ly, lx);
                            float virtualR = rs * 1.08f + MathF.Max(0f, dr) * 1.55f;
                            int sIdx = Math.Clamp((int)(normDr * 58f), 0, 63);
                            float audioBoost = 0.55f + spec[sIdx] * 0.95f;

                            float streaks = 0.52f + 0.48f * MathF.Sin(virtualR * 36f - diskPhase * 2.8f + MathF.Sin(phiLensed * 3f - diskPhase * 0.9f) * 2.0f);
                            float fine = 0.60f + 0.40f * MathF.Cos(phiLensed * 6f + virtualR * 22f - diskPhase * 2.2f);
                            float haloIntensity = archEnv * streaks * fine * audioBoost * doppler * diskGain * 0.85f;

                            // Hot inner photon-edge color -> cooler outer accretion color
                            float heat = Math.Clamp(1f - normDr * 1.15f + (doppler - 1f) * 0.35f, 0f, 1f);
                            AddBlackbodyThemeColor(heat, lowR, lowG, lowB, midR, midG, midB, hiR, hiG, hiB, hotR, hotG, hotB, haloIntensity * horizonMask, ref colR, ref colG, ref colB);
                        }
                    }
                }

                // 4. Razor-Sharp Incandescent Einstein Photon Ring right outside the Event Horizon!
                // Masked cleanly outside the shadow so the black hole sphere stays terrifyingly crisp & deep black.
                float photonDist = rEff - rs * 1.01f;
                if (photonDist > -0.025f && photonDist < 0.14f)
                {
                    float outsideMask = SmoothStep(-0.015f, 0.012f, photonDist);
                    float absDist = MathF.Abs(photonDist);
                    float coreRing = MathF.Exp(-absDist * 78f);
                    float softCorona = MathF.Exp(-MathF.Max(0f, photonDist) * 18f) * 0.28f * outsideMask;
                    float ringSparkle = 0.85f + 0.15f * MathF.Sin(MathF.Atan2(ly, lx) * 8f - diskPhase * 4.5f);
                    float pInt = (coreRing * outsideMask + softCorona) * ringSparkle * doppler * photonGain;

                    colR += (hiR * 0.55f + hotR * 0.45f) * pInt;
                    colG += (hiG * 0.55f + hotG * 0.45f) * pInt;
                    colB += (hiB * 0.55f + hotB * 0.45f) * pInt;
                }

                // 5. Primary Tilted Equatorial Accretion Disk (Front half crosses dramatically IN FRONT of the black hole shadow!)
                float diskY = ly * diskSquash;
                float diskR = MathF.Sqrt(lx * lx + diskY * diskY + 1e-6f);
                float outerLimit = 1.62f + bass * 0.18f;

                if (diskR > rs * 1.02f && diskR < outerLimit)
                {
                    // Front half of the equatorial disk (ly >= -0.02*rs) passes IN FRONT of the event horizon!
                    // Back half (ly < 0) is occluded by the black hole shadow inside r < rs.
                    float isFront = SmoothStep(-0.045f, 0.035f, ly);
                    float visibility = MathF.Max(isFront, horizonMask);

                    if (visibility > 0.004f)
                    {
                        float diskNorm = Math.Clamp((diskR - rs * 1.04f) / (outerLimit - rs * 1.04f), 0f, 1f);
                        float innerEdge = SmoothStep(rs * 1.02f, rs * 1.15f, diskR);
                        float outerEdge = SmoothStep(outerLimit, rs * 1.25f, diskR);

                        // Vertical thickness envelope of glowing plasma disk
                        float vertThickness = 0.075f + diskNorm * 0.12f + bass * 0.035f;
                        float vertEnv = MathF.Exp(-(ly * ly) / (vertThickness * vertThickness * 0.22f));

                        float phi = MathF.Atan2(diskY, lx);
                        float keplerOmega = 2.1f / (diskR + 0.20f);

                        // Swirling Keplerian logarithmic spiral plasma arms
                        float arm1 = 0.5f + 0.5f * MathF.Sin(phi * 2f + diskR * 15f - diskPhase * keplerOmega);
                        float arm2 = 0.5f + 0.5f * MathF.Sin(phi * 5f - diskR * 28f + diskPhase * keplerOmega * 0.72f);
                        float ringBands = 0.55f + 0.45f * MathF.Sin(diskR * 46f - diskPhase * 2.6f + arm1 * 2.2f);

                        // Live frequency spectrum drives radial plasma ring brightness (Bass inner -> Treble outer)
                        int specIdx = Math.Clamp((int)(diskNorm * 63f), 0, 63);
                        float specVal = spec[specIdx];
                        float audioPlasma = 0.48f + specVal * 1.15f;

                        float plasma = innerEdge * outerEdge * vertEnv * (0.35f + arm1 * 0.42f + arm2 * 0.23f) * ringBands * audioPlasma * doppler * diskGain;

                        // Front disk slightly darkens/occludes behind-it event horizon naturally before adding bright plasma
                        float heat = Math.Clamp(1.05f - diskNorm * 1.05f + (doppler - 1f) * 0.32f + specVal * 0.22f, 0f, 1f);
                        AddBlackbodyThemeColor(heat, lowR, lowG, lowB, midR, midG, midB, hiR, hiG, hiB, hotR, hotG, hotB, plasma * visibility, ref colR, ref colG, ref colB);
                    }
                }

                // 6. Subtle volumetric polar jet base glow emerging from North & South horizon poles
                if (horizonMask > 0.02f)
                {
                    float poleSpread = 0.065f + MathF.Abs(ly) * (0.14f + stereo * 0.06f);
                    float jetEnv = MathF.Exp(-(lx * lx) / (poleSpread * poleSpread))
                                 * SmoothStep(rs * 0.92f, rs * 1.25f, MathF.Abs(ly))
                                 * SmoothStep(1.45f, 0.28f, MathF.Abs(ly));
                    if (jetEnv > 0.005f)
                    {
                        float jetPulse = 0.65f + 0.35f * MathF.Sin(MathF.Abs(ly) * 22f - time * 14f);
                        float jInt = jetEnv * jetPulse * (0.16f + snare * 0.75f + high * 0.45f + centroid * 0.25f) * (0.65f + glowIntensity * 0.55f);
                        colR += (accR * 0.65f + hiR * 0.35f) * jInt;
                        colG += (accG * 0.65f + hiG * 0.35f) * jInt;
                        colB += (accB * 0.65f + hiB * 0.35f) * jInt;
                    }
                }

                // 7. Gentle temporal afterglow blend (keeps plasma silky while keeping the event horizon shadow crisp!)
                if (trailMix > 0.01f)
                {
                    // Suppress trail inside the obsidian shadow so the black hole never looks muddy
                    float localTrail = trailMix * (0.12f + 0.88f * horizonMask);
                    uint p = prevRow[x];
                    float pr = ((p >> 16) & 0xFF) * (1f / 255f);
                    float pg = ((p >> 8) & 0xFF) * (1f / 255f);
                    float pb = (p & 0xFF) * (1f / 255f);

                    colR = colR * (1f - localTrail) + MathF.Max(colR, pr * 0.94f) * localTrail;
                    colG = colG * (1f - localTrail) + MathF.Max(colG, pg * 0.94f) * localTrail;
                    colB = colB * (1f - localTrail) + MathF.Max(colB, pb * 0.94f) * localTrail;
                }

                // 8. Filmic HDR soft-shoulder tone mapping (keeps hot core blazing & outer plasma jewel-saturated)
                float maxCh = MathF.Max(colR, MathF.Max(colG, colB));
                if (maxCh > 0.82f)
                {
                    float compressed = 0.82f + (maxCh - 0.82f) / (1f + (maxCh - 0.82f) * 2.6f);
                    float ratio = compressed / maxCh;
                    colR *= ratio;
                    colG *= ratio;
                    colB *= ratio;
                }

                uint outR = (uint)(colR >= 1f ? 255 : (colR <= 0f ? 0 : (int)(colR * 255f)));
                uint outG = (uint)(colG >= 1f ? 255 : (colG <= 0f ? 0 : (int)(colG * 255f)));
                uint outB = (uint)(colB >= 1f ? 255 : (colB <= 0f ? 0 : (int)(colB * 255f)));
                dstRow[x] = 0xFF000000u | (outR << 16) | (outG << 8) | outB;
            }
        });

        _fieldB.NotifyPixelsChanged();
        (_fieldA, _fieldB) = (_fieldB, _fieldA);
        _fieldInit = true;

        var destRect = new SKRect(0, 0, mainW, mainH);
        using var blitPaint = new SKPaint
        {
            IsAntialias = false,
            FilterQuality = SKFilterQuality.Low
        };
        mainCanvas.DrawBitmap(_fieldA, destRect, blitPaint);
    }

    private static void AddBlackbodyThemeColor(
        float heat,
        float lowR, float lowG, float lowB,
        float midR, float midG, float midB,
        float hiR, float hiG, float hiB,
        float hotR, float hotG, float hotB,
        float intensity,
        ref float outR, ref float outG, ref float outB)
    {
        float r, g, b;
        if (heat < 0.38f)
        {
            float t = heat / 0.38f;
            r = lowR + (midR - lowR) * t;
            g = lowG + (midG - lowG) * t;
            b = lowB + (midB - lowB) * t;
        }
        else if (heat < 0.78f)
        {
            float t = (heat - 0.38f) / 0.40f;
            r = midR + (hiR - midR) * t;
            g = midG + (hiG - midG) * t;
            b = midB + (hiB - midB) * t;
        }
        else
        {
            float t = Math.Clamp((heat - 0.78f) / 0.22f, 0f, 1f);
            r = hiR + (hotR - hiR) * t;
            g = hiG + (hotG - hiG) * t;
            b = hiB + (hotB - hiB) * t;
        }

        outR += r * intensity;
        outG += g * intensity;
        outB += b * intensity;
    }

    private void UpdateAndDrawWarpStars(
        SKCanvas canvas,
        float cx,
        float cy,
        int width,
        int height,
        float scale,
        float rs,
        AudioAnalysisSnapshot snap,
        ThemePalette pal,
        float glowIntensity,
        int count)
    {
        float warpSpeed = _warpVelocity;
        float streakFactor = 1.0f + snap.BassEnergy * 2.2f + snap.KickPulse * 3.8f;

        using var starPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeCap = SKStrokeCap.Round,
            BlendMode = SKBlendMode.Plus
        };
        using var dotPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            BlendMode = SKBlendMode.Plus
        };

        for (int i = 0; i < count; i++)
        {
            ref var s = ref _stars[i];
            s.PrevZ = s.Z;
            s.Z -= warpSpeed * (0.65f + s.Brightness * 0.7f);

            if (s.Z <= 0.055f)
            {
                s = CreateStar(randomDepth: false);
                continue;
            }

            float projX = (s.X / s.Z) * scale;
            float projY = (s.Y / s.Z) * scale;

            // Subtle gravitational lensing deflection for stars passing close to the black hole!
            float distNorm = MathF.Sqrt(projX * projX + projY * projY) / scale;
            if (distNorm < rs * 0.96f)
                continue; // Occluded behind the black hole shadow!

            float lensPush = (rs * rs * 0.55f) / (distNorm * distNorm + 0.04f);
            float sx = cx + projX * (1f + lensPush);
            float sy = cy + projY * (1f + lensPush);

            if (sx < -40f || sx > width + 40f || sy < -40f || sy > height + 40f)
            {
                s = CreateStar(randomDepth: false);
                continue;
            }

            float prevZ = MathF.Min(1.05f, s.Z + warpSpeed * streakFactor);
            float prevProjX = (s.X / prevZ) * scale;
            float prevProjY = (s.Y / prevZ) * scale;
            float px = cx + prevProjX * (1f + lensPush);
            float py = cy + prevProjY * (1f + lensPush);

            float depthFade = SmoothStep(1.0f, 0.22f, s.Z);
            float alpha = Math.Clamp(depthFade * s.Brightness * (0.75f + snap.HighEnergy * 0.35f + glowIntensity * 0.25f), 0.08f, 0.95f);

            SKColor baseCol = s.TintPos > 0.55f
                ? VisualizerPalettes.BlendColor(pal.PeakCapColor, pal.HighColor, 0.38f)
                : VisualizerPalettes.BlendColor(pal.SampleSpectrum(s.TintPos), pal.PeakCapColor, 0.52f);

            float dx = sx - px;
            float dy = sy - py;
            float lenSq = dx * dx + dy * dy;

            if (lenSq > 3.5f)
            {
                starPaint.Color = baseCol.WithAlpha((byte)(alpha * 220));
                starPaint.StrokeWidth = s.Size * (0.55f + depthFade * 0.85f);
                canvas.DrawLine(px, py, sx, sy, starPaint);
            }
            else
            {
                dotPaint.Color = baseCol.WithAlpha((byte)(alpha * 235));
                canvas.DrawCircle(sx, sy, s.Size * (0.45f + depthFade * 0.65f), dotPaint);
            }
        }
    }

    private void DrawRelativisticPolarJets(
        SKCanvas canvas,
        float cx,
        float cy,
        float scale,
        float rs,
        float cosRoll,
        float sinRoll,
        AudioAnalysisSnapshot snap,
        ThemePalette pal,
        float glowIntensity)
    {
        float jetPower = Math.Clamp(0.26f + _jetSurge * 0.90f + snap.SnarePulse * 0.65f, 0.20f, 1.50f);
        float maxJetLen = scale * (0.92f + jetPower * 0.65f);
        // Start cleanly right outside the photon ring so jets never overlap the obsidian black hole shadow!
        float startY = rs * scale * 1.04f;

        // Polar axis unit vector in screen coordinates
        float axisX = sinRoll;
        float axisY = cosRoll;
        float perpX = cosRoll;
        float perpY = -sinRoll;

        // 1. Tapered Relativistic Astrophysical Plasma Cones (zero blunt pill caps!)
        for (int dir = -1; dir <= 1; dir += 2)
        {
            float sx = cx + axisX * (startY * dir);
            float sy = cy + axisY * (startY * dir);
            float ex = cx + axisX * ((startY + maxJetLen) * dir);
            float ey = cy + axisY * ((startY + maxJetLen) * dir);

            float outerStartW = 1.5f + snap.SnarePulse * 2.5f;
            float outerEndW = (24f + snap.SnarePulse * 22f + snap.StereoWidth * 18f) * (0.75f + glowIntensity * 0.4f);

            using var outerCone = new SKPath();
            outerCone.MoveTo(sx - perpX * outerStartW, sy - perpY * outerStartW);
            outerCone.LineTo(sx + perpX * outerStartW, sy + perpY * outerStartW);
            outerCone.LineTo(ex + perpX * outerEndW, ey + perpY * outerEndW);
            outerCone.LineTo(ex - perpX * outerEndW, ey - perpY * outerEndW);
            outerCone.Close();

            using var outerGrad = SKShader.CreateLinearGradient(
                new SKPoint(sx, sy),
                new SKPoint(ex, ey),
                new[]
                {
                    SKColors.Transparent,
                    pal.AccentGlow.WithAlpha((byte)Math.Clamp(64 * jetPower * (0.6f + glowIntensity * 0.45f), 14, 115)),
                    pal.MidColor.WithAlpha((byte)Math.Clamp(36 * jetPower, 8, 72)),
                    SKColors.Transparent
                },
                new[] { 0f, 0.14f, 0.55f, 1f },
                SKShaderTileMode.Clamp);

            using var outerConePaint = new SKPaint
            {
                IsAntialias = true,
                Style = SKPaintStyle.Fill,
                BlendMode = SKBlendMode.Plus,
                Shader = outerGrad
            };
            canvas.DrawPath(outerCone, outerConePaint);

            // Razor-narrow relativistic core needle flaring gently out from the magnetic pole
            float coreStartW = 0.8f;
            float coreEndW = 5.5f + snap.SnarePulse * 6.5f;
            using var coreCone = new SKPath();
            coreCone.MoveTo(sx - perpX * coreStartW, sy - perpY * coreStartW);
            coreCone.LineTo(sx + perpX * coreStartW, sy + perpY * coreStartW);
            coreCone.LineTo(ex + perpX * coreEndW, ey + perpY * coreEndW);
            coreCone.LineTo(ex - perpX * coreEndW, ey - perpY * coreEndW);
            coreCone.Close();

            using var coreGrad = SKShader.CreateLinearGradient(
                new SKPoint(sx, sy),
                new SKPoint(ex, ey),
                new[]
                {
                    SKColors.Transparent,
                    pal.PeakCapColor.WithAlpha((byte)Math.Clamp(135 * jetPower, 30, 210)),
                    pal.HighColor.WithAlpha((byte)Math.Clamp(70 * jetPower, 15, 130)),
                    SKColors.Transparent
                },
                new[] { 0f, 0.12f, 0.48f, 1f },
                SKShaderTileMode.Clamp);

            using var coreConePaint = new SKPaint
            {
                IsAntialias = true,
                Style = SKPaintStyle.Fill,
                BlendMode = SKBlendMode.Plus,
                Shader = coreGrad
            };
            canvas.DrawPath(coreCone, coreConePaint);

            // 2. Helical Stereo Waveform Magnetic Field Ribbons spiraling along the relativistic jet!
            int waveLen = snap.WaveformLeft.Length;
            if (waveLen >= 64)
            {
                const int steps = 72;
                using var helixL = new SKPath();
                using var helixR = new SKPath();

                for (int i = 0; i < steps; i++)
                {
                    float t = i / (float)(steps - 1);
                    float dist = startY + t * maxJetLen * 0.92f;
                    int wi = Math.Clamp((int)(t * (waveLen - 1)), 0, waveLen - 1);

                    float wl = Math.Clamp(snap.WaveformLeft[wi], -1f, 1f);
                    float wr = Math.Clamp(snap.WaveformRight[wi], -1f, 1f);

                    float coneWidth = scale * (0.018f + t * (0.095f + snap.StereoWidth * 0.085f));
                    float env = MathF.Sin(t * Pi) * (0.45f + 0.55f * (1f - t));
                    float spiralAng = t * 14f - _time * 9.5f;

                    float offL = (MathF.Sin(spiralAng) * 0.55f + wl * 0.95f) * coneWidth * env;
                    float offR = (MathF.Sin(spiralAng + Pi) * 0.55f + wr * 0.95f) * coneWidth * env;

                    float bx = cx + axisX * (dist * dir);
                    float by = cy + axisY * (dist * dir);

                    float lxPt = bx + perpX * offL;
                    float lyPt = by + perpY * offL;
                    float rxPt = bx + perpX * offR;
                    float ryPt = by + perpY * offR;

                    if (i == 0)
                    {
                        helixL.MoveTo(lxPt, lyPt);
                        helixR.MoveTo(rxPt, ryPt);
                    }
                    else
                    {
                        helixL.LineTo(lxPt, lyPt);
                        helixR.LineTo(rxPt, ryPt);
                    }
                }

                using var helixPaintL = new SKPaint
                {
                    IsAntialias = true,
                    Style = SKPaintStyle.Stroke,
                    StrokeWidth = 1.4f + snap.SnarePulse * 1.1f,
                    BlendMode = SKBlendMode.Plus,
                    Color = pal.HighColor.WithAlpha((byte)Math.Clamp(115 * jetPower, 55, 215))
                };
                using var helixPaintR = new SKPaint
                {
                    IsAntialias = true,
                    Style = SKPaintStyle.Stroke,
                    StrokeWidth = 1.25f + snap.SnarePulse * 0.9f,
                    BlendMode = SKBlendMode.Plus,
                    Color = pal.LowColor.WithAlpha((byte)Math.Clamp(105 * jetPower, 45, 200))
                };
                canvas.DrawPath(helixL, helixPaintL);
                canvas.DrawPath(helixR, helixPaintR);
            }
        }
    }

    private void UpdateAndDrawOrbitalEmbers(
        SKCanvas canvas,
        float cx,
        float cy,
        float scale,
        float rs,
        float cosRoll,
        float sinRoll,
        AudioAnalysisSnapshot snap,
        ThemePalette pal,
        float glowIntensity,
        int count)
    {
        float diskSquash = 4.65f - snap.StereoWidth * 0.55f;
        float invSquash = 1f / diskSquash;
        float speedBase = 0.012f + snap.BassEnergy * 0.022f + snap.MidEnergy * 0.014f;

        using var streakPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeCap = SKStrokeCap.Round,
            BlendMode = SKBlendMode.Plus
        };

        for (int i = 0; i < count; i++)
        {
            ref var e = ref _embers[i];

            // True Keplerian orbital velocity (inner embers whip around much faster than outer ones!)
            float omega = (speedBase * e.Speed) / MathF.Pow(e.Radius + 0.14f, 1.35f);
            float prevAng = e.Angle;
            e.Angle = (e.Angle + omega) % TwoPi;

            // Subtle inward accretion spiral + kick perturbation
            e.Radius -= 0.00055f + snap.BassEnergy * 0.0009f;
            if (e.Radius < rs * 1.06f)
            {
                e = CreateEmber();
                e.Radius = (float)(1.15 + _rng.NextDouble() * 0.42);
                continue;
            }

            // 3D tilted orbital position (sin(Angle) > 0 is FRONT half crossing in front of black hole!)
            float sinA = MathF.Sin(e.Angle);
            float cosA = MathF.Cos(e.Angle);

            float lx = cosA * e.Radius;
            float ly = sinA * e.Radius * invSquash + e.YOffset;

            // Back half of orbit occluded behind the event horizon shadow!
            if (sinA < 0f && (lx * lx + ly * ly) < (rs * rs * 1.02f))
                continue;

            float prevSin = MathF.Sin(prevAng - omega * 1.6f);
            float prevCos = MathF.Cos(prevAng - omega * 1.6f);
            float plx = prevCos * e.Radius;
            float ply = prevSin * e.Radius * invSquash + e.YOffset;

            // Rotate from local black hole space back to screen space (inverse roll)
            float sx = cx + (lx * cosRoll + ly * sinRoll) * scale;
            float sy = cy + (-lx * sinRoll + ly * cosRoll) * scale;
            float psx = cx + (plx * cosRoll + ply * sinRoll) * scale;
            float psy = cy + (-plx * sinRoll + ply * cosRoll) * scale;

            // Doppler brightening on approaching side (lx < 0) + radial heat gradient
            float doppler = Math.Clamp(1.05f - 0.48f * (lx / (e.Radius + 0.2f)), 0.45f, 1.55f);
            float heat = Math.Clamp(1.1f - (e.Radius - rs) * 0.82f, 0f, 1f);
            SKColor col = heat > 0.72f
                ? VisualizerPalettes.BlendColor(pal.HighColor, pal.PeakCapColor, (heat - 0.72f) / 0.28f)
                : pal.SampleSpectrum(1f - heat);

            float depthAlpha = sinA >= 0f ? 1.0f : 0.58f;
            byte alpha = (byte)Math.Clamp(e.Brightness * doppler * depthAlpha * (145f + glowIntensity * 110f), 28, 250);

            streakPaint.Color = col.WithAlpha(alpha);
            streakPaint.StrokeWidth = e.Size * (0.65f + heat * 0.65f);
            canvas.DrawLine(psx, psy, sx, sy, streakPaint);
        }
    }

    private void DrawGravitationalWaveRings(
        SKCanvas canvas,
        float cx,
        float cy,
        float scale,
        ThemePalette pal,
        float glowIntensity)
    {
        using var ringPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            BlendMode = SKBlendMode.Plus
        };

        for (int i = 0; i < MaxWaves; i++)
        {
            var w = _waves[i];
            if (w.Strength <= 0.03f) continue;

            float rad = w.Radius * scale;
            byte alpha = (byte)Math.Clamp(w.Strength * 145f * (0.6f + glowIntensity * 0.5f), 0, 180);
            ringPaint.Color = VisualizerPalettes.BlendColor(pal.AccentGlow, pal.PeakCapColor, 0.35f).WithAlpha(alpha);
            ringPaint.StrokeWidth = 1.2f + w.Strength * 2.2f;
            canvas.DrawCircle(cx, cy, rad, ringPaint);
        }
    }

    private void DrawPulsarGlints(
        SKCanvas canvas,
        float cx,
        float cy,
        int width,
        int height,
        ThemePalette pal,
        float glowIntensity)
    {
        using var glintPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeCap = SKStrokeCap.Round,
            BlendMode = SKBlendMode.Plus
        };
        using var corePaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            BlendMode = SKBlendMode.Plus
        };

        for (int i = 0; i < PulsarCount; i++)
        {
            ref var p = ref _pulsars[i];
            if (p.Alpha <= 0.02f) continue;

            p.Alpha *= 0.89f;

            float x = cx + p.Nx * width * 0.48f;
            float y = cy + p.Ny * height * 0.48f;
            float arm = p.Size * (0.7f + p.Alpha * 0.9f) * (0.7f + glowIntensity * 0.5f);

            SKColor col = VisualizerPalettes.BlendColor(pal.SampleSpectrum(p.Tint), pal.PeakCapColor, 0.65f);
            glintPaint.Color = col.WithAlpha((byte)(p.Alpha * 230));
            glintPaint.StrokeWidth = 1.15f;

            // Classic Hubble 4-point diffraction spike flare
            canvas.DrawLine(x - arm, y, x + arm, y, glintPaint);
            canvas.DrawLine(x, y - arm, x, y + arm, glintPaint);
            float diag = arm * 0.42f;
            glintPaint.Color = col.WithAlpha((byte)(p.Alpha * 135));
            canvas.DrawLine(x - diag, y - diag, x + diag, y + diag, glintPaint);
            canvas.DrawLine(x - diag, y + diag, x + diag, y - diag, glintPaint);

            corePaint.Color = pal.PeakCapColor.WithAlpha((byte)(p.Alpha * 245));
            canvas.DrawCircle(x, y, 1.8f + p.Alpha * 1.4f, corePaint);
        }
    }

    private static float SmoothStep(float edge0, float edge1, float x)
    {
        float t = Math.Clamp((x - edge0) / (edge1 - edge0), 0f, 1f);
        return t * t * (3f - 2f * t);
    }

    private void DisposeBuffers()
    {
        _fieldA?.Dispose();
        _fieldB?.Dispose();
        _fieldA = null;
        _fieldB = null;
    }

    public void Dispose()
    {
        DisposeBuffers();
    }
}
