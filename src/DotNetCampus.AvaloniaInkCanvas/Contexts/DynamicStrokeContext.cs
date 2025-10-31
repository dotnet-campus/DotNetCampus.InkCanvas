using DotNetCampus.Inking.Interactives;

namespace DotNetCampus.Inking.Contexts;

/// <summary>
/// 动态笔迹层的上下文，一个手指落下一个对象
/// </summary>
class DynamicStrokeContext
{
    public DynamicStrokeContext(InkingModeInputArgs lastInputArgs, AvaloniaSkiaInkCanvas canvas)
    {
        LastInputArgs = lastInputArgs;

        var settings = canvas.Context.Settings;

        SkiaSimpleInkRender? simpleInkRender = null;

        if(settings.InkStrokeRenderer is null)
        {
            simpleInkRender = canvas.SimpleInkRender;
        }

        Stroke = new SkiaStroke(InkId.NewId())
        {
            Color = settings.InkColor,
            InkThickness = settings.InkThickness,
            IgnorePressure = settings.IgnorePressure,
            InkStrokeRenderer = settings.InkStrokeRenderer,
            SimpleInkRender = simpleInkRender,
        };
    }

    public InkingModeInputArgs LastInputArgs { get; }

    public int Id => LastInputArgs.Id;

    public SkiaStroke Stroke { get; }
    public override string ToString() => $"DynamicStrokeContext_{Id}";
}
