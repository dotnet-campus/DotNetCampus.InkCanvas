using DotNetCampus.Inking.Model;
using DotNetCampus.Inking.Primitive;
using Jalium.UI.Media;

namespace DotNetCampus.Inking.Rendering;

internal sealed class NativeIncrementalStrokeRenderer : IStrokeRenderer
{
    public string Name => "native";

    public StrokeData CreateStroke(Color color, double thickness, InkStylusPoint startPoint)
    {
        var geometry = new PathGeometry();
        var figure = new PathFigure
        {
            StartPoint = new Jalium.UI.Point(startPoint.X, startPoint.Y),
            IsClosed = false,
            IsFilled = false,
        };
        figure.Segments.Add(new PolyLineSegment());
        geometry.Figures.Add(figure);

        return new StrokeData
        {
            Brush = new SolidColorBrush(color),
            Geometry = geometry,
            Points = [startPoint],
        };
    }

    public void AppendPoints(StrokeData stroke, IReadOnlyList<InkStylusPoint> points, double thickness)
    {
        if (points.Count == 0 || stroke.Geometry.Figures.Count == 0)
        {
            return;
        }

        var figure = stroke.Geometry.Figures[0];
        if (figure.Segments.FirstOrDefault() is not PolyLineSegment segment)
        {
            segment = new PolyLineSegment();
            figure.Segments.Clear();
            figure.Segments.Add(segment);
        }

        foreach (var point in points)
        {
            stroke.Points.Add(point);
            segment.Points.Add(new Jalium.UI.Point(point.X, point.Y));
        }
    }

    public bool HitTest(StrokeData stroke, Jalium.UI.Point point)
    {
        return stroke.Geometry.FillContains(point);
    }
}
