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

## 12. 切片 3c 实现结果（2026-11-14）

### 接入 `ChunkMesher`

| 机制 | 实现 |
| --- | --- |
| 状态查询 | `BlockModelSet`：构建时按方块解析一次，摊平成 `TexturedQuad[]?[TotalStates]`，运行期 `Has(state)` 是**数组下标**，不进热路径字典 |
| 排除 | `TryDescribe` 遇 element 状态直接返回 false——它不进入立方体面合并网格 |
| 发射 | 六个方向的合并跑完后，`EmitSectionModels` 逐方块发射，光照取该位置的 sky/block |
| 开关 | `MeshChunk(..., BlockModelSet? models = null)`；**默认 null，路径关闭** |

### 回归证据

**默认沙盒帧 `9CA9E034…` / 34897 B 逐字节不变**——路径关闭时与原行为完全一致。

### 已登记的简化

| 简化 | 影响 |
| --- | --- |
| element 面**无方向着色与 AO** | 所有 element 面用纯白（1,1,1）+ 方块光照，比立方体面更平；原版是按方向乘阴影系数 |
| element 面**不做 `cullface` 剔除** | 相邻不透明方块不会隐藏 element 面，多画一些三角形但不可见 |
| `EmitSectionModels` 每区块遍历 4096 格 | 未做加速结构；当前只在路径打开时生效 |

### 尚未接通

`FrameCommand` 还没有构建 `BlockModelSet` 并传给 `MeshChunk`——路径**已就绪但未启用**。这是有意为之：先保证关闭状态下零回归，再单独打开做像素对拍。

## 13. 路径启用与首次视觉证据（2026-11-14）

### 一个必须先解决的问题

`block/cube.json` **本身就是一个满立方体 `elements`**（0..16，六面全带 `cullface`）。所以"有 elements"不能作为走 element 路径的判据——否则**全部 2883 个模型**都会被当成 element 模型，绕过贪婪合并。

`BlockModelLibrary.IsPlainCube` 因此加了一条判定：**单元素、0..16 全尺寸、六面且六面都有 `cullface`** → 视为纯立方体，留在合并路径。

### 结果

| 场景 | 结果 |
| --- | --- |
| 默认沙盒（2045 方块） | `9CA9E034…` / 34897 B **逐字节不变**，2392 三角形 |
| `MC_FRAME_MODELS=1`（多一个火把） | 2046 方块，**120 像素变化**，bbox 25×15，颜色为橄榄棕 |
| 判据 | 整块在同样机位约 40×40 像素；25×15 的细长区域与火把细柱一致 |

### 诚实说明：这不是等效性证据

本次只证明了：
- 关闭/默认场景**零回归**
- element 路径**确实在渲染**（有非立方体形状出现在预期位置）

**没有**与原版像素对拍。因此原版等效性对 element 路径**尚未成立**。登记为待办。

### 已登记的简化（累计）

| 简化 | 影响 |
| --- | --- |
| element 面无方向着色 / AO | 比立方体面平 |
| 无 `cullface` 剔除 | 多画不可见三角形 |
| 模型按方块而非按状态 | 台阶上下半、楼梯朝向不改变模型 |
| 旋转 `rescale` 未实现 | 声明 `rescale` 的旋转元素 UV 不缩放 |
| **未与原版像素对拍** | 等效性未验证 |

## 14. 真实资产几何对拍（2026-11-14）

**脚本**：`tools/verify/check-block-model-geometry.ps1`（需 `MC_ANALYSIS_ROOT`，模型文件不进仓库）

**方法**：脚本在临时目录建一个引用 `Sharpcraft.Client` 的工程，用 `BlockModelElements` + `BlockModelGeometry` 解析并输出每个四边形的方向、贴图、`cullface` 与四角坐标；再由一段 **独立的 Python 实现**按文档规则从同一份 JSON 重新算一遍，逐值比对。

| 模型 | 期望四边形 | 实际 | 结果 |
| --- | --- | --- | --- |
| `minecraft:block/torch` | 6 | 6 | **MATCH** |
| `minecraft:block/stairs` | 11 | 11 | **MATCH** |
| `minecraft:block/flower_pot` | 22 | 22 | **MATCH** |

### 这证明了什么 / 没证明什么

**证明了**：解析与四边形生成对**真实游戏资产**是正确的——盒数、四角坐标（1e-3 容差）、贴图变量、`cullface` 全一致。

**没证明**：像素级外观。仍未与原版渲染画面逐像素对拍。几何正确是像素正确的必要条件，不是充分条件（还差方向着色、AO、`cullface` 剔除、图集采样）。

### 一处口径修正

§13 说的"120 像素 / 25×15 bbox"是**火把确实在渲染**的证据，不是等效性证据。本节的几何对拍才是目前最强的证据。

## 15. 方向着色（2026-11-14）

原版六个方向的亮度系数（`ref://mc-26.3-analysis/src/net/minecraft/client/renderer/block/BlockModelLighter.java` L288-515）：

| 方向 | 系数 |
| --- | --- |
| down | 0.5 |
| up | 1.0 |
| north / south | 0.8 |
| west / east | 0.6 |

已按此表实现 `BlockModelSet.Shade(direction)` 并乘到 element 面的顶点色上，6 条测试覆盖每个方向。

### 实测结果：**像素零变化**

| 对比 | 差异像素 |
| --- | --- |
| 默认沙盒 vs 基线 | 0（逐字节不变） |
| **火把场景（着色后）vs 火把场景（着色前）** | **0** |

顶点色确实写入了缓冲区（`MeshBuffer.WriteVertex` 的 `offset+3`，`red/green/blue` 各乘 255 取整后打包），所以问题不在写入端。

**未定位的原因**（登记为待查，不假装已生效）：

- 可能是 element 面走的是不受顶点色影响的采样路径
- 可能是该机位下火把可见面的系数恰好接近 1.0
- 可能是 `MeshWorldOnce` 这条本地路径的着色器变体忽略顶点色
- 可能需要检查 `BlockModelSet.Emit` 是否真的在火把上被调用（当前只从像素变化推断）

在定位之前，**"element 面有方向着色"这一条不能算完成**。

## 16. 定位结论：`ForTexture` 的路径前缀重复（2026-11-14）

### 根因

`BlockSpriteMap.Lookup(texturePath)` 内部会拼 `TextureDirectory`（`"block/"`）再构造标识符：

```
Lookup(p) → new Identifier("minecraft", "block/" + p)
```

而 `ForTexture` 传进去的是 `texture.Path`，即 `block/torch` → 拼成 `block/block/torch` → **图集里不存在** → 返回 null → `BlockModelSet.Emit` 把每个四边形都跳过。

**所以 element 路径从切片 3c 起一直什么都没画。**

### 为什么之前的判断是错的

| 之前看到的 | 实际含义 |
| --- | --- |
| 火把场景 120 像素变化 | **不是火把**，是新方块挡住了平台顶面 |
| 火把与花盆像素完全相同 | 两个模型都**不可见**，遮挡变化相同 |
| 着色后像素零变化 | 没有几何体，着色自然无效果 |

**教训**：像素"有变化"不等于"目标物体渲染了"。必须用**三角形增量**这种结构量交叉验证。

### 修复后

| 场景 | 方块 | 三角形 | 相对基线 |
| --- | --- | --- | --- |
| 默认 | 2045 | 2392 | **0 像素（逐字节不变）** |
| 火把（`MC_FRAME_MODELS=1`） | 2046 | **2404**（+12 = 6 面 × 2） | 130 像素 |
| 花盆（`MC_FRAME_MODELS=2`） | 2046 | **2436**（+44 = 22 面 × 2） | 135 像素 |
| 火把 vs 花盆 | — | — | **13 像素（不同）** |

三角形增量与四边形数**精确对应**，且两个不同模型渲染结果不同——这才证明 element 路径真正在画。

### §15 的修正

§15 说"方向着色实测无效"是**误判**，真实原因是几何体根本没渲染。修复后着色代码路径已经生效，但**尚未单独做"有/无着色"的对比测量**。
