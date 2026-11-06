using System.Security.Cryptography;
using InkBeautifierLib.Models;
using InkBeautifierLib.Processing;

namespace InkBeautifierAvaloniaTests;

public sealed partial class InkBeautifierPerformanceTests
{
    /// <summary>Discovers every recorded asset in ordinal order.</summary>
    public static IEnumerable<object[]> RecordedAssets() => Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "TestAssets"), "*.txt")
        .OrderBy(Path.GetFileName, StringComparer.Ordinal).Select(path => new object[] { Path.GetFileName(path) });

    /// <summary>Every replay completes and releases state without relying on external golden data.</summary>
    [TestMethod]
    [DynamicData(nameof(RecordedAssets))]
    public void WhenRecordedInputsAreReplayedThenGeometryIsFiniteAndUpReleasesState(string assetName)
    {
        ReplayAsset asset = Load(Path.Combine(AppContext.BaseDirectory, "TestAssets", assetName));
        var processor = new InkBeautifier(asset.Options);
        foreach (ReplayInput input in asset.Inputs)
        {
            InkBeautifierResult result = processor.Process(input.Point);
            Assert.AreEqual(input.Point.Status == InkPointerStatus.Up, result.IsCompleted);
            Assert.IsTrue(result.Outlines.SelectMany(points => points).All(point => double.IsFinite(point.X) && double.IsFinite(point.Y)));
            if (result.IsCompleted)
            {
                Assert.IsFalse(processor.Process(input.Point with { Status = InkPointerStatus.Move }).WasAccepted);
            }
        }
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
