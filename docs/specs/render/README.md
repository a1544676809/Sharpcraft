<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

# render

本目录的文档索引由 `tools/Sharpcraft.Docs.Indexer` 生成；手写内容放在标记之外。

<!-- BEGIN GENERATED: docs/specs/render -->

| 文档 | 摘要 | 状态 | 最后更新 |
| --- | --- | --- | --- |
| [`backend-interface.md`](backend-interface.md) | 能力协商、阶段模型、禁止泄漏规则；WGPU 为唯一实现，Vulkan 仅预留 | 草稿 | - |
| [`block-layer-and-tint.md`](block-layer-and-tint.md) | 真实服务器目视验收暴露的两项缺口（树叶黑斑、草色不对）。 | 未标注 | - |
| [`chunk-geometry-arena.md`](chunk-geometry-arena.md) | 为什么要合并、缓冲怎么长、区块卸载怎么回收、每帧怎么画；附实测归因与验收口径 | 未标注 | - |
| [`fluid-rendering.md`](fluid-rendering.md) | 水/岩浆的液面高度、面剔除、flow-UV、染色、逐面光、贴图动画与水下雾 | 已评审 | - |
| [`known-gaps.md`](known-gaps.md) | 当前渲染管线尚未做或仍为受控偏差的事项清单（几何/天空/雾/云/性能） | 草稿 | 2026-11-01 |
| [`lighting.md`](lighting.md) | 证据指针：ref://mc-26.3-analysis/src/net/minecraft/client/renderer/Lightmap.java、 .../LightmapRenderStateExtractor.java、..… | 草案（待评审）。范围：方块几何的逐顶点光照与 lightmap 乘子。 | - |
| [`occlusion-culling.md`](occlusion-culling.md) | Hi-Z 两阶段方案、分期、预算与验收；不依赖硬件遮挡查询 | 草稿 | - |
| [`oit-reference.md`](oit-reference.md) | 26.3 的 OIT 三阶段行为规格，作为窗口唯一采用的透明管线 | 已实现（反向 Z 适配，2026-11-05） | 2026-11-05 |
| [`optimization-plan.md`](optimization-plan.md) | 云 OIT/云几何、OIT 目标、天体与绘制路径的分阶段优化与验收标准 | 已评审 | 2026-11-01 |
| [`pipeline.md`](pipeline.md) | 阶段划分、几何与缓冲区策略、实体与方块实体、2D 合成、预算与分期 | 已评审 | 2026-10-29 |
| [`screenshot.md`](screenshot.md) | 交换链纹理不保证可 COPY_SRC（见 ADR-0003），直接拷贝会失败。 | 未标注 | - |
| [`shader-organisation.md`](shader-organisation.md) | 文件布局、命名、include、绑定单一来源、启动期自检、与测试门禁；WGSL 语境下的严谨约定 | 草稿 | - |
| [`shader-pack-compatibility.md`](shader-pack-compatibility.md) | 我们能吃什么、不能吃什么，以及不支持特性如何改写；面向包作者 | 草稿 | - |
| [`shader-packs.md`](shader-packs.md) | 渲染目标、阶段、顺序、采样与 uniform 的声明模型，以及失败回退规则 | 已评审 | - |
| [`sky-celestial.md`](sky-celestial.md) | 原版 SkyRenderer 的天空盘、日月星几何/变换/贴图与环境属性依赖 | 待评审 | - |

<!-- END GENERATED: docs/specs/render -->
