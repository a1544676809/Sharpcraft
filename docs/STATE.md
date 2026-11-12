<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

# 状态

> 跨会话的唯一进度真相源。只记录易变状态：当前里程碑、进行中的模块、阻塞项、下一步具体动作、未提交改动与后台任务。
> 稳定设计属于 `docs/modules/`，索引属于 `docs/README.md`，决策属于 `docs/decisions/`。本文件不超过 150 行（原则 14）。

## 当前里程碑

M5.3 渲染管线。当前焦点：**渲染性能**。

## 已完成

| 任务 | 内容 | 结果 |
| --- | --- | --- |
| T-786 | 渲染后端接缝收口 | 逃生口 **225 → 1**（−99.6%），`new WgpuRenderBackend` **9 → 2**，原生 pass **0**；唯一保留项 `SIM-SURFACE-NOT-IN-SEAM`（T-788）已裁决保持。`Brew.Wgpu` 在 `Framework/Rendering` 之外 **0 文件** |
| T-792 | 窗口只渲染 7–9 个区块 | 根因：预载后 29 秒无帧运行，无人应答 keep-alive，服务器 30 秒判死连接。修复后区块 **7 → 496**，服务器不再 `Timed out` |
| T-793 | 首帧请求 44.6 s | 根因：`BuildRequest` 二次网格化且未传 `StateCullTable`/`StateVisualCache`。根治：删除第二遍，改为消费 `ChunkMeshCache` 已有产出。**44.6 s → 26 ms**，几何逐项等价，分配 1.09 GB → 0 |
| T-794 | 首帧 6.8 s 空白 | 未复现——是 T-793 那 1.09 GB 分配风暴引发的一次性 GC，随修复消失。生产代码零改动 |
| T-798 | 区块几何分页共享 arena | 每层若干固定大小页，页内 first-fit + 释放合并；绘制绑定整页一次、按 `firstIndex`/`baseVertex` 逐区块绘制。**稳态 5.13–5.70 → 2.54–2.76 ms（−51%）**，`binds/frame` 2668 → 278（−90%），几何三项逐项一致。ADR-0029 取代 ADR-0020 |`n| T-797 | 默认选高性能 GPU + 可指定 | 新增原生垫片（复用绑定的 C 原型 + `[UnsafeAccessor]`），默认 `HighPerformance`；启动打印 `gpu: selected NVIDIA GeForce RTX 3060 Laptop GPU (DiscreteGPU, Vulkan) preference=high`。`MC_GPU` 支持 `high`/`low`/`list`/名称子串，失败回退并警告。ADR-0028 |`n| T-791 | GPU 区块剔除 | **实测否决**：剔除总收益 0.4–2.7 ms，CPU 循环 <0.05 ms（<1% 帧时间）；搬 GPU 省 <0.01 ms 却要新增 `DrawIndexedIndirect` + 一致性门禁。`ChunkCuller` 保留停放 |

## 进行中

**渲染帧时间归因（续）**：T-796 的装置已就绪（`MC_FRAME_CPU` + 三个 skip 开关 + `tools/verify/measure-frame.ps1`），结论见 `specs/performance-baseline.md` §12；用户要求暂停基准、等环境稳定，下一轮先重采基线（**注意：现在默认跑在 RTX 3060 上，旧基线是核显数据，不可直接比**）。`n`n**T-796 渲染帧时间归因的测量装置**：分段 CPU 计时（`MC_FRAME_CPU`）、三个 A/B pass 开关（`MC_SKIP_OIT` / `MC_SKIP_CLOUDS` / `MC_SKIP_TRANSLUCENT`）、脚本 `tools/verify/measure-frame.ps1`。装置已落地并跑出对照表，结论见 `specs/performance-baseline.md` §12。

## 阻塞项

- **T-801 结论**：地形+UI+实体合计仅 0.45–0.57 ms，OIT 1.32 ms、云 0.65 ms，剩余 **0.8–0.9 ms 为全帧固定成本**（清屏/天空/上屏拷贝/Submit/驱动）——下一轮用它自己的方式归因，未做之前不要再砍几何（收益被封顶在 0.45 ms）。

- **T-807 结论**：天体真实墙钟成本 **0.169 ms**（同进程交替 A/B，196 窗口），其中 CPU 侧约 0.39 ms（重建 0.05 + 逐 draw `WriteBuffer` 0.20 + sky disc 0.13），**约 0.22 ms 被 present 等待掩盖**。**阈值缓存实测无效**：1 mm 累积阈值下 `applied 38498/38498, skipped 0/0`（每帧位移都超阈值），实现保留为守卫但记为"当前无效"。**原版 `stars.vsh`/`sky.vsh` 只用 `ProjMat*ModelViewMat`，每帧零上传**；我们的实现每天体每帧一次 `wgpuQueueWriteBuffer`，这才是差异。**修法（未实施）**：天体基模型烘焙进顶点 + 复用共享相机 uniform；预计墙钟省 0.05–0.10 ms、CPU 省 0.32 ms。
- **T-806 结论**：P0 已修 —— 云深度 blit 的 bind group 改为随 `depthViewHandle` 缓存，创建次数 **每帧 1 次 → 全程 1 次**，地形带逐像素 0 差异；帧循环内再无其它每帧资源创建（只剩两处约 100 字节的 `new byte[]`）。`scene` 五子标记实测：**terrain 0.242 / entities 0.154 / celestial 0.140 / open 0.097 / sky 0.002**（合计 0.639），即那 0.23 ms 固定开销 = 天体 0.140 + 开 pass 0.097。**天体只有个位数 draw 却要 0.140 ms，疑为每帧重建/上传，是下一刀首选。**
- **T-804 结论**：实际是 **14 个 pass**（不是 12）。只有「世界文本 + 云 + 界面」可合并（同颜色/深度附件、顺序不变），**已实施：14 → 12**；`submit` 0.829 → 0.786 ms（**-0.043 ms**），地形带（含底部 UI 区域）**逐像素 0 差异**。由此测得**每个 pass 对象仅约 0.02 ms**，推翻"8 个 OIT pass = 0.5 ms"的旧推断（那 0.5 ms 是 pass 内容里约 1890 次 draw）。**结论：不值得继续砍 pass**；下一步应针对 draw 数量与每次 draw 的录制成本（`scene` 0.59-0.67 ms / 186 draw ≈ 3 µs/draw）。
- **T-803 结论**：原 `present` 0.87-1.02 ms 是**计时口径问题**（把宿主每帧回调也算了进去）。拆成 `swap` / `onframe` 后实测：真实交换链等待 **0.32-0.44 ms**，宿主回调 0.31-0.46 ms；三档 present 模式下 `swap` 几乎相同（0.32-0.44）⇒ 不是垂直同步，而是 `vkQueuePresentKHR` 的固定代价；fifo 的额外等待出现在 `acquire`（1.42-1.71），验证拆分正确。`desiredMaximumFrameLatency` / 图像数在 `Brew.Wgpu` 未暴露，无法做在飞帧数 A/B。**下一目标改为录制/提交路径**：`submit` 0.71-0.87（含 `encoder.Finish()` 对 12 个 pass 的校验）+ `scene` 0.57-0.67（186 draw）。
- **T-802 结论（重要）**：全部跳过后的**空帧 = 1.56 ms 且与分辨率无关**（858x527 与 429x263 差 0.001 ms）；CPU 分阶段显示 `present` 0.7-1.0 + `updates` 0.7 + `submit` 0.25。原版整帧 1.6 ms，而我们空帧就 1.56 ms。**下一步必须先做**：把 `updates` 与渲染帧解耦（tick / 60 Hz 步进）+ 查清 `present` 阻塞，否则继续优化渲染内容没有意义。
- 本会话期间本地 Fabric 服务器曾自行退出（`Can't keep up` 后进程消失，仓库外目录），已按 `run.bat` 的同款命令行重启；重启耗时约 14 s。后续测量需确认服务器存活。
- `docs/STATE.md` 在 `2e60e42` 之后与 `DocumentationDriftTests` 不一致（缺 SPDX 头、缺 `## 已完成`/`## 进行中`/`## 阻塞项

- **T-801 结论**：地形+UI+实体合计仅 0.45–0.57 ms，OIT 1.32 ms、云 0.65 ms，剩余 **0.8–0.9 ms 为全帧固定成本**（清屏/天空/上屏拷贝/Submit/驱动）——下一轮用它自己的方式归因，未做之前不要再砍几何（收益被封顶在 0.45 ms）。`/`## 下一步具体动作` 与 `最后更新：`、含绝对路径），已在 T-796 中按测试要求补齐。

## 下一步具体动作

**GPU 选择（用户明确指示，优先做）**：默认选最高性能 GPU，并预留设置可指定具体 GPU。

现状与依据：
- 绑定层 `Brew.Wgpu` 29.0.0.5 的 `Instance` **只有** `RequestAdapterBlocking(Surface)`，无 options 重载、无适配器枚举
- 但 `Native` 命名空间**存在** `WGPUPowerPreference`(`Undefined, LowPower, HighPerformance, Force32`) 与 `WGPURequestAdapterOptions`；原生库全平台齐备（`wgpu_native.dll` / `libwgpu_native.so` / `libwgpu_native.dylib`）
- 因此需自写 P/Invoke 垫片调 `wgpu_instance_request_adapter`（带 `powerPreference`）与 `wgpu_instance_enumerate_adapters`
- 按原则 12 需新增 ADR（绕开托管绑定属跨模块决定，须写明删除条件）
- 设置建议 `MC_GPU`：`high`(默认) / `low` / `list` / 其他文本按适配器名称子串匹配，匹配失败回退 high 并警告
- **必须启动时打印选中的适配器名称**——今天排查花时间正是因为原来只打印了驱动版本号

**之后的性能工作**：参考 OIT 的半透明重画占 50–56% 帧时间是已测得的结论（`MC_SKIP_OIT` / `MC_SKIP_TRANSLUCENT` 各能省 56.4% / 50.6%）；但用户指示**暂停测量**，因笔记本断电降频导致数值不可比，待环境稳定后重测。

**（更正）此前记录的"比原版慢约 4 倍"是错的**，由两个口径错误叠加造成：分辨率不同（1280×720 vs 854×480，2.25×）与**视距不同**（当时我强制设 32，而原版与我们的默认都是 **12**，区块数差约 6.8×）。

按同一口径重测（视距 12 = 双方默认、854×480、固定机位 `-74.098 / 121 / 37.303` 朝北、`MC_PRESENT_MODE=immediate`）：

| | 帧率 | 帧时间 | 区块 |
| --- | --- | --- | --- |
| 原版 26.3（核显） | 80 – 110 fps | 9 – 12.5 ms | — |
| 本实现（核显） | **≈ 84 fps** | ≈ 11.9 ms | 236 |

**即落在原版波动区间内偏下，不是慢 4 倍。**

**但两者都跑在核显上。** 日志 `window: surface ready on 101.7088` 中该字符串为 Intel 驱动版本（`32.0.101.7088`），即 **Iris Xe**；本机另有一块 **RTX 3060 Laptop** 未使用。原因是适配器请求用了无 options 重载（`RequestAdapterBlocking(surface)`），wgpu 自选时挑了核显。

**首要怀疑**：参考 OIT 序列每帧全跑 9 个 pass（bounds → cull → cloud bounds → transmittance → cloud transmittance → accumulate → cloud accumulate → composite），很可能是大头。先测其占比，不要凭猜测优化（原则 9）。

**注意**：用户提示机器即将断电，断电后**性能基准可能变化**，届时需重新采样基线。

## 已测量并记录的性能事实

- 剔除 ON：`chunks=646 culled=444` / `≈6.0 ms`；剔除 OFF：`chunks=674 culled=0` / `6.4–8.7 ms`
- 启动分段（进程年龄）：资源 0.38 s → 登录+预载 6.3–7.2 s → 客户端准备 0.26 s → 窗口 0.55 s → wgpu 1.01 s → 管线+上传+首帧 1.0 s。**渲染侧已无可去除项**
- compute 运行时真值：`pyramid: 9 levels, mismatches 0`、`motion: mismatches 0`（对 CPU 参考；原版无 compute shader，故无原版真值可提）

## 未提交改动

T-796 未提交：`src/Sharpcraft.Framework/Rendering/WindowedRenderer.cs`（分段计时 + 三个 pass 跳过）、`src/Sharpcraft.Framework/Rendering/FrameCpuTimings.cs`（新增）、`src/Sharpcraft.Framework/Rendering/FrameDiagnostics.cs`（4 个显式选项）、`src/Sharpcraft.Client/Game/GameOptions.cs`（宿主开关映射）、`tools/verify/measure-frame.ps1`（新增）、`docs/specs/performance-baseline.md` §12、`docs/traceability.md`、本文件。

最后更新：2026-11-11 12:05

## 待办（已登记，按优先级）

| 任务 | 内容 | 状态 |
| --- | --- | --- |
| — | 渲染帧时间归因（下一步动作） | 未开始 |
| T-795 | 草/植被类方块尚未实现 | `规格不足`——需先定原版植被渲染类别、随机偏移、双面、光照语义 |
| T-781 | Ctrl+F2 全景截图 | 未开始 |
| T-782 | 剩余 MCP 工具 | 未开始 |
| T-764 | 剩余 special 类型 | 未开始 |
| — | `ChunkCuller` 接线 | 停放，复查条件见 T-791 |

## 常用命令

```powershell
dotnet build Sharpcraft.slnx
dotnet test Sharpcraft.slnx --no-build
dotnet run --project tools/Sharpcraft.ClassMap -- --check
dotnet run --project tools/Sharpcraft.Docs.Indexer -- --check --check-coverage
pwsh -NoProfile -ExecutionPolicy Bypass -File tools/verify/check-simplifications.ps1
```

窗口启动（先杀进程，避免 DLL 锁导致构建静默失败）：

```powershell
Get-Process -Name 'Sharpcraft.Client' -ErrorAction SilentlyContinue | Stop-Process -Force
$pack=(Get-ChildItem "$env:LOCALAPPDATA\Sharpcraft\packs" -Filter 'minecraft-26.3-*.pack.zip' | Select-Object -First 1).FullName
$dbg="$env:TEMP\dbg.json"
'{"MC_CULL_STATS":"1","MC_VIEW_DISTANCE":"32","MC_PRESENT_MODE":"immediate","MC_PERF_SUMMARY":"1"}' | Set-Content $dbg -Encoding UTF8
src/Sharpcraft.Client/bin/Debug/net10.0/Sharpcraft.Client.exe --version 26.3 --gameDir "$env:APPDATA\.sharpcraft" --pack $pack --offlineDeveloperMode --username Test --quickPlayMultiplayer 127.0.0.1:25565 --width 1280 --height 720 --debugControl --debugOptionsFile $dbg
```

**配置传递规则（原则 13）**：游戏代码只读宿主传入的 `--debugOptionsFile` 快照，**不读环境变量**。用 env 设置 `MC_*` 会静默无效。

## 下一步动作（云改程序化生成，T-827）

规格已就绪：`docs/specs/render/cloud-geometry.md`（原版行为逐条写明，含位域与坐标位打包）。

| 片 | 内容 | 状态 |
| --- | --- | --- |
| S1 | 面表构建器 `CloudFaceTable.Build`（每面 3 词：单元 X/Z + 方向与标志位） | **已完成**，17 项单测，`CloudFaceTableTests` |
| S2 | ~~接缝能力~~ **无需改动**：两样能力已存在且为真实实现（见规格 §8） | **已完成（核查）** |
| S3 | 接线完成并实测：**无性能收益**（clouds 0.037→0.036 ms，缓存命中时两条路径都不重建几何）。价值在结构对齐与消除脆弱路径。A/B 截图无回归 | **已完成（无收益，见规格 §13）** |
| S4 | OIT 三阶段接到新顶点上 | 待做 |
| S5 | 删除 `CloudGeometryBuilder` / `CloudDrawCache` 的顶点路径 | 待做 |

下一步具体动作：**接线 `cloud_procedural.wgsl`**。着色器已写好但没有任何管线用它，且 WGSL 构建期不编译，所以它**完全未经验证**。接线步骤：①`CloudOitResources` 与两处 `CreatePipelineLayout` 增加 group 0 的 binding 2（CloudInfo uniform）与 binding 3（面表只读存储）；②上传面表并建 bind group；③建管线——**管线创建成功是第一道也是唯一的语法检查**；④确认只读存储能否绑在顶点阶段；⑤接主着色路径后与现有云逐像素比对。

注：云的损坏是**既存缺陷**（OIT 管线复用地形顶点布局，而数据是 7 float/28 字节，每 4 个顶点只有第 1 个落对），不是顶点打包引入的；顶点打包那笔改动已回退，等 S5 删掉旧路径后再单独重做。

接线方案已展开为逐条步骤，见 docs/specs/render/cloud-geometry.md §10（含 CloudInfo 的 48 字节布局与 vec3 对齐陷阱、绑定序号、绘制顶点数、以及只读存储能否绑顶点阶段的待实测点）。CloudOitResources.cs 的 CreateBindGroupLayout 与 CreateBindGroup 是两处主要改动位置。
