using DotNetCampus.Inking.Primitive;
using SkiaSharp;

namespace DotNetCampus.Inking.StrokeRenderers;

class SimpleSkiaInkStrokeRenderer : ISkiaInkStrokeRenderer
{
    private readonly SkiaSimpleInkRender _skiaSimpleInkRender = new SkiaSimpleInkRender();

    public SKPath RenderInkToPath(IReadOnlyList<InkStylusPoint> pointList, double inkThickness)
    {
    }
}