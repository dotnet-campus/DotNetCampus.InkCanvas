# DotNetCampus.InkCanvas

书写笔迹画板

这个项目起源于： https://github.com/AvaloniaUI/Avalonia/issues/1477

![](./docs/images/Image1.png)

### Avalonia InkCanvas

### 快速开始

1. 安装 NuGet 包 [`DotNetCampus.AvaloniaInkCanvas`](https://www.nuget.org/packages/DotNetCampus.AvaloniaInkCanvas)

   ```xml
     <ItemGroup>
       <PackageReference Include="DotNetCampus.AvaloniaInkCanvas" Version="1.0.1" />
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

### FAQ

**Q:** 这个库支持 AOT 编译吗？

**A:** 支持。这个库经过测试，确认可以在 AOT 环境下正常工作。

**Q:** 这个库可以在 Linux 环境下使用吗？

**A:** 可以。这个库基于 Avalonia 和 SkiaSharp 构建，这些都是支持 Linux 的跨平台框架。

**Q:** 我是否能直接用这个库制作出一个高性能笔迹白板应用？

**A:** 不可以。受限于 Avalonia 的渲染性能，目前这个库还不能用来制作高性能的笔迹白板应用。如果你需要一个高性能的笔迹白板应用，建议在 Windows 平台上添加 WPF 加速层，使用 WPF 绘制笔迹以提升性能；在 Linux 平台上使用 X11 原生绘制以提升性能。相关讨论请参考 <https://github.com/AvaloniaUI/Avalonia/discussions/18702> 和 <https://www.cnblogs.com/lindexi/p/18835048>

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

#### 如何自定义橡皮擦界面

1. 编写一个继承自 Control 的橡皮擦控件，且让其实现 IEraserView 接口
2. 将该橡皮擦控件的创建委托赋值给 InkCanvas.AvaloniaSkiaInkCanvas.Settings 的 EraserViewCreator 属性

示例代码如下：

```csharp
internal class CustomEraserView : Control, IEraserView
{
    ...
}

        var settings = InkCanvas.AvaloniaSkiaInkCanvas.Settings;
        settings.EraserViewCreator = new DelegateEraserViewCreator(() => new CustomEraserView());
```

注： 不能在使用过程中动态更换 EraserViewCreator 属性，仅支持在初始化时设置该属性。应当在任何橡皮擦视图创建之前设置该属性

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

#### `SkiaStroke.PointList` — 直接读取笔尖原始点列（零反射）

`SkiaStroke.Path` 是 Skia 渲染用的**外轮廓多边形**，看起来像线段，实际上是围绕中心线的封闭多边形，
直接连 `Path.Points` 会画出空心、变形的笔迹。

真实的、按书写时间排序的笔尖点列保存在 `SkiaStroke.PointList`（`IReadOnlyList<InkStylusPoint>`）。
4Darmygeometry 分支已将此属性提升为 public：

```csharp
using DotNetCampus.Inking;
using DotNetCampus.Inking.Primitive;

InkCanvas.StrokeCollected += (o, args) =>
{
    var stroke = args.SkiaStroke; // SkiaStroke
    var pts    = stroke.PointList; // IReadOnlyList<InkStylusPoint> — public，AOT/裁剪友好

    foreach (var p in pts)
    {
        // p.X, p.Y, p.Pressure, p.Timestamp, ...
    }
};
```

#### 将笔迹转换为 Logo 语言源码（PCLogo / MSWLogo / AOTLogoSharp 1.2.1+）

本分支内置 `DotNetCampus.Inking.LogoExport.InkToLogoConverter` 静态类，以及 `InkCanvas.ToLogoSource(...)` 扩展方法。
输出的是**标准 Logo 源码**，可直接交给任何 Logo 解释器（PCLogo、MSWLogo、
[AOTLogoSharp](https://www.nuget.org/packages/AOTLogoSharp.Drawing) 1.2.1+）由海龟回放。

支持三种导出模式（通过 `LogoExportMode` 选择）：

| 模式 | 说明 |
|---|---|
| `Optimized` | 指数平滑 + 曲率抽稀（默认）+ LT/RT 相对转角 + FD 合并（输出最小，1.0.1 推荐） |
| `AbsoluteCoordinates` | 纯 `SETXY` 绝对坐标，不平滑、不抽稀、不算角度（调试用） |
| `RawRelativeAngles` | 原始点列 + LT/RT + FD 合并，不平滑、不抽稀（调试用） |

**每一笔的起点、终点、第二个点始终原样保留**（首末点决定整笔的端点，第 2 个点决定 `SETH` 初始朝向），
所以优化过程**不会改变笔画整体方向**——后续 `RT/LT+FD` 链只是在端点之间的纯几何展开。

```csharp
using DotNetCampus.Inking;
using DotNetCampus.Inking.LogoExport;

// 1) 计算所有笔迹的包围盒，用中心点作为 Logo 坐标原点 (0,0)
var (minX, minY, maxX, maxY) = InkToLogoConverter.GetBoundingBox(InkCanvas.Strokes);
double cx = (minX + maxX) / 2.0;
double cy = (minY + maxY) / 2.0;

// 2) 转 Logo 源码
string logo = InkCanvas.ToLogoSource(
    flipY:         true,            // 屏幕 Y 向下 → Logo Y 向上
    originShiftX:  cx,
    originShiftY:  cy,
    mode:          LogoExportMode.Optimized);

// 3) 用 AOTLogoSharp 1.2.1+（或 PCLogo / MSWLogo）回放
File.WriteAllText("handwriting.logo", logo, System.Text.Encoding.UTF8);
```

`SkiaStroke`、`IReadOnlyList<SkiaStroke>`、`IReadOnlyList<IReadOnlyList<InkStylusPoint>>`
（点列形式，调用方已有原始笔尖点列时使用）、`InkCanvas` 四者都暴露了同名的
`ToLogoSource(...)` 扩展方法，签名一致，可以按需使用。

#### 用 `LogoExportOptions` 微调转换器

原先硬编码在 `InkToLogoConverter` 内部的常量（最小转角 / 最小步长 / 平滑 α / 抽稀 ε / 曲率阈值 等）
已经集中到一个 immutable 的公开配置类 `LogoExportOptions`，用 C# 9 init-only 语法即可覆盖任意一项：

```csharp
string logo = InkToLogoConverter.Convert(
    strokes,
    new LogoExportOptions
    {
        SmoothAlpha              = 0.5,   // 指数平滑 α（≤0 / ≥1 = 关闭）
        CurvatureAngleThresholdDeg = 8.0,  // 曲率抽稀的转角阈值（度）
        CurvatureMinGapPx        = 4.0,   // 直线段最小间距（像素）
        MinAngleDeg              = 0.5,   // 低于此角度的转角视为抖动（度）
        MinStepPx                = 0.5,   // 低于此距离的线段视为抖动（像素）
        MinMergedFdPx            = 0.5,   // 合并后 FD 低于此长度视为抖动不输出（像素）
    });
```

| 选项 | 默认 | 作用 |
|---|---|---|
| `SmoothAlpha` | 0.5 | 指数平滑 α；越小越平滑但滞后越大，越大越接近原始点列 |
| `MinSmoothedStepSq` | 1e-6 | 丢弃平滑后距离平方 < 此值的不动点（像素²） |
| `CurvatureAngleThresholdDeg` | 8° | 曲率抽稀的转角阈值：夹角 ≥ 此值的拐点必保留 |
| `CurvatureMinGapPx` | 4 px | 曲率抽稀的直线段最小间距：相邻保留点的距离下限 |
| `MinAngleDeg` | 0.5 | 低于此角度的转角视为抖动 |
| `MinStepPx` | 0.5 | 低于此距离的线段视为抖动 |
| `MinMergedFdPx` | 0.5 | 合并后 FD 低于此长度视为抖动不输出 |

#### 曲率抽稀

转换器使用**曲率抽稀**（不使用 RDP / 距离弦度量）。对每个内部点 P[i] 计算前后两段向量
（P[i-1]→P[i] 与 P[i]→P[i+1]）的夹角 θ，按下述规则决定保留：

- θ ≥ `CurvatureAngleThresholdDeg` → 视为**拐点**，必保留
- 否则若与上一个保留点的距离 ≥ `CurvatureMinGapPx` → 视为**直线段等距采样点**，保留
- 否则丢弃

这种方案对手写笔迹有两个关键保证：

- 每笔的**起点、终点、第 2 个点始终保留**。它们决定了整笔的端点 + `SETH` 初始朝向，
  所以简化后的笔迹在端点处与 `AbsoluteCoordinates` 输出**位级别一致**。
- 每笔**只输出一个 `SETH`**，后续是纯 `RT/LT+FD` 链——不存在"周期性 SETH 重置"把一笔连续曲线
  切成长度不一的折线段的问题。

如果对输出大小不敏感、只追求像素级回放准确度，可以改用 `LogoExportMode.AbsoluteCoordinates`——
该模式输出纯 `SETXY`，根本没有"旋转"这一概念，端点天然对齐。

#### 点列形式的 `Convert`

点列重载让你**不必构造 `SkiaStroke` 实例**就能转 Logo——当消费方手里已经是
`InkStylusPoint` 原始点列（例如从 `.meta` 调试文件反序列化得到，或者从录制回放里取出来）时
特别合适：

```csharp
IReadOnlyList<IReadOnlyList<InkStylusPoint>> pointLists = /* ... */;

string logo = InkToLogoConverter.Convert(
    pointLists,
    options:        new LogoExportOptions(),  // 默认：曲率抽稀 + 不再插多余 SETH
    flipY:          true,
    originShiftX:   cx,
    originShiftY:   cy,
    mode:           LogoExportMode.Optimized);

var (minX, minY, maxX, maxY) = InkToLogoConverter.GetBoundingBox(pointLists);
```

`GetBoundingBox` 同样有面向点列的重载，语义与 `SkiaStroke` 版完全一致。

转换器遵循的 Logo 方言规范：

| 命令 | 含义 |
|---|---|
| `SETH θ` | 设定绝对朝向；`0°` = 正北，顺时针增加；方向 = `(sin θ, cos θ)` |
| `LT a` | 逆时针旋转 `a` 度 |
| `RT a` | 顺时针旋转 `a` 度 |
| `FD d` | 沿当前朝向前进 `d` 单位 |
| `SETXY x y` | 跳到绝对坐标 `(x, y)` |
| `PU` / `PD` | 抬笔 / 落笔 |
| `HOME` | 回到 `(0, 0)`，朝向 0° |
| `CS` | 清屏 |

# 开源社区

如果你希望参与贡献，欢迎 [Pull Request](https://github.com/dotnet-campus/DotNetCampus.InkCanvas/pulls)，或给我们 [报告 Bug](https://github.com/dotnet-campus/DotNetCampus.InkCanvas/issues/new)

# 授权协议

[![](https://img.shields.io/badge/License-MIT-blue?style=flat-square)](./LICENSE)
