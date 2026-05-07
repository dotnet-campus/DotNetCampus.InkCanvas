namespace DotNetCampus.Inking;

public readonly record struct JaliumInkCanvasPerformanceSnapshot(
    double Fps,
    double AverageFrameMs,
    long InputPointsPerSecond,
    int CommittedStrokeCount,
    int DynamicStrokePointCount,
    int CacheHitCount,
    int CacheMissCount,
    double AverageDynamicRebuildMs,
    double AverageCommitUpdateMs,
    double AverageEraseMs);
