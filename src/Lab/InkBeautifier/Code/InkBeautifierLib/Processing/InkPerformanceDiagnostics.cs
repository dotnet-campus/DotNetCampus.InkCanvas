using System.Diagnostics;

namespace InkBeautifierLib.Processing;

/// <summary>Temporary synchronous, thread-local investigation; disabled unless a session is explicitly opened.</summary>
public sealed class InkPerformanceDiagnostics : IDisposable
{
    [ThreadStatic] private static InkPerformanceDiagnostics? _current;
    private readonly InkPerformanceDiagnostics? _previous;
    private readonly Frame[] _stack = new Frame[64];
    private int _depth;
    private readonly bool _detailed;
    private readonly Metric[] _metrics = Enum.GetValues<Stage>().Select(_ => new Metric()).ToArray();
    private readonly long[] _work = new long[Enum.GetValues<Work>().Length];

    /// <summary>Starts a diagnostic scope on the current synchronous processing thread.</summary>
    public InkPerformanceDiagnostics(bool detailed = false)
    {
        _previous = _current;
        _current = this;
        _detailed = detailed;
    }

    /// <summary>Returns independent snapshots; take these outside the measured Process call.</summary>
    public IReadOnlyList<StageMeasurement> Snapshot() => Enum.GetValues<Stage>()
        .Select(stage => new StageMeasurement(stage.ToString(), _metrics[(int) stage].Calls,
            _metrics[(int) stage].Ticks, _metrics[(int) stage].SelfTicks,
            _metrics[(int) stage].Bytes, _metrics[(int) stage].SelfBytes)).ToArray();

    /// <summary>Returns operation counts; units are named, not durations.</summary>
    public IReadOnlyDictionary<string, long> WorkSnapshot() => Enum.GetValues<Work>()
        .ToDictionary(work => work.ToString(), work => _work[(int) work]);

    /// <summary>Restores the previous thread-local session.</summary>
    public void Dispose() => _current = _previous;

    internal static Scope Measure(Stage stage, bool detailed = false)
    {
        var current = _current;
        if (current is null || (detailed && !current._detailed)) return default;
        int depth = current._depth++;
        current._stack[depth] = new Frame
        {
            Stage = stage,
            Bytes = GC.GetAllocatedBytesForCurrentThread(),
            Start = Stopwatch.GetTimestamp()
        };
        return new Scope(current);
    }

    internal static void Count(Work work, long amount = 1)
    {
        var current = _current;
        if (current is not null && current._detailed) current._work[(int) work] += amount;
    }

    private void End()
    {
        long end = Stopwatch.GetTimestamp();
        long bytes = GC.GetAllocatedBytesForCurrentThread();
        Frame frame = _stack[--_depth];
        long ticks = end - frame.Start;
        long allocated = bytes - frame.Bytes;
        Metric metric = _metrics[(int) frame.Stage];
        metric.Calls++;
        metric.Ticks += ticks;
        metric.SelfTicks += ticks - frame.ChildTicks;
        metric.Bytes += allocated;
        metric.SelfBytes += allocated - frame.ChildBytes;
        if (_depth > 0)
        {
            _stack[_depth - 1].ChildTicks += ticks;
            _stack[_depth - 1].ChildBytes += allocated;
        }
    }

    internal readonly struct Scope(InkPerformanceDiagnostics? owner) : IDisposable
    {
        public void Dispose() => owner?.End();
    }

    private struct Frame
    {
        internal Stage Stage;
        internal long Start, Bytes, ChildTicks, ChildBytes;
    }
    private sealed class Metric
    {
        internal long Calls, Ticks, SelfTicks, Bytes, SelfBytes;
    }

    internal enum Stage
    {
        Process, TryAddPoint, Sampling, BuildGeometry, TemplateSweep, RepairCoverage,
        RawCoverage, FinalSampleCoverage, SweepCells, NormalOffset,
        OverlapPoint, OverlapSegment, Winding, PenCapInterior, CoverageCap,
        BoundaryPoint, BoundaryPair, CrossBoundary, OuterTrim, BoundarySeam,
        TemplateSupport, TemplateArc, ConvexHull, AddPenCap, PlacePenCap, SignedArea, Duplicates
    }
    internal enum Work
    {
        RawPointsAtBuild, SamplesAtBuild, SamplingCandidates, SamplesAppended,
        SweepCellsBuilt, HullCandidates, TemplateTransforms, SupportCandidates,
        RawCoverageChecks, FinalSampleChecks, CoverageCaps,
        OverlapPointCandidates, OverlapSegmentCandidates, WindingEdges,
        IntersectionCalls, IntersectionBoundsPassed, CrossBoundaryCandidates,
        OuterTrimCandidates, BoundaryPointCandidates, AreaEdges
    }
}

/// <summary>Inclusive contains nested stages; exclusive subtracts only measured direct children.</summary>
public sealed record StageMeasurement(string Stage, long Calls, long InclusiveTicks, long ExclusiveTicks,
    long InclusiveBytes, long ExclusiveBytes);
