// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

<!-- doc
title: 原版实体模型几何事实基线
summary: 26.3 ModelPart/PartPose/CubeDeformation/MeshTransformer 的已核实行为事实，以及对本项目 DSL 与门禁的约束
status: 已评审
updated: 2026-11-07
-->

# 原版实体模型几何事实基线

## 1. 目的与范围

本文把 26.3 原版实体模型的关键几何行为固定为项目内部事实，作为 `VanillaMeshDefinition` DSL、模型迁移测试和后续几何门禁的共同依据。

本文只记录**行为、常量、符号约定与数据关系**，不复制原版实现代码。所有结论均来自本机分析工作区的 `ref://mc-26.3-analysis`。

覆盖范围：

- `ModelPart` 的局部变换与父子继承；
- `PartPose` 的 pivot / 欧拉角 / 三轴 scale；
- `CubeDeformation` 的标量/三轴 grow；
- `CubeDefinition` 的 UV 展开；
- `MeshTransformer.scaling` 的变换边界；
- 幼年鞘翅与幼年僵尸的已核实常量。

不覆盖：动画时间轴、装备资产 JSON、渲染 Pass 顺序。

## 2. 原版源码映射表

| 主题 | 原版类与方法 | `ref://` |
| --- | --- | --- |
| 模型节点变换 | `ModelPart.translateAndRotate`、`ModelPart.render` | `ref://mc-26.3-analysis/src/net/minecraft/client/model/geom/ModelPart.java` |
| 局部姿态 | `PartPose`（offset/rotation/xScale/yScale/zScale） | `ref://mc-26.3-analysis/src/net/minecraft/client/model/geom/PartPose.java` |
| 三轴膨胀 | `CubeDeformation(growX, growY, growZ)` | `ref://mc-26.3-analysis/src/net/minecraft/client/model/geom/builders/CubeDeformation.java` |
| 多 cube 构建 | `CubeListBuilder`（`texOffs`/`mirror`/`addBox`） | `ref://mc-26.3-analysis/src/net/minecraft/client/model/geom/builders/CubeListBuilder.java` |
| 部件树定义 | `PartDefinition`（cube 列表 + children map） | `ref://mc-26.3-analysis/src/net/minecraft/client/model/geom/builders/PartDefinition.java` |
| 部件树烘焙 | `PartDefinition.bake`、`ModelPart`（cube 列表 + children map） | `ref://mc-26.3-analysis/src/net/minecraft/client/model/geom/builders/PartDefinition.java`、`ref://.../ModelPart.java` |
| 模型层烘焙 | `EntityModelSet.bakeLayer` | `ref://mc-26.3-analysis/src/net/minecraft/client/model/geom/EntityModelSet.java` |
| 立方体与 UV | `CubeDefinition.bake`、`ModelPart.Cube` | `ref://mc-26.3-analysis/src/net/minecraft/client/model/geom/builders/CubeDefinition.java`、`ref://.../ModelPart.java` |
| 缩放变换 | `MeshTransformer.scaling(factor)` | `ref://mc-26.3-analysis/src/net/minecraft/client/model/geom/builders/MeshTransformer.java` |
| 根节点变换边界 | `MeshDefinition.transformed`、`PartDefinition.transformed` | `ref://.../builders/MeshDefinition.java`、`ref://.../builders/PartDefinition.java` |
| 幼年鞘翅 | `ElytraModel.createLayer`、`setupAnim` | `ref://mc-26.3-analysis/src/net/minecraft/client/model/object/equipment/ElytraModel.java` |
| 鞘翅层挂点 | `WingsLayer.submit` | `ref://mc-26.3-analysis/src/net/minecraft/client/renderer/entity/layers/WingsLayer.java` |
| 幼年僵尸尺寸 | `BabyZombieModel.createBodyLayer` | `ref://mc-26.3-analysis/src/net/minecraft/client/model/monster/zombie/BabyZombieModel.java` |
| 幼年护甲非均匀膨胀 | `LayerDefinitions` 的 `BABY_*_ARMOR_DEFORMATION` | `ref://mc-26.3-analysis/src/net/minecraft/client/model/geom/LayerDefinitions.java` |

## 3. 已核实的行为事实

### 3.1 `ModelPart` 的局部变换顺序与坐标系

原版每个 `ModelPart` 都按同一顺序应用局部姿态：

1. 平移到 `(x/16, y/16, z/16)`；
2. 若任意旋转非零，应用 `rotationZYX(zRot, yRot, xRot)`；
3. 若任意轴 scale 非 1，应用 `scale(xScale, yScale, zScale)`；
4. 再绘制本节点的 cubes，最后递归绘制 children。

关键结论：

- **根节点和子节点使用完全相同的变换顺序**，原版没有 root/child 两套旋转符号约定。
- 模型局部坐标是 **y-down 模型像素**；1 模型 px = 1/16 block。
- `PartPose` 携带三轴 scale；它参与父链继承，而不是直接改变 CubeDefinition 的尺寸。

### 3.2 `CubeDeformation` 支持三轴非均匀 grow

原版 `CubeDeformation` 的事实：

- 存在 `(growX, growY, growZ)` 三参数构造；
- 也存在单参数构造，等价于三轴相同；
- `extend` 可以对三轴分别累加。

在当前 26.3 模型集中，绝大多数 cuboid 使用标量或 `NONE`；已核实的非均匀使用只有幼年护甲层：

| 常量 | growX | growY | growZ |
| --- | ---: | ---: | ---: |
| `BABY_OUTER_ARMOR_DEFORMATION` | -0.1 | 0.5 | 0.3 |
| `BABY_INNER_ARMOR_DEFORMATION` | -0.1 | 0.3 | 0.3 |

这证明“grow 只能是标量”的假设不成立；DSL 与装备层若要完整覆盖原版，必须表达三轴 grow。

### 3.3 Cube UV 使用未膨胀的原始尺寸

`ModelPart.Cube` 的 UV 展开固定为：

- `u0 = texU`
- `u1 = u0 + depth`
- `u2 = u1 + width`
- `u3 = u2 + depth`
- `u4 = u3 + width`
- `v0 = texV`
- `v1 = v0 + depth`
- `v2 = v1 + height`

`growX/Y/Z` 只扩大几何的 min/max，不改变上述 UV 计算。

因此“几何尺寸”和“UV 采样尺寸”必须分离；这正是 `EntityModelPart.UvWidth/UvHeight/UvDepth` 的事实基础。

### 3.4 `MeshTransformer.scaling` 只变换根 pose

`MeshTransformer.scaling(factor)` 的事实：

- `yOffset = 24.016 * (1 - factor)`；
- 只对 `MeshDefinition` 的 **root `PartPose`** 应用 `scaled(factor)` 与 `translated(0, yOffset, 0)`；
- 子节点的 `CubeDefinition` 尺寸、UV、grow 均保持不变；
- 子节点世界几何的变化来自父 pose 的缩放继承。

这不是“把每个扁平化 part 的 Width/Height/Depth 都乘以 factor”。对只有 root 部件的模型（例如 Elytra 两翼）两种写法可等价；对嵌套模型则不能直接套用扁平化缩放。

### 3.5 幼年鞘翅的精确事实

`ElytraModel.createLayer`：

| 项 | 值 |
| --- | --- |
| 左翼 pivot | `(5, 0, 0)` |
| 右翼 pivot | `(-5, 0, 0)` |
| cube | `(-10, 0, 0)`、尺寸 `10×20×2`、grow `1.0` |
| 纹理偏移 | `texOffs(22, 0)` |
| 层纹理尺寸 | `64×32` |
| 幼年变换 | `MeshTransformer.scaling(0.5)` |

`WingsLayer.submit` 在提交鞘翅模型前额外 `translate(0, 0, 0.125)`，即 **+2 模型 px**。

`ElytraModel.setupAnim`：

- `leftWing.y = crouching ? 3.0 : 0.0`；
- 右翼 `y` 与左翼相同；
- 右翼 `yRot/zRot` 取左翼相反数，`xRot` 相同；
- `3.0` 位于 root `0.5` 缩放内部，实际世界下移 **1.5 px**。

当前实现的 `+2 px` layer Z、`-1.5f` 潜行偏移、`UvWidth/UvHeight/UvDepth = 10/20/2` 与此一致；无需重做。

### 3.6 幼年僵尸的身体/头部事实

`BabyZombieModel.createBodyLayer`：

| 部件 | pivot | cube min | cube 尺寸 |
| --- | --- | --- | --- |
| body | `(0, 17.5, 0)` | `(-2, -2.5, -1)` | `4×5×2` |
| head | `(0, 15.25, 0)` | `(-3, -6.25, -3)` | `6×6×6` |
| head 外层 | 同 head | `(-3, -6.15, -3)` | `6×6×6`，grow `0.25` |

在项目的 y-up 存储中，这对应 body 的 `Bottom = 4`、`Height = 5`、`bodyTop = 9`；head 的 `Bottom = 9`、`Height = 6`。

因此幼年背部装备的挂点不得高于 `bodyTop`；幼年鞘翅应以 `bodyTop` 为上限/锚点，而不是硬编码 12。

### 3.7 原版模型管线没有扁平化阶段

原版的模型构建与运行结构始终是树，而不是“一个 cuboid 一个 part”的扁平数组：

- `CubeListBuilder` 内部维护 `List<CubeDefinition>`；`texOffs`/`mirror` 是构建状态，`addBox` 每次追加一个 cube，所以一个 part 可以拥有多个 cube。
- `PartDefinition` 持有 `List<CubeDefinition>` 和 `Map<String, PartDefinition> children`；`bake` 递归烘焙 children，再把当前 part 的 cube 列表整体烘焙成 `List<ModelPart.Cube>`。
- `ModelPart` 持有 `List<ModelPart.Cube> cubes` 和 `Map<String, ModelPart> children`；渲染时先应用一次当前 part 的 `translateAndRotate`，绘制该 part 的所有 cube，再递归 children。
- `EntityModelSet.bakeLayer` 返回的是 `LayerDefinition.bakeRoot()` 的 `ModelPart` 根节点，不经过任何扁平数组转换。

因此：

- **原版没有“每 cube 一个 part”的表示**；
- 原版的一个 `ModelPart` 可以同时拥有多个 cube 和多个 children；
- 当前项目的 `VanillaPartDefinition` 树已经保留了原版结构；真正造成信息丢失的是 `VanillaMeshBake.Bake` 的 `EmitCubes`——它为每个 cube 创建一个 `EntityModelPart`，并把父链降级为字符串 `Parent`。

结论：扁平化只能是**渲染批次边界的可选展开步骤**，不能是模型定义的唯一存储形态。后续若要满足“写生物时保有结构”，应让 `VanillaMeshDefinition` / `EntityModelPart` 的树成为唯一模型真相源，把扁平数组限定为某一个集中方法的输出。

## 4. 对本项目 DSL 与门禁的约束

### 4.1 已验证成立

- `EntityModelPart.UvWidth/UvHeight/UvDepth` 与 `ModelPart.Cube` 的“UV 不随 grow/几何缩放变化”事实一致。
- 幼年鞘翅当前挂点、`+2` px Z、`-1.5` px 潜行偏移与 `ElytraModel`/`WingsLayer` 事实一致。
- `ModelUvInvariantTests` 的 adult/baby UV 跨度一致性检查有原版依据。

### 4.2 当前 DSL 的真实缺口

| 缺口 | 事实依据 | 需要 |
| --- | --- | --- |
| `VanillaCubeDefinition` 只有标量 `Grow` | `CubeDeformation` 支持三轴 | 增加 `Vector3? GrowXyz` 并在 Bake 中应用 |
| `VanillaPartPose` 只有 offset/rotation，无三轴 scale | `PartPose` 带 `xScale/yScale/zScale` | 增加三轴 scale，并让 Bake 参与父链 |
| `EntityModelLayer` 只有标量 `Grow` | 幼年护甲使用三轴 deformation | 增加 `GrowXyz`，并明确 overlay 不继承宿主 `GrowXyz` |
| `VanillaMeshBake.Scale` 是对扁平 part 的缩放 | `MeshTransformer.scaling` 只改 root pose | 仅限 root-only 模型使用；不要推广为通用 transformer |

### 4.3 旋转符号不能先按文档全局改

原版 `ModelPart` 对所有节点都使用 `rotationZYX(z, y, x)`，没有 root/child 两套规则。

当前项目 `BakeRoot` 保存原始 `rotationX/Y`，`BakeChild` 保存 `-rotationX/-rotationY/+rotationZ`。这可能是正确的坐标共轭，也可能掩盖了历史补丁；必须先用原版 Golden Vector 验证，尤其是同时包含 root 和 child 非零旋转的模型（Fox/Rabbit/Spider/Ravager）。

在验证前：

- 不得全局修改旋转符号；
- 不得去掉现有的逐模型符号处理；
- 新增模型必须先用事实提取器生成期望顶点。

### 4.4 Z-fighting / clearance 门禁的边界

原版存在大量：

- 有意的共面覆盖层（hat/jacket/armor overlay）；
- 透明层与主体同位置；
- 幼年/成年不同包围盒。

因此 Z-fighting 与装备 clearance 门禁必须先做“参考基线校准”：

- 以原版模型事实和允许的透明/覆盖层建立 allowlist；
- 阈值、法向容差、平面距离必须从参考模型实测得出；
- 不能直接把审查文档中的 `0.02`、`10^-4` 等数值当作已批准门禁。

## 5. 后续验证清单

1. **已实现（2026-11-07，T-707）**：`tools/Sharpcraft.ReferenceVerifier` 可扫描 `mc-26.3-analysis` 并输出纯数字事实快照：
   - `dotnet run --project tools/Sharpcraft.ReferenceVerifier -- --write`
   - 默认输出 `artifacts/reference/vanilla-model-facts.json`（本地生成、被 `.gitignore` 覆盖，不进仓库）；
   - 默认覆盖 Elytra、Warden、Breeze、AdultRabbit、AdultFox；后续按解析器覆盖逐批扩展到全部 model 类；
   - 输出只包含类名、`ref://` 路径、part 名/父链、`PartPose` 数值、`CubeDefinition` 数值与 grow，不包含 Java 源码或注释。
2. **已实现（2026-11-07，T-708）**：`tests/Sharpcraft.Client.Tests/Render/ReferenceModelFactsTests.cs` 读取本文件的数值事实并对比 Warden、AdultFox、AdultRabbit 的 DSL 输出；Elytra 额外断言 10×20×2 / `texOffs(22,0)` / `grow 1` / ±π/12 常量。Golden 测试发现了两个旧迁移遗漏：
   - Warden `left_ribcage` 缺少原版的 `mirror`（已在 `WardenModelDefinition` 修复）；
   - AdultFox 四条腿缺少 `CubeDeformation(0.001)`（已在 `FoxModelDefinition` 修复）。
   Breeze 暂不做 part-by-part 对比：原版 `head`/`eyes`/`wind_mid`/`wind_top` 是多 cube part，当前扁平表示拆成伪 part，需要几何级 Golden Vector 再覆盖。
3. 为 BabyZombie + Elytra、Fox、Rabbit、Spider、Warden 生成原版 Golden Vector。
4. 依据事实补齐 DSL 的三轴 grow / scale，再迁移仍有非均匀 deformation 的装备层。
5. 在 Golden Vector 通过后，再决定是否统一 root/child 旋转符号。
6. 最后才建立 Z-fighting / clearance 门禁，并对已知合法覆盖层登记 allowlist。

## 6. 结论

原版事实支持的优先级是：

1. 先补 **事实提取器与 Golden Vector**；
2. 再补 **DSL 的三轴 grow/scale**；
3. 再验证 **root/child 旋转共轭**；
4. 最后才写 **Z-fighting / clearance** 门禁。

不应直接从审查文档的缺陷描述跳到实现或门禁；先以本文件的事实与 Golden Vector 为准。
