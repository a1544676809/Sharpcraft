<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

---
name: 缺陷报告
about: 报告可复现的问题（结构见 AGENTS.md 原则 12）
title: "[bug] "
labels: bug
---

<!-- 提交前请确认：本仓库禁止附带任何 Minecraft 资产、jar、反编译代码或原始实现片段（原则 3）。 -->

## 摘要

一句话说明现象。

## 影响面

- 模块 / 目录：
- 追踪矩阵 ID（`docs/traceability.md`）：
- 是否影响协议 / 存档 / 模组 API：

## 复现步骤

1.
2.
3.

**最小复现用例**（修复前必须先落到 `tests/<工程>/Repro/Repro_<编号>Tests.cs`）：

- [ ] 已提交最小复现用例，路径：`tests/.../Repro/Repro_____Tests.cs`

## 期望行为 vs 实际行为

- 期望：
- 实际：

## 证据

- 日志 / 错误输出（**必须脱敏**：不得含 accessToken、refreshToken、玩家 UUID 之外的账号信息）：
- 原版行为对照（如有）：说明是"与 26.3 不一致"还是"双方一致但不符合需求"

## 环境

- 平台 / 架构：
- .NET 运行时版本：
- 基线版本：26.3（协议 777）
- 相关提交：

## 初步定位（可选）

- 怀疑的模块 / 文件：
- 已排除的假设：
