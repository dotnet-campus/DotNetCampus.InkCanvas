using InkBeautifierLib.Geometry;
using InkBeautifierLib.Models;

namespace InkBeautifierLib.Processing;

public sealed partial class InkBeautifier
{
    private sealed class StrokeState(InkInputPoint firstPoint)
    {
        internal List<RawPoint> RawPoints { get; } = [new(firstPoint.Position, firstPoint.Timestamp, 0, 0, 0, 0)];
        internal List<RawPoint> Samples { get; } = [];
        internal IReadOnlyList<InkPoint2D> Outline { get; set; } = [];
        internal IReadOnlyList<IReadOnlyList<InkPoint2D>> Outlines { get; set; } = [];
        internal List<RawPoint> CachedSamples { get; } = [];
        internal List<bool> SegmentOverlaps { get; } = [];
        internal List<bool> PointOverlaps { get; } = [];
        internal List<IReadOnlyList<InkPoint2D>> SweepCells { get; } = [];
        internal List<IReadOnlyList<InkPoint2D>> ReverseSweepCells { get; } = [];
        internal List<int> SweepCellSigns { get; } = [];
        internal int NarrowPointCount { get; set; }
        internal bool IsTap { get; set; }
    }

    private readonly record struct StrokeGeometry
    (
        IReadOnlyList<InkPoint2D> Outline,
        IReadOnlyList<IReadOnlyList<InkPoint2D>> Outlines
    );

    private readonly record struct RawPoint
    (
        InkPoint2D Position,
        long Timestamp,
        double RawSpeed,
        double SmoothSpeed,
        double RawHalfWidth,
        double HalfWidth
    )
    {
        internal double DistanceFrom(RawPoint other) => InkPoint2D.Distance(Position, other.Position);
    }
}
