using DotNetCampus.Inking.Model;
using DotNetCampus.Inking.Primitive;
using Jalium.UI.Media;
using WpfInk;

namespace DotNetCampus.Inking.Rendering;

internal sealed class SkiaFallbackStrokeRenderer : IStrokeRenderer
{
    public string Name => "skia-fallback";

    public StrokeData CreateStroke(Color color, double thickness, InkStylusPoint startPoint)
    {
        return new StrokeData
        {
            Brush = new SolidColorBrush(color),
            Geometry = new PathGeometry(),
            Points = [startPoint],
        };
    }

    public void AppendPoints(StrokeData stroke, IReadOnlyList<InkStylusPoint> points, double thickness)
    {
        if (points.Count == 0)
        {
            return;
        }

        stroke.Points.AddRange(points);
        if (stroke.Points.Count < 2)
        {
            return;
        }

        var geometry = new PathGeometry();
        var context = new JaliumStreamGeometryContext(geometry);
        InkStrokeRenderer.Render(context, new StrokeRendererInfo
        {
            StylusPointCollection = stroke.Points,
            Width = thickness,
            Height = thickness,
            FitToCurve = false,
        });
        stroke.Geometry.Figures.Clear();
        stroke.Geometry.Figures.AddRange(geometry.Figures);
    }

    public bool HitTest(StrokeData stroke, Jalium.UI.Point point)
    {
        return stroke.Geometry.FillContains(point);
    }
}
