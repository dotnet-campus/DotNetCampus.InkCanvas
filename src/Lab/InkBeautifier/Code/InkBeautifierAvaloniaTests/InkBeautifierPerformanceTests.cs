using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using InkBeautifierLib.Configuration;
using InkBeautifierLib.Geometry;
using InkBeautifierLib.Models;
using InkBeautifierLib.Processing;

namespace InkBeautifierAvaloniaTests;

[TestClass]
[DoNotParallelize]
public sealed partial class InkBeautifierPerformanceTests
{
    private const int Rounds = 3;
    private const double BudgetMilliseconds = 33;

    /// <summary>Receives diagnostic output from MSTest.</summary>
    public TestContext TestContext { get; set; } = null!;

    /// <summary>Replays every asset without machine-sensitive timing assertions.</summary>
    [TestMethod]
    [DynamicData(nameof(RecordedAssets))]
    [TestCategory("PerformanceDiagnostics")]
    public void WhenAllRecordedInkIsReplayedThenProcessTimingsAreReported(string assetName)
    {
        RunSuite($"InkBeautifier-performance-{assetName}", assetName);
    }

    /// <summary>Separately exposes the machine-sensitive mid-stroke latency budget.</summary>
    [TestMethod]
    [TestCategory("PerformanceBudget")]
    public void WhenWritingMidStrokeThenMoveStaysWithin33Milliseconds()
    {
        if (Environment.GetEnvironmentVariable("INK_PERFORMANCE_BUDGET") != "1")
        {
            Assert.Inconclusive("Machine-sensitive budget is opt-in: INK_PERFORMANCE_BUDGET=1.");
        }
        List<Measurement[]> runs = RunSuite("InkBeautifier-performance-budget.txt", "Ink3.txt");
        int violatingRounds = runs.Count(run => run.Any(sample =>
            sample.Input.Point.Status == InkPointerStatus.Move &&
            Segment(sample) == 1 &&
            sample.Milliseconds > BudgetMilliseconds));
        Assert.AreEqual(0, violatingRounds,
            $"Mid-stroke Move exceeded 33ms in {violatingRounds}/{runs.Count} rounds of Ink3.txt (middle third). See InkBeautifier-performance-budget.txt. This is a machine-sensitive library-only budget, not a functional regression.");
    }

    private List<Measurement[]> RunSuite(string reportName, string? assetName = null)
    {
        string[] paths = Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "TestAssets"), "*.txt")
            .OrderBy(Path.GetFileName, StringComparer.Ordinal).ToArray();
        Assert.IsGreaterThan(0, paths.Length);
        ReplayAsset[] assets = paths.Where(path => assetName is null || Path.GetFileName(path) == assetName).Select(Load).ToArray();
        // Warm the same geometry paths before recording; no parsed XML or warmup is timed.
        foreach (ReplayAsset asset in assets)
        {
            var beautifier = new InkBeautifier(asset.Options);
            foreach (ReplayInput input in asset.Inputs)
            {
                beautifier.Process(input.Point);
            }
        }

        var report = new StringBuilder();
        string configuration = typeof(InkBeautifier).Assembly
            .GetCustomAttribute<AssemblyConfigurationAttribute>()?.Configuration ?? "unknown";
        report.AppendLine($"InkBeautifierLib Process only; configuration={configuration}; runtime={Environment.Version}; pointerBits={IntPtr.Size * 8}");
        report.AppendLine($"Stopwatch frequency={Stopwatch.Frequency}; warmup=1 complete replay/asset; measuredRounds={Rounds}; indices=1-based; percentile=nearest-rank");
        report.AppendLine("Raw coordinates; per-stroke integer relative ms; pressure=Clamp(raw/1024,0,1); actual Status preferred; legacy final Up at last+1ms.");
        report.AppendLine("Options match screenshot replay (940x700 fit), without coordinate transformation or UI/rendering. No pacing, XML parsing or reporting is timed.");
        report.AppendLine("Allocation/GC deltas sampled around each call outside stopwatch; GC counts are process-wide. Geometry inspected only for top-5 Move candidates after timing. No forced GC.");
        var runs = new List<Measurement[]>();
        foreach (ReplayAsset asset in assets)
        {
            report.AppendLine($"ASSET {asset.Name}: sourcePoints={asset.SourcePoints}; strokes={asset.Strokes}; calls/round={asset.Inputs.Length}; syntheticUp={asset.Inputs.Count(input => input.Synthetic)}");
            var assetRuns = new List<Measurement[]>();
            for (int round = 1; round <= Rounds; round++)
            {
                Measurement[] samples = Measure(asset);
                runs.Add(samples);
                assetRuns.Add(samples);
                AppendRound(report, samples, round);
                File.WriteAllText(Path.Combine(AppContext.BaseDirectory, reportName), report.ToString(), new UTF8Encoding(false));
            }
            report.AppendLine("STABILITY Move max ms by round: " + string.Join(", ", assetRuns.Select(run => F(run.Where(IsMove).Max(sample => sample.Milliseconds)))));
            report.AppendLine("STABILITY middle-third Move >33ms by round: " + string.Join(", ", assetRuns.Select(run => run.Count(sample => IsMove(sample) && Segment(sample) == 1 && sample.Milliseconds > BudgetMilliseconds))));
        }
        string text = report.ToString();
        string reportPath = Path.Combine(AppContext.BaseDirectory, reportName);
        File.WriteAllText(reportPath, text, new UTF8Encoding(false));
        TestContext.WriteLine(text);
        TestContext.AddResultFile(reportPath);
        foreach (Measurement[] run in runs)
        {
            Assert.IsTrue(run.All(sample => sample.Ticks >= 0 && sample.AllocatedBytes >= 0));
            Assert.IsTrue(run.Any(IsMove));
            Assert.IsTrue(run.Any(sample => sample.Input.Point.Status == InkPointerStatus.Up && sample.Completed));
        }
        return runs;
    }

    private static Measurement[] Measure(ReplayAsset asset)
    {
        var beautifier = new InkBeautifier(asset.Options);
        var samples = new Measurement[asset.Inputs.Length];
        var slowest = new List<Measurement>(5);
        for (int index = 0; index < samples.Length; index++)
        {
            ReplayInput input = asset.Inputs[index];
            long bytes = GC.GetAllocatedBytesForCurrentThread();
            int gc0 = GC.CollectionCount(0), gc1 = GC.CollectionCount(1), gc2 = GC.CollectionCount(2);
            long start = Stopwatch.GetTimestamp();
            InkBeautifierResult result = beautifier.Process(input.Point);
            long end = Stopwatch.GetTimestamp();
            long allocated = GC.GetAllocatedBytesForCurrentThread() - bytes;
            int delta0 = GC.CollectionCount(0) - gc0, delta1 = GC.CollectionCount(1) - gc1, delta2 = GC.CollectionCount(2) - gc2;
            var sample = new Measurement(input, end - start, allocated, delta0, delta1, delta2, result.WasAccepted, result.IsCompleted);
            if (IsMove(sample) && (slowest.Count < 5 || sample.Ticks > slowest.Min(item => item.Ticks)))
            {
                sample.Contours = result.Outlines.Count;
                sample.Vertices = result.Outlines.Sum(outline => (long) outline.Count);
                sample.CompatibilityVertices = result.Outline.Count;
                slowest.Add(sample);
                if (slowest.Count > 5)
                {
                    slowest.Remove(slowest.MinBy(item => item.Ticks)!);
                }
            }
            samples[index] = sample;
        }
        return samples;
    }

    private static void AppendRound(StringBuilder report, Measurement[] samples, int round)
    {
        report.AppendLine($" ROUND {round}: calls={samples.Length}; accepted={samples.Count(sample => sample.Accepted)}; filtered={samples.Count(sample => !sample.Accepted)}; ProcessTotalMs={F(samples.Sum(sample => sample.Milliseconds))}; threadAllocatedBytes={samples.Sum(sample => sample.AllocatedBytes)}; GC0/1/2={samples.Sum(sample => sample.Gc0)}/{samples.Sum(sample => sample.Gc1)}/{samples.Sum(sample => sample.Gc2)}");
        foreach (InkPointerStatus status in Enum.GetValues<InkPointerStatus>())
        {
            Measurement[] group = samples.Where(sample => sample.Input.Point.Status == status).ToArray();
            double[] sorted = group.Select(sample => sample.Milliseconds).Order().ToArray();
            report.AppendLine($"  {status}: calls={group.Length}; accepted={group.Count(sample => sample.Accepted)}; filtered={group.Count(sample => !sample.Accepted)}; totalMs={F(sorted.Sum())}; p50={F(Percentile(sorted, .50))}; p95={F(Percentile(sorted, .95))}; p99={F(Percentile(sorted, .99))}; max={F(sorted.LastOrDefault())}; " +
                string.Join("; ", new[] { 8d, 16.67, 33, 50, 100 }.Select(limit => $">{F(limit)}ms={sorted.Count(value => value > limit)}")));
        }
        for (int segment = 0; segment < 3; segment++)
        {
            Measurement[] group = samples.Where(sample => IsMove(sample) && Segment(sample) == segment).ToArray();
            double[] sorted = group.Select(sample => sample.Milliseconds).Order().ToArray();
            report.AppendLine($"  TREND {new[] { "early", "middle", "late" }[segment]} (per-stroke input thirds): calls={group.Length}; accepted={group.Count(sample => sample.Accepted)}; meanMs={F(sorted.Length == 0 ? 0 : sorted.Average())}; p95={F(Percentile(sorted, .95))}; max={F(sorted.LastOrDefault())}; >33ms={sorted.Count(value => value > 33)}");
        }
        foreach (Measurement sample in samples.Where(IsMove).OrderByDescending(sample => sample.Ticks).Take(5))
        {
            report.AppendLine($"  SLOW Move ms={F(sample.Milliseconds)}; stroke={sample.Input.Point.StrokeId}; point={sample.Input.PointIndex}/{sample.Input.StrokeLength}; relativeMs={sample.Input.Point.Timestamp}; input={sample.Input.InputIndex}; accepted={sample.Accepted}; contours={sample.Contours}; vertices={sample.Vertices}; compatibilityVertices={sample.CompatibilityVertices}; allocatedBytes={sample.AllocatedBytes}; GC0/1/2={sample.Gc0}/{sample.Gc1}/{sample.Gc2}");
        }
    }

    private static ReplayAsset Load(string path)
    {
        XDocument document = XDocument.Parse(File.ReadAllText(path));
        XElement[][] strokes = (document.Root ?? throw new InvalidDataException("Missing asset root."))
            .Elements("ArrayOfInkDataModel").Select(stroke => stroke.Elements("InkDataModel").ToArray())
            .Where(stroke => stroke.Length > 0).ToArray();
        XElement[] all = strokes.SelectMany(stroke => stroke).ToArray();
        Assert.IsNotEmpty(all);
        double scale = Math.Min(940 / Math.Max(1, all.Max(point => Number(point, "X")) - all.Min(point => Number(point, "X"))),
            700 / Math.Max(1, all.Max(point => Number(point, "Y")) - all.Min(point => Number(point, "Y"))));
        var inputs = new List<ReplayInput>();
        int sourceIndex = 0;
        for (int stroke = 0; stroke < strokes.Length; stroke++)
        {
            XElement[] points = strokes[stroke];
            long first = Timestamp(points[0]);
            for (int index = 0; index < points.Length; index++)
            {
                XElement point = points[index];
                InkPointerStatus status = point.Element("Status") is { } element
                    ? Enum.Parse<InkPointerStatus>(element.Value)
                    : index == 0 ? InkPointerStatus.Down : InkPointerStatus.Move;
                var input = new InkInputPoint(new InkPoint2D(Number(point, "X"), Number(point, "Y")),
                    Math.Max(0, Timestamp(point) - first), Math.Clamp(Number(point, "PressureFactor") / 1024, 0, 1), stroke + 1, status);
                inputs.Add(new ReplayInput(input, index + 1, points.Length, ++sourceIndex, false));
            }
            if (points[^1].Element("Status") is null)
            {
                ReplayInput last = inputs[^1];
                inputs.Add(last with { Point = last.Point with { Timestamp = Math.Max(1, Timestamp(points[^1]) - first + 1), Status = InkPointerStatus.Up }, Synthetic = true });
            }
        }
        return new ReplayAsset(Path.GetFileName(path), inputs.ToArray(), strokes.Length, all.Length,
            new InkBeautifierOptions { MaximumWidth = 18 / scale, VelocityResponse = 1.8 * scale });
    }

    private static double Number(XElement point, string name) => double.Parse(point.Element(name)?.Value ?? throw new InvalidDataException("Missing point field."), CultureInfo.InvariantCulture);
    private static long Timestamp(XElement point) => (long) XmlConvert.ToTimeSpan(point.Element("Time")?.Value ?? throw new InvalidDataException("Missing timestamp.")).TotalMilliseconds;
    private static bool IsMove(Measurement sample) => sample.Input.Point.Status == InkPointerStatus.Move;
    private static int Segment(Measurement sample) => Math.Min(2, (sample.Input.PointIndex - 1) * 3 / sample.Input.StrokeLength);
    private static double Percentile(double[] sorted, double fraction) => sorted.Length == 0 ? 0 : sorted[(int) Math.Ceiling(sorted.Length * fraction) - 1];
    private static string F(double value) => value.ToString("F3", CultureInfo.InvariantCulture);

    private sealed record ReplayAsset(string Name, ReplayInput[] Inputs, int Strokes, int SourcePoints, InkBeautifierOptions Options);
    private sealed record ReplayInput(InkInputPoint Point, int PointIndex, int StrokeLength, int InputIndex, bool Synthetic);
    private sealed record Measurement(ReplayInput Input, long Ticks, long AllocatedBytes, int Gc0, int Gc1, int Gc2, bool Accepted, bool Completed)
    {
        internal double Milliseconds => Ticks * 1000d / Stopwatch.Frequency;
        internal int Contours { get; set; }
        internal long Vertices { get; set; }
        internal int CompatibilityVertices { get; set; }
    }
}
