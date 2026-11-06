using System.Security.Cryptography;
using InkBeautifierLib.Geometry;
using InkBeautifierLib.Models;
using InkBeautifierLib.Processing;

namespace InkBeautifierAvaloniaTests;

public sealed partial class InkBeautifierPerformanceTests
{
    /// <summary>Checks snapshots across later geometry, interleaved strokes, reset and rejected completion.</summary>
    [TestMethod]
    [DataRow("append")]
    [DataRow("reset")]
    [DataRow("other-stroke")]
    [DataRow("rejected-up")]
    public void WhenCachedStrokeChangesThenEarlierResultIsUnchanged(string operation)
    {
        var beautifier = new InkBeautifier();
        InkInputPoint Point(double x, double y, long time, InkPointerStatus status, long id = 1) =>
            new(new InkPoint2D(x, y), time, .5, id, status);
        beautifier.Process(Point(30, 80, 0, InkPointerStatus.Down));
        beautifier.Process(Point(70, 45, 12, InkPointerStatus.Move));
        InkBeautifierResult snapshot = beautifier.Process(Point(120, 85, 24, InkPointerStatus.Move));
        string Fingerprint(InkBeautifierResult result)
        {
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            AppendHash(hash, result);
            return Convert.ToHexString(hash.GetHashAndReset());
        }
        string before = Fingerprint(snapshot);
        switch (operation)
        {
            case "append":
                beautifier.Process(Point(170, 40, 36, InkPointerStatus.Move));
                beautifier.Process(Point(210, 75, 48, InkPointerStatus.Up));
                break;
            case "reset":
                beautifier.Reset();
                beautifier.Process(Point(30, 80, 0, InkPointerStatus.Down));
                beautifier.Process(Point(170, 40, 36, InkPointerStatus.Move));
                break;
            case "other-stroke":
                beautifier.Process(Point(30, 80, 0, InkPointerStatus.Down, 2));
                beautifier.Process(Point(170, 40, 36, InkPointerStatus.Move, 2));
                break;
            case "rejected-up":
                beautifier.Process(Point(120, 85, 100, InkPointerStatus.Up));
                beautifier.Process(Point(30, 80, 0, InkPointerStatus.Down));
                beautifier.Process(Point(170, 40, 36, InkPointerStatus.Move));
                break;
        }
        Assert.AreEqual(before, Fingerprint(snapshot));
    }

    /// <summary>Checks cache lifecycle against a fresh processor after cleanup.</summary>
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void WhenStrokeIsClearedThenReusedIdMatchesFreshProcessor(bool reset)
    {
        ReplayAsset asset = Load(Path.Combine(AppContext.BaseDirectory, "TestAssets", "Ink2.txt"));
        var reused = new InkBeautifier(asset.Options);
        foreach (var input in asset.Inputs) reused.Process(input.Point);
        if (reset) reused.Reset();
        var fresh = new InkBeautifier(asset.Options);
        using var expected = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        using var actual = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var input in asset.Inputs)
        {
            AppendHash(expected, fresh.Process(input.Point));
            AppendHash(actual, reused.Process(input.Point));
        }
        Assert.AreEqual(Convert.ToHexString(expected.GetHashAndReset()), Convert.ToHexString(actual.GetHashAndReset()));
    }
}
