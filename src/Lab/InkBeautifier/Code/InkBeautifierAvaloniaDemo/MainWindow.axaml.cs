using Avalonia.Controls;
using Avalonia.Interactivity;

namespace InkBeautifierAvaloniaDemo;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        WidthSlider.PropertyChanged += (_, _) => InkCanvas.MaximumWidth = WidthSlider.Value;
        ResponseSlider.PropertyChanged += (_, _) => InkCanvas.VelocityResponse = ResponseSlider.Value;
    }

    private void ClearButton_OnClick(object? sender, RoutedEventArgs e) => InkCanvas.Clear();
}
