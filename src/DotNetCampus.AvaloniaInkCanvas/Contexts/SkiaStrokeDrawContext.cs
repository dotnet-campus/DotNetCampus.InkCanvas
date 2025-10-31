using Avalonia;
using SkiaSharp;

namespace DotNetCampus.Inking.Contexts;

readonly record struct SkiaStrokeDrawContext(SKColor Color, SKPath Path, Rect DrawBounds, SKMatrix Transform, bool ShouldDisposePath) : IDisposable
{
    public void Dispose()
    {
        if (ShouldDisposePath)
        {
            Path.Dispose();
        }
    }
}

static class SkiaStrokeDrawContextExtension
{
    public static void DrawStroke(this SKCanvas canvas, in SkiaStrokeDrawContext skiaStrokeDrawContext, SKPaint skPaint)
    {
        skPaint.Color = skiaStrokeDrawContext.Color;
        var transform = skiaStrokeDrawContext.Transform;
        var useTransform = transform != SKMatrix.Empty && transform != SKMatrix.Identity;
        if (useTransform)
        {
            canvas.Save();
            canvas.Concat(ref transform);
        }

        canvas.DrawPath(skiaStrokeDrawContext.Path, skPaint);

        if (useTransform)
        {
            canvas.Restore();
        }
    }
}