<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

# 方块实体 NBT 数据层

> 状态：`规格已就绪`
> 更新：2026-11-14
> 关联：`docs/specs/render/special-block-rendering.md`

## 1. 现状（2026-11-14 核实）

| 层 | 状态 |
| --- | --- |
| 传输 | ✅ `ChunkPacketParser.ReadBlockEntity` 用 `NbtReader.TryReadNetworkRoot` 解析无名 NBT 根 |
| 存放 | ✅ `BlockEntityRecord.Tag`（`NbtCompound?`） |
| 通用读取 | ✅ `NbtCompound`：`GetInt` / `GetLong` / `GetFloat` / `GetBool` / `GetString` / `GetCompound` / `GetList` / `GetLongArray` |
| **按类型的 schema** | ❌ **缺失**——没有任何代码按方块实体类型读取特定字段 |

所以这一层的缺口不是"解析 NBT"，而是"**知道每种方块实体该读哪些字段、怎么读**"。

## 2. 原版字段（已核实，逐族补）

### 2.1 告示牌（`SignBlockEntity`）

`ref://mc-26.3-analysis/src/net/minecraft/world/level/block/entity/SignBlockEntity.java`

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `front_text` | `SignText` | 正面 |
| `back_text` | `SignText` | 背面 |
| `is_waxed` | bool | 是否已封蜡 |

`SignText` 结构（`SignText.CODEC`）：

| 字段 | 类型 | 默认 |
| --- | --- | --- |
| `messages` | 4 个 Component 的列表 | 全空 |
| `filtered_messages` | 可选，同上 | 无 |
| `color` | `DyeColor` | `black` |
| `has_glowing_text` | bool | `false` |

### 2.2 其余（待补）

旗帜 `patterns`、饰纹陶罐 `sherds`、头颅 `profile` 等待实现对应渲染器时逐族补齐，**不预先猜字段名**。

## 3. 设计

### 3.1 `BlockEntityData`

`Sharpcraft.Client.World.BlockEntityData`：一组静态读取器，输入 `BlockEntityRecord`，输出强类型结构。

```csharp
public readonly record struct SignLine(string Text);
public readonly record struct SignTextData(
    IReadOnlyList<string> Messages, string Colour, bool Glowing);
public readonly record struct SignData(SignTextData Front, SignTextData Back, bool Waxed);

public static bool TryReadSign(BlockEntityRecord record, out SignData data);
```

### 3.2 组件文本

`messages` 的元素是 Minecraft 的 **Component**。本仓尚无组件模型，因此：

- 元素是 `NbtString` → 直接取文本
- 其它形状 → **登记为简化**：返回空串，并计一次 `UnsupportedComponentCount` 诊断

这样告示牌渲染可以先支持纯文本牌子（占绝大多数），富文本后续再补。

## 4. 验证

1. **合成 NBT 单测**：构造与 wire 格式一致的 `NbtCompound`，断言读出的四行文本、颜色、发光、封蜡
2. **缺字段回退**：无 `front_text` 时返回 false，而不是抛异常
3. **真实数据锚点**：用分析工作区里任一含告示牌的区块包样本（若有）比对；没有则以合成 NBT + 原版字段名核对为准，并在文档中注明

## 5. 不做什么

- 不实现 Component 的完整模型（富文本、翻译键、点击事件）
- 不做 NBT 写回
- 不预读尚未实现渲染器的方块实体字段

## 6. 实现结果（2026-11-14）

| 项 | 内容 |
| --- | --- |
| `BlockEntityData.TryReadSign` | 读 `front_text` / `back_text` / `is_waxed`；每面读 `messages`（4 行）、`color`（默认 `black`）、`has_glowing_text`（默认 `false`） |
| 缺省回退 | 无 `front_text` → 返回 false；缺 `color`/`has_glowing_text`/`is_waxed` → 用原版默认值；`messages` 不足 4 行 → 补空串 |
| 富文本 | 非 `NbtString` 的组件读作空串并计入 `UnsupportedComponentCount`，不抛异常 |
| 测试 | 5 条：四行顺序、背面空行不位移、无 front 非告示牌、缺省默认值、富文本计数 |

**这一层没有新增解析代码**——传输与通用 NBT 读取早已具备，本次补的是"知道每种方块实体读哪些字段"。
