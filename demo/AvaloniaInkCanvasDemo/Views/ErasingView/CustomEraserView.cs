using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

using DotNetCampus.Inking.Erasing;

namespace AvaloniaInkCanvasDemo.Views.ErasingView;

internal class CustomEraserView : Control, IEraserView
{
    public CustomEraserView()
    {
        ViewPath = Geometry.Parse("M0,5.0093855C0,2.24277828,2.2303666,0,5.00443555,0L24.9955644,0C27.7594379,0,30,2.23861485,30,4.99982044L30,17.9121669C30,20.6734914,30,25.1514578,30,27.9102984L30,40.0016889C30,42.7621799,27.7696334,45,24.9955644,45L5.00443555,45C2.24056212,45,0,42.768443,0,39.9906145L0,5.0093855z");
        ViewPathFillBrush = new SolidColorBrush(new Color(0x33, 0, 0, 0));

        OutlinePathFillBrush = new SolidColorBrush(new Color(0xFF, 0xF2, 0xEE, 0xEB));

        var bounds = ViewPath.Bounds;
        Width = bounds.Width;
        Height = bounds.Height;

        HorizontalAlignment = HorizontalAlignment.Left;
        VerticalAlignment = VerticalAlignment.Top;
        IsHitTestVisible = false;

        var translateTransform = new TranslateTransform();
        _translateTransform = translateTransform;
        var scaleTransform = new ScaleTransform();
        _scaleTransform = scaleTransform;
        var transformGroup = new TransformGroup();
        transformGroup.Children.Add(_scaleTransform);
        transformGroup.Children.Add(_translateTransform);
        RenderTransform = transformGroup;

        _currentEraserSize = new Size(Width, Height);
    }

    private readonly TranslateTransform _translateTransform;

    private readonly ScaleTransform _scaleTransform;

    private Geometry ViewPath { get; }
    private IBrush ViewPathFillBrush { get; }

    private IBrush OutlinePathFillBrush { get; }

    private Size _currentEraserSize;

    public void Move(Point position)
    {
        _translateTransform.X = position.X - _currentEraserSize.Width / 2;
        _translateTransform.Y = position.Y - _currentEraserSize.Height / 2;
    }

    public void SetEraserSize(Size size)
    {
        _scaleTransform.ScaleX = size.Width / Width;
        _scaleTransform.ScaleY = size.Height / Height;

        _currentEraserSize = size;
    }

    public override void Render(DrawingContext context)
    {
        context.DrawGeometry(ViewPathFillBrush, null, ViewPath);
        context.DrawRectangle(OutlinePathFillBrush, null, new RoundedRect(new Rect(1, 1, 28, 43), 4));
    }
}
