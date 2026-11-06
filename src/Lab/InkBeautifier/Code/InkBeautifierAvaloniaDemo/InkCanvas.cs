using System.Globalization;
using System.Xml.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using InkBeautifierLib.Configuration;
using InkBeautifierLib.Geometry;
using InkBeautifierLib.Models;
using InkBeautifierLib.Processing;

namespace InkBeautifierAvaloniaDemo;

/// <summary>
/// 提供实时笔迹输入和美化轮廓绘制。
/// </summary>
public sealed class InkCanvas : Control
{
    public static readonly StyledProperty<double> MaximumWidthProperty =
        AvaloniaProperty.Register<InkCanvas, double>(nameof(MaximumWidth), 12);

    public static readonly StyledProperty<double> VelocityResponseProperty =
        AvaloniaProperty.Register<InkCanvas, double>(nameof(VelocityResponse), 2);

    private readonly Dictionary<long, IReadOnlyList<IReadOnlyList<InkPoint2D>>> _outlines = [];
    private readonly HashSet<long> _activeStrokeIds = [];
    private readonly Dictionary<long, List<InkInputPoint>> _recordedStrokes = [];
    private readonly List<long> _recordedStrokeOrder = [];
    private InkBeautifier _beautifier = new();
    private double _configuredMaximumWidth = 12;
    private double _configuredVelocityResponse = 2;
    private long _nextStrokeId;
    private long? _activeStrokeId;

    public InkCanvas()
    {
        ClipToBounds = true;
        PointerPressed += OnPointerPressed;
        PointerMoved += OnPointerMoved;
        PointerReleased += OnPointerReleased;
        PointerCaptureLost += OnPointerCaptureLost;
    }

    /// <summary>
    /// 获取或设置最大笔宽。
    /// </summary>
    public double MaximumWidth
    {
        get => GetValue(MaximumWidthProperty);
        set => SetValue(MaximumWidthProperty, value);
    }

    /// <summary>
    /// 获取或设置速度对笔宽的衰减强度。
    /// </summary>
    public double VelocityResponse
    {
        get => GetValue(VelocityResponseProperty);
        set => SetValue(VelocityResponseProperty, value);
    }

    /// <summary>
    /// 获取或设置绘制轮廓时应用的坐标变换。
    /// </summary>
    public Matrix OutlineTransform { get; set; } = Matrix.Identity;

    /// <summary>
    /// 清空画板上的全部笔迹。
    /// </summary>
    public void Clear()
    {
        _outlines.Clear();
        _beautifier.Reset();
        _activeStrokeIds.Clear();
        _recordedStrokes.Clear();
        _recordedStrokeOrder.Clear();
        _activeStrokeId = null;
        InvalidateVisual();
    }

    /// <summary>
    /// 获取当前画板是否包含可导出的原始输入。
    /// </summary>
    public bool HasRecordedInk => _recordedStrokeOrder.Count > 0;

    /// <summary>
    /// 将当前画板记录的原始输入导出为可重放的笔迹数据。
    /// </summary>
    public void ExportProblemInk(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var root = new XElement("ArrayOfArrayOfInkDataModel");
        foreach (long strokeId in _recordedStrokeOrder)
        {
            List<InkInputPoint> stroke = _recordedStrokes[strokeId];
            long firstTimestamp = stroke[0].Timestamp;
            root.Add
            (
                new XElement
                (
                    "ArrayOfInkDataModel",
                    stroke.Select
                    (point => new XElement
                        (
                            "InkDataModel",
                            new XElement
                            (
                                "Time",
                                System.Xml.XmlConvert.ToString
                                    (TimeSpan.FromMilliseconds(point.Timestamp - firstTimestamp))
                            ),
                            new XElement("X", point.Position.X.ToString("R", CultureInfo.InvariantCulture)),
                            new XElement("Y", point.Position.Y.ToString("R", CultureInfo.InvariantCulture)),
                            new XElement
                            (
                                "PressureFactor",
                                (point.Pressure * 1024).ToString("R", CultureInfo.InvariantCulture)
                            ),
                            new XElement("Status", point.Status)
                        )
                    )
                )
            );
        }

        new XDocument(new XDeclaration("1.0", "utf-8", null), root).Save(stream);
    }

    /// <summary>
    /// 以编程方式绘制一个笔迹点，供回放和测试使用。
    /// </summary>
    public InkBeautifierResult AddPoint(InkInputPoint point)
    {
        RecordInputPoint(point);
        if (point.Status == InkPointerStatus.Down)
        {
            RecreateBeautifierIfNeeded();
        }

        InkBeautifierResult result = _beautifier.Process(point);
        if (point.Status == InkPointerStatus.Down && result.WasAccepted)
        {
            _activeStrokeIds.Add(point.StrokeId);
        }
        else if (point.Status == InkPointerStatus.Up)
        {
            _activeStrokeIds.Remove(point.StrokeId);
        }

        if (result.Outlines.Count > 0)
        {
            _outlines[result.StrokeId] = result.Outlines;
            InvalidateVisual();
        }

        return result;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        context.FillRectangle(Brushes.White, new Rect(Bounds.Size));

        foreach (IReadOnlyList<IReadOnlyList<InkPoint2D>> outlines in _outlines.Values)
        {
            var geometry = new StreamGeometry();
            using (StreamGeometryContext geometryContext = geometry.Open())
            {
                geometryContext.SetFillRule(FillRule.NonZero);
                foreach (IReadOnlyList<InkPoint2D> outline in outlines)
                {
                    if (outline.Count < 3)
                    {
                        continue;
                    }

                    geometryContext.BeginFigure(ToPoint(outline[0]), true);
                    for (int index = 1; index < outline.Count; index++)
                    {
                        geometryContext.LineTo(ToPoint(outline[index]));
                    }

                    geometryContext.EndFigure(true);
                }
            }

            context.DrawGeometry(new SolidColorBrush(Color.FromRgb(24, 32, 48)), null, geometry);
        }
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        PointerPoint point = e.GetCurrentPoint(this);
        if (!point.Properties.IsLeftButtonPressed && e.Pointer.Type == PointerType.Mouse)
        {
            return;
        }

        RecreateBeautifierIfNeeded();
        long strokeId = ++_nextStrokeId;
        _activeStrokeId = strokeId;
        e.Pointer.Capture(this);
        AddPoint(CreateInputPoint(e, strokeId, InkPointerStatus.Down));
        e.Handled = true;
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_activeStrokeId is not long strokeId || e.Pointer.Captured != this)
        {
            return;
        }

        AddPoint(CreateInputPoint(e, strokeId, InkPointerStatus.Move));
        e.Handled = true;
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_activeStrokeId is not long strokeId)
        {
            return;
        }

        AddPoint(CreateInputPoint(e, strokeId, InkPointerStatus.Up));
        _activeStrokeId = null;
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    private void OnPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e) => _activeStrokeId = null;

    private InkInputPoint CreateInputPoint(PointerEventArgs e, long strokeId, InkPointerStatus status)
    {
        PointerPoint point = e.GetCurrentPoint(this);
        double pressure = point.Properties.Pressure;
        if (pressure <= 0)
        {
            pressure = 0.5;
        }

        return new InkInputPoint
        (
            new InkPoint2D(point.Position.X, point.Position.Y),
            Environment.TickCount64,
            pressure,
            strokeId,
            status
        );
    }

    private void RecordInputPoint(InkInputPoint point)
    {
        if (point.Status == InkPointerStatus.Down)
        {
            _recordedStrokes[point.StrokeId] = [point];
            _recordedStrokeOrder.Remove(point.StrokeId);
            _recordedStrokeOrder.Add(point.StrokeId);
            return;
        }

        if (_recordedStrokes.TryGetValue(point.StrokeId, out List<InkInputPoint>? stroke))
        {
            stroke.Add(point);
        }
    }

    private void RecreateBeautifierIfNeeded()
    {
        if (_activeStrokeIds.Count > 0 ||
            (_configuredMaximumWidth == MaximumWidth && _configuredVelocityResponse == VelocityResponse))
        {
            return;
        }

        _beautifier = new InkBeautifier
            (new InkBeautifierOptions { MaximumWidth = MaximumWidth, VelocityResponse = VelocityResponse });
        _configuredMaximumWidth = MaximumWidth;
        _configuredVelocityResponse = VelocityResponse;
    }

    private Point ToPoint(InkPoint2D point) => OutlineTransform.Transform(new Point(point.X, point.Y));
}
