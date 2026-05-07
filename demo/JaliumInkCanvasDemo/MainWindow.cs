using DotNetCampus.Inking;
using Jalium.UI;
using Jalium.UI.Controls;
using Jalium.UI.Input;
using Jalium.UI.Media;
using DotInkMode = DotNetCampus.Inking.InkCanvasEditingMode;

namespace JaliumInkCanvasDemo;

internal sealed class MainWindow : Window
{
    private readonly JaliumInkCanvas _inkCanvas;
    private readonly TextBlock _statusText;
    private readonly TextBlock _metricsText;
    private bool _isDarkMode = true;

    public MainWindow()
    {
        Title = "DotNetCampus InkCanvas - Jalium Demo (DX12)";
        Width = 1280;
        Height = 800;

        _inkCanvas = new JaliumInkCanvas
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            InkColor = Color.FromRgb(70, 168, 255),
            InkThickness = 8,
        };

        _statusText = new TextBlock
        {
            Margin = new Thickness(12),
            Text = "I: Ink, E: EraseByPoint, N: None, C: Clear, T: Toggle Theme",
            Foreground = new SolidColorBrush(Color.FromRgb(220, 220, 220)),
        };
        _metricsText = new TextBlock
        {
            Margin = new Thickness(12, 40, 12, 12),
            Foreground = new SolidColorBrush(Color.FromRgb(180, 180, 180)),
            Text = "metrics: warming up...",
        };

        var root = new Grid();
        root.Children.Add(_inkCanvas);
        root.Children.Add(_statusText);
        root.Children.Add(_metricsText);
        Content = root;
        ApplyTheme();

        AddHandler(KeyDownEvent, new KeyEventHandler(OnKeyDown));
        CompositionTarget.Rendering += OnRendering;
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.I:
                _inkCanvas.EditingMode = DotInkMode.Ink;
                _statusText.Text = "Mode: Ink";
                break;
            case Key.E:
                _inkCanvas.EditingMode = DotInkMode.EraseByPoint;
                _statusText.Text = "Mode: EraseByPoint";
                break;
            case Key.N:
                _inkCanvas.EditingMode = DotInkMode.None;
                _statusText.Text = "Mode: None";
                break;
            case Key.C:
                _inkCanvas.Clear();
                _statusText.Text = "Cleared";
                break;
            case Key.T:
                _isDarkMode = !_isDarkMode;
                ApplyTheme();
                _statusText.Text = _isDarkMode ? "Theme: Dark" : "Theme: Light";
                break;
            case Key.D1:
                _inkCanvas.Options.BackendMode = JaliumRenderBackendMode.NativePreferred;
                _inkCanvas.RefreshRenderer();
                _statusText.Text = "BackendMode: NativePreferred";
                break;
            case Key.D2:
                _inkCanvas.Options.BackendMode = JaliumRenderBackendMode.ForceNative;
                _inkCanvas.RefreshRenderer();
                _statusText.Text = "BackendMode: ForceNative";
                break;
            case Key.D3:
                _inkCanvas.Options.BackendMode = JaliumRenderBackendMode.ForceSkiaFallback;
                _inkCanvas.RefreshRenderer();
                _statusText.Text = "BackendMode: ForceSkiaFallback";
                break;
        }
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        var snapshot = _inkCanvas.GetPerformanceSnapshot();
        _metricsText.Text =
            $"renderer={_inkCanvas.ActiveRendererName}  " +
            $"fps={snapshot.Fps:F1}  " +
            $"frame={snapshot.AverageFrameMs:F2}ms  " +
            $"input/s={snapshot.InputPointsPerSecond}  " +
            $"cache(H/M)={snapshot.CacheHitCount}/{snapshot.CacheMissCount}";
    }

    private void ApplyTheme()
    {
        if (_isDarkMode)
        {
            Background = new SolidColorBrush(Color.FromRgb(24, 24, 24));
            _statusText.Foreground = new SolidColorBrush(Color.FromRgb(220, 220, 220));
            _metricsText.Foreground = new SolidColorBrush(Color.FromRgb(180, 180, 180));
            _inkCanvas.InkColor = Color.FromRgb(70, 168, 255);
        }
        else
        {
            Background = new SolidColorBrush(Color.FromRgb(246, 246, 246));
            _statusText.Foreground = new SolidColorBrush(Color.FromRgb(40, 40, 40));
            _metricsText.Foreground = new SolidColorBrush(Color.FromRgb(80, 80, 80));
            _inkCanvas.InkColor = Color.FromRgb(40, 80, 190);
        }
    }
}
