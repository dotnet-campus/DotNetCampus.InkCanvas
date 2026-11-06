using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace InkBeautifierAvaloniaDemo;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        WidthSlider.PropertyChanged += (_, _) => InkCanvas.MaximumWidth = WidthSlider.Value;
        ResponseSlider.PropertyChanged += (_, _) => InkCanvas.VelocityResponse = ResponseSlider.Value;
    }

    private async void ExportButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (!InkCanvas.HasRecordedInk)
        {
            return;
        }

        IStorageFile? file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "导出问题笔迹",
            SuggestedFileName = $"InkProblem-{DateTime.Now:yyyyMMdd-HHmmss}.txt",
            DefaultExtension = "txt",
            FileTypeChoices =
            [
                new FilePickerFileType("笔迹数据")
                {
                    Patterns = ["*.txt"]
                }
            ]
        });
        if (file is null)
        {
            return;
        }

        await using Stream stream = await file.OpenWriteAsync();
        stream.SetLength(0);
        InkCanvas.ExportProblemInk(stream);
    }

    private void ClearButton_OnClick(object? sender, RoutedEventArgs e) => InkCanvas.Clear();
}
