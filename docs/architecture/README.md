<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

状态：[草稿]
基线：Minecraft 26.3
证据：`ref://mc-26.3-analysis/reports`
最后更新：2026-10-28

# 架构（Architecture）

本目录存放**跨子系统的架构决策依据**，与 `analysis/`（对原版的事实分析）和 `specs/`（对外契约规格）区分：这里是"我们怎么组织代码"，并给出为什么这样组织的证据。

## 文档清单

| 文档 | 职责 | 状态 |
| --- | --- | --- |
| [`boundaries.md`](boundaries.md) | 三侧（Client / Server / Common）+ 外围宿主层的划分规则、证据、必须拆分的区域、C# 程序集映射 | 草稿 |

## 当前生效的边界规则（摘要）

1. **引用 `net.minecraft.client.*` 的代码不可能是 Common 或 Server**。这不是我们的发明：原版 565 个包中，除 `net.minecraft.realms`（4 类）外，无一触碰客户端类型。
2. 依赖方向只允许 `Client` / `Server` → `Common`。
3. 同一包内混合"共享数据 + 权威逻辑"时必须拆分，不得整包归类。
4. 引擎与工具链（`com.mojang.renderpearl` / `blaze3d` / `data` / `gametest`）属外围宿主层，不属于三侧，且不得被游戏逻辑反向引用。

细则与证据见 [`boundaries.md`](boundaries.md)。

## 下一步

- [ ] 把规则 1 与规则 2 做成**可自动检查**的构建任务（跨程序集引用检查），而不是靠人工评审。放在引入首批程序集之后实施。
- [ ] 逐区域细化拆分方案：`world.level.block`、`world.entity`、`world.item` 各需要一份独立的拆分清单，随对应模块文档产出。
- [ ] 与 `docs/modules/*.md` 对齐：每个模块文档必须声明侧别并引用本文的规则编号。
