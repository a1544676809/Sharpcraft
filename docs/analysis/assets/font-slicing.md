<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

<!-- doc
title: 位图字体切分（按资源包分辨率）
summary: 单元尺寸由图集与字符网格推导，度量由 height/ascent 决定；高清包只放大像素，不改变排版
status: 已核对
updated: 2026-10-30
-->

# 位图字体切分

证据：`ref://mc-26.3-analysis/src/net/minecraft/client/gui/font/providers/BitmapProvider.java`（Definition 与 Glyph）。
资源侧：`assets/minecraft/font/default.json` → `include/default.json` 中的 `bitmap` 提供器。

## 1. 提供器声明

```json
{ "type": "bitmap", "file": "minecraft:font/ascii.png", "ascent": 7, "chars": [ "16 个字符", "…共 16 行" ] }
```

- `file`：图集；`chars`：**字符网格**，行数 × 每行字符数决定切分。
- `height` **可选，默认 8**；`ascent` 必填（ascii 为 7）；校验 `ascent <= height`。
- `default.json` 只是 `reference`，真正的 `bitmap` 在 `include/default.json`（另有 `nonlatin_european.png`、`accented.png`）。

## 2. 切分与度量（关键公式）

```
glyphWidth  = textureWidth  / chars[0].length     // 列数
glyphHeight = textureHeight / chars.length        // 行数
pixelScale  = height / glyphHeight                // height 是声明值（默认 8）
advance     = (int)(0.5 + inkWidth * pixelScale) + 1
ascent      = 声明的 ascent
```

渲染时纹理坐标按 `1/pixelScale` 缩放：**高清包只是把像素画得更细，排版尺寸不变**。

## 3. 两个实测数据点

| 图集 | 尺寸 | 网格 | 单元 | pixelScale | 结果 |
| --- | --- | --- | --- | --- | --- |
| 原版 `ascii.png` | 128×128 | 16×16 | 8×8 | 8/8 = 1 | 原样 |
| Faithful 32x `ascii.png` | 256×256 | 16×16 | **16×16** | 8/16 = 0.5 | 画得更细，行高仍 8 |

## 4. 我们当前实现的缺陷（真包实测发现）

`BitmapFont.FromSheet` 写死 **8×8** 单元。用 Faithful 时每个字形只取到左上 1/4，且偏移按 8 计算 ⇒ 文字重叠、糊成一团（截图已留证）。

## 4.1 我们已修的部分（2026-10-30）

- 单元格由图集/列数推导（`BitmapFont` 早已如此，Faithful 256px → 16×16 单元）；
- **真正的错在绘制**：`UiScreenLayout` 用原始单元格尺寸画字，没乘 `pixelScale`，高清图集就会与后一个字重叠。
  已加 `BitmapFont.Scale = height / cellHeight`，绘制与行心偏移都乘它。
- 实测：Faithful 32x → `font: 16x16 cells, line 8`，文字清晰、排版尺寸与原版一致。

**仍待做**：从 `font/default.json`（跟进 `reference` 到 `bitmap` 项）读取 `height`/`ascent` 与 `chars` 网格，
而不是沿用 16 列 / height 8 / ascent 7 的缺省；否则声明了不同 `height` 或非 16 列的包仍会偏。

## 5. 正确做法

1. 从资源栈读 `font/default.json`，跟进 `reference` 到 `bitmap` 提供器；
2. 用 `chars` 的行数/列数切分（而非写死 16）；
3. `height`/`ascent` 决定度量，`pixelScale = height / glyphHeight`；
4. 缺省（无 JSON 或无 bitmap）时退回 8×8 + height 8 + ascent 7，并在日志说明。
