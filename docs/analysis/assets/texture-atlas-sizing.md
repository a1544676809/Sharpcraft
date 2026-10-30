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
| **方块图集/模型/blockstate 走统一资源栈** | ✅ 已改：`PrepareSprites(layout, resources)` 用会话栈；实测 Faithful 32x → 图集 **1024→2048**，世界贴图变 32x |

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

## 6. 分辨率在热重载中变化（设计）

| 类别 | 影响 | 处理 |
| --- | --- | --- |
| UI 字形/精灵 | 只变像素，排版不变（`pixelScale` 保住声明高度）| 已有：版本号变化即重建纹理与绑定组 |
| 方块图集 | 图集尺寸与拼接位置改变 ⇒ **所有 UV 失效** | 重建图集 → 换 GPU 纹理 → **作废并重建全部区块网格**；三步原子，禁止"新图集 + 旧 UV" |
| 精灵索引 | 包增删精灵会让序号漂移 | 应按**精灵身份**绑定而非序号（当前提示框只用 0/1 固定槽，风险低）|

全量区块重建只在重载发生时做一次，与参考实现一致，不进稳态热路径。

## 7. 换图集的落地障碍（实测代码）

- `WindowedRenderer` 只在**启动时**用 `request.Atlas` 创建一次 `atlasTexture`/`atlasView`（L71–101）；
  `rebuildRequest` 只替换**几何请求**（L569 附近），**不会重新上传图集**。
- 图集视图被多个绑定组引用（地形管线与 `uiBindGroup`），换图必须同时重建这些绑定组，不能只替换纹理。
- 区块网格的 UV 来自图集，图集尺寸/拼接一变就必须**作废并重建全部区块**（`MeshCache`/`ChunkWorkerPool` 持有旧 `BlockSpriteMap` 引用）。

结论：这是一次跨 `WindowedRenderer` + `MeshCache` + `WindowCommand` 的结构性改动，不能靠"顺手改两行"完成。
落地顺序（建议）：

1. `WindowedRenderer`：新增 `AtlasTextureSource`（`TextureData` + 版本号），版本变化时重建 `atlasTexture`/`atlasView`
   并**重建所有引用它的绑定组**（地形与 UI 各一套），旧资源延后一帧释放；
2. `MeshCache`/`ChunkWorkerPool`：支持整体作废并接收新 `BlockSpriteMap`；
3. `WindowCommand`：重载监听器里重建图集 → 更新映射 → 作废全部区块 → 提升图集版本；
4. 验收：同一窗口内把包从 16x 换成 32x（或用 Faithful 覆盖），画面贴图变细且**不残留旧 UV 的错位面**。
