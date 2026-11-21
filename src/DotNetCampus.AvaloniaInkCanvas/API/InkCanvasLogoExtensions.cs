using System.Collections.Generic;

using DotNetCampus.Inking.LogoExport;

namespace DotNetCampus.Inking;

/// <summary>
/// 为 <see cref="InkCanvas"/> 和 <see cref="SkiaStroke"/> 提供"笔迹 → Logo 源码"扩展方法。
/// 该扩展使任何使用 DotNetCampus.AvaloniaInkCanvas 库的应用都能直接调用，无需反射，
/// 完全兼容 AOT/裁剪发布。
///
/// <para><b>6 元配置</b>：本扩展也支持传入 <see cref="LogoExportOptions"/> 覆盖 6 个可调参数
/// （平滑 α / RDP ε / 最小转角 / 最小步长 / 平滑后最小步长² / 合并 FD 最小长度）。</para>
/// </summary>
public static class InkCanvasLogoExtensions
{
    /// <summary>
    /// 把 InkCanvas 上所有笔迹（多笔 SkiaStroke）转成标准 Logo 源码。
    /// 默认模式：指数平滑 + RDP 抽稀 + LT/RT 相对转角 + FD 合并（kImi 黄金压缩规则）。
    /// </summary>
    /// <param name="inkCanvas">InkCanvas 实例（取其 <see cref="InkCanvas.Strokes"/>）</param>
    /// <param name="options">6 元可配置参数，null 时取 <see cref="LogoExportOptions.Default"/></param>
    /// <param name="flipY">是否翻转 Y 轴（屏幕 Y 向下 → Logo Y 向上），默认 true</param>
    /// <param name="scale">坐标缩放系数，1.0 表示不缩放</param>
    /// <param name="enableFdMerge">是否合并连续无转角的 FD</param>
    /// <param name="originShiftX">X 方向原点平移量（屏幕坐标）</param>
    /// <param name="originShiftY">Y 方向原点平移量（屏幕坐标）</param>
    /// <param name="mode">导出模式（<see cref="LogoExportMode"/>）</param>
    /// <returns>Logo 源码字符串，可直接传给 AOTLogoSharp 解析</returns>
    public static string ToLogoSource(
        this InkCanvas inkCanvas,
        LogoExportOptions? options = null,
        bool flipY = true,
        double scale = 1.0,
        bool enableFdMerge = true,
        double originShiftX = 0.0,
        double originShiftY = 0.0,
        LogoExportMode mode = LogoExportMode.Optimized)
    {
        return InkToLogoConverter.Convert(
            inkCanvas.Strokes,
            options,
            flipY, scale, enableFdMerge,
            originShiftX, originShiftY, mode);
    }

    /// <summary>
    /// 把单笔 SkiaStroke 转成标准 Logo 源码。
    /// </summary>
    /// <param name="skiaStroke">单笔 SkiaStroke；其 <see cref="SkiaStroke.PointList"/> 按书写时间排序</param>
    /// <param name="options">6 元可配置参数，null 时取 <see cref="LogoExportOptions.Default"/></param>
    /// <param name="flipY">是否翻转 Y 轴</param>
    /// <param name="scale">坐标缩放系数</param>
    /// <param name="enableFdMerge">是否合并连续无转角的 FD</param>
    /// <param name="originShiftX">X 方向原点平移量</param>
    /// <param name="originShiftY">Y 方向原点平移量</param>
    /// <param name="mode">导出模式（<see cref="LogoExportMode"/>）</param>
    /// <returns>Logo 源码字符串</returns>
    public static string ToLogoSource(
        this SkiaStroke skiaStroke,
        LogoExportOptions? options = null,
        bool flipY = true,
        double scale = 1.0,
        bool enableFdMerge = true,
        double originShiftX = 0.0,
        double originShiftY = 0.0,
        LogoExportMode mode = LogoExportMode.Optimized)
    {
        return InkToLogoConverter.ConvertStroke(
            skiaStroke, options,
            flipY, scale, enableFdMerge,
            originShiftX, originShiftY, mode);
    }

    /// <summary>
    /// 把笔迹列表转成 Logo 源码（直接传入 <see cref="IReadOnlyList{SkiaStroke}"/>）。
    /// </summary>
    public static string ToLogoSource(
        this IReadOnlyList<SkiaStroke> strokes,
        LogoExportOptions? options = null,
        bool flipY = true,
        double scale = 1.0,
        bool enableFdMerge = true,
        double originShiftX = 0.0,
        double originShiftY = 0.0,
        LogoExportMode mode = LogoExportMode.Optimized)
    {
        return InkToLogoConverter.Convert(
            strokes, options,
            flipY, scale, enableFdMerge,
            originShiftX, originShiftY, mode);
    }
}
