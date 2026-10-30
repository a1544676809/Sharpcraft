<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

# rendering

本目录的文档索引由 `tools/Sharpcraft.Docs.Indexer` 生成；手写内容放在标记之外。

<!-- BEGIN GENERATED: docs/src/sharpcraft-framework/rendering -->

| 文档 | 摘要 | 状态 | 最后更新 |
| --- | --- | --- | --- |
| [`shader-packs.md`](shader-packs.md) | 把"外部着色器包"变成引擎的一等输入：包声明渲染目标、阶段、顺序与 uniform，引擎只负责校验与执行。 | 已评审 | 2026-10-29 |
| [`shaders.md`](shaders.md) | 着色器组织的工具与契约实现；规范见 docs/specs/render/shader-organisation.md。 | 未标注 | - |

<!-- END GENERATED: docs/src/sharpcraft-framework/rendering -->

## 2D 几何（界面用）

界面走独立的一套几何，不与世界几何共用顶点布局（`docs/modules/ui.md` §9）：

| 类型 | 职责 |
| --- | --- |
| `UiGeometry` | 追加四边形、按 scissor 切分绘制区间、复用缓冲，稳态零分配 |
| `UiGeometry.Range` | 一段共享 scissor 的索引区间；一次区间 = 一次绘制 |
| `UiProjection` | 左上为原点的正交投影，像素 → 裁剪空间，写入调用方提供的缓冲 |

顶点 **20 字节**（位置 2 + UV 2 + 打包颜色 1 个字），世界几何是 48 字节，两者不复用。

两个不变量：**调用方只追加数据、不发绘制调用**；**scissor 不变就不切区间**，
因此面板数决定绘制次数，与四边形数量无关（有测试守护"四十行两面板 = 两次区间"）。
