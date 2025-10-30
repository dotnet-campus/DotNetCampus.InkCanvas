using DotNetCampus.Numerics.Geometry;

namespace DotNetCampus.Inking.Utils;

static class PointExtension
{
    public static Avalonia.Point ToAvaloniaPoint(this global::DotNetCampus.Numerics.Geometry.Point2D point)
    {
        return new Avalonia.Point(point.X, point.Y);
    }

    public static Point2D ToPoint2D(this Avalonia.Point point)
    {
        return new Point2D(point.X, point.Y);
    }
}