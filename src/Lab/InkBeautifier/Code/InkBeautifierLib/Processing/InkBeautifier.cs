using InkBeautifierLib.Configuration;
using InkBeautifierLib.Geometry;
using InkBeautifierLib.Models;

namespace InkBeautifierLib.Processing;

/// <summary>
/// 将离散输入点转换为平滑、动态宽度的闭合笔迹轮廓。
/// </summary>
public sealed partial class InkBeautifier
{
    /// <summary>
    /// 初始化笔迹美化器。
    /// </summary>
    public InkBeautifier(InkBeautifierOptions? options = null)
    {
        Options = options ?? new InkBeautifierOptions();
        Options.Validate();
    }

    private readonly Dictionary<long, StrokeState> _strokes = [];

    /// <summary>
    /// 获取当前配置。
    /// </summary>
    public InkBeautifierOptions Options { get; }

    /// <summary>
    /// 处理一个输入采样点并返回该笔的最新完整轮廓。
    /// </summary>
    public InkBeautifierResult Process(InkInputPoint input)
    {
        using var diagnostic = InkPerformanceDiagnostics.Measure(InkPerformanceDiagnostics.Stage.Process);
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
        StrokeGeometry geometry = accepted
            ? BuildGeometry(stroke, input.Status == InkPointerStatus.Up)
            : new StrokeGeometry(stroke.Outline, stroke.Outlines);
        stroke.Outline = geometry.Outline;
        stroke.Outlines = geometry.Outlines;

        bool completed = input.Status == InkPointerStatus.Up;
        if (completed)
        {
            _strokes.Remove(input.StrokeId);
        }

        return new InkBeautifierResult
            (input.StrokeId, geometry.Outline, completed, accepted) { Outlines = geometry.Outlines };
    }

    /// <summary>
    /// 清除全部尚未完成的笔迹状态。
    /// </summary>
    public void Reset() => _strokes.Clear();

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

        if (!Enum.IsDefined(input.Status))
        {
            throw new ArgumentException("指针状态无效。", nameof(input));
        }
    }
}
