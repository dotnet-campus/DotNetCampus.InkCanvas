# DotNetCampus.InkCanvas

书写笔迹画板

这个项目起源于： https://github.com/AvaloniaUI/Avalonia/issues/1477

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
 settings.InkStrokeRenderer = new pfForSkiaInkStrokeRenderer();
 // 使用默认简单的笔迹渲染器
 settings.InkStrokeRenderer = null;
```

#### 控制橡皮擦属性

通过 AvaloniaSkiaInkCanvasSettings 进行控制，例如：

```csharp
        AvaloniaSkiaInkCanvasSettings settings = InkCanvas.SkiaInkCanvas.Settings;
        settings.EraserSize = new Size(100, 200);
```
