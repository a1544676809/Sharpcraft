<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

状态：[待评审]
基线：Minecraft 26.3
最后更新：2026-10-31
授权：用户 2026-10-31 指示"先写成模块文档，这是重要决策"。评审通过前不得据此开工（AGENTS 3.5）。

# 方块状态模块（Block State）

把协议里的**全局方块状态 id** 等价、零分配地解码成（方块 + 属性取值），并支持反向编码，作为渲染、流体、碰撞、模型匹配等一切"按状态分派"的公共基础。

## 1. 职责范围

负责：

- 维护**方块注册顺序 + 每状态的全局 id**（对齐原版 id 分配规则），即 `BlockStateLayout`；
- 维护**每方块的属性定义**（属性名、类型、取值、取值顺序、stride）并据此**解码/编码状态 id ↔ 属性取值**；
- 提供**流体状态派生**：由方块状态求 `amount / falling / ownHeight / isSource`（供流体渲染）。

明确不负责：

- 方块**行为/逻辑**（刻更新、碰撞形状、掉落等）——只提供状态数据；
- 方块**模型/纹理**解析（属资产/渲染模块，`asset-resolution`、`render`）；
- 方块状态的**网络增量同步策略**（`section_blocks_update` 等，属世界/网络模块）。

## 2. 侧别归属

- 归属：`Common`（跨 Client/Server 共享的数据与注册表；渲染热路径也只读不可变表）。
- 判定依据：方块状态 id 与属性是协议与两侧逻辑共用的数据（`docs/analysis/common/registry-order.md §5`）；渲染只依赖其结果，不推进世界。

## 3. 依赖关系

| 方向 | 对象 | 说明 |
| --- | --- | --- |
| 依赖 | `Common.Identifiers`（`Identifier`） | 方块/属性标识 |
| 依赖 | 内嵌注册表快照（数据，非资产） | 注册顺序、状态数、属性定义 |
| 被依赖 | `Client`（渲染/流体/模型）、`Server`（后续） | 只读消费 |

依赖方向自检：不得出现 Client↔Server 互引，也不得让游戏逻辑引用外围宿主层。

## 4. 对外契约

- `BlockStateLayout`：`id → blockIndex/BaseId/StateCount`，`(block, offset) → id`（已存在）。
- `BlockStateTable`（本模块核心，拟新增）：全局状态 id ↔ 属性取值的扁平解码/编码。
  - `int BlockOf(int stateId)`、`int OffsetOf(int stateId)`；
  - `int GetValue(int stateId, PropId prop)` / `bool TryGetInt(int stateId, PropId prop, out int value)`；
  - `int With(int stateId, PropId prop, int code)`（无分配）；
  - `FluidState FluidStateOf(int stateId)`。
- 明确不暴露：`Dictionary`/`HashSet` 的枚举顺序；任何**运行期排序/反射**得到的顺序。

## 5. 关键数据结构

| 结构 | 用途 | 生命周期 | 所属线程 |
| --- | --- | --- | --- |
| `BlockStateLayout` | 方块基址与状态数，id 范围 | 进程级缓存 | 只读，多线程 |
| `BlockDefinition`（struct） | 每方块：BaseId、StateCount、属性数组（已按名排序） | 加载期构建，不可变 | 只读 |
| `PropDef`（struct） | 属性名（`PropId`）、Kind、Min、Radix、**Stride**、可选取值名 | 加载期构建，不可变 | 只读 |
| `ushort[] stateToBlock` | `stateId → blockIndex`，O(1) | 加载期构建，不可变 | 只读 |
| `uint[]? statePackedProps` | 热属性 code 按位域打包，O(1) 取（可选） | 加载期构建，不可变 | 只读 |
| `BlockState`（record struct） | 全局 id 的薄封装 | 值类型 | 任意 |

内存量级（基线 1,286 方块 / 35,723 状态）：`stateToBlock` ≈ 71 KB；`baseId[]` ≈ 5 KB；`statePackedProps` ≈ 143 KB。

## 6. 行为对齐与偏离

对齐点（证据见 `docs/analysis/common/registry-order.md §5`）：

- 状态 id 分配链：**方块注册顺序 → 属性名排序 → 各属性取值顺序 → 笛卡尔积（末属性最快）**；
- 取值顺序：`BooleanProperty=[true,false]`、`IntegerProperty=min..max 升序`、`EnumProperty=枚举声明顺序`；
- 编译期把上述顺序**固化进数据快照**，运行期不再排序。

**有意偏离（实现层面，非可观测行为）**：

- **不采用 Java 的 `StateHolder` / `ImmutableSortedMap<Property,Comparable>` 对象图与装箱**。改用具值结构 + 扁平表 + 预计算 `stride` 的整数运算：
  `offset = Σ code[p] * stride[p]`（末属性 `stride=1`）。
  理由：原版形态强绑定 JVM（每状态一个映射对象、`Comparable` 装箱、类初始化笛卡尔积建对象），在 .NET 会成为 mesher 每帧数十万次查询的**分配/装箱/字典开销**与 GC 压力。此为性能驱动的偏离（原则 9），须登记受控偏差并附基准。
- 属性名**intern 成 `PropId`(int)**，运行期不比较字符串；字符串只用于诊断/模型匹配。

## 7. 未解决问题与风险

- **通用属性定义数据的生成**：现有快照只有 `index / id / 状态数`，缺每方块属性定义。需离线工具从原版数据产出（属注册表元数据，非资产）。Enum 取值"声明顺序"难以从 JSON 直接恢复，需专门核对（`registry-order.md §5` 已标注该假设）。
- **流体先行**：`minecraft:water` / `minecraft:lava` 各 16 状态、唯一属性 `level` 0..15 ⇒ `level = id - BaseId`，可先不解全表就跑通流体（见 §8 步骤 A）。
- **模组/服务端自定义注册表**：内嵌快照只覆盖原版 26.3；`registry_data` 包在可用时是权威，需在服务端追加/重排方块时对齐或降级。
- **热路径分配**：`GetValue/With/FluidStateOf` 必须零分配，需 `[HotPath]` + 分配测试守护；属性访问若退化为字典查找即视为回归。

## 8. 落地步骤（评审通过后）

- A（流体所需，最小）：`BlockStateDefinition("level",0..15)` + `FluidStateOf(stateId)`，用现有 `BlockStateLayout` 求 `level = id - base`，配 water/lava 单测（level→amount/falling/ownHeight）。
- B（通用）：`BlockStateTable` 扁平表 + stride + 可选 packed 属性落地，替换临时解码，加**全 35,723 状态 `id ↔ (block,props)` 往返**等效测试与零分配测试。
- C：供流体网格（四角高度/flow-UV/水色/逐面光/overlay）与后续方块消费。

## 9. 参考资料

- `ref://mc-26.3-analysis/src/net/minecraft/world/level/block/state/StateDefinition.java`
- `ref://mc-26.3-analysis/src/net/minecraft/world/level/block/state/StateHolder.java`
- `ref://mc-26.3-analysis/src/net/minecraft/world/level/block/state/properties/{BooleanProperty,IntegerProperty,EnumProperty}.java`
- `ref://mc-26.3-analysis/src/net/minecraft/world/level/block/Block.java`（`BLOCK_STATE_REGISTRY`）
- `docs/analysis/common/registry-order.md` §5
- `src/Sharpcraft.Common/Registries/{BlockStateLayout,BlockStateDefinition,BlockStateProperty,VanillaBlockRegistry}.cs`
