using Avalonia.Controls;
using Avalonia.Interactivity;
using DotNetCampus.Inking;

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
}
