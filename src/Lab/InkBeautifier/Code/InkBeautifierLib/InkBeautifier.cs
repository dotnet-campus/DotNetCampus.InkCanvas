namespace InkBeautifierLib;

/// <summary>
/// 将离散输入点转换为平滑、动态宽度的闭合笔迹轮廓。
/// </summary>
public sealed class InkBeautifier
{
    private readonly Dictionary<long, StrokeState> _strokes = [];

    /// <summary>
    /// 初始化笔迹美化器。
    /// </summary>
    public InkBeautifier(InkBeautifierOptions? options = null)
    {
        Options = options ?? new InkBeautifierOptions();
        Options.Validate();
    }

    /// <summary>
    /// 获取当前配置。
    /// </summary>
    public InkBeautifierOptions Options { get; }

    /// <summary>
    /// 处理一个输入采样点并返回该笔的最新完整轮廓。
    /// </summary>
    public InkBeautifierResult Process(InkInputPoint input)
    {
        ValidateInput(input);

        if (input.Status == InkPointerStatus.Down)
        {
            var state = new StrokeState(input);
            _strokes[input.StrokeId] = state;
            return new InkBeautifierResult(input.StrokeId, [], false, true);
        }

        if (!_strokes.TryGetValue(input.StrokeId, out StrokeState? stroke))
        {
            return new InkBeautifierResult(input.StrokeId, [], input.Status == InkPointerStatus.Up, false);
        }

        bool accepted = TryAddPoint(stroke, input);
        IReadOnlyList<InkPoint2D> outline = accepted ? BuildOutline(stroke) : stroke.Outline;
        stroke.Outline = outline;

        bool completed = input.Status == InkPointerStatus.Up;
        if (completed)
        {
            _strokes.Remove(input.StrokeId);
        }

        return new InkBeautifierResult(input.StrokeId, outline, completed, accepted);
    }

    /// <summary>
    /// 清除全部尚未完成的笔迹状态。
    /// </summary>
    public void Reset() => _strokes.Clear();

    private bool TryAddPoint(StrokeState stroke, InkInputPoint input)
    {
        RawPoint last = stroke.RawPoints[^1];
        double distance = InkPoint2D.Distance(last.Position, input.Position);
        long elapsed = input.Timestamp - last.Timestamp;
        bool isUp = input.Status == InkPointerStatus.Up;

        if (!isUp && (elapsed < Options.MinimumTimeInterval || distance < Options.MinimumDistance))
        {
            return false;
        }

        InkPoint2D position = input.Position;
        if (isUp && distance < double.Epsilon)
        {
            if (stroke.RawPoints.Count <= 2)
            {
                stroke.IsTap = true;
                return true;
            }

            if (elapsed > 20)
            {
                return false;
            }

            RawPoint previous = stroke.RawPoints[^2];
            position = new InkPoint2D(
                last.Position.X + (0.6 * (last.Position.X - previous.Position.X)),
                last.Position.Y + (0.6 * (last.Position.Y - previous.Position.Y)));
            distance = InkPoint2D.Distance(last.Position, position);
        }

        if (elapsed <= 0)
        {
            return false;
        }

        double speed = distance / elapsed;
        if (isUp && stroke.RawPoints.Count > 2 && speed < 0.03)
        {
            return false;
        }

        double pressure = input.Pressure is > 0 and <= 1 ? input.Pressure : 0.5;
        double velocityWidth = Options.MaximumWidth * Math.Exp(-Options.VelocityResponse * speed);
        double pressureScale = 1 - Options.PressureInfluence + (Options.PressureInfluence * pressure * 2);
        double width = Math.Clamp(velocityWidth * pressureScale, Options.MaximumWidth * 0.08, Options.MaximumWidth);

        var point = new RawPoint(position, input.Timestamp, speed, width / 2, width / 2);
        stroke.RawPoints.Add(point);

        if (stroke.RawPoints.Count == 2)
        {
            RawPoint second = stroke.RawPoints[1];
            stroke.RawPoints[0] = stroke.RawPoints[0] with
            {
                Speed = second.Speed,
                RawHalfWidth = second.RawHalfWidth,
                HalfWidth = second.HalfWidth
            };
        }

        SmoothCurrentWidth(stroke.RawPoints);

        RawPoint current = stroke.RawPoints[^1];
        if (current.HalfWidth / Options.MaximumWidth < 0.4)
        {
            stroke.NarrowPointCount++;
        }
        else
        {
            stroke.NarrowPointCount = 0;
        }

        bool isTail = IsTail(stroke, last, current, isUp);
        if (isTail)
        {
            stroke.RawPoints[^1] = current with { HalfWidth = current.RawHalfWidth };
            current = stroke.RawPoints[^1];
        }

        if (isTail && current.HalfWidth / Options.MaximumWidth < 0.4)
        {
            stroke.RawPoints[^1] = current with { HalfWidth = 0.03 };
        }

        AppendBezierSamples(stroke, isTail);
        return true;
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

    private static bool IsTail(StrokeState stroke, RawPoint previous, RawPoint current, bool isUp)
    {
        if (!isUp)
        {
            return false;
        }

        double angle = Math.Atan2(
            current.Position.Y - previous.Position.Y,
            current.Position.X - previous.Position.X) * 180 / Math.PI;
        bool isUpwardDirection = angle is > -155 and < -35;
        return stroke.NarrowPointCount >= 3 || (isUpwardDirection && stroke.NarrowPointCount >= 2);
    }

    private static void AppendBezierSamples(StrokeState stroke, bool isTail)
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
            < 60 => 7,
            _ => 8
        };
        int lastIndex = isTail ? interpolationTimes : interpolationTimes + 1;
        double interval = (isTail ? 1.0 : 0.5) / interpolationTimes;

        for (int index = 0; index <= lastIndex; index++)
        {
            double t = index * interval;
            double oneMinusT = 1 - t;
            double a = oneMinusT * oneMinusT;
            double b = 2 * t * oneMinusT;
            double c = t * t;
            double halfWidth = (a * p0.HalfWidth) + (b * p1.HalfWidth) + (c * p2.HalfWidth);

            var sample = new RawPoint(
                new InkPoint2D(
                    (a * p0.Position.X) + (b * p1.Position.X) + (c * p2.Position.X),
                    (a * p0.Position.Y) + (b * p1.Position.Y) + (c * p2.Position.Y)),
                p1.Timestamp,
                p1.Speed,
                halfWidth,
                halfWidth);
            if (stroke.Samples.Count == 0 || sample.DistanceFrom(stroke.Samples[^1]) > 0.01)
            {
                stroke.Samples.Add(sample);
            }
        }
    }

    private IReadOnlyList<InkPoint2D> BuildOutline(StrokeState stroke)
    {
        if (stroke.IsTap || stroke.RawPoints.Count == 1)
        {
            return BuildEllipse(stroke.RawPoints[0].Position, Options.MaximumWidth * 0.4, 32);
        }

        IReadOnlyList<RawPoint> samples = stroke.Samples.Count > 1 ? stroke.Samples : stroke.RawPoints;
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

    private static void AddRoundCap(List<InkPoint2D> output, RawPoint center, InkPoint2D start, InkPoint2D end, int steps)
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
            output.Add(new InkPoint2D(
                center.Position.X + (Math.Cos(angle) * center.HalfWidth),
                center.Position.Y + (Math.Sin(angle) * center.HalfWidth)));
        }
    }

    private static IReadOnlyList<InkPoint2D> BuildEllipse(InkPoint2D center, double radius, int count)
    {
        var points = new InkPoint2D[count];
        for (int index = 0; index < count; index++)
        {
            double angle = Math.PI * 2 * index / count;
            points[index] = new InkPoint2D(center.X + (Math.Cos(angle) * radius), center.Y + (Math.Sin(angle) * radius));
        }

        return points;
    }

    private static void ValidateInput(InkInputPoint input)
    {
        if (!double.IsFinite(input.Position.X) || !double.IsFinite(input.Position.Y))
        {
            throw new ArgumentException("坐标必须是有限数值。", nameof(input));
        }

        if (!double.IsFinite(input.Pressure))
        {
            throw new ArgumentException("压感必须是有限数值。", nameof(input));
        }
    }

    private sealed class StrokeState(InkInputPoint firstPoint)
    {
        internal List<RawPoint> RawPoints { get; } = [new(firstPoint.Position, firstPoint.Timestamp, 0, 0, 0)];
        internal List<RawPoint> Samples { get; } = [];
        internal IReadOnlyList<InkPoint2D> Outline { get; set; } = [];
        internal int NarrowPointCount { get; set; }
        internal bool IsTap { get; set; }
    }

    private readonly record struct RawPoint(
        InkPoint2D Position,
        long Timestamp,
        double Speed,
        double RawHalfWidth,
        double HalfWidth)
    {
        internal double DistanceFrom(RawPoint other) => InkPoint2D.Distance(Position, other.Position);
    }
}