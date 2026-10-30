<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

<!-- doc
title: 资源包格式号（pack format）
summary: 26.x 各版本的资源包格式号，取自 Minecraft Wiki；用于判断第三方包是否可用
status: 已核对（外部来源，非 jar）
updated: 2026-10-30
-->

# 资源包格式号

来源：[Minecraft Wiki — Pack format](https://minecraft.wiki/w/Pack_format)（`Template:Resource_pack_format`）。
这是**外部资料**而非 jar 分析，因为 26.3 的 `version.json` 与版本清单都没有可直接读取的 `pack_version` 字段（已实测为空）。

## 1. 关键取值

| 版本 | 资源包格式 |
| --- | --- |
| 26.3 Snapshot 1–10 | 89.0 → 97.0（逐快照 +1；Snapshot 8 = 96.0）|
| **26.3（正式）** | **97.1** |
| 26.2 | 84.0 → 88.0 |
| 26.1 / 26.1.2 | 76.0 → 84.0 |

自 25w31a 起格式号包含**次版本**：非破坏性变更只加次版本（`x.y`）。

## 2. 声明方式（三种，并存于生态中）

| 写法 | 时期 | 说明 |
| --- | --- | --- |
| `"min_format"` + `"max_format"` | 25w31a 起 | 推荐；两端都是 `int` 或 `"97.1"` 字符串 |
| `"supported_formats": n` 或 `[min, max]` | 过渡期 | 单值或二元组 |
| `"pack_format": n` | 旧 | 视作 `n.0` 的单版本区间 |

## 3. 我们怎么用

- 客户端格式取 **97.1**（26.3 正式版；本机目录名仍写着 snapshot 8，那是升级后没改名，不能据此推断版本）。
- 发现包时比较：包声明的区间不含当前格式 ⇒ **跳过并打印**
  `resource pack <id>: format <declared>, client <client>; skipped`。
- 包**没有**声明任何格式 ⇒ 接受（与"未知即接受"一致，不假装校验过）。

## 4. 风险与复查

- Wiki 是外部来源，可能与快照实际值有出入；升级基线时**必须**重新核对本表并更新。
- 若将来能从官方产物（例如客户端 jar 内的资源）读到真实格式号，应改为以官方为准，本表退化为参考。
