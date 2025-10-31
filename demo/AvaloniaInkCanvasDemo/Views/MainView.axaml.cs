using Avalonia.Controls;
using Avalonia.Interactivity;
using DotNetCampus.Inking;
using DotNetCampus.Inking.StrokeRenderers.WpfForSkiaInkStrokeRenderers;

namespace AvaloniaInkCanvasDemo.Views;

public partial class MainView : UserControl
{
    public MainView()
    {
        InitializeComponent();
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
        var settings = InkCanvas.SkiaInkCanvas.Settings;

        if (settings.InkStrokeRenderer is null)
        {
            settings.InkStrokeRenderer = new WpfForSkiaInkStrokeRenderer();
        }
        else
        {
            settings.InkStrokeRenderer = null;
        }
    }
}
