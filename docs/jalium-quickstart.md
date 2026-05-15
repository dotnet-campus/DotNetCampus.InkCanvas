# Jalium 平台运行与验收

## 目录

- 平台库：`src/DotNetCampus.JaliumInkCanvas`
- 示例工程：`demo/JaliumInkCanvasDemo`
- 映射说明：`docs/jalium-adapter-mapping.md`
- Avalonia 12 线：`src/DotNetCampus.AvaloniaInkCanvas`、`demo/AvaloniaInkCanvasDemo`

## 前置条件（必读）

Demo 与平台库已迁移为 NuGet 依赖，默认通过 `PackageReference` 使用 `Jalium.UI.Desktop`（当前固定版本 `26.10.2`）。

仓库当前采用双线并行：

- Avalonia 线：基于 Avalonia 12
- Jalium 线：默认原生渲染优先（DX12 可用时不走 Skia），不可用时回退到 Skia fallback

首次构建前请确认：

- 能访问 NuGet 源（通常为 `https://api.nuget.org/v3/index.json`）
- 机器安装了 .NET 10 SDK
- 没有被本地旧版 `jalium.native.*.dll` 污染输出目录

## 构建

```powershell
dotnet build demo/JaliumInkCanvasDemo/JaliumInkCanvasDemo.csproj -v minimal
```

## 运行

```powershell
dotnet run --project demo/JaliumInkCanvasDemo/JaliumInkCanvasDemo.csproj
```

直接运行 `artifacts\...\JaliumInkCanvasDemo.exe` 时，请确保同目录的 `jalium.native.*.dll` 来自同一套 NuGet 还原结果（必要时先清理 `artifacts` 再重新 build）。

## 故障排除

- **`EntryPointNotFoundException`：在 `jalium.native.core` 中找不到 `jalium_render_target_get_engine`（或其它 `jalium_*` 入口）**  
  表示已加载的 `jalium.native.core.dll` 与托管层版本不一致。处理：清理 `artifacts`、`bin`、`obj` 后重新 `dotnet restore` + `dotnet build`；若仍异常，检查是否有外部路径（如 PATH 或应用目录残留）注入了旧版 `jalium.native.*.dll`。

## 交互说明

- `I`：墨迹模式
- `E`：按点擦除模式
- `P`：路径橡皮模式（按 `EraserSize` 连续擦除）
- `N`：禁用输入
- `C`：清空笔迹
- `T`：浅色/深色配色切换
- `1`：后端策略 `NativePreferred`
- `2`：后端策略 `ForceNative`
- `3`：后端策略 `ForceSkiaFallback`

工具栏中可直接调节：
- 墨迹粗细（Thickness）
- 颜色选择（ColorPicker）
- 压力平滑开关与平滑系数（Pressure Smoothing / Pressure Smooth）
- 忽略压感（Ignore Pressure）
- 高采样开关（High Sampling，对应中间点采样）
- 最小点距（Min Distance）
- 橡皮基础尺寸（Eraser Size）
- 橡皮最小/最大尺寸（Min Eraser / Max Eraser）
- 触摸尺寸驱动橡皮（Stylus Eraser Size）
- 持续跟随触摸尺寸（Eraser Follow Always）
- 最小橡皮锁定（Lock Min Eraser）
- 提交层缓存开关（Layer Cache）
- 性能计数开关（Perf Counters）
- 橡皮覆盖层开关（Eraser Overlay）
- 切换默认/自定义橡皮覆盖层（Default Overlay / Custom Overlay）
- 添加示例笔迹（Add Sample Stroke，验证通过公共 API 注入笔迹）
- 删除蓝色笔迹（Remove Blue Strokes，验证按条件删除笔迹）

## DX12 后端验证

- Demo 在 `Program.cs` 中通过环境变量优先使用 D3D12：`JALIUM_RENDER_BACKEND=d3d12`，再以 `AppBuilder` 启动应用。
- 在 Windows 下配合已编译的 `jalium.native.d3d12` 与 `jalium.native.core` 即为 DX12 渲染路径。
- 控件内置后端策略选择器，支持：
  - `NativePreferred`（默认）
  - `ForceNative`
  - `ForceSkiaFallback`
  - `AutoFallback`

## 当前性能基线（高性能版）

- 已验证可构建通过（Debug，无错误）。
- 当前版本已包含：
  - 提交层缓存（按颜色聚合 `GeometryGroup`，减少重复绘制开销）
  - 动态层增量渲染（move 阶段追加点，不整条重建）
  - 压力曲线平滑（可配置低通）
  - 性能计数器（FPS、帧时、输入吞吐、缓存命中）
