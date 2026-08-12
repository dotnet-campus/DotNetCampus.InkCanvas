namespace InkBeautifierLib;

/// <summary>
/// 表示一次输入处理后可直接填充绘制的闭合笔迹轮廓。
/// </summary>
public sealed record InkBeautifierResult(
    long StrokeId,
    IReadOnlyList<InkPoint2D> Outline,
    bool IsCompleted,
    bool WasAccepted);