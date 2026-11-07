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
| 全模型 UV 边界/缩放等价门禁 | `ModelUvInvariantTests` 已扩到 compiled cubes、全注册类型、成人/幼年和 resolved equipment trees | 基本完成，Z-fighting/clearance 仍单列 |
| Z-fighting 自动检测 | 无对应测试，按策略先做诊断 | 未完成 |
| 装备 clearance / 穿模检测 | 无对应测试，按策略先做诊断 | 未完成 |
| 协议包全链路覆盖门禁 | `ProtocolDispatchGateTests` 已存在，按源码引用/allowlist 检查 | 基本完成，严格解码分支覆盖仍可加强 |
| ReferenceVerifier 多 cube/零组/装备树 facts | 仍只默认覆盖少量模型，schema 未扩展 | 未完成 |
| `EntityModelPart` 只能由 baker 构造 | 参数化构造已 internal；`With` 收口完成，`with` 仅集中 transformer 保留 | 已完成（T-746） |
| 原版同签名 `CubeListBuilder` / `MeshTransformer` | 已有 `VanillaMeshDefinition` 与集中 `VanillaMeshTransformer.Scale`，但没有同签名构建器/强类型弧度/UvBox | 部分完成 |
| 幼年鞘翅 UV/挂点/潜行偏移 | 当前 `UvWidth/UvHeight/UvDepth`、baby 翼 pivot 与 crouch 已落地并有测试 | 已完成 |
| `OverlayLayers` 不继承 `GrowXyz` | 当前 overlay 构造已改为重新构造 part，未见原缺陷路径 | 代码已收敛，缺专门回归 |
| EntityTracker / VecDelta / PositionPath 三缺陷 | T-749 已补正负 step、逐 step、ticks 回归，当前实现通过 | 已完成复核 |
| `ModHost` collectible ALC 闭包/流式加载 | 已去闭包、流式加载、`NoInlining`；R-019 GC 回收探针仍未稳定转绿 | 代码已硬化，R-019 独立风险 |
| `WindowedRenderer` / `OffscreenRenderer` 单轨化 | 主场景 Pass 已共用 `RecordScenePasses`；OIT/云/WorldText/UI 仍窗口独有，完整壳抽取未完成 | 部分完成（T-745） |
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

### T-746 EntityModelPart 类型收紧（P1，已完成）

- `T-746a`（本刀）：`EntityModelPart` 改为显式 `readonly record struct`，带参数的构造函数为 `internal`；
- 增加 `EntityModelPartEncapsulationTests`：禁止 public 参数化构造，并只允许 `EntityModelMesher`/`EntityModelResolver`/`VanillaMeshBake` 构造 baked part；
- `T-746b`（已完成）：模型定义中的 `part/source with` 已改 `EntityModelPart.With(...)`；helper、mesher 的 `part with` 也已清理，整个 Render 目录仅 `VanillaMeshBake.cs` 的集中 bake 保留 `with`；架构测试禁止其它位置新增 baked-part 克隆。

### T-747 ModHost ALC 收口（P1，已实现代码硬化）

- 消除了 `_contexts.RemoveAll` 的闭包捕获，改为反向索引扫描；
- `Unload` 加 `NoInlining`，主程序集改为 `LoadFromStream`；
- `UnloadProbe` 同步改用流式加载；
- R-019 的 GC 回收测量仍受运行时/宿主影响，未将其强行标绿；本任务只保证宿主侧不再主动持有强引用。

### T-748 EquipmentClientInfo（P2，解析层已落地）

- 已新增 `EquipmentClientInfo` 数据模型与 JSON 解析：19 个 `EquipmentLayerType`、texture、dyeable、
  use_player_texture、trim_overrides；
- 新增 `ResourceKind.Equipment`，路径为 `assets/<namespace>/equipment/<path>.json`；
- 新增 `EquipmentAssetRegistry`，`EquipmentTextureCatalog.LoadAssets` 从资源栈加载已知资产，`Resolve` 优先使用
  解析出的层纹理，静态 catalog 作为 fallback；
- `equippable` 组件现在保留 `EquippableAssetId`，经 `EntityTracker` 传入 `EntityRenderState` 和 texture selector；
- `EquipmentTexture` 已携带 `UsePlayerTexture` 与 `ColourWhenUndyed` 元数据；
- `EquipmentClientInfo.ResolveTrim` 已按 material/pattern 选择第一条匹配的 trim override；
- `dyed_color` 组件已进入 `ItemComponentInfo`/`EntityTracker`/`EntityRenderState`，`ResolveEquipmentTint` 将染料颜色转成 `EntityModelCube` 的 tint selector；
- `enchantment_glint_override` 已解析并透传到 `EquipmentTexture.Glint`；已用独立装备层加载 `misc/enchanted_glint_armor` 做 glint overlay，但专用 glint shader/pass 仍未完成；
- 已有 `EquipmentClientInfoTests` 覆盖 iron/leather/saddle/elytra 形状、非法输入、asset-driven resolution、自定义 asset id、dye/player-texture 元数据与 trim predicate；
- T-748d：资产纹理路径已进入 atlas —— `EntitySkinLibrary.Load` 遍历 `EquipmentTextureCatalog.Assets.TexturePaths` 并复用 `LoadTexture` 的按路径去重，`EquipmentClientInfoTests` 补资产层纹理路径断言；
- T-748i：`EntityModelResolver.ResolveEquipmentTextureInfo` 暴露完整 `EquipmentTexture`（primary/overlay/dye/glint），`ResolveEquipmentTexture` 改为薄委托选 Primary/Overlay、行为不变；`EquipmentClientInfoTests` 补 dyeable 元数据与委托一致性断言；
- 尚未完成 dye/glint/player-texture 的最终着色与 trim 纹理/palette 的实际渲染。

### T-749 EntityTracker T-668 复核（P1，已完成回归核验）

- 已补 `ASteppedDeltaKeepsOppositeSignedSteps`：正负 fixed-point delta 不被净和吞掉；
- 已补 `SteppedMoveDoesNotCollapseOppositeSignedDeltas`：`ApplyMove` 逐 step 走完正负位移；
- 已补 `SteppedMoveConsumesExactlyItsTickSpan`：step 的 ticks 只消耗一次；
- 当前实现通过三组回归，未发现 T-668 所述的生产缺陷；保留测试作为防回归。

### T-751 Entity layer 性能分组（P2，已完成）

- `EntityModelMesher.BuildModelBackedLayers` 从「每层每实体一次 `BuildInto` + `AppendGeometry`」改为按有效层模型分组，同组实体一次 `BuildInto`、一次 Append；
- 组只合并**连续相邻**的同模型实体：原版按实体遍历顺序提交，跨实体重新分组会改变层内顶点顺序，对 translucent 的稳定排序/混合不保证等效；因此交错模型不会被跨组重排；
- 组键为 `entityLayer.Model ?? model` 加 `AllCubes` 与 `TextureSelector`；`VisibleWhen` 仍按实体逐个过滤，plan 名称/kind/order 与返回层顺序不变，`AllCubes`/emissive/modelOverride/纹理 selector/动画 pose 语义不变；
- 为让紧凑分组 span 仍能取到原批次下标的 pose，`BuildInto` 增加可选 `poses` 列表；plan 字典、组记录、实体与 pose 列表全是复用字段并就地清空，稳态帧不产生每层分配；
- `EntityLayerTests` 增加多实体同模型几何计数/层顺序断言、Interleaved submission order 断言，并覆盖 `VisibleWhen` 过滤与混合模型分组；既有层顺序/装备层测试保持全绿。

## 4. 执行约束

- 先补原版 facts 与门禁，再修改生产行为；
- 不用 `LegacyModelPartFactory` 作为 Golden；
- 不把外部审查稿中的旧协议 775、旧行号或旧状态直接当作当前事实；
- 每项一个 commit，文档与代码同提交。
