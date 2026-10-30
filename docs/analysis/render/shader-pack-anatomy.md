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
