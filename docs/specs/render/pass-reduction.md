<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

# RenderPass 合并分析：能不能少开几个 pass

**状态**：`[已评审]`　**任务**：T-834　**依据**：`frame-baseline.md` §5

## 1. 为什么值得分析

实测每个 pass ≈ **0.023 ms** 的 `CommandEncoder.Finish()` 成本，且**与该 pass 画多少东西无关**。当前一帧约 11 个 pass，`sub-finish` 是整帧最大单项（0.38–0.48 ms，24%）。

遮挡剔除砍掉 78% 三角形却没有帧时间收益，正是因为这条成本只跟 pass 数有关。

## 2. 当前 pass 清单

| # | pass | 位置 | 目标 | 深度附件 | 可跳过条件 |
| --- | --- | --- | --- | --- | --- |
| 1 | 主场景（天空/地形/切图/半透明） | `WindowedRenderer` | 帧目标 | `Depth` | — |
| 2 | 深度 blit | `WindowedRenderer` L1540 | 帧目标 | — | — |
| 3 | OIT 1a 深度边界 | `ReferenceOitPasses` L138 | `Bounds`（MAX 混合） | `Depth` | `MC_SKIP_OIT` |
| 4 | OIT 1b 不透明深度剔除 | L161 | `BoundsCulled`（Clear） | `Depth` | 同上 |
| 5 | OIT 1c 云深度边界 | L180 | `BoundsCulled`（Keep） | **`CloudDepth`** | 同上 + 无云 |
| 6 | OIT 2 小波透射率 | L204 | `Transmittance0/1`（加性×2） | — | 同上 |
| 7 | OIT 3 累积 | L255 | `Accumulate` | — | 同上 |
| 8 | OIT 合成 | `ReferenceOitPasses` | 帧目标 | `Depth` | 同上 |
| 9–12 | 物品 OIT 四阶段 | `ItemOitSet` | 同上 | — | `ItemsHasGeometry`（**已实现**） |
| 13 | UI | `WindowedRenderer` L1723 | 帧目标 | `Depth` | — |

## 3. 为什么大多数 pass 不能合并

### 3.1 数据依赖链（硬约束）

```
1a Bounds ──读──► 1b Cull ──写──► BoundsCulled ──读──► 2 透射率 ──读──► 3 累积 ──读──► 合成
```

每个阶段都**读上一个阶段的输出**。同一个 encoder 内，一个 pass 不能既写某纹理又采样它（write-after-read 在同一 pass 内非法），所以这条链**无法合并**——除非改算法。

### 3.2 1b 与 1c 深度附件不同（本次核查的具体发现）

| 阶段 | 目标 | LoadOp | 深度附件 |
| --- | --- | --- | --- |
| 1b 剔除 | `BoundsCulled` | `Clear` | `Depth` |
| 1c 云边界 | `BoundsCulled` | `Keep` | **`CloudDepth`** |

目标看起来相同、深度附件也确实不同（云用独立深度）。但**可以合并**，因为剔除 pass 根本不使用深度：`CreateCullPipeline` 声明的是 `CompareFunction.Always` 加不写深度，其片元阶段只 `textureLoad` 边界纹理。它被绑 `p.Depth` 纯属创建管线时的顺手声明。\n\n**已实施**：合并后该 pass 绑 `p.CloudDepth`，先画剔除全屏三角，再切管线画云边界，两者深度语义均不受影响。省 1 个 pass（≈0.023 ms）。

## 4. 结论

**OIT 组的 8 个 pass 是参考算法的固有结构，不是实现浪费。** 在不改变算法（例如改成单 pass 加权混合 WBOIT）的前提下，这块的 `Finish()` 成本降不下来。

因此 T-834 的结论是 **保持**：不在 pass 数上做文章，把方向转到别处。

## 5. 若日后仍要减 pass，可行方向（均需独立评审）

1. **换成 WBOIT 单 pass**：放弃参考 OIT 的精确排序语义，换取 pass 数从 8 降到 1。代价是视觉偏差，按原则 9 需进受控偏差清单。
2. **合并 pass 1 与 pass 2**：深度 blit 若能与主场景末尾共用目标，可省 1 个（约 0.023 ms），但主场景深度仍需先完成写入。
3. **物品 OIT 的跳过条件已实现**，无进一步空间。

## 6. 已排除的方向

- ❌ 减 draw 数：`Finish()` 与 draw 数无关，只与 pass 数成正比（实测遮挡剔除 −78% 三角形无收益）
- ❌ 校验层：`InstanceFlags.None`，本就关闭
- ❌ 切段增加命令数：实测仅 +0.054 ms
- ❌ 云边界与剔除合一：深度附件冲突，见 §3.2
