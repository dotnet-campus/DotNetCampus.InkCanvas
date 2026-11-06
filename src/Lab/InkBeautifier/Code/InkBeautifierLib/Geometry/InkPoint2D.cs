namespace InkBeautifierLib.Geometry;

/// <summary>
/// 表示平台无关的二维坐标。
/// </summary>
public readonly record struct InkPoint2D(double X, double Y)
{
    internal static double Distance(InkPoint2D left, InkPoint2D right) =>
        double.Hypot(left.X - right.X, left.Y - right.Y);

    internal static InkPoint2D Lerp(InkPoint2D start, InkPoint2D end, double amount) =>
        new(start.X + ((end.X - start.X) * amount), start.Y + ((end.Y - start.Y) * amount));
}
