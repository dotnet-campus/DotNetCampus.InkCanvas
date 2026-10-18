namespace DotNetCampus.Inking.LogoExport;

/// <summary>
/// 笔迹转 Logo 语言的导出模式。
/// </summary>
public enum LogoExportMode
{
    /// <summary>
    /// 标准导出：指数平滑 + 曲率抽稀 + LT/RT 相对转角 + FD 合并
    /// （Kimi 黄金压缩规则，常规发布用）。
    /// </summary>
    Optimized = 0,

    /// <summary>
    /// 调试模式：纯 SETXY 绝对坐标连线，**不**优化、**不**算角度。
    /// 用于排查"RT/LT 角度错"还是"平滑丢细节"导致回放笔迹变形。
    /// </summary>
    AbsoluteCoordinates = 1,

    /// <summary>
    /// 调试模式：保留原始点列 + LT/RT 相对转角 + FD 合并，**不**平滑、**不**抽稀。
    /// 用于隔离排查"平滑丢细节" vs "RT/LT 角度算错"。
    ///   - 若此模式正确而 Optimized 变形 → 罪魁是平滑/抽稀；
    ///   - 若此模式也变形但 AbsoluteCoordinates 正确 → 罪魁是 RT/LT 角度算法。
    /// </summary>
    RawRelativeAngles = 2,
}
