using DotNetCampus.Inking.Primitive;
using DotNetCampus.Numerics.Geometry;

namespace DotNetCampus.Inking.Interactives;

public readonly record struct InkingModeInputArgs(int Id, InkStylusPoint StylusPoint, ulong Timestamp)
{
    public Point2D Position => StylusPoint.Point;

    /// <summary>
    /// 是否来自鼠标的输入
    /// </summary>
    public bool IsMouse { init; get; }

    /// <summary>
    /// 被合并的其他历史的触摸点。可能为空
    /// </summary>
    public IReadOnlyList<InkStylusPoint>? StylusPointList { init; get; }
}