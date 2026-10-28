<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 MinecraftCSharp contributors -->

状态：[草稿]
基线：Minecraft 26.3
证据：`ref://mc-26.3-analysis`
最后更新：2026-10-28

# 架构分析（第一轮）

本目录回答"Minecraft 26.3 整体是什么结构"，不深入单个子系统行为。子系统级分析各自成目录（见 `docs/analysis/README.md`）。

## 文档清单

| 文档 | 职责 | 状态 |
| --- | --- | --- |
| [`overview.md`](overview.md) | 基线标识、分层结构、入口与启动、渲染栈概况、三侧初判、风险 | 草稿 |
| [`package-map.md`](package-map.md) | 包级计数地图与各子树职责/归属倾向 | 草稿 |

## 第一轮得出的关键事实

1. 单一 jar 内同时包含客户端与服务端（`net.minecraft.client` 2,987 类 / `net.minecraft.server` 698 类），靠包名区分，无进程边界。
2. 渲染已是**双后端架构**：`com.mojang.renderpearl`（OpenGL + Vulkan）+ 既有 `com.mojang.blaze3d`。图形 API 选择暴露在 `client.PreferredGraphicsApi`。
3. 内容高度数据驱动：`assets/` 11,813 条、`data/` 10,449 条，且两者都有 `.mcassetsroot` 根标记，支持根级替换。
4. 原版客户端是**命令行驱动的独立进程**（`net.minecraft.client.main.Main` + 9 项会话参数），与原则 8 的解耦要求天然一致。
5. 客户端体重集中在 `client.renderer`（1,006）+ `client.gui`（794），约占客户端 60%，是移植工作量与性能的主战场。

## 复现方式

1. 用 .NET 的 ZipFile 枚举 jar 条目，导出 `entries.csv`（路径/大小/压缩后大小）。
2. 用 Vineflower 1.12.0 反编译整包到 `src/`：`java -Xmx8g -jar vineflower-1.12.0.jar -dgs=1 <jar> <out>`。
3. 环境：GraalVM JDK 25（含 javap/jdeps/jar），.NET SDK 10.0.401。

工具与产物**全部位于仓库之外**（原则 3）；本目录只保存结论与指针。

## 下一轮待办

- [ ] `docs/architecture/boundaries.md`：逐子系统（重点是 `world.level.*`、`world.entity.*`）给出 Client / Server / Common / 外围归属与依据。
- [ ] `docs/specs/network/`：握手、登录、配置、游戏四阶段数据包与版本 777 的映射。
- [ ] `docs/specs/render/`：`renderpearl` 能力模型与 WGPU 的逐项对照。
- [ ] `docs/analysis/assets/`：资产树分类、格式、提取策略。
- [ ] 待验证项清零：`world.attribute`、`gizmos`、`waypoints`、`world.clock`、`util.filefix`、`server.jsonrpc` 的确切职责。
