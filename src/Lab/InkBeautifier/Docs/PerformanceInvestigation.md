# InkBeautifierLib 分阶段性能调查（临时诊断，不是优化）

## 结论与范围

调查范围仅 `src/Lab/InkBeautifier`，只计同步库 `Process`，不含 UI、渲染、XML 解析、坐标变换、输出文件和指纹计算。未更改算法分支、阈值、缓存策略或几何计算；原性能测试保留，仅将测试类改为 partial 以复用相同资产解析器。

**不是只有“整笔重建”一个问题：历史重叠扫描和覆盖绕数是两个主要 CPU 成因；全部扫掠单元/凸包重建是独立的主要分配成因；最终样本检查又是接受 Up 独有的额外延迟。** 接缝交叉虽然次数巨大，但绝大多数被包围盒排除，不应按次数误判为首要 CPU 成因。采样本身是增量的且很便宜。

测量环境：Release，net10.0，运行时 10.0.14，x64 测试宿主，Stopwatch 10,000,000 ticks/s。没有记录 CPU 型号、功耗、后台负载或 GC 暂停事件；这是受共享环境噪声影响的阶段定位实验，而不是稳定延迟 SLA 基准。

## 仓库配置

- 根 `global.json` 只配置 Uno.Sdk=5.2.139，没有固定 dotnet SDK 版本。
- 根 `Directory.Build.props`：LangVersion=latest、Nullable/ImplicitUsings=enable，导入 `build/Version.props`，统一 `ArtifactsPath` 为仓库 `artifacts`，CA1416 作为错误；未修改这些设置。
- 实验库、测试均 net10.0；测试引用 MSTest 4.0.1、Avalonia.Headless 12.1.0 和 demo/lib；测试代码复用库 API，不调用 UI。
- `TestAssets/*.txt` 自动发现；当前全部 9 资产，不硬编码资产名单。沿用原性能测试的 940×700 fit 参数、原始坐标、压力换算、逐笔时间、实际 Status 优先和旧资产末尾 synthetic Up。
- 构建目标是实验测试 csproj，不是根解决方案；程序集在 `artifacts/bin/InkBeautifierAvaloniaTests/release/InkBeautifierAvaloniaTests.dll`。

## 插桩与结果口径

新增 `Processing/InkPerformanceDiagnostics.cs`：默认关闭；显式同步线程作用域开启 coarse/fine，Dispose 恢复上一作用域。不开启不会构造诊断会话。仅适合同步 Process 所在线程，不是异步/跨线程 profiler。

- coarse：Process → TryAddPoint → Sampling；Process → BuildGeometry → TemplateSweep、RepairCoverage（RawCoverage、FinalSampleCoverage）、SweepCells。
- fine：进一步计完整重叠查询、完整绕数查询、完整凸包、完整模板添加/支撑/弧、边界追加/配对/交叉查询、外侧历史裁剪、面积和去重。
- 微操作如线段交叉、每条绕数边、每个重叠候选、单点模板变换**不逐次 Stopwatch**，只计次数；完整查询/循环批次计时。
- **inclusive** 包含已计时的子阶段；**exclusive** 从当前阶段减去所有直接计时子阶段的 inclusive。只能对同一层互不重叠阶段或全部 exclusive 求和，不能把 Process、BuildGeometry、TemplateSweep、OverlapSegment 相加。
- 分配同样分 inclusive/exclusive，单位为当前线程 GC 分配字节，不是存活堆大小。计时作用域为 struct，会话/快照/报告在外层计时之外。残余 exclusive 包含未单独计时逻辑和探针开销，不是纯算法 self CPU。
- GC0/1/2 在每次调用外采样，是进程范围集合次数，不是暂停时间或每阶段归因。外部报告/哈希分配也可能改变后续 GC 时机。
- FinalSampleCoverage 的 scope 每次 Repair 都会进入，Move 上只有空分支；看 `FinalSampleChecks` 才能确认实际最终扫描量。

## 实验及功能验证

1. 每个资产完整预热一次（不计时）。
2. 依次 off-before / coarse / fine / off-after，逐次调用外计 Stopwatch、分配和 GC；保存每个输入的状态、笔序号、笔内位置、接受与否及所有阶段/工作量增量。
3. 对每次返回的 StrokeId、接受/完成标志、Outline 和全部 Outlines 的顶点数量与 double 位模式做 SHA256。四种模式指纹一致才通过。
4. 全部 9 资产诊断测试多轮执行均 9/9 通过。另运行排除性能预算、原性能诊断和新诊断的功能/渲染/汇总测试，55/55 通过。最后一次汇总测试单独通过。Release 构建成功。
5. 报告均在计时外写入 `artifacts/InkBeautifierDiagnostics`；再次执行会将同名上轮原始输出复制到 `previous/<UTC时间>/`。

产物：每资产 `*.summary.txt`，四模式 `*.calls.csv`，粗/细 `*.stages.csv` 和 `*.work.csv`。`DerivedMeasurements.md` 按 Move/Up、前中后三段汇总，并挑每笔约 1/6、1/2、5/6 最近接受点及所有 Up 输出分解。每一点的完整分解保存在 CSV，不只是代表点。

## 全部资产最近一轮结果

总时间为所有 Process 调用墙钟之和，ms；分配 MB 用十进制。关闭前后给出噪声范围，不能把它解释为真正未插桩二进制基线。

|资产|off-before ms|coarse ms|fine ms|off-after ms|off-before 分配 MB|
|---|---:|---:|---:|---:|---:|
|Ink0|536.342|2057.276|855.978|783.278|331.722|
|Ink1|573.516|679.122|1029.966|1479.707|263.527|
|Ink2|19.154|20.304|30.048|23.670|14.558|
|Ink3|18990.532|18709.066|17927.836|17608.643|3254.952|
|InkProblem-1|21.251|20.588|24.117|23.725|24.910|
|InkProblem-2|43.961|44.731|49.391|44.769|48.174|
|日期资产 20261106-093537|2157.046|2222.103|2336.296|2187.851|874.168|
|InkProblem-3|20.174|19.907|24.144|21.173|24.827|
|InkProblem-4|572.911|616.160|648.503|626.474|317.415|

### 插桩开销和漂移

日期资产最近一轮关闭前后均值 2172.449ms，coarse +2.28%，fine +7.54%；InkProblem-2 均值 44.365ms，coarse +0.83%，fine +11.33%。Ink3 最近一轮均值 18299.588ms，coarse +2.24%，fine -2.03%，负值不是加速，说明环境漂移超过待测开销。Ink0/Ink1 该轮尤其不稳定，不能声称其差值是探针开销。

较早完整插桩一轮（已归档）Ink0：423.220 / 434.803 / 474.021 / 420.936ms，相对两端均值 coarse +3.01%、fine +12.31%；Ink1：321.435 / 327.442 / 354.977 / 316.092ms，coarse +2.72%、fine +11.36%。更早第一轮 Ink3：14108.189 / 14450.921 / 15421.433 / 14334.794ms，coarse +1.61%、fine +8.43%，但此轮尚未补齐模板/裁剪计时，不作为当前完整细模式绝对性能依据。多轮的工作量计数一致。

**限制：off 是含禁用探针分支的基线，并未另编译完全去除探针的原始二进制；因此尚不能量化默认 off 相对原始代码的开销。** 高频计数分支自身也可能影响 JIT 内联。所有模式哈希/报告虽在计时外，仍影响堆与缓存；未进行随机模式顺序或统计显著性实验。不要用上述单轮差值声称精确探针成本。

## Ink3 细分

1414 次 Process、1 笔、497 次 BuildGeometry（496 次长笔轮廓）；接受 Move 164+145+187=496，另接受 Up。不是每个输入都重算，而是每个接受点都整笔生成；过滤点直接复用旧几何。

### 粗模式互不重叠主阶段

|阶段|inclusive ms|分配字节|
|---|---:|---:|
|TryAddPoint（含 Sampling）|1.650|516240|
|TemplateSweep|9702.624|99869352|
|RawCoverage|6653.560|31562664|
|FinalSampleCoverage|71.317|0|
|SweepCells|2271.626|3122861360|

BuildGeometry inclusive=18704.723ms；Process inclusive=18707.360ms，外计=18709.066ms。上表没有把容器父阶段重复相加。SweepCells 约占所有 Process 分配 **95.94%**，而非 CPU 最大头。

### 细模式重点（不是与粗模式同一时间样本）

|阶段|调用次数|inclusive ms|exclusive ms|inclusive bytes|
|---|---:|---:|---:|---:|
|OverlapSegment|619175|8619.747|8619.747|0|
|Winding|397336|6357.054|6357.054|0|
|PenCapInterior|124248|6324.272|17.870|0|
|OverlapPoint|126238|219.468|219.468|0|
|ConvexHull|619672|1476.424|1476.424|1740048456|
|BoundaryPair|619671|410.113|70.736|0|
|CrossBoundary|1238350|174.951|174.951|0|
|BoundaryPoint|1270897|168.731|168.731|0|
|OuterTrim|43778|93.468|93.468|0|
|TemplateSupport|620167|44.481|44.481|0|
|AddPenCap|1239344|99.304|99.304|0|
|TemplateArc|995|1.698|0.549|0|
|SignedArea|621313|62.919|62.919|0|
|Duplicates|496|5.430|5.430|19741760|
|CoverageCap|650|8.836|6.860|31562664|

PenCapInterior 和 Winding 重叠，不能相加；BoundaryPair 同样包含 CrossBoundary/部分 BoundaryPoint。模板变换总计 54,614,232，支撑候选 26,667,181，凸包候选 53,291,792。模板大小 43，单元候选为两个模板的 86 点。

### 前中后代表接受点与 Up（粗模式）

|输入/笔内位置|Process ms|TemplateSweep ms|RawCoverage ms|最终样本 ms|SweepCells ms|分配 bytes|Outlines|
|---|---:|---:|---:|---:|---:|---:|---:|
|235/1413|6.1802|2.8037|1.3854|0|1.9818|2425920|457|
|705/1413|32.5216|16.5266|12.0752|0|3.9042|5945592|1122|
|1178/1413|54.1797|28.7367|21.2240|0.0001|4.2063|10710960|2047|
|Up（输入1414）|153.3135|42.321|31.340|71.293|8.334|12932720|2487|

Up 此调用 GC0/1/2=1/1/1，但不能将 71.293ms 全归因于 GC；最终样本检查确实扫描了 2486 个样本。该分支本轮没有新补环分配，说明“检查很贵”不等于“真的补了很多环”。最终 Outline 4958 顶点。粗模式 Move 三段累计 1094.724 / 5559.424 / 11901.601ms；计数相近但接受率不同，不能以所有 Move 的低 p50 掩盖接受点高延迟。

## 日期资产细分

279 次输入、226 次 BuildGeometry，225 次长笔轮廓。Move 前中后接受 80/87/59。**Up 未接受**，仅 Process/TryAddPoint，约 0.008ms、72 bytes，不执行 BuildGeometry 或最终补环。这里没有改动原算法去强制完成扫描。

粗模式：TemplateSweep=1146.142ms；RawCoverage=650.096ms；FinalSampleCoverage 空分支累计=0.007ms；SweepCells=423.832ms，分配 821,536,648 bytes，占 Process 874,154,176 bytes 的约 **93.98%**。

细模式：OverlapSegment 162793 次/1059.461ms；Winding 44457 次/652.642ms；OverlapPoint 25650 次/15.979ms；凸包 163019 次/342.262ms/457,757,352 bytes；CoverageCap 3314 次/10.064ms/23,967,848 bytes。边界配对 91.572ms（exclusive 16.548ms），CrossBoundary 38.529ms，BoundaryPoint 53.925ms，OuterTrim 4.821ms，TemplateSupport 10.543ms，TemplateArc 19.668ms（exclusive 3.452ms），SignedArea 14.936ms，Duplicates 1.416ms/7,136,968 bytes。弧生成不是零，但仍不是主要瓶颈。

代表接受点：46/279=1.8101ms/1,548,136 bytes（Sweep .6723、Raw .1723、Cells .9632）；140/279=9.5289ms/4,258,376 bytes（4.5339、3.0225、1.9650）；233/279=18.1835ms/6,162,600 bytes（9.2494、5.2822、3.6417）。最终已有 Outline 5001 顶点、1480 Outlines。

## 各问题机制、独立性与源码位置

以下行号为本次插桩后的 `Code/InkBeautifierLib/Processing/Outlines/InkBeautifier.OutlineBuilder.cs`；Process 在 `Processing/InkBeautifier.cs:31`。

1. **整笔几何重建入口**：BuildGeometry:259、BuildTemplateSweepOutline:322、BuildSweepCells:295。采样列表持续增长，遍历起点仍是 0，没有已生成轮廓或单元前缀复用。Ink3 实际只追加 2488 样本，却累计处理 620167 样本、重建 619671 单元，约最终单元数的 249 倍。日期资产只追加 1481 样本，却重建 163018 单元，约最终单元数的 110 倍。此为其余问题的重复乘数，不是其余问题的替代解释。
2. **历史线段重叠查询**：OverlapsEarlierSweep:493 附近。每个历史样本段对更早前缀扫描，只有距离/包围盒/命中早退，没有空间索引。Ink3 508,203,052 候选，日期资产 77,285,315。给定 S 样本，一次重建最坏 O(S²)；若 S 与接受点 A 成正比，逐次重建累计最坏 O(A³)。早退、局部重叠和笔形决定常数。此处本身零分配，不能靠减少 GC 解释其 CPU 成本。
3. **原点覆盖及绕数**：RepairRawPointCoverage:1461、IsPenCapInteriorFilled:1550 附近、IsFilledByNonZeroWinding:1598。每次重建扫描全部 RawPoints；中心填充检查必要时再测四个内部点，短路返回。Ink3 124248 原点检查、397336 绕数调用、1,308,114,868 条实际边；日期资产 25875/44457/141,397,225。复杂度至少涉及 O(R×V) 全边扫描，另有 O(R²) 历史点重叠扫描。它是独立 CPU 热点，同时受轮廓长度和补环数影响。
4. **补覆盖环与检查反馈**：AppendCoveragePenCap:1515 附近。Ink3 共补650环，日期资产3314环，后者原点更少却补环更多。补环会增大之后检查使用的 currentOutline，放大同次重建剩余绕数开销，并增加复制/数组分配；但下一次重建又从模板轮廓开始，并不复用此次修复。因此补环是独立构造成本，也与绕数成本相互影响。不能直接把 CoverageCap inclusive 与 RepairCoverage 相加。
5. **最终样本检查**：RepairRawPointCoverage 内 completesStroke && rawPoints.Count>=10。每样本先历史点重叠再可能绕数，遇到首个未覆盖点补环并 break。Ink3 Up 实际2486检查；日期资产0。最坏 O(S²+S×V)，仅接受 Up 有此附加项；和普通 Move 原点覆盖独立，但共享查询方法和轮廓。
6. **Outlines 扫掠单元与凸包**：BuildSweepCells:295、AddPenCap:1665 附近、BuildConvexHull:1682。每相邻样本新建候选 List、变换两份43点模板、Sort 86点、上下凸包链新建 List；面积方向不同时 Reverse().ToArray()。每次 O(S×T log T)，T=86 常数；逐笔累计 O(A²) 级别构造。Ink3 单元阶段分配3.123GB，其中凸包1.740GB，外层候选/容器/可能反向数组等 exclusive约1.383GB。日期资产分别821.537MB/457.757MB/363.782MB。候选数组、凸包缓冲和外层集合是实际大分配；AddPenCap 自身填已有列表时记录为0并不表示整条构造链0分配。方向反转分配尚未单独拆出，包含在 SweepCells exclusive 中。
7. **接缝/自交与外侧裁剪**：AppendBoundaryPoint:1054 附近、AppendBoundaryPair:865 附近、TryFindCrossBoundaryIntersection:993、TrimOuterBoundaryAtEarlierIntersection:926、TryGetSegmentIntersection:1126/1149/1190/1229。近端自交和跨边界只扫约模板长度窗口（O(T)），外侧裁剪会扫更老前缀（可 O(V)）。Ink3 近端候选52,400,691、跨边界52,357,504、老前缀36,369,439，共141,147,094交叉入口，仅39040通过包围盒进入解析求交，约0.028%；日期资产34,254,139入口/65257通过，约0.191%。所以次数多而细模式耗时远小于重叠/绕数；包围盒确实有效。OuterTrim 可有额外增长，但当前不是头号热点。线段交叉只计数，不为每次开 Stopwatch，计时归属上层扫描批次。
8. **模板支撑/变换/弧、面积、去重**：GetTemplateSupportIndices:773、PlaceTemplatePoint:1330 附近、AddTemplateArc/AddBoundaryTemplateArc、GetSignedArea、RemoveConsecutiveDuplicates。重复模板支撑扫描和变换均存在，但已量化为次级；去重另构造整轮廓 List，是独立小分配（Ink3 19.742MB），没有忽略它。
9. **采样/输入过滤**：TryAddPoint:12、AppendBezierSamples:205 附近，只读取尾部控制点并追加有限插值样本，非整笔重采样；Ink3 候选2983/新增2488，日期1705/1481。主轮粗模式采样总0.368ms/0.142ms。平滑速度/宽度只处理末尾常数个点。Process 自身状态字典和返回对象也非主要成本。NormalOffset fallback 有单独阶段，本资产实验没有出现，不给未执行路径虚构耗时。

## GC 与未完成的归因

Ink3 最近一轮 off-before GC=210/143/31，coarse=217/150/29，fine=225/148/25；日期资产 off-before=55/35/4、coarse=56/36/5、fine=56/35/5。高分配确实伴随多代 GC，但没有 GC 事件跟踪，不能判断每次暂停长度或认为某个慢点一定由 GC 导致。代表点中多个慢调用 GC=0/0/0，证明算法扫描本身已足以产生高延迟。

本调查已定位全部主要实际执行热点，但未做到：完全无探针二进制对照、隔离后台负载、每轮固定 GC 状态、逐分配调用栈、GC 暂停事件、独立计时方向反转和每个凸包内部 sort/pop 子操作、未触发 fallback 的合成覆盖。没有为了测试覆盖修改原算法，也没有把这些局限包装为已验证结论。

## 复现入口（使用构建/测试工具，不需脚本）

- Release 构建实验测试项目。
- 对上述 Release 测试程序集运行筛选 `TestCategory=StageDiagnostics`（当前9例，单轮约2分钟到数分钟，受后台负载影响）。
- 再运行 `TestCategory=StageAnalysis`，生成所有笔前中后代表点和分组表。
- 功能验证筛选 `TestCategory!=PerformanceBudget&TestCategory!=PerformanceDiagnostics&TestCategory!=StageDiagnostics`。原机器敏感33ms预算保留但不作为本诊断成败条件。
- 若需真正无插桩基线，应在用户准备撤回时保存本轮 artifacts，再单独构建原始库重复相同回放；本报告没有执行 git 操作。
