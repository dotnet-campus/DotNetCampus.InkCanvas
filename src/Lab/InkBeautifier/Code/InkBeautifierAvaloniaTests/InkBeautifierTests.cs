using Avalonia.Platform;
using InkBeautifierLib;

namespace InkBeautifierAvaloniaTests;

[TestClass]
public sealed class InkBeautifierTests
{
    [TestMethod]
    public void WhenDownIsProcessedThenNoOutlineIsProduced()
    {
        var beautifier = new InkBeautifier();

        InkBeautifierResult result = beautifier.Process(Point(10, 10, 0, InkPointerStatus.Down));

        Assert.IsEmpty(result.Outline);
    }

    [TestMethod]
    public void WhenMoveIntervalIsTooShortThenPointIsRejected()
    {
        var beautifier = new InkBeautifier();
        beautifier.Process(Point(10, 10, 0, InkPointerStatus.Down));

        InkBeautifierResult result = beautifier.Process(Point(30, 10, 3, InkPointerStatus.Move));

        Assert.IsFalse(result.WasAccepted);
    }

    [TestMethod]
    public void WhenStrokeContainsCurvesThenClosedOutlineIsProduced()
    {
        var beautifier = new InkBeautifier();
        beautifier.Process(Point(30, 80, 0, InkPointerStatus.Down));
        beautifier.Process(Point(70, 45, 12, InkPointerStatus.Move));
        beautifier.Process(Point(120, 85, 24, InkPointerStatus.Move));
        beautifier.Process(Point(170, 40, 36, InkPointerStatus.Move));

        InkBeautifierResult result = beautifier.Process(Point(210, 75, 48, InkPointerStatus.Up));

        Assert.IsGreaterThan(20, result.Outline.Count);
    }

    [TestMethod]
    public void WhenTwoStrokesOverlapThenTheirStateRemainsIndependent()
    {
        var beautifier = new InkBeautifier();
        beautifier.Process(Point(10, 10, 0, InkPointerStatus.Down, 1));
        beautifier.Process(Point(100, 100, 0, InkPointerStatus.Down, 2));
        beautifier.Process(Point(30, 10, 10, InkPointerStatus.Move, 1));

        InkBeautifierResult secondStroke = beautifier.Process(Point(120, 100, 10, InkPointerStatus.Up, 2));

        Assert.AreEqual(2L, secondStroke.StrokeId);
    }

    private static InkInputPoint Point(
        double x,
        double y,
        long timestamp,
        InkPointerStatus status,
        long strokeId = 1) => new(new InkPoint2D(x, y), timestamp, 0.5, strokeId, status);
}