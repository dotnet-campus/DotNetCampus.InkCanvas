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

    /// <summary>A filtered Up completes the stroke while retaining its last geometry.</summary>
    [TestMethod]
    public void WhenUpIsFilteredThenLastGeometryIsReturnedAndStrokeCompletes()
    {
        var processor = new InkBeautifier();
        InkBeautifierResult snapshot = Populate(processor);
        InkBeautifierResult up = processor.Process(Point(23, InkPointerStatus.Up) with { Timestamp = 10_000 });
        Assert.AreEqual((false, true, snapshot.Outline, snapshot.Outlines), (up.WasAccepted, up.IsCompleted, up.Outline, up.Outlines));
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
