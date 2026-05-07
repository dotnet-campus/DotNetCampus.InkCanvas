using DotNetCampus.Inking.Primitive;
using Jalium.UI;
using Jalium.UI.Media;

namespace DotNetCampus.Inking.Model;

internal sealed class StrokeData
{
    public required SolidColorBrush Brush { get; init; }

    public required PathGeometry Geometry { get; init; }

    public required List<InkStylusPoint> Points { get; init; }

    public Rect Bounds => Geometry.Bounds;
}
