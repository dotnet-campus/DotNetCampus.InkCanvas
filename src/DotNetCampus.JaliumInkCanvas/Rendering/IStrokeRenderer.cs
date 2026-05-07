using DotNetCampus.Inking.Primitive;
using DotNetCampus.Inking.Model;
using Jalium.UI.Media;

namespace DotNetCampus.Inking.Rendering;

internal interface IStrokeRenderer
{
    string Name { get; }

    StrokeData CreateStroke(Color color, double thickness, InkStylusPoint startPoint);

    void AppendPoints(StrokeData stroke, IReadOnlyList<InkStylusPoint> points, double thickness);

    bool HitTest(StrokeData stroke, Jalium.UI.Point point);
}
