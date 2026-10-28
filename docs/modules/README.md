<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

状态：[草稿]
基线：Minecraft 26.3
证据：`docs/architecture/boundaries.md`
最后更新：2026-10-29

# 模块设计文档

每份文档对应一个待实现模块，遵循 AGENTS.md 3.5：**模块文档状态达到 `[已评审]` 之前不写该模块代码**。模板见 [`../_templates/module-doc.md`](../_templates/module-doc.md)。

## 文档清单

| 文档 | 模块 | 侧别 | 状态 |
| --- | --- | --- | --- |
| [`build-scaffolding.md`](build-scaffolding.md) | 构建骨架与程序集划分 | 外围（工程） | 草稿（已实施，待评审） |
| [`common-primitives.md`](common-primitives.md) | Common 基础类型：标识符、注册表、随机数、坐标 | Common | 已评审 |
| [`nbt.md`](nbt.md) | NBT 读写与上限 | Common | 已评审 |
| [`world.md`](world.md) | 世界数据：调色板容器、区块段与列、坐标查询 | Common（数据结构）/ Client（`ClientWorld`） | 已评审 |
| [`auth.md`](auth.md) | 认证链路（设备码优先） | 外围（宿主） | 已评审 |
| [`launcher.md`](launcher.md) | 启动器：认证、资产、启动编排 CLI | 外围（宿主） | 已评审 |
| [`accounts.md`](accounts.md) | 多账号与离线登录闸门 | 外围（宿主） | 已评审 |
| [`assets.md`](assets.md) | 资产管线：jar 校验与单文件 pack | 外围（宿主） | 已评审 |
| [`asset-resolution.md`](asset-resolution.md) | 资源解析层：来源栈、覆盖、元数据兄弟 | Client | 已评审 |
| [`render.md`](render.md) | 渲染层：网格、相机、离屏/窗口渲染、纹理 | Client + `Framework` | 已评审 |

## 当前进度

- `build-scaffolding`：7 个源工程 + 6 个测试工程已落地，`dotnet build`（警告即错误）/ `dotnet test` 全绿（444 项）；架构守卫（B1–B3 + 引擎层隔离）可拦截越界引用。
- `common-primitives` / `nbt` / `world` / `auth` / `launcher` / `accounts` / `assets` / `asset-resolution` / `render`：文档与实现均已落地，验收证据见 `docs/traceability.md` 对应行。
- 尚未开工的模块（模组加载器、服务端侧、着色器包）没有模块文档，按 3.5 也不得先写代码。

## 编写要求

每份模块文档必须回答模板中的 7 个问题；侧别判定必须引用 `docs/architecture/boundaries.md` 的规则编号（R1–R7），不得只写结论。
