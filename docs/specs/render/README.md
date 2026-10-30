<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

# render

本目录的文档索引由 `tools/Sharpcraft.Docs.Indexer` 生成；手写内容放在标记之外。

<!-- BEGIN GENERATED: docs/specs/render -->

| 文档 | 摘要 | 状态 | 最后更新 |
| --- | --- | --- | --- |
| [`backend-interface.md`](backend-interface.md) | 能力协商、阶段模型、禁止泄漏规则；WGPU 为唯一实现，Vulkan 仅预留 | 草稿 | - |
| [`block-layer-and-tint.md`](block-layer-and-tint.md) | 真实服务器目视验收暴露的两项缺口（树叶黑斑、草色不对）。 | 未标注 | - |
| [`occlusion-culling.md`](occlusion-culling.md) | Hi-Z 两阶段方案、分期、预算与验收；不依赖硬件遮挡查询 | 草稿 | - |
| [`pipeline.md`](pipeline.md) | 阶段划分、几何与缓冲区策略、实体与方块实体、2D 合成、预算与分期 | 已评审 | 2026-10-29 |
| [`shader-organisation.md`](shader-organisation.md) | 文件布局、命名、include、绑定单一来源、启动期自检、与测试门禁；WGSL 语境下的严谨约定 | 草稿 | - |
| [`shader-pack-compatibility.md`](shader-pack-compatibility.md) | 我们能吃什么、不能吃什么，以及不支持特性如何改写；面向包作者 | 草稿 | - |
| [`shader-packs.md`](shader-packs.md) | 渲染目标、阶段、顺序、采样与 uniform 的声明模型，以及失败回退规则 | 已评审 | - |

<!-- END GENERATED: docs/specs/render -->
