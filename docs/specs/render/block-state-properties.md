<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

# 方块状态属性解码（T-041 支撑项）

> 状态：`规格已就绪`
> 更新：2026-11-14
> 关联：`docs/specs/render/special-block-rendering.md` §14

## 1. 要解决什么

方块实体与方块渲染经常需要读**方块状态的属性**，例如：

| 属性 | 用于 |
| --- | --- |
| `facing`（Direction） | 箱子、潜影盒、熔炉、告示牌等一切有朝向的方块 |
| `axis` | 原木、柱状方块 |
| `half` / `type` | 台阶、箱子左右半 |
| `waterlogged` | 流体渲染 |

现在只能拿到 state id，**拿不到属性值**。

## 2. 现有事实（2026-11-14 核实）

| 事实 | 证据 |
| --- | --- |
| `BlockStateLayout` 只有「方块 → 状态数量」 | `src/Sharpcraft.Common/Registries/Data/vanilla-block-states-26.3.tsv`：`245　minecraft:chest　24` |
| `BlockStateDefinition` 有构造器与正向 `IndexOf(values)`，但**仓库内无任何构造点** | `BlockStateDefinition.cs` |
| `BlockStateProperty` 定义了值序语义：布尔 `[true,false]`、整数升序、枚举按声明序 | `BlockStateProperty.cs` 注释，依据 `docs/analysis/common/registry-order.md` |
| **反向解码 `ValuesAt(offset)` 不存在** | 同上 |
| blockstate JSON **不能**提供属性 schema | `assets/minecraft/blockstates/chest.json` 只有单个 variant，无 property 段 |

## 3. 状态编码（已由现有实现固定）

- 属性**按名称 Ordinal 排序**
- 偏移枚举：**最后一个属性变化最快**
- `stateId = baseId + offset`
- `offset = Σ (valueIndex_i × Π_{j>i} radix_j)`

`minecraft:chest` 的 24 = `facing`(4) × `type`(3) × `waterlogged`(2)，按名排序为 `facing, type, waterlogged`，因此：

```
offset = facingIndex*6 + typeIndex*2 + waterloggedIndex
```

## 4. 缺的那张表

需要**每方块一份属性 schema**：属性名 + 值列表（顺序有意义）。

### 数据来源必须从原版提取

- **不能**从 blockstate JSON 推（箱子这类单 variant 方块没有属性段）
- **不能**从状态总数反推（24 只能得出 4×3×2，推不出哪个属性是 4）

**方案**：新增一个 Java 导出工具，跑在分析工作区的原版 jar 上，输出：

```
# blockId <TAB> propertyName <TAB> value1,value2,...
minecraft:chest	facing	north,south,west,east
minecraft:chest	type	single,left,right
minecraft:chest	waterlogged	true,false
```

**先例**：`tools/Sharpcraft.ItemRender.Audit/truth/MatrixDump.java` 已用同样的方式从原版导出真值。

**注意**：导出的是**属性名与值列表**（元数据），不是资产、不是代码，符合原则 3。

## 5. 交付物

| # | 内容 | 文件 |
| --- | --- | --- |
| 1 | `BlockStateDefinition.ValuesAt(int offset)`：反解属性值，与 `IndexOf` 互逆 | `BlockStateDefinition.cs` |
| 2 | 属性 schema TSV 与内嵌资源 | `Data/vanilla-block-properties-26.3.tsv` |
| 3 | 加载器：TSV → `IReadOnlyDictionary<Identifier, BlockStateDefinition>` | 新增 `BlockStateDefinitions.cs` |
| 4 | 便捷读取：`TryGetProperty(stateId, "facing", out string value)` | 同上 |
| 5 | 一个 Java 导出工具（仓库内只放工具代码，产物是元数据 TSV） | `tools/Sharpcraft.BlockState.Audit/` |

## 6. 验证

**必须能证明解码正确，而不只是"能编译"**：

1. **互逆性**：对每个方块、每个状态，`ValuesAt(offset)` 再 `IndexOf(values)` 必须等于 `offset`（全量 35723 个状态）
2. **状态数一致**：`Π radix` 必须等于 TSV 里的 state count（能抓出漏掉或多出的属性）
3. **真值向量**：至少 5 个方块的原版状态与属性值对照（chest / stairs / door / log / slab），落盘为测试基准
4. **渲染验证**：箱子四种 `facing` 各渲一帧，像素互不相同，且与原版朝向一致

## 7. 风险

| 风险 | 缓解 |
| --- | --- |
| 属性值顺序与原版不符（枚举顺序不是字母序） | 导出工具直接读原版 `BlockStateProperties`，不手抄 |
| 部分方块的属性来自方块实体而非状态 | 明确区分：`facing` 对箱子是状态属性，对某些方块不是 |
| 导出工具依赖原版 jar 的内部结构 | 与 `MatrixDump.java` 同样的运行前提，只在仓库外执行 |

## 8. 不做什么

- 不实现**方块更新/状态同步**（客户端已在区块包中收到 state id，本项只解码）
- 不做属性写回（客户端不修改方块状态）
