using System.Globalization;
using System.Xml.Linq;
using Avalonia.Platform;
using InkBeautifierLib.Configuration;
using InkBeautifierLib.Geometry;
using InkBeautifierLib.Models;
using InkBeautifierLib.Processing;

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
    public void WhenStrokeContainsCurvesThenRenderOutlinesContainSweepCells()
    {
        var beautifier = new InkBeautifier();
        beautifier.Process(Point(30, 80, 0, InkPointerStatus.Down));
        beautifier.Process(Point(70, 45, 12, InkPointerStatus.Move));
        beautifier.Process(Point(120, 85, 24, InkPointerStatus.Move));
        beautifier.Process(Point(170, 40, 36, InkPointerStatus.Move));

        InkBeautifierResult result = beautifier.Process(Point(210, 75, 48, InkPointerStatus.Up));

        Assert.IsGreaterThan(1, result.Outlines.Count);
    }

    [TestMethod]
    public void WhenStrokeContainsCurvesThenSelfIntersectingCompatibilityOutlineIsNotRendered()
    {
        var beautifier = new InkBeautifier();
        beautifier.Process(Point(30, 80, 0, InkPointerStatus.Down));
        beautifier.Process(Point(70, 45, 12, InkPointerStatus.Move));
        beautifier.Process(Point(120, 85, 24, InkPointerStatus.Move));
        beautifier.Process(Point(170, 40, 36, InkPointerStatus.Move));

        InkBeautifierResult result = beautifier.Process(Point(210, 75, 48, InkPointerStatus.Up));

        Assert.IsFalse(result.Outlines.Any(outline => ReferenceEquals(outline, result.Outline)));
    }

    [TestMethod]
    public void WhenFourthProblemStrokeIsProcessedThenCompatibilityOutlineIsNotRendered()
    {
        XDocument document = XDocument.Load(Path.Combine(AppContext.BaseDirectory, "TestAssets", "InkProblem-4.txt"));
        XElement[] points = document.Root!
            .Element("ArrayOfInkDataModel")!
            .Elements("InkDataModel")
            .ToArray();
        double minimumX = points.Min(point => ParseDouble(point, "X"));
        double maximumX = points.Max(point => ParseDouble(point, "X"));
        double minimumY = points.Min(point => ParseDouble(point, "Y"));
        double maximumY = points.Max(point => ParseDouble(point, "Y"));
        double scale = Math.Min(940 / Math.Max(1, maximumX - minimumX), 700 / Math.Max(1, maximumY - minimumY));
        var beautifier = new InkBeautifier(new InkBeautifierOptions
        {
            MaximumWidth = 18 / scale,
            VelocityResponse = 1.8 * scale
        });
        long firstTimestamp = ParseTimestamp(points[0]);
        InkBeautifierResult result = points
            .Select(point => new InkInputPoint(
                new InkPoint2D(ParseDouble(point, "X"), ParseDouble(point, "Y")),
                Math.Max(0, ParseTimestamp(point) - firstTimestamp),
                Math.Clamp(ParseDouble(point, "PressureFactor") / 1024, 0, 1),
                1,
                Enum.Parse<InkPointerStatus>(point.Element("Status")!.Value)))
            .Select(beautifier.Process)
            .Last();

        Assert.IsFalse(result.Outlines.Any(outline => ReferenceEquals(outline, result.Outline)));
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

    [TestMethod]
    public void WhenNonTailUpIsTooCloseThenPointIsRejected()
    {
        var beautifier = new InkBeautifier();
        beautifier.Process(Point(0, 0, 0, InkPointerStatus.Down));
        beautifier.Process(Point(20, 0, 10, InkPointerStatus.Move));
        beautifier.Process(Point(40, 0, 20, InkPointerStatus.Move));

        InkBeautifierResult result = beautifier.Process(Point(41, 0, 30, InkPointerStatus.Up));

        Assert.IsFalse(result.WasAccepted);
    }

    [TestMethod]
    public void WhenTailUpIsTooCloseThenPointIsAccepted()
    {
        var beautifier = new InkBeautifier(new InkBeautifierOptions
        {
            MaximumWidth = 12,
            VelocityResponse = 10
        });
        beautifier.Process(Point(0, 40, 0, InkPointerStatus.Down));
        beautifier.Process(Point(20, 40, 10, InkPointerStatus.Move));
        beautifier.Process(Point(40, 40, 20, InkPointerStatus.Move));
        beautifier.Process(Point(60, 40, 30, InkPointerStatus.Move));
        beautifier.Process(Point(80, 40, 40, InkPointerStatus.Move));

        InkBeautifierResult result = beautifier.Process(Point(81, 40, 41, InkPointerStatus.Up));

        Assert.IsTrue(result.WasAccepted);
    }

    [TestMethod]
    public void WhenPointerStatusIsUndefinedThenArgumentExceptionIsThrown()
    {
        var beautifier = new InkBeautifier();
        InkInputPoint input = Point(10, 10, 0, (InkPointerStatus) 99);

        Assert.Throws<ArgumentException>(() => beautifier.Process(input));
    }

    [TestMethod]
    public void WhenZeroDistanceUpEndsTapThenOutlineIsProduced()
    {
        var beautifier = new InkBeautifier();
        beautifier.Process(Point(10, 10, 0, InkPointerStatus.Down));

        InkBeautifierResult result = beautifier.Process(Point(10, 10, 1, InkPointerStatus.Up));

        Assert.IsGreaterThan(20, result.Outline.Count);
    }

    [TestMethod]
    public void WhenTwoPointStrokeEndsThenConnectedOutlineIsProduced()
    {
        var beautifier = new InkBeautifier();
        beautifier.Process(Point(10, 10, 0, InkPointerStatus.Down));

        InkBeautifierResult result = beautifier.Process(Point(11, 10, 1, InkPointerStatus.Up));

        Assert.IsGreaterThan(10, result.Outline.Count);
    }

    [TestMethod]
    public void WhenMoveHasSameTimestampThenPointIsRejected()
    {
        var beautifier = new InkBeautifier();
        beautifier.Process(Point(10, 10, 10, InkPointerStatus.Down));

        InkBeautifierResult result = beautifier.Process(Point(30, 10, 10, InkPointerStatus.Move));

        Assert.IsFalse(result.WasAccepted);
    }

    [TestMethod]
    public void WhenMoveHasSamePositionThenPointIsRejected()
    {
        var beautifier = new InkBeautifier();
        beautifier.Process(Point(10, 10, 0, InkPointerStatus.Down));

        InkBeautifierResult result = beautifier.Process(Point(10, 10, 10, InkPointerStatus.Move));

        Assert.IsFalse(result.WasAccepted);
    }

    [TestMethod]
    public void WhenLongStrokeEndsAtSamePositionThenOutlineRemainsFinite()
    {
        var beautifier = new InkBeautifier();
        beautifier.Process(Point(0, 0, 0, InkPointerStatus.Down));
        beautifier.Process(Point(20, 0, 10, InkPointerStatus.Move));
        beautifier.Process(Point(40, 0, 20, InkPointerStatus.Move));

        InkBeautifierResult result = beautifier.Process(Point(40, 0, 21, InkPointerStatus.Up));

        Assert.IsTrue(result.Outline.All(point => double.IsFinite(point.X) && double.IsFinite(point.Y)));
    }

    [TestMethod]
    public void WhenLongStrokeEndsAtSamePositionThenUpIsExtendedAlongLastDirection()
    {
        var beautifier = new InkBeautifier();
        beautifier.Process(Point(0, 0, 0, InkPointerStatus.Down));
        beautifier.Process(Point(20, 0, 10, InkPointerStatus.Move));
        beautifier.Process(Point(40, 0, 20, InkPointerStatus.Move));

        InkBeautifierResult result = beautifier.Process(Point(40, 0, 30, InkPointerStatus.Up));

        Assert.IsGreaterThan(40, result.Outline.Max(point => point.X));
    }

    [TestMethod]
    public void WhenLongStrokeZeroDistanceUpIsDelayedThenPointIsRejected()
    {
        var beautifier = new InkBeautifier();
        beautifier.Process(Point(0, 0, 0, InkPointerStatus.Down));
        beautifier.Process(Point(20, 0, 10, InkPointerStatus.Move));
        beautifier.Process(Point(40, 0, 20, InkPointerStatus.Move));

        InkBeautifierResult result = beautifier.Process(Point(40, 0, 41, InkPointerStatus.Up));

        Assert.IsFalse(result.WasAccepted);
    }

    [TestMethod]
    public void WhenNonTailUpIsAcceptedThenOutlineReachesStrokeEnd()
    {
        var beautifier = new InkBeautifier();
        beautifier.Process(Point(0, 0, 0, InkPointerStatus.Down));
        beautifier.Process(Point(20, 0, 10, InkPointerStatus.Move));

        InkBeautifierResult result = beautifier.Process(Point(100, 0, 20, InkPointerStatus.Up));

        Assert.IsGreaterThan(100, result.Outline.Max(point => point.X));
    }

    [TestMethod]
    public void WhenTapEndsThenFixedPenCapTemplateIsProduced()
    {
        var beautifier = new InkBeautifier();
        beautifier.Process(Point(10, 10, 0, InkPointerStatus.Down));

        InkBeautifierResult result = beautifier.Process(Point(10, 10, 1, InkPointerStatus.Up));

        Assert.HasCount(43, result.Outline);
    }

    [TestMethod]
    public void WhenTwoPointStrokeEndsThenOutlineCoversBothCenters()
    {
        var beautifier = new InkBeautifier();
        beautifier.Process(Point(10, 10, 0, InkPointerStatus.Down));

        InkBeautifierResult result = beautifier.Process(Point(30, 10, 10, InkPointerStatus.Up));

        Assert.IsTrue(result.Outline.Min(point => point.X) < 10 && result.Outline.Max(point => point.X) > 30);
    }

    [TestMethod]
    public void WhenUpIsRejectedThenStrokeStateIsRemoved()
    {
        var beautifier = new InkBeautifier();
        beautifier.Process(Point(0, 0, 0, InkPointerStatus.Down));
        beautifier.Process(Point(20, 0, 10, InkPointerStatus.Move));
        beautifier.Process(Point(40, 0, 20, InkPointerStatus.Move));
        beautifier.Process(Point(41, 0, 30, InkPointerStatus.Up));

        InkBeautifierResult result = beautifier.Process(Point(60, 0, 40, InkPointerStatus.Move));

        Assert.IsFalse(result.WasAccepted);
    }

    [TestMethod]
    public void WhenStrokeContainsTightLoopThenOutlineCoversLoopArea()
    {
        var beautifier = new InkBeautifier();
        beautifier.Process(Point(20, 50, 0, InkPointerStatus.Down));
        beautifier.Process(Point(50, 20, 10, InkPointerStatus.Move));
        beautifier.Process(Point(80, 50, 20, InkPointerStatus.Move));
        beautifier.Process(Point(50, 80, 30, InkPointerStatus.Move));
        beautifier.Process(Point(25, 50, 40, InkPointerStatus.Move));

        InkBeautifierResult result = beautifier.Process(Point(55, 25, 50, InkPointerStatus.Up));

        double outlineWidth = result.Outline.Max(point => point.X) - result.Outline.Min(point => point.X);
        double outlineHeight = result.Outline.Max(point => point.Y) - result.Outline.Min(point => point.Y);

        Assert.IsTrue(
            result.Outline.All(point => double.IsFinite(point.X) && double.IsFinite(point.Y)) &&
            outlineWidth > 20 &&
            outlineHeight > 20);
    }

    [TestMethod]
    public void WhenStrokeReturnsNearEarlierPathThenEarlierExtentIsPreserved()
    {
        var beautifier = new InkBeautifier();
        beautifier.Process(Point(0, 0, 0, InkPointerStatus.Down));
        beautifier.Process(Point(40, 0, 10, InkPointerStatus.Move));
        beautifier.Process(Point(80, 0, 20, InkPointerStatus.Move));
        beautifier.Process(Point(120, 0, 30, InkPointerStatus.Move));
        beautifier.Process(Point(120, 40, 40, InkPointerStatus.Move));
        beautifier.Process(Point(80, 40, 50, InkPointerStatus.Move));
        beautifier.Process(Point(40, 40, 60, InkPointerStatus.Move));
        beautifier.Process(Point(40, 5, 70, InkPointerStatus.Move));

        InkBeautifierResult result = beautifier.Process(Point(100, 5, 80, InkPointerStatus.Up));

        double outlineWidth = result.Outline.Max(point => point.X) - result.Outline.Min(point => point.X);
        double outlineHeight = result.Outline.Max(point => point.Y) - result.Outline.Min(point => point.Y);

        Assert.IsTrue(outlineWidth > 100 && outlineHeight > 30);
    }

    [TestMethod]
    public void WhenStrokeMovesQuicklyThenOutlineRetainsVisibleThickness()
    {
        var beautifier = new InkBeautifier();
        beautifier.Process(Point(0, 0, 0, InkPointerStatus.Down));
        beautifier.Process(Point(40, 0, 10, InkPointerStatus.Move));
        beautifier.Process(Point(80, 0, 20, InkPointerStatus.Move));

        InkBeautifierResult result = beautifier.Process(Point(120, 0, 30, InkPointerStatus.Move));

        double outlineHeight = result.Outline.Max(point => point.Y) - result.Outline.Min(point => point.Y);

        Assert.IsGreaterThan(1.7, outlineHeight);
    }

    [TestMethod]
    public void WhenTwoPointStrokeEndsThenOutlineRetainsVisibleThickness()
    {
        var beautifier = new InkBeautifier();
        beautifier.Process(Point(10, 10, 0, InkPointerStatus.Down));

        InkBeautifierResult result = beautifier.Process(Point(11, 10, 1, InkPointerStatus.Up));

        double outlineHeight = result.Outline.Max(point => point.Y) - result.Outline.Min(point => point.Y);

        Assert.IsGreaterThan(1, outlineHeight);
    }

    [TestMethod]
    public void WhenTwoPointStrokeEndsAtLastPositionThenOutlineCoversBothCenters()
    {
        var beautifier = new InkBeautifier();
        beautifier.Process(Point(10, 10, 0, InkPointerStatus.Down));
        beautifier.Process(Point(30, 10, 10, InkPointerStatus.Move));

        InkBeautifierResult result = beautifier.Process(Point(30, 10, 11, InkPointerStatus.Up));

        Assert.IsTrue(
            result.Outline.Min(point => point.X) < 10 &&
            result.Outline.Max(point => point.X) > 30 &&
            result.Outline.Max(point => point.Y) - result.Outline.Min(point => point.Y) > 1);
    }

    [TestMethod]
    public void WhenAcceptedUpWidensThenTerminalGrowthRemainsBounded()
    {
        var beautifier = new InkBeautifier(new InkBeautifierOptions
        {
            MaximumWidth = 12,
            VelocityResponse = 10
        });
        beautifier.Process(Point(0, 40, 0, InkPointerStatus.Down));
        beautifier.Process(Point(20, 40, 10, InkPointerStatus.Move));
        beautifier.Process(Point(40, 40, 20, InkPointerStatus.Move));
        beautifier.Process(Point(60, 40, 30, InkPointerStatus.Move));
        beautifier.Process(Point(80, 40, 40, InkPointerStatus.Move));

        InkBeautifierResult result = beautifier.Process(Point(83, 40, 110, InkPointerStatus.Up));

        IReadOnlyList<InkPoint2D> terminalCap = result.Outline
            .Where(point => point.X > 82.9)
            .ToArray();
        double terminalHeight = terminalCap.Max(point => point.Y) - terminalCap.Min(point => point.Y);

        Assert.IsLessThan(2.2, terminalHeight);
    }

    [TestMethod]
    public void WhenNarrowStrokeEndsWithTailThenTerminalCapIsNarrow()
    {
        var beautifier = new InkBeautifier(new InkBeautifierOptions
        {
            MaximumWidth = 12,
            VelocityResponse = 10
        });
        beautifier.Process(Point(0, 40, 0, InkPointerStatus.Down));
        beautifier.Process(Point(20, 40, 10, InkPointerStatus.Move));
        beautifier.Process(Point(40, 40, 20, InkPointerStatus.Move));
        beautifier.Process(Point(60, 40, 30, InkPointerStatus.Move));
        beautifier.Process(Point(80, 40, 40, InkPointerStatus.Move));

        InkBeautifierResult result = beautifier.Process(Point(81, 40, 41, InkPointerStatus.Up));

        IReadOnlyList<InkPoint2D> terminalCap = result.Outline
            .Where(point => point.X > 80.95)
            .ToArray();
        double terminalHeight = terminalCap.Max(point => point.Y) - terminalCap.Min(point => point.Y);

        Assert.IsLessThan(0.2, terminalHeight);
    }

    [TestMethod]
    public void WhenShortNarrowStrokeEndsUpwardThenTerminalCapIsNarrow()
    {
        var beautifier = new InkBeautifier(new InkBeautifierOptions
        {
            MaximumWidth = 12,
            VelocityResponse = 10
        });
        beautifier.Process(Point(0, 40, 0, InkPointerStatus.Down));
        beautifier.Process(Point(20, 40, 10, InkPointerStatus.Move));
        beautifier.Process(Point(40, 40, 20, InkPointerStatus.Move));
        beautifier.Process(Point(60, 40, 30, InkPointerStatus.Move));

        InkBeautifierResult result = beautifier.Process(Point(62, 37, 31, InkPointerStatus.Up));

        IReadOnlyList<InkPoint2D> terminalCap = result.Outline
            .Where(point => point.X > 61.9)
            .ToArray();
        double terminalHeight = terminalCap.Max(point => point.Y) - terminalCap.Min(point => point.Y);

        Assert.IsLessThan(0.2, terminalHeight);
    }

    [TestMethod]
    public void WhenStrokeFoldsBackTightlyThenOutlineRemainsLocallyBounded()
    {
        var beautifier = new InkBeautifier(new InkBeautifierOptions
        {
            MaximumWidth = 12,
            VelocityResponse = 0
        });
        beautifier.Process(Point(0, 20, 0, InkPointerStatus.Down));
        beautifier.Process(Point(30, 20, 10, InkPointerStatus.Move));
        beautifier.Process(Point(60, 20, 20, InkPointerStatus.Move));
        beautifier.Process(Point(32, 24, 30, InkPointerStatus.Move));
        beautifier.Process(Point(58, 28, 40, InkPointerStatus.Move));

        InkBeautifierResult result = beautifier.Process(Point(34, 32, 50, InkPointerStatus.Up));

        double outlineHeight = result.Outline.Max(point => point.Y) - result.Outline.Min(point => point.Y);

        Assert.IsLessThan(25, outlineHeight);
    }

    [TestMethod]
    public void WhenStrokeNearlyReversesThenCornerExpansionRemainsBounded()
    {
        var beautifier = new InkBeautifier(new InkBeautifierOptions
        {
            MaximumWidth = 12,
            VelocityResponse = 0
        });
        beautifier.Process(Point(0, 20, 0, InkPointerStatus.Down));
        beautifier.Process(Point(40, 20, 10, InkPointerStatus.Move));
        beautifier.Process(Point(80, 20, 20, InkPointerStatus.Move));
        beautifier.Process(Point(42, 26, 30, InkPointerStatus.Move));
        beautifier.Process(Point(76, 32, 40, InkPointerStatus.Move));

        InkBeautifierResult result = beautifier.Process(Point(44, 38, 50, InkPointerStatus.Up));

        double outlineHeight = result.Outline.Max(point => point.Y) - result.Outline.Min(point => point.Y);

        Assert.IsLessThan(32, outlineHeight);
    }

    [TestMethod]
    public void WhenStrokeDirectionHasMinorJitterThenOutlineWidthRemainsStable()
    {
        var beautifier = new InkBeautifier(new InkBeautifierOptions
        {
            MaximumWidth = 12,
            VelocityResponse = 0
        });
        beautifier.Process(Point(0, 20, 0, InkPointerStatus.Down));
        beautifier.Process(Point(20, 20.4, 10, InkPointerStatus.Move));
        beautifier.Process(Point(40, 19.7, 20, InkPointerStatus.Move));
        beautifier.Process(Point(60, 20.5, 30, InkPointerStatus.Move));
        beautifier.Process(Point(80, 19.8, 40, InkPointerStatus.Move));

        InkBeautifierResult result = beautifier.Process(Point(100, 20, 50, InkPointerStatus.Up));

        double outlineHeight = result.Outline.Max(point => point.Y) - result.Outline.Min(point => point.Y);

        Assert.IsLessThan(12, outlineHeight);
    }

    [TestMethod]
    public void WhenStrokeTurnsSharplyThenCornerExpansionIsBounded()
    {
        var beautifier = new InkBeautifier(new InkBeautifierOptions
        {
            MaximumWidth = 12,
            VelocityResponse = 0
        });
        beautifier.Process(Point(0, 0, 0, InkPointerStatus.Down));
        beautifier.Process(Point(30, 0, 10, InkPointerStatus.Move));
        beautifier.Process(Point(60, 0, 20, InkPointerStatus.Move));
        beautifier.Process(Point(62, 30, 30, InkPointerStatus.Move));

        InkBeautifierResult result = beautifier.Process(Point(64, 60, 40, InkPointerStatus.Up));

        double maximumX = result.Outline.Max(point => point.X);

        Assert.IsLessThan(71, maximumX);
    }

    [TestMethod]
    public void WhenStrokeRepeatedlyCrossesThenOutlineAreaRemainsBounded()
    {
        var beautifier = new InkBeautifier(new InkBeautifierOptions
        {
            MaximumWidth = 12,
            VelocityResponse = 0
        });
        beautifier.Process(Point(0, 40, 0, InkPointerStatus.Down));
        beautifier.Process(Point(30, 10, 10, InkPointerStatus.Move));
        beautifier.Process(Point(60, 40, 20, InkPointerStatus.Move));
        beautifier.Process(Point(30, 70, 30, InkPointerStatus.Move));
        beautifier.Process(Point(0, 40, 40, InkPointerStatus.Move));
        beautifier.Process(Point(30, 40, 50, InkPointerStatus.Move));
        beautifier.Process(Point(60, 10, 60, InkPointerStatus.Move));
        beautifier.Process(Point(30, 40, 70, InkPointerStatus.Move));

        InkBeautifierResult result = beautifier.Process(Point(60, 70, 80, InkPointerStatus.Up));

        Assert.IsLessThan(1_500, GetPolygonArea(result.Outline));
    }

    [TestMethod]
    public void WhenStrokeRepeatedlyCrossesThenLocalThicknessRemainsBounded()
    {
        var beautifier = new InkBeautifier(new InkBeautifierOptions
        {
            MaximumWidth = 12,
            VelocityResponse = 0
        });
        beautifier.Process(Point(0, 40, 0, InkPointerStatus.Down));
        beautifier.Process(Point(30, 10, 10, InkPointerStatus.Move));
        beautifier.Process(Point(60, 40, 20, InkPointerStatus.Move));
        beautifier.Process(Point(30, 70, 30, InkPointerStatus.Move));
        beautifier.Process(Point(0, 40, 40, InkPointerStatus.Move));
        beautifier.Process(Point(30, 40, 50, InkPointerStatus.Move));
        beautifier.Process(Point(60, 10, 60, InkPointerStatus.Move));
        beautifier.Process(Point(30, 40, 70, InkPointerStatus.Move));

        InkBeautifierResult result = beautifier.Process(Point(60, 70, 80, InkPointerStatus.Up));

        Assert.IsLessThan(24, GetMaximumVerticalCrossSectionThickness(result.Outline, 26, 34, 40));
    }

    [TestMethod]
    public void WhenStrokeRepeatedlyCrossesThenCrossingRemainsConnected()
    {
        var beautifier = new InkBeautifier(new InkBeautifierOptions
        {
            MaximumWidth = 12,
            VelocityResponse = 0
        });
        beautifier.Process(Point(0, 40, 0, InkPointerStatus.Down));
        beautifier.Process(Point(30, 10, 10, InkPointerStatus.Move));
        beautifier.Process(Point(60, 40, 20, InkPointerStatus.Move));
        beautifier.Process(Point(30, 70, 30, InkPointerStatus.Move));
        beautifier.Process(Point(0, 40, 40, InkPointerStatus.Move));
        beautifier.Process(Point(30, 40, 50, InkPointerStatus.Move));
        beautifier.Process(Point(60, 10, 60, InkPointerStatus.Move));
        beautifier.Process(Point(30, 40, 70, InkPointerStatus.Move));

        InkBeautifierResult result = beautifier.Process(Point(60, 70, 80, InkPointerStatus.Up));

        Assert.IsGreaterThan(1, GetMaximumVerticalCrossSectionThickness(result.Outline, 26, 34, 40));
    }

    [TestMethod]
    public void WhenStrokeEndsInsideEarlierSweepThenTerminalThicknessRemainsBounded()
    {
        var beautifier = new InkBeautifier(new InkBeautifierOptions
        {
            MaximumWidth = 12,
            VelocityResponse = 0
        });
        beautifier.Process(Point(0, 40, 0, InkPointerStatus.Down));
        beautifier.Process(Point(20, 10, 10, InkPointerStatus.Move));
        beautifier.Process(Point(50, 0, 20, InkPointerStatus.Move));
        beautifier.Process(Point(80, 10, 30, InkPointerStatus.Move));
        beautifier.Process(Point(100, 40, 40, InkPointerStatus.Move));
        beautifier.Process(Point(80, 70, 50, InkPointerStatus.Move));
        beautifier.Process(Point(50, 80, 60, InkPointerStatus.Move));
        beautifier.Process(Point(20, 70, 70, InkPointerStatus.Move));
        beautifier.Process(Point(0, 40, 80, InkPointerStatus.Move));

        InkBeautifierResult result = beautifier.Process(Point(20, 40, 90, InkPointerStatus.Up));

        Assert.IsLessThan(16, GetMaximumVerticalCrossSectionThickness(result.Outline, 16, 24, 40));
    }

    [TestMethod]
    public void WhenTurningSegmentEntersEarlierCapThenLocalThicknessRemainsBounded()
    {
        var beautifier = new InkBeautifier(new InkBeautifierOptions
        {
            MaximumWidth = 12,
            VelocityResponse = 0
        });
        beautifier.Process(Point(30, 40, 0, InkPointerStatus.Down));
        beautifier.Process(Point(60, 40, 10, InkPointerStatus.Move));
        beautifier.Process(Point(80, 70, 20, InkPointerStatus.Move));
        beautifier.Process(Point(30, 70, 30, InkPointerStatus.Move));
        beautifier.Process(Point(30, 45, 40, InkPointerStatus.Move));

        InkBeautifierResult result = beautifier.Process(Point(60, 10, 50, InkPointerStatus.Up));

        Assert.IsLessThan(9, GetMaximumVerticalCrossSectionThickness(result.Outline, 26, 34, 40));
    }

    [TestMethod]
    public void WhenStrokeClosesALoopThenInteriorIsNotFilled()
    {
        var beautifier = new InkBeautifier(new InkBeautifierOptions
        {
            MaximumWidth = 12,
            VelocityResponse = 0
        });
        beautifier.Process(Point(0, 40, 0, InkPointerStatus.Down));
        beautifier.Process(Point(20, 10, 10, InkPointerStatus.Move));
        beautifier.Process(Point(50, 0, 20, InkPointerStatus.Move));
        beautifier.Process(Point(80, 10, 30, InkPointerStatus.Move));
        beautifier.Process(Point(100, 40, 40, InkPointerStatus.Move));
        beautifier.Process(Point(80, 70, 50, InkPointerStatus.Move));
        beautifier.Process(Point(50, 80, 60, InkPointerStatus.Move));
        beautifier.Process(Point(20, 70, 70, InkPointerStatus.Move));
        beautifier.Process(Point(0, 40, 80, InkPointerStatus.Move));

        InkBeautifierResult result = beautifier.Process(Point(20, 40, 90, InkPointerStatus.Up));

        Assert.IsLessThan(4_000, GetPolygonArea(result.Outline));
    }

    private static double ParseDouble(XElement point, string elementName)
    {
        return double.Parse(point.Element(elementName)!.Value, CultureInfo.InvariantCulture);
    }

    private static long ParseTimestamp(XElement point)
    {
        return (long) System.Xml.XmlConvert.ToTimeSpan(point.Element("Time")!.Value).TotalMilliseconds;
    }

    private static double GetPolygonArea(IReadOnlyList<InkPoint2D> polygon)
    {
        double twiceArea = 0;
        for (int index = 0; index < polygon.Count; index++)
        {
            InkPoint2D current = polygon[index];
            InkPoint2D next = polygon[(index + 1) % polygon.Count];
            twiceArea += (current.X * next.Y) - (next.X * current.Y);
        }

        return Math.Abs(twiceArea) / 2;
    }

    private static double GetMaximumVerticalCrossSectionThickness(
        IReadOnlyList<InkPoint2D> polygon,
        double minimumX,
        double maximumX,
        double centerY)
    {
        double maximumThickness = 0;
        for (double x = minimumX; x <= maximumX; x += 0.25)
        {
            var intersections = new List<double>();
            for (int index = 0; index < polygon.Count; index++)
            {
                InkPoint2D start = polygon[index];
                InkPoint2D end = polygon[(index + 1) % polygon.Count];
                if ((start.X <= x && end.X > x) || (end.X <= x && start.X > x))
                {
                    double ratio = (x - start.X) / (end.X - start.X);
                    intersections.Add(start.Y + ((end.Y - start.Y) * ratio));
                }
            }

            intersections.Sort();
            for (int index = 0; index + 1 < intersections.Count; index += 2)
            {
                double minimumY = intersections[index];
                double maximumY = intersections[index + 1];
                if (minimumY <= centerY && centerY <= maximumY)
                {
                    maximumThickness = Math.Max(maximumThickness, maximumY - minimumY);
                }
            }
        }

        return maximumThickness;
    }

    private static InkInputPoint Point(
        double x,
        double y,
        long timestamp,
        InkPointerStatus status,
        long strokeId = 1) => new(new InkPoint2D(x, y), timestamp, 0.5, strokeId, status);
}