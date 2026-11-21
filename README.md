# DotNetCampus.InkCanvas

The InkCanvas control for .NET applications, such as Avalonia, providing a versatile canvas for handwriting and drawing.

书写笔迹画板

| Build | NuGet |
|--|--|
|![](https://github.com/dotnet-campus/DotNetCampus.InkCanvas/workflows/.NET%20Build/badge.svg)|[![](https://img.shields.io/nuget/v/DotNetCampus.AvaloniaInkCanvas.svg)](https://www.nuget.org/packages/DotNetCampus.AvaloniaInkCanvas)|

[中文文档](./README_zh-CN.md)

This project originated from: https://github.com/AvaloniaUI/Avalonia/issues/1477

![](./docs/images/Image1.png)

## Avalonia InkCanvas

### Quick Start

1. Install the NuGet package [`DotNetCampus.AvaloniaInkCanvas`](https://www.nuget.org/packages/DotNetCampus.AvaloniaInkCanvas)

   ```xml
     <ItemGroup>
       <PackageReference Include="DotNetCampus.AvaloniaInkCanvas" Version="1.0.1" />
     </ItemGroup>
   ```

2. Use the `InkCanvas` control in XAML:

   ```xml
   xmlns:inking="using:DotNetCampus.Inking"
   
   <inking:InkCanvas x:Name="InkCanvas"/>
   ```

3. Switch input modes in code:

    ```csharp
          // Switch to ink mode
          InkCanvas.EditingMode = InkCanvasEditingMode.Ink;
          // Switch to eraser mode
          InkCanvas.EditingMode = InkCanvasEditingMode.EraseByPoint;
    ```

### FAQ

**Q:** Does this library support AOT (Ahead-Of-Time) compilation?

**A:** Yes, this library supports AOT compilation. It has been tested and confirmed to work correctly in AOT environments.

**Q:** Can this library be used in Linux environments?

**A:** Yes, this library can be used in Linux environments. It is built on Avalonia and SkiaSharp, which are cross-platform frameworks that support Linux.

**Q:** Can I directly use this library to create a high-performance handwriting whiteboard application?

**A:** No, due to the rendering performance limitations of Avalonia, this library cannot currently be used to create high-performance handwriting whiteboard applications. If you need a high-performance handwriting whiteboard application, it is recommended to add a WPF acceleration layer on the Windows platform to use WPF for rendering strokes to improve performance; on the Linux platform, use native X11 rendering to enhance performance. For related discussions, please refer to <https://github.com/AvaloniaUI/Avalonia/discussions/18702>

### Advanced Usage

#### Switch stroke renderer

The library includes the following stroke renderers by default:

- `SimpleInkRender`: A simple and fast stroke renderer suitable for most scenarios. It uses a straightforward algorithm and performs well, but in some input cases strokes may showaliasing.
- `WpfForSkiaInkStrokeRenderer`: A renderer that uses WPF's stroke rendering algorithm adapted for Skia. It provides higher-quality strokes at the cost of performance. Its implementation is based on the WPF open-source codebase and is more complex.

Example of switching the stroke renderer:

```csharp
AvaloniaSkiaInkCanvasSettings settings = InkCanvas.SkiaInkCanvas.Settings;

// Use the WPF-based stroke renderer
settings.InkStrokeRenderer = new WpfForSkiaInkStrokeRenderer();

// Revert to the default (simple) stroke renderer
settings.InkStrokeRenderer = null;
```

Note: Using `WpfForSkiaInkStrokeRenderer` only utilizes the stroke rendering algorithm from the WPF open-source repository and does not depend on the WPF framework itself.

#### Handle stroke collected event

```csharp
        InkCanvas.StrokeCollected += (o, args) =>
        {
            var addedStroke = args.SkiaStroke; // Use addedStroke as needed
        };
```


#### Handle stroke erased event

```csharp
        InkCanvas.StrokeErased += (o, args) =>
        {
            foreach (ErasedSkiaStroke erasedSkiaStroke in args.ErasingSkiaStrokeList)
            {
                if (erasedSkiaStroke.IsErased)
                {
                    // The stroke was erased; it may be split into multiple new strokes,
                    // or it may be fully erased resulting in 0 new strokes. 
                    IReadOnlyList<SkiaStroke> newStrokes = erasedSkiaStroke.NewStrokeList;

                    foreach (var skiaStroke in newStrokes)
                    {
                        // Process each resulting stroke segment
                    }
                }
                else
                {
                    // The stroke was not erased; it remains unchanged
                    SkiaStroke originalStroke = erasedSkiaStroke.OriginStroke;
                }
            }
        };
```


#### Control eraser properties

Control eraser behavior via `AvaloniaSkiaInkCanvasSettings`, for example:

```csharp
        AvaloniaSkiaInkCanvasSettings settings = InkCanvas.SkiaInkCanvas.Settings;
        settings.EraserSize = new Size(100, 200);
```

#### How to customize the eraser view

1. Create a custom eraser control by inheriting from `Control` and implementing the `IEraserView` interface.
2. Assign a delegate that creates an instance of your custom eraser control to the `EraserViewCreator` property of `InkCanvas.AvaloniaSkiaInkCanvas.Settings`.

Example code:

```csharp
internal class CustomEraserView : Control, IEraserView
{
    ...
}

        var settings = InkCanvas.AvaloniaSkiaInkCanvas.Settings;
        settings.EraserViewCreator = new DelegateEraserViewCreator(() => new CustomEraserView());
```

Note: You cannot dynamically change the `EraserViewCreator` property during usage; it should only be set during initialization. Ensure to set this property before any eraser views are created.

#### Save strokes as SVG image

You can export the strokes drawn on the `InkCanvas` to an SVG image format. Here's an example of how to do this:

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

#### `SkiaStroke.PointList` — read the raw stylus points (no reflection)

`SkiaStroke.Path` is the **outer contour polygon** used by the Skia renderer. It looks like a stroke but is
actually a closed ring around the center line, so drawing a poly-line from `Path.Points` will produce a
hollow, deformed shape.

The real, time-ordered stylus point sequence lives in `SkiaStroke.PointList` (`IReadOnlyList<InkStylusPoint>`).
In the 4Darmygeometry fork it is exposed as a public property:

```csharp
using DotNetCampus.Inking;
using DotNetCampus.Inking.Primitive;

InkCanvas.StrokeCollected += (o, args) =>
{
    var stroke = args.SkiaStroke; // SkiaStroke
    var pts    = stroke.PointList; // IReadOnlyList<InkStylusPoint> — public, AOT-friendly

    foreach (var p in pts)
    {
        // p.X, p.Y, p.Pressure, p.Timestamp, ...
    }
};
```

#### Convert handwriting to Logo language source (PCLogo / MSWLogo / AOTLogoSharp 1.2.1+)

The fork ships `DotNetCampus.Inking.LogoExport.InkToLogoConverter` and an `InkCanvas.ToLogoSource(...)`
extension method. The output is **standard Logo source code** that any Logo interpreter (PCLogo, MSWLogo,
[AOTLogoSharp](https://www.nuget.org/packages/AOTLogoSharp.Drawing) 1.2.1+) can execute to play back
the handwriting via a turtle.

Three export modes are supported (selected via `LogoExportMode`):

| Mode | Description |
|---|---|
| `Optimized` | Exponential smoothing + curvature simplification (default) + LT/RT relative angles + FD merge (smallest output, 1.0.1) |
| `AbsoluteCoordinates` | Pure `SETXY` absolute coordinates, no smoothing / simplification / angles (debug) |
| `RawRelativeAngles` | Raw points + LT/RT + FD merge, no smoothing / simplification (debug) |

The start, end and the second point of every stroke are always preserved (the endpoints fix
the stroke's path; the second point fixes the initial `SETH` heading), so the optimization
process never changes a stroke's overall direction — the subsequent `RT/LT+FD` chain is
just a geometric unrolling between the endpoints.

```csharp
using DotNetCampus.Inking;
using DotNetCampus.Inking.LogoExport;

// 1) Calculate the bounding box of all strokes and use its center as the Logo origin (0,0)
var (minX, minY, maxX, maxY) = InkToLogoConverter.GetBoundingBox(InkCanvas.Strokes);
double cx = (minX + maxX) / 2.0;
double cy = (minY + maxY) / 2.0;

// 2) Convert to Logo source
string logo = InkCanvas.ToLogoSource(
    flipY:         true,            // screen Y points down → Logo Y points up
    originShiftX:  cx,
    originShiftY:  cy,
    mode:          LogoExportMode.Optimized);

// 3) Play back with AOTLogoSharp 1.2.1+ (or PCLogo / MSWLogo)
File.WriteAllText("handwriting.logo", logo, System.Text.Encoding.UTF8);
```

You can also convert a single `SkiaStroke`, any `IReadOnlyList<SkiaStroke>`, or any
`IReadOnlyList<IReadOnlyList<InkStylusPoint>>` (point-list form, useful when the consumer
already has the raw stylus point list) — all four expose a `ToLogoSource(...)` extension
method with the same parameters.

#### Tuning the converter with `LogoExportOptions`

The hardcoded constants that the converter used to keep internally (min turn angle / min
step / smoothing α / simplification ε / curvature threshold …) are now exposed as a public
immutable options class. You can override any of them with C# 9 init-only syntax:

```csharp
string logo = InkToLogoConverter.Convert(
    strokes,
    new LogoExportOptions
    {
        SmoothAlpha                = 0.5,   // EMA smoothing (≤0 / ≥1 = off)
        CurvatureAngleThresholdDeg = 8.0,   // curvature: inflection if θ ≥ this (deg)
        CurvatureMinGapPx          = 4.0,   // curvature: min spacing on straight segments (px)
        MinAngleDeg                = 0.5,   // ignore turns below this angle (deg)
        MinStepPx                  = 0.5,   // ignore segments shorter than this many px
        MinMergedFdPx              = 0.5,   // ignore merged FD shorter than this many px
    });
```

| Option | Default | Effect |
|---|---|---|
| `SmoothAlpha` | 0.5 | EMA smoothing factor; lower = smoother but laggier, higher = closer to raw |
| `MinSmoothedStepSq` | 1e-6 | Drop stationary points (px²) |
| `CurvatureAngleThresholdDeg` | 8° | An interior point is kept if the angle between its neighbouring vectors is ≥ this |
| `CurvatureMinGapPx` | 4 px | Minimum gap between two kept points on a near-straight segment |
| `MinAngleDeg` | 0.5 | Drop jitter turns below this angle |
| `MinStepPx` | 0.5 | Drop jitter segments below this distance |
| `MinMergedFdPx` | 0.5 | Drop merged FD below this length |

#### Curvature-based simplification

The converter uses a **curvature-based** simplification (no RDP, no distance-to-chord
metric). For every interior point it computes the angle θ between the two neighbour
vectors (P[i-1]→P[i] and P[i]→P[i+1]) and decides:

- θ ≥ `CurvatureAngleThresholdDeg` → keep as an **inflection point**
- otherwise, distance to the last kept point ≥ `CurvatureMinGapPx` → keep as a
  **straight-segment uniform sample**
- otherwise drop

Two key properties of this approach for handwriting:

- The **start, end, and 2nd point** of every stroke are always kept. They fix the path
  endpoints and the initial `SETH` heading, so the simplified stroke is bit-equal to the
  `AbsoluteCoordinates` output at the endpoints.
- **Every stroke has exactly one `SETH`**, followed by a pure `RT/LT+FD` chain — no
  periodic re-anchor that could split a continuous curve into discontinuous chunks.

If you need pixel-perfect replay and don't care about output size, use
`LogoExportMode.AbsoluteCoordinates` instead — that mode emits pure `SETXY` and has no
rotation concept at all.

#### Point-list form of `Convert`

The point-list overload lets you convert a Logo stream **without constructing `SkiaStroke`
instances** — useful when you've deserialized points from a `.meta` file or are replaying
recorded points:

```csharp
IReadOnlyList<IReadOnlyList<InkStylusPoint>> pointLists = /* ... */;

string logo = InkToLogoConverter.Convert(
    pointLists,
    options:        new LogoExportOptions(),  // default: curvature + no extra SETH
    flipY:          true,
    originShiftX:   cx,
    originShiftY:   cy,
    mode:           LogoExportMode.Optimized);

var (minX, minY, maxX, maxY) = InkToLogoConverter.GetBoundingBox(pointLists);
```

`GetBoundingBox` is also overloaded for the point-list form, with identical semantics.

Logo dialect conventions used by the converter:

| Command | Meaning |
|---|---|
| `SETH θ` | Set absolute heading; `0°` = north, clockwise positive; direction = `(sin θ, cos θ)` |
| `LT a` | Turn left (counter-clockwise) by `a` degrees |
| `RT a` | Turn right (clockwise) by `a` degrees |
| `FD d` | Move forward `d` units |
| `SETXY x y` | Jump to absolute `(x, y)` |
| `PU` / `PD` | Pen up / pen down |
| `HOME` | Return to `(0, 0)` with heading `0` |
| `CS` | Clear screen |

# Contributing

[![PRs Welcome](https://img.shields.io/badge/PRs-welcome-brightgreen.svg?style=flat-square)](https://github.com/dotnet-campus/DotNetCampus.InkCanvas/pulls)

If you would like to contribute, feel free to create a [Pull Request](https://github.com/dotnet-campus/DotNetCampus.InkCanvas/pulls), or give us [Bug Report](https://github.com/dotnet-campus/DotNetCampus.InkCanvas/issues/new).

# License

[![](https://img.shields.io/badge/License-MIT-blue?style=flat-square)](./LICENSE)
