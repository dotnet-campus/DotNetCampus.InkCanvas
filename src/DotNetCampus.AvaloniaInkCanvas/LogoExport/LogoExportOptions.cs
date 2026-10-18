namespace DotNetCampus.Inking.LogoExport;

/// <summary>
/// <see cref="InkToLogoConverter"/> 的可配置参数。
/// 把原先硬编码在 <c>InkToLogoConverter</c> 内部的常量
/// （最小转角 / 最小步长 / 平滑 α / 曲率抽稀阈值 / 曲率抽稀最小间距 / 合并 FD 最小长度）
/// 集中到一个 immutable 配置对象里，让调用方按需覆盖。
///
/// <para><b>用法</b>：</para>
/// <code>
/// // 用默认值
/// var logo = InkToLogoConverter.Convert(strokes);
///
/// // 只改一项
/// var logo = InkToLogoConverter.Convert(
///     strokes,
///     new LogoExportOptions { CurvatureAngleThresholdDeg = 12.0 });
/// </code>
///
/// <para><b>配置项</b>（按转换流水线顺序）：</para>
/// <list type="number">
///   <item><see cref="SmoothAlpha"/>：指数平滑 α</item>
///   <item><see cref="MinSmoothedStepSq"/>：平滑后两点最小距离平方（过滤不动点）</item>
///   <item><see cref="CurvatureAngleThresholdDeg"/>：曲率抽稀的转角阈值（度），高于此值的拐点必保留</item>
///   <item><see cref="CurvatureMinGapPx"/>：曲率抽稀的直线段最小间距（像素）</item>
///   <item><see cref="MinAngleDeg"/>：最小转角（度）</item>
///   <item><see cref="MinStepPx"/>：最小步长（像素）</item>
///   <item><see cref="MinMergedFdPx"/>：合并后 FD 最小长度（像素）</item>
/// </list>
/// </summary>
public sealed class LogoExportOptions
{
    /// <summary>
    /// 指数平滑因子 α：S_t = α * Y_t + (1-α) * S_{t-1}。
    /// 0.5 比 0.3 更接近原始点 → 保留曲线细节（避免抽稀后形状变形）。
    /// 默认 0.5；&lt;= 0 或 &gt;= 1 表示不平滑。
    /// </summary>
    public double SmoothAlpha { get; init; } = 0.5;

    /// <summary>
    /// 平滑后两点的最小距离平方（像素²），低于此值视为不动点直接丢弃。
    /// 防止连续多帧都落在同一点上造成 FD 指令 0。默认 1e-6。
    /// </summary>
    public double MinSmoothedStepSq { get; init; } = 1e-6;

    /// <summary>
    /// 曲率抽稀的转角阈值（度）。
    /// 当某点 P[i] 的"前后两段向量夹角 ≥ 该阈值"时把 P[i] 视为拐点保留，
    /// 即使它距离前后段弦很近。默认 8°；&lt;= 0 时退化为 8°，&gt;= 90 时等同不抽稀。
    /// </summary>
    public double CurvatureAngleThresholdDeg { get; init; } = 8.0;

    /// <summary>
    /// 曲率抽稀的直线段最小间距（像素）。
    /// 即使一段路径几乎完全直线（转角 &lt; 阈值），相邻两个被保留点的距离也不能小于该值，
    /// 防止长直线段被压成单点。默认 4 px。
    /// </summary>
    public double CurvatureMinGapPx { get; init; } = 4.0;

    /// <summary>
    /// 最小转角（度），低于此角度视为抖动，不输出 RT/LT。默认 0.5。
    /// </summary>
    public double MinAngleDeg { get; init; } = 0.5;

    /// <summary>
    /// 最小步长（像素），低于此距离视为抖动，不输出 FD。默认 0.5。
    /// </summary>
    public double MinStepPx { get; init; } = 0.5;

    /// <summary>
    /// 合并后 FD 的最小长度（像素），低于此长度视为抖动不输出。默认 0.5。
    /// </summary>
    public double MinMergedFdPx { get; init; } = 0.5;

    /// <summary>
    /// 默认配置。曲率抽稀参数：<see cref="CurvatureAngleThresholdDeg"/> = 8° / <see cref="CurvatureMinGapPx"/> = 4 px。
    /// </summary>
    public static LogoExportOptions Default { get; } = new();

    /// <summary>
    /// 浅拷贝出当前配置的副本，方便在原配置基础上改一项。
    /// </summary>
    public LogoExportOptions Clone() => new()
    {
        SmoothAlpha = SmoothAlpha,
        MinSmoothedStepSq = MinSmoothedStepSq,
        CurvatureAngleThresholdDeg = CurvatureAngleThresholdDeg,
        CurvatureMinGapPx = CurvatureMinGapPx,
        MinAngleDeg = MinAngleDeg,
        MinStepPx = MinStepPx,
        MinMergedFdPx = MinMergedFdPx,
    };
}
