using DotNetCampus.Inking.Primitive;

namespace DotNetCampus.Inking.Smoothing;

internal sealed class PressureSmoother
{
    public IReadOnlyList<InkStylusPoint> Smooth(
        IReadOnlyList<InkStylusPoint> points,
        float factor,
        float? previousPressure = null)
    {
        if (points.Count == 0)
        {
            return [];
        }

        var output = new List<InkStylusPoint>(points.Count);
        var lastPressure = previousPressure ?? points[0].Pressure;
        foreach (var point in points)
        {
            var p = point.Pressure;
            var smoothed = lastPressure + (p - lastPressure) * Math.Clamp(factor, 0f, 1f);
            output.Add(point with { Pressure = smoothed });
            lastPressure = smoothed;
        }

        return output;
    }
}
