<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

<!-- doc
title: 硬约束与后续行动
summary: 根据外部硬约束蓝图、全量审查稿与当前树/装备层进度整理的下一步执行清单
status: 已评审（排期文档）
updated: 2026-11-07
-->

# 硬约束与后续行动

本文把两份外部审查稿（原文见 [`external/`](external/README.md)）与当前分支状态对齐，作为下一阶段的排期依据。外部稿中的旧行号、旧协议版本和旧“已完成”状态不作为当前事实；以本文和 `docs/STATE.md` 为准。

## 1. 当前基线

- 全部注册实体模型已接入 `Compiled` 树；`EntityModelTreeCoverageTests` 强制全注册表无 flat 定义。
- humanoid / Piglin / 非 baby 装备树已接入模型级 `EntityRenderLayerDefinition`；baby humanoid/Piglin 护甲已独立，其他 baby 装备仍为 merged compiled。
- 模型级层目前采用正确性优先的逐实体 scratch 构建，尚未做按模型分组的性能优化。
- `WindowedRenderer` 与 `OffscreenRenderer` 仍是两套实现。

## 2. 现状对照

| 外部建议 | 当前状态 | 结论 |
| --- | --- | --- |
| 全模型 UV 边界/缩放等价门禁 | `ModelUvInvariantTests` 已存在，但主要扫描 flat `Parts` 与 Elytra 特例 | 部分完成，需扩到 compiled cubes、全部变体和装备层 |
| Z-fighting 自动检测 | 无对应测试 | 未完成 |
| 装备 clearance / 穿模检测 | 无对应测试 | 未完成 |
| 协议包全链路覆盖门禁 | `ProtocolDispatchGateTests` 已存在，按源码引用/allowlist 检查 | 基本完成，严格解码分支覆盖仍可加强 |
| ReferenceVerifier 多 cube/零组/装备树 facts | 仍只默认覆盖少量模型，schema 未扩展 | 未完成 |
| `EntityModelPart` 只能由 baker 构造 | 仍是 public record struct，可直接 `new` / `with` | 未完成 |
| 原版同签名 `CubeListBuilder` / `MeshTransformer` | 已有 `VanillaMeshDefinition` 与集中 `VanillaMeshTransformer.Scale`，但没有同签名构建器/强类型弧度/UvBox | 部分完成 |
| 幼年鞘翅 UV/挂点/潜行偏移 | 当前 `UvWidth/UvHeight/UvDepth`、baby 翼 pivot 与 crouch 已落地并有测试 | 已完成 |
| `OverlayLayers` 不继承 `GrowXyz` | 当前 overlay 构造已改为重新构造 part，未见原缺陷路径 | 代码已收敛，缺专门回归 |
| EntityTracker / VecDelta / PositionPath 三缺陷 | 代码有相关改动，但无独立 T-668 回归门禁 | 待复核 |
| `ModHost` collectible ALC 闭包/流式加载 | `ModHost` 仍有 `RemoveAll` 闭包与 `LoadFromAssemblyPath` | 未完成 |
| `WindowedRenderer` / `OffscreenRenderer` 单轨化 | 两个类仍分别约 2311 / 1588 行，无共享 `WorldRenderPipeline` | 未完成 |
| `InteractiveSession` 拆分 | 仍约 2112 行 | 未完成 |
| `ChunkMesher` 贪心矩形合并 | `RectangleIsUniformlyLit` 路径仍在 | 未完成 |

## 3. 下一阶段任务

### T-744 UV/面积全量扫描（P0，先落地）

扩展 `tests/Sharpcraft.Architecture.Tests/ModelUvInvariantTests.cs`：

1. 遍历全注册模型、全部变体、成人/幼年解析结果与装备树的 `Compiled.Cubes`，检查 UV 边界和面积。
2. 检查幼年/成年共享贴图的 UV 跨度守恒，不仅限于 Elytra。
3. 对已知原版越界（baby llama chest）保留显式、会收缩的基线。

Z-fighting 与 equipment clearance 不混入本任务；先走诊断报告，规则稳定后再升级为门禁。判定依据见 [`model-geometry-invariant-policy.md`](model-geometry-invariant-policy.md)。

验收：不修改模型即可自动扫描；新增违规 compiled cube 立即失败。

### T-745 RendererSingleTrackTests（P1，分两刀）

**原版语义澄清**：原版世界渲染只有一套 `GameRenderer`/`LevelRenderer` 编排；`Screenshot.grab` 直接读取 `gameRenderer.mainRenderTarget()`，不存在第二个离屏世界渲染器。原版确实有多个 `RenderTarget` 与多阶段 Pass（main、entity outline、OIT depth-bounds/transmittance/accumulate 等），但它们是同一条世界管线的内部阶段，不是两套平行实现。因此 T-745 的目标是**一套 Pass 编排 + 两个薄壳**，不是把多 Pass 结构压成单 Pass。

- **T-745a（本刀）**：新增 `WorldRenderPipeline` / `FramePassRecorder` 的共享 pass 顺序契约，两个渲染器引用同一 plan；增加 plan 顺序与 recorder 单元测试。只建立契约，不伪称已完成世界 Pass 迁移。
- **T-745b（后续）**：把两边的世界 Pass 录制实际迁入共享 `WorldRenderPipeline`，保留多 Pass/多 RenderTarget；窗口只负责最终上屏，离屏只负责目标与回读；增加壳文件行数与依赖门禁、窗口/离屏几何一致性测试。
  - `T-745b-1`：`OffscreenRenderer` 的主场景 Pass 已改为 `WorldRenderPipeline.RecordScenePasses`，共享 sky/opaque/cutout/entities/translucent 顺序；
  - `T-745b-2`：`WindowedRenderer` 的主场景 Pass 也已改为 `RecordScenePasses`；静态世界回退保留原顺序；
  - `T-745c`（后续）：OIT、云、WorldText、UI 目前只存在于窗口路径，Offscreen 没有对应实现；它们不算跨渲染器双轨债务。若 Offscreen 将来补齐这些功能，必须复用同一套 PostPass 计划。窗口/离屏主场景的几何/像素一致性验收仍待补。

### T-746 EntityModelPart 类型收紧（P1，部分完成）

- `T-746a`（本刀）：`EntityModelPart` 改为显式 `readonly record struct`，带参数的构造函数为 `internal`；
- 增加 `EntityModelPartEncapsulationTests`：禁止 public 参数化构造，并只允许 `EntityModelMesher`/`EntityModelResolver`/`VanillaMeshBake` 构造 baked part；
- `T-746b`（后续）：清理模型定义中绕过 baker 的几何 `with` 克隆；当前已加 `ModelDefinitionsDoNotGrowNewGeometryCloneSites` 棘轮（3 个已知文件），新增克隆会失败，修复一个必须同步收缩基线；不能把当前部分完成误报为全部完成。

### T-747 ModHost ALC 收口（P1，已实现代码硬化）

- 消除了 `_contexts.RemoveAll` 的闭包捕获，改为反向索引扫描；
- `Unload` 加 `NoInlining`，主程序集改为 `LoadFromStream`；
- `UnloadProbe` 同步改用流式加载；
- R-019 的 GC 回收测量仍受运行时/宿主影响，未将其强行标绿；本任务只保证宿主侧不再主动持有强引用。

### T-748 EquipmentClientInfo（P2，解析层已落地）

- 已新增 `EquipmentClientInfo` 数据模型与 JSON 解析：19 个 `EquipmentLayerType`、texture、dyeable、
  use_player_texture、trim_overrides；
- 新增 `ResourceKind.Equipment`，路径为 `assets/<namespace>/equipment/<path>.json`；
- 已有 `EquipmentClientInfoTests` 覆盖 iron/leather/saddle/elytra 形状与非法输入；
- 尚未把解析结果接入 `EquipmentTextureCatalog`/渲染状态；静态 catalog 仍是当前绘制来源，后续需要 item→equipment asset
  绑定与真实 ResourceManager 加载。

### T-749 EntityTracker T-668 复核（P1）

- `StepInterpolation` 同帧双重 Advance；
- `TryReadVecDelta` 多步正负抵消；
- `PositionPath.Stepped` 时长计算；
- 补最小复现与回归用例后再判定是否修复。

## 4. 执行约束

- 先补原版 facts 与门禁，再修改生产行为；
- 不用 `LegacyModelPartFactory` 作为 Golden；
- 不把外部审查稿中的旧协议 775、旧行号或旧状态直接当作当前事实；
- 每项一个 commit，文档与代码同提交。
