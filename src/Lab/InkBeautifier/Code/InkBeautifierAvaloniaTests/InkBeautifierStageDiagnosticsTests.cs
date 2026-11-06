using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using InkBeautifierLib.Models;
using InkBeautifierLib.Processing;

namespace InkBeautifierAvaloniaTests;

public sealed partial class InkBeautifierPerformanceTests
{
    /// <summary>Discovers every recorded asset, including future additions.</summary>
    public static IEnumerable<object[]> DiagnosticAssets() => Directory.GetFiles(
        Path.Combine(AppContext.BaseDirectory, "TestAssets"), "*.txt")
        .OrderBy(Path.GetFileName, StringComparer.Ordinal).Select(path => new object[] { Path.GetFileName(path) });

    /// <summary>Measures library stages after warmup and verifies bit-exact output fingerprints across modes.</summary>
    [TestMethod]
    [DynamicData(nameof(DiagnosticAssets))]
    [TestCategory("StageDiagnostics")]
    public void WhenDiagnosticsAreToggledThenAllAssetGeometryIsUnchanged(string name)
    {
        ReplayAsset asset = Load(Path.Combine(AppContext.BaseDirectory, "TestAssets", name));
        var warm = new InkBeautifier(asset.Options);
        foreach (var input in asset.Inputs) warm.Process(input.Point);
        string directory = Path.Combine(FindRepository(), "artifacts", "InkBeautifierDiagnostics");
        Directory.CreateDirectory(directory);
        string[] previousFiles = Directory.GetFiles(directory, name + ".*");
        if (previousFiles.Length > 0)
        {
            string archive = Path.Combine(directory, "previous", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture));
            Directory.CreateDirectory(archive);
            foreach (string file in previousFiles) File.Copy(file, Path.Combine(archive, Path.GetFileName(file)));
        }
        var summary = new StringBuilder();
        summary.AppendLine($"asset={name}; runtime={Environment.Version}; configuration={typeof(InkBeautifier).Assembly.GetCustomAttribute<AssemblyConfigurationAttribute>()?.Configuration}; frequency={Stopwatch.Frequency}; calls={asset.Inputs.Length}; strokes={asset.Strokes}; warmup=1; no UI; allocations=current thread; GC=process-wide");
        string? expected = null;
        foreach (string mode in new[] { "off-before", "coarse", "fine", "off-after" })
        {
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var beautifier = new InkBeautifier(asset.Options);
            using var session = mode.StartsWith("off", StringComparison.Ordinal) ? null : new InkPerformanceDiagnostics(mode == "fine");
            var rows = new List<DiagnosticCall>(asset.Inputs.Length);
            var stages = new StringBuilder("input,stroke,point,length,status,third,stage,calls,inclusiveMs,exclusiveMs,inclusiveBytes,exclusiveBytes\n");
            var work = new StringBuilder("input,stroke,status,third,work,count\n");
            var previous = session?.Snapshot();
            var previousWork = session?.WorkSnapshot();
            for (int i = 0; i < asset.Inputs.Length; i++)
            {
                ReplayInput input = asset.Inputs[i];
                int third = Math.Min(2, (input.PointIndex - 1) * 3 / input.StrokeLength);
                int gc0 = GC.CollectionCount(0), gc1 = GC.CollectionCount(1), gc2 = GC.CollectionCount(2);
                long bytes = GC.GetAllocatedBytesForCurrentThread();
                long start = Stopwatch.GetTimestamp();
                InkBeautifierResult result = beautifier.Process(input.Point);
                long ticks = Stopwatch.GetTimestamp() - start;
                long allocated = GC.GetAllocatedBytesForCurrentThread() - bytes;
                rows.Add(new DiagnosticCall(i + 1, input.Point.StrokeId, input.PointIndex, input.StrokeLength,
                    input.Point.Status, third, ticks * 1000d / Stopwatch.Frequency, allocated,
                    GC.CollectionCount(0) - gc0, GC.CollectionCount(1) - gc1, GC.CollectionCount(2) - gc2,
                    result.WasAccepted, result.Outline.Count, result.Outlines.Count));
                AppendHash(hash, result);
                if (session is null) continue;
                var current = session.Snapshot();
                for (int s = 0; s < current.Count; s++)
                {
                    var a = current[s];
                    var b = previous![s];
                    if (a.Calls == b.Calls) continue;
                    stages.AppendLine(FormattableString.Invariant($"{i + 1},{input.Point.StrokeId},{input.PointIndex},{input.StrokeLength},{input.Point.Status},{third},{a.Stage},{a.Calls - b.Calls},{(a.InclusiveTicks - b.InclusiveTicks) * 1000d / Stopwatch.Frequency:F6},{(a.ExclusiveTicks - b.ExclusiveTicks) * 1000d / Stopwatch.Frequency:F6},{a.InclusiveBytes - b.InclusiveBytes},{a.ExclusiveBytes - b.ExclusiveBytes}"));
                }
                previous = current;
                var currentWork = session.WorkSnapshot();
                foreach (var pair in currentWork)
                {
                    long count = pair.Value - previousWork![pair.Key];
                    if (count != 0) work.AppendLine($"{i + 1},{input.Point.StrokeId},{input.Point.Status},{third},{pair.Key},{count}");
                }
                previousWork = currentWork;
            }
            string fingerprint = Convert.ToHexString(hash.GetHashAndReset());
            expected ??= fingerprint;
            Assert.AreEqual(expected, fingerprint, $"Output changed for {name}/{mode}");
            summary.AppendLine(FormattableString.Invariant($"MODE {mode}: ProcessMs={rows.Sum(r => r.Milliseconds):F3}; bytes={rows.Sum(r => r.Bytes)}; GC={rows.Sum(r => r.Gc0)}/{rows.Sum(r => r.Gc1)}/{rows.Sum(r => r.Gc2)}; SHA256={fingerprint}"));
            foreach (var group in rows.GroupBy(r => (r.Status, r.Third)))
            {
                double[] times = group.Select(r => r.Milliseconds).Order().ToArray();
                summary.AppendLine(FormattableString.Invariant($" {group.Key}: calls={times.Length}; accepted={group.Count(r => r.Accepted)}; totalMs={times.Sum():F3}; p50={Percentile(times, .5):F3}; p95={Percentile(times, .95):F3}; max={times[^1]:F3}; bytes={group.Sum(r => r.Bytes)}"));
            }
            if (session is not null)
            {
                foreach (var s in session.Snapshot().Where(s => s.Calls > 0))
                    summary.AppendLine(FormattableString.Invariant($" STAGE {s.Stage}: calls={s.Calls}; inclusiveMs={s.InclusiveTicks * 1000d / Stopwatch.Frequency:F3}; exclusiveMs={s.ExclusiveTicks * 1000d / Stopwatch.Frequency:F3}; inclusiveBytes={s.InclusiveBytes}; exclusiveBytes={s.ExclusiveBytes}"));
                foreach (var pair in session.WorkSnapshot().Where(p => p.Value != 0)) summary.AppendLine($" WORK {pair.Key}={pair.Value}");
            }
            var calls = new StringBuilder("input,stroke,point,length,status,third,ms,bytes,gc0,gc1,gc2,accepted,outlineVertices,outlines\n");
            foreach (var r in rows) calls.AppendLine(FormattableString.Invariant($"{r.Input},{r.Stroke},{r.Point},{r.Length},{r.Status},{r.Third},{r.Milliseconds:F6},{r.Bytes},{r.Gc0},{r.Gc1},{r.Gc2},{r.Accepted},{r.Vertices},{r.Outlines}"));
            File.WriteAllText(Path.Combine(directory, name + "." + mode + ".calls.csv"), calls.ToString());
            if (session is not null)
            {
                File.WriteAllText(Path.Combine(directory, name + "." + mode + ".stages.csv"), stages.ToString());
                File.WriteAllText(Path.Combine(directory, name + "." + mode + ".work.csv"), work.ToString());
            }
            File.WriteAllText(Path.Combine(directory, name + ".summary.txt"), summary.ToString());
        }
        TestContext.WriteLine(summary.ToString());
    }

    /// <summary>Aggregates recorded CSV files outside all timed regions.</summary>
    [TestMethod]
    [TestCategory("StageAnalysis")]
    public void WhenMeasurementsExistThenStageBreakdownIsWritten()
    {
        string directory = Path.Combine(FindRepository(), "artifacts", "InkBeautifierDiagnostics");
        var report = new StringBuilder("# Derived measurements\n\nTimes in ms; stage totals are inclusive: do not add nested stages.\n\n");
        foreach (string path in Directory.GetFiles(directory, "*.coarse.calls.csv").Order(StringComparer.Ordinal))
        {
            string name = Path.GetFileName(path).Replace(".coarse.calls.csv", "");
            report.AppendLine("## " + name);
            var calls = File.ReadAllLines(path).Skip(1).Select(l => l.Split(',')).ToArray();
            var stages = File.ReadAllLines(Path.Combine(directory, name + ".coarse.stages.csv")).Skip(1).Select(l => l.Split(',')).ToArray();
            report.AppendLine("\n|Status/third|Process ms|TemplateSweep ms|RawCoverage ms|FinalSamples ms|SweepCells ms|Process bytes|\n|---|---:|---:|---:|---:|---:|---:|");
            foreach (var group in calls.GroupBy(r => r[4] + "/" + r[5]))
            {
                var parts = group.Key.Split('/');
                double Sum(string stage) => stages.Where(r => r[4] == parts[0] && r[5] == parts[1] && r[6] == stage).Sum(r => double.Parse(r[8], CultureInfo.InvariantCulture));
                report.AppendLine(FormattableString.Invariant($"|{group.Key}|{group.Sum(r => double.Parse(r[6], CultureInfo.InvariantCulture)):F3}|{Sum("TemplateSweep"):F3}|{Sum("RawCoverage"):F3}|{Sum("FinalSampleCoverage"):F3}|{Sum("SweepCells"):F3}|{group.Sum(r => long.Parse(r[7], CultureInfo.InvariantCulture))}|"));
            }
            foreach (var stroke in calls.GroupBy(r => r[1]))
            {
                var moves = stroke.Where(r => r[4] == "Move" && r[11] == "True").ToArray();
                var representatives = new List<string[]>();
                foreach (double fraction in new[] { 1d / 6, .5, 5d / 6 })
                {
                    var chosen = moves.MinBy(r => Math.Abs(double.Parse(r[2], CultureInfo.InvariantCulture) / double.Parse(r[3], CultureInfo.InvariantCulture) - fraction));
                    if (chosen is not null) representatives.Add(chosen);
                }
                representatives.AddRange(stroke.Where(r => r[4] == "Up"));
                foreach (var row in representatives.Distinct())
                {
                    report.AppendLine($"- stroke={row[1]}, point={row[2]}/{row[3]}, input={row[0]}, {row[4]}, accepted={row[11]}, wallMs={row[6]}, bytes={row[7]}, GC={row[8]}/{row[9]}/{row[10]}, vertices={row[12]}, outlines={row[13]}");
                    foreach (var s in stages.Where(r => r[0] == row[0])) report.AppendLine($"  - {s[6]}: calls={s[7]}, inclusiveMs={s[8]}, exclusiveMs={s[9]}, bytes={s[10]}, selfBytes={s[11]}");
                }
            }
        }
        File.WriteAllText(Path.Combine(directory, "DerivedMeasurements.md"), report.ToString());
        Assert.IsGreaterThan(500, report.Length);
    }

    private static string FindRepository()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Directory.Build.props"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository configuration not found.");
    }

    private static void AppendHash(IncrementalHash hash, InkBeautifierResult result)
    {
        Span<byte> buffer = stackalloc byte[8];
        void Add(long value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            hash.AppendData(bytes);
        }
        Add(result.StrokeId);
        Add(result.WasAccepted ? 1 : 0);
        Add(result.IsCompleted ? 1 : 0);
        Add(result.Outline.Count);
        foreach (var point in result.Outline)
        {
            BitConverter.TryWriteBytes(buffer, point.X); hash.AppendData(buffer);
            BitConverter.TryWriteBytes(buffer, point.Y); hash.AppendData(buffer);
        }
        Add(result.Outlines.Count);
        foreach (var contour in result.Outlines)
        {
            Add(contour.Count);
            foreach (var point in contour)
            {
                BitConverter.TryWriteBytes(buffer, point.X); hash.AppendData(buffer);
                BitConverter.TryWriteBytes(buffer, point.Y); hash.AppendData(buffer);
            }
        }
    }

    private sealed record DiagnosticCall(int Input, long Stroke, int Point, int Length, InkPointerStatus Status,
        int Third, double Milliseconds, long Bytes, int Gc0, int Gc1, int Gc2, bool Accepted, int Vertices, int Outlines);
}
