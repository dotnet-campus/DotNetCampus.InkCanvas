# Jalium 平台运行与验收

## 目录

- 平台库：`src/DotNetCampus.JaliumInkCanvas`
- 示例工程：`demo/JaliumInkCanvasDemo`
- 映射说明：`docs/jalium-adapter-mapping.md`

## 构建

```powershell
dotnet build demo/JaliumInkCanvasDemo/JaliumInkCanvasDemo.csproj -v minimal
```

## 运行

```powershell
dotnet run --project demo/JaliumInkCanvasDemo/JaliumInkCanvasDemo.csproj
```

## 交互说明

- `I`：墨迹模式
- `E`：按点擦除模式
- `N`：禁用输入
- `C`：清空笔迹
- `T`：浅色/深色配色切换
- `1`：后端策略 `NativePreferred`
- `2`：后端策略 `ForceNative`
- `3`：后端策略 `ForceSkiaFallback`

## DX12 后端验证

- Demo 启动时在 `Program.cs` 显式配置：
  - `RenderContext.GetOrCreateCurrent(RenderBackend.D3D12)`
  - `renderContext.DefaultRenderingEngine = RenderingEngine.Impeller`
- 在 Windows 下该配置即为默认 DX12 后端入口。
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
