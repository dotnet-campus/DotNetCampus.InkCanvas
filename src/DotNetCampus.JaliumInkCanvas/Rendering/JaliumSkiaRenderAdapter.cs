using DotNetCampus.Inking.Primitive;
using Jalium.UI.Media;
using SkiaSharp;
using WpfInk;

namespace DotNetCampus.Inking.Rendering;

internal sealed class JaliumSkiaRenderAdapter
{
    public PathGeometry BuildStrokeGeometry(IReadOnlyList<InkStylusPoint> points, double inkThickness)
    {
        var geometry = new PathGeometry();
        if (points.Count < 2)
        {
            return geometry;
        }

        var context = new JaliumStreamGeometryContext(geometry);
        InkStrokeRenderer.Render(context, new StrokeRendererInfo
        {
            StylusPointCollection = points,
            Width = inkThickness,
            Height = inkThickness,
            FitToCurve = false,
        });
        return geometry;
    }

    public SolidColorBrush BuildBrush(SKColor color)
    {
        return new SolidColorBrush(Color.FromArgb(color.Alpha, color.Red, color.Green, color.Blue));
    }
}
