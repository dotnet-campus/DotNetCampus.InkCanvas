using System.Diagnostics;

namespace DotNetCampus.Inking.Metrics;

internal sealed class InkPerformanceCounter
{
    private readonly Stopwatch _frameStopwatch = Stopwatch.StartNew();
    private readonly Queue<double> _frameWindow = [];
    private readonly Queue<double> _dynamicRebuildWindow = [];
    private readonly Queue<double> _commitWindow = [];
    private readonly Queue<double> _eraseWindow = [];
    private readonly Stopwatch _pointRateStopwatch = Stopwatch.StartNew();
    private long _pointsSinceLastSample;
    private long _pointsPerSecond;

    public void OnFramePresented()
    {
        var ms = _frameStopwatch.Elapsed.TotalMilliseconds;
        _frameStopwatch.Restart();
        Push(_frameWindow, ms, 120);
    }

    public void AddInputPoints(int count)
    {
        _pointsSinceLastSample += count;
        if (_pointRateStopwatch.Elapsed >= TimeSpan.FromSeconds(1))
        {
            _pointsPerSecond = _pointsSinceLastSample;
            _pointsSinceLastSample = 0;
            _pointRateStopwatch.Restart();
        }
    }

    public void AddDynamicRebuild(double ms) => Push(_dynamicRebuildWindow, ms, 120);
    public void AddCommitUpdate(double ms) => Push(_commitWindow, ms, 120);
    public void AddErase(double ms) => Push(_eraseWindow, ms, 120);

    public JaliumInkCanvasPerformanceSnapshot Snapshot(
        int committedStrokeCount,
        int dynamicStrokePointCount,
        int cacheHit,
        int cacheMiss)
    {
        var avgFrame = Average(_frameWindow);
        var fps = avgFrame > 0.001 ? 1000d / avgFrame : 0;
        return new JaliumInkCanvasPerformanceSnapshot(
            fps,
            avgFrame,
            _pointsPerSecond,
            committedStrokeCount,
            dynamicStrokePointCount,
            cacheHit,
            cacheMiss,
            Average(_dynamicRebuildWindow),
            Average(_commitWindow),
            Average(_eraseWindow));
    }

    private static void Push(Queue<double> queue, double value, int capacity)
    {
        queue.Enqueue(value);
        while (queue.Count > capacity)
        {
            queue.Dequeue();
        }
    }

    private static double Average(IEnumerable<double> values)
    {
        var arr = values as double[] ?? values.ToArray();
        return arr.Length == 0 ? 0 : arr.Average();
    }
}
