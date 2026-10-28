<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

# 资源解析语义（行为规格）

本文件回答"原版如何把 `namespace:path` 变成一个可读文件、多个来源冲突时谁赢、找不到时怎么办"。它是资产解析层（`docs/modules/asset-resolution.md`）的设计依据。

证据均为 `ref://mc-26.3-analysis/src/net/minecraft/` 下的具体类型与方法；**只描述行为，不摘录实现**。

## 1. 标识符

资源用 `Identifier`（26.x 起由 `ResourceLocation` 更名）标识：`namespace:path`，缺省命名空间为 `minecraft`，命名空间与路径各自有字符集与长度约束。本项目已在 `docs/analysis/common/identifier.md` 立规格，实现见 `Common/Identifiers/Identifier`，本层直接复用，不另造类型。

## 2. 资源类型 → 文件路径

原版用 `FileToIdConverter(prefix, extension)` 做双向映射：

- `idToFile(id) = <prefix>/<id.path><extension>`
- `fileToId(file)` = 去掉 `<prefix>/` 前缀与 `<extension>` 后缀

| 资源类型 | prefix | extension | 证据 |
| --- | --- | --- | --- |
| 方块状态 | `blockstates` | `.json` | 与 jar 条目 `assets/minecraft/blockstates/…json` 一致 |
| 模型 | `models` | `.json` | 同上（`models/block`、`models/item` 为其子目录） |
| 物品定义 | `items` | `.json` | 条目 `assets/minecraft/items/…json` |
| 纹理 | `textures` | `.png` | 条目 `assets/minecraft/textures/…png` |
| 字体 | `font` | `.json` | 条目 `assets/minecraft/font/…json` |
| 粒子定义 | `particles` | `.json` | 条目 `assets/minecraft/particles/…json` |
| 语言 | `lang` | `.json` | 路径形如 `lang/<locale>.json` |
| 着色器 | `shaders` | `.vsh`/`.fsh` | 条目 `assets/minecraft/shaders/core/…` |

**包类型决定根目录**：客户端资源用 `assets`，服务端数据用 `data`（`PackType` 枚举的两个值）。因此客户端实际打开的是 `assets/<namespace>/<prefix>/<path><ext>`。

## 3. 包栈与优先级

已验证的链路（自上而下读）：

1. `PackRepository` 的选中列表 = **必需包（原版）排在最前**，其后是用户选中的资源包（必需包按"底部位置"插入到索引 0）。
2. `openAllSelected()` 按该顺序展开为 `List<PackResources>`。
3. `MultiPackResourceManager` 依序把每个包 push 进对应命名空间的 `FallbackResourceManager`，因此**内部列表顺序 = 上面的列表顺序**。
4. `getResource(location)` **从列表末尾向前**扫描，返回**第一个命中**。

结论：**列表末尾的包优先级最高**（即用户资源包覆盖原版），原版是兜底。

### 3.1 原版的排序原语（没有数值优先级）

原版**不使用数值优先级**，而是**有序列表 + 每个包的声明**：

- 选中列表是一个 `List<Pack>`，顺序即优先级顺序（配合 §3 的反向扫描，末尾最高）。
- 每个包带 `PackSelectionConfig(required, defaultPosition, fixedPosition)`：
  - `required = true`：原版必需包，必须在列表里；
  - `defaultPosition` ∈ {`TOP`, `BOTTOM`}：默认插入位置，必需包为 `BOTTOM`；
  - `fixedPosition = true`：锁定位置，不允许用户拖动。
- `Position.insert(list, …)` 的语义：`BOTTOM` 从索引 0 开始找第一个"非固定在底部"的位置插入，因此**原版包落在索引 0**；`TOP` 反向从末尾插。
- 用户资源包默认按"选中顺序"叠在原版之上，列表顺序就是覆盖顺序。

本项目因此**用有序列表建模**，不引入数值优先级（数值会掩盖"顺序即语义"这件事，接错顺序时是静默改变画面）。

## 4. 元数据兄弟文件

查找资源 `X` 时，同包的元数据位置是 **`X + ".mcmeta"`**（`getMetadataLocation`）。这正是纹理动画元数据（`textures/block/water_still.png.mcmeta`）的定位规则，也解释了 jar 里为什么有 218 个 `.mcmeta`（见 `jar-entry-map.md` §2）。

## 5. 多层查找与合并

- `getResource` 只取最高优先级的一份。
- `getResourceStack` 返回**全部**命中者，顺序为**高优先级在前**。
- 语言用 `getResourceStack` 拿到栈后逐份写入同一张表（后写覆盖先写）。因为栈是高→低，所以**同 key 时低优先级（原版）反而覆盖高优先级（资源包）**。

> 这一条与"资源包可以改翻译"的直觉相反，但它是代码事实（`ClientLanguage.loadFrom` → `appendFrom`）。本项目按"等效"要求照此实现，同时登记为待实测项（见 §8）。

## 6. 过滤包

包可在元数据里声明路径/命名空间过滤（`ResourceFilterSection`）。若某个包声明过滤了该资源：

- 命中过滤时返回**空**，并且**停止继续向下查找**（不会回落到更低优先级的包）；
- 命名空间过滤用"只 push 过滤器"的方式参与，即使该包不提供此命名空间也会记录过滤规则。

## 7. 缺失行为

- 普通资源缺失：`getResource` 返回空，交由调用方决定（多数渲染路径使用占位纹理）；
- 语言整类缺失：抛 `EmptyTranslationsException`（即"该语言一个键都没有"是硬错误）。

## 8. 未验证项（实现前需注意）

1. **语言合并方向**：§5 的结论来自代码阅读，尚未用资源包实测；若实测不同，以实测为准并更新本文档。
2. **原版语言文件的来源**：本机 26.3 jar 内只有 `en_us` 与 `deprecated`，其余语言在资产索引里（索引 34，见 `jar-entry-map.md` §4）。因此"原版包"在我们的实现中实际是"pack + 官方索引"两个来源，它们的相对优先级需要在设计里显式定义。
3. **`pack.mcmeta` 的 `pack_format` 校验**：原版会按版本区间决定是否接受一个资源包；本阶段先不实现（我们只消费自己的 pack 与官方索引），后续支持外部资源包时再补。
