<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 MinecraftCSharp contributors -->

状态：[草稿]
基线：Minecraft 26.3
证据：`E:\mcanalysis\26.3\src\net\minecraft\resources\Identifier.java`（反编译工作区，仓库外）
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

`[待验证]` 以下三点尚未核实，实现前必须确认：等值/哈希的构成、`compareTo` 的排序语义（影响注册表有序同步）、路径是否允许空串或存在长度上限。

设计取向：`readonly struct` 持有两个 `string`，解析走 `ReadOnlySpan<char>` 校验以避免中间分配；合法性校验失败用**不抛异常**的 `TryParse` 与抛异常的 `Parse` 两个入口（原版只有抛异常版本，属允许的实现差异，不改变可观测行为）。

### 4.2 `RegistryKey<T>` 与 `Registry<T>`

- 键 = `(registryNamespace:path, valueIdentifier)` 的组合；容器保证 `Identifier → T` 单射。
- 必须支持：按标识符查找、按标识符前缀过滤、**有序遍历**（顺序必须可复现，影响协议与数据包加载）、冻结（加载完成后拒绝再注册）。
- `[待验证]` 原版注册表的元素顺序由什么决定（注册顺序 / 排序后的标识符），实现前需确认，因为它决定协议包体与存档一致性。

### 4.3 坐标与几何基元

- `BlockPos`（整数三元组）、`Vec3`（双精度三元组）、`Aabb`。
- 必须与原版一致的点：整数/浮点语义、`BlockPos` 到区块坐标的位运算取整方式（负数取整是常见错误源，需专门测试）。

### 4.4 随机数

- `[待分析]` 原版使用的随机源与算法。**不得**用 `System.Random` 顶替：世界生成与战利品必须与原版序列一致，否则同种子不同结果。此项在实现前必须补齐分析，未确认前不提供该类型。

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

- 上述三处 `[待验证]` 与一处 `[待分析]`（随机数）是**开工前必须闭环**的项，否则上层会建立在错误假设上。
- `Identifier` 若采用结构体，需评估在字典键高频使用下的哈希成本与装箱风险；取值类型后 `Equals`/`GetHashCode` 必须手写并缓存哈希（原版按字符串拼接计算，我们不复制其实现但需保证等值语义一致）。
- 与 `docs/specs/network/` 的交界：字符串编解码的字节级细节属于协议规格，不在本模块。

## 8. 参考资料

- `docs/architecture/boundaries.md`（侧别判定）
- `docs/specs/network/`（后续产出，届时补链接）
