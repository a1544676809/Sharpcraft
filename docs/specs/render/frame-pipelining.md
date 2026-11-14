<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

# 帧间流水线（规格，待评审）

> 状态：**规格已就绪，未实施**。实施前需项目所有者评审。
> 关联：`frame-baseline.md`（实测数字）、`pass-reduction.md`（pass 成本模型）。

## 1. 要解决的问题

> **数据更正（2026-11-14，实测）**：本节最初的「整帧 1.919 ms、录制 1.28 ms、可省 −27%」是 OIT 半精度化（T-829）与顶点压缩（T-832）等优化**之前**的测量。当前构建实测录制只占整帧约 10–13%，见 §14。以下为当前实测。

实测一帧 **0.85–1.05 ms**（`frame-gap ≈ 0.001–0.002 ms`），整帧没有明显空闲等待，各阶段首尾相接。因此没有"等 GPU"的时间可回收；唯一可能的并行收益来源是把 CPU 录制分到另一线程——但录制本身只有约 0.1 ms，没有多少可隐藏（见 §14）。

当前分解（2026-11-14，Release，`MC_PRESENT_MODE=immediate`，`MC_FRAME_LOCAL` demo 与 `MC_FRAME_WATER` 两种确定性场景，1400×754；同机 2534×1334 数值相当）：

| 类别 | 阶段（`frame-cpu` 分段） | 合计 |
| --- | --- | --- |
| CPU 录制（`FrameExecutor.Record` 内部） | `scene-open` `scene` `scene-sky` `scene-celestial` `cel-build` `cel-disc` `oit` `text` `clouds` `ui` | **≈0.08–0.13 ms（≈10–13%）** |
| CPU 准备 | `prep-pump` `prep-ui` `prep-entities` `camera` `acquire` | ≈0.14–0.25 ms |
| 提交 | `sub-finish` 0.20–0.27 + `sub-queue` 0.12–0.15 | ≈0.32–0.42 ms |
| 呈现 | `swap` | ≈0.28–0.33 ms |

## 2. 目标与判据

> **2026-11-14 更新**：下表「自 1.92 ms」「−25% / −27%」基于已过时的录制数字（见 §1 更正与 §14 实测）。当前实测整帧 0.85–1.05 ms、录制仅 0.08–0.13 ms，该项优化已无实际可挖掘空间；下表仅作历史记录保留，不再作为验收目标。

| 项 | 值 |
| --- | --- |
| 目标帧时间 | ≤1.45 ms（自 1.92 ms，**−25%**） |
| 理论最优 | max(录制+`Finish()` 1.28, 提交+呈现 0.51) + `acquire` 0.11 ≈ **1.39 ms（−27%）** |
| 判据 | 同机位、同进程 A/B（单线程 vs 双线程），**不得跨运行对比** |
| 画面判据 | 离线 `--frame` 改动前后**哈希相等**（不得有视觉差异） |

**不得把本项当作"已提速"引用**，直到同进程 A/B 给出数字。

## 3. 线程划分

```
主线程（提交线程）              录制线程
  acquire 下一帧图像
  ── 交棒 ──▶                   录制第 N+1 帧
  submit 第 N 帧                  · 准备（pump/UI/实体/相机）
  present 第 N 帧                 · 构建几何
  ◀── 等待录制完成 ──             · 全部 Pass 录制
                                 · Finish()
```

**同步点**：swapchain `acquire` 留在提交线程，因为录制需要目标纹理。因此关键路径为
`acquire + max(录制, 提交+呈现)`。

**约束（原则 14 单轨）**：不得因此产生第二套帧管线。离屏与窗口路径 MUST 继续共用同一套 Pass 执行器；本项只是改变执行它的线程。

## 4. 必须双缓冲的数据

GPU 侧的 uniform 无需处理：`Queue.WriteBuffer` 内部有暂存环，不会踩坏在飞的写入。

**竞态在 C# 侧数组**——录制第 N+1 帧会覆盖第 N 帧仍在读取的内容：

| 数据 | 位置 | 份数 |
| --- | --- | --- |
| 间接绘制参数记录 | `ChunkGeometryStore` / 各 arena 的参数页 | 2 |
| UI 顶点与索引 | `WindowedRenderer` 的 UI 缓冲 | 2 |
| 动态实体几何 | 实体绘制路径 | 2 |
| 文本几何 | 文本绘制路径 | 2 |
| 每帧 uniform 字节数组 | `WindowedRenderer` 的 `cameraBytes` / `frameLightmapBytes` / `fogValues` | 2 |

**份数取 2**（不是 3）：同步点保证同一时刻最多两帧在飞。

## 5. GPU 缓冲的延迟释放

录制线程不再持有"这一帧用完就能删"的假设。规则：

- 缓冲/纹理的释放 MUST 挂到"使用它的帧已完成提交"之后；
- 采用**帧编号队列**：释放请求入队时带上当前帧号，提交线程每帧出队所有 `帧号 <= 已提交帧 - 2` 的请求；
- 不得依赖 GC 或 `using` 作用域的隐式时机。

**已实施**：`DeferredBufferRelease`。`Release(handle)` 以当前帧号入队，`Collect()` 只销毁 `帧号 <= 当前 - 2` 的项。生产侧在构造时绑定 `backend.Destroy`（一次性委托，不是每帧分配），因此无需图形设备即可测试。
窗口循环中已接管六处"缓冲扩容后销毁旧缓冲"：`uiVertex`、`uiIndex`、`dynamicVertex`、`dynamicIndex`、`entityLayerVertex[i]`、`entityLayerIndex[i]`。关闭路径（进程退出时）仍立即销毁，因为那时没有在飞的帧。

## 6. 计时

现有 `FrameCpuTimings` 是单线程累加，跨线程会互相污染。要求：

- 每个阶段 MUST 记录**所属线程**，分开累加；
- 新增 `pipeline-wait` 阶段（提交线程等待录制完成的时间），它与 `frame-gap` 一起用于判断流水线是否真的重叠；
- 若 `pipeline-wait ≈ 录制时长`，说明并行没有生效，必须视为失败。

## 7. 回退开关

MUST 提供单线程路径并作为默认，双线程由宿主层显式开启（原则 13：开关只在宿主层解析）。

- `MC_FRAME_PIPELINE=0|1`，默认 `0`；
- 两条路径 MUST 共用同一套 Pass 执行器，只有"谁来调用它"不同；
- 回退路径 MUST 在每次改动后仍被测试覆盖。

## 8. 风险

| 风险 | 影响 | 缓解 |
| --- | --- | --- |
| 双缓冲遗漏某处数组 | 画面撕裂/闪烁，且难复现 | 逐项列出（§4），每项加注释指明份数 |
| 缓冲释放时机错误 | 使用已释放资源，崩溃或黑屏 | 帧号队列（§5），不靠作用域 |
| 跨线程异常传播 | 崩溃信息丢失 | 录制线程的异常 MUST 回传提交线程并原样抛出 |
| 并行未生效却以为生效 | 假收益 | `pipeline-wait` 必须显著小于录制时长（§6） |
| 复杂度上升导致后续维护困难 | 长期成本 | 默认单线程，双线程是可选路径 |

## 9. 实施切片（每片可独立验收）

1. 抽出"录制一帧"为独立可调用单元（不改线程，单线程跑通，离线帧哈希不变）
2. 加 `pipeline-wait` 计时与线程归属（仍单线程，仅计时正确）
3. 引入录制线程 + 双缓冲（先只覆盖间接参数与 UI 顶点）
4. 补齐其余双缓冲项与帧号释放队列
5. 同进程 A/B 测收益；若 `pipeline-wait` 不显著小于录制时长，回滚

**每片都必须保持离线 `--frame` 哈希不变。**

### 9.1 哈希基线的有效期（实测更正，2026-11-13）

离线 `--frame` 渲染的是**当前服务器世界状态**。同一世界状态下哈希稳定（连续两次相同），但世界随时间演化（实体移动、水流、方块变化）后哈希会变。

因此正确判据不是"等于某个固定值"，而是：

1. 改动前后各渲一次，**必须相同**；
2. 若中途过了较长时间，用 `git stash` 回到改动前重渲以确认基线，不要拿历史值比对。

实例：本会话早期基线 `5821CCAD…` 在数小时后变为 `4D5A24C0…`，经 `git stash` 对拍确认是**世界状态变化**而非代码回归。

## 10. 切片 1 的状态清点（实测，2026-11-13）

录制区是 `WindowedRenderer.cs` 的 **L1223–L1829**（camera → present-rec，约 600 行）。用脚本对"区外声明、区内引用"的局部量做清点，得到 **105 个**，分三类：

| 类别 | 数量 | 处置 |
| --- | --- | --- |
| 诊断类（`skip*`、`cameraStarted`、`cameraTotalMs`、`worstCameraMs`） | 13 | 按原则 13 可整体删除，不进状态对象 |
| **每帧重写的 CPU 数组** | **10** | **必须双缓冲（§4 的准确清单）** |
| GPU 资源 / 管线 / 标量 | 82 | 状态对象持有即可，无并发问题 |

### 10.1 更正：不是 10 个"数组"，而是三类不同问题

初版把脚本找出的 10 个名字一律当成"每帧重写的 CPU 数组"，**这是错的**。逐个核对声明后分三类：

| 类别 | 名字 | 需要的机制 |
| --- | --- | --- |
| **CPU 暂存缓冲**（每帧完整重写） | `cameraBytes`、`frameLightmapBytes`、`frameFogValues`、`frameFogBytes` | **真正的双缓冲** |
| **CPU 列表**（每帧重填） | `uiRanges`（原 `ui.Ranges.ToArray()`） | 复用容器，无需双缓冲 |
| **GPU 缓冲池**（持久、按需扩容） | `entityLayerVertex`、`entityLayerIndex`、`dynamicVertex`、`dynamicIndex`、`uiVertex`、`uiIndex` | **在飞保护**，不是 CPU 双缓冲 |
| 容量/计数标量 | `entityLayerVertexCapacity`、`entityLayerIndexCount`、`dynamicVertexCapacity`、`dynamicIndexCapacity`、`uiVertexCapacity`、`uiIndexCount` | 无（单线程写入） |

GPU 缓冲池按 L443 的循环持久增长，每帧只重置计数（`entityLayerIndexCount[i] = 0`），句柄本身跨帧存活。它们**不能**靠 CPU 双缓冲解决——需要的是"提交后延迟销毁/延迟复用"，与 §5 的帧号释放队列是同一套机制。

### 10.1.1 已顺带修掉的两处每帧分配

| 位置 | 问题 | 处理 |
| --- | --- | --- |
| 帧循环内 | `frameLightmapBytes`、`frameFogValues`、`frameFogBytes` 每帧新建 | 抽成 `FrameUniformStaging`，一次分配 |
| `uiRanges` | `ui.Ranges.ToArray()` 每帧分配新数组 | 改为 `List` + `Clear()`/`AddRange()` |

600 FPS 下这两处合计每秒约 2400 次分配，全部是完整覆写前的无谓分配。
### 10.2 结论

状态对象需承载 82 个稳定引用 + 10 组双缓冲数组。这是约 300 行的重构，**超出微切片范围**，需作为独立任务实施；实施顺序仍按 §9 的切片 1（先单线程抽出、离线帧哈希不变）。
## 11. 录制区的分块实测（2026-11-13）

按 `Cpu(FrameCpuTimings.*)` 的阶段边界测量，录制区的实际构成（`WindowedRenderer.cs`，抽离后 2344 行）：

| 块 | 行数 | 内容 |
| --- | --- | --- |
| `scene` | **183** | 场景主 pass 编排（天空、地形、实体的绑定与绘制） |
| `scene-entities` | **107** | 实体层绘制 |
| `prep-ui` | **103** | 界面几何准备与上传（非录制，属准备侧） |
| `clouds` 建立 | **73** | 云几何的构建（非绘制） |
| `clouds` 绘制 | **13** | 只有两行是绘制（`SetDraws` + `Draw`），早已是组件 |
| `updates` | 69 | 世界更新推进 |
| `frame-gap` | 53 | 泵与报告 |
| `prep-entities` | 46 | 实体几何准备 |

合计约 **700 行**，其中真正的"录制"部分是 `scene` + `scene-entities` + `clouds`（绘制侧）≈ 440 行。

### 11.1 已抽离的（每次提交都验证离线帧哈希不变）

| 组件 | 减少 |
| --- | --- |
| `FrameUniformStaging` | −5 |
| `EntityLayerPool` | −28 |
| `GrowableGeometryBuffers`（界面与动态各一实例） | −23 |
| `EntityPassRecorder` | −74 |
| `TerrainPassRecorder` | −25 |
| `SkyPassRecorder` | −52 |
| `FrameGeometryUploader` | −86 |
| `FrameUniformWriter` | −9 |
| `StaticWorldBuffers` | −35 |

累计 **−335 行**，`WindowedRenderer.cs` 由 2400 降至 **2065**。scene 主 pass 的 `switch` 现只剩四段各约 6 行的调用，共约 36 行（原约 180 行）。

### 11.2 关键发现：单个 pass 不需要大状态对象

文本 pass 只有两行（`worldText.SetDraws(...)` / `worldText.Draw(...)`），因为 `WorldTextResources` 早已封装。**这说明逐个 pass 抽成"显式传句柄的静态方法"即可，不必先把 82 个句柄塞进状态对象。**

大状态对象只在最后把整个录制序列整体搬走时才需要。因此建议顺序：

1. ~~`clouds` 绘制块~~ **已证实无内容可抽**（13 行中 2 行是组件调用；那 73 行是云的**建立**，属几何构建）
2. ~~`scene-entities`~~ **已完成**（`EntityPassRecorder`，98 行 → 7 行调用）。140 → 7 行
3. ~~`scene`（183 行）~~ **已完成**：四个 case 全部抽成 `SkyPassRecorder` / `TerrainPassRecorder` / `EntityPassRecorder`

每步判据相同：**离线 `--frame` 哈希不变 + 全量测试通过**。

## 12. 切片 3A：多帧哈希判据（2026-11-14，已建立）

**为什么需要新判据**：切片 3 改的是 `WindowedRenderer` 的**循环 / 线程编排**，而
`OffscreenRenderer` 只调一次 `Record`、没有帧循环。沙盒帧（即使 2B-2b 后共用执行器）
验证的是"录制输出不变"，验证不了"线程编排正确"。切片 3 的判据必须来自窗口路径的
多帧输出。

**实现（只加诊断，不改渲染语义）**：新增 `MC_FRAME_HASHES=N`，由宿主经
`GameOptions.ForFrame()` 解析进 `FrameDiagnostics.FrameHashes`（游戏程序集零环境变量读取）。
`FrameHashTrace.cs` 每帧渲完后用现成的 `FrameCapture.Capture` 回读颜色目标，打印
`frame-hash: #i <sha256>`，收尾打印 `frame-hashes: count/distinct/digest`。单文件 +
窗口循环三处调用，可整体删除；N 帧后停止，稳态零开销（启用时每帧一次 readback +
一次额外 submit，故不可同时用于帧时间测量）。

**判据命令（写死，勿变选项）**：`--quickPlayMultiplayer 127.0.0.1:25565 --width 1400
--height 754 --debugOptionsFile <pinned.json>`，JSON：

```json
{"MC_FRAME_LOCAL":"1","MC_FRAME_HASHES":"20","MC_PRESENT_MODE":"immediate",
 "MC_VIEW_DISTANCE":"12","MC_FORCE_DAYTIME":"6000","MC_FORCE_PARTIAL":"0.5",
 "MC_WINDOW_SECONDS":"8","MC_AB_TOGGLE":"5","MC_SKIP_SKY":"1"}
```

`MC_FRAME_LOCAL` 给静止世界与固定机位，`MC_FORCE_DAYTIME/PARTIAL` 钉住昼夜与云，
`MC_AB_TOGGLE=5 + MC_SKIP_SKY=1` 让画面按 5 帧一组在"有天空 / 无天空"间**确定性变化**，
避免"20 帧全同"这种对帧间状态不敏感的弱序列。

**两次运行实测（同一二进制、同一配置）**：

```
frame-hashes: count=20 distinct=2 digest=2eb9f70612dae232ea8ca3af53ea4a1833f48ce49d8198dbbe8f7618db9713b4
序列（前 8 位）: 478e065e×5, 3f71957c×5, 478e065e×5, 3f71957c×5
```

两次**逐一相同**、digest 相同。**敏感性**：单独加 `MC_SKIP_SKY=1`（不设 AB）时 #0 由
`478e065e…` 变 `3f71957c…`，证明哈希对录制内容敏感、不是空白帧的常数。

**对照（为什么必须钉住配置）**：同一诊断放在连服务器配置（`MC_FRAME_HASHES=20`、
`MC_WINDOW_SECONDS=12`、`MC_FORCE_DAYTIME=1`、`MC_CULL_STATS=1`、`MC_FRAME_CPU=1`）时，
两次运行 `distinct=20`、digest 分别 `9a4d26d5…` / `161e92ad…`、#0 也不同。不稳定来源：
区块流式到达顺序与上传量（260.9 vs 245.7 MB）、云/粒子随实时时钟、GUI 动画、服务器
tick、`GlintTime==0` 时的 Stopwatch 时间戳 padding。世界状态跨运行不同，故不能当判据。

**已知局限（3B 前须知）**：`MC_FRAME_LOCAL` 的窗口路径 `arena 0 chunks`，走
`EntityPassRecorder` 的 static-world fallback，因此判据**不覆盖分页 arena 参数页的双缓冲**
（§4 第一项）；它覆盖的是"同一执行器录出的帧是否逐像素一致"以及帧间 A/B 状态切换。
3B 除跑本判据外仍须用连服务器冒烟确认不崩、统计对齐，但**不得**用后者做哈希相等判据。

**状态**：3A 单独提交完毕，判据可用；3B 待做。
### 12.1 多帧哈希判据的正确用法（实测，2026-11-14）

`MC_FRAME_HASHES=N` 会打印前 N 帧颜色目标的哈希。**连服务器的实时世界下该序列跨运行不稳定**（实测两次运行 6 帧全部不同），原因是世界在演化（实体、水流、方块、相机），不是判据本身有问题。

**正确用法：配合 `MC_FRAME_LOCAL=1`（沙盒世界，内容确定）。** 实测两次运行、每次 6 帧，全部哈希相同：

```
b26acc2d0088decf97255c2b3b5af285e152cf2a5a353  (× 两次运行 × 6 帧全部相同)
```

切片 3B 的判据因此是双重的：**同一运行内 N 帧互相相同**（双缓冲若撕裂会立刻不同），**且跨运行相同**。

### 13. 切片 3B 的设计（未实施：2026-11-14 实测否决，见 §14）

**线程划分。** 保留一个录制线程，生命周期与窗口循环相同。主线程每帧：`BeginFrame()` → 采样相机与输入 → 把本帧的 `Frame` 快照交给录制线程 → **继续做下一帧的 prep** → 在 `Submit` 前等待录制线程交回 encoder。

**跨线程共享的状态（只有这些）。**

| 状态 | 归属 | 并发保护 |
| --- | --- | --- |
| `FrameUniformStaging` | 录制线程读写 | **2 槽位**：主线程写槽 A 时录制线程读槽 B，`Advance()` 在两边都完成的边界推进 |
| `DeferredBufferRelease` | 主线程入队、两帧后释放 | 帧号比较，`FramesInFlight=2` 保证被替换的缓冲不会在录制线程仍在用时销毁 |
| `FrameCpuTimings` | 两个线程都写 | 每阶段记录线程归属，输出 `threads=one\|mixed` |
| `FrameTarget` | 主线程持有 | 录制线程只读；resize 必须等待录制线程空闲后才能重建 |

**必须等待的两个点**（`pipeline-wait` 就量在这里）：

1. 提交前：等录制线程交回 encoder。
2. resize：等录制线程空闲，否则目标会在录制中途被销毁。

**降级与开关。** 宿主选项 `MC_PIPELINE_THREAD=1` 开启；关闭时全部在调用线程同步执行，行为与 `a733250` 完全一致。默认关闭，便于 A/B。

**判据（已验证可用，见 §12.1）。**
- 正确性：`MC_FRAME_LOCAL=1` + `MC_FRAME_HASHES=6`，同一运行 6 帧互相同**且**跨运行相同（基线 `b26acc2d0088decf…`）。
- 性能：同进程 A/B，`pipeline-wait` 与 `frame` 是否实降（稳态中位数基线 1.63 ms）。
- **回滚条件**：若 `pipeline-wait` 不显著小于录制时长、或帧时间无实质改善，回滚并在此记录结论。

**已知风险。** 录制线程首次运行会承担管线预热，首帧耗时可能升高（不影响稳态）；`FrameOitTargets` 的 resize 与录制必须互斥。

## 14. 切片 3B 实测结论：否决（2026-11-14）

按 §13 实现前，先测量"把录制移到另一线程"到底能隐藏多少。结论：**在当前构建上不值得实施，否决，不写线程代码。**

### 14.1 实测数据

2026-11-14，Release，`MC_PRESENT_MODE=immediate`、`MC_FRAME_CPU=1`，`MC_FRAME_LOCAL`（demo）与 `MC_FRAME_WATER`（water）两种**确定性**场景，同一进程多窗口 `frame-cpu` 分段（每窗口 60 帧）：

| 分项 | 1400×754 | 2534×1334 |
| --- | --- | --- |
| `FrameExecutor.Record` 全部阶段合计 | 0.08–0.13 ms | ≈0.10 ms |
| `submit`（`sub-finish`+`sub-queue`） | 0.32–0.42 ms | ≈0.35 ms |
| `swap` | 0.28–0.40 ms | 0.28–0.31 ms |
| 整帧 `frame`（稳态中位） | ≈0.9–1.05 ms | ≈0.85–1.05 ms |

（对照：§1 原文的 1.919 ms / 录制 1.28 ms 已过时，见 §1 更正框。）

### 14.2 为什么否决

- **可隐藏上限 ≈ 0.1 ms，约整帧 10%。** 双线程最多只能把录制藏到另一线程，而录制只占 10–13%；"−27%"的前提（录制 1.28 ms）在当前构建已不成立。
- **收益不值代价。** 为 0.1 ms 要付出：常驻录制线程 + 1 帧延迟提交（已超出 §13 的"只换执行线程"）、四类跨线程共享状态，以及下条的真实并发隐患。
- **并发风险（实测发现，非推测）**：`WgpuRenderBackend` 的 `_bufferSizes`（`Dictionary<BufferHandle, long>`）在 `CreateBufferInit`/`Destroy` 中被改写，`_draws`/`_bindGroupSets`/`_pipelineSets` 计数在 `Draw`/`SetPipeline`/`SetBindGroup` 中自增，**均非线程安全**。录制线程与主线程并发调用会产生数据竞争。`MC_FRAME_LOCAL` 的 demo 场景不触发，只是因为它没有 OIT 静态几何（`hasRefStatic=false`），不代表安全。
- §9 的回滚条件本就写着"若 `pipeline-wait` 不显著小于录制时长则回滚"；现在"录制时长"本身只有 0.1 ms——**没有可隐藏的东西**。

### 14.3 结论与复查条件

- **不实施**：不新增 `MC_PIPELINE_THREAD` 开关，不改 `WindowedRenderer`，不引入录制线程。
- 与"实现后回滚"的区别：这是**动手前用测量否决**，比实现后再撤回更省；测量后决定不做，是正确收口，不是"未完成"。
- **复查条件**：当 `FrameExecutor.Record` 的 CPU 合计回升到整帧的 **≥30%**（例如新增重负载 pass、或切到 OIT 全精度/更高分辨率导致录制占比抬升）时，按 §13 重新评估并重测本表。
- 相关并发隐患登记于 `docs/risks.md` R-027。

### 15. 提交与呈现的实测分解（2026-11-14）

视距 4/8/12，每档约 230 个 `n=60` 窗口样本，取中位数：

| 视距 | `drawn` | 整帧 | `sub-finish` | `sub-queue` | `swap` |
| --- | --- | --- | --- | --- | --- |
| 4 | 29 | 1.317 | 0.314 | 0.152 | 0.379 |
| 8 | 90 | 1.623 | 0.388 | 0.169 | 0.395 |
| 12 | 181 | 2.165 | 0.504 | 0.199 | 0.428 |

`sub-finish` 三点拟合极好：**`≈ 0.278 ms + 1.25 µs × drawn`**。

**推论**：`Finish()` 的固定成本约 **0.278 ms**，与绘制数量无关；每 draw 约 1.25 µs。视距 4 时固定部分占 `sub-finish` 的 89%。

**⚠️ 更正（2026-11-14，初版结论有误）**：初版写"3 个 pass，约 0.093 ms/pass，推翻旧的 0.023 ms/pass 记录"——**这是错的**。当时只数了 `FrameExecutor` 的 3 个 `BeginRenderPass`，漏掉了其余文件：

| 文件 | `BeginRenderPass` 数 |
| --- | --- |
| `FrameExecutor.cs` | 3 |
| `ReferenceOitPasses.cs` | **7** |
| `ItemOitSet.cs` | **4** |
| `FramePresentation.cs` | 1 |

实际每帧约 **8–15 个 pass**（视有无半透明/物品透明而定）。0.278 / ~12 ≈ **0.023 ms/pass**——**旧的 0.023 ms/pass 记录是对的**，初版把它当"错 4 倍"是我数错了 pass。

**因此"pass 结构不可合并"的结论也作废**：减少 pass 数才是固定成本的主要杠杆，每省 1 个 pass ≈ 0.023 ms。

### 15.1 三个 pass 与可合并性

| # | 用途 | 附件 | 可合并性 |
| --- | --- | --- | --- |
| 1 | 场景（天空/地形/实体/OIT） | 颜色 `frameView` + 深度 `depthViewHandle` | 基准 |
| 2 | 深度 blit（拷贝主深度到 `oit.CloudDepth`） | **无颜色** + 深度 `oit.CloudDepth` | 写入**不同**深度目标，无法并入 1 或 3 |
| 3 | 文字/云/UI | 颜色 `frameView` + 深度 `depthViewHandle`（均 Keep） | **与 pass 1 附件完全相同，可合并** |

pass 3 与 pass 1 附件一致，之所以分开是因为 **pass 2 必须夹在中间**——云在 pass 3 里绘制，需要 `oit.CloudDepth` 已就绪。

因此**若消除 pass 2，pass 3 可与 pass 1 合并，3 个 pass 降为 1 个**，预计省 2 × 0.093 ≈ **0.19 ms**（视距 4 时约 14%，视距 12 时约 9%）。

消除 pass 2 的路径：让云 pass **直接采样主深度**，而不是先拷贝到 `oit.CloudDepth`。

**可行性结论（2026-11-14 核实）：不可行，3 个 pass 的结构是必需的。**

1. ✅ 主深度**可采样**：`FrameOitTargets.CreateDepthTexture` 的 usage 含 `RenderAttachment | CopySrc | CopyDst | TextureBinding`，注释亦写明"render attachment, a copy source and a sampled …"。
2. ❌ **语义上必须有独立云深度目标**。`FrameExecutor.cs` L561–569 的注释说明：地形半透明（玻璃/水）要用不透明主深度做深度测试，而**云 OIT 的 depth-bounds 会写深度**；若共用同一目标，云的写入会破坏地形半透明的深度判断（会遮挡本应可见的半透明地形）。因此 `oit.CloudDepth` 不是"拷贝的便利"，而是**必需的隔离**。
3. ❌ 云 pass 需要"初始值等于不透明深度、且可写"的深度目标——这恰好只能由一次拷贝得到。去掉 blit 就得改成纯采样读深度，但那样 depth-bounds 的内部面剔除（依赖写深度）会失效，等于改变与参考一致的云 OIT 算法。

**关于 `FrameExecutor` 内这 3 个 pass 的结论**：pass 2 无法消除，pass 3 因此无法并入 pass 1。**但这不意味着固定成本不可降**——大部分 pass 在 `ReferenceOitPasses`（7 个）与 `ItemOitSet`（4 个）里，合并空间在那里。

**外部评审（Gemini Hydrogen，2026-11-14）指出的两个可合并点**，待验证：

1. **OIT 1b（cull）与 1c（cloud bounds）可合并**：`fs_cull` 只用 `textureLoad(depthBoundsTexture)`，**既不读也不写深度附件**；绑定 `p.Depth` 只是管线创建时顺手声明的 `DepthStencilState`。改为绑定 `p.CloudDepth`，同一 pass 内先画全屏三角形写 BoundsCulled，再切管线写云边界，数学与深度结果应逐比特一致。
2. **OIT Composite 与 uiPass 可合并**：两者目标均为 `(view, depthView)` 且均为 Load/Store，之间无采样依赖；把 composite 的全屏三角形作为 uiPass 的第一条指令录制即可。

每合并一个 pass ≈ **0.023 ms**；两个都成则 ≈ **0.046 ms**（视距 12 时约 2%）。验证判据：沙盒帧逐字节不变（`MC_FRAME_LOCAL=1` + `MC_FRAME_HASHES=6`，基线 `b26acc2d…`）。

3. **`blitPass` 可改为 `CopyTextureToTexture`（Hydrogen 指出，我方此前结论有误）**：本项目此前判断"blit 不能消除"——**方向对了一半**：深度隔离确实必需，但**实现方式可以不是 render pass**。主深度与云深度尺寸/格式（`Depth32Float`）一致且均带 `CopySrc | CopyDst`，可直接用 `encoder.CopyTextureToTexture(...)` 做 DMA 拷贝，砍掉整个 render pass（再省 ~0.023 ms）并免去 `blitDepthLayout` / BindGroup 维护。

**小结：三个 pass 合并点合计约 0.07 ms**（1b+1c、composite+ui、blit→copy）。相对整帧 1.3–2.2 ms 约 3–5%。

**可行性分级（2026-11-14 核实）**：

| 合并点 | 需要的 API | 是否已具备 | 工作量 |
| --- | --- | --- | --- |
| 1b cull + 1c cloud bounds | `BeginRenderPass` + `SetPipeline` | ✅ 已有 | 小（改绑定与顺序） |
| composite + uiPass | `BeginRenderPass` + `SetPipeline` | ✅ 已有 | 小 |
| blit → `CopyTextureToTexture` | **`IRenderBackend.CopyTextureToTexture`** | ❌ **不存在**（全仓仅有 `CopyTextureToBuffer`） | **需扩展后端接缝** |

**因此 Hydrogen 的"10 行顺手捡"只对前两项成立。** blit→copy 需要新增后端方法（`IRenderBackend` + `WgpuRenderBackend` + 接缝文档），并先确认两件事：① `Brew.Wgpu` 的 `CommandEncoder` 是否暴露该方法；② WebGPU 是否允许 `Depth32Float` 的纹理到纹理拷贝（深度格式的拷贝受 aspect/格式匹配约束）。

**建议顺序**：先做前两项（低风险、API 现成、沙盒判据现成），把 blit→copy 作为独立的接缝任务评估。

### 15.2 双线程帧间流水线的收益测算（Hydrogen，按本项目实测数据）

把整帧拆为 `T_准备+录制 + T_Finish+Submit+Swap`，用上表实测值：

| 视距 | 整帧 | finish+queue+swap | 剩余 CPU（准备/遍历/录制） | 理论稳态周期 max(A,B) | 预期净收益 |
| --- | --- | --- | --- | --- | --- |
| 4 | 1.317 | 0.845 | 0.472 | ~0.95–1.05 | −0.25~0.35 ms（−20~25%） |
| 8 | 1.623 | 0.952 | 0.671 | ~1.10–1.20 | −0.42~0.52 ms（−26~32%） |
| 12 | 2.165 | 1.131 | 1.034 | ~1.35–1.48 | −0.68~0.80 ms（−31~37%） |

**与 T-837 切片 3B 的对比**：3B 只把"录制"搬到另一线程（录制仅 0.08–0.13 ms），故上限 ~10%；这里搬的是**准备 + 录制**整段（0.47–1.03 ms），量级差一个数量级。**这两件事不是同一个优化**，此前把 3B 的结论套到流水线上是错的。

**前提与代价**：① `frame-gap ≈ 0.001 ms` 只证明 CPU 未等 GPU，不阻止双线程流水线；② 增加 1 帧输入延迟（600+ FPS 下约 1.4 ms）；③ **线程 A 必须零 wgpu 调用**（避免撞 wgpu-core 的 Device 锁），只产出双缓冲的 C# 数组与待销毁句柄列表；所有 `WriteBuffer`/录制/`Finish`/`Submit`/`Swap`/延迟 `Dispose` 归线程 B。

**状态**：仅为测算，未立项。若采纳，需按 §13 的设计（`FrameUniformStaging` 双槽、`DeferredBufferRelease`）扩展为"准备/提交"两段，而不是 3B 的"只换录制线程"。
