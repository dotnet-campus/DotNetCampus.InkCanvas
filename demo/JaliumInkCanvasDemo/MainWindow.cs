using DotNetCampus.Inking;
using DotNetCampus.Inking.Primitive;
using Jalium.UI;
using Jalium.UI.Controls;
using Jalium.UI.Input;
using Jalium.UI.Media;
using DotInkMode = DotNetCampus.Inking.InkCanvasEditingMode;
using System.Text;

namespace JaliumInkCanvasDemo;

internal sealed class MainWindow : Window
{
    private readonly JaliumInkCanvas _inkCanvas;
    private readonly TextBlock _statusText;
    private readonly TextBlock _metricsText;
    private readonly TextBlock _modeText;
    private readonly TextBlock _thicknessText;
    private bool _isDarkMode = false;

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
            Margin = new Thickness(12, 6, 12, 0),
            Text = "Ready",
            Foreground = new SolidColorBrush(Color.FromRgb(220, 220, 220)),
        };
        _modeText = new TextBlock
        {
            Margin = new Thickness(12, 2, 12, 0),
            Text = "Mode: Ink",
            Foreground = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
        };
        _metricsText = new TextBlock
        {
            Margin = new Thickness(12, 2, 12, 12),
            Foreground = new SolidColorBrush(Color.FromRgb(180, 180, 180)),
            Text = "metrics: warming up...",
        };
        _thicknessText = new TextBlock
        {
            Margin = new Thickness(0, 0, 0, 0),
            Text = $"Thickness: {_inkCanvas.InkThickness:F1}",
            VerticalAlignment = VerticalAlignment.Center,
        };

        var settingsPanel = BuildToolbar();
        var footer = BuildFooter();

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var canvasHost = new Border
        {
            Background = new SolidColorBrush(Color.White),
            Padding = new Thickness(0),
            Child = _inkCanvas,
        };

        Grid.SetRow(canvasHost, 0);
        Grid.SetRow(settingsPanel, 1);
        Grid.SetRow(footer, 2);
        root.Children.Add(canvasHost);
        root.Children.Add(settingsPanel);
        root.Children.Add(footer);
        Content = root;
        ApplyTheme();

        AddHandler(KeyDownEvent, new KeyEventHandler(OnKeyDown));
        _inkCanvas.StrokeCollected += _ => UpdateMetrics();
        _inkCanvas.StrokeErased += _ => UpdateMetrics();
        SetMode(DotInkMode.Ink, "Mode: Ink");
        UpdateMetrics();
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.I:
                SetMode(DotInkMode.Ink, "Mode: Ink");
                break;
            case Key.E:
                SetMode(DotInkMode.EraseByPoint, "Mode: EraseByPoint");
                break;
            case Key.P:
                SetMode(DotInkMode.EraseByPath, "Mode: EraseByPath");
                break;
            case Key.N:
                SetMode(DotInkMode.None, "Mode: None");
                break;
            case Key.C:
                _inkCanvas.Clear();
                _statusText.Text = "Cleared all strokes";
                UpdateMetrics();
                break;
            case Key.T:
                _isDarkMode = !_isDarkMode;
                ApplyTheme();
                _statusText.Text = _isDarkMode ? "Theme: Dark" : "Theme: Light";
                break;
        }
    }

    private void UpdateMetrics()
    {
        var snapshot = _inkCanvas.GetPerformanceSnapshot();
        _metricsText.Text =
            $"renderer={_inkCanvas.ActiveRendererName}  " +
            $"input/s={snapshot.InputPointsPerSecond}  " +
            $"strokes={snapshot.CommittedStrokeCount}  " +
            $"points={_inkCanvas.GetStrokeSnapshots().Sum(s => s.Points.Count)}";
    }

    private void ApplyTheme()
    {
        Background = new SolidColorBrush(Color.FromRgb(160, 160, 160));

        if (_isDarkMode)
        {
            _statusText.Foreground = new SolidColorBrush(Color.FromRgb(220, 220, 220));
            _modeText.Foreground = new SolidColorBrush(Color.FromRgb(200, 200, 200));
            _metricsText.Foreground = new SolidColorBrush(Color.FromRgb(180, 180, 180));
            _inkCanvas.InkColor = Color.FromRgb(70, 168, 255);
        }
        else
        {
            _statusText.Foreground = new SolidColorBrush(Color.FromRgb(40, 40, 40));
            _modeText.Foreground = new SolidColorBrush(Color.FromRgb(60, 60, 60));
            _metricsText.Foreground = new SolidColorBrush(Color.FromRgb(80, 80, 80));
            _inkCanvas.InkColor = Color.FromRgb(40, 80, 190);
        }
    }

    private FrameworkElement BuildToolbar()
    {
        var root = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Margin = new Thickness(12, 12, 12, 8),
            Spacing = 8,
        };

        var firstRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            VerticalAlignment = VerticalAlignment.Center,
        };

        firstRow.Children.Add(new TextBlock { Text = "Mode", VerticalAlignment = VerticalAlignment.Center });
        firstRow.Children.Add(CreateModeRadio("Pen", DotInkMode.Ink, true));
        firstRow.Children.Add(CreateModeRadio("Eraser", DotInkMode.EraseByPoint, false));
        firstRow.Children.Add(CreateModeRadio("Path Eraser", DotInkMode.EraseByPath, false));
        firstRow.Children.Add(CreateModeRadio("None", DotInkMode.None, false));

        // Thickness selector: use Jalium Slider.
        var thicknessSlider = new Slider
        {
            Minimum = 1,
            Maximum = 18,
            Value = _inkCanvas.InkThickness,
            Width = 180,
            Margin = new Thickness(8, 0, 0, 0),
        };
        thicknessSlider.ValueChanged += (_, e) =>
        {
            _inkCanvas.InkThickness = e.NewValue;
            _thicknessText.Text = $"Thickness: {e.NewValue:F1}";
            UpdateMetrics();
        };
        firstRow.Children.Add(_thicknessText);
        firstRow.Children.Add(thicknessSlider);

        // Ink color selector: use Jalium ColorPicker control.
        var colorPicker = new ColorPicker
        {
            IsCompact = true,
            IsAlphaEnabled = false,
            Color = _inkCanvas.InkColor,
            Margin = new Thickness(8, 0, 0, 0),
        };
        colorPicker.ColorChanged += (_, e) =>
        {
            _inkCanvas.InkColor = e.NewColor;
            _statusText.Text = $"InkColor changed: #{e.NewColor.R:X2}{e.NewColor.G:X2}{e.NewColor.B:X2}";
            UpdateMetrics();
        };
        firstRow.Children.Add(colorPicker);

        var secondRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var minDistanceText = new TextBlock
        {
            Text = $"Min Distance: {_inkCanvas.Options.MinPointDistance:F2}",
            VerticalAlignment = VerticalAlignment.Center,
        };
        var minDistanceSlider = new Slider
        {
            Minimum = 0,
            Maximum = 2,
            Value = _inkCanvas.Options.MinPointDistance,
            Width = 160,
        };
        minDistanceSlider.ValueChanged += (_, e) =>
        {
            _inkCanvas.Options.MinPointDistance = e.NewValue;
            minDistanceText.Text = $"Min Distance: {e.NewValue:F2}";
        };
        var eraserSizeText = new TextBlock
        {
            Text = $"Eraser Size: {_inkCanvas.Options.EraserSize:F1}",
            VerticalAlignment = VerticalAlignment.Center,
        };
        var eraserSizeSlider = new Slider
        {
            Minimum = 6,
            Maximum = 56,
            Value = _inkCanvas.Options.EraserSize,
            Width = 160,
        };
        eraserSizeSlider.ValueChanged += (_, e) =>
        {
            _inkCanvas.Options.EraserSize = e.NewValue;
            eraserSizeText.Text = $"Eraser Size: {e.NewValue:F1}";
        };
        var minEraserText = new TextBlock
        {
            Text = $"Min Eraser: {_inkCanvas.Options.MinEraserSize:F1}",
            VerticalAlignment = VerticalAlignment.Center,
        };
        var minEraserSlider = new Slider
        {
            Minimum = 1,
            Maximum = 80,
            Value = _inkCanvas.Options.MinEraserSize,
            Width = 130,
        };
        minEraserSlider.ValueChanged += (_, e) =>
        {
            _inkCanvas.Options.MinEraserSize = e.NewValue;
            minEraserText.Text = $"Min Eraser: {e.NewValue:F1}";
        };
        var maxEraserText = new TextBlock
        {
            Text = $"Max Eraser: {_inkCanvas.Options.MaxEraserSize:F0}",
            VerticalAlignment = VerticalAlignment.Center,
        };
        var maxEraserSlider = new Slider
        {
            Minimum = 40,
            Maximum = 800,
            Value = _inkCanvas.Options.MaxEraserSize,
            Width = 130,
        };
        maxEraserSlider.ValueChanged += (_, e) =>
        {
            _inkCanvas.Options.MaxEraserSize = e.NewValue;
            maxEraserText.Text = $"Max Eraser: {e.NewValue:F0}";
        };
        var pressureText = new TextBlock
        {
            Text = $"Pressure Smooth: {_inkCanvas.Options.PressureSmoothingFactor:F2}",
            VerticalAlignment = VerticalAlignment.Center,
        };
        var pressureSlider = new Slider
        {
            Minimum = 0,
            Maximum = 0.95,
            Value = _inkCanvas.Options.PressureSmoothingFactor,
            Width = 150,
        };
        pressureSlider.ValueChanged += (_, e) =>
        {
            _inkCanvas.Options.PressureSmoothingFactor = (float)e.NewValue;
            pressureText.Text = $"Pressure Smooth: {e.NewValue:F2}";
        };

        var sampleToggle = CreateToggle("High Sampling", _inkCanvas.Options.EnableIntermediatePoints, isChecked =>
        {
            _inkCanvas.Options.EnableIntermediatePoints = isChecked;
            _statusText.Text = $"HighSampling: {(isChecked ? "On" : "Off")}";
            UpdateMetrics();
        });

        secondRow.Children.Add(pressureText);
        secondRow.Children.Add(pressureSlider);
        secondRow.Children.Add(sampleToggle);
        secondRow.Children.Add(minDistanceText);
        secondRow.Children.Add(minDistanceSlider);
        secondRow.Children.Add(eraserSizeText);
        secondRow.Children.Add(eraserSizeSlider);
        secondRow.Children.Add(minEraserText);
        secondRow.Children.Add(minEraserSlider);
        secondRow.Children.Add(maxEraserText);
        secondRow.Children.Add(maxEraserSlider);
        secondRow.Children.Add(CreateToggle("Ignore Pressure", _inkCanvas.Options.IgnorePressure, isChecked =>
        {
            _inkCanvas.Options.IgnorePressure = isChecked;
            _inkCanvas.RefreshRenderer();
            _statusText.Text = $"IgnorePressure: {(isChecked ? "On" : "Off")}";
            UpdateMetrics();
        }));
        secondRow.Children.Add(CreateToggle("Stylus Eraser Size", _inkCanvas.Options.EnableStylusSizeAsEraserSize, isChecked =>
        {
            _inkCanvas.Options.EnableStylusSizeAsEraserSize = isChecked;
            _statusText.Text = $"StylusEraserSize: {(isChecked ? "On" : "Off")}";
        }));
        secondRow.Children.Add(CreateToggle("Eraser Follow Always", _inkCanvas.Options.CanEraserAlwaysFollowsTouchSize, isChecked =>
        {
            _inkCanvas.Options.CanEraserAlwaysFollowsTouchSize = isChecked;
            _statusText.Text = $"EraserFollowAlways: {(isChecked ? "On" : "Off")}";
        }));
        secondRow.Children.Add(CreateToggle("Lock Min Eraser", _inkCanvas.Options.LockMinEraserSize, isChecked =>
        {
            _inkCanvas.Options.LockMinEraserSize = isChecked;
            _statusText.Text = $"LockMinEraser: {(isChecked ? "On" : "Off")}";
        }));
        secondRow.Children.Add(CreateToggle("Pressure Smoothing", _inkCanvas.Options.EnablePressureSmoothing, isChecked =>
        {
            _inkCanvas.Options.EnablePressureSmoothing = isChecked;
            _statusText.Text = $"PressureSmoothing: {(isChecked ? "On" : "Off")}";
            UpdateMetrics();
        }));
        secondRow.Children.Add(CreateToggle("Layer Cache", _inkCanvas.Options.EnableCommittedLayerCache, isChecked =>
        {
            _inkCanvas.Options.EnableCommittedLayerCache = isChecked;
            _statusText.Text = $"CommittedLayerCache: {(isChecked ? "On" : "Off")}";
            _inkCanvas.InvalidateVisual();
            UpdateMetrics();
        }));
        secondRow.Children.Add(CreateToggle("Perf Counters", _inkCanvas.Options.EnablePerformanceCounters, isChecked =>
        {
            _inkCanvas.Options.EnablePerformanceCounters = isChecked;
            _statusText.Text = $"PerformanceCounters: {(isChecked ? "On" : "Off")}";
            UpdateMetrics();
        }));
        secondRow.Children.Add(CreateToggle("Eraser Overlay", _inkCanvas.Options.ShowEraserOverlay, isChecked =>
        {
            _inkCanvas.Options.ShowEraserOverlay = isChecked;
            _statusText.Text = $"EraserOverlay: {(isChecked ? "On" : "Off")}";
            _inkCanvas.InvalidateVisual();
            UpdateMetrics();
        }));

        secondRow.Children.Add(CreateActionButton("Default Overlay", () =>
        {
            _inkCanvas.Options.EraserOverlayCreator = null;
            _statusText.Text = "Eraser overlay: default";
        }));
        secondRow.Children.Add(CreateActionButton("Custom Overlay", () =>
        {
            _inkCanvas.Options.EraserOverlayCreator = static () => new DemoEraserOverlay();
            _statusText.Text = "Eraser overlay: custom";
        }));

        secondRow.Children.Add(CreateActionButton("Save Stroke As Svg", SaveStrokeAsSvg));
        secondRow.Children.Add(CreateActionButton("Add Sample Stroke", AddSampleStroke));
        secondRow.Children.Add(CreateActionButton("Remove Blue Strokes", RemoveBlueStrokes));
        secondRow.Children.Add(CreateActionButton("Clear", () =>
        {
            _inkCanvas.Clear();
            _statusText.Text = "Cleared all strokes";
            UpdateMetrics();
        }));
        secondRow.Children.Add(CreateActionButton("Theme", () =>
        {
            _isDarkMode = !_isDarkMode;
            ApplyTheme();
            _statusText.Text = _isDarkMode ? "Theme: Dark" : "Theme: Light";
        }));

        root.Children.Add(firstRow);
        root.Children.Add(secondRow);
        return root;
    }

    private FrameworkElement BuildFooter()
    {
        var footer = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Margin = new Thickness(0, 4, 0, 8),
        };
        footer.Children.Add(_statusText);
        footer.Children.Add(_modeText);
        footer.Children.Add(_metricsText);
        return footer;
    }

    private Button CreateActionButton(string text, Action handler)
    {
        var button = new Button
        {
            Content = text,
            Padding = new Thickness(10, 6),
        };
        button.Click += (_, _) => handler();
        return button;
    }

    private RadioButton CreateModeRadio(string text, DotInkMode mode, bool isChecked)
    {
        var radio = new RadioButton
        {
            GroupName = "InkMode",
            Content = text,
            IsChecked = isChecked,
            Padding = new Thickness(10, 6),
            VerticalAlignment = VerticalAlignment.Center,
        };
        radio.Checked += (_, _) => SetMode(mode, $"Mode: {mode}");
        return radio;
    }

    private CheckBox CreateToggle(string text, bool isChecked, Action<bool> onChanged)
    {
        var checkBox = new CheckBox
        {
            Content = text,
            IsChecked = isChecked,
            VerticalAlignment = VerticalAlignment.Center,
        };
        checkBox.Checked += (_, _) => onChanged(true);
        checkBox.Unchecked += (_, _) => onChanged(false);
        return checkBox;
    }

    private void SetMode(DotInkMode mode, string modeText)
    {
        _inkCanvas.EditingMode = mode;
        _modeText.Text = modeText;
        _statusText.Text = $"{modeText} (Toolbar/Hotkey)";
        UpdateMetrics();
    }

    private void SaveStrokeAsSvg()
    {
        var strokes = _inkCanvas.GetStrokeSnapshots();
        if (strokes.Count == 0)
        {
            _statusText.Text = "No stroke to export";
            return;
        }

        var allPoints = strokes.SelectMany(s => s.Points).ToList();
        var minX = allPoints.Min(p => p.X);
        var minY = allPoints.Min(p => p.Y);
        var maxX = allPoints.Max(p => p.X);
        var maxY = allPoints.Max(p => p.Y);

        var width = Math.Max(1, maxX - minX + 8);
        var height = Math.Max(1, maxY - minY + 8);

        var outputDir = Path.Combine(AppContext.BaseDirectory, "exports");
        Directory.CreateDirectory(outputDir);
        var file = Path.Combine(outputDir, $"ink-{DateTime.Now:yyyyMMdd-HHmmss}.svg");

        var sb = new StringBuilder();
        sb.AppendLine($"""<svg xmlns="http://www.w3.org/2000/svg" width="{width:F1}" height="{height:F1}" viewBox="0 0 {width:F1} {height:F1}">""");
        foreach (var stroke in strokes)
        {
            if (stroke.Points.Count == 0)
            {
                continue;
            }

            var color = stroke.Color;
            var hex = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
            var pathBuilder = new StringBuilder();
            for (int i = 0; i < stroke.Points.Count; i++)
            {
                var p = stroke.Points[i];
                var x = p.X - minX + 4;
                var y = p.Y - minY + 4;
                pathBuilder.Append(i == 0 ? "M " : " L ");
                pathBuilder.Append($"{x:F2} {y:F2}");
            }

            sb.AppendLine($"""  <path d="{pathBuilder}" stroke="{hex}" stroke-width="{stroke.Thickness:F2}" fill="none" stroke-linecap="round" stroke-linejoin="round" />""");
        }

        sb.AppendLine("</svg>");
        File.WriteAllText(file, sb.ToString(), Encoding.UTF8);
        _statusText.Text = $"SVG exported: {file}";
    }

    private void AddSampleStroke()
    {
        var color = _isDarkMode ? Color.FromRgb(255, 196, 64) : Color.FromRgb(30, 144, 255);
        var points = new List<InkStylusPoint>
        {
            new(120, 120, 0.9f),
            new(220, 180, 0.8f),
            new(360, 160, 0.7f),
            new(520, 240, 0.75f),
        };

        _inkCanvas.AddStroke(new InkStrokeSnapshot(color, Math.Max(1d, _inkCanvas.InkThickness), points));
        _statusText.Text = "Added sample stroke";
        UpdateMetrics();
    }

    private void RemoveBlueStrokes()
    {
        var removed = _inkCanvas.RemoveStrokes(stroke => stroke.Color.B > stroke.Color.R && stroke.Color.B > stroke.Color.G);
        _statusText.Text = removed > 0 ? $"Removed {removed} blue stroke(s)" : "No blue stroke removed";
        UpdateMetrics();
    }

    private sealed class DemoEraserOverlay : IJaliumEraserOverlay
    {
        private readonly SolidColorBrush _fill = new(Color.FromRgb(255, 128, 0));
        private readonly SolidColorBrush _stroke = new(Color.FromRgb(255, 240, 180));
        private Rect _rect;

        public void Update(Rect eraserRect)
        {
            _rect = eraserRect;
        }

        public void Render(DrawingContext drawingContext)
        {
            var geometry = new RectangleGeometry(_rect, 10, 10);
            drawingContext.DrawGeometry(_fill, new Pen(_stroke, 1.5), geometry);
        }
    }
}
