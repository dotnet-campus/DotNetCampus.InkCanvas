using InkBeautifierLib.Geometry;
using InkBeautifierLib.Models;

namespace InkBeautifierLib.Processing;

public sealed partial class InkBeautifier
{
    private const double MinimumWidthRatio = 0.2;
    private const double MinimumTemplateArcTurn = 0.05;
    private const double MinimumTemplateArcAlignment = -0.9;

    private bool TryAddPoint(StrokeState stroke, InkInputPoint input)
    {
        RawPoint last = stroke.RawPoints[^1];
        InkPoint2D position = input.Position;
        double distance = InkPoint2D.Distance(last.Position, position);
        long elapsed = input.Timestamp - last.Timestamp;
        bool isUp = input.Status == InkPointerStatus.Up;
        bool isShortStrokeUp = isUp && stroke.RawPoints.Count <= 2;

        if (!isUp && (elapsed < Options.MinimumTimeInterval || distance < double.Epsilon))
        {
            return false;
        }

        if (isUp && distance < double.Epsilon)
        {
            if (stroke.RawPoints.Count == 1)
            {
                stroke.IsTap = true;
                return true;
            }

            if (stroke.RawPoints.Count == 2)
            {
                return true;
            }

            if (elapsed > 20)
            {
                return false;
            }

            RawPoint previous = stroke.RawPoints[^2];
            position = new InkPoint2D
            (
                last.Position.X + (0.6 * (last.Position.X - previous.Position.X)),
                last.Position.Y + (0.6 * (last.Position.Y - previous.Position.Y))
            );
            distance = last.DistanceFrom(previous);
        }

        bool isTail = IsTail(stroke.NarrowPointCount, last.Position, position, isUp);
        if ((distance < Options.MinimumDistance || elapsed < Options.MinimumTimeInterval) &&
            !isShortStrokeUp &&
            !isTail)
        {
            return false;
        }

        if (elapsed <= 0)
        {
            return false;
        }

        double speed = distance / elapsed;
        if (isUp && !isShortStrokeUp && speed < 0.03)
        {
            return false;
        }

        double pressure = input.Pressure is > 0 and <= 1 ? input.Pressure : 0.5;
        double velocityWidth = Options.MaximumWidth * Math.Exp(-Options.VelocityResponse * speed);
        double pressureScale = 1 - Options.PressureInfluence + (Options.PressureInfluence * pressure * 2);
        double width = Math.Clamp
        (
            velocityWidth * pressureScale,
            Options.MaximumWidth * MinimumWidthRatio,
            Options.MaximumWidth
        );

        var point = new RawPoint(position, input.Timestamp, speed, speed, width / 2, width / 2);
        stroke.RawPoints.Add(point);

        if (isUp && stroke.RawPoints.Count > 2)
        {
            RawPoint previous = stroke.RawPoints[^2];
            double maximumHalfWidth = previous.HalfWidth * 1.3;
            if (point.HalfWidth > maximumHalfWidth)
            {
                point = point with { RawHalfWidth = maximumHalfWidth, HalfWidth = maximumHalfWidth };
                stroke.RawPoints[^1] = point;
            }
        }

        if (stroke.RawPoints.Count == 2)
        {
            RawPoint second = stroke.RawPoints[1];
            stroke.RawPoints[0] = stroke.RawPoints[0] with
            {
                RawSpeed = second.RawSpeed,
                SmoothSpeed = second.SmoothSpeed,
                RawHalfWidth = second.RawHalfWidth,
                HalfWidth = second.HalfWidth
            };
        }

        SmoothCurrentSpeed(stroke.RawPoints);

        if (!isTail)
        {
            SmoothCurrentWidth(stroke.RawPoints);
        }

        RawPoint current = stroke.RawPoints[^1];
        if (stroke.RawPoints.Count > 2)
        {
            if (current.HalfWidth / Options.MaximumWidth < 0.4)
            {
                stroke.NarrowPointCount++;
            }
            else
            {
                stroke.NarrowPointCount = 0;
            }
        }

        if (isTail && current.HalfWidth / Options.MaximumWidth < 0.4)
        {
            stroke.RawPoints[^1] = current with { HalfWidth = 0.03 };
        }

        AppendBezierSamples(stroke, isUp);
        return true;
    }

    private static void SmoothCurrentSpeed(List<RawPoint> points)
    {
        int count = points.Count;
        if (count == 2)
        {
            double speed = (0.6 * points[0].RawSpeed) + (0.4 * points[1].RawSpeed);
            points[1] = points[1] with { SmoothSpeed = speed };
        }
        else if (count == 3)
        {
            double speed = (0.3 * points[0].SmoothSpeed) + (0.3 * points[1].SmoothSpeed) +
                           (0.4 * points[2].RawSpeed);
            points[2] = points[2] with { SmoothSpeed = speed };
        }
        else if (count >= 4)
        {
            double speed = (0.1 * points[^4].SmoothSpeed) + (0.2 * points[^3].SmoothSpeed) +
                           (0.3 * points[^2].SmoothSpeed) + (0.4 * points[^1].RawSpeed);
            points[^1] = points[^1] with { SmoothSpeed = speed };
        }
    }

    private static void SmoothCurrentWidth(List<RawPoint> points)
    {
        int count = points.Count;
        if (count == 2)
        {
            double halfWidth = (0.6 * points[0].RawHalfWidth) + (0.4 * points[1].RawHalfWidth);
            points[0] = points[0] with { HalfWidth = halfWidth };
            points[1] = points[1] with { HalfWidth = halfWidth };
        }
        else if (count == 3)
        {
            double halfWidth = (0.3 * points[0].HalfWidth) + (0.3 * points[1].HalfWidth) +
                               (0.4 * points[2].RawHalfWidth);
            points[2] = points[2] with { HalfWidth = halfWidth };
        }
        else if (count >= 4)
        {
            double halfWidth = (0.1 * points[^4].HalfWidth) + (0.2 * points[^3].HalfWidth) +
                               (0.3 * points[^2].HalfWidth) + (0.4 * points[^1].RawHalfWidth);
            points[^1] = points[^1] with { HalfWidth = halfWidth };
        }
    }

    private static bool IsTail
    (
        int narrowPointCount,
        InkPoint2D previousPosition,
        InkPoint2D currentPosition,
        bool isUp
    )
    {
        if (!isUp)
        {
            return false;
        }

        double angle = Math.Atan2
        (
            currentPosition.Y - previousPosition.Y,
            currentPosition.X - previousPosition.X
        ) * 180 / Math.PI;
        bool isUpwardDirection = angle is > -155 and < -35;
        return narrowPointCount >= 3 || (isUpwardDirection && narrowPointCount >= 2);
    }

    private static void AppendBezierSamples(StrokeState stroke, bool completesStroke)
    {
        int count = stroke.RawPoints.Count;
        if (count < 3)
        {
            return;
        }

        RawPoint p0 = stroke.Samples.Count == 0 ? stroke.RawPoints[0] : stroke.Samples[^1];
        RawPoint p1 = stroke.RawPoints[^2];
        RawPoint p2 = stroke.RawPoints[^1];
        int interpolationTimes = p1.DistanceFrom(stroke.RawPoints[^3]) switch
        {
            < 10 => 4,
            < 20 => 5,
            < 40 => 6,
            < 60 => 8,
            _ => 10
        };
        int lastIndex = completesStroke ? interpolationTimes : interpolationTimes + 1;
        double interval = (completesStroke ? 1.0 : 0.5) / interpolationTimes;

        for (int index = 0; index <= lastIndex; index++)
        {
            double t = index * interval;
            double oneMinusT = 1 - t;
            double a = oneMinusT * oneMinusT;
            double b = 2 * t * oneMinusT;
            double c = t * t;
            double halfWidth = (a * p0.HalfWidth) + (b * p1.HalfWidth) + (c * p2.HalfWidth);

            var sample = new RawPoint
            (
                new InkPoint2D
                (
                    (a * p0.Position.X) + (b * p1.Position.X) + (c * p2.Position.X),
                    (a * p0.Position.Y) + (b * p1.Position.Y) + (c * p2.Position.Y)
                ),
                p1.Timestamp,
                p1.RawSpeed,
                p1.SmoothSpeed,
                halfWidth,
                halfWidth
            );
            if (stroke.Samples.Count == 0 || sample.DistanceFrom(stroke.Samples[^1]) > 0.01)
            {
                stroke.Samples.Add(sample);
            }
        }
    }

    private StrokeGeometry BuildGeometry(StrokeState stroke, bool completesStroke)
    {
        if (stroke.IsTap || stroke.RawPoints.Count == 1)
        {
            IReadOnlyList<InkPoint2D> tap = PlacePenCap(stroke.RawPoints[0].Position, Options.MaximumWidth * 0.4);
            return new StrokeGeometry(tap, [tap]);
        }

        if (stroke.RawPoints.Count == 2)
        {
            var candidates = new List<InkPoint2D>(PenCapTemplate.Length * 2);
            foreach (RawPoint point in stroke.RawPoints)
            {
                AddPenCap(candidates, point.Position, point.HalfWidth);
            }

            IReadOnlyList<InkPoint2D> shortStroke = BuildConvexHull(candidates);
            return new StrokeGeometry(shortStroke, [shortStroke]);
        }

        IReadOnlyList<RawPoint> samples = stroke.Samples.Count > 1 ? stroke.Samples : stroke.RawPoints;
        IReadOnlyList<InkPoint2D> templateSweep = BuildTemplateSweepOutline(samples);
        IReadOnlyList<InkPoint2D> outline = templateSweep.Count >= 3 && templateSweep.All
        (static point =>
            double.IsFinite(point.X) && double.IsFinite(point.Y)
        )
            ? templateSweep
            : BuildNormalOffsetOutline(samples);
        outline = RepairRawPointCoverage(outline, stroke.RawPoints, samples, completesStroke);
        IReadOnlyList<IReadOnlyList<InkPoint2D>> outlines = BuildSweepCells(outline, samples);
        return new StrokeGeometry(outline, outlines);
    }

    private static IReadOnlyList<IReadOnlyList<InkPoint2D>> BuildSweepCells
    (
        IReadOnlyList<InkPoint2D> outline,
        IReadOnlyList<RawPoint> samples
    )
    {
        var outlines = new List<IReadOnlyList<InkPoint2D>>(samples.Count - 1);
        double outlineArea = GetSignedArea(outline);
        for (int index = 0; index < samples.Count - 1; index++)
        {
            var candidates = new List<InkPoint2D>(PenCapTemplate.Length * 2);
            AddPenCap(candidates, samples[index].Position, samples[index].HalfWidth);
            AddPenCap(candidates, samples[index + 1].Position, samples[index + 1].HalfWidth);
            IReadOnlyList<InkPoint2D> sweepCell = BuildConvexHull(candidates);
            if (Math.Sign(GetSignedArea(sweepCell)) != Math.Sign(outlineArea))
            {
                sweepCell = sweepCell.Reverse().ToArray();
            }

            outlines.Add(sweepCell);
        }

        return outlines;
    }

    private static IReadOnlyList<InkPoint2D> BuildTemplateSweepOutline(IReadOnlyList<RawPoint> samples)
    {
        var forwardBoundary = new List<InkPoint2D>((samples.Count * 4) + PenCapTemplate.Length);
        var reverseBoundary = new List<InkPoint2D>((samples.Count * 4) + PenCapTemplate.Length);

        InkPoint2D initialTangent = GetDirection(samples[0].Position, samples[1].Position);
        InkPoint2D previousTangent = initialTangent;
        (int initialForwardIndex, int initialReverseIndex) = GetTemplateSupportIndices(previousTangent);
        int previousForwardIndex = initialForwardIndex;
        int previousReverseIndex = initialReverseIndex;
        AppendBoundaryPoint(forwardBoundary, PlaceTemplatePoint(samples[0], PenCapTemplate[previousForwardIndex]));
        AppendBoundaryPoint(reverseBoundary, PlaceTemplatePoint(samples[0], PenCapTemplate[previousReverseIndex]));

        for (int index = 0; index < samples.Count - 1; index++)
        {
            RawPoint sample = samples[index];
            RawPoint nextSample = samples[index + 1];
            InkPoint2D tangent = index == 0
                ? previousTangent
                : GetDirection(sample.Position, nextSample.Position);
            (int forwardIndex, int reverseIndex) = GetTemplateSupportIndices(tangent);
            bool overlapsEarlierSweep = index > 0 && OverlapsEarlierSweep(samples, index);

            double turn = index > 0 ? Cross(previousTangent, tangent) : 0;
            if (index > 0 && !overlapsEarlierSweep)
            {
                double alignment = Dot(previousTangent, tangent);
                if (alignment > MinimumTemplateArcAlignment && turn < -MinimumTemplateArcTurn)
                {
                    AddBoundaryTemplateArc
                    (
                        forwardBoundary,
                        sample,
                        previousForwardIndex,
                        forwardIndex,
                        new InkPoint2D(-tangent.Y, tangent.X)
                    );
                }
                else if (alignment > MinimumTemplateArcAlignment && turn > MinimumTemplateArcTurn)
                {
                    AddBoundaryTemplateArc
                    (
                        reverseBoundary,
                        sample,
                        previousReverseIndex,
                        reverseIndex,
                        new InkPoint2D(tangent.Y, -tangent.X)
                    );
                }
            }

            InkPoint2D forwardPoint = PlaceTemplatePoint(nextSample, PenCapTemplate[forwardIndex]);
            InkPoint2D reversePoint = PlaceTemplatePoint(nextSample, PenCapTemplate[reverseIndex]);
            if (overlapsEarlierSweep)
            {
                if (turn < -MinimumTemplateArcTurn)
                {
                    TrimOuterBoundaryAtEarlierIntersection(forwardBoundary, forwardPoint);
                }
                else if (turn > MinimumTemplateArcTurn)
                {
                    TrimOuterBoundaryAtEarlierIntersection(reverseBoundary, reversePoint);
                }
            }

            AppendBoundaryPair
            (
                forwardBoundary,
                reverseBoundary,
                forwardPoint,
                reversePoint
            );
            previousForwardIndex = forwardIndex;
            previousReverseIndex = reverseIndex;
            previousTangent = tangent;
        }

        RawPoint last = samples[^1];
        if (!OverlapsEarlierSweepAtPoint(samples, samples.Count - 1))
        {
            AddTemplateArc
            (
                forwardBoundary,
                last,
                previousForwardIndex,
                previousReverseIndex,
                previousTangent
            );
        }

        AppendBoundarySeam(forwardBoundary, reverseBoundary, true);

        RawPoint first = samples[0];
        AddTemplateArc
        (
            forwardBoundary,
            first,
            initialReverseIndex,
            initialForwardIndex,
            Negate(initialTangent)
        );
        return RemoveConsecutiveDuplicates(forwardBoundary);
    }

    private static bool OverlapsEarlierSweepAtPoint(IReadOnlyList<RawPoint> samples, int currentIndex)
    {
        RawPoint current = samples[currentIndex];
        InkPoint2D currentPosition = current.Position;
        int lastIndex = currentIndex - 2;
        RawPoint earlier = samples[0];
        for (int index = 0; index <= lastIndex; index++)
        {
            double pointOverlapDistance = current.HalfWidth + earlier.HalfWidth;
            InkPoint2D earlierPosition = earlier.Position;
            if (GetDistanceSquared(currentPosition, earlierPosition) <= pointOverlapDistance * pointOverlapDistance)
            {
                return true;
            }

            if (index < lastIndex)
            {
                RawPoint nextEarlier = samples[index + 1];
                double overlapDistance = current.HalfWidth + Math.Max(earlier.HalfWidth, nextEarlier.HalfWidth);
                double overlapDistanceSquared = overlapDistance * overlapDistance;
                InkPoint2D nextEarlierPosition = nextEarlier.Position;
                double earlierSegmentMinX = Math.Min(earlierPosition.X, nextEarlierPosition.X);
                double earlierSegmentMaxX = Math.Max(earlierPosition.X, nextEarlierPosition.X);
                double earlierSegmentMinY = Math.Min(earlierPosition.Y, nextEarlierPosition.Y);
                double earlierSegmentMaxY = Math.Max(earlierPosition.Y, nextEarlierPosition.Y);
                if (AreBoundsWithinDistanceSquared
                    (
                        currentPosition.X,
                        currentPosition.X,
                        currentPosition.Y,
                        currentPosition.Y,
                        earlierSegmentMinX,
                        earlierSegmentMaxX,
                        earlierSegmentMinY,
                        earlierSegmentMaxY,
                        overlapDistanceSquared
                    ))
                {
                    double earlierSegmentX = nextEarlierPosition.X - earlierPosition.X;
                    double earlierSegmentY = nextEarlierPosition.Y - earlierPosition.Y;
                    double earlierSegmentLengthSquared =
                        (earlierSegmentX * earlierSegmentX) + (earlierSegmentY * earlierSegmentY);
                    if (GetDistanceToSegmentSquared
                        (
                            currentPosition,
                            earlierPosition,
                            earlierSegmentX,
                            earlierSegmentY,
                            earlierSegmentLengthSquared
                        ) <=
                        overlapDistanceSquared)
                    {
                        return true;
                    }
                }

                earlier = nextEarlier;
            }
        }

        return false;
    }

    private static bool OverlapsEarlierSweep(IReadOnlyList<RawPoint> samples, int currentIndex)
    {
        RawPoint current = samples[currentIndex];
        RawPoint next = samples[currentIndex + 1];
        InkPoint2D currentPosition = current.Position;
        InkPoint2D nextPosition = next.Position;
        double currentHalfWidth = Math.Max(current.HalfWidth, next.HalfWidth);
        double currentSegmentX = nextPosition.X - currentPosition.X;
        double currentSegmentY = nextPosition.Y - currentPosition.Y;
        double currentSegmentLengthSquared =
            (currentSegmentX * currentSegmentX) + (currentSegmentY * currentSegmentY);
        double currentSegmentMinX = Math.Min(currentPosition.X, nextPosition.X);
        double currentSegmentMaxX = Math.Max(currentPosition.X, nextPosition.X);
        double currentSegmentMinY = Math.Min(currentPosition.Y, nextPosition.Y);
        double currentSegmentMaxY = Math.Max(currentPosition.Y, nextPosition.Y);
        int lastIndex = currentIndex - 2;
        RawPoint earlier = samples[0];
        for (int index = 0; index <= lastIndex; index++)
        {
            double pointOverlapDistance = currentHalfWidth + earlier.HalfWidth;
            double pointOverlapDistanceSquared = pointOverlapDistance * pointOverlapDistance;
            InkPoint2D earlierPosition = earlier.Position;
            if (GetDistanceSquared(currentPosition, earlierPosition) <= pointOverlapDistanceSquared ||
                (AreBoundsWithinDistanceSquared
                 (
                     earlierPosition.X,
                     earlierPosition.X,
                     earlierPosition.Y,
                     earlierPosition.Y,
                     currentSegmentMinX,
                     currentSegmentMaxX,
                     currentSegmentMinY,
                     currentSegmentMaxY,
                     pointOverlapDistanceSquared
                 ) &&
                 GetDistanceToSegmentSquared
                 (
                     earlierPosition,
                     currentPosition,
                     currentSegmentX,
                     currentSegmentY,
                     currentSegmentLengthSquared
                 ) <=
                 pointOverlapDistanceSquared))
            {
                return true;
            }

            if (index < lastIndex)
            {
                RawPoint nextEarlier = samples[index + 1];
                double earlierHalfWidth = Math.Max(earlier.HalfWidth, nextEarlier.HalfWidth);
                double pointToEarlierSweepDistance = current.HalfWidth + earlierHalfWidth;
                double pointToEarlierSweepDistanceSquared = pointToEarlierSweepDistance * pointToEarlierSweepDistance;
                double overlapDistance = currentHalfWidth + earlierHalfWidth;
                double overlapDistanceSquared = overlapDistance * overlapDistance;
                InkPoint2D nextEarlierPosition = nextEarlier.Position;
                double earlierSegmentMinX = Math.Min(earlierPosition.X, nextEarlierPosition.X);
                double earlierSegmentMaxX = Math.Max(earlierPosition.X, nextEarlierPosition.X);
                double earlierSegmentMinY = Math.Min(earlierPosition.Y, nextEarlierPosition.Y);
                double earlierSegmentMaxY = Math.Max(earlierPosition.Y, nextEarlierPosition.Y);
                double earlierSegmentX = 0;
                double earlierSegmentY = 0;
                double earlierSegmentLengthSquared = 0;
                bool hasEarlierSegmentParameters = false;
                if (AreBoundsWithinDistanceSquared
                    (
                        currentPosition.X,
                        currentPosition.X,
                        currentPosition.Y,
                        currentPosition.Y,
                        earlierSegmentMinX,
                        earlierSegmentMaxX,
                        earlierSegmentMinY,
                        earlierSegmentMaxY,
                        pointToEarlierSweepDistanceSquared
                    ))
                {
                    earlierSegmentX = nextEarlierPosition.X - earlierPosition.X;
                    earlierSegmentY = nextEarlierPosition.Y - earlierPosition.Y;
                    earlierSegmentLengthSquared =
                        (earlierSegmentX * earlierSegmentX) + (earlierSegmentY * earlierSegmentY);
                    hasEarlierSegmentParameters = true;
                    if (GetDistanceToSegmentSquared
                        (
                            currentPosition,
                            earlierPosition,
                            earlierSegmentX,
                            earlierSegmentY,
                            earlierSegmentLengthSquared
                        ) <=
                        pointToEarlierSweepDistanceSquared)
                    {
                        return true;
                    }
                }

                if (AreBoundsWithinDistanceSquared
                    (
                        currentSegmentMinX,
                        currentSegmentMaxX,
                        currentSegmentMinY,
                        currentSegmentMaxY,
                        earlierSegmentMinX,
                        earlierSegmentMaxX,
                        earlierSegmentMinY,
                        earlierSegmentMaxY,
                        overlapDistanceSquared
                    ))
                {
                    if (!hasEarlierSegmentParameters)
                    {
                        earlierSegmentX = nextEarlierPosition.X - earlierPosition.X;
                        earlierSegmentY = nextEarlierPosition.Y - earlierPosition.Y;
                        earlierSegmentLengthSquared =
                            (earlierSegmentX * earlierSegmentX) + (earlierSegmentY * earlierSegmentY);
                    }

                    if (AreSegmentsWithinDistanceSquared
                        (
                            currentPosition,
                            nextPosition,
                            currentSegmentMinX,
                            currentSegmentMaxX,
                            currentSegmentMinY,
                            currentSegmentMaxY,
                            currentSegmentX,
                            currentSegmentY,
                            currentSegmentLengthSquared,
                            earlierPosition,
                            nextEarlierPosition,
                            earlierSegmentMinX,
                            earlierSegmentMaxX,
                            earlierSegmentMinY,
                            earlierSegmentMaxY,
                            earlierSegmentX,
                            earlierSegmentY,
                            earlierSegmentLengthSquared,
                            overlapDistanceSquared
                        ))
                    {
                        return true;
                    }
                }

                earlier = nextEarlier;
            }
        }

        return false;
    }

    private static bool AreBoundsWithinDistanceSquared
    (
        double firstMinX,
        double firstMaxX,
        double firstMinY,
        double firstMaxY,
        double secondMinX,
        double secondMaxX,
        double secondMinY,
        double secondMaxY,
        double distanceSquared
    )
    {
        double horizontalGap = Math.Max
        (
            0,
            Math.Max(firstMinX - secondMaxX, secondMinX - firstMaxX)
        );
        double verticalGap = Math.Max
        (
            0,
            Math.Max(firstMinY - secondMaxY, secondMinY - firstMaxY)
        );
        return (horizontalGap * horizontalGap) + (verticalGap * verticalGap) <= distanceSquared;
    }

    private static bool AreSegmentsWithinDistanceSquared
    (
        InkPoint2D firstStart,
        InkPoint2D firstEnd,
        double firstMinX,
        double firstMaxX,
        double firstMinY,
        double firstMaxY,
        double firstDx,
        double firstDy,
        double firstLengthSquared,
        InkPoint2D secondStart,
        InkPoint2D secondEnd,
        double secondMinX,
        double secondMaxX,
        double secondMinY,
        double secondMaxY,
        double secondDx,
        double secondDy,
        double secondLengthSquared,
        double distanceSquared
    )
    {
        if (TryGetSegmentIntersection
            (
                firstStart,
                firstMinX,
                firstMaxX,
                firstMinY,
                firstMaxY,
                firstDx,
                firstDy,
                secondStart,
                secondMinX,
                secondMaxX,
                secondMinY,
                secondMaxY,
                secondDx,
                secondDy,
                out _
            ))
        {
            return true;
        }

        if (GetDistanceToSegmentSquared(firstStart, secondStart, secondDx, secondDy, secondLengthSquared) <=
            distanceSquared ||
            GetDistanceToSegmentSquared(firstEnd, secondStart, secondDx, secondDy, secondLengthSquared) <=
            distanceSquared)
        {
            return true;
        }

        return GetDistanceToSegmentSquared(secondStart, firstStart, firstDx, firstDy, firstLengthSquared) <=
               distanceSquared ||
               GetDistanceToSegmentSquared(secondEnd, firstStart, firstDx, firstDy, firstLengthSquared) <=
               distanceSquared;
    }

    private static double GetDistanceToSegmentSquared(InkPoint2D point, InkPoint2D start, InkPoint2D end)
    {
        double dx = end.X - start.X;
        double dy = end.Y - start.Y;
        double lengthSquared = (dx * dx) + (dy * dy);
        return GetDistanceToSegmentSquared(point, start, dx, dy, lengthSquared);
    }

    private static double GetDistanceToSegmentSquared
    (
        InkPoint2D point,
        InkPoint2D start,
        double dx,
        double dy,
        double lengthSquared
    )
    {
        if (lengthSquared <= 1e-12)
        {
            return GetDistanceSquared(point, start);
        }

        double amount = Math.Clamp
        (
            (((point.X - start.X) * dx) + ((point.Y - start.Y) * dy)) / lengthSquared,
            0,
            1
        );
        double projectionX = start.X + (amount * dx);
        double projectionY = start.Y + (amount * dy);
        double distanceX = point.X - projectionX;
        double distanceY = point.Y - projectionY;
        return (distanceX * distanceX) + (distanceY * distanceY);
    }

    private static double GetDistanceSquared(InkPoint2D left, InkPoint2D right)
    {
        double x = left.X - right.X;
        double y = left.Y - right.Y;
        return (x * x) + (y * y);
    }

    private static (int ForwardIndex, int ReverseIndex) GetTemplateSupportIndices(InkPoint2D tangent)
    {
        double normalX = -tangent.Y;
        double normalY = tangent.X;
        int forwardIndex = 0;
        int reverseIndex = 0;
        double maximumProjection = double.NegativeInfinity;
        double minimumProjection = double.PositiveInfinity;

        for (int index = 0; index < PenCapTemplate.Length; index++)
        {
            InkPoint2D templatePoint = PenCapTemplate[index];
            double projection = (templatePoint.X * normalX) + (templatePoint.Y * normalY);
            if (projection > maximumProjection)
            {
                maximumProjection = projection;
                forwardIndex = index;
            }

            if (projection < minimumProjection)
            {
                minimumProjection = projection;
                reverseIndex = index;
            }
        }

        return (forwardIndex, reverseIndex);
    }

    private static void AddBoundaryTemplateArc
    (
        List<InkPoint2D> boundary,
        RawPoint sample,
        int startIndex,
        int endIndex,
        InkPoint2D preferredDirection
    )
    {
        if (startIndex == endIndex)
        {
            return;
        }

        int step = GetMaximumDirectionProjection(sample, startIndex, endIndex, 1, preferredDirection) >=
                   GetMaximumDirectionProjection(sample, startIndex, endIndex, -1, preferredDirection)
            ? 1
            : -1;
        int index = (startIndex + step + PenCapTemplate.Length) % PenCapTemplate.Length;
        while (true)
        {
            AppendBoundaryPoint(boundary, PlaceTemplatePoint(sample, PenCapTemplate[index]));
            if (index == endIndex)
            {
                return;
            }

            index = (index + step + PenCapTemplate.Length) % PenCapTemplate.Length;
        }
    }

    private static void AppendBoundarySeam
    (
        List<InkPoint2D> outline,
        IReadOnlyList<InkPoint2D> boundary,
        bool reverse
    )
    {
        int boundaryCount = boundary.Count;
        int index = reverse ? boundaryCount - 1 : 0;
        int endIndex = reverse ? -1 : boundaryCount;
        int step = reverse ? -1 : 1;
        int seamPointCount = Math.Min(PenCapTemplate.Length, boundaryCount);
        int appendedCount = 0;

        for (; index != endIndex; index += step)
        {
            if (appendedCount < seamPointCount)
            {
                AppendBoundaryPoint(outline, boundary[index]);
            }
            else
            {
                InkPoint2D point = boundary[index];
                if (GetDistanceSquared(outline[^1], point) > 1e-12)
                {
                    outline.Add(point);
                }
            }

            appendedCount++;
        }
    }

    private static void AppendBoundaryPair
    (
        List<InkPoint2D> forwardBoundary,
        List<InkPoint2D> reverseBoundary,
        InkPoint2D forwardPoint,
        InkPoint2D reversePoint
    )
    {
        int reverseIntersectionIndex = -1;
        InkPoint2D forwardIntersection = default;
        int forwardIntersectionIndex = -1;
        InkPoint2D reverseIntersection = default;
        InkPoint2D forwardBoundaryEnd = default;
        InkPoint2D reverseBoundaryEnd = default;
        bool forwardIntersects = forwardBoundary.Count > 1 && reverseBoundary.Count > 1 &&
                                 TryFindCrossBoundaryIntersection
                                 (
                                     reverseBoundary,
                                     forwardBoundaryEnd = forwardBoundary[^1],
                                     forwardPoint,
                                     out reverseIntersectionIndex,
                                     out forwardIntersection
                                 );
        bool reverseIntersects = forwardBoundary.Count > 1 && reverseBoundary.Count > 1 &&
                                 TryFindCrossBoundaryIntersection
                                 (
                                     forwardBoundary,
                                     reverseBoundaryEnd = reverseBoundary[^1],
                                     reversePoint,
                                     out forwardIntersectionIndex,
                                     out reverseIntersection
                                 );

        if (forwardIntersects &&
            (!reverseIntersects ||
             GetDistanceSquared(forwardBoundaryEnd, forwardIntersection) <=
             GetDistanceSquared(reverseBoundaryEnd, reverseIntersection)))
        {
            reverseBoundary.RemoveRange
                (reverseIntersectionIndex + 1, reverseBoundary.Count - reverseIntersectionIndex - 1);
            AppendBoundaryPoint(reverseBoundary, forwardIntersection);
            AppendBoundaryPoint(forwardBoundary, forwardIntersection);
        }
        else if (reverseIntersects)
        {
            forwardBoundary.RemoveRange
                (forwardIntersectionIndex + 1, forwardBoundary.Count - forwardIntersectionIndex - 1);
            AppendBoundaryPoint(forwardBoundary, reverseIntersection);
            AppendBoundaryPoint(reverseBoundary, reverseIntersection);
        }

        AppendBoundaryPoint(forwardBoundary, forwardPoint);
        AppendBoundaryPoint(reverseBoundary, reversePoint);
    }

    private static void TrimOuterBoundaryAtEarlierIntersection
    (
        List<InkPoint2D> boundary,
        InkPoint2D nextPoint
    )
    {
        int boundaryCount = boundary.Count;
        if (boundaryCount <= PenCapTemplate.Length + 2)
        {
            return;
        }

        InkPoint2D segmentStart = boundary[boundaryCount - 1];
        double segmentMinX = Math.Min(segmentStart.X, nextPoint.X);
        double segmentMaxX = Math.Max(segmentStart.X, nextPoint.X);
        double segmentMinY = Math.Min(segmentStart.Y, nextPoint.Y);
        double segmentMaxY = Math.Max(segmentStart.Y, nextPoint.Y);
        double segmentX = nextPoint.X - segmentStart.X;
        double segmentY = nextPoint.Y - segmentStart.Y;
        int lastIndex = boundaryCount - PenCapTemplate.Length - 2;
        int intersectionIndex = -1;
        InkPoint2D nearestIntersection = default;
        double nearestDistanceSquared = double.PositiveInfinity;
        InkPoint2D historicalEnd = boundary[lastIndex + 1];
        for (int index = lastIndex; index >= 0; index--)
        {
            InkPoint2D historicalStart = boundary[index];
            if (!TryGetSegmentIntersection
                (
                    historicalStart,
                    historicalEnd,
                    segmentStart,
                    nextPoint,
                    segmentMinX,
                    segmentMaxX,
                    segmentMinY,
                    segmentMaxY,
                    segmentX,
                    segmentY,
                    out InkPoint2D intersection
                ))
            {
                historicalEnd = historicalStart;
                continue;
            }

            historicalEnd = historicalStart;
            double distanceSquared = GetDistanceSquared(segmentStart, intersection);
            if (distanceSquared < nearestDistanceSquared)
            {
                intersectionIndex = index;
                nearestIntersection = intersection;
                nearestDistanceSquared = distanceSquared;
            }
        }

        if (intersectionIndex < 0)
        {
            return;
        }

        boundary.RemoveRange(intersectionIndex + 1, boundaryCount - intersectionIndex - 1);
        AppendBoundaryPoint(boundary, nearestIntersection);
    }

    private static bool TryFindCrossBoundaryIntersection
    (
        IReadOnlyList<InkPoint2D> boundary,
        InkPoint2D segmentStart,
        InkPoint2D segmentEnd,
        out int intersectionIndex,
        out InkPoint2D intersection
    )
    {
        int boundaryCount = boundary.Count;
        int firstIndex = Math.Max(0, boundaryCount - PenCapTemplate.Length - 1);
        intersectionIndex = -1;
        intersection = default;
        double nearestDistanceSquared = double.PositiveInfinity;
        double segmentMinX = Math.Min(segmentStart.X, segmentEnd.X);
        double segmentMaxX = Math.Max(segmentStart.X, segmentEnd.X);
        double segmentMinY = Math.Min(segmentStart.Y, segmentEnd.Y);
        double segmentMaxY = Math.Max(segmentStart.Y, segmentEnd.Y);
        double segmentX = segmentEnd.X - segmentStart.X;
        double segmentY = segmentEnd.Y - segmentStart.Y;
        InkPoint2D historicalEnd = boundary[boundaryCount - 1];
        for (int index = boundaryCount - 2; index >= firstIndex; index--)
        {
            InkPoint2D historicalStart = boundary[index];
            if (!TryGetSegmentIntersection
                (
                    historicalStart,
                    historicalEnd,
                    segmentStart,
                    segmentEnd,
                    segmentMinX,
                    segmentMaxX,
                    segmentMinY,
                    segmentMaxY,
                    segmentX,
                    segmentY,
                    out InkPoint2D candidate
                ))
            {
                historicalEnd = historicalStart;
                continue;
            }

            historicalEnd = historicalStart;
            double distanceSquared = GetDistanceSquared(segmentStart, candidate);
            if (distanceSquared < nearestDistanceSquared)
            {
                intersectionIndex = index;
                intersection = candidate;
                nearestDistanceSquared = distanceSquared;
            }
        }

        return intersectionIndex >= 0;
    }

    private static void AppendBoundaryPoint(List<InkPoint2D> boundary, InkPoint2D point)
    {
        int boundaryCount = boundary.Count;
        if (boundaryCount == 0)
        {
            boundary.Add(point);
            return;
        }

        InkPoint2D segmentStart = boundary[^1];
        if (GetDistanceSquared(segmentStart, point) <= 1e-12)
        {
            return;
        }

        double segmentMinX = Math.Min(segmentStart.X, point.X);
        double segmentMaxX = Math.Max(segmentStart.X, point.X);
        double segmentMinY = Math.Min(segmentStart.Y, point.Y);
        double segmentMaxY = Math.Max(segmentStart.Y, point.Y);
        double segmentX = point.X - segmentStart.X;
        double segmentY = point.Y - segmentStart.Y;
        int firstIndex = Math.Max(0, boundaryCount - PenCapTemplate.Length - 1);
        int intersectionIndex = -1;
        InkPoint2D nearestIntersection = default;
        double nearestDistanceSquared = double.PositiveInfinity;
        InkPoint2D historicalEnd = boundaryCount >= 3 ? boundary[^2] : default;
        for (int index = boundaryCount - 3; index >= firstIndex; index--)
        {
            InkPoint2D historicalStart = boundary[index];
            if (!TryGetSegmentIntersection
                (
                    historicalStart,
                    historicalEnd,
                    segmentStart,
                    point,
                    segmentMinX,
                    segmentMaxX,
                    segmentMinY,
                    segmentMaxY,
                    segmentX,
                    segmentY,
                    out InkPoint2D intersection
                ))
            {
                historicalEnd = historicalStart;
                continue;
            }

            historicalEnd = historicalStart;
            double distanceSquared = GetDistanceSquared(segmentStart, intersection);
            if (distanceSquared < nearestDistanceSquared)
            {
                intersectionIndex = index;
                nearestIntersection = intersection;
                nearestDistanceSquared = distanceSquared;
            }
        }

        if (intersectionIndex >= 0)
        {
            boundary.RemoveRange(intersectionIndex + 1, boundaryCount - intersectionIndex - 1);
            if (GetDistanceSquared(boundary[^1], nearestIntersection) > 1e-12)
            {
                boundary.Add(nearestIntersection);
            }
        }

        if (GetDistanceSquared(boundary[^1], point) > 1e-12)
        {
            boundary.Add(point);
        }
    }

    private static bool TryGetSegmentIntersection
    (
        InkPoint2D firstStart,
        InkPoint2D firstEnd,
        InkPoint2D secondStart,
        InkPoint2D secondEnd,
        out InkPoint2D intersection
    ) =>
        TryGetSegmentIntersection
        (
            firstStart,
            firstEnd,
            secondStart,
            secondEnd,
            Math.Min(secondStart.X, secondEnd.X),
            Math.Max(secondStart.X, secondEnd.X),
            Math.Min(secondStart.Y, secondEnd.Y),
            Math.Max(secondStart.Y, secondEnd.Y),
            secondEnd.X - secondStart.X,
            secondEnd.Y - secondStart.Y,
            out intersection
        );

    private static bool TryGetSegmentIntersection
    (
        InkPoint2D firstStart,
        double firstMinX,
        double firstMaxX,
        double firstMinY,
        double firstMaxY,
        double firstX,
        double firstY,
        InkPoint2D secondStart,
        double secondMinX,
        double secondMaxX,
        double secondMinY,
        double secondMaxY,
        double secondX,
        double secondY,
        out InkPoint2D intersection
    )
    {
        if (firstMaxX < secondMinX ||
            secondMaxX < firstMinX ||
            firstMaxY < secondMinY ||
            secondMaxY < firstMinY)
        {
            intersection = default;
            return false;
        }

        return TryGetSegmentIntersection
        (
            firstStart,
            firstX,
            firstY,
            secondStart,
            secondX,
            secondY,
            out intersection
        );
    }

    private static bool TryGetSegmentIntersection
    (
        InkPoint2D firstStart,
        InkPoint2D firstEnd,
        InkPoint2D secondStart,
        InkPoint2D secondEnd,
        double secondMinX,
        double secondMaxX,
        double secondMinY,
        double secondMaxY,
        double secondX,
        double secondY,
        out InkPoint2D intersection
    )
    {
        if (Math.Max(firstStart.X, firstEnd.X) < secondMinX ||
            secondMaxX < Math.Min(firstStart.X, firstEnd.X) ||
            Math.Max(firstStart.Y, firstEnd.Y) < secondMinY ||
            secondMaxY < Math.Min(firstStart.Y, firstEnd.Y))
        {
            intersection = default;
            return false;
        }

        double firstX = firstEnd.X - firstStart.X;
        double firstY = firstEnd.Y - firstStart.Y;
        return TryGetSegmentIntersection
        (
            firstStart,
            firstX,
            firstY,
            secondStart,
            secondX,
            secondY,
            out intersection
        );
    }

    private static bool TryGetSegmentIntersection
    (
        InkPoint2D firstStart,
        double firstX,
        double firstY,
        InkPoint2D secondStart,
        double secondX,
        double secondY,
        out InkPoint2D intersection
    )
    {
        double denominator = (firstX * secondY) - (firstY * secondX);
        if (Math.Abs(denominator) <= 1e-9)
        {
            intersection = default;
            return false;
        }

        double startX = secondStart.X - firstStart.X;
        double startY = secondStart.Y - firstStart.Y;
        double firstAmount = ((startX * secondY) - (startY * secondX)) / denominator;
        double secondAmount = ((startX * firstY) - (startY * firstX)) / denominator;
        const double endpointTolerance = 1e-6;
        if (firstAmount <= endpointTolerance || firstAmount >= 1 - endpointTolerance ||
            secondAmount <= endpointTolerance || secondAmount >= 1 - endpointTolerance)
        {
            intersection = default;
            return false;
        }

        intersection = new InkPoint2D
        (
            firstStart.X + (firstAmount * firstX),
            firstStart.Y + (firstAmount * firstY)
        );
        return true;
    }

    private static IReadOnlyList<InkPoint2D> BuildNormalOffsetOutline(IReadOnlyList<RawPoint> samples)
    {
        var left = new List<InkPoint2D>(samples.Count);
        var right = new List<InkPoint2D>(samples.Count);

        InkPoint2D previousNormal = GetSegmentNormal(samples[0].Position, samples[1].Position);
        for (int index = 0; index < samples.Count; index++)
        {
            InkPoint2D nextNormal = index == samples.Count - 1
                ? previousNormal
                : GetSegmentNormal(samples[index].Position, samples[index + 1].Position);
            InkPoint2D offset = GetJoinOffset(previousNormal, nextNormal, samples[index].HalfWidth);
            left.Add(new InkPoint2D(samples[index].Position.X + offset.X, samples[index].Position.Y + offset.Y));
            right.Add(new InkPoint2D(samples[index].Position.X - offset.X, samples[index].Position.Y - offset.Y));
            previousNormal = nextNormal;
        }

        var outline = new List<InkPoint2D>((samples.Count * 2) + 18);
        outline.AddRange(left);
        AddRoundCap(outline, samples[^1], left[^1], right[^1], 8);
        for (int index = right.Count - 1; index >= 0; index--)
        {
            outline.Add(right[index]);
        }

        AddRoundCap(outline, samples[0], right[0], left[0], 8);
        return outline;
    }

    private static InkPoint2D GetSampleTangent(IReadOnlyList<RawPoint> samples, int index)
    {
        InkPoint2D start = index == 0 ? samples[0].Position : samples[index - 1].Position;
        InkPoint2D end = index == samples.Count - 1 ? samples[^1].Position : samples[index + 1].Position;
        double dx = end.X - start.X;
        double dy = end.Y - start.Y;
        double length = Math.Sqrt((dx * dx) + (dy * dy));
        if (length >= 1e-6)
        {
            return new InkPoint2D(dx / length, dy / length);
        }

        if (index > 0)
        {
            return GetDirection(samples[index - 1].Position, samples[index].Position);
        }

        return GetDirection(samples[index].Position, samples[index + 1].Position);
    }

    private static InkPoint2D GetDirection(InkPoint2D start, InkPoint2D end)
    {
        double dx = end.X - start.X;
        double dy = end.Y - start.Y;
        double length = Math.Sqrt((dx * dx) + (dy * dy));
        return length < 1e-6 ? new InkPoint2D(1, 0) : new InkPoint2D(dx / length, dy / length);
    }

    private static InkPoint2D Negate(InkPoint2D point) => new(-point.X, -point.Y);

    private static InkPoint2D PlaceTemplatePoint(RawPoint sample, InkPoint2D templatePoint) => new
    (
        sample.Position.X + (templatePoint.X * sample.HalfWidth),
        sample.Position.Y + (templatePoint.Y * sample.HalfWidth)
    );

    private static void AddTemplateArc
    (
        List<InkPoint2D> output,
        RawPoint sample,
        int startIndex,
        int endIndex,
        InkPoint2D preferredDirection
    )
    {
        int step = GetMaximumDirectionProjection(sample, startIndex, endIndex, 1, preferredDirection) >=
                   GetMaximumDirectionProjection(sample, startIndex, endIndex, -1, preferredDirection)
            ? 1
            : -1;
        for (int index = (startIndex + step + PenCapTemplate.Length) % PenCapTemplate.Length;
             index != endIndex;
             index = (index + step + PenCapTemplate.Length) % PenCapTemplate.Length)
        {
            AppendBoundaryPoint(output, PlaceTemplatePoint(sample, PenCapTemplate[index]));
        }
    }

    private static double GetMaximumDirectionProjection
    (
        RawPoint sample,
        int startIndex,
        int endIndex,
        int step,
        InkPoint2D direction
    )
    {
        double maximumProjection = double.NegativeInfinity;
        int index = startIndex;
        while (true)
        {
            InkPoint2D point = PlaceTemplatePoint(sample, PenCapTemplate[index]);
            double projection = ((point.X - sample.Position.X) * direction.X) +
                                ((point.Y - sample.Position.Y) * direction.Y);
            maximumProjection = Math.Max(maximumProjection, projection);
            if (index == endIndex)
            {
                return maximumProjection;
            }

            index = (index + step + PenCapTemplate.Length) % PenCapTemplate.Length;
        }
    }

    private static IReadOnlyList<InkPoint2D> RemoveConsecutiveDuplicates(List<InkPoint2D> points)
    {
        var result = new List<InkPoint2D>(points.Count);
        foreach (InkPoint2D point in points)
        {
            if (result.Count == 0 || GetDistanceSquared(result[^1], point) > 1e-12)
            {
                result.Add(point);
            }
        }

        if (result.Count > 1 && GetDistanceSquared(result[0], result[^1]) <= 1e-12)
        {
            result.RemoveAt(result.Count - 1);
        }

        return result;
    }

    private static InkPoint2D GetSegmentNormal(InkPoint2D start, InkPoint2D end)
    {
        double dx = end.X - start.X;
        double dy = end.Y - start.Y;
        double length = Math.Sqrt((dx * dx) + (dy * dy));
        return length < 1e-6 ? new InkPoint2D(0, 1) : new InkPoint2D(-dy / length, dx / length);
    }

    private static InkPoint2D GetJoinOffset(InkPoint2D previousNormal, InkPoint2D nextNormal, double halfWidth)
    {
        double x = previousNormal.X + nextNormal.X;
        double y = previousNormal.Y + nextNormal.Y;
        double length = Math.Sqrt((x * x) + (y * y));
        if (length < 1e-6)
        {
            return new InkPoint2D(nextNormal.X * halfWidth, nextNormal.Y * halfWidth);
        }

        x /= length;
        y /= length;
        double denominator = Math.Abs((x * nextNormal.X) + (y * nextNormal.Y));
        double scale = halfWidth / Math.Max(0.5, denominator);
        scale = Math.Min(scale, halfWidth * 1.6);
        return new InkPoint2D(x * scale, y * scale);
    }

    private static void AddRoundCap
        (List<InkPoint2D> output, RawPoint center, InkPoint2D start, InkPoint2D end, int steps)
    {
        double startAngle = Math.Atan2(start.Y - center.Position.Y, start.X - center.Position.X);
        double endAngle = Math.Atan2(end.Y - center.Position.Y, end.X - center.Position.X);
        while (endAngle <= startAngle)
        {
            endAngle += Math.PI * 2;
        }

        if (endAngle - startAngle > Math.PI * 1.5)
        {
            endAngle -= Math.PI * 2;
        }

        for (int index = 1; index < steps; index++)
        {
            double angle = startAngle + ((endAngle - startAngle) * index / steps);
            output.Add
            (
                new InkPoint2D
                (
                    center.Position.X + (Math.Cos(angle) * center.HalfWidth),
                    center.Position.Y + (Math.Sin(angle) * center.HalfWidth)
                )
            );
        }
    }

    private static IReadOnlyList<InkPoint2D> RepairRawPointCoverage
    (
        IReadOnlyList<InkPoint2D> outline,
        IReadOnlyList<RawPoint> rawPoints,
        IReadOnlyList<RawPoint> samples,
        bool completesStroke
    )
    {
        List<InkPoint2D>? repairedOutline = null;
        double outlineArea = GetSignedArea(outline);

        for (int rawPointIndex = 0; rawPointIndex < rawPoints.Count; rawPointIndex++)
        {
            RawPoint rawPoint = rawPoints[rawPointIndex];
            IReadOnlyList<InkPoint2D> currentOutline = repairedOutline ?? outline;
            bool checkInterior = rawPointIndex >= 2 && OverlapsEarlierSweepAtPoint(rawPoints, rawPointIndex);
            if (IsPenCapInteriorFilled(currentOutline, rawPoint, checkInterior))
            {
                continue;
            }

            AppendCoveragePenCap(outline, outlineArea, rawPoint.Position, rawPoint.HalfWidth, ref repairedOutline);
        }

        if (completesStroke && rawPoints.Count >= 10)
        {
            for (int sampleIndex = 2; sampleIndex < samples.Count; sampleIndex++)
            {
                RawPoint sample = samples[sampleIndex];
                if (!OverlapsEarlierSweepAtPoint(samples, sampleIndex))
                {
                    continue;
                }

                IReadOnlyList<InkPoint2D> currentOutline = repairedOutline ?? outline;
                if (!IsFilledByNonZeroWinding(currentOutline, sample.Position))
                {
                    AppendCoveragePenCap(outline, outlineArea, sample.Position, sample.HalfWidth, ref repairedOutline);
                    break;
                }
            }
        }

        return repairedOutline ?? outline;
    }

    private static void AppendCoveragePenCap
    (
        IReadOnlyList<InkPoint2D> outline,
        double outlineArea,
        InkPoint2D position,
        double halfWidth,
        ref List<InkPoint2D>? repairedOutline
    )
    {
        repairedOutline ??= new List<InkPoint2D>(outline.Count + PenCapTemplate.Length + 3);
        if (repairedOutline.Count == 0)
        {
            repairedOutline.AddRange(outline);
        }

        IReadOnlyList<InkPoint2D> penCap = PlacePenCap(position, halfWidth);
        bool reversePenCap = Math.Sign(GetSignedArea(penCap)) != Math.Sign(outlineArea);
        InkPoint2D bridgeStart = repairedOutline[0];
        InkPoint2D penCapStart = reversePenCap ? penCap[^1] : penCap[0];
        repairedOutline.Add(bridgeStart);
        repairedOutline.Add(penCapStart);

        if (reversePenCap)
        {
            for (int index = penCap.Count - 2; index >= 0; index--)
            {
                repairedOutline.Add(penCap[index]);
            }
        }
        else
        {
            for (int index = 1; index < penCap.Count; index++)
            {
                repairedOutline.Add(penCap[index]);
            }
        }

        repairedOutline.Add(penCapStart);
        repairedOutline.Add(bridgeStart);
    }

    private static bool IsPenCapInteriorFilled
    (
        IReadOnlyList<InkPoint2D> outline,
        RawPoint sample,
        bool checkInterior
    )
    {
        if (!IsFilledByNonZeroWinding(outline, sample.Position))
        {
            return false;
        }

        if (!checkInterior)
        {
            return true;
        }

        double interiorRadius = sample.HalfWidth * 0.25;
        return IsFilledByNonZeroWinding
               (
                   outline,
                   new InkPoint2D(sample.Position.X + interiorRadius, sample.Position.Y)
               ) &&
               IsFilledByNonZeroWinding
               (
                   outline,
                   new InkPoint2D(sample.Position.X - interiorRadius, sample.Position.Y)
               ) &&
               IsFilledByNonZeroWinding
               (
                   outline,
                   new InkPoint2D(sample.Position.X, sample.Position.Y + interiorRadius)
               ) &&
               IsFilledByNonZeroWinding
               (
                   outline,
                   new InkPoint2D(sample.Position.X, sample.Position.Y - interiorRadius)
               );
    }

    private static bool IsFilledByNonZeroWinding
    (
        IReadOnlyList<InkPoint2D> polygon,
        InkPoint2D point
    )
    {
        int windingNumber = 0;
        for (int index = 0; index < polygon.Count; index++)
        {
            InkPoint2D start = polygon[index];
            InkPoint2D end = polygon[(index + 1) % polygon.Count];
            if (GetDistanceToSegmentSquared(point, start, end) <= 1e-12)
            {
                return true;
            }

            if (start.Y <= point.Y)
            {
                if (end.Y > point.Y && Cross(start, end, point) > 0)
                {
                    windingNumber++;
                }
            }
            else if (end.Y <= point.Y && Cross(start, end, point) < 0)
            {
                windingNumber--;
            }
        }

        return windingNumber != 0;
    }

    private static double GetSignedArea(IReadOnlyList<InkPoint2D> polygon)
    {
        double twiceArea = 0;
        for (int index = 0; index < polygon.Count; index++)
        {
            InkPoint2D current = polygon[index];
            InkPoint2D next = polygon[(index + 1) % polygon.Count];
            twiceArea += (current.X * next.Y) - (next.X * current.Y);
        }

        return twiceArea / 2;
    }

    private static IReadOnlyList<InkPoint2D> PlacePenCap(InkPoint2D center, double halfWidth)
    {
        var points = new InkPoint2D[PenCapTemplate.Length];
        for (int index = 0; index < PenCapTemplate.Length; index++)
        {
            InkPoint2D templatePoint = PenCapTemplate[index];
            points[index] = new InkPoint2D
            (
                center.X + (templatePoint.X * halfWidth),
                center.Y + (templatePoint.Y * halfWidth)
            );
        }

        return points;
    }

    private static void AddPenCap(List<InkPoint2D> output, InkPoint2D center, double halfWidth)
    {
        foreach (InkPoint2D templatePoint in PenCapTemplate)
        {
            output.Add
            (
                new InkPoint2D
                (
                    center.X + (templatePoint.X * halfWidth),
                    center.Y + (templatePoint.Y * halfWidth)
                )
            );
        }
    }

    private static IReadOnlyList<InkPoint2D> BuildConvexHull(List<InkPoint2D> points)
    {
        if (points.Count <= 1)
        {
            return points;
        }

        points.Sort
        (static (left, right) =>
            {
                int xComparison = left.X.CompareTo(right.X);
                return xComparison != 0 ? xComparison : left.Y.CompareTo(right.Y);
            }
        );

        var hull = new List<InkPoint2D>(points.Count * 2);
        foreach (InkPoint2D point in points)
        {
            while (hull.Count >= 2 && Cross(hull[^2], hull[^1], point) <= 0)
            {
                hull.RemoveAt(hull.Count - 1);
            }

            hull.Add(point);
        }

        int lowerCount = hull.Count;
        for (int index = points.Count - 2; index >= 0; index--)
        {
            InkPoint2D point = points[index];
            while (hull.Count > lowerCount && Cross(hull[^2], hull[^1], point) <= 0)
            {
                hull.RemoveAt(hull.Count - 1);
            }

            hull.Add(point);
        }

        hull.RemoveAt(hull.Count - 1);
        return hull;
    }

    private static double Dot(InkPoint2D first, InkPoint2D second) =>
        (first.X * second.X) + (first.Y * second.Y);

    private static double Cross(InkPoint2D first, InkPoint2D second) =>
        (first.X * second.Y) - (first.Y * second.X);

    private static double Cross(InkPoint2D origin, InkPoint2D first, InkPoint2D second) =>
        Cross
        (
            new InkPoint2D(first.X - origin.X, first.Y - origin.Y),
            new InkPoint2D(second.X - origin.X, second.Y - origin.Y)
        );
}
