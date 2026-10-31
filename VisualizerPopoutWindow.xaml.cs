using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using SkiaSharp;
using SkiaSharp.Views.Desktop;
using SonicDeck.Audio;
using SonicDeck.Audio.Dsp;
using SonicDeck.Models;
using SonicDeck.Visuals;

namespace SonicDeck;

public partial class VisualizerPopoutWindow : Window
{
    private readonly SkiaVisualizerRenderer _renderer;
    private readonly AudioAnalysisSnapshot _snapshot;
    private readonly BiquadEqChain _eqChain;
    private readonly IReadOnlyList<EqBand> _bands;
    private readonly Action _onEqChanged;
    private readonly DispatcherTimer _hudHideTimer;

    private bool _isInitialized;
    private bool _isFullscreen;
    private WindowState _prevWindowState = WindowState.Normal;
    private WindowStyle _prevWindowStyle = WindowStyle.SingleBorderWindow;
    private ResizeMode _prevResizeMode = ResizeMode.CanResize;

    private int _draggingBandIndex = -1;
    private Point _lastMousePos;
    private DateTime _lastClickTime = DateTime.MinValue;

    public VisualizerPopoutWindow(
        SkiaVisualizerRenderer renderer,
        AudioAnalysisSnapshot snapshot,
        BiquadEqChain eqChain,
        IReadOnlyList<EqBand> bands,
        Action onEqChanged,
        bool startFullscreen)
    {
        _renderer = renderer;
        _snapshot = snapshot;
        _eqChain = eqChain;
        _bands = bands;
        _onEqChanged = onEqChanged;

        InitializeComponent();
        Icon = AppIconGenerator.CreateWindowIconSource();

        CmbPopoutMode.ItemsSource = VisualizerPalettes.ModeItems;
        CmbPopoutMode.DisplayMemberPath = nameof(VisualizerModeItem.DisplayName);
        CmbPopoutMode.SelectedItem = VisualizerPalettes.ModeItems.FirstOrDefault(m => m.Mode == _renderer.Mode);

        CmbPopoutRtaStyle.ItemsSource = VisualizerPalettes.RtaStyleItems;
        CmbPopoutRtaStyle.DisplayMemberPath = nameof(RtaStyleItem.DisplayName);
        CmbPopoutRtaStyle.SelectedItem = VisualizerPalettes.RtaStyleItems.FirstOrDefault(s => s.Style == _renderer.RtaStyle);

        CmbPopoutTheme.ItemsSource = VisualizerPalettes.ThemeItems;
        CmbPopoutTheme.SelectedItem = VisualizerPalettes.ThemeItems.FirstOrDefault(t => t.Theme == _renderer.Theme);

        ChkPopoutEqOverlay.IsChecked = _renderer.ShowEqCurveOverlay;
        _isInitialized = true;

        _hudHideTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2.6)
        };
        _hudHideTimer.Tick += (_, _) =>
        {
            if (!HudBar.IsMouseOver && _draggingBandIndex < 0)
            {
                HudBar.Visibility = Visibility.Collapsed;
                if (_isFullscreen)
                    Cursor = Cursors.None;
            }
            _hudHideTimer.Stop();
        };
        _hudHideTimer.Start();

        Loaded += (_, _) =>
        {
            if (startFullscreen)
            {
                SetFullscreen(true);
            }
        };

        Closed += (_, _) =>
        {
            _eqChain.SetSoloBand(-1);
            _renderer.CursorSkiaPoint = null;
        };
    }

    public bool IsFullscreen => _isFullscreen;

    public void InvalidateVisualizer()
    {
        PopoutSkiaCanvas.InvalidateVisual();
    }

    public void SetFullscreen(bool fullscreen)
    {
        if (_isFullscreen == fullscreen)
            return;

        _isFullscreen = fullscreen;
        if (_isFullscreen)
        {
            _prevWindowState = WindowState;
            _prevWindowStyle = WindowStyle;
            _prevResizeMode = ResizeMode;

            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            WindowState = WindowState.Normal;
            WindowState = WindowState.Maximized;
            Topmost = true;

            BtnToggleFullscreen.Content = "❐ Windowed (F11)";
        }
        else
        {
            Topmost = false;
            WindowStyle = _prevWindowStyle;
            ResizeMode = _prevResizeMode;
            WindowState = _prevWindowState == WindowState.Maximized ? WindowState.Normal : _prevWindowState;
            Cursor = Cursors.Arrow;

            BtnToggleFullscreen.Content = "⛶ Fullscreen (F11)";
        }

        ShowHudTemporarily();
    }

    public void ToggleFullscreen() => SetFullscreen(!_isFullscreen);

    private void ShowHudTemporarily()
    {
        HudBar.Visibility = Visibility.Visible;
        Cursor = Cursors.Arrow;
        _hudHideTimer.Stop();
        _hudHideTimer.Start();
    }

    private void Window_MouseMove(object sender, MouseEventArgs e)
    {
        Point pos = e.GetPosition(this);
        if ((pos - _lastMousePos).Length > 3.0)
        {
            _lastMousePos = pos;
            ShowHudTemporarily();
        }
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                Close();
                e.Handled = true;
                break;

            case Key.F11:
            case Key.F:
                ToggleFullscreen();
                e.Handled = true;
                break;

            case Key.D1:
            case Key.NumPad1:
                SelectMode(VisualizerMode.StudioRtaEq);
                break;
            case Key.D2:
            case Key.NumPad2:
                SelectMode(VisualizerMode.WinampClassicLed);
                break;
            case Key.D3:
            case Key.NumPad3:
                SelectMode(VisualizerMode.HarmonicOscilloscope);
                break;
            case Key.D4:
            case Key.NumPad4:
                SelectMode(VisualizerMode.RadialBassReactor);
                break;
            case Key.D5:
            case Key.NumPad5:
                SelectMode(VisualizerMode.CyberHorizonGrid);
                break;

            case Key.T:
                int count = VisualizerPalettes.ThemeItems.Count;
                CmbPopoutTheme.SelectedIndex = (CmbPopoutTheme.SelectedIndex + 1) % count;
                ShowHudTemporarily();
                e.Handled = true;
                break;
        }
    }

    private void SelectMode(VisualizerMode mode)
    {
        _renderer.Mode = mode;
        CmbPopoutMode.SelectedItem = VisualizerPalettes.ModeItems.FirstOrDefault(m => m.Mode == mode);
    }

    private void PopoutSkiaCanvas_PaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        _renderer.Render(
            e.Surface.Canvas,
            e.Info.Width,
            e.Info.Height,
            _snapshot,
            _eqChain,
            _bands);
    }

    private SKRect GetCanvasPlotRect()
    {
        var dpi = VisualTreeHelper.GetDpi(PopoutSkiaCanvas);
        float w = (float)(PopoutSkiaCanvas.ActualWidth * dpi.DpiScaleX);
        float h = (float)(PopoutSkiaCanvas.ActualHeight * dpi.DpiScaleY);
        return new SKRect(48f, 32f, w - 24f, h - 34f);
    }

    private Point ToSkiaPoint(MouseEventArgs e)
    {
        Point pt = e.GetPosition(PopoutSkiaCanvas);
        var dpi = VisualTreeHelper.GetDpi(PopoutSkiaCanvas);
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

    private void PopoutSkiaCanvas_MouseDown(object sender, MouseButtonEventArgs e)
    {
        ShowHudTemporarily();

        SKRect rect = GetCanvasPlotRect();
        Point pt = ToSkiaPoint(e);
        int idx = _renderer.ShowEqCurveOverlay
            ? FindNearestBandNode(pt, rect, out double nodeDist)
            : -1;
        double dist = idx >= 0 ? FindDistance(pt, rect) : double.MaxValue;

        if (idx >= 0 && dist <= 28.0)
        {
            if (e.RightButton == MouseButtonState.Pressed)
            {
                _bands[idx].ResetToDefault();
                _onEqChanged();
                return;
            }

            _draggingBandIndex = idx;
            _renderer.HoveredBandIndex = idx;
            PopoutSkiaCanvas.CaptureMouse();
            UpdateDraggedBand2D(pt, rect);
            return;
        }

        if (e.LeftButton == MouseButtonState.Pressed)
        {
            if ((DateTime.UtcNow - _lastClickTime).TotalMilliseconds < 320)
            {
                ToggleFullscreen();
                _lastClickTime = DateTime.MinValue;
            }
            else
            {
                _lastClickTime = DateTime.UtcNow;
            }
        }
    }

    private double FindDistance(Point skPt, SKRect rect)
    {
        FindNearestBandNode(skPt, rect, out double dist);
        return dist;
    }

    private void PopoutSkiaCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        SKRect rect = GetCanvasPlotRect();
        Point pt = ToSkiaPoint(e);
        _renderer.CursorSkiaPoint = new SKPoint((float)pt.X, (float)pt.Y);

        if (_draggingBandIndex >= 0 && e.LeftButton == MouseButtonState.Pressed)
        {
            UpdateDraggedBand2D(pt, rect);
        }
        else if (_renderer.ShowEqCurveOverlay)
        {
            int idx = FindNearestBandNode(pt, rect, out double dist);
            _renderer.HoveredBandIndex = dist <= 26.0 ? idx : -1;
        }
    }

    private void PopoutSkiaCanvas_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_draggingBandIndex >= 0)
        {
            _draggingBandIndex = -1;
            _eqChain.SetSoloBand(-1);
            PopoutSkiaCanvas.ReleaseMouseCapture();
            _onEqChanged();
        }
    }

    private void PopoutSkiaCanvas_MouseWheel(object sender, MouseWheelEventArgs e)
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
            _onEqChanged();
            e.Handled = true;
        }
    }

    private void PopoutSkiaCanvas_MouseLeave(object sender, MouseEventArgs e)
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
        band.GainDb = Math.Clamp(normalizedY * dbRange, -15.0, 15.0);

        // Horizontal 2D frequency sweep bounded gently between neighboring bands
        double minFreq = _draggingBandIndex == 0 ? 22.0 : _bands[_draggingBandIndex - 1].Frequency * 1.12;
        double maxFreq = _draggingBandIndex == _bands.Count - 1 ? 19000.0 : _bands[_draggingBandIndex + 1].Frequency * 0.88;
        double targetFreq = Math.Clamp(SkiaVisualizerRenderer.XToFreq((float)skPt.X, rect), minFreq, maxFreq);
        band.Frequency = targetFreq;

        // Shift + Drag = Solo / Audition Band Mode
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            _eqChain.SetSoloBand(_draggingBandIndex, band.Frequency, band.QFactor);
        }
        else
        {
            _eqChain.SetSoloBand(-1);
        }

        _onEqChanged();
    }

    private void CmbPopoutMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isInitialized) return;
        if (CmbPopoutMode.SelectedItem is VisualizerModeItem item)
        {
            _renderer.Mode = item.Mode;
        }
    }

    private void CmbPopoutRtaStyle_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isInitialized) return;
        if (CmbPopoutRtaStyle.SelectedItem is RtaStyleItem item)
        {
            _renderer.RtaStyle = item.Style;
        }
    }

    private void CmbPopoutTheme_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isInitialized) return;
        if (CmbPopoutTheme.SelectedItem is VisualizerThemeItem item)
        {
            _renderer.Theme = item.Theme;
        }
    }

    private void ChkPopoutEqOverlay_Changed(object sender, RoutedEventArgs e)
    {
        if (!_isInitialized || _renderer == null) return;
        _renderer.ShowEqCurveOverlay = ChkPopoutEqOverlay.IsChecked == true;
    }

    private void BtnToggleFullscreen_Click(object sender, RoutedEventArgs e) => ToggleFullscreen();

    private void BtnDockBack_Click(object sender, RoutedEventArgs e) => Close();
}
