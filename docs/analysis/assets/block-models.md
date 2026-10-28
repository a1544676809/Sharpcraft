<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

# 方块模型与方块状态（行为规格）

回答一个问题：**给定一个方块，它的六个面各用哪张纹理？** 这是把 53% 覆盖率的"名字猜测"换成"数据驱动"的前提（`docs/modules/render.md` §6.1 片二）。

证据全部取自 `ref://mc-26.3-analysis/src/assets/minecraft/` 下的真实 JSON，不是推断。

## 1. 方块状态（`blockstates/<方块>.json`）

两种形态：

| 形态 | 结构 | 例子 |
| --- | --- | --- |
| `variants` | 属性组合字符串 → 一个模型，或**一组**模型（按权重随机） | `stone.json` 的 `""` 键给出 4 个带 `y` 旋转的 `block/stone` 变体 |
| `multipart` | `when` 条件 + `apply` 模型列表，多条可同时命中 | 栅栏、红石线一类 |

- 键是属性组合：`"snowy=false"` / `"facing=north,half=bottom"`；空串表示"无属性"。
- 每个模型条目还带可选 `x` / `y` / `uvlock` / `weight`。
- **本阶段只用它取纹理**，不做几何：即"取第一个变体的模型"，忽略 `x/y` 旋转与 `weight` 随机。

## 2. 模型（`models/block/<名字>.json`）

字段：

| 字段 | 含义 |
| --- | --- |
| `parent` | 父模型标识（`block/cube` 或 `minecraft:block/cube`） |
| `textures` | 变量名 → 纹理路径（`block/xxx`）或 **`#另一个变量`** |
| `elements` | 几何：`from` / `to`（0–16 的方块空间坐标）+ `faces` |

`faces` 里每一项可带：`texture`（`#变量`）、`uv`、`cullface`、`tintindex`、`rotation`。

### 2.1 继承

- `textures` 按**从根到子**顺序累加：子模型的同名键覆盖父模型。
- 父模型的 `elements` 被子模型继承；子模型若自带 `elements` 则整体替换。
- `#变量` 是**间接引用**，可以链式（`cube_all` 的 `up: "#all"` → 子模型给 `all`），必须递归解析到纹理路径为止。

实例（真实文件）：

```
cube.json         parent: block/block        faces: up->#up, down->#down, north->#north …
cube_all.json     parent: block/cube         textures: up/down/north/… -> "#all"
grass_block.json  parent: block/block        textures: top=grass_block_top, side=grass_block_side,
                                                      bottom=dirt, overlay=grass_block_side_overlay
oak_wood.json     parent: block/cube_column  textures: end=oak_log, side=oak_log
```

`oak_wood` 因此**从不**读 `block/oak_wood.png`（那张图不存在），而是 `oak_log`——这正是之前 604 个方块变紫黑格子的原因。

### 2.2 面 → 纹理

| 面标签 | 我们的朝向 |
| --- | --- |
| `up` | 顶（+Y） |
| `down` | 底（−Y） |
| `north` / `south` / `east` / `west` | 侧（四个竖面） |

- 同一方块的不同侧可能不同（`cube_column` 的 `end` 只用于顶底，`side` 用于四周）。
- 一个模型可有**多个 element**（`grass_block` 有主体 + 侧面叠加层）。本阶段取**第一个含该面的 element**，忽略叠加层与 `tintindex`。

### 2.3 `uv` 缺省

`uv` 可省略。原版按 element 在**该面所在两轴**上的 `from`/`to` 决定默认 UV（即 `[x1,y1,x2,y2]` 取对应轴）。

- 本阶段我们只关心"用哪张纹理"，因此**暂不消费 `uv`**：贪心合并后的四边形在自己的 UV 空间里平铺整张纹理（见 `render.md` §6.1）。

## 3. 与资源栈的关系

- 模型与方块状态都是**资源**：`blockstates/<name>.json` 与 `models/block/<name>.json`，走同一个 `AssetStack`，资源包可覆盖（优先级语义与纹理一致）。
- 缺失时按 §4 的顺序回退。

## 4. 解析顺序（`BlockSpriteMap` 的最终规则）

1. `blockstates/<方块>.json` → 取第一个变体的模型 → 沿 `parent` 链合并 `textures` → 解析 `#变量` → 得到每面纹理路径。
2. 若该方块**没有** blockstate 文件：退回"与方块同名的纹理"（`block/<名字>`）——这是没有模型的方块（如 `stone` 的简化路径）的兜底。
3. 仍没有：试 `<名字>_top` / `<名字>_side` / `<名字>_bottom`（上一阶段的启发式，保留作为兜底）。
4. 仍没有：missing 精灵（紫黑格子）。

顺序是"从最有依据到最没依据"，每一步都可诊断（`MC_DEBUG_SPRITES` 会报告未解析的方块）。

## 5. 本阶段明确不做

- `multipart` 条件求值（栅栏、红石线的部分状态会更粗略）。
- 属性驱动的变体选择（如 `snowy=true` 换成雪顶纹理）——需要先把方块状态的属性值带进渲染层。
- element 级几何与 `rotation`：我们仍按整方块绘制。
- `tintindex` 与生物群系染色（草/树叶的真实绿色需要它）。
- 叠加层（`grass_block` 的 `overlay`）。

这些都在 `docs/modules/render.md` 的后续项里，不假装已实现。
