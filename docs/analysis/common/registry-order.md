<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

状态：[草稿]
基线：Minecraft 26.3
证据：`ref://mc-26.3-analysis/src/net/minecraft/core/MappedRegistry.java`、`.../core/RegistrySynchronization.java`、`.../core/IdMapper.java`、`.../resources/RegistryDataLoader.java`、`.../resources/{RegistryLoadTask,ResourceManagerRegistryLoadTask}.java`、`.../world/level/block/{Blocks.java,state/StateDefinition.java,state/properties/BooleanProperty.java}`
最后更新：2026-10-28

# 注册表顺序（Registry ordering）

"注册表元素顺序"不是实现细节：它决定网络注册表同步的包体顺序、方块状态全局 id、以及数据包加载后的可复现性。本文回答"顺序由什么决定"。

## 1. 结论速查

| 对象 | 顺序来源 |
| --- | --- |
| `MappedRegistry` 迭代 / `listElements()` | **注册顺序**（内部有序列表 `byId` 的下标，即 id 递增） |
| `keySet()` / `entrySet()` | 由 `HashMap` 派生，**无序**，不得当作契约 |
| 内置注册表（block / item / entity…） | **Java 静态初始化顺序**：`BuiltInRegistries` 字段声明顺序 + 各 `bootstrap` 内的注册调用顺序 |
| 数据包注册表（worldgen / damage_type…） | 加载前**按 `ResourceKey`（注册表键, 标识符）排序**，再逐个注册 |
| 网络同步包内的元素顺序 | 注册顺序（`listElements()`） |
| 方块状态全局 id | 遍历方块注册顺序 × 每个方块的状态枚举顺序，逐个 `add` 到 `IdMapper` |

## 2. 内部结构与遍历

- 有序容器只有一个：`byId`（`ObjectArrayList<Holder.Reference<T>>`）；`iterator()` 与 `listElements()` 都走它。
- `byLocation` / `byKey` / `byValue` 都是 `HashMap` / `IdentityHashMap`；通过它们得到的集合**顺序不稳定**。
- 注册时追加到 `byId` 末尾，`toId` 记录 `对象 → id`；id 单调递增。
- 重复标识符注册直接抛 `IllegalStateException`（"Adding duplicate key"）。
- `freeze()` 后拒绝继续写入。

对我们的实现含义：**`Registry` 的公开 API 必须暴露"有序枚举"，不能直接暴露字典的枚举顺序**；否则同一份数据在 Java 与 C# 上会得到不同顺序。

## 3. 内置注册表：顺序 = 代码顺序

- `BuiltInRegistries` 里每个注册表是 `static final` 字段，字段按声明顺序初始化；`registerSimple(..., Xxx::bootstrap)` 在创建时立即执行该 `bootstrap`，注册项因此按调用顺序进入。
- 这意味着 block / item 的 id 顺序 = **`Blocks` / `Items` 里 `register(...)` 的书写顺序**，跨类的相对顺序由类初始化触发顺序决定。
- **不可依赖** C# 的反射顺序、字段顺序或源代码文件顺序来"自然得到"同样的顺序。必须维护**显式顺序表**（例如按注册名排序的清单文件或代码内固定顺序），并配一条"顺序快照"测试把它钉住。

## 4. 数据包注册表：排序后注册

`ResourceManagerRegistryLoadTask` 在注册前对加载结果做 `sorted(Entry.comparingByKey())`，`ResourceKey` 的比较是"先注册表键、后标识符"。因此同一注册表内，元素 id 顺序 = **标识符字典序**。

这带来一个便利：数据包注册表天然可复现，不依赖文件系统枚举顺序。我们的加载器必须复刻这条排序（不要依赖目录枚举顺序）。

## 5. 方块状态全局 id（协议关键）

`Blocks` 的静态初始化块做两件事，顺序固定：

1. 遍历 `BuiltInRegistries.BLOCK`（= 方块注册顺序）；
2. 对每个方块，遍历 `getStateDefinition().getPossibleStates()`，逐个 `BLOCK_STATE_REGISTRY.add(state)`。

`IdMapper.add` 的实现就是"取当前 `nextId`，自增"，`byId` 是稀疏列表。所以全局状态 id 完全由前两步的遍历顺序决定。

`getPossibleStates()` 的顺序同样不是随机的：

- 属性集合是 `ImmutableSortedMap`（**按属性名字典序**）；
- 状态 = 属性取值列表的**笛卡尔积**，属性顺序即上面的排序结果；
- 每个属性的取值顺序由该属性决定：
  - `BooleanProperty`：**`[true, false]`**（真在前，很容易写反）；
  - `IntegerProperty`：`min..max` 升序；
  - `EnumProperty`：枚举声明顺序。

因此"方块状态 id"的完整链条是：**方块注册顺序 → 属性名排序 → 各属性取值顺序 → 笛卡尔积顺序**。任何一环与 Java 不同，区块数据里的状态 id 就会整体错位。

## 6. 网络同步

`RegistrySynchronization.packRegistry` 通过 `registry.listElements()` 逐个打包（按注册顺序），并可能因"客户端已知数据包"而省略元素内容（只发键）。因此：

- 包内元素顺序 = 注册顺序；
- 元素内容的可选性由 `KnownPack` 协商决定，不影响顺序。

## 7. 对 C# 实现的约束

1. `Registry<T>` 提供 `IReadOnlyList<Entry>`（有序）作为唯一枚举入口；字典只用于查找，不对外暴露枚举。
2. 内置注册表需要**显式顺序表**：建议由数据驱动（资源清单）或固定代码顺序生成，并在启动时做"顺序哈希"自检，发现漂移立即失败。
3. 数据包注册表加载 MUST 先按 `(registryKey, identifier)` 排序再注册。
4. `IdMapper<T>` 需要等价实现：`Add` 递增赋值、`ById` 稀疏访问、`GetId` 反查；并与注册顺序联动。
5. 方块状态 id 生成器 MUST 按 §5 的四级顺序实现，并配"给定方块清单 → 状态 id 快照"测试。
6. 严禁用 `Dictionary<TKey,TValue>` 的枚举顺序、`HashSet` 顺序、并行注册的完成顺序来构造任何有序结构。

## 7.1 实测数据（2026-10-28，官方 jar）

用同一套"跑 jar"的方式导出，作为顺序契约的真值：

| 观察项 | 值 |
| --- | --- |
| `BuiltInRegistries.BLOCK` 元素数 | **1286**（首个 `minecraft:air`，末个 `minecraft:firefly_bush`） |
| 方块状态全局 id 总数 | **35723** |
| `minecraft:grass_block[snowy=true]` | id **8** |
| `minecraft:grass_block[snowy=false]` | id **9** |

最后两行**实证了 §5 的推断**：`BooleanProperty` 的取值顺序是 `[true, false]`（真在前）。这是最容易写反的一处，且一旦写反，整类方块的状态 id 会整体偏移。

方块顺序快照入库为 `tests/Sharpcraft.Common.Tests/TestData/vanilla-block-order.txt`（纯标识符清单，不含资产），由 `RegistryOrderTests` 钉住"id 跟随注册顺序且顺序非字典序"。

## 8. 待验证

- `BuiltInRegistries` 中是否存在**非** `static final` 字段或惰性初始化路径，使个别注册表顺序晚于声明顺序（本轮抽样确认了 block/item/entity 等主表，未全量核对）。
- 各注册表的具体注册清单与顺序尚未抄录；这是"顺序快照"测试的输入，需要另立一份数据文件（属于分析产物，可放仓库，因为它是标识符清单而非游戏资产）。
- `EnumProperty` 的取值顺序需按每个枚举逐一核对（声明顺序是默认假设，未逐个验证）。
