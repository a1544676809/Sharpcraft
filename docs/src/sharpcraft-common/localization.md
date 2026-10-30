<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

<!-- doc
title: src/Sharpcraft.Common/Localization
summary: 翻译表：键查表与参数替换
status: [已评审]
-->

# Sharpcraft.Common/Localization

## 1. 职责

把服务端发来的**翻译键**解析成文本，并填入参数。

## 2. 内容

| 类型 | 说明 |
| --- | --- |
| `LanguageTable` | 从语言文件解析出的键值表；`TryGet` / `Resolve` / `Format` |
| `LanguageTable.Empty` | 空表：未知键原样返回（用于没有资源包时）|

## 3. 约定

- **格式**：只处理 `%s`（顺序）、`%1$s`（编号）、`%%`（字面百分号）。其余说明符原样保留，不猜。
  参数不足时对应位置留空，不打印占位符。
- **未知键原样返回**：`command.unknown.command` 这类键出现在界面上是**故意的**——它让缺失的翻译可见，
  而不是静默变成空白。
- **非字符串值跳过**而不是整表失败：语言文件是游戏资产，个别异常条目不应连累整张表。
- **数据来源**：语言文件由启动器从游戏 jar 提取到仓库外（`assets/minecraft/lang/en_us.json`），
  仓库内不存任何语言文件（原则 3）。
