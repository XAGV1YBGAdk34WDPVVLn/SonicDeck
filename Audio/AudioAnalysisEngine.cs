using NAudio.CoreAudioApi;
using NAudio.Dsp;
using NAudio.Wave;
using SonicDeck.Audio.Dsp;

namespace SonicDeck.Audio;

public class AudioAnalysisSnapshot
{
    public const int WaterfallRows = 42;
    public const int BinCount = 128;

    public float[] RawSpectrum { get; init; } = new float[BinCount];
    public float[] PostEqSpectrum { get; init; } = new float[BinCount];
    public float[] SmoothedSpectrum { get; init; } = new float[BinCount];
    public float[] SideSpectrum { get; init; } = new float[BinCount];
    public float[] PeakHoldSpectrum { get; init; } = new float[BinCount];
    public double[] BinFrequencies { get; init; } = new double[BinCount];

    // Rolling 2D Waterfall Spectrogram History [row 0 = newest, row WaterfallRows-1 = oldest]
    public float[,] WaterfallHistory { get; init; } = new float[WaterfallRows, BinCount];

    public float[] WaveformLeft { get; init; } = new float[512];
    public float[] WaveformRight { get; init; } = new float[512];

    public float LeftRms { get; set; }
    public float RightRms { get; set; }
    public float LeftPeak { get; set; }
    public float RightPeak { get; set; }

    public float BassEnergy { get; set; }
    public float MidEnergy { get; set; }
    public float HighEnergy { get; set; }
    public float BeatPulse { get; set; }
    public bool IsBeatFrame { get; set; }

    // Dominant Pitch & Peak Frequency Callout
    public int DominantBinIndex { get; set; }
    public double DominantFrequencyHz { get; set; }
    public float DominantLevel { get; set; }
    public string DominantMusicalNote { get; set; } = "--";

    // 12-Semitone Chromagram (0=C, 1=C#, 2=D, ..., 11=B)
    public float[] Chroma { get; init; } = new float[12];
    public string DetectedChordLabel { get; set; } = "";

    // 3-Band Spectral Flux Onsets (Kick / Snare / Hi-Hat)
    public float KickPulse { get; set; }
    public bool IsKickFrame { get; set; }
    public float SnarePulse { get; set; }
    public bool IsSnareFrame { get; set; }
    public float HatPulse { get; set; }
    public bool IsHatFrame { get; set; }

    // Timbral & Spatial Descriptors
    public float SpectralCentroid { get; set; } // 0 = dark sub-heavy, 1 = bright crystalline
    public float StereoWidth { get; set; }      // 0 = pure mono, 1 = ultra-wide stereo
    public float HarmonicModeM { get; set; } = 3f;
    public float HarmonicModeN { get; set; } = 5f;
}

public class AudioAnalysisEngine : IDisposable
{
    private const int FftLength = 4096; // 2^12
    private const int FftM = 12;
    private const int NumBins = AudioAnalysisSnapshot.BinCount;
    private const int WaveLength = 512;

    private static readonly string[] NoteNames =
        { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };

    private readonly BiquadEqChain _eqChain;
    private readonly Complex[] _fftMidBuffer = new Complex[FftLength];
    private readonly Complex[] _fftSideBuffer = new Complex[FftLength];
    private readonly float[] _ringLeft = new float[FftLength];
    private readonly float[] _ringRight = new float[FftLength];
    private readonly float[] _window = new float[FftLength];
    private readonly double[] _binFreqs = new double[NumBins];
    private readonly object _dataLock = new();

    private WasapiLoopbackCapture? _loopback;
    private int _ringWritePos;
    private int _sampleRate = 48000;

    // Live computed buffers
    private readonly float[] _latestRawBins = new float[NumBins];
    private readonly float[] _latestPostEqBins = new float[NumBins];
    private readonly float[] _smoothedBins = new float[NumBins];
    private readonly float[] _smoothedSideBins = new float[NumBins];
    private readonly float[] _peakBins = new float[NumBins];
    private readonly float[] _waveL = new float[WaveLength];
    private readonly float[] _waveR = new float[WaveLength];

    private float _leftRms, _rightRms, _leftPeak, _rightPeak;
    private float _bassEnergy, _midEnergy, _highEnergy;
    private float _rollingBassAvg = 0.05f;
    private float _beatPulse;
    private DateTime _lastBeatTime = DateTime.MinValue;
    private double _smoothedDomFreq = 100.0;

    // Extended musical & transient analysis state
    private readonly float[] _prevBins = new float[NumBins];
    private readonly float[] _smoothedChroma = new float[12];
    private readonly float[] _rawChroma = new float[12];
    private float _rollingKickFlux = 0.02f;
    private float _rollingSnareFlux = 0.02f;
    private float _rollingHatFlux = 0.02f;
    private float _kickPulse, _snarePulse, _hatPulse;
    private DateTime _lastKickTime = DateTime.MinValue;
    private DateTime _lastSnareTime = DateTime.MinValue;
    private DateTime _lastHatTime = DateTime.MinValue;
    private float _smoothedCentroid = 0.35f;
    private float _smoothedStereoWidth = 0.25f;
    private float _harmonicModeM = 3.0f;
    private float _harmonicModeN = 5.0f;

    public AudioAnalysisEngine(BiquadEqChain eqChain)
    {
        _eqChain = eqChain;

        for (int i = 0; i < FftLength; i++)
        {
            _window[i] = (float)(0.5 * (1.0 - Math.Cos(2.0 * Math.PI * i / (FftLength - 1))));
        }

        double minLog = Math.Log10(22.0);
        double maxLog = Math.Log10(19500.0);
        for (int b = 0; b < NumBins; b++)
        {
            double t = (double)b / (NumBins - 1);
            _binFreqs[b] = Math.Pow(10.0, minLog + t * (maxLog - minLog));
        }
    }

    public int SampleRate => _sampleRate;
    public double[] BinFrequencies => _binFreqs;
    public bool ApplyEqToVisuals { get; set; } = true;

    public static string FrequencyToMusicalNote(double freqHz)
    {
        if (freqHz < 16.0 || double.IsNaN(freqHz) || double.IsInfinity(freqHz))
            return "--";

        double midi = 69.0 + 12.0 * Math.Log2(freqHz / 440.0);
        int midiRounded = (int)Math.Round(midi);
        if (midiRounded < 12 || midiRounded > 127)
            return "--";

        int noteIdx = ((midiRounded % 12) + 12) % 12;
        int octave = (midiRounded / 12) - 1;
        return $"{NoteNames[noteIdx]}{octave}";
    }

    public void StartLoopback(MMDevice? device = null)
    {
        StopLoopback();

        try
        {
            _loopback = device != null
                ? new WasapiLoopbackCapture(device)
                : new WasapiLoopbackCapture();

            _sampleRate = _loopback.WaveFormat.SampleRate;
            _loopback.DataAvailable += OnDataAvailable;
            _loopback.StartRecording();
        }
        catch { }
    }

    public void StopLoopback()
    {
        if (_loopback != null)
        {
            try
            {
                _loopback.DataAvailable -= OnDataAvailable;
                _loopback.StopRecording();
                _loopback.Dispose();
            }
            catch { }
            _loopback = null;
        }
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (e.BytesRecorded <= 0 || _loopback == null)
            return;

        WaveFormat fmt = _loopback.WaveFormat;
        int channels = Math.Max(1, fmt.Channels);
        int bytesPerSample = fmt.BitsPerSample / 8;
        if (bytesPerSample <= 0) return;

        int frameCount = e.BytesRecorded / (bytesPerSample * channels);
        if (frameCount <= 0) return;

        float sumSqL = 0f, sumSqR = 0f;
        float maxL = 0f, maxR = 0f;

        lock (_dataLock)
        {
            for (int f = 0; f < frameCount; f++)
            {
                int byteIndex = f * channels * bytesPerSample;
                float l = ReadSample(e.Buffer, byteIndex, fmt);
                float r = channels > 1 ? ReadSample(e.Buffer, byteIndex + bytesPerSample, fmt) : l;

                _ringLeft[_ringWritePos] = l;
                _ringRight[_ringWritePos] = r;
                _ringWritePos = (_ringWritePos + 1) & (FftLength - 1);

                sumSqL += l * l;
                sumSqR += r * r;
                float absL = Math.Abs(l);
                float absR = Math.Abs(r);
                if (absL > maxL) maxL = absL;
                if (absR > maxR) maxR = absR;
            }

            _leftRms = (float)Math.Sqrt(sumSqL / frameCount);
            _rightRms = (float)Math.Sqrt(sumSqR / frameCount);
            _leftPeak = Math.Max(maxL, _leftPeak * 0.88f);
            _rightPeak = Math.Max(maxR, _rightPeak * 0.88f);
        }
    }

    private static float ReadSample(byte[] buffer, int offset, WaveFormat fmt)
    {
        if (fmt.Encoding == WaveFormatEncoding.IeeeFloat || fmt.BitsPerSample == 32)
            return BitConverter.ToSingle(buffer, offset);
        if (fmt.BitsPerSample == 16)
            return BitConverter.ToInt16(buffer, offset) / 32768f;
        return 0f;
    }

    public void ComputeFrame(
        AudioAnalysisSnapshot target,
        float smoothing,
        float gravity,
        float gain,
        float tiltDbPerOctave,
        float beatSensitivity)
    {
        lock (_dataLock)
        {
            int pos = _ringWritePos;
            for (int i = 0; i < FftLength; i++)
            {
                int idx = (pos + i) & (FftLength - 1);
                float l = _ringLeft[idx];
                float r = _ringRight[idx];
                float mid = 0.5f * (l + r);
                float side = 0.5f * (l - r);

                _fftMidBuffer[i].X = mid * _window[i];
                _fftMidBuffer[i].Y = 0f;
                _fftSideBuffer[i].X = side * _window[i];
                _fftSideBuffer[i].Y = 0f;
            }

            for (int i = 0; i < WaveLength; i++)
            {
                int idx = (pos + FftLength - WaveLength + i) & (FftLength - 1);
                _waveL[i] = _ringLeft[idx];
                _waveR[i] = _ringRight[idx];
            }

            _leftRms *= 0.92f;
            _rightRms *= 0.92f;
            _leftPeak *= 0.92f;
            _rightPeak *= 0.92f;
        }

        FastFourierTransform.FFT(true, FftM, _fftMidBuffer);
        FastFourierTransform.FFT(true, FftM, _fftSideBuffer);

        int halfFft = FftLength / 2;
        double binHzStep = (double)_sampleRate / FftLength;

        float bassSum = 0f, midSum = 0f, highSum = 0f;
        int bassCount = 0, midCount = 0, highCount = 0;

        float kickFluxSum = 0f, snareFluxSum = 0f, hatFluxSum = 0f;
        int kickFluxCount = 0, snareFluxCount = 0, hatFluxCount = 0;
        float centroidNum = 0f, centroidDen = 0f;
        float midTotalSum = 0f, sideTotalSum = 0f;

        Array.Clear(_rawChroma, 0, 12);

        int domBin = 0;
        float domLevel = 0f;

        for (int b = 0; b < NumBins; b++)
        {
            double fCenter = _binFreqs[b];
            double fLow = b == 0 ? 18.0 : Math.Sqrt(_binFreqs[b - 1] * fCenter);
            double fHigh = b == NumBins - 1 ? 20500.0 : Math.Sqrt(fCenter * _binFreqs[b + 1]);

            int idxLow = Math.Clamp((int)Math.Floor(fLow / binHzStep), 1, halfFft - 1);
            int idxHigh = Math.Clamp((int)Math.Ceiling(fHigh / binHzStep), idxLow, halfFft - 1);

            float maxMidMag = 0f;
            float maxSideMag = 0f;
            for (int k = idxLow; k <= idxHigh; k++)
            {
                float reM = _fftMidBuffer[k].X;
                float imM = _fftMidBuffer[k].Y;
                float magM = (float)Math.Sqrt(reM * reM + imM * imM);
                if (magM > maxMidMag) maxMidMag = magM;

                float reS = _fftSideBuffer[k].X;
                float imS = _fftSideBuffer[k].Y;
                float magS = (float)Math.Sqrt(reS * reS + imS * imS);
                if (magS > maxSideMag) maxSideMag = magS;
            }

            double octavesFrom100Hz = Math.Log2(Math.Max(20.0, fCenter) / 100.0);
            double tiltLinear = Math.Pow(10.0, (octavesFrom100Hz * tiltDbPerOctave) / 20.0);

            float scaledRaw = maxMidMag * (float)tiltLinear * gain * 18.0f;
            float dbRaw = 20f * (float)Math.Log10(Math.Max(1e-5f, scaledRaw));
            float normRaw = Math.Clamp((dbRaw + 52f) / 52f, 0f, 1.25f);

            double eqLinear = _eqChain.EvaluateFilterOnlyLinear(fCenter);
            float scaledPost = scaledRaw * (float)eqLinear;
            float dbPost = 20f * (float)Math.Log10(Math.Max(1e-5f, scaledPost));
            float normPost = Math.Clamp((dbPost + 52f) / 52f, 0f, 1.25f);

            float scaledSide = maxSideMag * (float)tiltLinear * (float)eqLinear * gain * 22.0f;
            float dbSide = 20f * (float)Math.Log10(Math.Max(1e-5f, scaledSide));
            float normSide = Math.Clamp((dbSide + 52f) / 52f, 0f, 1.20f);

            _latestRawBins[b] = normRaw;
            _latestPostEqBins[b] = normPost;

            float activeVal = ApplyEqToVisuals ? normPost : normRaw;

            // Half-wave rectified spectral flux for 3-band transient onset detection
            float diff = activeVal - _prevBins[b];
            _prevBins[b] = activeVal;
            if (diff > 0f)
            {
                if (fCenter <= 135.0) { kickFluxSum += diff; kickFluxCount++; }
                else if (fCenter >= 220.0 && fCenter <= 3200.0) { snareFluxSum += diff; snareFluxCount++; }
                else if (fCenter >= 5800.0) { hatFluxSum += diff; hatFluxCount++; }
            }

            float prev = _smoothedBins[b];
            float alpha = activeVal > prev
                ? Math.Clamp(1.0f - smoothing * 0.65f, 0.12f, 0.95f)
                : Math.Clamp((1.0f - smoothing) * 0.45f, 0.04f, 0.60f);
            float smoothed = prev + (activeVal - prev) * alpha;
            _smoothedBins[b] = smoothed;

            float prevSide = _smoothedSideBins[b];
            float smoothedSide = prevSide + (normSide - prevSide) * alpha;
            _smoothedSideBins[b] = smoothedSide;

            if (smoothed >= _peakBins[b])
                _peakBins[b] = smoothed;
            else
                _peakBins[b] = Math.Max(0f, _peakBins[b] - gravity * 0.018f);

            if (smoothed > domLevel)
            {
                domLevel = smoothed;
                domBin = b;
            }

            if (fCenter <= 150.0) { bassSum += smoothed; bassCount++; }
            else if (fCenter <= 2800.0) { midSum += smoothed; midCount++; }
            else { highSum += smoothed; highCount++; }

            // Spectral centroid & stereo width accumulators
            float bNorm = (float)b / (NumBins - 1);
            centroidNum += bNorm * smoothed * smoothed;
            centroidDen += smoothed * smoothed;
            midTotalSum += smoothed;
            sideTotalSum += smoothedSide;

            // 12-semitone Chromagram projection (55 Hz A1 .. 4200 Hz C8)
            if (fCenter >= 55.0 && fCenter <= 4200.0 && smoothed > 0.04f)
            {
                double midi = 69.0 + 12.0 * Math.Log2(fCenter / 440.0);
                double pitchPos = ((midi % 12.0) + 12.0) % 12.0;
                int p0 = ((int)Math.Floor(pitchPos)) % 12;
                int p1 = (p0 + 1) % 12;
                float frac = (float)(pitchPos - Math.Floor(pitchPos));
                // Emphasize harmonic core (110 Hz - 1800 Hz) slightly over extreme sub/air
                float octaveWeight = fCenter < 110.0 ? 0.65f : (fCenter > 2200.0 ? 0.55f : 1.0f);
                float energy = smoothed * smoothed * octaveWeight;
                _rawChroma[p0] += energy * (1f - frac);
                _rawChroma[p1] += energy * frac;
            }
        }

        _bassEnergy = bassCount > 0 ? bassSum / bassCount : 0f;
        _midEnergy = midCount > 0 ? midSum / midCount : 0f;
        _highEnergy = highCount > 0 ? highSum / highCount : 0f;

        // Normalize & smooth 12-semitone Chromagram
        float maxChroma = 0.001f;
        for (int c = 0; c < 12; c++)
            if (_rawChroma[c] > maxChroma) maxChroma = _rawChroma[c];

        float chromaScale = maxChroma > 0.08f ? (1f / maxChroma) : (1f / 0.08f);
        for (int c = 0; c < 12; c++)
        {
            float targetC = Math.Clamp(_rawChroma[c] * chromaScale * Math.Clamp(domLevel * 1.15f, 0f, 1.1f), 0f, 1f);
            float prevC = _smoothedChroma[c];
            float cAlpha = targetC > prevC ? 0.34f : 0.14f;
            _smoothedChroma[c] = prevC + (targetC - prevC) * cAlpha;
        }

        // Smooth Spectral Centroid & Stereo Width
        float rawCentroid = centroidDen > 1e-4f ? Math.Clamp(centroidNum / centroidDen, 0f, 1f) : 0.32f;
        _smoothedCentroid += (rawCentroid - _smoothedCentroid) * 0.14f;

        float rawStereo = (midTotalSum + sideTotalSum) > 0.05f
            ? Math.Clamp((sideTotalSum / Math.Max(0.05f, midTotalSum + sideTotalSum)) * 1.85f, 0f, 1f)
            : 0.15f;
        _smoothedStereoWidth += (rawStereo - _smoothedStereoWidth) * 0.16f;

        // 3-Band Spectral Flux Onset Detection (Kick / Snare / Hi-Hat)
        DateTime nowUtc = DateTime.UtcNow;
        float kickFlux = kickFluxCount > 0 ? kickFluxSum / kickFluxCount : 0f;
        float snareFlux = snareFluxCount > 0 ? snareFluxSum / snareFluxCount : 0f;
        float hatFlux = hatFluxCount > 0 ? hatFluxSum / hatFluxCount : 0f;

        _rollingKickFlux = _rollingKickFlux * 0.90f + kickFlux * 0.10f;
        _rollingSnareFlux = _rollingSnareFlux * 0.90f + snareFlux * 0.10f;
        _rollingHatFlux = _rollingHatFlux * 0.88f + hatFlux * 0.12f;

        float sensFactor = Math.Clamp(1.80f - beatSensitivity * 0.48f, 0.85f, 1.70f);
        bool isKick = false;
        if (kickFlux > Math.Max(0.032f, _rollingKickFlux * sensFactor) &&
            _bassEnergy > 0.14f &&
            (nowUtc - _lastKickTime).TotalMilliseconds > 155)
        {
            isKick = true;
            _lastKickTime = nowUtc;
            _kickPulse = Math.Clamp(0.45f + kickFlux * 4.2f * beatSensitivity, 0.45f, 1.35f);
        }
        else
        {
            _kickPulse *= 0.86f;
        }

        bool isSnare = false;
        if (snareFlux > Math.Max(0.028f, _rollingSnareFlux * (sensFactor + 0.12f)) &&
            _midEnergy > 0.10f &&
            (nowUtc - _lastSnareTime).TotalMilliseconds > 130)
        {
            isSnare = true;
            _lastSnareTime = nowUtc;
            _snarePulse = Math.Clamp(0.40f + snareFlux * 5.0f * beatSensitivity, 0.40f, 1.30f);
        }
        else
        {
            _snarePulse *= 0.82f;
        }

        bool isHat = false;
        if (hatFlux > Math.Max(0.022f, _rollingHatFlux * (sensFactor + 0.08f)) &&
            _highEnergy > 0.06f &&
            (nowUtc - _lastHatTime).TotalMilliseconds > 85)
        {
            isHat = true;
            _lastHatTime = nowUtc;
            _hatPulse = Math.Clamp(0.35f + hatFlux * 5.5f * beatSensitivity, 0.35f, 1.25f);
        }
        else
        {
            _hatPulse *= 0.78f;
        }

        // Beat detection on Sub-Bass / Kick energy
        _rollingBassAvg = _rollingBassAvg * 0.94f + _bassEnergy * 0.06f;
        float threshold = Math.Max(0.14f, _rollingBassAvg * (1.75f - beatSensitivity * 0.45f));
        bool isBeat = false;
        if (_bassEnergy > threshold && (nowUtc - _lastBeatTime).TotalMilliseconds > 170)
        {
            isBeat = true;
            _lastBeatTime = nowUtc;
            _beatPulse = Math.Clamp((_bassEnergy - _rollingBassAvg) * 2.4f * beatSensitivity, 0.35f, 1.3f);
            if (!isKick)
            {
                isKick = true;
                _lastKickTime = nowUtc;
                _kickPulse = Math.Max(_kickPulse, _beatPulse);
            }
        }
        else
        {
            _beatPulse *= 0.86f;
        }

        // Derive smooth Chladni standing-wave harmonic modes (m, n) from top 2 active pitch classes
        int topNote1 = 0, topNote2 = 7;
        float topVal1 = 0f, topVal2 = 0f;
        for (int c = 0; c < 12; c++)
        {
            float v = _smoothedChroma[c];
            if (v > topVal1)
            {
                topVal2 = topVal1; topNote2 = topNote1;
                topVal1 = v; topNote1 = c;
            }
            else if (v > topVal2)
            {
                topVal2 = v; topNote2 = c;
            }
        }

        // Map top pitch classes + spectral brightness into rich, non-degenerate Chladni mode numbers
        float targetM = 2.0f + (topNote1 % 5) + _smoothedCentroid * 2.2f;
        int interval = Math.Abs(topNote2 - topNote1);
        if (interval > 6) interval = 12 - interval;
        float targetN = 3.0f + ((topNote2 + interval) % 6) + _highEnergy * 1.8f;
        if (Math.Abs(targetM - targetN) < 0.85f)
            targetN += 1.6f;

        _harmonicModeM += (targetM - _harmonicModeM) * 0.045f;
        _harmonicModeN += (targetN - _harmonicModeN) * 0.045f;

        // Lightweight musical chord label from top chroma triad
        string chordLabel = "";
        if (topVal1 > 0.35f)
        {
            int root = topNote1;
            float maj3 = _smoothedChroma[(root + 4) % 12];
            float min3 = _smoothedChroma[(root + 3) % 12];
            float fifth = _smoothedChroma[(root + 7) % 12];
            if (maj3 > 0.36f && fifth > 0.32f && maj3 >= min3)
                chordLabel = $"{NoteNames[root]} maj";
            else if (min3 > 0.36f && fifth > 0.32f)
                chordLabel = $"{NoteNames[root]} min";
            else if (fifth > 0.42f)
                chordLabel = $"{NoteNames[root]}5";
            else
                chordLabel = NoteNames[root];
        }

        // Scroll Waterfall Spectrogram History down 1 row and insert newest spectrum at row 0
        int rows = AudioAnalysisSnapshot.WaterfallRows;
        for (int r = rows - 1; r > 0; r--)
        {
            for (int b = 0; b < NumBins; b++)
            {
                target.WaterfallHistory[r, b] = target.WaterfallHistory[r - 1, b];
            }
        }
        for (int b = 0; b < NumBins; b++)
        {
            target.WaterfallHistory[0, b] = _smoothedBins[b];
        }

        // Dominant frequency & musical note tracking
        if (domLevel > 0.08f)
        {
            double rawDomHz = _binFreqs[domBin];
            _smoothedDomFreq = _smoothedDomFreq * 0.72 + rawDomHz * 0.28;
            target.DominantBinIndex = domBin;
            target.DominantFrequencyHz = rawDomHz;
            target.DominantLevel = domLevel;
            target.DominantMusicalNote = FrequencyToMusicalNote(rawDomHz);
        }
        else
        {
            target.DominantLevel = 0f;
            target.DominantMusicalNote = "--";
        }

        Array.Copy(_latestRawBins, target.RawSpectrum, NumBins);
        Array.Copy(_latestPostEqBins, target.PostEqSpectrum, NumBins);
        Array.Copy(_smoothedBins, target.SmoothedSpectrum, NumBins);
        Array.Copy(_smoothedSideBins, target.SideSpectrum, NumBins);
        Array.Copy(_peakBins, target.PeakHoldSpectrum, NumBins);
        Array.Copy(_binFreqs, target.BinFrequencies, NumBins);
        Array.Copy(_waveL, target.WaveformLeft, WaveLength);
        Array.Copy(_waveR, target.WaveformRight, WaveLength);
        Array.Copy(_smoothedChroma, target.Chroma, 12);

        target.LeftRms = _leftRms;
        target.RightRms = _rightRms;
        target.LeftPeak = _leftPeak;
        target.RightPeak = _rightPeak;
        target.BassEnergy = _bassEnergy;
        target.MidEnergy = _midEnergy;
        target.HighEnergy = _highEnergy;
        target.BeatPulse = _beatPulse;
        target.IsBeatFrame = isBeat;

        target.DetectedChordLabel = chordLabel;
        target.KickPulse = _kickPulse;
        target.IsKickFrame = isKick;
        target.SnarePulse = _snarePulse;
        target.IsSnareFrame = isSnare;
        target.HatPulse = _hatPulse;
        target.IsHatFrame = isHat;
        target.SpectralCentroid = _smoothedCentroid;
        target.StereoWidth = _smoothedStereoWidth;
        target.HarmonicModeM = _harmonicModeM;
        target.HarmonicModeN = _harmonicModeN;
    }

    public void Dispose()
    {
        StopLoopback();
    }
}
