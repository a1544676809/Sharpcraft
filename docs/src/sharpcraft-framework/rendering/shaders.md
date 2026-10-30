<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

# Framework / 渲染 / 着色器（shaders）

着色器组织的**工具与契约**实现；规范见 `docs/specs/render/shader-organisation.md`。

| 类型 | 职责 |
| --- | --- |
| `ShaderIncludeComposer` | 展开 `//#include "..."`：每个文件只展开一次、深度上限 4、缺失时报"管线 + 行号 + 目标" |
| `ShaderBindings` | binding 号的**唯一事实来源**（0 图集 / 1 采样器 / 2 相机 64B / 3 雾 32B），布局必须由它生成 |
| `ShaderDescriptor` | 管线期望（文件、入口、binding 集合）与着色器源码的**一致性校验**，双向报告差异 |

待落地：`Shaders/pipelines/*.wgsl` 文件拆分（G1）、管线条目表（G5）、以及"禁止内联 WGSL"的门禁测试。
