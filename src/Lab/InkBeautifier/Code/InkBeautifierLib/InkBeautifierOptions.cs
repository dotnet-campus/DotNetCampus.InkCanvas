namespace InkBeautifierLib;

/// <summary>
/// 配置笔迹宽度和采样行为。
/// </summary>
public sealed record InkBeautifierOptions
{
    /// <summary>
    /// 获取或设置低速时的最大笔宽。
    /// </summary>
    public double MaximumWidth { get; init; } = 12;

    /// <summary>
    /// 获取或设置速度对宽度的衰减强度。
    /// </summary>
    public double VelocityResponse { get; init; } = 2;

    /// <summary>
    /// 获取或设置接受移动点所需的最小时间间隔（毫秒）。
    /// </summary>
    public double MinimumTimeInterval { get; init; } = 7;

    /// <summary>
    /// 获取或设置接受移动点所需的最小距离。
    /// </summary>
    public double MinimumDistance { get; init; } = 2;

    /// <summary>
    /// 获取或设置压感对笔宽的影响程度，范围为 0 到 1。
    /// </summary>
    public double PressureInfluence { get; init; }

    internal void Validate()
    {
        if (!double.IsFinite(MaximumWidth) || MaximumWidth <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(MaximumWidth));
        }

        if (!double.IsFinite(VelocityResponse) || VelocityResponse < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(VelocityResponse));
        }

        if (!double.IsFinite(MinimumTimeInterval) || MinimumTimeInterval < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(MinimumTimeInterval));
        }

        if (!double.IsFinite(MinimumDistance) || MinimumDistance < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(MinimumDistance));
        }

        if (!double.IsFinite(PressureInfluence) || PressureInfluence is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(PressureInfluence));
        }
    }
}