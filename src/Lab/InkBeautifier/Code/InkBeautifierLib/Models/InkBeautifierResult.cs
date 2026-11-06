using InkBeautifierLib.Geometry;

namespace InkBeautifierLib.Models;

/// <summary>
/// 表示一次输入处理后可直接填充绘制的闭合笔迹轮廓。
/// </summary>
public sealed record InkBeautifierResult
(
    long StrokeId,
    IReadOnlyList<InkPoint2D> Outline,
    bool IsCompleted,
    bool WasAccepted
)
{
    /// <summary>
    /// 获取构成完整笔迹的全部闭合轮廓。
    /// </summary>
    public IReadOnlyList<IReadOnlyList<InkPoint2D>> Outlines { get; init; } =
        Outline.Count == 0 ? [] : [Outline];
}
