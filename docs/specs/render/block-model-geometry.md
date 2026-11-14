<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

# 方块模型几何（element 解析与生成）

> 状态：`规格已就绪`
> 更新：2026-11-14
> 关联：`docs/specs/render/special-block-rendering.md`、`docs/specs/world/block-entity-data.md`

## 1. 要解决什么

`BlockModelResolver` 只解析**面贴图**，不解析几何——`ChunkMesher` 把每个方块都画成**完整立方体**。因此所有非立方体方块（台阶、楼梯、栅栏、墙、火把、作物、告示牌板、箱子以外的方块实体）**都没有正确外观**。

这也是"特殊渲染方块"数量看起来很大的根因：不是缺 265 个渲染器，而是**模型几何管线缺失**。

## 2. 原版模型格式（已核实）

模型 JSON（`assets/minecraft/models/block/*.json`）的关键字段：

| 字段 | 含义 |
| --- | --- |
| `parent` | 父模型，几何与贴图按层合并 |
| `textures` | 变量名 → 贴图路径 |
| `elements` | 盒列表；**父模型没有 elements 时子模型定义** |
| `elements[].from` / `to` | 盒的两个对角，单位 1/16 方块，范围 −16..32 |
| `elements[].rotation` | 可选：`angle`（±22.5/±45）、`axis`、`origin`、`rescale` |
| `elements[].faces` | 六个方向之一 → `{ uv, texture, rotation, cullface, tintindex }` |
| `faces[].uv` | `[u1,v1,u2,v2]`，缺省时按盒在贴图上的投影自动推导 |
| `faces[].texture` | `#变量名`，经 `textures` 解引用 |

`blockstates/*.json` 的 `variants` 把状态字符串映射到模型；`multipart` 用条件组合多个模型。

## 3. 本仓现状

| 项 | 状态 |
| --- | --- |
| `blockstates` 读取 + `variants` 首个变体 + `parent` 链 + `textures` 解引用 | ✅ `BlockModelResolver` |
| **`elements` 解析** | ❌ 缺失 |
| **盒面 UV 推导** | ❌ 缺失 |
| **元素旋转** | ❌ 缺失 |
| 面剔除（`cullface`） | ❌ 缺失 |
| `multipart` / 属性驱动变体 | ❌ 缺失（已登记为解析器的已知限制） |

## 4. 切片

| 切片 | 内容 | 文件 | 验收 |
| --- | --- | --- | --- |
| **1** | `elements` 解析为纯数据（盒、面、UV、旋转、cullface），含 `parent` 继承 | 新增 `BlockModelElements.cs` | 单测：用原版 `torch` / `stairs` / `cube_all` 三个模型，断言盒数、坐标、UV、旋转 |
| **2** | 盒面 → 四边形顶点（含 UV 与自动推导） | 同上 + 测试 | 单测：一个盒的 6 面顶点与 UV 逐值比对 |
| **3** | 接入 `ChunkMesher`：按 state 选模型，非立方体走 element 路径 | `ChunkMesher` | 沙盒像素对比：火把、台阶与原版一致 |
| **4** | `multipart` 与属性驱动变体 | 解析器 | 栅栏、墙的连接形状 |

## 5. 验证

1. **真值来自原版 JSON**：直接从 `ref://mc-26.3-analysis/src/assets/minecraft/models/block/` 读模型文件做断言，不手抄
2. **像素判据**：切片 3 后用单方块沙盒对拍（火把、台阶），与参考截图比较
3. **不得**用"能解析"代替"画得对"

## 6. 风险

| 风险 | 缓解 |
| --- | --- |
| UV 自动推导规则容易写错 | 先用显式 `uv` 的模型验证，再单独测推导 |
| 元素旋转的 `rescale` 影响 UV | 先支持无旋转的盒，旋转单独一片 |
| 与现有立方体路径并存导致双轨 | 切片 3 必须**替换**立方体路径，不是新增分支 |

## 7. 不做什么

- 不做 `tintindex` 的生态染色（先按白色）
- 不做 `overlay`（草方块侧面叠加）
- 不做物品模型的 element 路径（那是另一条）

## 8. 真实数据调查（2026-11-14）

对 `ref://mc-26.3-analysis/src/assets/minecraft/models/block/` 全量扫描：

| 项 | 数量 |
| --- | --- |
| 模型文件 | **2883** |
| **含 `elements` 的模型** | **346** |
| 元素总数 | 1203 |
| 面总数 | 4289 |
| 含 `rotation` 的元素 | 303 |
| 解析失败 | **0** |

**结论：非立方体几何只集中在 346 个模型**，其余 2537 个是纯立方体（无 `elements`，走现有路径）。这让切片 3 的验证范围可控——按 346 个模型分批对拍，而不是 2883 个。

## 9. 切片 1 实现结果

`BlockModelElements`：

| 能力 | 说明 |
| --- | --- |
| `elements` 解析 | `from` / `to` / `faces` / `rotation`；缺 `from` 或 `to` 的元素跳过 |
| `parent` 继承 | 从子往父走，**第一个声明 `elements` 的模型胜出**；走到头仍无则返回 null（纯立方体） |
| 面字段 | `texture`（变量名原样）、`uv`（可空）、`cullface`、`tintindex`（默认 −1） |
| 可注入读取器 | `Func<Identifier, byte[]?>`，测试无需文件系统 |

7 条测试：盒角、面字段、父继承、自有元素覆盖父、纯立方体为 null、旋转、畸形元素跳过。

**未做的真实数据对拍**：C# 解析器尚未在 346 个真实模型上逐值比对（单元测试用的是合成 JSON）。切片 2 实现面生成时会加真实模型的对拍工具。

## 10. 切片 2 实现结果（2026-11-14）

`BlockModelGeometry.Build(elements)` → `List<ModelQuad>`：

| 能力 | 说明 |
| --- | --- |
| 角点表 | 六个方向各四个角点，按原版 `FaceInfo` 的**周长顺序** |
| 绕序 | 三角形 0-1-2 / 0-2-3；六个方向的法向均由测试逐方向验证 |
| UV 角映射 | 索引 0/1 取 minU、2/3 取 maxU；0/3 取 minV、1/2 取 maxV |
| 缺省 UV 推导 | 六方向规则见 §2；**侧面 v 范围是 `16−to.y .. 16−from.y`**，不是盒高 |
| 显式 UV | 优先于推导 |
| 元素旋转 | 绕 origin 旋转位置（`rescale` 未实现，见简化） |
| 未知面名 | 跳过 |

**18 条测试**：满方块六面、六方向绕序法向、UP 面角点与 UV、六方向缺省 UV、内缩盒投影、显式 UV 优先、旋转、未知面跳过。

### 已登记的简化

| 简化 | 影响 | 状态 |
| --- | --- | --- |
| 元素旋转的 `rescale` 未实现 | 303 个含旋转元素中声明 `rescale` 的那些，其 UV 不会按原版缩放 | 待切片 3 后单独处理 |
| 未做真实 346 模型对拍 | 切片 2 的测试仍是合成 JSON；真实模型对拍在切片 3 随像素验证一并做 | 待切片 3 |

## 11. 切片 3a / 3b 实现结果（2026-11-14）

### 3a：贴图映射暴露

`BlockModelResolver` 新增：

| 方法 | 用途 |
| --- | --- |
| `ResolveModel(blockId)` | 方块 → 首个变体模型 id |
| `ResolveTextures(blockId)` | 方块 → 合并后的完整 `变量名 → 值` 映射 |
| `ResolveTexture(blockId, variable)` | 变量（带或不带 `#`）→ 最终贴图路径 |

原有 `Resolve` 只给 Top/Side/Bottom 三张，element 模型需要每个 `#变量`，所以这层必须补。测试：`EveryTextureVariableIsExposedForElementModels`。

### 3b：模型库

`BlockModelLibrary.Resolve(blockId)` → `TexturedQuad[]?`：

| 情况 | 结果 |
| --- | --- |
| 模型含 `elements` | 四边形数组，每面贴图已解析为路径 |
| 纯立方体模型 | **null**（留在立方体路径） |
| 某面贴图解析不出来 | 该面丢弃；全部丢弃则返回 null |
| 重复调用 | 缓存返回同一数组 |

`ElementModelCount` 统计找到 element 几何的方块数。

5 条测试：element 模型出四边形、纯立方体为 null、blockstate 指向别的模型、贴图不可解析时丢弃、缓存。

### 已登记的简化（新增）

| 简化 | 影响 |
| --- | --- |
| 模型按**方块**缓存而非按**方块状态** | 同一方块的所有状态共用一个模型——因为 `BlockModelResolver` 只取 blockstate 的首个变体。台阶的上半/下半、楼梯的朝向都不会改变模型 |
