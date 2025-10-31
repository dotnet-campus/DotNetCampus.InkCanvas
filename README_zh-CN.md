# DotNetCampus.InkCanvas

书写笔迹画板

这个项目起源于： https://github.com/AvaloniaUI/Avalonia/issues/1477

![](./docs/images/Image1.png)

### Avalonia InkCanvas

### 快速开始

1. 安装 NuGet 包 [`DotNetCampus.AvaloniaInkCanvas`](https://www.nuget.org/packages/DotNetCampus.AvaloniaInkCanvas)

   ```xml
     <ItemGroup>
       <PackageReference    Include="DotNetCampus.AvaloniaInkCanvas"    Version="1.0.0-alpha.2" />
     </ItemGroup>
   ```

2. 在 XAML 中使用 InkCanvas 控件

   ```xml
   xmlns:inking="using:DotNetCampus.Inking"
   
   <inking:InkCanvas x:Name="InkCanvas"/>
   ```

3. 在代码中使用 InkCanvas 控件，切换不同的输入模式

   ```csharp
        // 切换书写模式
        InkCanvas.EditingMode = InkCanvasEditingMode.Ink;
        // 切换橡皮擦模式
        InkCanvas.EditingMode = InkCanvasEditingMode.EraseByPoint;
   ```

### 进阶用法

#### 切换笔迹渲染器

当前内置了以下笔迹渲染器：

- `SimpleInkRender`: 最简单的笔迹渲染器，适合大部分场景，速度快，逻辑简单。但在某些输入情况下，笔迹可能出现锯齿等问题
- `WpfForSkiaInkStrokeRenderer`: 使用 WPF 的笔迹渲染算法的渲染器，适合对笔迹质量要求较高的场景，但性能相对较低。其实现代码源于 WPF 开源仓库，逻辑复杂

切换笔迹渲染器示例：

```csharp
 AvaloniaSkiaInkCanvasSettings settings = InkCanvas.SkiaInkCanvas.Settings;
 // 使用 WPF 的笔迹渲染算法的渲染器
 settings.InkStrokeRenderer = new WpfForSkiaInkStrokeRenderer();
 // 使用默认简单的笔迹渲染器
 settings.InkStrokeRenderer = null;
```

注： 使用 `WpfForSkiaInkStrokeRenderer` 仅使用 WPF 开源仓库中的笔迹渲染算法代码，不依赖 WPF 框架本身

#### 处理笔迹收集事件

```csharp
        InkCanvas.StrokeCollected += (o, args) =>
        {
            var addedStroke = args.SkiaStroke;
        };
```

#### 处理笔迹擦除事件

```csharp
        InkCanvas.StrokeErased += (o, args) =>
        {
            foreach (ErasedSkiaStroke erasedSkiaStroke in args.ErasingSkiaStrokeList)
            {
                if (erasedSkiaStroke.IsErased)
                {
                    // 被擦掉的笔迹
                    IReadOnlyList<SkiaStroke> newStrokes = erasedSkiaStroke.NewStrokeList;

                    // 一段笔迹可以被擦掉成多段笔迹。但也可能被完全擦掉，变成 0 段笔迹
                    foreach (var skiaStroke in newStrokes)
                    {
                        
                    }
                }
                else
                {
                    // 没有被擦掉的笔迹，保持原样
                    SkiaStroke originalStroke = erasedSkiaStroke.OriginStroke;
                }
            }
        };
```

#### 控制橡皮擦属性

通过 AvaloniaSkiaInkCanvasSettings 进行控制，例如：

```csharp
        AvaloniaSkiaInkCanvasSettings settings = InkCanvas.SkiaInkCanvas.Settings;
        settings.EraserSize = new Size(100, 200);
```

#### 将笔迹导出为 SVG 图片

演示将每一笔笔迹导出为单独的 SVG 图片：

```csharp
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
```

# 开源社区

如果你希望参与贡献，欢迎 [Pull Request](https://github.com/dotnet-campus/DotNetCampus.InkCanvas/pulls)，或给我们 [报告 Bug](https://github.com/dotnet-campus/DotNetCampus.InkCanvas/issues/new)

# 授权协议

[![](https://img.shields.io/badge/License-MIT-blue?style=flat-square)](./LICENSE)