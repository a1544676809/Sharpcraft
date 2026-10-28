<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 MinecraftCSharp contributors -->

状态：[草稿]
基线：Minecraft 26.3
证据：`ref://mc-26.3-analysis/src/net/minecraft/resources/Identifier.java`（反编译工作区，仓库外）
最后更新：2026-10-28

# 模块：Common 基础类型（common-primitives）

## 1. 职责

提供全项目最底层、无副作用、无 IO 的值类型与容器。它是一切上层模块的地基：任何其它模块都可以引用它，它不引用任何其它模块（除 BCL）。

负责：标识符、注册表键与注册表容器、坐标与几何基元、随机数封装。
不负责：世界数据内容、协议编解码（另立 `common-network`）、渲染数学（Framework 侧）。

## 2. 侧别归属

`Common`。判定依据：`net.minecraft.resources.Identifier`、`core` 注册表、`world.phys` 三者的客户端依赖均为 **0**，而两侧均在消费（见 `boundaries.md` 第 3 节）。

## 3. 依赖关系

| 方向 | 对象 | 说明 |
| --- | --- | --- |
| 依赖 | 无（仅 BCL） | 保证 Common 不反过来拖入引擎 |
| 被依赖 | Common 其它模块、Client、Server | 全部 |

## 4. 对外契约（首批）

### 4.1 `Identifier`（对应原版 `net.minecraft.resources.Identifier`）

原版行为规格（已核对 26.3 源码）：

| 项 | 规则 |
| --- | --- |
| 分隔符 | `:`（常量 `NAMESPACE_SEPARATOR`） |
| 默认命名空间 | `minecraft` |
| 专用命名空间 | `realms`（用于 Realms 内容） |
| 命名空间字符集 | `[a-z0-9_.-]` |
| 路径字符集 | `[a-z0-9/._-]` |
| 解析形态 | `namespace:path`；无冒号 → `minecraft:path`；`":path"`（空命名空间）→ 走默认命名空间 |
| 非法字符 | 抛 `IdentifierException`，消息区分命名空间与路径两类 |
| 序列化 | 字符串形式 `namespace:path`；网络编码为 UTF-8 字符串 |

已闭环（详见 [`docs/analysis/common/identifier.md`](../analysis/common/identifier.md)）：

- 等值：命名空间与路径都相等；哈希构成 `31 × hash(namespace) + hash(path)`。
- 排序：**先比 path 再比 namespace**（直接决定注册表遍历顺序）。
- 长度与空串：**无长度上限**，且**空路径合法**；命名空间单独拒绝字面量 `..`。

设计取向：`readonly struct` 持有两个 `string`，解析走 `ReadOnlySpan<char>` 校验以避免中间分配；提供 `TryParse`（不抛）与 `Parse`（抛）双入口。哈希数值不必与 Java 相同（不跨协议边界），但**等值与排序语义必须一致**。

### 4.2 `RegistryKey<T>` 与 `Registry<T>`

已闭环（详见 [`docs/analysis/common/registry-order.md`](../analysis/common/registry-order.md)）：

- 迭代 / 有序枚举 = **注册顺序**（内部有序列表下标），`keySet` / `entrySet` 的字典顺序**不是契约**。
- 内置注册表顺序 = 代码中的注册调用顺序（不是排序、不是字典序）→ C# 侧必须准备**显式顺序表**并在启动时做顺序自检。
- 数据包注册表 = 加载前按 `(registryKey, identifier)` **排序后注册** → 天然可复现。
- 网络同步按注册顺序打包；方块状态全局 id = 方块注册顺序 × 状态笛卡尔积顺序（属性名排序、`BooleanProperty` 取值 `[true,false]`）。

契约要求：`Registry<T>` 只暴露 `IReadOnlyList<Entry>` 作为枚举入口；字典仅用于查找，不得对外暴露枚举顺序。

### 4.3 坐标与几何基元

- `BlockPos`（整数三元组）、`Vec3`（双精度三元组）、`Aabb`。
- 必须与原版一致的点：整数/浮点语义、`BlockPos` 到区块坐标的位运算取整方式（负数取整是常见错误源，需专门测试）。

### 4.4 随机数

已闭环（详见 [`docs/analysis/common/random.md`](../analysis/common/random.md)）。结论要点：

- 必须实现三类算法，且各自逐位一致：**48 位 LCG**（`RandomSource.create` 的默认返回，实体/方块刻/粒子用）、**xoroshiro128++**（世界生成与战利品序列）、以及种子升级混合（Stafford 13 + MD5 摘要）。
- 同一 `nextInt(bound)` 在两种算法上语义不同，不可共用实现。
- 战利品走 `RandomSequence`：`(世界种子, 战利品表 ID)` 派生独立序列，与调用顺序无关。

分层：`RandomSource` 门面 + `LegacyRandom` + `Xoroshiro128PlusPlus` + `PositionalRandomFactory`（两实现）+ `WorldgenRandom` + `RandomSequence`。每个实现必须配"参考向量测试"（固定种子断言前若干个输出值）。

## 5. 关键数据结构

| 结构 | 用途 | 生命周期 | 线程 |
| --- | --- | --- | --- |
| `Identifier` | 全局命名引用 | 值类型，随用随建/缓存 | 任意（不可变） |
| `Registry<T>` | 内容注册表 | 启动时构建，加载后冻结 | 构建期单线程，冻结后只读 |

## 6. 行为对齐与偏离

对齐点：字符集与解析规则、默认命名空间、异常语义（区分类别）、序列化格式。
允许偏离：提供 `TryParse`、内部零分配解析、`Identifier` 用结构体（原版为对象）。这些不改变可观测行为与协议字节。
禁止偏离：排序语义、哈希/等值语义、注册表遍历顺序。

## 7. 未解决问题与风险

- 开工前的四项阻塞已闭环（三处 `[待验证]` + 一处 `[待分析]`），证据见 `docs/analysis/common/`。
- 仍待确认：注册表元素顺序的决定因素（`RegistryKey`/`Registry` 一节），它影响协议包体与存档一致性，**不得**在弄清前实现注册表。
- `Identifier` 采用结构体后需评估字典键的哈希成本；`Equals`/`GetHashCode` 手写，等值语义必须与 Java 一致，哈希数值可不同。
- 随机数实现的主要风险是 Java 语义细节（`unchecked` 溢出、符号扩展、`>>>` 与 `>>`）；参考向量测试是唯一可靠的验收手段，必须在实现同时落地。
- 与 `docs/specs/network/` 的交界：字符串编解码的字节级细节属于协议规格，不在本模块。

## 8. 参考资料

- `docs/architecture/boundaries.md`（侧别判定）
- [`docs/analysis/common/identifier.md`](../analysis/common/identifier.md)
- [`docs/analysis/common/random.md`](../analysis/common/random.md)
- `docs/specs/network/`（后续产出，届时补链接）
