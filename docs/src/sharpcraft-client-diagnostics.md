<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

<!-- doc
title: Sharpcraft.Client.Diagnostics —— 调试控制通道（Debug 专属）
summary: 以 MCP Streamable HTTP 暴露运行中客户端的截图与状态查询；不进入 Release，可整体删除
status: 活文档
updated: 2026-11-09
-->

# Sharpcraft.Client.Diagnostics

调试期用来驱动运行中客户端的**可移除**程序集（`docs/specs/debug-control.md`，任务 T-782）。

## 1. 职责

| 类型 | 职责 |
| --- | --- |
| `McpServer` | 回环监听的 MCP 服务：`POST /mcp`，JSON-RPC 2.0，实现 `initialize` / `tools/list` / `tools/call`；通知类按规范忽略 |
| `DebugChannel` | 客户端按名加载的入口：建服务、注册工具（`ping`/`state`），返回监听端口 |

本程序集**零项目引用**：客户端通过 `Func<string>` 传入自身状态快照，本程序集不感知任何游戏类型。

## 2. 易分离性（原则 13）

| 条件 | 实现 |
| --- | --- |
| 游戏程序集不引用本程序集 | 客户端仅在 `Condition="'$(Configuration)' == 'Debug'"` 下 `ProjectReference`；加载走 `Assembly.LoadFrom` + `GetType("…DebugChannel")` |
| Release 不含 | Release 构建不编译、不复制本程序集；客户端加载前先 `File.Exists` 检查，缺失即跳过 |
| 无 `#if` / 无环境变量 / 无全局开关 | 客户端入口只有一次 `--debugControl` 判断与文件存在性检查 |
| 可整体删除 | 删除本目录与 `ProjectReference` 后构建、测试、行为不变（A4 验收） |

## 3. 实现与原版类对应

本程序集**没有原版对应物**：它是开发期工具，不是 Minecraft 行为的一部分。因此不声明 `[ReferenceClass]`，也不参与等效性矩阵。

## 4. 已知刻意的简化

| 编号 | 简化 | 影响 | 状态 |
| --- | --- | --- | --- |
| SIM-MCP-HTTP | 手写最小 HTTP/1.1 请求解析（仅 `Content-Length` + 单请求单连接），不使用 ASP.NET Core | 只服务本地代理；不支持分块传输、keep-alive 多请求 | 保持（复查条件：需要 SSE 流或浏览器直连时改用 `HttpListener`/Kestrel） |
| SIM-MCP-TOOLS | 目前挂 `ping`/`state`/`screenshot`；实体查询与相机控制待后续 | 无法通过 MCP 摆相机或查实体 | 待实现（T-782 A2 余项） |
