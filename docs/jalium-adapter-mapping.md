# Jalium 适配映射清单

## 输入映射（Jalium -> InkCore）

- `PointerEventArgs.Pointer.PointerId` -> `InkingModeInputArgs.Id`
- `PointerEventArgs.GetCurrentPoint(this).Position` -> `InkStylusPoint.Point`
- `PointerPointProperties.Pressure` -> `InkStylusPoint.Pressure`
- `PointerPointProperties.ContactRect.Width/Height` -> `InkStylusPoint.Width/Height`
- `PointerPoint.Timestamp` -> `InkingModeInputArgs.Timestamp`
- `PointerDeviceType.Mouse` -> `InkingModeInputArgs.IsMouse`
- `GetIntermediatePoints(this)` -> `InkingModeInputArgs.StylusPointList`

## 压力兼容策略

- 当 `PointerPointProperties.HasPressure == false` 时，复用上一个输入点压力，避免压力突变。
- 当压力值异常（`<= 0`）时，回退到上一个输入点压力或 `InkStylusPoint.DefaultPressure`。

## 几何渲染映射（InkCore -> Jalium）

- `WpfInk.InkStrokeRenderer` 输出 `IStreamGeometryContext` 指令
- `JaliumStreamGeometryContext` 将指令映射到 `PathGeometry`：
  - `BeginFigure` -> `PathFigure`
  - `PolyBezierTo` -> `PolyBezierSegment`
  - `PolyLineTo` -> `PolyLineSegment`
  - `ArcTo` -> `ArcSegment`
- `PathGeometry` 最终通过 `DrawingContext.DrawGeometry(...)` 渲染到 Jalium 画布

## 模式映射

- `InkCanvasEditingMode.Ink`：收集点并渲染几何
- `InkCanvasEditingMode.EraseByPoint`：使用 `PathGeometry.FillContains` 做命中擦除
- `InkCanvasEditingMode.None`：禁用输入处理

## 后端策略

- 在 Demo 启动时设置 `RenderBackend.D3D12`，作为 Windows 默认后端。
- 同时保留 Jalium 运行时内部降级能力（后端不可用时由 Jalium 自行处理）。
