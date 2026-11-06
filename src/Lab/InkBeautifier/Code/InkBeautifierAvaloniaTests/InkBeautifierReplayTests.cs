using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using InkBeautifierLib.Models;
using InkBeautifierLib.Processing;

namespace InkBeautifierAvaloniaTests;

public sealed partial class InkBeautifierPerformanceTests
{
    /// <summary>Discovers every recorded asset in ordinal order.</summary>
    public static IEnumerable<object[]> RecordedAssets() => Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "TestAssets"), "*.txt")
        .OrderBy(Path.GetFileName, StringComparer.Ordinal).Select(path => new object[] { Path.GetFileName(path) });

    /// <summary>Opt-in cross-binary experiment; never records a replacement for an existing golden.</summary>
    [TestMethod]
    [DynamicData(nameof(RecordedAssets))]
    [TestCategory("CrossBinaryDiagnostics")]
    public void WhenOriginalBinaryIsReplayedThenEveryInputMatchesGolden(string assetName)
    {
        RunCrossBinary(assetName);
    }

    private static void RunCrossBinary(string assetName)
    {
        DirectoryInfo? root = new(AppContext.BaseDirectory);
        while (root != null && !File.Exists(Path.Combine(root.FullName, "Directory.Build.props"))) root = root.Parent;
        Assert.IsNotNull(root);
        string directory = Path.Combine(root.FullName, "artifacts", "InkBeautifierCleanReplay");
        string golden = Path.Combine(root.FullName, "artifacts", "InkBeautifierGolden", assetName + ".golden.csv");
        if (!File.Exists(golden))
        {
            Assert.Inconclusive("Optional cross-binary experiment requires preserved original artifacts; it never records a golden.");
        }
        Directory.CreateDirectory(directory);
        ReplayAsset asset = Load(Path.Combine(AppContext.BaseDirectory, "TestAssets", assetName));
        var processor = new InkBeautifier(asset.Options);
        var lines = new List<string>();
        var timings = new List<string> { "input,ms,bytes" };
        for (int i = 0; i < asset.Inputs.Length; i++)
        {
            long bytes = GC.GetAllocatedBytesForCurrentThread();
            long start = Stopwatch.GetTimestamp();
            InkBeautifierResult result = processor.Process(asset.Inputs[i].Point);
            long end = Stopwatch.GetTimestamp();
            long allocated = GC.GetAllocatedBytesForCurrentThread() - bytes;
            timings.Add(FormattableString.Invariant($"{i},{(end-start)*1000d/Stopwatch.Frequency:R},{allocated}"));
            lines.Add($"{i},{result.StrokeId},{result.WasAccepted},{result.IsCompleted},{result.Outline.Count},{result.Outlines.Count},{Fingerprint(result)}");
        }
        CollectionAssert.AreEqual(File.ReadAllLines(golden), lines.ToArray(), "Original cross-binary geometry differs.");
        File.WriteAllLines(Path.Combine(directory, assetName + $".after-{DateTime.UtcNow:yyyyMMdd-HHmmss-fffffff}.csv"), timings);
    }

    /// <summary>Summarizes the original-binary and latest optimized-binary Process measurements.</summary>
    [TestMethod]
    [TestCategory("CrossBinarySummary")]
    public void WhenCrossBinaryReplayCompletesThenComparisonIsReported()
    {
        DirectoryInfo? root = new(AppContext.BaseDirectory);
        while (root != null && !File.Exists(Path.Combine(root.FullName, "Directory.Build.props"))) root = root.Parent;
        Assert.IsNotNull(root);
        string directory = Path.Combine(root.FullName, "artifacts", "InkBeautifierCleanReplay");
        if (!Directory.Exists(directory) || RecordedAssets().Any(row =>
            !File.Exists(Path.Combine(directory, (string)row[0] + ".before.csv")) ||
            Directory.GetFiles(directory, (string)row[0] + ".after-*.csv").Length == 0))
        {
            Assert.Inconclusive("Optional summary requires preserved original timings and completed optimized replays.");
        }
        var report = new StringBuilder("# Clean replay: original vs optimized\n\nUnwarmed cross-binary replay, Process only; not a controlled benchmark. Three-round warmed performance reports are separate.\n\n|asset/status|before ms|after ms|before bytes|after bytes|before p95|after p95|before p99|after p99|before max|after max|\n|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|\n");
        foreach (object[] row in RecordedAssets())
        {
            string name = (string)row[0];
            ReplayAsset asset = Load(Path.Combine(AppContext.BaseDirectory, "TestAssets", name));
            string afterPath = Directory.GetFiles(directory, name + ".after-*.csv").Order(StringComparer.Ordinal).Last();
            (int Index, double Ms, long Bytes)[] Read(string path) => File.ReadLines(path).Skip(1).Select(line => line.Split(',')).Select(parts =>
                (int.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture), double.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture), long.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture))).ToArray();
            var before = Read(Path.Combine(directory, name + ".before.csv"));
            var after = Read(afterPath);
            CollectionAssert.AreEqual(before.Select(x => x.Index).ToArray(), after.Select(x => x.Index).ToArray());
            Assert.HasCount(asset.Inputs.Length, before);
            foreach (string group in name == "Ink3.txt" ? new[] { "All", "Move", "Up" } : new[] { "All" })
            {
                var b = before.Where(x => group == "All" || asset.Inputs[x.Index].Point.Status.ToString() == group).ToArray();
                var a = after.Where(x => group == "All" || asset.Inputs[x.Index].Point.Status.ToString() == group).ToArray();
                double[] bs = b.Select(x => x.Ms).Order().ToArray(), ass = a.Select(x => x.Ms).Order().ToArray();
                report.AppendLine(FormattableString.Invariant($"|{name}/{group}|{b.Sum(x => x.Ms):F3}|{a.Sum(x => x.Ms):F3}|{b.Sum(x => x.Bytes)}|{a.Sum(x => x.Bytes)}|{Percentile(bs,.95):F3}|{Percentile(ass,.95):F3}|{Percentile(bs,.99):F3}|{Percentile(ass,.99):F3}|{bs.LastOrDefault():F3}|{ass.LastOrDefault():F3}|"));
            }
            report.AppendLine($"\nSource: {Path.GetFileName(afterPath)}\n");
        }
        string path = Path.Combine(directory, "Comparison.md");
        File.WriteAllText(path, report.ToString());
        TestContext.WriteLine(report.ToString());
        TestContext.AddResultFile(path);
    }

    private static string Fingerprint(InkBeautifierResult result)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        void Integer(long value) => hash.AppendData(BitConverter.GetBytes(value));
        void Points(IReadOnlyList<InkBeautifierLib.Geometry.InkPoint2D> points)
        {
            Integer(points.Count);
            foreach (var point in points)
            {
                hash.AppendData(BitConverter.GetBytes(point.X));
                hash.AppendData(BitConverter.GetBytes(point.Y));
            }
        }
        Integer(result.StrokeId);
        Integer(result.WasAccepted ? 1 : 0);
        Integer(result.IsCompleted ? 1 : 0);
        Points(result.Outline);
        Integer(result.Outlines.Count);
        foreach (var outline in result.Outlines) Points(outline);
        return Convert.ToHexString(hash.GetHashAndReset());
    }
}
