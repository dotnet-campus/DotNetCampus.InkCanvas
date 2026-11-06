using InkBeautifierLib.Geometry;
using InkBeautifierLib.Models;
using InkBeautifierLib.Processing;

namespace InkBeautifierAvaloniaTests;

public sealed partial class InkBeautifierPerformanceTests
{
    /// <summary>Earlier geometry must remain an independent snapshot across all lifecycle changes.</summary>
    [TestMethod]
    [DataRow("append")]
    [DataRow("reset")]
    [DataRow("other-stroke")]
    [DataRow("same-id-down")]
    [DataRow("rejected-up")]
    public void WhenCachedStrokeChangesThenEarlierResultIsUnchanged(string change)
    {
        var processor = new InkBeautifier();
        InkBeautifierResult snapshot = Populate(processor);
        string expected = Fingerprint(snapshot);
        switch (change)
        {
            case "append": processor.Process(Point(24, InkPointerStatus.Move)); break;
            case "reset": processor.Reset(); Populate(processor); break;
            case "other-stroke": Populate(processor, 2); break;
            case "same-id-down": Populate(processor); break;
            case "rejected-up": processor.Process(Point(23, InkPointerStatus.Up) with { Timestamp = 10_000 }); break;
            default: throw new ArgumentOutOfRangeException(nameof(change));
        }
        Assert.AreEqual(expected, Fingerprint(snapshot));
    }

    /// <summary>Reset and both accepted and filtered Up must release cached state.</summary>
    [TestMethod]
    [DataRow("reset")]
    [DataRow("accepted-up")]
    [DataRow("rejected-up")]
    public void WhenStrokeIsClearedThenReusedIdMatchesFreshProcessor(string change)
    {
        var processor = new InkBeautifier();
        Populate(processor);
        switch (change)
        {
            case "reset": processor.Reset(); break;
            case "accepted-up": processor.Process(Point(24, InkPointerStatus.Up)); break;
            case "rejected-up": processor.Process(Point(23, InkPointerStatus.Up) with { Timestamp = 10_000 }); break;
            default: throw new ArgumentOutOfRangeException(nameof(change));
        }
        Assert.IsFalse(processor.Process(Point(25, InkPointerStatus.Move)).WasAccepted);
        Assert.AreEqual(Fingerprint(Populate(new InkBeautifier())), Fingerprint(Populate(processor)));
    }

    /// <summary>A filtered Up completes geometry without accepting the rejected input.</summary>
    [TestMethod]
    public void WhenUpIsFilteredThenLastGeometryIsReturnedAndStrokeCompletes()
    {
        var processor = new InkBeautifier();
        InkBeautifierResult snapshot = Populate(processor);
        InkBeautifierResult up = processor.Process(Point(23, InkPointerStatus.Up) with { Timestamp = 10_000 });
        Assert.AreEqual((false, true, false), (up.WasAccepted, up.IsCompleted, ReferenceEquals(snapshot.Outlines, up.Outlines)));
    }

    [TestMethod]
    public void WhenRejectedUpCompletesThenSweepReachesLastAcceptedPointWithoutRejectedCoordinates()
    {
        var processor = new InkBeautifier(new() { MaximumWidth = 12, VelocityResponse = 0 });
        processor.Process(new(new(10, 40), 0, .5, 1, InkPointerStatus.Down));
        processor.Process(new(new(50, 40), 10, .5, 1, InkPointerStatus.Move));
        processor.Process(new(new(90, 40), 20, .5, 1, InkPointerStatus.Move));
        InkBeautifierResult result = processor.Process(new(new(500, 500), 20, .5, 1, InkPointerStatus.Up));
        // Sweep cells precede repair caps; the final segment must finish at the accepted endpoint.
        Assert.IsTrue(!result.WasAccepted && result.IsCompleted &&
            result.Outlines.Any(contour => contour.Min(p => p.X) > 70 && contour.Max(p => p.X) > 90) &&
            result.Outlines.SelectMany(contour => contour).All(p => p.X < 110 && p.Y < 60));
    }

    /// <summary>Cached cells cannot be changed through a mutable list cast.</summary>
    [TestMethod]
    public void WhenSweepCellIsExposedThenItsVerticesAreReadOnly()
    {
        InkBeautifierResult result = Populate(new InkBeautifier());
        var cell = (IList<InkPoint2D>)result.Outlines[0];
        Assert.Throws<NotSupportedException>(() => cell[0] = new InkPoint2D(0, 0));
    }

    private static InkBeautifierResult Populate(InkBeautifier processor, long id = 1)
    {
        processor.Process(Point(0, InkPointerStatus.Down) with { StrokeId = id });
        InkBeautifierResult result = null!;
        for (int i = 1; i < 24; i++) result = processor.Process(Point(i, InkPointerStatus.Move) with { StrokeId = id });
        return result;
    }

    private static InkInputPoint Point(int i, InkPointerStatus status) => new(new InkPoint2D(i * 5, Math.Sin(i * .3) * 30), i * 12, .5, 1, status);
}
