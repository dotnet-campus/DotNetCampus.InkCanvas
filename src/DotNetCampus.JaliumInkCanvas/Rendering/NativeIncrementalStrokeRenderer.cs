using DotNetCampus.Inking.Model;
using DotNetCampus.Inking.Primitive;
using Jalium.UI.Media;

namespace DotNetCampus.Inking.Rendering;

internal sealed class NativeIncrementalStrokeRenderer : IStrokeRenderer
{
    public string Name => "native";

    public StrokeData CreateStroke(Color color, double thickness, InkStylusPoint startPoint)
    {
        var geometry = BuildGeometry([startPoint], thickness);

        return new StrokeData
        {
            Brush = new SolidColorBrush(color),
            Geometry = geometry,
            Points = [startPoint],
            Thickness = thickness,
        };
    }

    public void AppendPoints(StrokeData stroke, IReadOnlyList<InkStylusPoint> points, double thickness)
    {
        if (points.Count == 0)
        {
            return;
        }

        stroke.Points.AddRange(points);
        var geometry = BuildGeometry(stroke.Points, thickness);
        stroke.Geometry.Figures.Clear();
        stroke.Geometry.Figures.AddRange(geometry.Figures);
    }

    public bool HitTest(StrokeData stroke, Jalium.UI.Point point)
    {
        return stroke.Geometry.FillContains(point);
    }

    private static PathGeometry BuildGeometry(IReadOnlyList<InkStylusPoint> points, double thickness)
    {
        if (points.Count == 0)
        {
            return new PathGeometry();
        }

        if (points.Count == 1)
        {
            return BuildSinglePointGeometry(points[0], thickness);
        }

        var outlinePoints = SimpleInkRender.GetOutlinePointList(points, thickness);
        if (outlinePoints.Length == 0)
        {
            return new PathGeometry();
        }

        var geometry = new PathGeometry();
        var figure = new PathFigure
        {
            StartPoint = new Jalium.UI.Point(outlinePoints[0].X, outlinePoints[0].Y),
            IsClosed = true,
            IsFilled = true,
        };
        figure.Segments.Add(new PolyLineSegment(outlinePoints.Skip(1).Select(p => new Jalium.UI.Point(p.X, p.Y)), true));
        geometry.Figures.Add(figure);
        return geometry;
    }

    private static PathGeometry BuildSinglePointGeometry(InkStylusPoint point, double thickness)
    {
        var radius = Math.Max(0.01d, thickness * point.Pressure / 2d);
        var geometry = new PathGeometry();
        var figure = new PathFigure
        {
            StartPoint = new Jalium.UI.Point(point.X + radius, point.Y),
            IsClosed = true,
            IsFilled = true,
        };
        figure.Segments.Add(new ArcSegment(
            new Jalium.UI.Point(point.X - radius, point.Y),
            new Jalium.UI.Size(radius, radius),
            0,
            false,
            SweepDirection.Clockwise,
            true));
        figure.Segments.Add(new ArcSegment(
            new Jalium.UI.Point(point.X + radius, point.Y),
            new Jalium.UI.Size(radius, radius),
            0,
            false,
            SweepDirection.Clockwise,
            true));
        geometry.Figures.Add(figure);
        return geometry;
    }
}
