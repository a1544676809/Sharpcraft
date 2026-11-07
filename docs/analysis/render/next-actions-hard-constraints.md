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

### T-744 ModelGeometryInvariantTests（P0）

新增 `tests/Sharpcraft.Architecture.Tests/ModelGeometryInvariantTests.cs`：

1. 遍历全注册模型、全部变体、全部装备组合与全部 `Compiled.Cubes`，检查 UV 边界/面积。
2. 检查幼年/成年共享贴图的 UV 跨度守恒，不仅限于 Elytra。
3. 扫描共面反法线三角形，检测 Z-fighting。
4. 扫描装备层与基础身体/头部的静止姿态 clearance。
5. 对合法覆盖层使用显式、会收缩的基线，禁止无限白名单。

验收：不修改模型即可自动扫描；新增违规模型或装备层立即失败。

### T-745 RendererSingleTrackTests（P1）

- 抽出 `WorldRenderPipeline` / `FramePassRecorder`，让 `WindowedRenderer` 与 `OffscreenRenderer` 只保留窗口/离屏壳职责。
- 增加架构测试限制两个壳文件的最大行数与依赖边界。
- 保留同一相机、同一几何、同一 pass 顺序的像素/几何一致性测试。

### T-746 EntityModelPart 类型收紧（P1）

- 将 `EntityModelPart` 构造函数改为 `internal`；
- 禁止在 `VanillaMeshTransformer` / 集中 baker 之外 `new EntityModelPart` 或直接改几何 `with`；
- 增加架构测试扫描生产代码。

### T-747 ModHost ALC 收口（P1）

- 消除 `_contexts.RemoveAll` 闭包捕获；
- 使用流式加载/非同名程序集验证 collectible ALC 回收；
- `UnloadProbe` 保持为仓库外测试负载，不进入生产路径。

### T-748 EquipmentClientInfo（P2）

- 19 个 LayerType、每层 texture/dyeable/use_player_texture；
- trim_overrides、glint、玩家披风/鞘翅纹理；
- 静态 `EquipmentTextureCatalog` 退化为 fallback。

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
