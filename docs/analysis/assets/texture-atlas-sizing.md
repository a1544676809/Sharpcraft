<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

<!-- doc
title: 贴图图集与分辨率无关性（方块纹理）
summary: 图集按原生尺寸打包；真正缺的是方块相关加载器仍绕过统一资源栈
status: 部分核对
updated: 2026-10-30
-->

# 贴图图集与分辨率无关性

证据：`ref://mc-26.3-analysis/src/net/minecraft/client/renderer/texture/{TextureAtlas,Stitcher,SpriteLoader,SpriteContents}.java`。

## 1. 原版怎么做（已核对）

- **按原生尺寸打包**：`SpriteLoader` 把每张图按自身像素尺寸交给 `Stitcher`；不存在"统一 16×16 网格"的假设。
- **图集尺寸**：`Stitcher` 产出 2 的幂尺寸（上限受 GPU 限制），必要时缩放全部贴图。
- **动画帧**：帧尺寸来自动画元数据（`AnimationMetadataSection` → `FrameSize`），`SpriteContents` 据此切帧；
  因此**帧尺寸必须读元数据**，不能只按"高/宽"整数倍猜。
- **UV**：以图集归一化坐标表示，与单张贴图分辨率无关。

## 2. 我们的现状（已核对代码）

| 项 | 状态 |
| --- | --- |
| 图集按原生尺寸打包（非统一网格）| ✅ `SpriteAtlas` 用真实尺寸，见 `Stitch`/`SpriteRect` |
| 图集为 2 的幂 | ✅ `MaxAtlasSize = 8192` |
| 动画帧数 = 高/宽（**假定方帧**）| ⚠️ 未读动画元数据；非方帧会错 |
| **方块图集/模型/blockstate 走统一资源栈** | ❌ **最大缺口**：`FrameCommand` 在 L480 自建"仅基础包"的栈后调用 `SpriteAtlasBuilder.Build` |

## 3. 结论

"分辨率无关"在图集这一层**天然成立**；Faithful 这类高清包真正失效的地方是**加载路径**——
方块图集、模型与 blockstate 解析没有经过 `ResourceManager`，所以包里的贴图/模型根本没进图集。
这与字体问题是**两个不同原因**，必须分开修：

- 字体：切分规则写死 8×8（已定方案，见 `font-slicing.md`）；
- 方块：加载器绕过统一栈（本文件）。

## 4. 计划

1. 把 `SpriteAtlasBuilder`、`BlockModelResolver`、blockstate 解析改为从 `ResourceManager` 读取（并让图集随重载一起重建）。
2. 动画帧尺寸读 `animation` 元数据；缺省时退回"方帧"假设并记诊断。
3. 高清包重载后同步刷新图集（与 T-183 的纹理版本机制合并）。
