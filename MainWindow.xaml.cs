using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using NAudio.CoreAudioApi;
using SkiaSharp;
using SkiaSharp.Views.Desktop;
using SonicDeck.Audio;
using SonicDeck.Audio.Dsp;
using SonicDeck.Audio.EqEngines;
using SonicDeck.Models;
using SonicDeck.Visuals;

namespace SonicDeck;

public partial class MainWindow : Window
{
    private readonly BiquadEqChain _eqChain = new();
    private readonly EqualizerApoService _apoService = new();
    private readonly InternalDspRouter _dspRouter;
    private readonly AudioAnalysisEngine _analysisEngine;
    private readonly SystemAudioMixerService _mixerService = new();
    private readonly SkiaVisualizerRenderer _renderer = new();
    private readonly AudioAnalysisSnapshot _snapshot = new();

    private readonly ObservableCollection<EqBand> _bands = new();
    private readonly ObservableCollection<AppAudioSessionViewModel> _appSessions = new();
    private readonly DispatcherTimer _renderTimer;
    private readonly DispatcherTimer _sessionPollTimer;

    private EqEngineMode _engineMode = EqEngineMode.Both;
    private bool _isInitialized;
    private bool _isUpdatingUi;
    private int _draggingBandIndex = -1;
    private VisualizerPopoutWindow? _popoutWindow;
    private DateTime _lastCanvasClickTime = DateTime.MinValue;

    public MainWindow()
    {
        _dspRouter = new InternalDspRouter(_eqChain);
        _analysisEngine = new AudioAnalysisEngine(_eqChain);

        _isUpdatingUi = true;
        InitializeComponent();
        _isInitialized = true;
        _isUpdatingUi = false;

        InitializeEqBands();
        InitializePresetsAndThemes();
        PopulateAudioDevices();
        RefreshAppSessions();

        _analysisEngine.StartLoopback();
        SyncEqEngines(forceApoWrite: true);

        _renderTimer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(16)
        };
        _renderTimer.Tick += OnRenderTick;
        _renderTimer.Start();

        _sessionPollTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(120)
        };
        _sessionPollTimer.Tick += OnSessionPollTick;
        _sessionPollTimer.Start();

        Closed += (_, _) =>
        {
            _renderTimer.Stop();
            _sessionPollTimer.Stop();
            try { _popoutWindow?.Close(); } catch { }
            _dspRouter.Dispose();
            _analysisEngine.Dispose();
            _mixerService.Dispose();
        };
    }

    private void InitializeEqBands()
    {
        double[] freqs = { 31, 62, 125, 250, 500, 1000, 2000, 4000, 8000, 16000 };

        for (int i = 0; i < freqs.Length; i++)
        {
            var band = new EqBand
            {
                Index = i,
                DefaultFrequency = freqs[i],
                Frequency = freqs[i],
                FilterType = i == 0
                    ? BiquadFilterType.LowShelf
                    : i == freqs.Length - 1
                        ? BiquadFilterType.HighShelf
                        : BiquadFilterType.Peaking,
                GainDb = 0.0,
                QFactor = 1.41
            };
            band.Changed += (_, _) =>
            {
                if (!_isUpdatingUi)
                    SyncEqEngines();
            };
            _bands.Add(band);
        }

        ItemsEqBands.ItemsSource = _bands;
    }

    private void InitializePresetsAndThemes()
    {
        _isUpdatingUi = true;

        CmbVisualMode.ItemsSource = VisualizerPalettes.ModeItems;
        CmbVisualMode.DisplayMemberPath = nameof(VisualizerModeItem.DisplayName);
        CmbVisualMode.SelectedIndex = 0;

        CmbRtaStyle.ItemsSource = VisualizerPalettes.RtaStyleItems;
        CmbRtaStyle.DisplayMemberPath = nameof(RtaStyleItem.DisplayName);
        CmbRtaStyle.SelectedIndex = 0;

        CmbPresets.ItemsSource = EqPreset.BuiltInPresets;
        CmbPresets.DisplayMemberPath = nameof(EqPreset.Name);
        CmbPresets.SelectedIndex = 1; // Default to "Bass Boost + Punch"

        CmbTheme.ItemsSource = VisualizerPalettes.ThemeItems;
        CmbTheme.SelectedIndex = 0;

        _isUpdatingUi = false;

        if (CmbPresets.SelectedItem is EqPreset initialPreset)
        {
            ApplyPreset(initialPreset);
        }
    }

    private void PopulateAudioDevices()
    {
        _isUpdatingUi = true;

        var outputs = _mixerService.GetOutputDevices();
        CmbOutputDevices.ItemsSource = outputs;
        CmbDspOutput.ItemsSource = outputs;

        var defaultOut = outputs.FirstOrDefault(d => d.IsDefault) ?? outputs.FirstOrDefault();
        CmbOutputDevices.SelectedItem = defaultOut;
        CmbDspOutput.SelectedItem = defaultOut;

        var inputs = _mixerService.GetInputAndLoopbackDevices();
        CmbDspInput.ItemsSource = inputs;
        CmbDspInput.SelectedItem = inputs.FirstOrDefault();

        float masterVol = _mixerService.GetMasterVolumePercent();
        SldMasterVolume.Value = masterVol;
        TxtMasterVolume.Text = $"{masterVol:0}%";
        UpdateMuteButtonVisual(_mixerService.GetMasterMute());

        _isUpdatingUi = false;
    }

    private void RefreshAppSessions()
    {
        var sessions = _mixerService.GetActiveAppSessions(out string nowPlaying);
        _appSessions.Clear();
        foreach (var s in sessions)
        {
            _appSessions.Add(s);
        }
        ItemsAppSessions.ItemsSource = _appSessions;

        if (!string.IsNullOrWhiteSpace(nowPlaying))
        {
            TxtNowPlaying.Text = $"Now Playing: {nowPlaying}";
        }
        else if (_dspRouter.IsTestSynthPlaying)
        {
            TxtNowPlaying.Text = "Now Playing: Built-in C# DSP Synthwave Groove (118 BPM)";
        }
        else
        {
            TxtNowPlaying.Text = $"Capturing WASAPI Loopback: {_mixerService.ActiveDeviceName}";
        }
    }

    private void SyncEqEngines(bool forceApoWrite = false)
    {
        if (!_isInitialized || _bands.Count == 0 || SldPreamp == null)
            return;

        bool enabled = ChkEqEnabled.IsChecked == true;
        double preamp = SldPreamp.Value;
        double bassBoost = SldBassBoost.Value;
        double trebleTilt = SldTrebleTilt.Value;
        double globalQ = SldGlobalQ.Value;
        double balance = SldBalance.Value;
        bool softLimiter = ChkSoftLimiter.IsChecked == true;

        bool useInternal = _engineMode is EqEngineMode.Both or EqEngineMode.InternalDsp;
        _eqChain.UpdateParameters(
            _analysisEngine.SampleRate,
            enabled && useInternal,
            preamp,
            bassBoost,
            trebleTilt,
            globalQ,
            balance,
            softLimiter,
            _bands);

        if (_engineMode is EqEngineMode.Both or EqEngineMode.EqualizerApo)
        {
            _apoService.SyncToApo(enabled, preamp, bassBoost, trebleTilt, globalQ, balance, _bands, forceApoWrite);
        }

        UpdateFooterStatus();
    }

    private void UpdateFooterStatus()
    {
        if (TxtStatusLeft == null) return;

        string modeName = _engineMode switch
        {
            EqEngineMode.Both => "Dual Engine (Equalizer APO + C# BiQuad DSP)",
            EqEngineMode.EqualizerApo => "Equalizer APO 0ms Mode",
            _ => "Internal C# BiQuad DSP Mode"
        };

        TxtStatusLeft.Text = $"{modeName}  |  {_apoService.StatusMessage}  |  {_dspRouter.StatusText}";
    }

    // ======================= 60 FPS RENDER & METER LOOPS =======================

    private void OnRenderTick(object? sender, EventArgs e)
    {
        float smoothing = (float)SldVisSmoothing.Value;
        float gravity = (float)SldVisGravity.Value;
        float gain = (float)SldVisGain.Value;
        float tilt = (float)SldVisTilt.Value;
        float beatSens = (float)SldVisBeat.Value;

        _analysisEngine.ComputeFrame(_snapshot, smoothing, gravity, gain, tilt, beatSens);

        if (_popoutWindow != null)
        {
            _popoutWindow.InvalidateVisualizer();
        }
        else
        {
            SkiaCanvas.InvalidateVisual();
        }
    }

    private void OnSessionPollTick(object? sender, EventArgs e)
    {
        foreach (var s in _appSessions)
        {
            s.RefreshLiveMetrics();
        }
    }

    private void SkiaCanvas_PaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        _renderer.Render(
            e.Surface.Canvas,
            e.Info.Width,
            e.Info.Height,
            _snapshot,
            _eqChain,
            _bands);
    }

    // ======================= INTERACTIVE CANVAS 2D DRAGGING, WHEEL Q, & SOLO =======================

    private SKRect GetCanvasPlotRect()
    {
        var dpi = VisualTreeHelper.GetDpi(SkiaCanvas);
        float w = (float)(SkiaCanvas.ActualWidth * dpi.DpiScaleX);
        float h = (float)(SkiaCanvas.ActualHeight * dpi.DpiScaleY);
        return new SKRect(48f, 32f, w - 24f, h - 34f);
    }

    private Point ToSkiaPoint(MouseEventArgs e)
    {
        Point pt = e.GetPosition(SkiaCanvas);
        var dpi = VisualTreeHelper.GetDpi(SkiaCanvas);
        return new Point(pt.X * dpi.DpiScaleX, pt.Y * dpi.DpiScaleY);
    }

    private int FindNearestBandNode(Point skPt, SKRect rect, out double distance)
    {
        distance = double.MaxValue;
        int best = -1;
        float zeroDbY = rect.MidY;
        const float dbRange = 18.0f;

        for (int i = 0; i < _bands.Count; i++)
        {
            float bx = SkiaVisualizerRenderer.FreqToX(_bands[i].Frequency, rect);
            double db = Math.Clamp(_eqChain.EvaluateResponseDb(_bands[i].Frequency), -dbRange, dbRange);
            float by = zeroDbY - (float)(db / dbRange) * (rect.Height * 0.5f);

            double dx = skPt.X - bx;
            double dy = skPt.Y - by;
            double d = Math.Sqrt(dx * dx + dy * dy);
            if (d < distance)
            {
                distance = d;
                best = i;
            }
        }
        return best;
    }

    private void SkiaCanvas_MouseDown(object sender, MouseButtonEventArgs e)
    {
        SKRect rect = GetCanvasPlotRect();
        Point pt = ToSkiaPoint(e);
        int idx = _renderer.ShowEqCurveOverlay
            ? FindNearestBandNode(pt, rect, out double _)
            : -1;
        double nodeDist = idx >= 0 ? FindNearestNodeDistance(pt, rect) : double.MaxValue;

        if (idx >= 0 && nodeDist <= 28.0)
        {
            if (e.RightButton == MouseButtonState.Pressed)
            {
                // Right-click resets Gain, Frequency, and Q to default
                _bands[idx].ResetToDefault();
                SyncEqEngines(forceApoWrite: true);
                return;
            }

            _draggingBandIndex = idx;
            _renderer.HoveredBandIndex = idx;
            SkiaCanvas.CaptureMouse();
            UpdateDraggedBand2D(pt, rect);
            return;
        }

        // Double-clicking empty canvas space breaks out into Fullscreen immediately!
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            if ((DateTime.UtcNow - _lastCanvasClickTime).TotalMilliseconds < 320)
            {
                OpenVisualizerPopout(startFullscreen: true);
                _lastCanvasClickTime = DateTime.MinValue;
            }
            else
            {
                _lastCanvasClickTime = DateTime.UtcNow;
            }
        }
    }

    private double FindNearestNodeDistance(Point skPt, SKRect rect)
    {
        FindNearestBandNode(skPt, rect, out double dist);
        return dist;
    }

    private void SkiaCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        SKRect rect = GetCanvasPlotRect();
        Point pt = ToSkiaPoint(e);
        _renderer.CursorSkiaPoint = new SKPoint((float)pt.X, (float)pt.Y);

        if (_draggingBandIndex >= 0 && e.LeftButton == MouseButtonState.Pressed)
        {
            UpdateDraggedBand2D(pt, rect);
        }
        else
        {
            int idx = FindNearestBandNode(pt, rect, out double dist);
            _renderer.HoveredBandIndex = dist <= 26.0 ? idx : -1;
        }
    }

    private void SkiaCanvas_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_draggingBandIndex >= 0)
        {
            _draggingBandIndex = -1;
            _eqChain.SetSoloBand(-1);
            SkiaCanvas.ReleaseMouseCapture();
            SyncEqEngines(forceApoWrite: true);
        }
    }

    private void SkiaCanvas_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (!_renderer.ShowEqCurveOverlay) return;

        SKRect rect = GetCanvasPlotRect();
        Point pt = ToSkiaPoint(e);
        int idx = _draggingBandIndex >= 0
            ? _draggingBandIndex
            : FindNearestBandNode(pt, rect, out double dist) is var best && dist <= 32.0 ? best : -1;

        if (idx >= 0)
        {
            double step = e.Delta > 0 ? 0.18 : -0.18;
            _bands[idx].QFactor = Math.Clamp(_bands[idx].QFactor + step, 0.30, 8.00);
            _renderer.HoveredBandIndex = idx;
            SyncEqEngines(forceApoWrite: true);
            e.Handled = true;
        }
    }

    private void SkiaCanvas_MouseLeave(object sender, MouseEventArgs e)
    {
        _renderer.CursorSkiaPoint = null;
        if (_draggingBandIndex < 0)
            _renderer.HoveredBandIndex = -1;
    }

    private void UpdateDraggedBand2D(Point skPt, SKRect rect)
    {
        if (_draggingBandIndex < 0 || _draggingBandIndex >= _bands.Count)
            return;

        var band = _bands[_draggingBandIndex];
        float zeroDbY = rect.MidY;
        const double dbRange = 18.0;
        double normalizedY = (zeroDbY - skPt.Y) / (rect.Height * 0.5);
        double targetDb = Math.Clamp(normalizedY * dbRange - SldPreamp.Value, -15.0, 15.0);
        band.GainDb = targetDb;

        // Horizontal 2D frequency sweep bounded gently between neighboring bands
        double minFreq = _draggingBandIndex == 0 ? 22.0 : _bands[_draggingBandIndex - 1].Frequency * 1.12;
        double maxFreq = _draggingBandIndex == _bands.Count - 1 ? 19000.0 : _bands[_draggingBandIndex + 1].Frequency * 0.88;
        double targetFreq = Math.Clamp(SkiaVisualizerRenderer.XToFreq((float)skPt.X, rect), minFreq, maxFreq);
        band.Frequency = targetFreq;

        // Shift + Drag = Solo / Audition Band Mode
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            _eqChain.SetSoloBand(_draggingBandIndex, band.Frequency, band.QFactor * SldGlobalQ.Value);
        }
        else
        {
            _eqChain.SetSoloBand(-1);
        }
    }

    // ======================= UI EVENT HANDLERS =======================

    private void ApplyPreset(EqPreset preset)
    {
        _isUpdatingUi = true;
        SldPreamp.Value = preset.PreampDb;
        SldBassBoost.Value = preset.BassBoostDb;
        SldTrebleTilt.Value = preset.TrebleTiltDb;

        for (int i = 0; i < Math.Min(_bands.Count, preset.BandGains.Length); i++)
        {
            _bands[i].GainDb = preset.BandGains[i];
        }
        UpdateToneLabels();
        _isUpdatingUi = false;

        SyncEqEngines(forceApoWrite: true);
    }

    private void CmbPresets_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingUi) return;
        if (CmbPresets.SelectedItem is EqPreset preset)
        {
            ApplyPreset(preset);
        }
    }

    private void BtnResetEq_Click(object sender, RoutedEventArgs e)
    {
        _isUpdatingUi = true;
        CmbPresets.SelectedIndex = 0;
        SldGlobalQ.Value = 1.0;
        SldBalance.Value = 0.0;
        foreach (var b in _bands)
        {
            b.ResetToDefault();
        }
        _isUpdatingUi = false;
        ApplyPreset(EqPreset.BuiltInPresets[0]);
    }

    private void ChkEqEnabled_Changed(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingUi) return;
        SyncEqEngines(forceApoWrite: true);
    }

    private void CmbEqEngineMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingUi || CmbEqEngineMode == null) return;
        _engineMode = CmbEqEngineMode.SelectedIndex switch
        {
            1 => EqEngineMode.EqualizerApo,
            2 => EqEngineMode.InternalDsp,
            _ => EqEngineMode.Both
        };
        SyncEqEngines(forceApoWrite: true);
    }

    private void ToneControls_ValueChanged(object sender, RoutedEventArgs e)
    {
        if (!_isInitialized || _isUpdatingUi) return;
        UpdateToneLabels();
        SyncEqEngines();
    }

    private void UpdateToneLabels()
    {
        if (!_isInitialized) return;
        TxtPreampVal.Text = $"{SldPreamp.Value:+0.0;-0.0;0.0} dB";
        TxtBassBoostVal.Text = $"{SldBassBoost.Value:+0.0;-0.0;0.0} dB";
        TxtTrebleTiltVal.Text = $"{SldTrebleTilt.Value:+0.0;-0.0;0.0} dB";
        TxtGlobalQVal.Text = $"{SldGlobalQ.Value:0.00}x";

        double bal = SldBalance.Value;
        TxtBalanceVal.Text = Math.Abs(bal) < 0.04
            ? "Center"
            : bal < 0 ? $"L {-bal * 100:0}%" : $"R {bal * 100:0}%";
    }

    private void CmbVisualMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isInitialized) return;
        if (CmbVisualMode.SelectedItem is VisualizerModeItem item)
        {
            _renderer.Mode = item.Mode;
            bool isRta = item.Mode == VisualizerMode.StudioRtaEq;
            LblRtaStyle.Opacity = isRta ? 1.0 : 0.45;
            CmbRtaStyle.IsEnabled = isRta;
        }
    }

    private void BtnPrevMode_Click(object sender, RoutedEventArgs e)
    {
        int count = VisualizerPalettes.ModeItems.Count;
        CmbVisualMode.SelectedIndex = (CmbVisualMode.SelectedIndex - 1 + count) % count;
    }

    private void BtnNextMode_Click(object sender, RoutedEventArgs e)
    {
        int count = VisualizerPalettes.ModeItems.Count;
        CmbVisualMode.SelectedIndex = (CmbVisualMode.SelectedIndex + 1) % count;
    }

    private void CmbRtaStyle_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isInitialized) return;
        if (CmbRtaStyle.SelectedItem is RtaStyleItem item)
        {
            _renderer.RtaStyle = item.Style;
        }
    }

    private void CmbTheme_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isInitialized) return;
        if (CmbTheme.SelectedItem is VisualizerThemeItem item)
        {
            _renderer.Theme = item.Theme;
        }
    }

    private void BtnPrevTheme_Click(object sender, RoutedEventArgs e)
    {
        int count = VisualizerPalettes.ThemeItems.Count;
        CmbTheme.SelectedIndex = (CmbTheme.SelectedIndex - 1 + count) % count;
    }

    private void BtnNextTheme_Click(object sender, RoutedEventArgs e)
    {
        int count = VisualizerPalettes.ThemeItems.Count;
        CmbTheme.SelectedIndex = (CmbTheme.SelectedIndex + 1) % count;
    }

    private void VisControls_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_isInitialized || _isUpdatingUi) return;
        TxtVisGain.Text = $"{SldVisGain.Value:0.0}x";
        TxtVisSmoothing.Text = $"{SldVisSmoothing.Value * 100:0}%";
        TxtVisGravity.Text = $"{SldVisGravity.Value:0.1}x";
        TxtVisTilt.Text = $"+{SldVisTilt.Value:0.0} dB";

        _renderer.GlowIntensity = (float)SldVisGlow.Value;
        _renderer.BeatFlashIntensity = (float)SldVisBeat.Value;
    }

    private void CmbBandCount_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isInitialized || _renderer == null || CmbBandCount == null) return;
        _renderer.BandCount = CmbBandCount.SelectedIndex switch
        {
            0 => 16,
            1 => 32,
            2 => 64,
            _ => 128
        };
    }

    private void VisToggles_Changed(object sender, RoutedEventArgs e)
    {
        if (!_isInitialized || _renderer == null) return;
        _renderer.ShowEqCurveOverlay = ChkShowEqCurve.IsChecked == true;
        _renderer.ShowPreEqGhost = ChkShowPreGhost.IsChecked == true;
        _renderer.ShowPeakHoldCaps = ChkShowPeaks.IsChecked == true;
        _renderer.ShowMirrorReflection = ChkShowMirror.IsChecked == true;
        _analysisEngine.ApplyEqToVisuals = ChkApplyEqVisuals.IsChecked == true;
    }

    // ======================= MASTER VOLUME, TRANSPORT & ROUTING =======================

    private void CmbOutputDevices_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingUi) return;
        if (CmbOutputDevices.SelectedItem is AudioDeviceItem dev)
        {
            _mixerService.RefreshDefaultDevice(dev.Id);
            var mmDev = _mixerService.GetDeviceById(dev.Id, DataFlow.Render);
            _analysisEngine.StartLoopback(mmDev);
            RefreshAppSessions();
        }
    }

    private void SldMasterVolume_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isUpdatingUi || TxtMasterVolume == null) return;
        float v = (float)SldMasterVolume.Value;
        _mixerService.SetMasterVolumePercent(v);
        TxtMasterVolume.Text = $"{v:0}%";
    }

    private void BtnMasterMute_Click(object sender, RoutedEventArgs e)
    {
        bool newMute = !_mixerService.GetMasterMute();
        _mixerService.SetMasterMute(newMute);
        UpdateMuteButtonVisual(newMute);
    }

    private void UpdateMuteButtonVisual(bool isMuted)
    {
        BtnMasterMute.Content = isMuted ? "MUTE" : "VOL";
        BtnMasterMute.Background = isMuted
            ? new SolidColorBrush(Color.FromRgb(190, 18, 60))
            : new SolidColorBrush(Color.FromRgb(30, 41, 59));
    }

    private void BtnMediaPrev_Click(object sender, RoutedEventArgs e) => _mixerService.SendMediaPrevious();
    private void BtnMediaPlayPause_Click(object sender, RoutedEventArgs e) => _mixerService.SendMediaPlayPause();
    private void BtnMediaNext_Click(object sender, RoutedEventArgs e) => _mixerService.SendMediaNext();
    private void BtnMediaStop_Click(object sender, RoutedEventArgs e) => _mixerService.SendMediaStop();

    private void BtnTestSynth_Click(object sender, RoutedEventArgs e)
    {
        MMDevice? outDev = null;
        if (CmbOutputDevices.SelectedItem is AudioDeviceItem item)
        {
            outDev = _mixerService.GetDeviceById(item.Id, DataFlow.Render);
        }

        _dspRouter.ToggleTestSynth(outDev);
        BtnTestSynth.Content = _dspRouter.IsTestSynthPlaying
            ? "■ Stop Built-in DSP Synth Groove"
            : "▶ Test Internal DSP Synth Groove";
        BtnTestSynth.Background = _dspRouter.IsTestSynthPlaying
            ? new SolidColorBrush(Color.FromRgb(124, 58, 237))
            : new SolidColorBrush(Color.FromRgb(30, 27, 75));

        RefreshAppSessions();
        UpdateFooterStatus();
    }

    private void BtnRefreshSessions_Click(object sender, RoutedEventArgs e) => RefreshAppSessions();

    // ======================= POP-OUT & FULLSCREEN VISUALIZER =======================

    private void OpenVisualizerPopout(bool startFullscreen)
    {
        if (_popoutWindow != null)
        {
            _popoutWindow.Activate();
            if (startFullscreen)
            {
                _popoutWindow.ToggleFullscreen();
            }
            return;
        }

        _popoutWindow = new VisualizerPopoutWindow(
            _renderer,
            _snapshot,
            _eqChain,
            _bands,
            () => SyncEqEngines(forceApoWrite: true),
            startFullscreen);

        _popoutWindow.Closed += (_, _) =>
        {
            _popoutWindow = null;
            SkiaCanvas.Visibility = Visibility.Visible;
            UndockedBanner.Visibility = Visibility.Collapsed;
            BtnPopoutVisualizer.Content = "↗ Pop Out";
            ChkShowEqCurve.IsChecked = _renderer.ShowEqCurveOverlay;
            CmbVisualMode.SelectedItem = VisualizerPalettes.ModeItems.FirstOrDefault(m => m.Mode == _renderer.Mode);
            CmbRtaStyle.SelectedItem = VisualizerPalettes.RtaStyleItems.FirstOrDefault(s => s.Style == _renderer.RtaStyle);
            CmbTheme.SelectedItem = VisualizerPalettes.ThemeItems.FirstOrDefault(t => t.Theme == _renderer.Theme);
        };

        SkiaCanvas.Visibility = Visibility.Collapsed;
        UndockedBanner.Visibility = Visibility.Visible;
        BtnPopoutVisualizer.Content = "↙ Dock Back";
        _popoutWindow.Show();
    }

    private void DockVisualizerBack()
    {
        if (_popoutWindow != null)
        {
            _popoutWindow.Close();
            _popoutWindow = null;
        }
    }

    private void BtnPopoutVisualizer_Click(object sender, RoutedEventArgs e)
    {
        if (_popoutWindow != null)
        {
            DockVisualizerBack();
        }
        else
        {
            OpenVisualizerPopout(startFullscreen: false);
        }
    }

    private void BtnFullscreenVisualizer_Click(object sender, RoutedEventArgs e)
    {
        OpenVisualizerPopout(startFullscreen: true);
    }

    private void BtnDockVisualizerBack_Click(object sender, RoutedEventArgs e)
    {
        DockVisualizerBack();
    }

    private void MainWindow_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F11)
        {
            OpenVisualizerPopout(startFullscreen: true);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape && _popoutWindow != null)
        {
            DockVisualizerBack();
            e.Handled = true;
        }
    }

    private void BtnOpenApoFolder_Click(object sender, RoutedEventArgs e)
    {
        SyncEqEngines(forceApoWrite: true);
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = _apoService.ConfigDirectory,
                UseShellExecute = true
            });
        }
        catch { }
    }

    private void BtnToggleDspRoute_Click(object sender, RoutedEventArgs e)
    {
        if (_dspRouter.IsRouting)
        {
            _dspRouter.StopRouting();
            BtnToggleDspRoute.Content = "Start Route";
        }
        else
        {
            if (CmbDspInput.SelectedItem is AudioDeviceItem inItem &&
                CmbDspOutput.SelectedItem is AudioDeviceItem outItem)
            {
                var inDev = _mixerService.GetDeviceById(inItem.Id, inItem.IsLoopback ? DataFlow.Render : DataFlow.Capture);
                var outDev = _mixerService.GetDeviceById(outItem.Id, DataFlow.Render);
                if (inDev != null && outDev != null)
                {
                    _dspRouter.StartRouting(inDev, inItem.IsLoopback, outDev);
                    if (_dspRouter.IsRouting)
                    {
                        BtnToggleDspRoute.Content = "Stop Route";
                    }
                }
            }
        }
        UpdateFooterStatus();
    }
}
