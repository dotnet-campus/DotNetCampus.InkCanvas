using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Skia;
using AvaloniaInkCanvasDemo.Views.ErasingView;
using DotNetCampus.Inking;
using DotNetCampus.Inking.Erasing;
using DotNetCampus.Inking.LogoExport;
using DotNetCampus.Inking.StrokeRenderers.WpfForSkiaInkStrokeRenderers;
using SkiaSharp;

namespace AvaloniaInkCanvasDemo.Views;

public partial class MainView : UserControl
{
    public MainView()
    {
        InitializeComponent();
        var settings = InkCanvas.AvaloniaSkiaInkCanvas.Settings;
        settings.EraserViewCreator = new DelegateEraserViewCreator(() => new CustomEraserView());
    }

    private void PenModeButton_OnClick(object? sender, RoutedEventArgs e)
    {
        InkCanvas.EditingMode = InkCanvasEditingMode.Ink;
    }

    private void EraserModeButton_OnClick(object? sender, RoutedEventArgs e)
    {
        InkCanvas.EditingMode = InkCanvasEditingMode.EraseByPoint;
    }

    private void SwitchRendererButton_OnClick(object? sender, RoutedEventArgs e)
    {
        var settings = InkCanvas.AvaloniaSkiaInkCanvas.Settings;

        if (settings.InkStrokeRenderer is null)
        {
            settings.InkStrokeRenderer = new WpfForSkiaInkStrokeRenderer();
        }
        else
        {
            settings.InkStrokeRenderer = null;
        }
    }

    private async void SaveStrokeAsSvgButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (InkCanvas.Strokes.Count == 0)
        {
            return;
        }

        // 让用户选择导出文件夹；若取消则使用默认时间戳子目录
        var saveFolder = await PickFolderAsync(fallbackFolderName: $"{DateTime.Now:yyyy-MM-dd_HH-mm-ss}");
        if (saveFolder == null) return;

        Directory.CreateDirectory(saveFolder);

        using var skPaint = new SKPaint();
        skPaint.IsAntialias = true;
        skPaint.Style = SKPaintStyle.Fill;

        for (var i = 0; i < InkCanvas.Strokes.Count; i++)
        {
            var saveSvgFile = Path.Join(saveFolder, $"{i}.svg");
            using var fileStream = File.Create(saveSvgFile);

            var stroke = InkCanvas.Strokes[i];

            var bounds = InkCanvas.Bounds.ToSKRect();
            using var skCanvas = SKSvgCanvas.Create(bounds, fileStream);

            skPaint.Color = stroke.Color;
            skCanvas.DrawPath(stroke.Path, skPaint);
        }
    }

    private async void ConvertToLogoButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (InkCanvas.Strokes.Count == 0)
        {
            return;
        }

        // 让用户选择导出文件夹；若取消则使用默认时间戳子目录
        var saveFolder = await PickFolderAsync(fallbackFolderName: $"Logo_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}");
        if (saveFolder == null) return;

        Directory.CreateDirectory(saveFolder);

        // 1) 计算笔迹中心，让 Logo 的 (0,0) 对齐到笔迹中心 → 回放不形变
        var (minX, minY, maxX, maxY) = InkToLogoConverter.GetBoundingBox(InkCanvas.Strokes);
        double cx = (minX + maxX) / 2.0;
        double cy = (minY + maxY) / 2.0;

        // 2) 一次性产出三种"整笔迹合并"Logo 文件，方便对比/调试
        //    standard .logo       : 优化（指数平滑 + RDP + LT/RT + FD 合并）— 日常发布
        //    debug   .abs.logo    : 纯 SETXY 绝对坐标（不优化、不算角度）— 排查 RDP/平滑丢细节
        //    debug   .raw.logo    : 原始点 + LT/RT + FD 合并（不平滑、不 RDP）— 排查 RT/LT 角度
        //
        // 不再生成 000.logo / 001.logo / 002.logo 这种 per-stroke 文件，
        // 那是无意义的——回放永远按整笔迹顺序来，逐笔文件无法直接执行。
        string baseName = "handwriting";
        File.WriteAllText(
            Path.Join(saveFolder, $"{baseName}.logo"),
            InkCanvas.ToLogoSource(flipY: true, originShiftX: cx, originShiftY: cy, mode: LogoExportMode.Optimized),
            System.Text.Encoding.UTF8);
        File.WriteAllText(
            Path.Join(saveFolder, $"{baseName}.abs.logo"),
            InkCanvas.ToLogoSource(flipY: true, originShiftX: cx, originShiftY: cy, mode: LogoExportMode.AbsoluteCoordinates),
            System.Text.Encoding.UTF8);
        File.WriteAllText(
            Path.Join(saveFolder, $"{baseName}.raw.logo"),
            InkCanvas.ToLogoSource(flipY: true, originShiftX: cx, originShiftY: cy, mode: LogoExportMode.RawRelativeAngles),
            System.Text.Encoding.UTF8);
    }

    /// <summary>
    /// 弹出文件夹选择对话框；用户取消时回退到 <c>AppContext.BaseDirectory/fallbackFolderName</c>。
    /// </summary>
    private async Task<string?> PickFolderAsync(string fallbackFolderName)
    {
        try
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel != null && topLevel.StorageProvider.CanOpen)
            {
                var picked = await topLevel.StorageProvider.OpenFolderPickerAsync(
                    new Avalonia.Platform.Storage.FolderPickerOpenOptions
                    {
                        Title = "选择导出文件夹",
                        AllowMultiple = false,
                    });
                if (picked != null && picked.Count > 0)
                {
                    return picked[0].Path.LocalPath;
                }
                // 用户取消 → 返回 null（调用方不写文件）
                return null;
            }
        }
        catch
        {
            // 平台不支持 storage provider 或调用失败时，回退到默认子目录
        }
        return Path.Join(AppContext.BaseDirectory, fallbackFolderName);
    }

    private void SelectingItemsControl_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count is 1)
        {
            if (e.AddedItems[0] is ISolidColorBrush brush)
            {
                InkCanvas.AvaloniaSkiaInkCanvas.Settings.InkColor = brush.Color.ToSKColor();
            }
        }
    }
}
