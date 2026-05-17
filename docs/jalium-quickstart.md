# Jalium 平台运行与验收

## 目录

- 平台库：`src/DotNetCampus.JaliumInkCanvas`
- 示例工程：`demo/JaliumInkCanvasDemo`
- 映射说明：`docs/jalium-adapter-mapping.md`
- Avalonia 12 线：`src/DotNetCampus.AvaloniaInkCanvas`、`demo/AvaloniaInkCanvasDemo`

## 前置条件

Demo 与平台库通过 `PackageReference` 使用 `Jalium.UI.Desktop`，当前固定版本为 `26.10.3`。

仓库当前采用双线并行：

- Avalonia 线：基于 Avalonia 12。
- Jalium 线：内部承载 Jalium 原生 `InkCanvas`，由 Jalium 负责动态书写输入与渲染，DotNetCampus 适配层负责收集后压力平滑、点距过滤和公共 API 兼容。

首次构建前请确认：

- 能访问 NuGet 源，通常为 `https://api.nuget.org/v3/index.json`。
- 机器已安装 .NET 10 SDK。
- 输出目录没有残留旧版本 `jalium.native.*.dll`。必要时先清理 `artifacts` 后重新构建。

## 构建

```powershell
dotnet build demo/JaliumInkCanvasDemo/JaliumInkCanvasDemo.csproj -v minimal
```

## 运行

```powershell
dotnet run --project demo/JaliumInkCanvasDemo/JaliumInkCanvasDemo.csproj
```

直接运行 `artifacts\...\JaliumInkCanvasDemo.exe` 时，请确保同目录的 `jalium.native.*.dll` 来自同一套 NuGet 还原结果。

## 交互说明

- `I`：墨迹模式。
- `E`：按点擦除模式，当前使用 Jalium 原生擦除能力。
- `P`：路径擦除入口，本轮映射为 Jalium 原生最接近的擦除模式。
- `N`：禁用输入。
- `C`：清空笔迹。
- `T`：浅色/深色主题切换。

工具栏中可直接调节：

- 墨迹粗细：`Thickness`
- 颜色选择：`ColorPicker`
- 压力平滑：`Pressure Smoothing` / `Pressure Smooth`
- 忽略压感：`Ignore Pressure`
- 高采样开关：`High Sampling`，对应 Jalium intermediate points
- 最小点距：`Min Distance`
- 原生擦除相关参数：`Eraser Size` 等控件保留为兼容入口
- 添加示例笔迹：`Add Sample Stroke`
- 删除蓝色笔迹：`Remove Blue Strokes`
- 清空笔迹：`Clear`

## 当前 Jalium 实现

- 主渲染路径使用 Jalium 原生 `InkCanvas`、`DynamicRenderer`、`Ink.StrokeCollection`、`Ink.DrawingAttributes`、`StylusPointCollection`。
- 书写模式直接使用 Jalium 原生输入管线，确保按下后立即出现动态墨迹；DotNetCampus 适配层在 `StrokeCollected` 后执行压力平滑和最小点距过滤，并在需要时替换为标准化后的原生 stroke。
- Demo 指标文本改为事件驱动更新，不再通过 `CompositionTarget.Rendering` 每帧更新 UI，以避免静止窗口持续闪烁。

## 验收重点

- 鼠标、触摸、手写笔按下后立即出现墨迹。
- 快速书写时墨迹连续，不明显掉点。
- 压力变化平滑，关闭压力平滑后能看到行为差异。
- `High Sampling` 开关能影响采样密度。
- `Add Sample Stroke`、`Save Stroke As Svg`、`Remove Blue Strokes`、`Clear` 正常。
- 静止窗口不再持续闪烁。
