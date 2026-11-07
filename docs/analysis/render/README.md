<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

# render

本目录的文档索引由 `tools/Sharpcraft.Docs.Indexer` 生成；手写内容放在标记之外。

<!-- BEGIN GENERATED: docs/analysis/render -->

| 文档 | 摘要 | 状态 | 最后更新 |
| --- | --- | --- | --- |
| [`entity-animation-architecture.md`](entity-animation-architecture.md) | 现有动画 switch/共享 RenderState 的瓶颈，provider + 每生物状态上下文的组织方式与效率判断 | 已评审 | 2026-11-03 |
| [`entity-animation.md`](entity-animation.md) | 26.3 的 AnimationDefinition / KeyframeAnimation / ModelPart 姿态覆盖语义，以及本项目现状 | 草稿 | 2026-11-03 |
| [`entity-coordinate-system.md`](entity-coordinate-system.md) | 地形/相机/实体状态/模型空间的坐标约定、当前已统一与未统一的部分 | 草稿 | 2026-11-07 |
| [`entity-equipment.md`](entity-equipment.md) | set_equipment 包、装备槽、物品栈编码、装备资产层与各实体装备层的实现边界 | 已评审 | 2026-11-03 |
| [`entity-model-approximations.md`](entity-model-approximations.md) | 已完成实体模型中仍需父子层级变换才能精确等价的部分 | 已评审 | 2026-11-02 |
| [`entity-model-geometry-facts.md`](entity-model-geometry-facts.md) | 26.3 ModelPart/PartPose/CubeDeformation/MeshTransformer 的已核实行为事实，以及对本项目 DSL 与门禁的约束 | 已评审 | 2026-11-07 |
| [`entity-model-migration.md`](entity-model-migration.md) | 原版生物模型来源、按复杂度排序的移植顺序、当前进度与已知缺口 | 已评审 | 2026-11-02 |
| [`entity-model-parity-gap.md`](entity-model-parity-gap.md) | 实体模型定义、烘焙、运行时、动画与装备/附加层相对 Minecraft 26.3 的完整差距清单 | 已评审 | 2026-11-07 |
| [`entity-render-backlog.md`](entity-render-backlog.md) | 装备层之后的实体渲染欠账：动画、装备特例、资产与验收顺序 | 草稿 | 2026-11-03 |
| [`entity-render-layers.md`](entity-render-layers.md) | 合并 part 的简化实现与原版独立 RenderLayer 的差异、影响和修复契约 | 已评审 | 2026-11-02 |
| [`entity-special-layers.md`](entity-special-layers.md) | 不属于普通 base/overlay cube 的实体渲染层清单，以及后续补充顺序 | 已评审 | 2026-11-02 |
| [`entity-variants.md`](entity-variants.md) | 26.3 变体的三条来源、实体数据访问器索引表、注册表驱动解析、几何变体差异与首批范围 | 分析已定稿（依据 26.3 源码） | 2026-11-03 |
| [`lighting-color-space.md`](lighting-color-space.md) | 26.3 block 着色器全程 sRGB 域直接相乘；据此修正我们误加的 gamma 解码/编码 | 草稿 | 2026-11-01 |
| [`offscreen-ui-pass.md`](offscreen-ui-pass.md) | T-104 / T-159 的行为规格：离屏路径补上界面 pass，并用已知颜色反查 gamma 与通道顺序 | 草稿 | - |
| [`shader-organisation.md`](shader-organisation.md) | 原版 93 个着色器条目的组织规范；我们当前的内联状态与改造建议 | 草稿 | - |
| [`shader-pack-anatomy.md`](shader-pack-anatomy.md) | 真实 Iris 包的文件布局、阶段清单、包装层与共享翻译单元、选项系统；全部为行为级描述 | 草稿 | - |
| [`sky-and-fog.md`](sky-and-fog.md) | 26.3 的属性驱动雾与天空盘常量；我们的等效实现路径与验证方式 | 草稿 | - |
| [`transparent-blocks.md`](transparent-blocks.md) | 本文件只描述可观测行为与判定规则，证据以 ref:// 指针引用仓库外分析工作区。 | 未标注 | - |
| [`upscaling-and-raytracing.md`](upscaling-and-raytracing.md) | 逐项核实 DLSS / XeSS / FSR / 硬件光追的可用性、依据，以及无论选谁都必须先具备的管线输入 | 草稿 | - |
| [`vanilla-equivalence-check.md`](vanilla-equivalence-check.md) | 逐项把我们的实现与原版参考对照。 | 未标注 | - |
| [`world-space-text.md`](world-space-text.md) | 原版世界空间文字（名字牌、告示牌、文本展示实体等）的提交契约、坐标、字体、背景、朝向与光照语义 | 草稿 | 2026-11-02 |

<!-- END GENERATED: docs/analysis/render -->
