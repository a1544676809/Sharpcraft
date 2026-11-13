<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

# 单轨渲染执行器（消除窗口/离屏双轨）

> 状态：`[已评审]`　更新：2026-11-13
> 关联：AGENTS.md 原则 14（单一执行主路径）、原则 3.7；`docs/analysis/render/external/sharpcraft-hard-constraints-and-workflow-blueprint.md` §3 与"第四步"

## 1. 问题

原则 14 要求"离屏与窗口渲染 MUST 共用同一套帧 Pass 执行器（窗口仅作最终上屏呈现）"。当前实际是**两套完整且互不引用的实现**。

实测（2026-11-13，本仓）：

| | `OffscreenRenderer.cs`（1533 行） | `WindowedRenderer.cs`（2070 行） |
| --- | --- | --- |
| `BeginRenderPass` | **1** | **3** |
| 管线创建方法 | **5** | **15** |
| 引用 `TerrainPassRecorder` 等组件 | **0 处** | 全部 |

`OffscreenRenderer` 只建了天空、地形、半透明、cutout、位置色 5 条管线；`WindowedRenderer` 另外还有 OIT 四段（深度包围 / 透射率 / 累加 / 剔除）、blit-depth、合成、实体 5 条（普通 / cutout / 鞘翅 / 自发光 / 附魔光效）与物品 cutout。

## 2. 造成的实际损害

1. **窗口侧改动无法验证。** 离线 `--frame` 与沙盒 `MC_FRAME_LOCAL` 都只走 `OffscreenRenderer`，其哈希对 `WindowedRenderer` 的任何改动**都不敏感**。本会话给 9 个组件抽取跑的"离线帧哈希不变"因此是无效判据（见 `docs/STATE.md` 的判据覆盖表）。
2. **同一份画面逻辑要改两遍**，且容易只改一处。已出现的实例：雾 uniform 曾由两条路径各自填充。
3. **离屏产物与窗口产物本就不一致**（离屏不画实体、不画 OIT），使"截图为窗口所见"这一前提不成立。

## 3. 目标

一条 `FrameExecutor`，接收"渲染目标 + 特性开关"，录制整帧；两条路径只负责目标与呈现方式：

```text
FrameExecutor.Record(target, request)   →  command encoder
  window  : target = surface texture, 然后 present
  offscreen: target = 回读纹理, 然后 copy-to-buffer
```

**验收判据**：同一 `FrameRequest` 下，离屏读回的像素与窗口截屏的像素**逐像素相同**（容许明确登记的差异项）。

## 4. 关键难点（必须先解决，否则收敛会静默改变画面）

离屏当前**少画**实体、OIT、云、UI。收敛后它会开始画这些，因此：

- 所有现存像素基线（`docs/specs/render/` 下的期望帧、测试中的颜色断言）**都会改变**；
- 必须先在规格层面确认"离屏应当包含哪些特性"，而不是让实现顺手决定。

**先抽执行器，再切换目标（2026-11-13 修订，取代初版建议）**。

初版建议"先补齐离屏特性、再抽执行器"是**错的**：补齐等于把实体、UI、OIT 各写两遍，正是原则 14 禁止的重复实现，且每补一项都在加深双轨。

正确顺序：

```text
1. 从 WindowedRenderer 抽出整帧录制 → 参数化目标（surface / texture）
2. WindowedRenderer 调用它（行为不变）
3. OffscreenRenderer 改为：建纹理 → 调同一执行器 → 回读
4. 删除 OffscreenRenderer 的重复管线与录制代码
```

切换时离屏一次性获得实体、UI、OIT——因为它开始使用游戏本体的代码。代价是这一步的画面变化无法逐项隔离，但判据更硬：**同一请求下离屏回读像素与窗口截图逐像素相同**。

## 5. 切片

1. **特性差异清单**（已完成，2026-11-13 实测）：

   | 特性 | `OffscreenRenderer` 引用数 | `WindowedRenderer` 引用数 | 结论 |
   | --- | --- | --- | --- |
   | 实体（`EntityGeometryLayer`） | **0** | 3 | 离屏**不画实体** |
   | UI（`uiPass`） | **0** | 14 | 离屏**不画 UI** |
   | OIT（深度包围/透射/累加/剔除） | **0** | 2 | 离屏**无 OIT** |
   | 半透明 | 4 | 3 | 两者都有 |
   | 天体 | 2 | 1 | 两者都有 |

   **结论：离线 `--frame` 与沙盒画面里从来没有过实体和 UI。** 这不只是"帧里地形少"，而是整层缺失——任何依赖离线帧验证实体或 UI 的尝试都无效。
2. **逐项补齐**：每项一个提交，沙盒对拍确认该特性出现且只影响预期区域。

   **进展与障碍（2026-11-13）**：

   - 实体管线工厂已抽为共享的 `FramePipelines`（`WindowedRenderer` 1975 行）。`EntityPassRecorder` 本就按参数接收管线，可直接复用。
   - **真正障碍**：`FrameRequest` **不含任何实体数据**——实体在窗口路径由 `entityLayers` 回调提供，而 `OffscreenRenderer.Render(FrameRequest)` 是请求的纯函数。
   - 因此下一步是**扩展 `FrameRequest` 承载实体层与皮肤/物品图集**，再由 `FrameCommand` 填充、`OffscreenRenderer` 消费。这是契约变更，应单独提交并在 `docs/specs/render/` 记录新字段。

   **更深一层的障碍（同日核实）**：离线路径**没有实体数据源**。`ClientWorld` 不含实体集合（0 处），`FrameCommand` 里的 28 处 Entity 全部是 `BlockEntityPlaceholderMesher`（方块实体占位，非活动实体）。因此"让离屏画实体"不只是契约问题，还需要**宿主侧为离线路径采集实体**，或改为只在窗口路径验证收敛。

   **对本切片顺序的影响**：实体项的工作量远超估计。可先做**不需要新数据源**的部分（如 OIT：其输入 `TranslucentVertices/Indices` 已在请求中），把实体项后移。
3. **抽取执行器**：此时两条路径画的东西相同，抽公共录制单元，判据是两侧像素**都不变**。
4. **切换离屏目标**：`OffscreenRenderer` 变为"建纹理 → 调执行器 → 回读"。
5. 删除 `OffscreenRenderer` 中重复的管线创建与录制代码。

切片 3 之前，两侧输出必须已经一致；否则该切片无法用"像素不变"验收。

### 切片 1（§4 第 1 步）完成时的接缝形态与第 2 步清理清单（2026-11-13）

> 本节记录的是切片 1 当时的过渡形态；其中 4 个委托已在 2A 消除，见下节。

**已完成**：`WindowedRenderer.Run` 的整帧录制（原 L1089–L1503）原样搬入
`src/Sharpcraft.Framework/Rendering/FrameExecutor.cs` 的 `FrameExecutor.Record`；渲染目标由新增的
`FrameTarget(Colour, Depth, Width, Height)` 传入，窗口侧只保留目标获取（`FramePresentation.Ensure`）、
`RecordPresent`、`Submit` 与截图。相机采样（含 `cameraPerFrame(window)`）、`WindowHost` 与 surface 仍在窗口侧。
`WindowedRenderer.cs` **1975 → 1663 行**，新增 `FrameExecutor.cs` **787 行**，`OffscreenRenderer.cs` 未动（1533 行）。

**接缝形态不是干净的"目标 + 请求"**：`Record` 除 `FrameTarget` 外，还通过 `FrameExecutor.Resources` 的
**4 个委托**读取仍属于 `WindowedRenderer` 的可变局部量（闭包式耦合）。这是为了本次抽取行为不变的**过渡形态**，
不是抽象边界；第 2 步（`OffscreenRenderer` 改用同一执行器）开始前必须把它们换成执行器自己拥有的状态。

| 委托 | 读 / 改的窗口侧状态 | 用途 |
| --- | --- | --- |
| `ReferenceTargetsReady` | `refTargetsCreated`（bool） | 判断尺寸相关的 OIT 目标是否已建 |
| `CreateReferenceTargets` | 局部函数 `CreateReferenceTargets()`：按 `frameWidth/frameHeight` 建 `refDepthBounds*`、`refTransmittance0/1`、`refAccumulate` 及其绑定组、`cloudDepthTexture/View`，调 `itemOit.Create(...)`，并置 `refTargetsCreated` 与 `cloudDepthTargetsCreated` | 首个需要透明的帧或 resize 时重建目标 |
| `BlitDepthGroup` | 局部函数 `BlitDepthGroup()`：`depthViewHandle`、`blitDepthBindGroup`、`blitDepthBoundFor`、`blitDepthRebuilds` | 深度视图变化时惰性重建 blit-depth 绑定组 |
| `GetReferenceTargets` | 快照 `refDepthBoundsView`、`refDepthBoundsCulledView`、`refTransmittance0/1View`、`refAccumulateView`、`refCullBindGroup`、`refTransmittanceBindGroup`、`refAccumulateBindGroup`、`refCompositeBindGroup`、`cloudDepthViewHandle`、`cloudDepthTargetsCreated` | 录制中读当前 OIT 句柄；必须在 `CreateReferenceTargets` 之后取，故用委托而非一次性快照 |

**第 2 步清理清单**：① 把上述目标与绑定组移进执行器自有对象，尺寸变化由执行器自己重建，删除 4 个委托；
② 相机与 `eye/target` 改由请求提供，不再经窗口回调；③ 之后 `OffscreenRenderer` 才能只做"建纹理 → 调
`Record` → 回读"。在此之前，执行器的真实依赖面大于 `FrameTarget(Colour, Depth, Width, Height)`。

**判据**：离线 `--frame` 哈希对 `WindowedRenderer` 的改动不敏感（只走 `OffscreenRenderer`），本次以
"构建 0 警告 + 2718 测试全绿 + ClassMap/Docs 全绿 + Release 运行时冒烟（cull / frame-cpu 正常、无崩溃）"验收，
不用帧哈希。

### 2A 完成：4 个委托已消除（2026-11-13）

按上节清理清单①，`ReferenceTargetsReady` / `CreateReferenceTargets` / `BlitDepthGroup` /
`GetReferenceTargets` 四个委托全部删除。尺寸相关的 OIT 纹理、视图、绑定组、云深度目标、blit-depth
绑定组缓存与 `blitDepthRebuilds` 计数移入新文件
`src/Sharpcraft.Framework/Rendering/FrameOitTargets.cs`，经 `FrameExecutor.Resources.OitTargets`
在构造时注入执行器。窗口侧只保留深度纹理 / 视图的重建，resize 时调 `FrameOitTargets.Resize`；
`FrameTarget.Depth` 是唯一与深度相关的每帧输入，`BlitDepthGroup(depthView)` 以它为缓存键。
`WindowedRenderer.cs` **1663 → 1469 行**（另删切片 1 遗留的死 `cameraBlock` stackalloc），
新增 `FrameOitTargets.cs` **309 行**，`FrameExecutor.cs` **787 → 741 行**，`OffscreenRenderer.cs` 未动（1533 行）。

**判据（窗口路径行为不变）**——按论证顺序记录，避免下一会话误判为"没查清楚"：

1. **主要论据：变慢是"全阶段同倍 ~1.5×"，且集中在 2A 完全没有改动的阶段。** 第 4 次样本里
   `scene-terrain 0.163→0.239`、`oit 0.187→0.259`、`cel-build 0.063→0.097`、`submit 0.587→0.898`
   同步 ~1.4–1.5× 变慢，这四个阶段 2A 一行未动；同时 swapchain / GPU 侧没有相应放大：
   `swap 0.198` 反而低于基线 `0.352`，`acquire 0.169` 仅比基线 `0.034` 高 0.13 ms（相比第 3 次
   争用尖峰的 `8.786 ms` 低 50 倍）。**纯状态归属搬移不可能让四个互不相关的 CPU 阶段同步慢 1.5 倍、
   却恰好让 swap 变快**——这个分布只能说明慢在整台机器（采样时 DeepSeek Harness / msedge / QQ
   持续占核、CPU 保持 2300 MHz 基频无睿频），不在任何一段代码里。
2. 静态验证全绿：build 0 警告 0 错误、2718 测试全绿、ClassMap / Docs / 两 verify 全绿。
   四次 Release 冒烟（1400×754、视距 12、30 s）的 `cull: chunks=637 culled=449 … drawn=183`、
   `blit bind group rebuilt 1 times`、EXIT=0、无 stderr 异常**逐项一致**；overall frame 依次
   **1.921 / 2.027 / 15.873 / 2.704 ms**（第 3 次是 `acquire=8.786` 的 swapchain 争用尖峰，
   第 4 次中位数 2.409 ms）。
3. 稳态判据（丢弃前 20 个 n=60 窗口后中位数 ≤2.2 ms）第 1 次通过（1.680），第 4 次未通过（2.409）。
   按裁决**不重复试错、接受并如实记录**：第 2、3、4 次代码完全相同，差异只可能来自环境；
   结合第 1 条判定纯搬移未改变行为。异常数据一并进入提交信息，未隐藏、未放行。

**剩余**：清理清单②（相机 `eye/target` 仍由窗口回调采样后传入，未改由请求提供）与
③（`OffscreenRenderer` 切到同一执行器）属 2B 范围。2A 提交后暂停，2B 需要干净基线。

## 6. 与其它任务的关系

- **T-837 切片 3（录制线程）依赖本项**：线程化改的是 `WindowedRenderer`，而该路径缺乏像素级判据。先收敛，才有一条能被像素验证的主路径。
- 架构棘轮 `RendererSingleTrackTests` 已限制两个文件的行数上限（2400 / 1700）；本项应使其**下降**而非上升。
