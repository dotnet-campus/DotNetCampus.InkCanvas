using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using InkBeautifierLib.Processing;

namespace InkBeautifierAvaloniaTests;

public sealed partial class InkBeautifierPerformanceTests
{
    /// <summary>Summarizes persisted original and latest optimized per-call measurements.</summary>
    [TestMethod]
    [TestCategory("GoldenSummary")]
    public void WhenGoldenMeasurementsExistThenComparisonIsWritten()
    {
        string directory = Path.Combine(FindRepository(), "artifacts", "InkBeautifierGolden");
        var report = new StringBuilder("| Asset | calls | before ms | after ms | before bytes | after bytes |\n|---|---:|---:|---:|---:|---:|\n");
        string[] baselines = Directory.GetFiles(directory, "*.before.csv").Order().ToArray();
        Assert.HasCount(DiagnosticAssets().Count(), baselines);
        foreach (string before in baselines)
        {
            string name = Path.GetFileName(before)[..^".before.csv".Length];
            string after = Directory.GetFiles(directory, name + ".after-*.csv").Order(StringComparer.Ordinal).Last();
            string[][] a = File.ReadAllLines(before).Skip(1).Select(l => l.Split(',')).ToArray();
            string[][] b = File.ReadAllLines(after).Skip(1).Select(l => l.Split(',')).ToArray();
            CollectionAssert.AreEqual(a.Select(r => r[0]).ToArray(), b.Select(r => r[0]).ToArray());
            double Time(string[][] rows) => rows.Sum(r => double.Parse(r[1], CultureInfo.InvariantCulture));
            long Bytes(string[][] rows) => rows.Sum(r => long.Parse(r[2], CultureInfo.InvariantCulture));
            report.AppendLine(FormattableString.Invariant($"|{name}|{a.Length}|{Time(a):F3}|{Time(b):F3}|{Bytes(a)}|{Bytes(b)}|"));
        }
        report.AppendLine("\n| Asset/status | calls | before ms | after ms | before p95 | after p95 | before p99 | after p99 | before max | after max | before bytes | after bytes |\n|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|");
        foreach (string before in baselines)
        {
            string name = Path.GetFileName(before)[..^".before.csv".Length];
            string after = Directory.GetFiles(directory, name + ".after-*.csv").Order(StringComparer.Ordinal).Last();
            ReplayAsset asset = Load(Path.Combine(AppContext.BaseDirectory, "TestAssets", name));
            string[][] a = File.ReadAllLines(before).Skip(1).Select(l => l.Split(',')).ToArray();
            string[][] b = File.ReadAllLines(after).Skip(1).Select(l => l.Split(',')).ToArray();
            Assert.HasCount(asset.Inputs.Length, a);
            foreach (string status in new[] { "Move", "Up" })
            {
                int[] indices = Enumerable.Range(0, asset.Inputs.Length).Where(i => asset.Inputs[i].Point.Status.ToString() == status).ToArray();
                double[] Times(string[][] rows) => indices.Select(i => double.Parse(rows[i][1], CultureInfo.InvariantCulture)).Order().ToArray();
                long Bytes(string[][] rows) => indices.Sum(i => long.Parse(rows[i][2], CultureInfo.InvariantCulture));
                double[] x = Times(a), y = Times(b);
                double Percentile(double[] values, double p) => values[(int)Math.Ceiling(values.Length * p) - 1];
                report.AppendLine(FormattableString.Invariant($"|{name}/{status}|{indices.Length}|{x.Sum():F6}|{y.Sum():F6}|{Percentile(x, .95):F6}|{Percentile(y, .95):F6}|{Percentile(x, .99):F6}|{Percentile(y, .99):F6}|{x[^1]:F6}|{y[^1]:F6}|{Bytes(a)}|{Bytes(b)}|"));
            }
        }
        File.WriteAllText(Path.Combine(directory, "Comparison.md"), report.ToString());
        TestContext.WriteLine(report.ToString());
    }

    /// <summary>Persists original per-call bit fingerprints and compares subsequent binaries against them.</summary>
    [TestMethod]
    [DynamicData(nameof(DiagnosticAssets))]
    [TestCategory("Golden")]
    public void WhenRecordedAssetIsReplayedThenEveryCallMatchesOriginalBinary(string name)
    {
        ReplayAsset asset = Load(Path.Combine(AppContext.BaseDirectory, "TestAssets", name));
        string directory = Path.Combine(FindRepository(), "artifacts", "InkBeautifierGolden");
        Directory.CreateDirectory(directory);
        string baseline = Path.Combine(directory, name + ".golden.csv");
        bool recording = !File.Exists(baseline);
        string[]? expected = recording ? null : File.ReadAllLines(baseline);
        var fingerprints = new List<string>();
        var timings = new StringBuilder("input,ms,bytes\n");
        var beautifier = new InkBeautifier(asset.Options);
        for (int i = 0; i < asset.Inputs.Length; i++)
        {
            long bytes = GC.GetAllocatedBytesForCurrentThread();
            long start = Stopwatch.GetTimestamp();
            var result = beautifier.Process(asset.Inputs[i].Point);
            long elapsed = Stopwatch.GetTimestamp() - start;
            bytes = GC.GetAllocatedBytesForCurrentThread() - bytes;
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            AppendHash(hash, result);
            string row = $"{i},{result.StrokeId},{result.WasAccepted},{result.IsCompleted},{result.Outline.Count},{result.Outlines.Count},{Convert.ToHexString(hash.GetHashAndReset())}";
            fingerprints.Add(row);
            if (expected is not null) Assert.AreEqual(expected[i], row, $"{name}: input {i}");
            timings.AppendLine(FormattableString.Invariant($"{i},{elapsed * 1000d / Stopwatch.Frequency:F6},{bytes}"));
        }
        if (expected is not null) Assert.HasCount(expected.Length, fingerprints);
        if (recording)
        {
            using var stream = new FileStream(baseline, FileMode.CreateNew);
            using var writer = new StreamWriter(stream);
            foreach (string row in fingerprints) writer.WriteLine(row);
        }
        string measurements = Path.Combine(directory, name + (recording ? ".before.csv" : ".after-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff", CultureInfo.InvariantCulture) + ".csv"));
        File.WriteAllText(measurements, timings.ToString());
        TestContext.WriteLine($"{name}: {(recording ? "recorded original" : "matched original")} {fingerprints.Count} calls; {measurements}");
    }
}
