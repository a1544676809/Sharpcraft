<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

# 受控偏差清单

> 原则 9：任何改变**可观测行为**的改动都必须在此登记并评审。默认行为必须与原版一致；偏差必须可关闭、可回退。

| 编号 | 偏差 | 默认状态 | 开关 | 影响面 | 依据 |
| --- | --- | --- | --- | --- | --- |
| D-001 | 已加载的**目录资源包**文件变化后实时重载（原版只有整体重载）| **关闭** | `MC_RESOURCE_PACK_WATCH=1` / `--watch-resource-packs` | 仅开启时；关闭时零偏差 | ADR-0016 |
