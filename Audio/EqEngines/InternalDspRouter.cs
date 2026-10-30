using NAudio.CoreAudioApi;
using NAudio.Wave;
using SonicDeck.Audio.Dsp;

namespace SonicDeck.Audio.EqEngines;

public class AudioDeviceItem
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public bool IsLoopback { get; init; }
    public bool IsDefault { get; init; }

    public override string ToString() => Name;
}

/// <summary>
/// Self-contained C# real-time DSP audio router and procedural test synth.
/// Can route any Windows Capture / Virtual Cable / Loopback device through the
/// BiquadEqChain to any physical output device, or generate a live synth groove
/// to audition the internal C# DSP equalizer immediately.
/// </summary>
public class InternalDspRouter : IDisposable
{
    private readonly BiquadEqChain _eqChain;
    private IWaveIn? _capture;
    private WasapiOut? _waveOut;
    private BufferedWaveProvider? _bufferedProvider;

    private WasapiOut? _synthOut;
    private SynthGrooveProvider? _synthProvider;

    public InternalDspRouter(BiquadEqChain eqChain)
    {
        _eqChain = eqChain;
    }

    public bool IsRouting { get; private set; }
    public bool IsTestSynthPlaying { get; private set; }
    public string StatusText { get; private set; } = "Internal DSP Ready";

    public void StartRouting(MMDevice inputDevice, bool isLoopbackInput, MMDevice outputDevice, int latencyMs = 25)
    {
        StopRouting();

        try
        {
            if (isLoopbackInput && inputDevice.ID == outputDevice.ID)
            {
                StatusText = "Select different Input & Output devices to prevent feedback loop.";
                return;
            }

            _capture = isLoopbackInput
                ? new WasapiLoopbackCapture(inputDevice)
                : new WasapiCapture(inputDevice, true, latencyMs);

            WaveFormat fmt = _capture.WaveFormat;
            _bufferedProvider = new BufferedWaveProvider(fmt)
            {
                DiscardOnBufferOverflow = true,
                BufferDuration = TimeSpan.FromMilliseconds(Math.Max(120, latencyMs * 5))
            };

            var dspSampleProvider = new DspFilteredSampleProvider(_bufferedProvider.ToSampleProvider(), _eqChain);

            _capture.DataAvailable += (_, e) =>
            {
                if (e.BytesRecorded > 0)
                {
                    _bufferedProvider.AddSamples(e.Buffer, 0, e.BytesRecorded);
                }
            };

            _waveOut = new WasapiOut(outputDevice, AudioClientShareMode.Shared, true, latencyMs);
            _waveOut.Init(dspSampleProvider);
            _capture.StartRecording();
            _waveOut.Play();

            IsRouting = true;
            StatusText = $"Routing: {inputDevice.FriendlyName} -> DSP EQ -> {outputDevice.FriendlyName}";
        }
        catch (Exception ex)
        {
            StopRouting();
            StatusText = $"DSP Route Error: {ex.Message}";
        }
    }

    public void StopRouting()
    {
        try
        {
            _capture?.StopRecording();
            _capture?.Dispose();
        }
        catch { }
        _capture = null;

        try
        {
            _waveOut?.Stop();
            _waveOut?.Dispose();
        }
        catch { }
        _waveOut = null;
        _bufferedProvider = null;
        IsRouting = false;
        if (!IsTestSynthPlaying)
            StatusText = "Internal DSP Standby";
    }

    public void ToggleTestSynth(MMDevice? outputDevice = null)
    {
        if (IsTestSynthPlaying)
        {
            StopTestSynth();
        }
        else
        {
            StartTestSynth(outputDevice);
        }
    }

    public void StartTestSynth(MMDevice? outputDevice = null)
    {
        StopTestSynth();
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            MMDevice target = outputDevice ?? enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);

            _synthProvider = new SynthGrooveProvider(48000, _eqChain);
            _synthOut = new WasapiOut(target, AudioClientShareMode.Shared, true, 25);
            _synthOut.Init(_synthProvider);
            _synthOut.Play();

            IsTestSynthPlaying = true;
            StatusText = $"Internal C# DSP Synth Active -> {target.FriendlyName}";
        }
        catch (Exception ex)
        {
            StopTestSynth();
            StatusText = $"Synth Error: {ex.Message}";
        }
    }

    public void StopTestSynth()
    {
        try
        {
            _synthOut?.Stop();
            _synthOut?.Dispose();
        }
        catch { }
        _synthOut = null;
        _synthProvider = null;
        IsTestSynthPlaying = false;
        if (!IsRouting)
            StatusText = "Internal DSP Standby";
    }

    public void Dispose()
    {
        StopRouting();
        StopTestSynth();
    }

    private sealed class DspFilteredSampleProvider : ISampleProvider
    {
        private readonly ISampleProvider _source;
        private readonly BiquadEqChain _eq;

        public DspFilteredSampleProvider(ISampleProvider source, BiquadEqChain eq)
        {
            _source = source;
            _eq = eq;
            WaveFormat = source.WaveFormat;
        }

        public WaveFormat WaveFormat { get; }

        public int Read(float[] buffer, int offset, int count)
        {
            int read = _source.Read(buffer, offset, count);
            if (read > 0)
            {
                _eq.ProcessStereoBuffer(buffer.AsSpan(offset, read), WaveFormat.Channels);
            }
            return read;
        }
    }

    /// <summary>
    /// Procedural full-spectrum synthwave groove (Sub-Bass Kick + Arp Bass + Chords + Crisp Hi-Hats)
    /// processed directly through the C# BiquadEqChain so the user can audition the internal DSP EQ
    /// across 31Hz - 16kHz immediately without needing external audio routing.
    /// </summary>
    private sealed class SynthGrooveProvider : ISampleProvider
    {
        private readonly BiquadEqChain _eq;
        private readonly Random _rng = new(42);
        private long _sampleIndex;
        private double _kickPhase;
        private double _bassPhase;
        private double _leadPhase;
        private double _padPhase1, _padPhase2, _padPhase3;

        // Synthwave minor progression frequencies
        private static readonly double[] BassNotes =
        {
            55.0, 55.0, 110.0, 55.0,  65.41, 65.41, 130.81, 65.41,
            73.42, 73.42, 146.83, 73.42,  61.74, 61.74, 123.47, 82.41
        };

        private static readonly double[] ArpNotes =
        {
            220.0, 261.63, 329.63, 440.0,  261.63, 329.63, 392.0, 523.25,
            293.66, 349.23, 440.0, 587.33, 246.94, 329.63, 392.0, 493.88
        };

        public SynthGrooveProvider(int sampleRate, BiquadEqChain eq)
        {
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 2);
            _eq = eq;
        }

        public WaveFormat WaveFormat { get; }

        public int Read(float[] buffer, int offset, int count)
        {
            int sr = WaveFormat.SampleRate;
            double samplesPerSixteenth = sr * (60.0 / 118.0) / 4.0; // 118 BPM

            for (int i = 0; i < count; i += 2)
            {
                double stepPos = _sampleIndex / samplesPerSixteenth;
                int step = (int)stepPos % 16;
                double stepFrac = stepPos - Math.Floor(stepPos);

                // 1. Punchy 4-on-the-floor Kick (35Hz - 140Hz sweep)
                int beatStep = step % 4;
                double beatFrac = (stepPos % 4.0) / 4.0;
                double kick = 0.0;
                if (beatStep == 0 || (step == 14 && stepFrac < 0.5))
                {
                    double kEnv = Math.Exp(-beatFrac * 18.0);
                    double kFreq = 44.0 + 135.0 * Math.Exp(-beatFrac * 42.0);
                    _kickPhase += 2.0 * Math.PI * kFreq / sr;
                    kick = Math.Sin(_kickPhase) * kEnv * 0.55;
                }

                // 2. Crisp Snare & 16th Hi-Hat (2kHz - 16kHz air)
                double noise = _rng.NextDouble() * 2.0 - 1.0;
                double hatEnv = Math.Exp(-stepFrac * (step % 2 == 1 ? 14.0 : 32.0));
                double hat = noise * hatEnv * (step % 2 == 1 ? 0.14 : 0.07);

                double snare = 0.0;
                if (step == 4 || step == 12)
                {
                    double sEnv = Math.Exp(-stepFrac * 10.0);
                    snare = (noise * 0.8 + Math.Sin(_kickPhase * 2.3) * 0.3) * sEnv * 0.28;
                }

                // 3. Sawtooth Sub/Mid Bassline (55Hz - 400Hz)
                double bassFreq = BassNotes[step];
                _bassPhase = (_bassPhase + bassFreq / sr) % 1.0;
                double rawSaw = 2.0 * _bassPhase - 1.0;
                double bassEnv = Math.Exp(-stepFrac * 5.5);
                double bass = Math.Tanh(rawSaw * 1.8) * bassEnv * 0.26;

                // 4. Shimmering Stereo Arp + Pad (220Hz - 4kHz)
                double arpFreq = ArpNotes[step];
                _leadPhase += 2.0 * Math.PI * arpFreq / sr;
                double arpEnv = Math.Exp(-stepFrac * 8.0);
                double arpL = Math.Sin(_leadPhase) * arpEnv * 0.14;
                double arpR = Math.Sin(_leadPhase * 1.003 + 0.6) * arpEnv * 0.14;

                _padPhase1 += 2.0 * Math.PI * 220.0 / sr;
                _padPhase2 += 2.0 * Math.PI * 329.63 / sr;
                _padPhase3 += 2.0 * Math.PI * 659.25 / sr;
                double pad = (Math.Sin(_padPhase1) + Math.Sin(_padPhase2) * 0.7 + Math.Sin(_padPhase3) * 0.4) * 0.05;

                float left = (float)((kick + bass + snare + pad + arpL + hat * 0.85) * 0.65);
                float right = (float)((kick + bass + snare + pad + arpR + hat * 1.15) * 0.65);

                buffer[offset + i] = left;
                if (i + 1 < count)
                    buffer[offset + i + 1] = right;

                _sampleIndex++;
            }

            // Run directly through our C# 10-band BiQuad Equalizer Chain!
            _eq.ProcessStereoBuffer(buffer.AsSpan(offset, count), 2);
            return count;
        }
    }
}
