# InkBeautifier 精确性能优化重做记录（最终收尾）

## 完成状态与范围

生产优化已验证，本文已完成收尾，不继续优化。未执行 shell 或 git 操作，未改变渲染、采样、阈值、NonZero、兼容 Outline 修复、TFM 或依赖。本文将必要数据固化在源码文档中，撤回代码及丢失 artifacts 后仍可按下述步骤重做；旧 artifacts 不是实现的前提。

上一轮 Release 验证：Golden+Cache 15/15、功能/渲染 61/61、性能 10/10、Stage 9/9、最后汇总 11/11。这些集合有重叠，**不能相加**。9 资产每次输出的状态和全部 double 位模式 SHA256 均与原始二进制黄金一致。

本次收尾仅修改 StageAnalysis 新增比较断言 `Assert.IsTrue(report.Length > 500)` 为 `Assert.IsGreaterThan(500, report.Length)`，针对 MSTEST0037，不改无关现存断言。修改后 Release 构建成功（0 警告、0 错误）；Release DLL 的 Golden/StageDiagnostics/GoldenSummary 合并筛选 19/19 通过；StageAnalysis 单独通过。一次按猜测方法名筛选未匹配测试，不计为验证。没有重新跑全部性能预算，也没有由此宣称新增性能结果。

## 最终 before/after 固化表

来源：收尾开始时读取仓库根 `artifacts/InkBeautifierGolden/Comparison.md`，而非 PerformanceInvestigation 的 off-before。以下为上一轮完整最终汇总的固定快照，避免后续测试按时间戳生成的新测量改变交付数字。总时间为全部 Process 调用之和，ms；分配是当前线程字节，不是峰值/存活内存。比较包含 Down、过滤 Move、接受 Move 和 Up，非仅接受点。

|资产|calls|before ms|after ms|before bytes|after bytes|
|---|---:|---:|---:|---:|---:|
|Ink0.txt|2537|1026.351|202.809|331723528|38759136|
|Ink1.txt|2254|381.472|50.177|263527200|33930704|
|Ink2.txt|92|10.208|15.601|14558304|13214256|
|Ink3.txt|1414|19098.687|1323.840|3254955048|184317760|
|InkProblem-1.txt|52|21.686|21.924|24909448|18882160|
|InkProblem-2.txt|70|51.527|48.076|48174456|44350360|
|InkProblem-20261106-093537.txt|279|3732.566|563.036|874167816|279231072|
|InkProblem-3.txt|52|43.924|22.863|24827400|26529920|
|InkProblem-4.txt|154|1121.096|457.874|317415008|363986512|

### Ink3 Move / Up 明细

按回放 Status 归类，包括未接受调用。p95/p99 为排序后下标 `ceil(n*p)-1` 的 nearest-rank，单位 ms；Up 只有一次，不能作尾延迟分布推论。还有一次 Down，因此 Move+Up 不等于全部调用。

|状态|calls|before 总ms|after 总ms|before p95|after p95|before p99|after p99|before max|after max|before bytes|after bytes|
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
|Move|1412|18952.177200|1315.909500|70.343100|5.075300|92.277900|6.282100|278.396200|7.357000|3242021696|183786504|
|Up|1|146.506800|7.925100|146.506800|7.925100|146.506800|7.925100|146.506800|7.925100|12932968|530632|

### 真实剩余问题（不在本次继续修复）

- InkProblem-3 分配 24,827,400 → 26,529,920（增加 1,702,520 字节）；InkProblem-4 317,415,008 → 363,986,512（增加 46,571,504 字节）。不能声称所有资产分配都减少。当前覆盖修复仍会建立/重建 Y 桶，缓存也有自身分配；未单独隔离归因，不能把全部增量武断归于一种结构。
- Ink2 本次固定表总时间增加，InkProblem-1 略增；不抹去这些不利数据，也不把单轮时差解释为统计显著回归。
- 前轮记录出现 Ink1 单次约 431ms 离群；当前 `InkBeautifier-performance-Ink1.txt` 已是另一轮报告（首轮 Move max 2.831ms），不能把后轮覆盖报告当成离群从未发生，也不能虚构该离群的输入索引/GC 原因。缺少保留的对应逐调用记录，具体原因未确定。
- 仅同步库 Process，不包含输入调度、UI、Skia、呈现和帧时间。共享环境、JIT、GC、调度等未受控；这不是 SLA 或所有机器所有调用 <33ms 的承诺。预算测试只覆盖 Ink3 笔中三分之一的 Move，不能推广到其他状态或所有资产。

## 逐调用跨二进制证据，而非 self 模式哈希

定位 `Code/InkBeautifierAvaloniaTests/InkBeautifierGoldenTests.cs`：`WhenRecordedAssetIsReplayedThenEveryCallMatchesOriginalBinary` 在循环的**每一个** i 执行 Process，生成 `i,StrokeId,WasAccepted,IsCompleted,Outline.Count,Outlines.Count,SHA256`，读取旧 `.golden.csv` 的 expected[i] 逐行比较，最后检查调用数量。没有过滤 accepted/status，没有只比最终 Up。时序表另按 input 列逐项对齐。

哈希序列来自 StageDiagnosticsTests 的 `AppendHash`：按顺序写入 StrokeId、接受/完成标志（long 0/1）、Outline 数量及逐顶点 X/Y 原始 double 8 字节、Outlines 数量、每个 contour 数量和其逐点 X/Y。不能排序、舍入、转字符串坐标或仅对主 Outline 哈希。当前使用 BitConverter 的本机字节序，复现实验保持相同端序（已验证环境 x64 little-endian）；不是跨端序通用文件格式。SHA256 是高强度逐位指纹证据，不是数学上排除碰撞的几何证明。

Stage 的 off-before/coarse/fine/off-after 是同一二进制自比较，用于探针透明性，**不能替代**原始库与优化库的 Golden。PerformanceInvestigation.md 的 off-before 也不能替代跨二进制 before。Golden 测试本次又全部匹配已有黄金；这不是重新生成当前实现的自基线。

## 无旧 artifacts、无临时探针的独立重做步骤

所有路径以下以 `src/Lab/InkBeautifier/` 为前缀，artifacts 则在仓库根。不要先优化再创建黄金；不要把当前优化库首次 recording 当作原始基线。

1. 从用户恢复的原始干净代码开始，保留同一批 9 个 TestAssets 原始文件和原 options。读取根 global.json、Directory.Build.props、测试 csproj：当前实验 net10.0，MSTest 4.0.1，Avalonia 12.1.0；不升级。先构建原始库。
2. **只加测试基线工具**：可重建一个 public instance `[TestClass][DoNotParallelize]`，不必重建 Stage 插桩。实现下一段的资产解析器、逐调用序列化和哈希 helper（private），Golden 数据驱动测试及汇总；无需生产 `InkPerformanceDiagnostics`、Session/Snapshot 或任何内部可见性。当前 Golden 文件复用 partial PerformanceTests 的 Load、DiagnosticAssets、AppendHash、FindRepository；若这些诊断文件已撤回，把这四个 helper 的必要逻辑移入基线测试本身即可，不能因为缺少 Stage 文件停止。
3. 使用全新独立工作副本/输出目录保存临时原始黄金：根 artifacts/InkBeautifierGolden 初始不存在或为空，不删除用户已有目录。当前测试 FindRepository 向上找 Directory.Build.props，默认指向该根 artifacts；独立副本能隔离旧数据。首次原始 Release 运行 Golden 时以 FileMode.CreateNew 写 9 个 `.golden.csv`，并保存 `.before.csv`。固定这批资产、options、输入顺序与原始文件。以后 `.golden.csv` 不覆盖；不能遇到比较失败就删黄金重新录制。
4. 计量只包围 Process：进入前取当前线程分配字节和 Stopwatch timestamp，Process 返回立即取差；哈希、XML、文件写入、状态汇总都在计时外。输出 CSV 表头 `input,ms,bytes`，input 从 0 开始，时间 `ticks*1000d/Stopwatch.Frequency`。Golden 不预热；另行性能实验预热一次并测三轮，不混淆两种数据。
5. 按下面生产依赖顺序逐项实施：StrokeState 字段及完整样本前缀校验 → SegmentOverlaps → 正/反只读凸包和新外层快照 → CoverageIndex 与原绕数查询候选接入 → PointOverlaps 及两个消费点。每项后运行相同 9 资产 Golden；不改变原算法公式、循环顺序、短笔分支或数值阈值。失败则修实现，不能更新原始黄金。
6. 保留 Cache 6 场景、9 资产实际 Skia 渲染和 Golden 汇总测试；Release 验证后记录新的 before/after。所有后续 `.after-UTC.csv` 必须与原始 input 列和调用总数一致，不能混用不同资产集合或自己与自己比较。独立重做所得时耗不必复现固定表数值，状态和 double 指纹必须一致。

### 资产解析与基线 helper 的独立规格

- DiagnosticAssets：从测试输出 `TestAssets/*.txt` 按文件名 Ordinal 顺序自动发现，当前九个文件即上表；csproj 沿用资产复制规则。
- Load：XDocument 根的每个 ArrayOfInkDataModel 为一笔，取非空 InkDataModel 数组；X/Y/PressureFactor 用 invariant double；Time 用 XmlConvert.ToTimeSpan，再转 `(long)TotalMilliseconds`。strokeId=笔索引+1。每笔 timestamp=`max(0,t-first)`；pressure=`Clamp(raw/1024,0,1)`；有 Status 就原样解析，否则首点 Down、其余 Move。最后点无 Status 才额外复制最后点为 Up，timestamp=`max(1,last-first+1)`；不得无条件再补 Up。
- 所有原始点范围 dx/dy 计算 `s=min(940/max(1,dx),700/max(1,dy))`；options 为 `MaximumWidth=18/s, VelocityResponse=1.8*s`，其他默认。Process 输入仍是原坐标，**不要**先缩放坐标再传入。轮廓的渲染变换单独进行。
- FindRepository：从 AppContext.BaseDirectory 向上找 Directory.Build.props；找不到明确失败。AppendHash 按上一节规格重建，不依赖内部实现；每次调用新建 SHA256 增量对象，只哈希该次返回，非累积到整资产才比。
- 汇总：每资产 `.before.csv` 与指定同一轮 `.after-*.csv` 全行 input 相等；total ms/bytes 分别 Sum；状态分组依赖同一个 Load 的 inputs[i].Status，分位数先排序再 nearest-rank。不计入序列化/报告耗时。当前 GoldenSummary 选择字典序最后时间戳，且可能先于同批 Golden 执行，因此**必须先单独跑 Golden，再单独跑 GoldenSummary**，交付报告应记录选择的轮次，而非靠混合测试顺序。

## 生产改动定位与精确重做规则

### 生命周期和样本前缀

`Processing/InkBeautifier.cs:Process/Reset`：Down 新建并替换同 ID StrokeState；未知 ID 返回原有空结果；只有 accepted 才 BuildGeometry；未接受继续复用旧 Outline/Outlines；Up 无论接受与否都会移除状态；Reset 清空字典。缓存仅归属 StrokeState，不能跨笔或全局保留。

`Processing/InkBeautifier.State.cs:StrokeState` 加 `CachedSamples:List<RawPoint>`、`SegmentOverlaps/PointOverlaps:List<bool>`、`SweepCells/ReverseSweepCells:List<IReadOnlyList<InkPoint2D>>`、`SweepCellSigns:List<int>`。RawPoint 完整字段为 Position、Timestamp、RawSpeed、SmoothSpeed、RawHalfWidth、HalfWidth。

`Outlines/InkBeautifier.OutlineBuilder.cs:BuildGeometry`（约283行）短笔分支之后选 `samples = Samples.Count>1 ? Samples : RawPoints`，先 PrepareSampleCache，再模板、正常 fallback、Repair，最后 SweepCells。TryAddPoint 会改 RawPoints 头部及宽度，不能把 RawPoints 视为天然不可变；AppendBezierSamples 当前仅追加 Samples，仍需防未来行为变化。

`PrepareSampleCache`（约326行）：若旧长度>新长度，或任意旧索引 RawPoint 的 record equality 不等，则 CachedSamples、SegmentOverlaps、PointOverlaps、正反 SweepCells 和 SweepCellSigns **全部清空**。每次都校验整个已有前缀（不是只看 Count 或最后点）；对新增 i 复制 RawPoint，追加 `i>=2 && OverlapsEarlierSweepAtPoint(samples,i)`。当前 equality 是 RawPoint 值相等，不是额外逐位比较；黄金才做原始位指纹。不得无验证将其改为仅长度校验。

### 段和点历史判定

模板循环（约365行）对段 index，只有 `index==SegmentOverlaps.Count` 时追加 `index>0 && OverlapsEarlierSweep(samples,index)`，消费仍在原分支位置。段依赖 [0..index+1]，点依赖 [0..i]；未来追加不会改变这些查询。已有布尔不重新扫描历史，但模板/边界本身仍每次按原顺序生成，不缓存依赖未来的裁剪或原始点宽度判定。

保留原 OverlapsEarlierSweep/AtPoint 全部运算：点 i 扫 earlier=0..i-2，先检查距离平方 <= `(current.HalfWidth+earlier.HalfWidth)^2`；历史段只在 j<i-2 时检查，半径=`current.HalfWidth+max(h[j],h[j+1])`，先原包围盒距离筛选再原点到段距离。段版本对当前段 i→i+1 取 H=max(h[i],h[i+1])，历史点 j=0..i-2 的阈值为 (H+h[j])²，保留先当前点距离、再包围盒及历史点到当前段距离的短路次序；j<i-2 才检查历史段 j→j+1，Hj=max(h[j],h[j+1])，当前点到历史段阈值 (h[i]+Hj)²，段间阈值 (H+Hj)²，继续调用原函数保留相交/距离和包围盒逻辑。不把原函数替换成近似空间查询。

PointOverlaps 消费两个位置：模板末端（约428行）用 `PointOverlaps[samples.Count-1]`；Repair 最终 samples 扫描（约1527行）用 `PointOverlaps[sampleIndex]`。Repair 的 rawPoints 重叠查询仍调用原函数，不能套用 samples 的索引/缓存。completesStroke && rawPoints.Count>=10 才执行最终样本检查，首次缺失补 cap 后 break 不变。

### SweepCells、方向和旧结果快照

`BuildSweepCells`（约296行）每个段 i，原 AddPenCap 两端 → 原 BuildConvexHull，首次构造转数组再 Array.AsReadOnly 包装；另建立反向独立数组并只读包装；符号=`Math.Sign(GetSignedArea(originalCell))`。不暴露可直接转型修改的缓存数组，不对共享 cell 原地 Reverse。

每次调用仍重新算修复后兼容 Outline 的面积：`A=0.5*sum(x[i]*y[i+1]-x[i+1]*y[i])`，闭合到首点，原顺序求和不变。每段取 `cellSign==Math.Sign(A) ? original : reversed`，包括 sign=0 的原语义；不能把方向永久固定为第一次结果。每次创建新外层 List，内层只读共享且以后不修改；append、其他笔、Reset、同 ID Down、拒绝 Up 都不得反写旧结果。只构造新增段凸包；不是删除完整轮廓修复，不用 SweepCells 面积替代兼容 Outline 面积契约。

### CoverageIndex、索引更新和 fallback

`Outlines/InkBeautifier.CoverageIndex.cs` 为 private IReadOnlyList 包装原 polygon；`RepairRawPointCoverage`（约1487行）开始创建一个，补任何 raw cap 后重新 new 包装，不能沿用旧闭合边或旧桶；最终样本扫描复用当前包装。Count 变化会触发惰性 Rebuild；当前依赖轮廓仅 append 的内部约定，同 Count 原地改点不会被索引检测，未来若改该约定须显式重建。

128 桶，padding=0.00001；min/max 为 polygon Y 范围，minimumY=min-padding，step=(max-min+2*padding)/128；`Bucket(y)=Clamp((int)((y-minimumY)/step),0,127)`。每条原始边 i→(i+1)%Count，将扩张后的端点 minY-padding..maxY+padding 覆盖桶逐个插入边号 i，按原始 i 递增插入，候选顺序不变，包含最后→首边。

Count<32、任意顶点坐标非有限或绝对值>1,000,000 则不建桶；查询 Y 非有限返回 null，绕数函数恢复完整原边扫描；有效索引范围外返回空候选。padding 大于原 `distanceSquared<=1e-12` 对应 1e-6 容差，有限受限坐标路径保留额外余量。不是降低精度或改变容差。

`IsFilledByNonZeroWinding`（约1630行）：候选 null 则扫全部边，非 null 按边号扫候选；每条边仍先原 GetDistanceToSegmentSquared<=1e-12 则 true；上跨 `start.Y<=p.Y && end.Y>p.Y && Cross>0` 加1，下跨 `start.Y>p.Y && end.Y<=p.Y && Cross<0` 减1，最后 winding!=0。不排序重组边、不改 <=/> 边界或 Cross 计算表达式。

## 保留与丢弃的文件范围

- 必须重做的生产内容：State 缓存字段、OutlineBuilder 上述接入、CoverageIndex private partial。Process 生命周期原逻辑不改。
- 必须保留/重建的验证：Golden（含独立 Load/AppendHash 等 helper）、Cache 6 场景、9 资产渲染测试。Cache 四个旧快照场景 append/reset/other-stroke/rejected-up，另有完成清理和 Reset 后同 ID 与 fresh processor 指纹一致两个场景。
- 不需要重做的临时诊断：InkPerformanceDiagnostics 的阶段/计数插桩、StageDiagnostics/StageAnalysis 报告器、临时调查探针和 artifacts。可以整套不移植；不要留下引用已丢弃类型的 using/scope/count 调用。当前工作区为延续历史验证保留了既有诊断文件，本次没有扩大范围删除。生产优化本身不依赖这些探针的结果或文件。

## 9 张实际 PNG 审查、Ink2 证据和局限

审查路径为根 `artifacts/bin/InkBeautifierAvaloniaTests/release/<名称>-Beautified.png`。已将以下九个明确文件按表顺序传入图像分析逐张查看；分析返回按输入顺序编号，子分析器界面未显示文件名，因此文件名映射来自本次传入的路径顺序，不声称子分析器独立识别文件名。Ink2 另直接按路径加载核对。没有生成或修改渲染。

|文件|实际可见审查|
|---|---|
|Ink0-Beautified.png|上方永字形，独立点画、左下小墨块；未见主体粗画白孔或意外断口；均未触边，大片下方留白。|
|Ink1-Beautified.png|上方永字形和独立点画/左下墨块；局部收笔凸起、线宽变化，未见明显长游离刺、粗画白孔或触边。|
|Ink2-Beautified.png|多条长弧、交叉及锐角回折；左边界有入线、右下底边有出线；左下回弯近底边，分析可见末端窄白带，无法将该回弯断言为硬裁切。开放端点及锐角不能直接当作缺陷。|
|Ink3-Beautified.png|连续波浪长线及多次 U 形回转，中间窄折返、右端渐尖；未见明显断口、内部白孔或触边。最下约 y513，其下约246px留白，不能说是丢失轨迹。|
|InkProblem-1-Beautified.png|低位长弧向右上、右上大环及交叉；未见交叉白缝、粗画内孔或边界裁切；环内空白不是墨层漏洞。|
|InkProblem-2-Beautified.png|外侧大弧、内环和粗斜线；中央偏右明显锐角、回折加粗，未见意外断口或触边。|
|InkProblem-3-Beautified.png|粗大回弧、左侧小环和细长尾；左下粗端虽近边仍有白边，未见明显内部白孔或断口。|
|InkProblem-4-Beautified.png|上方细回环、中部拱线和下方粗回绕；短突出端、底段局部增厚、右上较尖，未见明显交叉白缝或触边，不能仅以凸出认定缺陷。|
|InkProblem-20261106-093537-Beautified.png|多大环相交，外侧/底部较粗、中环较细、底端圆钝；转折局部加粗，未见主要笔段断裂或粗墨内孔，各边有余量。|

### Ink2：不是凭视觉判回归

原始 Golden 的 Ink2 有92次逐调用结果；本次 Golden 再次逐调用匹配原始黄金，覆盖状态、主 Outline 和全部 SweepCells 的 double 位模式与顺序。性能 Load 的 options/坐标/时间/pressure/status/synthetic Up 与 `InkCanvasRenderingTests.DrawDeviceStrokes` 一致：1000×760、margin30，对所有原始点边界计算 `s=min(940/max(1,dx),700/max(1,dy))`，输入原坐标，width18/s、response1.8*s。实际 `OutlineTransform` 为 `x'=s*x+30-minX*s, y'=s*y+30-minY*s`；`InkCanvas` 构造设置 `ClipToBounds=true`。这证明在相同回放和变换下当前几何没有因优化改变；不是拿 Stage self 模式哈希来替代旧黄金。

原始输入中心范围 fit 有30px余量，但**所有输出几何**未必处于该边界：原算法的末端外推、轮廓构造和修复会延伸，当前图存在触边。没有旧 PNG 的像素差分，也没有完整输出边界/原始末端对应的逐点定位证据，因此具体触边是输入记录端点、原有外推还是画布裁剪所致的组合仍未确定。结论限于：有真实边缘截断外观；在黄金覆盖的回放下无优化几何变化证据，不能宣称优化回归，也不将“左下裁切”武断解释为已定位的原始输入缺陷。保留渲染不改。

视觉为当前 PNG 的显示尺度审查，不是像素矩阵/alpha/连通域扫描；不能排除单像素孔洞/细线断点。位置仅近似估读。自动覆盖断言只检查规定点邻域，不是整图一致性；逐调用黄金提供的是库输出一致性，不证明 Skia/显示/DPI 在任意环境像素一致。

## 精确复跑命令（仅记录，本文收尾未执行 shell）

在仓库根使用 .NET 10 SDK；P 表示 `src/Lab/InkBeautifier/Code/InkBeautifierAvaloniaTests/InkBeautifierAvaloniaTests.csproj`，下面为可直接使用的完整命令。独立重做时先原始代码执行 Golden 创建临时基线，再优化后执行相同 Golden 比较。当前工具验证使用 Release DLL 避免默认 Debug。

1. `dotnet build src/Lab/InkBeautifier/Code/InkBeautifierAvaloniaTests/InkBeautifierAvaloniaTests.csproj -c Release`
2. `dotnet test src/Lab/InkBeautifier/Code/InkBeautifierAvaloniaTests/InkBeautifierAvaloniaTests.csproj -c Release --no-build --filter "TestCategory=Golden"`
3. `dotnet test src/Lab/InkBeautifier/Code/InkBeautifierAvaloniaTests/InkBeautifierAvaloniaTests.csproj -c Release --no-build --filter "FullyQualifiedName~WhenCachedStrokeChangesThenEarlierResultIsUnchanged|FullyQualifiedName~WhenStrokeIsClearedThenReusedIdMatchesFreshProcessor"`
4. `dotnet test src/Lab/InkBeautifier/Code/InkBeautifierAvaloniaTests/InkBeautifierAvaloniaTests.csproj -c Release --no-build --filter "FullyQualifiedName~InkBeautifierAvaloniaTests.InkBeautifierTests|FullyQualifiedName~InkCanvasRenderingTests"`
5. `dotnet test src/Lab/InkBeautifier/Code/InkBeautifierAvaloniaTests/InkBeautifierAvaloniaTests.csproj -c Release --no-build --filter "TestCategory=PerformanceDiagnostics|TestCategory=PerformanceBudget"`
6. `dotnet test src/Lab/InkBeautifier/Code/InkBeautifierAvaloniaTests/InkBeautifierAvaloniaTests.csproj -c Release --no-build --filter "TestCategory=GoldenSummary"`
7. 仅保留诊断实验时：`dotnet test src/Lab/InkBeautifier/Code/InkBeautifierAvaloniaTests/InkBeautifierAvaloniaTests.csproj -c Release --no-build --filter "TestCategory=StageDiagnostics"`
8. 待7完成再汇总：`dotnet test src/Lab/InkBeautifier/Code/InkBeautifierAvaloniaTests/InkBeautifierAvaloniaTests.csproj -c Release --no-build --filter "TestCategory=StageAnalysis"`

未恢复 Stage 时跳过7/8，不影响 Golden 独立验证。不要把这些筛选的计数简单相加；截图 UseSkia、UseHeadlessDrawing=false 不变，输出只到 artifacts/bin。无实现阻塞；保留的证据局限是 Ink1 431ms 原始调用记录未定位、Ink2 精确触边成因未确定，均如实记录且不扩大修改范围。

## 干净重实现验证记录

本节为撤回后的新验证，前文数字仅为历史记录，不代表本次执行结果。

1. 已分段全文读取本文、撤回后 State/Process、轮廓接入点、测试解析器及配置。net10.0/MSTest 4.0.1 不变；旧 artifacts 的九份 golden 仍存在，尚待逐输入核对。
2. 新增 InkBeautifierReplayTests.cs，复用 PerformanceTests 的原解析器（类改为 partial），不引入生产诊断。临时 CrossBinaryDiagnostics 将独占新建 artifacts/InkBeautifierCleanReplay 黄金，并先核对旧黄金；本次实验结束须恢复显式 opt-in，避免外部 artifacts 成为 CI 前提。生产代码此时尚未修改。
3. 撤回版 Release 构建 0 警告/0 错误，CrossBinaryDiagnostics 9/9（45s），全部旧 golden 逐调用匹配，独立九份原始黄金和 before 已生成；不是优化二进制 self 比较。
4. 修改生产 State.cs、OutlineBuilder.cs，新增 CoverageIndex.cs：完整 RawPoint 前缀验证及全部缓存失效、段/点历史缓存、正反只读单元及新外层快照、128 桶精确边候选及异常 fallback。Process/Reset 生命周期未改。偏差：为减少构建批次，此次将生产接入一次完成，下面统一跨二进制验证，未逐项分别运行。
5. 优化版 Release 构建成功；独立原始黄金跨二进制逐输入校验 9/9（10s），主 Outline/所有单元及状态全部匹配。新增 CacheTests.cs：旧快照 append/reset/其他笔/同 ID Down/过滤 Up，清理后复用 ID、过滤 Up 状态及共享单元不可写回归。性能自动发现所有 txt，保留33ms预算但以环境变量显式启用；补齐日期资产渲染数据行。新增独立 CrossBinarySummary，将结果写入 artifacts。
6. 接续核对确认上述代码及 artifacts 已实际落盘，没有因前任务 Function failed 重写生产实现。本次检查 State、OutlineBuilder 缓存接入及 CoverageIndex：完整 RawPoint 前缀验证、段/点布尔缓存、正反只读单元、补 cap 后重建 Y 索引及最终样本缓存均存在。生产目录全文搜索 InkPerformanceDiagnostics/Stage 无匹配；不带临时阶段插桩。
7. 修正 ReplayTests 的黄金保护：移除 recording 分支，不再创建/覆盖任何 golden 或 before；每次直接读取仓库根 artifacts/InkBeautifierGolden/*.golden.csv，比较所有调用状态、数量及主 Outline/全部 contours 的原始 double 字节 SHA256 指纹。缺少原始 golden 时 Assert.Inconclusive，汇总缺少原始 before 或 after 时同样跳过，故外部 artifacts 不成为默认 CI 成功必需条件。存在黄金时自动执行，不要求环境变量。输出只新增 CleanReplay/*.after-时间戳.csv 及 Comparison.md，原黄金保持只读。
8. 本次 Release 构建 InkBeautifier.slnx：退出码0，0警告/0错误，1.57s。随后直接测试 Release DLL（避免默认 Debug）：排除 CrossBinarySummary 的集合通过83、跳过1、失败0、总84（30s）；之后 Summary 独立通过1（76ms）。两集合互斥，本轮共通过84、跳过1、失败0、总85。包括全部9资产跨原始黄金比较、9资产性能报告、缓存公共API回归及现有功能/渲染；跳过项仅为显式机器敏感 PerformanceBudget。未启用预算环境变量，不声称预算测试通过。测试使用真实 UseSkia / UseHeadlessDrawing=false。

### 本轮原始 before 与优化后逐调用 Stopwatch 汇总

来源 artifacts/InkBeautifierCleanReplay/Comparison.md；after 来源时间戳为20261106-033146至033201，本轮测试输出。before 是第3条保留的撤回原版测量，不重新录制。Process-only、未预热跨二进制单轮，线程分配字节；不是受控统计基准，也不包含 Skia/UI。以下不替换前文历史固定表。

|资产|原版总ms|优化总ms|原版bytes|优化bytes|
|---|---:|---:|---:|---:|
|Ink0.txt|757.491|108.139|331747056|38770960|
|Ink1.txt|484.959|88.138|263544560|33953552|
|Ink2.txt|17.879|16.455|14558552|13217136|
|Ink3.txt|25526.355|1275.310|3255028512|184342688|
|InkProblem-1.txt|47.153|18.860|24909944|18885848|
|InkProblem-2.txt|77.157|46.474|48174704|44357672|
|InkProblem-20261106-093537.txt|3658.244|559.227|874178480|279273080|
|InkProblem-3.txt|40.316|22.908|24827648|26534168|
|InkProblem-4.txt|947.303|497.532|317418480|364024464|

Ink3逐调用分组（Move包含过滤调用，nearest-rank）：

|状态|原总ms|优化总ms|原p95|优化p95|原p99|优化p99|原max|优化max|原bytes|优化bytes|
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
|Move|25278.651|1267.761|97.573|4.754|129.510|5.986|490.806|8.875|3242095160|183811176|
|Up|247.699|7.547|247.699|7.547|247.699|7.547|247.699|7.547|12932968|530888|

另有完整预热1轮、测量3轮的9资产独立报告，目录 artifacts/bin/InkBeautifierAvaloniaTests/release/InkBeautifier-performance-<资产名>。Ink3报告确认 Release/net10.0.14/x64、Stopwatch frequency=10000000，1414 calls/round（1412 Move、1 Down、1 Up），三轮数据：

|轮次|总ms|Move总ms|Move p50|Move p95|Move p99|Move max|Up ms|线程总分配bytes|
|---|---:|---:|---:|---:|---:|---:|---:|---:|
|1|696.634|692.169|0.000|2.664|3.316|4.503|4.460|184284712|
|2|1238.923|1230.883|0.000|4.807|5.870|6.496|8.037|184310152|
|3|1243.951|1236.358|0.000|4.875|5.842|7.220|7.589|184292648|

p50为三位小数显示精度，非证明零耗时。共享环境/JIT/GC/调度未控制，首轮明显更快如实保留，不能择优声称稳定696ms。每轮全部 Move 无>33ms记录，但单次观测不是延迟保证，也不替代跳过的预算断言。Problem-3分配增加1706520 bytes，Problem-4增加46605984 bytes；不宣称所有资产分配下降。

### 本轮9张真实PNG逐张加载验收

本轮重新执行渲染后，对 artifacts/bin/InkBeautifierAvaloniaTests/release 下以下9张图片分别调用加载，并按同一顺序交给独立图像分析实际读取（全部可辨读）。分析器只显示顺序，文件名映射来自本轮明确传入路径，而非声称分析器识别文件名；日期资产是带日期的文件名，不是画有日期文字。

|顺序/文件|本轮目视结论|
|---|---|
|1 Ink0-Beautified.png|上部永字及左下孤立墨点；未见粗笔内白孔、意外断裂或异常长刺，未触边。|
|2 Ink1-Beautified.png|永字及分离点画；未见明显白孔、意外断裂或异常尖刺，未触边。|
|3 Ink2-Beautified.png|细长交错弧线及回转；左侧入线、右下弧线到达底边，有明确触边/截断外观。不能据此认定本次回归，原始黄金逐调用全部匹配；具体外推/记录端点/裁剪归因仍未定位。|
|4 Ink3-Beautified.png|连续起伏及回转；未见明显白孔、断裂或异常尖刺，未贴底，底部约三分之一连续留白。|
|5 InkProblem-1-Beautified.png|斜上长弧穿大环，粗段实心；未见明显白孔/断裂/异常刺或裁切。|
|6 InkProblem-2-Beautified.png|外环、折返内环、斜线相交；右上尖折可见，不直接作为异常，未见明显白孔/断裂或裁切，左下保留白边。|
|7 InkProblem-20261106-093537-Beautified.png|日期命名资产：多重宽回环叠交，底部粗横弧向右收尾；粗段未见明显孔洞、断裂或异常刺，交点连续且未触边，下部大片留白。|
|8 InkProblem-3-Beautified.png|左下粗弧上扬至大环，再细线回转；未见明显白孔/断裂/异常刺，左下虽近底仍有白边。|
|9 InkProblem-4-Beautified.png|上部大环、中部拱弧及右下回环相叠；局部加粗与转折可见，未见明显白孔、意外断裂或异常刺，未触边。|

验收仅当前PNG目视，不是像素diff，未提供旧PNG对照，不能证明任意像素一致或排除单像素问题。真实Skia覆盖断言及原始黄金分别约束渲染采样点与库几何。

### 文件范围与剩余

干净重实现累计相关文件（并非git diff，本次未执行git）：生产 Processing/InkBeautifier.State.cs、Processing/Outlines/InkBeautifier.OutlineBuilder.cs、Processing/Outlines/InkBeautifier.CoverageIndex.cs；测试 InkBeautifierReplayTests.cs、InkBeautifierCacheTests.cs、InkBeautifierPerformanceTests.cs、InkCanvasRenderingTests.cs；本 Docs/PerformanceOptimizationReplay.md。路径均相对本实验 Code/InkBeautifierLib、Code/InkBeautifierAvaloniaTests 或 Docs。本次接续只修改 ReplayTests黄金保护/可选缺件跳过及本文，没有重复修改已落盘生产优化。报告为 CleanReplay/Comparison.md、新after CSV、9份性能txt和9张PNG；原黄金未覆写。

剩余：未做无artifacts环境的隔离实际执行（缺件跳过分支已检查但未动态验证），未启用机器预算测试；Ink2触边具体成因、Problem-3/4分配增长的独立归因仍未定位。没有新增TFM/依赖/阈值变更，没有shell/git操作，没有删除用户文档。以上第6条之后的结果才是本次接续验证，不将历史Stage/Golden数字累计为本轮测试数。
