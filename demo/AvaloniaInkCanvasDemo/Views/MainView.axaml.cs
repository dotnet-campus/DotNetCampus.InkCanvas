using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Skia;
using AvaloniaInkCanvasDemo.Views.ErasingView;
using DotNetCampus.Inking;
using DotNetCampus.Inking.Erasing;
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

    private void SaveStrokeAsSvgButton_OnClick(object? sender, RoutedEventArgs e)
    {
        var saveFolder = Path.Join(AppContext.BaseDirectory, $"{DateTime.Now:yyyy-MM-dd_HH-mm-ss}");
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
