<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

<!-- doc
title: 实体渲染模块
summary: M5.3 P1 静态实体模型、渲染状态、部件几何与渲染/游戏逻辑解耦
status: 已评审
updated: 2026-11-02
-->

# 实体渲染模块（M5.3 P1）

## 1. 范围

本模块负责把实体渲染状态变为几何并提交渲染，不推进实体模拟、不读取网络、不修改世界。

P1 只做**静态姿态 + 朝向**，明确排除骨骼动画、物品装备渲染、皮肤叠加层的完整像素等效（装备/动画另立项）。

## 2. 解耦边界（硬约束）

原版实体渲染与游戏逻辑解耦：`EntityRenderer` 消费 `*RenderState`，不反向驱动实体。

本项目对应：

- `Common/Entities`：实体模拟、碰撞、移动；不引用渲染。
- `Client/World/EntityTracker`：把网络实体状态整理成 `EntityRenderState`（id、type、位置、yaw、pitch、UUID）；不引用 `Framework.Rendering`。
- `Client/Render`：只消费 `EntityRenderState` + `EntityModelDefinition`，产出中性几何；不读取 `ClientWorld`、不推进 tick。
- `Launcher/WindowCommand`：宿主只做状态到 draw 的装配；不得把实体逻辑写进渲染器。

未来实体渲染扩展（皮肤、装备、动画）也只能通过 `RenderState` 增加字段，不得让渲染器持有 `Entity`/`ClientWorld` 引用。

## 3. 原版模型证据

证据：`ref://mc-26.3-analysis/src/net/minecraft/client/model/HumanoidModel.java`、
`ref://.../model/player/PlayerModel.java`。

`HumanoidModel.createMesh` 的成年玩家部件（单位模型像素，1/16 block）：

| 部件 | 偏移（根坐标） | 立方体 min（相对部件） | 尺寸 |
| --- | --- | --- | --- |
| head | (0, 0, 0) | (-4, -8, -4) | 8×8×8 |
| hat（覆盖层） | 同 head | (-4, -8, -4) | 8×8×8，膨胀 0.5 |
| body | (0, 0, 0) | (-4, 0, -2) | 8×12×4 |
| right_arm | (-5, 2, 0) | (-3, -2, -2) | 4×12×4 |
| left_arm | (5, 2, 0) | (-1, -2, -2) | 4×12×4（镜像） |
| right_leg | (-1.9, 12, 0) | (-2, 0, -2) | 4×12×4 |
| left_leg | (1.9, 12, 0) | (-2, 0, -2) | 4×12×4（镜像） |

### 3.1 UV 与纹理

- 各部件使用实体皮肤图集的 `texOffs`：head `(0,0)`、body `(16,16)`、right_arm `(40,16)`、left_arm `(40,16)`（mirror）、right_leg `(0,16)`、left_leg `(0,16)`（mirror）。
- 立方体每面按 vanilla `CubeListBuilder` 的 UV 展开规则映射；UV 必须按实际皮肤纹理坐标，不可用占位色代替最终外观。
- 覆盖层（hat/jacket/sleeves/pants）在 P1 只保留模型结构，默认可见性由 `RenderState` 的部件可见性位控制；P1 先启用基础部件。
- 纹理由启动器从 jar 提取，仓库不存资产；提取失败时资源层必须给可诊断错误，不用硬编码 skin。

### 3.2 姿态

- P1 仅使用 `Yaw`/`Pitch` 做根节点朝向；`PlayerModel.setupAnim` 的骨骼动画不在 P1。
- 部件按上表偏移叠加到实体脚点；根旋转先 Yaw 后 Pitch（与现有 `LookConvention` 保持一致）。
- 名字牌锚点必须使用模型计算结果中的**头部/附着点**，不得再用占位的 `PlayerHeight + 0.5` 硬编码。

## 4. 数据与类型

建议新增（名字可在实现评审时调整）：

- `EntityModelDefinition`：`Parts[]`，每个 `Part` 有 `Name`、`Parent`、`Offset`、`Cubes[]`、`VisibleByDefault`。
- `EntityModelCube`：`Min`, `Size`, `TexOffset`, `Mirror`, `Inflate`。
- `EntityModelResolver`：按 `TypeId` 返回模型定义（P1 至少 player；其它 type 回退占位）。
- `EntityModelMesher`：`Build(ReadOnlySpan<EntityRenderState>, EntityModelDefinition, TextureRegion skin, pose)` → `DynamicGeometry`/中性顶点。

所有类型位于 `Client/Render`，不得位于 `Common/Entities`；`Framework` 只接受中性几何/纹理。

## 5. 渲染与纹理

- 实体纹理单独图集（skin atlas）或按材质分图集；P1 先接入一张 skin 纹理，图集容量不足时再评估纹理数组（pipeline §7.3）。
- 使用现有实体/方块兼容的顶点布局（位置 3 + 颜色 3 + UV 2 + 光照 2/共享顶点格式），保证可复用现有地形管线。
- P1 输出静态部件一次构建、按实体 draw；不引入动画顶点重算。

## 6. 测试与验收

1. 模型定义数值测试：每个部件的 offset/cube/UV 与 `HumanoidModel.createMesh` 逐值一致；
2. 网格测试：每盒 6 面、24 顶点/36 索引、镜像 UV 正确；
3. 渲染状态测试：`EntityRenderState` 只读消费，渲染层不得引用 world/entity 模拟类型（架构测试）；
4. 固定机位双客户端：两个玩家模型可见，名字牌锚点来自真实头部高度；与占位盒体对照记录偏差；
5. 纹理缺失/损坏有可诊断回退，不崩溃。

## 7. 当前状态

- [x] 渲染状态输入 `EntityRenderState` 与视锥剔除（T-344）
- [x] 世界空间名字牌（T-351）
- [ ] 玩家静态模型定义/网格/纹理接入（本模块下一片）
- [ ] 名称牌高度改为真实模型 `nameTagAttachment`
- [ ] 其它实体类型模型（P1 后）
