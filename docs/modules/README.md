<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

状态：[草稿]
基线：Minecraft 26.3
证据：`docs/architecture/boundaries.md`
最后更新：2026-10-29

# 模块设计文档

每份文档对应一个待实现模块，遵循 AGENTS.md 3.5：**模块文档状态达到 `[已评审]` 之前不写该模块代码**。模板见 [`../_templates/module-doc.md`](../_templates/module-doc.md)。

## 文档清单

<!-- BEGIN GENERATED: docs/modules -->

| 文档 | 摘要 | 状态 | 最后更新 |
| --- | --- | --- | --- |
| [`accounts.md`](accounts.md) | 启动器的多账号与离线登录闸门。 | 已评审 | 2026-10-28 |
| [`asset-resolution.md`](asset-resolution.md) | 把多个资产来源变成一个行为与原版一致的只读资源视图：给定 Identifier，得到唯一一份数据；给定前缀，能列出全部可用资源。 | 已评审 | 2026-10-28 |
| [`assets.md`](assets.md) | 把用户本地的原版 jar 变成客户端可消费的只读资源目录，且仓库内不出现任何资产（原则 3）。 | 已评审 | 2026-10-29 |
| [`auth.md`](auth.md) | 负责"从微软账号到 Minecraft 会话令牌"的完整链路，供启动器与客户端共用。 | 已评审 | 2026-10-28 |
| [`build-scaffolding.md`](build-scaffolding.md) | 提供所有后续模块赖以存在的工程底座：解决方案结构、程序集划分、编译期约束、测试基础设施、边界自动校验。 | 草稿 | 2026-10-28 |
| [`common-primitives.md`](common-primitives.md) | 提供全项目最底层、无副作用、无 IO 的值类型与容器。 | 已评审 | 2026-10-28 |
| [`doc-indexer.md`](doc-indexer.md) | 从文档与源码目录生成目录索引，并在 CI 检查索引与模块覆盖 | 已评审 | 2026-10-29 |
| [`entities.md`](entities.md) | 玩家不是特例，而是实体系统的一个成员。 | 已评审 | 2026-10-29 |
| [`input.md`](input.md) | 把平台输入收敛成与平台无关的动作状态，把客户端状态输出到窗口与日志。 | 已评审 | 2026-10-29 |
| [`launcher.md`](launcher.md) | 独立跨平台启动器：认证、版本/资产获取、参数装配、进程启动。 | 已评审 | 2026-10-28 |
| [`modding.md`](modding.md) | 把"模组"做成宿主结构的一部分：模组程序集加载进可卸载的 AssemblyLoadContext，卸载后必须能真正被 GC 回收。 | 已评审 | 2026-10-29 |
| [`nbt.md`](nbt.md) | 网络与数据包里的 NBT 读写：标签类型表、复合/列表、数组、字符串，以及读取上限。 | 已评审 | 2026-10-28 |
| [`player.md`](player.md) | 让"人"在世界里动起来：速度、重力、跳跃、AABB 碰撞与台阶逻辑，量级对齐原版。 | 已评审 | 2026-10-29 |
| [`render.md`](render.md) | 把 ClientWorld 的方块数据变成 GPU 可绘制的几何，并产出可验证的图像。 | 已评审 | 2026-10-29 |
| [`world.md`](world.md) | 客户端的世界数据表示：区块存储、方块状态查询、维度与本地玩家标识。 | 已评审 | 2026-10-28 |

<!-- END GENERATED: docs/modules -->

## 当前进度

- `build-scaffolding`：7 个源工程 + 6 个测试工程 + 1 个工具工程已落地，`dotnet build`（警告即错误）/ `dotnet test` 全绿（446 项）；架构守卫（B1–B3 + 引擎层隔离 + 文档索引/覆盖门禁）可拦截越界引用与索引漂移。
- `common-primitives` / `nbt` / `world` / `auth` / `launcher` / `accounts` / `assets` / `asset-resolution` / `render`：文档与实现均已落地，验收证据见 `docs/traceability.md` 对应行。
- 尚未开工的模块（模组加载器、服务端侧、着色器包）没有模块文档，按 3.5 也不得先写代码。

## 编写要求

每份模块文档必须回答模板中的 7 个问题；侧别判定必须引用 `docs/architecture/boundaries.md` 的规则编号（R1–R7），不得只写结论。
