<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

# 方块渲染层与生物群系着色（cutout + tint）

真实服务器目视验收暴露的两项缺口（树叶黑斑、草色不对）。**本文先记录原版实现依据，再定实现步骤**——
依据全部取自分析工作区，仓库内不复制任何原始实现。

## 1. 渲染层：只有三层，按贴图透明度判定

依据：`ref://mc-26.3-analysis/src/net/minecraft/client/renderer/chunk/ChunkSectionLayer.java`

| 枚举 | 混合 | 说明 |
| --- | --- | --- |
| `SOLID` | 否，深度写开 | 完全不透明 |
| `CUTOUT` | 否，**alpha 测试**（丢弃 alpha < 阈值 的片元）| 镂空：树叶、花草、铁栏杆 |
| `TRANSLUCENT` | 是，深度写关、由远到近 | 水、玻璃、冰 |

判定不是"按方块名查表"，而是按**该方块模型所用贴图的 alpha 内容**：

```
Transparency(hasTransparent, hasTranslucent)
byTransparency: hasTranslucent → TRANSLUCENT
                hasTransparent → CUTOUT
                否则          → SOLID
```

即：贴图里存在**部分透明**像素（0<alpha<255）⇒ TRANSLUCENT；只存在**全透明**像素（alpha=0 的镂空）⇒ CUTOUT。

> 原版 26.3 只有这三层；旧版本的 `cutout_mipped` 已并入 `CUTOUT`。

## 2. 生物群系着色：按方块位置、按面（tintindex）

依据：`BiomeColors.java`、`GrassColor.java`、`FoliageColor.java`、`ColorMapColorUtil.java`、
`BlockColors.java`（注册表）、`ClientLevel.calculateBlockTint`、`BlockTintSources.java`。

### 2.1 谁被着色

| 方块 | 着色来源 |
| --- | --- |
| `grass_block` | `BlockTintSources.grassBlock()`（草地顶面）|
| `short_grass`/`fern`/`bush` 等 | `grass()` |
| `oak/jungle/acacia/dark_oak/mangrove_leaves` | `foliage()` |
| `spruce_leaves` / `birch_leaves` | **常量** `-10380959` / `-8345771` |
| `leaf_litter` | `dryFoliage()` |
| 水 | 群系水色（默认 `4159204`）|

只对模型里 `tintindex >= 0` 的面生效——着色是**逐顶点**的，不是整个方块。

### 2.2 颜色怎么算

`ColorMapColorUtil.get(temp, rain, pixels, fallback)`：

```
rain *= temp
x = (int)((1 - temp) * 255)
y = (int)((1 - rain) * 255)
index = (y << 8) | x        // 256×256，行优先
color = pixels[index]  （越界用 fallback）
```

- `grass.png` / `foliage.png` 是 256×256 的**颜色表**（`textures/colormap/*.png`），由资源包提供，**不进仓库**。
- 草：`GrassColor.get(temp, downfall)`，fallback `-65281`；默认色 `get(0.5, 1.0)`。
- 叶：`FoliageColor.get(temp, rain)`，fallback `-12012264`。

### 2.3 群系混合半径

`ClientLevel.calculateBlockTint`：以 `options.biomeBlendRadius`（默认 **2**）为半径，对
`(2r+1)² = 25` 个位置的群系颜色求平均（R/G/B 分别累加后取整）。半径 0 时直接用该点群系。

### 2.4 数据从哪来

群系的 `temperature`/`downfall`/`water_color` 等来自**服务端下发的群系注册表数据**；
`colormap/*.png` 来自资源包（jar 提取）。两者都不在仓库内。

## 3. 我们的实现步骤

| 步 | 内容 | 验收 |
| --- | --- | --- |
| **A1** | 贴图 alpha 分析 → `Transparency`；区块网格按三层拆分（solid / cutout / translucent）| 树叶进入 cutout 层（三角形数 > 0），石头仍在 solid 层 |
| **A2** | `cutout` 管线：alpha 测试（discard），深度写开、无混合；两条渲染路径都接 | 真实服务器树叶**不再是黑斑**；solid 区域逐像素不变 |
| **A3** | `tintindex` → 逐顶点颜色：colormap 载入、群系 temp/downfall 查表、25 点平均；草丛/树叶/水按群系上色 | 同角度截图草色分层；水呈蓝；单测覆盖 `ColorMapColorUtil` 公式 |

## 4. 风险与边界

- **不是**按方块名判断层/着色（原版按贴图 alpha 与模型 `tintindex`）；我们若图省事做名表，会在模组方块上立刻翻车。
- 群系数据缺失时必须有确定回退（`GrassColor.getDefaultColor()`），并在日志中标注缺失项，不得静默变黑。
- `colormap` 缺失时回退常量色同理。

### 实测核对（2026-10-30，按用户要求先查原版/原版数据）

从资产包（`minecraft-26.3-*.pack.zip`，11534 项）里取出实际文件核对：

| 事实 | 证据 |
| --- | --- |
| **`models/block/leaves.json` 没有 `render_type`** | 模型只有 parent/textures/elements；六个面都带 `tintindex: 0` |
| 树叶层由**贴图 alpha** 决定 | `textures/block/oak_leaves.png` 16×16：**alpha=0 有 84 个像素，部分透明 0 个** ⇒ `Transparency(true,false)` ⇒ **CUTOUT** |
| 树叶需要**群系着色** | 模型每个面 `tintindex: 0`，即逐顶点上色 |

⇒ 我们 A1 的"按贴图 alpha 判定层"与原版一致；**名称表不应作为主判据**（已改为：alpha 优先，名称表仅作无图集/不透明贴图时的兜底）。

**现状与遗留**：
- 真实服务器（用户确认：**Fabric，基本无 mod**）实测 `cutout 4256 tri / translucent 1214 tri`，cutout 层确实生效；
- 树叶仍呈**黑白棋盘格** ⇒ 属于 **sprite 解析或 UV 取样**问题（不是分层判据）。下一步：给 sprite 未命中加日志，并与 `FaceBakery` 的 UV 推导 + 图集 padding 对照。
- 另：本地合成场景（水/树叶）此前是**纯色**，因为该路径 `PrepareSprites` 返回 null（无图集），不能用来验证贴图；贴图验证必须走真实资产路径。
