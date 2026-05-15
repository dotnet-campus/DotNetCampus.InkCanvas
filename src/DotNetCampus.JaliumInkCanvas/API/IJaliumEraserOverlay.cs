using Jalium.UI;
using Jalium.UI.Media;

namespace DotNetCampus.Inking;

public interface IJaliumEraserOverlay
{
    void Update(Rect eraserRect);

    void Render(DrawingContext drawingContext);
}
