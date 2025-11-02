using System.Collections.ObjectModel;
using Avalonia.Media;

namespace AvaloniaInkCanvasDemo.ViewModels;

public class MainViewModel : ViewModelBase
{
    public MainViewModel()
    {
        SolidColorBrushCollection =
        [
            Brushes.Red,
            Brushes.Black,
            Brushes.Green,
            Brushes.Blue,
            Brushes.Orange,
            Brushes.Purple
        ];
    }

    public ObservableCollection<IBrush> SolidColorBrushCollection { get; }
}
