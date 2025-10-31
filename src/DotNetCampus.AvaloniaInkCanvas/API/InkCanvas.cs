using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

using DotNetCampus.Inking.Contexts;
using DotNetCampus.Inking.Erasing;
using DotNetCampus.Inking.Interactives;
using DotNetCampus.Inking.Primitive;
using DotNetCampus.Inking.Utils;

namespace DotNetCampus.Inking;

public class InkCanvas : Control
{
    public InkCanvas()
    {
        var avaloniaSkiaInkCanvas = new AvaloniaSkiaInkCanvas()
        {
            IsHitTestVisible = false
        };
        AddChild(avaloniaSkiaInkCanvas);
        SkiaInkCanvas = avaloniaSkiaInkCanvas;
        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Stretch;
    }

    public InkCanvasEditingMode EditingMode
    {
        get => _editingMode;
        set
        {
            if (IsDuringInput)
            {
                throw new InvalidOperationException($"EditingMode should not be switched during the input process.");
            }

            _editingMode = value;
            EditingModeChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public event EventHandler? EditingModeChanged;

    public IReadOnlyList<SkiaStroke> Strokes => SkiaInkCanvas.StaticStrokeList;

    private InkCanvasEditingMode _editingMode = InkCanvasEditingMode.Ink;

    /// <summary>
    /// 为 Avalonia 实现的基于 Skia 的 InkCanvas 笔迹画布
    /// </summary>
    public AvaloniaSkiaInkCanvas SkiaInkCanvas { get; }

    private AvaloniaSkiaInkCanvasEraserMode EraserMode => SkiaInkCanvas.EraserMode;

    public event EventHandler<SkiaStrokeCollectionEventArgs>? StrokesCollected
    {
        add => SkiaInkCanvas.StrokesCollected += value;
        remove => SkiaInkCanvas.StrokesCollected -= value;
    }

    public event EventHandler<ErasingCompletedEventArgs>? StrokeErased
    {
        add => EraserMode.ErasingCompleted += value;
        remove => EraserMode.ErasingCompleted -= value;
    }

    #region Input

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        if (EditingMode == InkCanvasEditingMode.None)
        {
            return;
        }

        var args = ToArgs(e);
        _inputDictionary[e.Pointer.Id] = new InputInfo(args.StylusPoint);

        if (EditingMode == InkCanvasEditingMode.Ink)
        {
            if (!IsDuringInput)
            {
                SkiaInkCanvas.WritingStart();
            }

            SkiaInkCanvas.WritingDown(in args);
        }
        else if (EditingMode == InkCanvasEditingMode.EraseByPoint)
        {
            EraserMode.EraserDown(in args);
        }

        base.OnPointerPressed(e);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        if (EditingMode == InkCanvasEditingMode.None)
        {
            return;
        }

        if (!_inputDictionary.TryGetValue(e.Pointer.Id, out var inputInfo))
        {
            // Mouse? Not pressed yet.
            return;
        }

        var args = ToArgs(e, inputInfo);
        inputInfo.LastStylusPoint = args.StylusPoint;

        if (EditingMode == InkCanvasEditingMode.Ink)
        {
            SkiaInkCanvas.WritingMove(in args);
        }
        else if (EditingMode == InkCanvasEditingMode.EraseByPoint)
        {
            EraserMode.EraserMove(in args);
        }

        base.OnPointerMoved(e);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        if (EditingMode == InkCanvasEditingMode.None)
        {
            return;
        }

        if (!_inputDictionary.Remove(e.Pointer.Id, out var inputInfo))
        {
            // Mouse? Not pressed yet.
            return;
        }

        var args = ToArgs(e);
        inputInfo.LastStylusPoint = args.StylusPoint;

        if (EditingMode == InkCanvasEditingMode.Ink)
        {
            SkiaInkCanvas.WritingUp(in args);

            if (!IsDuringInput)
            {
                SkiaInkCanvas.WritingCompleted();
            }
        }
        else if (EditingMode == InkCanvasEditingMode.EraseByPoint)
        {
            EraserMode.EraserUp(in args);
        }

        base.OnPointerReleased(e);
    }

    class InputInfo
    {
        public InputInfo(InkStylusPoint lastStylusPoint)
        {
            LastStylusPoint = lastStylusPoint;
        }

        public InkStylusPoint LastStylusPoint { get; set; }
    }

    private readonly Dictionary<int /*Id*/, InputInfo> _inputDictionary = [];
    private bool IsDuringInput => _inputDictionary.Count != 0;

    private InkingModeInputArgs ToArgs(PointerEventArgs args, InputInfo? inputInfo = null)
    {
        PointerPoint currentPoint = args.GetCurrentPoint(SkiaInkCanvas);
        var inkStylusPoint = ToInkStylusPoint(currentPoint);

        IReadOnlyList<InkStylusPoint>? stylusPointList = null;
        var list = args.GetIntermediatePoints(SkiaInkCanvas);
        if (list.Count > 1)
        {
            stylusPointList = list.Select(ToInkStylusPoint)
                .ToList();
        }

        return new InkingModeInputArgs(args.Pointer.Id, inkStylusPoint, args.Timestamp)
        {
            StylusPointList = stylusPointList,
        };

        InkStylusPoint ToInkStylusPoint(PointerPoint point)
        {
            var pressure = EnsurePressure(currentPoint.Properties.Pressure);
            var contactRect = currentPoint.Properties.ContactRect;
            var width = contactRect.Width;
            var height = contactRect.Height;

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

            var stylusPoint = new InkStylusPoint(currentPoint.Position.ToPoint2D(), pressure)
            {
                Width = width != 0 ? width : null,
                Height = height != 0 ? height : null,
            };

            return stylusPoint;
        }

        float EnsurePressure(float pressure)
        {
            // 这是一个修复补丁。在 Linux X11 上，如果前后两个点的压力是相同的，则后点将不会报告压力，此时 Avalonia 上将使用默认压力值 0.5 来填充压力值
            // 为了避免压力值抖动，将压力值修正为上一个点的压力值
            const float defaultPressure = InkStylusPoint.DefaultPressure;
            if (inputInfo != null && (pressure == 0 || Math.Abs(pressure - defaultPressure) < 0.00001))
            {
                return inputInfo.LastStylusPoint.Pressure;
            }

            return pressure;
        }
    }

    #endregion

    internal void AddChild(Control childControl)
    {
        LogicalChildren.Add(childControl);
        VisualChildren.Add(childControl);
    }

    internal void RemoveChild(Control childControl)
    {
        LogicalChildren.Remove(childControl);
        VisualChildren.Remove(childControl);
    }

    protected override Size MeasureCore(Size availableSize)
    {
        var width = availableSize.Width;
        var height = availableSize.Height;

        if (double.IsInfinity(width))
        {
            width = 0;
        }

        if (double.IsInfinity(height))
        {
            height = 0;
        }

        base.MeasureCore(availableSize);
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var size = base.ArrangeOverride(finalSize);

        return size;
    }

    public override void Render(DrawingContext context)
    {
        // to enable hit testing
        context.DrawRectangle(Brushes.Transparent, null, new Rect(new Point(), Bounds.Size));
    }


}

