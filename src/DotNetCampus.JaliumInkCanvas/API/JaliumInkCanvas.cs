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

    private readonly IStrokeRenderer _nativeRenderer = new NativeIncrementalStrokeRenderer();
    private readonly IStrokeRenderer _skiaFallbackRenderer = new SkiaFallbackStrokeRenderer();
    private readonly RenderBackendSelector _backendSelector;
    private IStrokeRenderer _strokeRenderer;

    private double _inkThickness = 6d;
    private Color _inkColor = Color.Red;

    public JaliumInkCanvas()
    {
        _backendSelector = new RenderBackendSelector(() => true);
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

    public string ActiveRendererName => _strokeRenderer.Name;

    public JaliumInkCanvasPerformanceSnapshot GetPerformanceSnapshot()
    {
        var dynamicPointCount = _dynamicStrokes.Values.Sum(s => s.Points.Count);
        return _performanceCounter.Snapshot(
            _committedLayerCache.StrokeCount,
            dynamicPointCount,
            _committedLayerCache.CacheHitCount,
            _committedLayerCache.CacheMissCount);
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
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        if (Options.EnableCommittedLayerCache)
        {
            _committedLayerCache.Draw(drawingContext);
        }

        foreach (var stroke in _dynamicStrokes.Values)
        {
            drawingContext.DrawGeometry(stroke.Brush, null, stroke.Geometry);
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
        _activeInputs[e.Pointer.PointerId] = new InputInfo(args.StylusPoint);

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
            var smoothedPoints = ApplySmoothing(points, stroke.Points.LastOrDefault().Pressure);

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
            sw.Stop();

            _performanceCounter.AddInputPoints(smoothedPoints.Count);
            _performanceCounter.AddCommitUpdate(sw.Elapsed.TotalMilliseconds);
        }
        else if (EditingMode == InkCanvasEditingMode.EraseByPoint)
        {
            EraseAt(args.StylusPoint.Point);
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
        _committedLayerCache.RemoveWhere(stroke => _strokeRenderer.HitTest(stroke, hitPoint));
        sw.Stop();
        _performanceCounter.AddErase(sw.Elapsed.TotalMilliseconds);
    }

    private IEnumerable<InkStylusPoint> ExpandPoints(InkingModeInputArgs args)
    {
        if (args.StylusPointList is { Count: > 0 })
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
        var stylusPoint = ToInkStylusPoint(currentPoint, inputInfo);
        var list = args.GetIntermediatePoints(this);
        IReadOnlyList<InkStylusPoint>? stylusPointList = null;
        if (list.Count > 1)
        {
            stylusPointList = list.Select(p => ToInkStylusPoint(p, inputInfo)).ToList();
        }

        return new InkingModeInputArgs((int)args.Pointer.PointerId, stylusPoint, currentPoint.Timestamp)
        {
            IsMouse = currentPoint.PointerDeviceType == PointerDeviceType.Mouse,
            StylusPointList = stylusPointList,
        };
    }

    private static InkStylusPoint ToInkStylusPoint(PointerPoint point, InputInfo? inputInfo)
    {
        var pressure = EnsurePressure(point.Properties, inputInfo);
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

        return new InkStylusPoint(point.Position.X, point.Position.Y, pressure)
        {
            Width = width != 0 ? width : null,
            Height = height != 0 ? height : null,
        };
    }

    private static float EnsurePressure(PointerPointProperties properties, InputInfo? inputInfo)
    {
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

    private sealed class InputInfo(InkStylusPoint lastStylusPoint)
    {
        public InkStylusPoint LastStylusPoint { get; set; } = lastStylusPoint;
    }
}
