using Jalium.UI;
using Jalium.UI.Media;

namespace DotNetCampus.Inking;

public sealed class DefaultJaliumEraserOverlay : IJaliumEraserOverlay
{
    private readonly SolidColorBrush _fillBrush = new(Color.FromRgb(255, 84, 84));
    private Rect _eraserRect;

    public void Update(Rect eraserRect)
    {
        _eraserRect = eraserRect;
    }

    public void Render(DrawingContext drawingContext)
    {
        var geometry = new RectangleGeometry(_eraserRect, 4, 4);
        drawingContext.DrawGeometry(_fillBrush, null, geometry);
    }
}
