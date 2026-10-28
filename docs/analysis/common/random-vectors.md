<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 MinecraftCSharp contributors -->

状态：[草稿]
基线：Minecraft 26.3
证据：`ref://mc-26.3-analysis/vectors/`（生成器与原始输出）；入库数据 `tests/MinecraftCSharp.Common.Tests/TestData/random-vectors.txt`
最后更新：2026-10-28

# 随机数参考向量（reference vectors）

随机数算法无法靠"读源码"验证正确性：LCG 与 xoroshiro128++ 的每一位都必须一致，而源码推导无法排除理解偏差。本文记录**如何用运行原版的方式取真值**，以及入库的向量文件。

## 1. 方法（行为实验，不涉及反编译）

- 用客户端 jar 作为**库**：把 `26.3.jar` 与 `.minecraft/libraries` 下全部依赖 jar 放进 classpath，写一个只调用**公开 API** 的小程序（`LegacyRandomSource`、`XoroshiroRandomSource`、`RandomSource.create`、`Mth.getSeed`、`PositionalRandomFactory`、`RandomSequence`），打印输出。
- 只记录**数值**；生成器与原始输出都留在仓库外（`ref://mc-26.3-analysis/vectors/`）。
- 复现命令要点（踩过的坑都记下来）：
  1. `libraries/*` 通配符**不递归**，必须把所有 jar 递归拼接成显式 classpath（本机 834 个 jar）。
  2. classpath 太长（约 85k 字符）超出命令行上限，需用 `java @argfile`。
  3. **argfile 里反斜杠是转义符**，路径必须写成正斜杠，否则 `\s` 会被吞掉导致 `ClassNotFoundException`。
  4. **用十六进制输出**：16～19 位十进制整数会被部分工具误判为卡号并脱敏，导致数据损坏。

## 2. 已实证的结论

- `create_long|<seed>` 与 `legacy_long|<seed>` **逐位完全相同**：坐实了 `RandomSource.create(seed)` 返回 `LegacyRandomSource`（LCG），而不是 xoroshiro。这条与 `random.md` §1 的源码结论互相印证。
- `lootSequence` 与同 seed 的 `xoro_long` 不同，说明战利品序列确实经过独立的 `(世界种子, 表 ID)` 派生。

## 3. 覆盖范围

| kind | 参数 | 数量 | 说明 |
| --- | --- | --- | --- |
| `legacy_long` / `xoro_long` / `create_long` | seed ∈ {0, 1, 42, -1, 1234567890123} | 各 5×8 值 | `nextLong` 连续 8 次 |
| `legacy_int` / `xoro_int` / `create_int` | 同上 | 各 5×8 值 | `nextInt()` |
| `*_intBound` | 同上 + bound ∈ {3, 16, 100, 1000000007} | 各 5×4×8 值 | 覆盖"2 的幂"与"非 2 的幂"两类路径 |
| `*_doubleBits` / `*_floatBits` | 同上 | 各 5×4 值 | 以原始位模式记录，避免浮点格式化误差 |
| `posHash` | 4 组坐标 | 8 值 | `Mth.getSeed` 位置哈希 |
| `legacyPosAt` / `xoroPosAt` | 2 个种子 × 4 组坐标 | 8 值/类 | 位置化随机的首个 `nextLong` |
| `legacyPosHashOf` / `xoroPosHashOf` | 3 个名字 | 6 值/类 | 覆盖两种工厂不同的 `fromHashOf` 派生（Java 字符串哈希 vs MD5） |
| `lootSequence` | seed 42 + `minecraft:chests/simple_dungeon` | 8 值 | `RandomSequence` 派生序列 |

**消费顺序很重要**：每个实例按 long×8 → int×8 → 各 bound×8 → double×4 → float×4 的顺序连续取值，向量是该顺序下的结果；测试必须按同一顺序回放。

## 4. 样本（可用于快速人工比对）

```
legacy_long|42|0xba419d350dfe8af7|0xaee7bbe10c45c028|...
xoro_long|42|0xbed4a3d469c5d91f|0x65e301cb50e8f4ab|...
create_long|42|0xba419d350dfe8af7|...          # 与 legacy 完全一致
posHash|1|2|3|0xffffe15fa16d39f8
lootSequence|42|0xf43aa0b3e22aabc6|0xc30fefe8bf3c5b22|...
```

## 5. 未覆盖（后续补）

- `nextGaussian`（Marsaglia 极坐标法，含缓存第二个值的行为）。
- `nextInt(origin, bound)`、`nextBetween`、三角形分布等辅助方法。
- `consumeCount`、`fork()` / `forkPositional()` 的链式派生序列（目前只覆盖了 `forkPositional` 后的一次取值）。
- `WorldgenRandom` 的五个种子混合器（`setDecorationSeed` 等）与 `RandomState` 的算法选择：需要更深的调用链，等实现到世界生成时补。

## 6. 与 C# 测试的关系

- 向量文件入库为测试数据：`tests/MinecraftCSharp.Common.Tests/TestData/random-vectors.txt`（纯数值，不含任何资产或反编译内容）。
- 实现 `LegacyRandom` / `Xoroshiro128PlusPlus` 时，每个 kind 至少一条用例；`*_intBound` 必须同时覆盖 2 的幂与非 2 的幂路径。
- 向量回放测试属于"行为对齐证据"，不得为了让测试通过而修改向量——向量只能重新生成。
