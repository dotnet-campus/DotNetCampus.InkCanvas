using DotNetCampus.Inking.Metrics;
using DotNetCampus.Inking.Primitive;
using DotNetCampus.Inking.Smoothing;
using DotNetCampus.Numerics.Geometry;
using Jalium.UI;
using Jalium.UI.Controls;
using Jalium.UI.Media;
using NativeDrawingAttributes = Jalium.UI.Controls.Ink.DrawingAttributes;
using NativeInkCanvas = Jalium.UI.Controls.InkCanvas;
using NativeInkCanvasEditingMode = Jalium.UI.Controls.InkCanvasEditingMode;
using NativeStroke = Jalium.UI.Controls.Ink.Stroke;
using NativeStylusPoint = Jalium.UI.Input.StylusPoint;
using NativeStylusPointCollection = Jalium.UI.Input.StylusPointCollection;

namespace DotNetCampus.Inking;

public class JaliumInkCanvas : Border
{
    private readonly NativeInkCanvas _nativeInkCanvas;
    private readonly PressureSmoother _pressureSmoother = new();
    private readonly InkPerformanceCounter _performanceCounter = new();

    private InkCanvasEditingMode _editingMode = InkCanvasEditingMode.Ink;
    private double _inkThickness = 6d;
    private Color _inkColor = Color.Red;
    private bool _isNormalizingCollectedStroke;

    public JaliumInkCanvas()
    {
        _nativeInkCanvas = new NativeInkCanvas
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Background = new SolidColorBrush(Color.FromArgb(1, 255, 255, 255)),
            EditingMode = NativeInkCanvasEditingMode.None,
        };

        Child = _nativeInkCanvas;
        Background = new SolidColorBrush(Color.FromArgb(1, 255, 255, 255));

        _nativeInkCanvas.StrokeCollected += OnNativeStrokeCollected;
        _nativeInkCanvas.StrokeErasing += OnNativeStrokeErasing;

        ApplyDrawingAttributes();
        ApplyEditingMode();
    }

    public InkCanvasEditingMode EditingMode
    {
        get => _editingMode;
        set
        {
            if (_editingMode == value)
            {
                return;
            }

            _editingMode = value;
            ApplyEditingMode();
        }
    }

    public JaliumInkCanvasRenderingOptions Options { get; } = new();

    public Color InkColor
    {
        get => _inkColor;
        set
        {
            _inkColor = value;
            ApplyDrawingAttributes();
        }
    }

    public double InkThickness
    {
        get => _inkThickness;
        set
        {
            _inkThickness = Math.Max(1d, value);
            ApplyDrawingAttributes();
        }
    }

    public int StrokeCount => _nativeInkCanvas.Strokes.Count;

    public IReadOnlyList<InkStrokeSnapshot> Strokes => GetStrokeSnapshots();

    public string ActiveRendererName => "jalium-native-inkcanvas";

    public bool IsNativeBackendAvailable => true;

    public event Action<InkStrokeSnapshot>? StrokeCollected;

    public event Action<IReadOnlyList<InkStrokeSnapshot>>? StrokeErased;

    public JaliumInkCanvasPerformanceSnapshot GetPerformanceSnapshot()
    {
        return _performanceCounter.Snapshot(
            _nativeInkCanvas.Strokes.Count,
            0,
            0,
            0);
    }

    public IReadOnlyList<InkStrokeSnapshot> GetStrokeSnapshots()
    {
        return _nativeInkCanvas.Strokes.Select(ToSnapshot).ToList();
    }

    public void RefreshRenderer()
    {
        ApplyEditingMode();
        _nativeInkCanvas.InvalidateVisual();
    }

    public void Clear()
    {
        _nativeInkCanvas.ClearStrokes();
        _nativeInkCanvas.InvalidateVisual();
    }

    public void AddStroke(InkStrokeSnapshot stroke)
    {
        if (stroke.Points.Count == 0)
        {
            return;
        }

        _nativeInkCanvas.Strokes.Add(ToNativeStroke(stroke));
        _nativeInkCanvas.InvalidateVisual();
    }

    public void AddStrokes(IEnumerable<InkStrokeSnapshot> strokes)
    {
        var nativeStrokes = strokes
            .Where(stroke => stroke.Points.Count > 0)
            .Select(ToNativeStroke)
            .ToList();

        if (nativeStrokes.Count == 0)
        {
            return;
        }

        _nativeInkCanvas.Strokes.AddRange(nativeStrokes);
        _nativeInkCanvas.InvalidateVisual();
    }

    public int RemoveStrokes(Predicate<InkStrokeSnapshot> predicate)
    {
        var removed = _nativeInkCanvas.Strokes
            .Where(stroke => predicate(ToSnapshot(stroke)))
            .ToList();
        if (removed.Count == 0)
        {
            return 0;
        }

        _nativeInkCanvas.Strokes.RemoveRange(removed);
        StrokeErased?.Invoke(removed.Select(ToSnapshot).ToList());
        _nativeInkCanvas.InvalidateVisual();
        return removed.Count;
    }

    private void OnNativeStrokeCollected(object? sender, InkCanvasStrokeCollectedEventArgs e)
    {
        if (_isNormalizingCollectedStroke)
        {
            return;
        }

        var snapshot = ToSnapshot(e.Stroke);
        var points = NormalizeCollectedPoints(snapshot.Points);
        _performanceCounter.AddInputPoints(points.Count);
        _performanceCounter.AddCommitUpdate(0);

        if (!AreSamePoints(snapshot.Points, points))
        {
            var normalizedStroke = ToNativeStroke(snapshot with { Points = points });
            ReplaceNativeStroke(e.Stroke, normalizedStroke);
            StrokeCollected?.Invoke(ToSnapshot(normalizedStroke));
            return;
        }

        StrokeCollected?.Invoke(snapshot);
    }

    private void OnNativeStrokeErasing(object? sender, InkCanvasStrokeErasingEventArgs e)
    {
        StrokeErased?.Invoke([ToSnapshot(e.Stroke)]);
    }

    private void ApplyEditingMode()
    {
        _nativeInkCanvas.EditingMode = _editingMode switch
        {
            InkCanvasEditingMode.Ink => NativeInkCanvasEditingMode.Ink,
            InkCanvasEditingMode.EraseByPoint => NativeInkCanvasEditingMode.EraseByPoint,
            InkCanvasEditingMode.EraseByPath => NativeInkCanvasEditingMode.EraseByStroke,
            InkCanvasEditingMode.None => NativeInkCanvasEditingMode.None,
            _ => NativeInkCanvasEditingMode.None,
        };
    }

    private void ApplyDrawingAttributes()
    {
        var attributes = new NativeDrawingAttributes
        {
            Color = _inkColor,
            Width = _inkThickness,
            Height = _inkThickness,
            IgnorePressure = Options.IgnorePressure,
            FitToCurve = true,
        };
        _nativeInkCanvas.DefaultDrawingAttributes = attributes;
        if (_nativeInkCanvas.DynamicRenderer is { } dynamicRenderer)
        {
            dynamicRenderer.DrawingAttributes = attributes.Clone();
        }
    }

    private IReadOnlyList<InkStylusPoint> NormalizeCollectedPoints(IReadOnlyList<InkStylusPoint> points)
    {
        if (points.Count == 0)
        {
            return [];
        }

        var filtered = new List<InkStylusPoint>(points.Count)
        {
            points[0],
        };

        filtered.AddRange(FilterPointsByDistance(points.Skip(1).ToList(), points[0]));
        var pressureSource = Options.EnablePressureSmoothing
            ? _pressureSmoother.Smooth(filtered, Options.PressureSmoothingFactor)
            : filtered;

        if (Options.EnableIntermediatePoints)
        {
            return pressureSource;
        }

        return pressureSource.Count <= 2
            ? pressureSource
            : FilterPointsByDistance(pressureSource.Skip(1).ToList(), pressureSource[0]).Prepend(pressureSource[0]).ToList();
    }

    private List<InkStylusPoint> FilterPointsByDistance(IReadOnlyList<InkStylusPoint> points, InkStylusPoint lastPoint)
    {
        var minDistance = Math.Max(0d, Options.MinPointDistance);
        if (minDistance <= 0)
        {
            return points.ToList();
        }

        var result = new List<InkStylusPoint>(points.Count);
        var previous = lastPoint;
        foreach (var point in points)
        {
            var dx = point.X - previous.X;
            var dy = point.Y - previous.Y;
            if (Math.Sqrt(dx * dx + dy * dy) < minDistance)
            {
                continue;
            }

            result.Add(point);
            previous = point;
        }

        return result;
    }

    private void ReplaceNativeStroke(NativeStroke oldStroke, NativeStroke newStroke)
    {
        var index = _nativeInkCanvas.Strokes.IndexOf(oldStroke);
        if (index < 0)
        {
            return;
        }

        _isNormalizingCollectedStroke = true;
        try
        {
            _nativeInkCanvas.Strokes[index] = newStroke;
            _nativeInkCanvas.InvalidateVisual();
        }
        finally
        {
            _isNormalizingCollectedStroke = false;
        }
    }

    private static bool AreSamePoints(IReadOnlyList<InkStylusPoint> left, IReadOnlyList<InkStylusPoint> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        for (var i = 0; i < left.Count; i++)
        {
            if (Math.Abs(left[i].X - right[i].X) > 0.001
                || Math.Abs(left[i].Y - right[i].Y) > 0.001
                || Math.Abs(left[i].Pressure - right[i].Pressure) > 0.001)
            {
                return false;
            }
        }

        return true;
    }

    private NativeStroke ToNativeStroke(InkStrokeSnapshot stroke)
    {
        var attributes = new NativeDrawingAttributes
        {
            Color = stroke.Color,
            Width = Math.Max(1d, stroke.Thickness),
            Height = Math.Max(1d, stroke.Thickness),
            IgnorePressure = Options.IgnorePressure,
            FitToCurve = true,
        };
        var points = new NativeStylusPointCollection(stroke.Points.Select(ToNativeStylusPoint));
        return new NativeStroke(points, attributes);
    }

    private static NativeStylusPoint ToNativeStylusPoint(InkStylusPoint point)
    {
        return new NativeStylusPoint(point.X, point.Y, Math.Clamp(point.Pressure, 0f, 1f));
    }

    private static InkStrokeSnapshot ToSnapshot(NativeStroke stroke)
    {
        var attributes = stroke.DrawingAttributes;
        var points = stroke.StylusPoints
            .Select(point => new InkStylusPoint(new Point2D(point.X, point.Y), point.PressureFactor))
            .ToList();
        var thickness = Math.Max(attributes.Width, attributes.Height);
        return new InkStrokeSnapshot(attributes.Color, thickness, points);
    }

}
