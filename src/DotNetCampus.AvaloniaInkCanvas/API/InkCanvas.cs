using Avalonia.Controls;

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
    }

    private AvaloniaSkiaInkCanvas _avaloniaSkiaInkCanvas;

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
}