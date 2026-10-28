<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 MinecraftCSharp contributors -->

状态：[草稿]
基线：Minecraft 26.3
证据：`ref://mc-26.3-analysis/src/net/minecraft/util/RandomSource.java`、`.../world/level/levelgen/{LegacyRandomSource,XoroshiroRandomSource,Xoroshiro128PlusPlus,RandomSupport,PositionalRandomFactory,WorldgenRandom,RandomState}.java`、`.../world/RandomSequence.java`
最后更新：2026-10-28

# 随机数行为规格

随机数必须**逐位复刻**：同一世界种子必须产出同一地形、同一战利品。用 `System.Random` 或"逻辑等价"的近似实现都会导致结果不同。

## 1. 家族与用途（★ 最关键的一条）

| 实现 | 算法 | 默认用途 |
| --- | --- | --- |
| `LegacyRandomSource` | 48 位 LCG（同 `java.util.Random`） | **`RandomSource.create(seed)` 的默认返回**：实体 AI、方块随机刻、粒子、音效等 |
| `SingleThreadedRandomSource` | 同上，无原子操作 | `createThreadLocalInstance()`（线程内高频使用） |
| `ThreadSafeLegacyRandomSource` | 同上，加锁 | `createThreadSafe()`（已废弃） |
| `XoroshiroRandomSource` | xoroshiro128++ | **世界生成**（默认）、**战利品序列** |
| `WorldgenRandom` | 委派给上表之一 | 世界生成入口，`Algorithm` 可选 LEGACY / XOROSHIRO |

**务必注意**：`RandomSource.create(seed)` 返回 **LegacyRandomSource（LCG）**，不是 xoroshiro；26.3 里只有世界生成走 xoroshiro。

## 2. LegacyRandomSource（LCG）

- 状态：48 位整数（掩码 `0xFFFFFFFFFFFF`）；乘数 `25214903917`（`0x5DEECE66D`）；增量 `11`（`0xB`）。
- 播种：`state = (seed XOR 乘数) AND 掩码`（**先异或再掩码**，写错整条链就废）。
- 推进：`state = state × 乘数 + 增量`（模 2^48）；`next(bits)` 取 `state >> (48 − bits)` 的低 32 位。

派生：

| 方法 | 定义 |
| --- | --- |
| `nextInt()` | `next(32)` |
| `nextFloat()` | `next(24) × 2^-24` |
| `nextDouble()` | `((next(26) << 27) + next(27)) × 2^-53` |
| `nextBoolean()` | `next(1) != 0` |
| `nextLong()` | `(next(32) << 32) + next(32)` — 后者以 **int 带符号扩展**参与加法，不是按位或 |
| `nextInt(bound)` | 见下，两条路径结果不同 |

`nextInt(bound)`：

1. `bound` 为 2 的幂：`(int)((bound × next(31)) >> 31)`（64 位乘后算术右移）。
2. 否则：拒绝采样——反复取 `next(31)`，`mod = sample % bound`，当 `sample − mod + (bound − 1) < 0`（32 位溢出）时重抽。

## 3. XoroshiroRandomSource（xoroshiro128++）

- 状态 `lo` / `hi`；两者全 0 时替换为 `lo = 0x9E3779B97F4A7C15`、`hi = 0x6A09E667F3BCC909`。
- `nextLong`：`result = rotl(lo + hi, 17) + lo`；`hi ^= lo`；`lo = rotl(lo, 49) ^ hi ^ (hi << 21)`；`hi = rotl(hi, 28)`。

派生：

| 方法 | 定义 |
| --- | --- |
| `nextInt()` | `(int)nextLong()` |
| `nextBoolean()` | `(nextLong() & 1) != 0` |
| `nextFloat()` | `nextBits(24) × 2^-24` |
| `nextDouble()` | `nextBits(53) × 2^-53` |
| `nextBits(n)` | `nextLong() >>> (64 − n)`（无符号右移） |
| `nextInt(bound)` | 乘法高位法：32 位无符号随机数 × bound，低 32 位为小数部分；小数 < bound 时用 `(−bound mod 2^32) mod bound` 修正，再取高 32 位 |

同种子下 `nextInt(bound)` 在 LCG 与 xoroshiro 上**语义不同**，不可共用实现。

## 4. 64 → 128 位种子升级与混合

| 步骤 | 定义 |
| --- | --- |
| `upgradeSeedTo128bitUnmixed(seed)` | `lo = seed ^ 0x6A09E667F3BCC909`；`hi = lo + 0x9E3779B97F4A7C15` |
| `mixStafford13(z)` | `z ^= z >>> 30; z *= 0xBF58476D1CE4E5B9; z ^= z >>> 27; z *= 0x94D049BB133111EB; z ^= z >>> 31` |
| `upgradeSeedTo128bit(seed)` | unmixed 后对 lo/hi 各混合一次 |
| `seedFromHashOf(String)` | UTF-8 字节做 **MD5**，前 8 字节按**大端**组成 `lo`，后 8 字节组成 `hi`（不再混合） |
| `Seed128bit.xor(other)` | lo/hi 分别异或 |
| `Seed128bit.mixed()` | lo/hi 各混合一次 |

## 5. 位置化随机（`PositionalRandomFactory`）

`at(x, y, z)` 用 `Mth.getSeed` 派生：

```
s = x × 3129871 XOR z × 116129781 XOR y
s = s × s × 42317861 + s × 11
return s >> 16
```

- Legacy 工厂：`new LegacyRandomSource(posHash XOR factorySeed)`。
- Xoroshiro 工厂：`(posHash XOR seedLo, seedHi)`（只异或低位）。

`fromHashOf(String)` 两种工厂**完全不同**：

| 工厂 | 派生 |
| --- | --- |
| Legacy | `new LegacyRandomSource(javaStringHashCode(name) XOR factorySeed)` |
| Xoroshiro | `seedFromHashOf(name)`（MD5 大端切分）与工厂 128 位种子异或 |

因此实现必须区分工厂类型，并同时需要 **Java 字符串哈希**（`h = 31h + char`，int 溢出）与 **MD5**。

`fromSeed(long)`：Legacy → 直接作种子；Xoroshiro → `(seed XOR lo, seed XOR hi)`。

## 6. 世界生成的种子混合器（`WorldgenRandom`）

该类继承 `LegacyRandomSource`，但把状态操作**委派**给 `Algorithm` 选定实例（默认 XOROSHIRO，数据包可切 LEGACY）。以下混合器是地形与结构的种子来源：

| 方法 | 定义 |
| --- | --- |
| `setDecorationSeed(seed, cx, cz)` | `setSeed(seed)`；`xs = nextLong() \| 1`；`zs = nextLong() \| 1`；`result = cx×xs + cz×zs XOR seed`；再 `setSeed(result)` |
| `setFeatureSeed(seed, index, step)` | `seed + index + 10000 × step` |
| `setLargeFeatureSeed(seed, cx, cz)` | `setSeed(seed)`；`xs = nextLong()`；`zs = nextLong()`；`result = cx×xs XOR cz×zs XOR seed` |
| `setLargeFeatureWithSalt(seed, x, z, salt)` | `x × 341873128712 + z × 132897987541 + seed + salt` |
| `seedSlimeChunk(x, z, seed, salt)` | `seed + x²×4987142 + x×5947611 + z²×4392871 + z×389711 XOR salt`（线程本地实例） |

差别要点：`setDecorationSeed` 对随机数取 `| 1`（强制奇数）并用加法；`setLargeFeatureSeed` 直接相乘并用异或。两者极易混淆。

## 7. 战利品序列（`RandomSequence`）

战利品不直接消费世界随机源，而是为每张表派生独立序列：

```
seed128 = upgradeSeedTo128bitUnmixed(worldSeed)  XOR  seedFromHashOf(identifier.toString())
source  = new XoroshiroRandomSource(seed128.mixed())
```

战利品结果只依赖 `(世界种子, 战利品表 ID)`，与调用顺序无关。存档兼容的硬要求。

## 8. 高斯随机

Marsaglia 极坐标法：循环取两个 `nextDouble` 映射到 [−1, 1)，直到 `r² < 1` 且 `≠ 0`；乘子 `sqrt(−2·ln(r²)/r²)`；**缓存第二个值**供下次调用，`setSeed` 必须清空缓存。接口上另有已废弃的 `GAUSSIAN_SPREAD_FACTOR = 2.297`，使用点待查。

## 9. 对 C# 实现的约束

1. 整型运算必须 `unchecked`：溢出是算法的一部分（`nextInt(bound)` 的溢出判定、`s × s × 42317861`）。
2. 符号扩展与移位方向必须照抄（`>>` vs `>>>`、`nextLong` 的加法、`nextInt()` 截断）。
3. 需要 `rotl`、Java 字符串哈希、MD5（BCL 自带，注意大端切分）。
4. 分层建议：`RandomSource` 门面 + `LegacyRandom` + `Xoroshiro128PlusPlus` + `PositionalRandomFactory`（两实现）+ `WorldgenRandom` + `RandomSequence`。
5. 每个实现都要"参考向量测试"：固定种子下断言前若干个 `nextLong` / `nextInt(bound)` 的具体数值，作为与原版对齐的回归基线。

## 10. 待验证

- 各调用点实际选用的实现（实体、方块刻、粒子、结构、战利品、附魔）需逐子系统确认；本轮只确认了工厂默认返回值与世界生成/战利品的算法。
- `RandomSource` 接口上其余辅助方法（区间取整、三角形分布等）未展开。
- `GAUSSIAN_SPREAD_FACTOR` 的实际使用点未查。

- 参考向量已取（`random-vectors.md`）：覆盖 LCG、xoroshiro、`intBound` 两条路径、位置化随机、两种 `fromHashOf`、战利品序列；`nextGaussian` 与 `fork` 链尚未覆盖。
