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

## 6. 与其它任务的关系

- **T-837 切片 3（录制线程）依赖本项**：线程化改的是 `WindowedRenderer`，而该路径缺乏像素级判据。先收敛，才有一条能被像素验证的主路径。
- 架构棘轮 `RendererSingleTrackTests` 已限制两个文件的行数上限（2400 / 1700）；本项应使其**下降**而非上升。
