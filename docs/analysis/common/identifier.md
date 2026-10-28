<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

状态：[草稿]
基线：Minecraft 26.3
证据：`ref://mc-26.3-analysis/src/net/minecraft/resources/Identifier.java`、`ref://mc-26.3-analysis/src/net/minecraft/IdentifierException.java`
最后更新：2026-10-28

# 标识符（Identifier）行为规格

26.3 中该类名为 `net.minecraft.resources.Identifier`（旧名 `ResourceLocation` 已不再使用）。它是全项目的通用命名引用：注册表键、纹理路径、数据包条目、进度 ID 等。

## 1. 组成与常量

| 项 | 值 |
| --- | --- |
| 命名空间分隔符 | `:` |
| 默认命名空间 | `minecraft` |
| Realms 专用命名空间 | `realms` |
| 命名空间合法字符 | `a-z` `0-9` `_` `-` `.` |
| 路径合法字符 | `a-z` `0-9` `_` `-` `.` `/` |
| 命名空间特殊拒绝 | 字面量 `..`（单独判断，见 §3） |

注意路径允许 `/` 而命名空间不允许；两者都不允许大写字母。

## 2. 解析形态（`parse` / `tryParse`）

按**第一个** `:` 切分：

| 输入 | 结果 |
| --- | --- |
| `ns:path` | 命名空间 `ns`，路径 `path` |
| `path`（无冒号） | 命名空间 `minecraft`，路径 `path` |
| `:path`（冒号在最前） | 命名空间 `minecraft`，路径 `path` |
| `ns:a:b` | 命名空间 `ns`，路径 `a:b` → **校验失败**（`:` 不在路径字符集内） |

- `parse` 失败抛 `IdentifierException`（`RuntimeException` 子类）。
- `tryParse` / `tryBuild` 失败返回 `null`（不抛）。
- 命令参数解析另有 `readNonEmpty`：空字符串直接报 `argument.id.invalid`。
- 构造入口 `fromNamespaceAndPath(namespace, path)` 校验两侧；`withPath` / `withPrefix` / `withSuffix` 保持命名空间不变。

## 3. 校验规则（关键细节）

- 路径校验：逐字符检查，**空串通过**（循环不执行）。
- 命名空间校验：先判断字面量 `..` 并拒绝，再逐字符检查；**空串通过**。
- 因此存在两个可利用的原版"怪癖"，我们必须**行为一致**：
  1. **空路径合法**：`minecraft:` 可构造成功（`isValidPath("")` 为真）。
  2. **空命名空间在构造入口合法**：`fromNamespaceAndPath("", "x")` 通过校验；但 `parse(":x")` 走的是默认命名空间分支，得到 `minecraft:x`。
- **没有长度上限**：全类无任何长度检查。不要"顺手"加长度限制，否则会拒绝原版接受的输入。
- 错误消息按"命名空间"与"路径"两类区分，且消息会经 Java 转义（`escapeJava`）后放进异常——这是原版的异常文本怪癖，不影响逻辑。

## 4. 等值、哈希、排序（★ 协议相关）

| 操作 | 语义 |
| --- | --- |
| `equals` | 命名空间与路径**都相等**才算相等（同一性短路：引用相同时也算） |
| `hashCode` | `31 × namespace.hashCode + path.hashCode`（复刻 Java 字符串哈希） |
| `compareTo` | **先比 path，再比 namespace**（不是先命名空间！） |

`compareTo` 的顺序颠倒很容易写错，而它直接决定注册表的遍历顺序，进而影响协议包体与数据包加载产物。实现时必须按此顺序，并补一条"排序键顺序"的测试。

哈希值本身**不跨协议边界**，因此 C# 侧不要求与原版哈希数值相同；但 `equals` 与 `compareTo` 的**语义**必须一致。

## 5. 序列化与派生字符串

| 场景 | 形式 |
| --- | --- |
| `toString` | `namespace:path`（省略默认命名空间后仍是完整形式） |
| 网络/数据编解码 | UTF-8 字符串，内容即 `toString` |
| 调试文件名 | 把 `/` 与 `:` 都替换为 `_` |
| 语言键 | `namespace.path` |
| 短语言键 / 短形式 | 命名空间为 `minecraft` 时省略前缀，只保留 path |
| 路径拼接 | `resolveAgainst(root)` 把 `root/namespace/path` 规范化后必须仍在 root 内，否则抛 `IllegalStateException` |

`resolveAgainst` 是路径穿越防线，我们的资产加载层必须保留同等保护。

## 6. 对 C# 实现的约束

1. `Identifier` 建议为 `readonly struct`（两个 `string` 字段）；等值与排序语义照抄上表，**但**不必复刻 Java 的哈希数值。
2. 解析走 `ReadOnlySpan<char>`，提供 `Parse`（抛异常）与 `TryParse`（返回 false）两个入口。原版只有"抛异常 + 返回 null"两种风格，我们两种都给，属于不改变可观测行为的差异。
3. 校验规则逐条对应 §3，**包括空串通过**与**无长度上限**；`..` 特判必须实现。
4. 排序测试必须覆盖：同 namespace 不同 path、同 path 不同 namespace、以及默认命名空间与显式 `minecraft:` 的等价性。
5. 不做字符串驻留/缓存（原版也不做），避免引入与生命周期相关的隐藏状态。

## 7. 待验证

- 无。本节结论来自对 26.3 源码的直接核对（含校验函数实现与比较器实现）。
- 唯一遗留：`readGreedy`（命令参数贪婪读取）的字符集含 `:`，那是命令解析层行为，不属于 `Identifier` 核心语义，留待命令系统分析。
