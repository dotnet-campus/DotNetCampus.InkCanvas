using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

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
        _avaloniaSkiaInkCanvas = avaloniaSkiaInkCanvas;
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
        }
    }

    private InkCanvasEditingMode _editingMode = InkCanvasEditingMode.Ink;

    private readonly AvaloniaSkiaInkCanvas _avaloniaSkiaInkCanvas;

    private AvaloniaSkiaInkCanvasEraserMode EraserMode => _eraserMode ??= new AvaloniaSkiaInkCanvasEraserMode(_avaloniaSkiaInkCanvas);

    private AvaloniaSkiaInkCanvasEraserMode? _eraserMode;

    #region Input

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        if (EditingMode == InkCanvasEditingMode.None)
        {
            return;
        }

        _inputDictionary[e.Pointer.Id] = new InputInfo();

        var args = ToArgs(e);

        if (EditingMode == InkCanvasEditingMode.Ink)
        {
            if (!IsDuringInput)
            {
                _avaloniaSkiaInkCanvas.WritingStart();
            }

            _avaloniaSkiaInkCanvas.WritingDown(in args);
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

        var args = ToArgs(e);
        if (EditingMode == InkCanvasEditingMode.Ink)
        {
            _avaloniaSkiaInkCanvas.WritingMove(in args);
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

        if (EditingMode == InkCanvasEditingMode.Ink)
        {
            _avaloniaSkiaInkCanvas.WritingUp(in args);

            if (!IsDuringInput)
            {
                _avaloniaSkiaInkCanvas.WritingCompleted();
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
    }

    private readonly Dictionary<int /*Id*/, InputInfo> _inputDictionary = [];
    private bool IsDuringInput => _inputDictionary.Count != 0;

    private InkingModeInputArgs ToArgs(PointerEventArgs args)
    {
        var currentPoint = args.GetCurrentPoint(_avaloniaSkiaInkCanvas);
        var inkStylusPoint = new InkStylusPoint(currentPoint.Position.ToPoint2D(), currentPoint.Properties.Pressure);

        IReadOnlyList<InkStylusPoint>? stylusPointList = null;
        var list = args.GetIntermediatePoints(_avaloniaSkiaInkCanvas);
        if (list.Count > 1)
        {
            stylusPointList = list.Select(t => new InkStylusPoint(t.Position.ToPoint2D(), t.Properties.Pressure))
                .ToList();
        }

        return new InkingModeInputArgs(args.Pointer.Id, inkStylusPoint, args.Timestamp)
        {
            StylusPointList = stylusPointList,
        };
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

