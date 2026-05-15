using System.Diagnostics;
using DotNetCampus.Inking.Caching;
using DotNetCampus.Inking.Interactives;
using DotNetCampus.Inking.Metrics;
using DotNetCampus.Inking.Model;
using DotNetCampus.Inking.Primitive;
using DotNetCampus.Inking.Rendering;
using DotNetCampus.Inking.Smoothing;
using DotNetCampus.Numerics.Geometry;
using Jalium.UI;
using Jalium.UI.Controls;
using Jalium.UI.Input;
using Jalium.UI.Media;

namespace DotNetCampus.Inking;

public class JaliumInkCanvas : FrameworkElement
{
    private readonly Dictionary<uint, InputInfo> _activeInputs = [];
    private readonly Dictionary<uint, StrokeData> _dynamicStrokes = [];
    private readonly JaliumCommittedLayerCache _committedLayerCache = new();
    private readonly PressureSmoother _pressureSmoother = new();
    private readonly InkPerformanceCounter _performanceCounter = new();
    private readonly Stopwatch _eraserInputStopwatch = new();

    private readonly IStrokeRenderer _nativeRenderer = new NativeIncrementalStrokeRenderer();
    private readonly IStrokeRenderer _skiaFallbackRenderer = new SkiaFallbackStrokeRenderer();
    private readonly RenderBackendSelector _backendSelector;
    private IStrokeRenderer _strokeRenderer;

    private double _inkThickness = 6d;
    private Color _inkColor = Color.Red;
    private uint? _mainEraserPointerId;
    private double _lastEraserSize;
    private IJaliumEraserOverlay? _eraserOverlay;
    private Rect? _eraserOverlayRect;

    public JaliumInkCanvas()
    {
        _backendSelector = new RenderBackendSelector(JaliumDx12AvailabilityProbe.IsAvailable);
        _strokeRenderer = _backendSelector.Select(Options.BackendMode, _nativeRenderer, _skiaFallbackRenderer);

        AddHandler(UIElement.PointerPressedEvent, new PointerPressedEventHandler(OnPointerPressed));
        AddHandler(UIElement.PointerMovedEvent, new PointerMovedEventHandler(OnPointerMoved));
        AddHandler(UIElement.PointerReleasedEvent, new PointerReleasedEventHandler(OnPointerReleased));
    }

    public InkCanvasEditingMode EditingMode { get; set; } = InkCanvasEditingMode.Ink;

    public JaliumInkCanvasRenderingOptions Options { get; } = new();

    public Color InkColor
    {
        get => _inkColor;
        set
        {
            _inkColor = value;
            InvalidateVisual();
        }
    }

    public double InkThickness
    {
        get => _inkThickness;
        set
        {
            _inkThickness = Math.Max(1d, value);
            InvalidateVisual();
        }
    }

    public int StrokeCount => _committedLayerCache.StrokeCount;

    public IReadOnlyList<InkStrokeSnapshot> Strokes => GetStrokeSnapshots();

    public string ActiveRendererName => _strokeRenderer.Name;

    public bool IsNativeBackendAvailable => _backendSelector.LastNativeAvailable;

    public event Action<InkStrokeSnapshot>? StrokeCollected;

    public event Action<IReadOnlyList<InkStrokeSnapshot>>? StrokeErased;

    public JaliumInkCanvasPerformanceSnapshot GetPerformanceSnapshot()
    {
        var dynamicPointCount = _dynamicStrokes.Values.Sum(s => s.Points.Count);
        return _performanceCounter.Snapshot(
            _committedLayerCache.StrokeCount,
            dynamicPointCount,
            _committedLayerCache.CacheHitCount,
            _committedLayerCache.CacheMissCount);
    }

    public IReadOnlyList<InkStrokeSnapshot> GetStrokeSnapshots()
    {
        var snapshots = _committedLayerCache.GetStrokeSnapshots().ToList();
        foreach (var stroke in _dynamicStrokes.Values)
        {
            snapshots.Add(new InkStrokeSnapshot(stroke.Brush.Color, stroke.Thickness, stroke.Points.ToList()));
        }

        return snapshots;
    }

    public void RefreshRenderer()
    {
        _strokeRenderer = _backendSelector.Select(Options.BackendMode, _nativeRenderer, _skiaFallbackRenderer);
        InvalidateVisual();
    }

    public void Clear()
    {
        _activeInputs.Clear();
        _dynamicStrokes.Clear();
        _committedLayerCache.Reset();
        _mainEraserPointerId = null;
        _eraserOverlayRect = null;
        InvalidateVisual();
    }

    public void AddStroke(InkStrokeSnapshot stroke)
    {
        if (stroke.Points.Count == 0)
        {
            return;
        }

        var data = CreateStrokeData(stroke.Color, stroke.Thickness, stroke.Points);
        _committedLayerCache.Add(data);
        InvalidateVisual();
    }

    public void AddStrokes(IEnumerable<InkStrokeSnapshot> strokes)
    {
        var hasChanged = false;
        foreach (var stroke in strokes)
        {
            if (stroke.Points.Count == 0)
            {
                continue;
            }

            var data = CreateStrokeData(stroke.Color, stroke.Thickness, stroke.Points);
            _committedLayerCache.Add(data);
            hasChanged = true;
        }

        if (hasChanged)
        {
            InvalidateVisual();
        }
    }

    public int RemoveStrokes(Predicate<InkStrokeSnapshot> predicate)
    {
        var removed = _committedLayerCache.ReplaceWhere(stroke => predicate(ToSnapshot(stroke)), _ => Array.Empty<StrokeData>());
        if (removed.Count == 0)
        {
            return 0;
        }

        InvalidateVisual();
        return removed.Count;
    }

    protected override void OnRender(object? drawingContextObject)
    {
        if (drawingContextObject is not DrawingContext drawingContext)
        {
            return;
        }

        base.OnRender(drawingContextObject);

        if (Options.EnableCommittedLayerCache)
        {
            _committedLayerCache.Draw(drawingContext);
        }

        foreach (var stroke in _dynamicStrokes.Values)
        {
            drawingContext.DrawGeometry(stroke.Brush, null, stroke.Geometry);
        }

        if (Options.ShowEraserOverlay
            && EditingMode == InkCanvasEditingMode.EraseByPath
            && _eraserOverlayRect is { } eraserRect)
        {
            var overlay = _eraserOverlay ??= (Options.EraserOverlayCreator?.Invoke() ?? new DefaultJaliumEraserOverlay());
            overlay.Update(eraserRect);
            overlay.Render(drawingContext);
        }

        if (Options.EnablePerformanceCounters)
        {
            _performanceCounter.OnFramePresented();
        }
    }

    private void OnPointerPressed(object sender, PointerPressedEventArgs e)
    {
        if (EditingMode == InkCanvasEditingMode.None)
        {
            return;
        }

        var args = ToInputArgs(e);
        _activeInputs[e.Pointer.PointerId] = new InputInfo(args.StylusPoint, Options.IgnorePressure);

        if (EditingMode == InkCanvasEditingMode.Ink)
        {
            var points = ApplySmoothing([args.StylusPoint], null);
            var stroke = _strokeRenderer.CreateStroke(_inkColor, _inkThickness, points[0]);
            _dynamicStrokes[e.Pointer.PointerId] = stroke;
            _performanceCounter.AddInputPoints(points.Count);
        }
        else if (EditingMode == InkCanvasEditingMode.EraseByPoint)
        {
            EraseAt(args.StylusPoint.Point);
        }
        else if (EditingMode == InkCanvasEditingMode.EraseByPath)
        {
            _mainEraserPointerId ??= e.Pointer.PointerId;
            if (_mainEraserPointerId == e.Pointer.PointerId)
            {
                _eraserInputStopwatch.Restart();
                _lastEraserSize = Math.Max(1d, Options.EraserSize);
                EraseByEraserRect(args.StylusPoint);
                UpdateEraserOverlay(args.StylusPoint);
            }
        }

        InvalidateVisual();
    }

    private void OnPointerMoved(object sender, PointerMovedEventArgs e)
    {
        if (EditingMode == InkCanvasEditingMode.None || !_activeInputs.TryGetValue(e.Pointer.PointerId, out var inputInfo))
        {
            return;
        }

        var args = ToInputArgs(e, inputInfo);
        inputInfo.LastStylusPoint = args.StylusPoint;

        if (EditingMode == InkCanvasEditingMode.Ink)
        {
            if (!_dynamicStrokes.TryGetValue(e.Pointer.PointerId, out var stroke))
            {
                return;
            }

            var points = ExpandPoints(args).ToList();
            var filteredPoints = FilterPointsByDistance(points, stroke.Points[^1]);
            if (filteredPoints.Count == 0)
            {
                return;
            }

            var smoothedPoints = ApplySmoothing(filteredPoints, stroke.Points.LastOrDefault().Pressure);

            var sw = Stopwatch.StartNew();
            _strokeRenderer.AppendPoints(stroke, smoothedPoints, _inkThickness);
            sw.Stop();

            _performanceCounter.AddInputPoints(smoothedPoints.Count);
            _performanceCounter.AddDynamicRebuild(sw.Elapsed.TotalMilliseconds);
        }
        else if (EditingMode == InkCanvasEditingMode.EraseByPoint)
        {
            EraseAt(args.StylusPoint.Point);
        }
        else if (EditingMode == InkCanvasEditingMode.EraseByPath)
        {
            if (_mainEraserPointerId == e.Pointer.PointerId)
            {
                foreach (var point in ExpandPoints(args))
                {
                    EraseByEraserRect(point);
                    UpdateEraserOverlay(point);
                }
            }
        }

        InvalidateVisual();
    }

    private void OnPointerReleased(object sender, PointerReleasedEventArgs e)
    {
        if (EditingMode == InkCanvasEditingMode.None || !_activeInputs.Remove(e.Pointer.PointerId, out var inputInfo))
        {
            return;
        }

        var args = ToInputArgs(e, inputInfo);

        if (EditingMode == InkCanvasEditingMode.Ink && _dynamicStrokes.TryGetValue(e.Pointer.PointerId, out var stroke))
        {
            var smoothedPoints = ApplySmoothing([args.StylusPoint], stroke.Points.LastOrDefault().Pressure);
            var sw = Stopwatch.StartNew();
            _strokeRenderer.AppendPoints(stroke, smoothedPoints, _inkThickness);
            _dynamicStrokes.Remove(e.Pointer.PointerId);
            _committedLayerCache.Add(stroke);
            StrokeCollected?.Invoke(new InkStrokeSnapshot(stroke.Brush.Color, stroke.Thickness, stroke.Points.ToList()));
            sw.Stop();

            _performanceCounter.AddInputPoints(smoothedPoints.Count);
            _performanceCounter.AddCommitUpdate(sw.Elapsed.TotalMilliseconds);
        }
        else if (EditingMode == InkCanvasEditingMode.EraseByPoint)
        {
            EraseAt(args.StylusPoint.Point);
        }
        else if (EditingMode == InkCanvasEditingMode.EraseByPath && _mainEraserPointerId == e.Pointer.PointerId)
        {
            EraseByEraserRect(args.StylusPoint);
            _mainEraserPointerId = null;
            _eraserOverlayRect = null;
        }

        InvalidateVisual();
    }

    private IReadOnlyList<InkStylusPoint> ApplySmoothing(IReadOnlyList<InkStylusPoint> points, float? previousPressure)
    {
        if (!Options.EnablePressureSmoothing)
        {
            return points;
        }

        return _pressureSmoother.Smooth(points, Options.PressureSmoothingFactor, previousPressure);
    }

    private void EraseAt(Point2D point)
    {
        var sw = Stopwatch.StartNew();
        var hitPoint = new Jalium.UI.Point(point.X, point.Y);
        var erasedStrokes = _committedLayerCache.RemoveWhere(stroke => _strokeRenderer.HitTest(stroke, hitPoint));

        var dynamicErased = new List<InkStrokeSnapshot>();
        var dynamicKeys = _dynamicStrokes
            .Where(pair => _strokeRenderer.HitTest(pair.Value, hitPoint))
            .Select(pair => pair.Key)
            .ToList();
        foreach (var key in dynamicKeys)
        {
            var stroke = _dynamicStrokes[key];
            dynamicErased.Add(new InkStrokeSnapshot(stroke.Brush.Color, stroke.Thickness, stroke.Points.ToList()));
            _dynamicStrokes.Remove(key);
        }

        sw.Stop();
        _performanceCounter.AddErase(sw.Elapsed.TotalMilliseconds);

        RaiseStrokeErased(erasedStrokes, dynamicErased);
    }

    private void EraseByEraserRect(InkStylusPoint point)
    {
        var size = ResolveEraserSize(point);
        var half = size / 2;
        var rect = new Rect(point.X - half, point.Y - half, size, size);

        var sw = Stopwatch.StartNew();
        var erasedStrokes = _committedLayerCache.ReplaceWhere(
            stroke => stroke.Bounds.IntersectsWith(rect),
            stroke => SplitStrokeByRect(stroke, rect));

        var dynamicErased = new List<InkStrokeSnapshot>();
        var dynamicReplacement = new List<(uint Key, IReadOnlyList<StrokeData> Replacement)>();
        foreach (var pair in _dynamicStrokes)
        {
            if (!pair.Value.Bounds.IntersectsWith(rect))
            {
                continue;
            }

            var replacement = SplitStrokeByRect(pair.Value, rect);
            if (replacement.Count == 1 && ReferenceEquals(replacement[0], pair.Value))
            {
                continue;
            }

            dynamicErased.Add(new InkStrokeSnapshot(pair.Value.Brush.Color, pair.Value.Thickness, pair.Value.Points.ToList()));
            dynamicReplacement.Add((pair.Key, replacement));
        }

        foreach (var (key, replacement) in dynamicReplacement)
        {
            _dynamicStrokes.Remove(key);
            if (replacement.Count == 0)
            {
                continue;
            }

            _dynamicStrokes[key] = replacement[0];
            for (var i = 1; i < replacement.Count; i++)
            {
                _committedLayerCache.Add(replacement[i]);
            }
        }

        sw.Stop();
        _performanceCounter.AddErase(sw.Elapsed.TotalMilliseconds);

        RaiseStrokeErased(erasedStrokes, dynamicErased);
    }

    private double ResolveEraserSize(InkStylusPoint point)
    {
        var size = _lastEraserSize > 0 ? _lastEraserSize : Math.Max(1d, Options.EraserSize);

        if (Options.EnableStylusSizeAsEraserSize)
        {
            var touchWidth = point.Width ?? size;
            var touchHeight = point.Height ?? size;
            var touchSize = Math.Max(touchWidth, touchHeight);
            var allowResize = Options.CanEraserAlwaysFollowsTouchSize || _eraserInputStopwatch.Elapsed < Options.EraserCanResizeDuringTimeSpan;
            if (allowResize)
            {
                size = touchSize;
            }
        }

        if (Options.LockMinEraserSize)
        {
            size = Math.Max(size, Math.Max(1d, Options.MinEraserSize));
        }

        size = Math.Min(size, Math.Max(1d, Options.MaxEraserSize));
        _lastEraserSize = size;
        return size;
    }

    private IReadOnlyList<StrokeData> SplitStrokeByRect(StrokeData stroke, Rect rect)
    {
        if (stroke.Points.Count == 0)
        {
            return [stroke];
        }

        var segments = new List<List<InkStylusPoint>>();
        List<InkStylusPoint>? current = null;
        foreach (var p in stroke.Points)
        {
            var inside = p.X >= rect.X && p.X <= rect.X + rect.Width && p.Y >= rect.Y && p.Y <= rect.Y + rect.Height;
            if (inside)
            {
                if (current is { Count: > 1 })
                {
                    segments.Add(current);
                }

                current = null;
                continue;
            }

            current ??= new List<InkStylusPoint>();
            current.Add(p);
        }

        if (current is { Count: > 1 })
        {
            segments.Add(current);
        }

        if (segments.Count == 0)
        {
            return Array.Empty<StrokeData>();
        }

        if (segments.Count == 1 && segments[0].Count == stroke.Points.Count)
        {
            return [stroke];
        }

        return segments.Select(segment => CreateStrokeData(stroke.Brush.Color, stroke.Thickness, segment)).ToList();
    }

    private StrokeData CreateStrokeData(Color color, double thickness, IReadOnlyList<InkStylusPoint> points)
    {
        var stroke = _strokeRenderer.CreateStroke(color, thickness, points[0]);
        if (points.Count > 1)
        {
            _strokeRenderer.AppendPoints(stroke, points.Skip(1).ToList(), thickness);
        }

        return stroke;
    }

    private void RaiseStrokeErased(IReadOnlyList<InkStrokeSnapshot> erasedStrokes, IReadOnlyList<InkStrokeSnapshot> dynamicErased)
    {
        if (erasedStrokes.Count == 0 && dynamicErased.Count == 0)
        {
            return;
        }

        var allErased = new List<InkStrokeSnapshot>(erasedStrokes.Count + dynamicErased.Count);
        allErased.AddRange(erasedStrokes);
        allErased.AddRange(dynamicErased);
        StrokeErased?.Invoke(allErased);
    }

    private void UpdateEraserOverlay(InkStylusPoint point)
    {
        var size = Math.Max(1d, _lastEraserSize);
        var half = size / 2;
        _eraserOverlayRect = new Rect(point.X - half, point.Y - half, size, size);
    }

    private static InkStrokeSnapshot ToSnapshot(StrokeData stroke)
    {
        return new InkStrokeSnapshot(stroke.Brush.Color, stroke.Thickness, stroke.Points.ToList());
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

    private IEnumerable<InkStylusPoint> ExpandPoints(InkingModeInputArgs args)
    {
        if (Options.EnableIntermediatePoints && args.StylusPointList is { Count: > 0 })
        {
            foreach (var point in args.StylusPointList)
            {
                yield return point;
            }
            yield break;
        }

        yield return args.StylusPoint;
    }

    private InkingModeInputArgs ToInputArgs(PointerEventArgs args, InputInfo? inputInfo = null)
    {
        var currentPoint = args.GetCurrentPoint(this);
        var stylusPoint = ToInkStylusPoint(currentPoint, NormalizeToLocalPosition(currentPoint.Position), inputInfo);
        var list = args.GetIntermediatePoints(this);
        IReadOnlyList<InkStylusPoint>? stylusPointList = null;
        if (list.Count > 1)
        {
            stylusPointList = list
                .Select(p => ToInkStylusPoint(p, NormalizeToLocalPosition(p.Position), inputInfo))
                .ToList();
        }

        return new InkingModeInputArgs((int)args.Pointer.PointerId, stylusPoint, currentPoint.Timestamp)
        {
            IsMouse = currentPoint.PointerDeviceType == PointerDeviceType.Mouse,
            StylusPointList = stylusPointList,
        };
    }

    private Jalium.UI.Point NormalizeToLocalPosition(Jalium.UI.Point rawPosition)
    {
        return rawPosition;
    }

    private static InkStylusPoint ToInkStylusPoint(PointerPoint point, Jalium.UI.Point localPosition, InputInfo? inputInfo)
    {
        var pressure = EnsurePressure(point.Properties, inputInfo, inputInfo?.IgnorePressure ?? false);
        var width = point.Properties.ContactRect.Width;
        var height = point.Properties.ContactRect.Height;

        if (inputInfo is not null)
        {
            if (width == 0 && inputInfo.LastStylusPoint.Width is { } lastWidth)
            {
                width = lastWidth;
            }

            if (height == 0 && inputInfo.LastStylusPoint.Height is { } lastHeight)
            {
                height = lastHeight;
            }
        }

        return new InkStylusPoint(localPosition.X, localPosition.Y, pressure)
        {
            Width = width != 0 ? width : null,
            Height = height != 0 ? height : null,
        };
    }

    private static float EnsurePressure(PointerPointProperties properties, InputInfo? inputInfo, bool ignorePressure)
    {
        if (ignorePressure)
        {
            return InkStylusPoint.DefaultPressure;
        }

        if (!properties.HasPressure && inputInfo is not null)
        {
            return inputInfo.LastStylusPoint.Pressure;
        }

        var pressure = properties.Pressure;
        if (pressure <= 0 && inputInfo is not null)
        {
            return inputInfo.LastStylusPoint.Pressure;
        }

        return pressure > 0 ? pressure : InkStylusPoint.DefaultPressure;
    }

    private sealed class InputInfo(InkStylusPoint lastStylusPoint, bool ignorePressure)
    {
        public InkStylusPoint LastStylusPoint { get; set; } = lastStylusPoint;

        public bool IgnorePressure { get; } = ignorePressure;
    }
}
