using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using DotNetCampus.Inking.Contexts;
using DotNetCampus.Inking.Interactives;
using DotNetCampus.Inking.Utils;
using SkiaSharp;
//using Microsoft.Maui.Graphics;
using Point = Avalonia.Point;
using Rect = Avalonia.Rect;
using Size = Avalonia.Size;

namespace DotNetCampus.Inking.Erasing;

public class AvaloniaSkiaInkCanvasEraserMode
{
    public AvaloniaSkiaInkCanvasEraserMode(AvaloniaSkiaInkCanvas inkCanvas)
    {
        InkCanvas = inkCanvas;
    }

    private void InkCanvas_PointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        _debugEraserSizeScale += e.Delta.Y;
    }

    private double _debugEraserSizeScale = 0;

    public AvaloniaSkiaInkCanvas InkCanvas { get; }
    private AvaloniaSkiaInkCanvasSettings Settings => InkCanvas.Settings;

    public bool IsErasing { get; private set; }
    private int MainEraserInputId { set; get; }

    private PointPathEraserManager PointPathEraserManager { get; } = new PointPathEraserManager();

    private IEraserView EraserView
    {
        get
        {
            if (_eraserView is null)
            {
                var eraserViewCreator = Settings.EraserViewCreator;
                _eraserView = eraserViewCreator?.CreateEraserView()
                    ?? new EraserView();
            }

            return _eraserView;
        }
    }

    private IEraserView? _eraserView;

    private void StartEraser()
    {
#if DEBUG
        var topLevel = TopLevel.GetTopLevel(InkCanvas)!;
        topLevel.PointerWheelChanged -= InkCanvas_PointerWheelChanged;
        topLevel.PointerWheelChanged += InkCanvas_PointerWheelChanged;
#endif

        var staticStrokeList = InkCanvas.StaticStrokeList;
        PointPathEraserManager.StartEraserPointPath(staticStrokeList);

        if (EraserView is Control eraserView)
        {
            InkCanvas.AddChild(eraserView);
        }
    }

    public void EraserDown(in InkingModeInputArgs args)
    {
        InkCanvas.EnsureInputConflicts();
        if (!IsErasing)
        {
            MainEraserInputId = args.Id;

            IsErasing = true;

            StartEraser();

            EraserView.SetEraserSize(Settings.EraserSize);
            EraserView.Move(args.Position.ToAvaloniaPoint());
            InkCanvas.InvalidateVisual();
        }
        else
        {
            // 忽略其他的输入点
        }
    }

    public void EraserMove(in InkingModeInputArgs args)
    {
        InkCanvas.EnsureInputConflicts();
        if (IsErasing && args.Id == MainEraserInputId)
        {
            // 擦除
            var eraserWidth = Settings.EraserSize.Width;
            var eraserHeight = Settings.EraserSize.Height;

#if DEBUG
            if (_debugEraserSizeScale > 0)
            {
                _debugEraserSizeScale = Math.Min(100, _debugEraserSizeScale);

                eraserWidth *= (1 + _debugEraserSizeScale / 10);
                eraserHeight *= (1 + _debugEraserSizeScale / 10);
            }
            else if (_debugEraserSizeScale < -10)
            {
                _debugEraserSizeScale = Math.Max(-100, _debugEraserSizeScale);

                eraserWidth *= (1 + _debugEraserSizeScale / 100);
                eraserHeight *= (1 + _debugEraserSizeScale / 100);
            }
#endif

            var rect = new Rect(args.Position.X - eraserWidth / 2, args.Position.Y - eraserHeight / 2, eraserWidth, eraserHeight);
            PointPathEraserManager.Move(rect.ToRect2D());

            EraserView.SetEraserSize(new Size(eraserWidth, eraserHeight));
            EraserView.Move(args.Position.ToAvaloniaPoint());
            InkCanvas.InvalidateVisual();
        }
    }

    public void EraserUp(in InkingModeInputArgs args)
    {
        InkCanvas.EnsureInputConflicts();
        if (IsErasing && args.Id == MainEraserInputId)
        {
            IsErasing = false;
            var pointPathEraserResult = PointPathEraserManager.Finish();

            var skiaStrokeList = pointPathEraserResult.ErasingSkiaStrokeList
                .SelectMany(t => t.IsErased 
                    ? t.NewStrokeList // 被擦掉的，使用新的笔迹列表替代
                    : [t.OriginStroke]); // 没有被擦掉的，使用原笔迹

            InkCanvas.ResetStaticStrokeListByEraserResult(skiaStrokeList);

            ClearEraser();

            ErasingCompleted?.Invoke(this, new ErasingCompletedEventArgs(pointPathEraserResult.ErasingSkiaStrokeList));
        }
    }

    private void ClearEraser()
    {
        if (EraserView is Control eraserView)
        {
            InkCanvas.RemoveChild(eraserView);
        }
    }

    public event EventHandler<ErasingCompletedEventArgs>? ErasingCompleted;

    public void Render(DrawingContext context)
    {
        context.Custom(new EraserModeCustomDrawOperation(this));
    }

    class EraserModeCustomDrawOperation : ICustomDrawOperation
    {
        public EraserModeCustomDrawOperation(AvaloniaSkiaInkCanvasEraserMode eraserMode)
        {
            var pointPathEraserManager = eraserMode.PointPathEraserManager;
            IReadOnlyList<SkiaStrokeDrawContext> drawContextList = pointPathEraserManager.GetDrawContextList();
            DrawContextList = drawContextList;

            if (drawContextList.Count == 0)
            {
                Bounds = new Rect(0, 0, 0, 0);
            }
            else
            {
                Rect bounds = drawContextList[0].DrawBounds;

                for (var i = 1; i < drawContextList.Count; i++)
                {
                    bounds = bounds.Union(drawContextList[i].DrawBounds);
                }

                Bounds = bounds;
            }
        }

        private IReadOnlyList<SkiaStrokeDrawContext> DrawContextList { get; }

        public void Dispose()
        {
            foreach (var skiaStrokeDrawContext in DrawContextList)
            {
                skiaStrokeDrawContext.Dispose();
            }
        }

        public bool Equals(ICustomDrawOperation? other)
        {
            return false;
        }

        public bool HitTest(Point p)
        {
            return false;
        }

        public void Render(ImmediateDrawingContext context)
        {
            var skiaSharpApiLeaseFeature = context.TryGetFeature<ISkiaSharpApiLeaseFeature>();
            if (skiaSharpApiLeaseFeature == null)
            {
                return;
            }

            using var skiaSharpApiLease = skiaSharpApiLeaseFeature.Lease();
            var canvas = skiaSharpApiLease.SkCanvas;

            using var skPaint = new SKPaint();
            skPaint.Color = SKColors.Red;
            skPaint.Style = SKPaintStyle.Fill;

            skPaint.IsAntialias = true;

            skPaint.StrokeWidth = 10;

            foreach (var drawContext in DrawContextList)
            {
                // 绘制
                skPaint.Color = drawContext.Color;
                canvas.DrawPath(drawContext.Path, skPaint);
            }
        }

        public Rect Bounds { get; }
    }
}