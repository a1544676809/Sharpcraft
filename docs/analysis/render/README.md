<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

# render

本目录的文档索引由 `tools/Sharpcraft.Docs.Indexer` 生成；手写内容放在标记之外。

<!-- BEGIN GENERATED: docs/analysis/render -->

| 文档 | 摘要 | 状态 | 最后更新 |
| --- | --- | --- | --- |
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
