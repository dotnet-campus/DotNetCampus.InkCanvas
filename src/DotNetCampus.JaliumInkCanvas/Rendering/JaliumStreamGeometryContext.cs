using DotNetCampus.Numerics.Geometry;
using Jalium.UI.Media;
using WpfInk;

namespace DotNetCampus.Inking.Rendering;

internal sealed class JaliumStreamGeometryContext(PathGeometry geometry) : IStreamGeometryContext
{
    private PathFigure? _currentFigure;

    public void BeginFigure(Point2D startPoint, bool isFilled, bool isClosed)
    {
        _currentFigure = new PathFigure
        {
            StartPoint = new Jalium.UI.Point(startPoint.X, startPoint.Y),
            IsClosed = isClosed,
            IsFilled = isFilled,
        };
        geometry.Figures.Add(_currentFigure);
    }

    public void PolyBezierTo(IList<Point2D> points, bool isStroked, bool isSmoothJoin)
    {
        if (_currentFigure is null || points.Count == 0)
        {
            return;
        }

        var segment = new PolyBezierSegment
        {
            IsStroked = isStroked,
        };
        foreach (var point in points)
        {
            segment.Points.Add(new Jalium.UI.Point(point.X, point.Y));
        }
        _currentFigure.Segments.Add(segment);
    }

    public void PolyLineTo(IList<Point2D> points, bool isStroked, bool isSmoothJoin)
    {
        if (_currentFigure is null || points.Count == 0)
        {
            return;
        }

        _currentFigure.Segments.Add(new PolyLineSegment(points.Select(p => new Jalium.UI.Point(p.X, p.Y)), isStroked));
    }

    public void ArcTo(Point2D point, Size2D size, double rotationAngle, bool isLargeArc, bool sweepDirection, bool isStroked, bool isSmoothJoin)
    {
        if (_currentFigure is null)
        {
            return;
        }

        _currentFigure.Segments.Add(new ArcSegment(
            new Jalium.UI.Point(point.X, point.Y),
            new Jalium.UI.Size(size.Width, size.Height),
            rotationAngle,
            isLargeArc,
            sweepDirection ? SweepDirection.Clockwise : SweepDirection.Counterclockwise,
            isStroked));
    }
}
