<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

<!-- doc
title: 着色器包的解剖（以 ComplementaryUnbound r5.9.3 为样本）
summary: 真实 Iris 包的文件布局、阶段清单、包装层与共享翻译单元、选项系统；全部为行为级描述
status: 草稿
-->

# 着色器包的解剖（样本：ComplementaryUnbound r5.9.3）

证据指针：`ref://mc-26.3-analysis/artifacts/shaderpacks/ComplementaryUnbound_r5.9.3.zip`
（仓库外本地样本；仓库内**不含任何着色器源码或资产**，本文只记录结构、名称与数量。）

样本量：**468 个条目 / 解压 1.2 MB / 压缩 553,400 字节**。

## 1. 顶层布局

| 路径 | 条目数 | 作用 |
| --- | --- | --- |
| `shaders/` | 1 | 包根；**所有内容都在这一层之下**（Iris 约定） |
| `shaders/lib/` | 179 | 共享库：光照、材质、空间变换、抗锯齿等 |
| `shaders/world0/` | 77 | 主世界（overworld）的阶段包装文件 |
| `shaders/world-1/` | 77 | 下界 |
| `shaders/world1/` | 77 | 末地 |
| `shaders/program/` | 35 | **共享翻译单元**（`.glsl`），每阶段一个 |
| `shaders/lang/` | 2 | 选项界面的本地化 |
| `shaders/*.properties` | 6 | 配置：`shaders` / `block` / `entity` / `item` / `dimension` / `colorwheel` |
| `shaders/pack.json` | 1 | 包元数据 |

扩展名分布：`.glsl` 189、`.fsh` 108、`.vsh` 108、`.csh` 3、`.properties` 6、`.json` 5、`.placebo` 7。

> `.placebo` 与 `pack.json` 一起表明：这个包依赖一个前置库（Placebo）。**依赖型包**是兼容层必须能识别并给出可读报错的形态。

## 2. 两层结构（关键约定）

同一个阶段由**两份文件**组成：

1. `program/<stage>.glsl` —— 共享翻译单元，真正逻辑都在这里，经 `#include "/lib/..."` 组装；
2. `worldN/<stage>.vsh` + `.fsh` —— **薄包装**，按维度选择要编译进共享单元的定义。

同一阶段在不同维度可以有不同定义，因此 `world0/` 里能出现 `world-1/` 没有的变体：
`gbuffers_block_translucent`、`gbuffers_entities_glowing`、`gbuffers_entities_translucent`、`gbuffers_line`，
以及 `clrwl_*`（colorwheel 专用）系列。

对我们的含义：**兼容层要按"维度 × 阶段"解析，而不是全局单份**。

## 3. 阶段清单（实测存在的名字）

| 类别 | 阶段 |
| --- | --- |
| 几何缓冲 | `gbuffers_terrain`、`_textured`、`_basic`、`_block`、`_entities`、`_hand`、`_water`、`_clouds`、`_weather`、`_skybasic`、`_skytextured`、`_armor_glint`、`_spidereyes`、`_beaconbeam`、`_damagedblock`、`_lightning`（+ `_translucent`/`_glowing`/`_line` 变体）|
| 阴影 | `shadow`（投影）、`shadowcomp`（`.csh` 计算） |
| 延迟 | `deferred1` |
| 合成 | `composite`、`composite1`…`composite7` |
| 最终 | `final` |
| 远景扩展 | `dh_terrain`、`dh_water`（Distant Horizons）、`voxy_opaque`、`voxy_translucent`（Voxy 模组）|

对我们的含义：`docs/specs/render/pipeline.md` §3 的 9 个 pass 能对上 `gbuffers_*` / `shadow` / `deferred` / `composite` / `final` 的主干；
`dh_*`、`voxy_*`、`clrwl_*` 属于**模组扩展阶段**，内置管线不提供，需按"未识别阶段 → 跳过并报告"处理。

## 4. 选项系统

`shaders.properties`（**280 行**）承担三件事：

- `profile.<NAME> = 一串 #define 赋值`：预设档位（POTATO…COMPLEMENTARY 共 9 档），每档给出 `SHADOW_QUALITY`、`shadowDistance`、`WATER_REFLECT_QUALITY`、`SSAO_QUALI_DEFINE`、`COLORED_LIGHTING` 等；
- `screen=` / `screen.<TAB>=`：选项界面**布局**（把 `slider.*`、`option.*` 摆到页签里）；
- `slider.*` / `option.*`：可调项与开关。

对我们的含义：包的"设置界面"是**数据驱动的布局描述**，不是代码。兼容层至少要做到：能读 `#define` 集合、能接受 `sliders` 取值、能忽略界面布局而不报错。

## 5. 我们需要的接入点（与 `shader-packs.md` 对照）

| 包需要 | 我们的现状 |
| --- | --- |
| 可命名渲染目标（colortex0-15、shadow、depth 等）| §1 三层模型已定义，未实现 |
| 阶段可插入/替换 | §3 已定义 pass 表，未实现 |
| 采样与 uniform 按名声明 | §4 已定义，未实现 |
| 编译失败回退内置管线 | §5 已定义规则，未实现 |
| **维度 × 阶段** 解析 | **规格缺失**，需补 |
| 未识别阶段（DH/Voxy/colorwheel）跳过 | **规格缺失**，需补 |

## 6. 不支持项（须在兼容指导中明说）

计算着色器（`.csh`）、Distant Horizons / Voxy 远景、colorwheel 系列、Placebo 依赖包、GLSL 高级特性（几何/细分着色器、`textureQueryLod` 等）
——这些**不在 P3 首批支持范围**，需在 `shader-pack-compatibility.md` 给出"如何改写成可运行"的指导。

## 7. 官方参考对照（2026-10-30 联网核对）

本地样本只有 Complementary 系列（Unbound / Reimagined / Shaders v4，共 3 个不同世代），族内同源，**不足以代表全部形态**。
因此以 Iris 官方文档为准补齐广度，来源：

- [Programs 概览](https://shaders.properties/current/reference/programs/overview/)（阶段与执行顺序）
- [shaders.properties 概览](https://shaders.properties/current/reference/shadersproperties/overview/)（指令族）
- [dimension.properties](https://shaders.properties/current/reference/miscellaneous/dimension_properties/)（维度选择）

### 7.1 官方执行顺序（比样本更完整）

`setup`(仅计算, 1-99) → `begin`(1-99) → `shadow` → `shadowcomp`(1-99) → `prepare`(1-99) → `gbuffers`(不透明) →
`deferred`(1-99) → `gbuffers`(半透明) → `composite`(1-99) → `final`。

样本里没有出现 `setup` / `begin` / `prepare`，说明**样本只用了子集**；兼容层必须按官方全序实现。

### 7.2 阶段扩展名与后缀

`.vsh` / `.fsh` / `.gsh`(几何) / `.tcs`+`.tes`(细分) / `.csh`(计算)。阶段后缀 `1`–`99`；
计算着色器在同一 composite 阶段内可再用 `_a`…`_z` 追加，**单个 pass 最多 27 个计算着色器**，按字母序执行（除非 `allowConcurrentCompute`）。
细分仅 gbuffers 类，且需 `TESSELLATION_SHADERS` 特性标志。

### 7.3 维度选择有两套机制（**重要修正**）

| 机制 | 语义 |
| --- | --- |
| `world0` / `world-1` / `world1`（OptiFine 遗留）| 一旦存在**任一** `worldN` 目录，就**只**从这些目录加载，**基础 `shaders/` 不再参与** |
| `dimension.properties`（Iris）| `dimension.<文件夹>=<维度 id>...`，文件夹名任意，支持 `namespace:` 前缀与 `*` 通配；未列出的维度回落到基础目录 |

⇒ 我先前只写了 `worldN`，**遗漏了 `dimension.properties` 及其"回落"语义**，这是规格缺陷，必须补。

### 7.4 shaders.properties 指令族（官方分类）

| 族 | 代表指令 | 用途 |
| --- | --- | --- |
| 特性标志 | `iris.features.required` / `optional`（`COMPUTE_SHADERS`、`SSBO`、`CUSTOM_IMAGES`、`REVERSED_CULLING`…）| **包声明自己需要什么**；不支持时应拒绝加载并给可读原因 |
| 程序与顺序 | `program.enabled`、`shadow.*`、`particles.ordering`、`skipAllRendering`、`indirect.pass` | 按选项开关程序 |
| 功能开关 | `clouds`、`sun`、`moon`、`sky`、`stars`、`weather`、`vignette`、`oldLighting`、`separateAo` | 关闭原版对应几何 |
| 渲染状态 | `alphaTest`、`blend`、`scale`、`flip`、`size.buffer`、`backFace`、`frustum.culling`、`occlusion.culling`、`shadow.culling` | 逐程序/逐缓冲设置 |
| 自定义 uniform | `uniform.*` / `variable.*` | CPU 求值后上传 |
| 纹理与缓冲 | `texture`、`texture.noise`、`customTexture`、`image`、`bufferObject` | 绑定图集/噪声/自定义图像/SSBO |

### 7.5 需要写进规格的常量与缓冲

`RENDERTARGETS`（现代）与遗留 `DRAWBUFFERS`、`BUFFER_CLEAR`/`BUFFER_CLEAR_COLOR`、`BUFFER_FORMAT`、
`COLORTEX_MIPMAPS`、`SHADOW_MIPMAPS`、`workGroups`/`workGroupsRender`；缓冲族：`colortex0-15`、`depthtex0-2`、
`shadowcolor0-1`、`shadowtex0-1`、`noisetex`、自定义 `image.*`、SSBO。

### 7.6 结论：兼容分层的现实目标

- **可支持**：`gbuffers_*` / `shadow` / `deferred` / `composite*` / `final` 的 vsh+fsh 子集、`colortex` 系列、
  常用 uniform、`alphaTest`/`blend`/`size.buffer`/`flip`、`dimension.properties`、profile 与 slider。
- **明确不支持（须回退并报错）**：`.csh` 计算、`.gsh` 几何、`.tcs/.tes` 细分、SSBO、自定义 `image`、Distant Horizons / Voxy / Colorwheel / Photonics。
- 依据：`iris.features.required` 若声明了不支持的特性，**直接拒绝并列出缺哪一项**，好过编译到一半失败。
