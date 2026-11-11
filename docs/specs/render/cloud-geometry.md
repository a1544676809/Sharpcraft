<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

# 云的几何生成：原版行为规格

> 来源：`assets/minecraft/shaders/core/clouds.vsh`（Minecraft 26.3 jar），逐条描述**行为**，不含任何原始实现代码。
> 目的：把本项目"每帧构建并上传云顶点"的做法，换成原版"不传顶点、全程序化生成"的做法（T-827）。

## 1. 数据来源：一张面表，不是顶点表

原版每帧**只上传面的描述**，不上传顶点。表里每 **3 个整数**描述一个要发的面：

| 槽位 | 含义 |
| --- | --- |
| 0 | 单元 X 坐标（**已右移一位**，见 §2 的坐标拼接） |
| 1 | 单元 Z 坐标（同上） |
| 2 | 方向与标志位（见下表） |

方向与标志位的位域：

| 位 | 含义 |
| --- | --- |
| 0–2 | 方向 0–5（下、上、北、南、西、东） |
| 4 | 内侧面：四边形绕序取反 |
| 5 | 用顶面色而不是本方向的颜色 |
| 6 | 单元 Z 坐标的最低位 |
| 7 | 单元 X 坐标的最低位 |

**坐标按 2 倍缩放存储**：真实的单元坐标 = `(存储值 << 1) | 对应最低位`。这样 16 位足以覆盖极大范围。

**对比本项目现状**：每个面发 4 个顶点 × 7 个 float = **112 字节**；原版 **12 字节**。

## 2. 顶点完全由索引算出来

一次绘制里，顶点索引连续覆盖所有面：

```
面序号     = 顶点索引 / 4
面内角序号 = 顶点索引 % 4
```

角序号在**内侧面**时取 `3 - 角序号`（反转绕序），否则原样。

角点位置由一个**常量位表**解出：三个 24 位常量分别给出每个角序号的 X/Y/Z 是不是 1。即 8 个立方体角点被编码进三个整数常量，不占任何缓冲空间。

**所以顶点数 = 面数 × 4，且没有任何顶点缓冲。**

## 3. 位置与颜色

```
位置 = 角点 * 单元尺寸 + vec3(单元X, 0, 单元Z) * 单元尺寸 + 云偏移
```

- **只有一层**（Y 固定为 0 的平面），云是平的
- `云偏移` 来自 uniform，负责让它跟着相机走
- 单元尺寸来自 uniform

颜色：

```
顶点色 = (标志位5 ? 顶面色 : 该方向的颜色) * 云颜色
```

六个方向有固定亮度：下 0.7、上 1.0、南北 0.8、东西 0.9。**顶面色是给"从上方看到的侧面"用的**——原版让云顶看起来连成一片。

## 4. 与本项目现状的差异

| 维度 | 原版 | 本项目（当前） |
| --- | --- | --- |
| 顶点缓冲 | **没有** | 每帧构建 7 float/顶点并上传 |
| 每面数据 | 12 字节 | 112 字节 |
| 几何生成 | 顶点着色器按索引算 | CPU 构建 |
| 绘制 | 一次绘制覆盖所有面 | 一个面表解析成顶点数组 |
| 缓存 | 面表随单元变化才重建 | `CloudDrawCache` 缓存顶点数组 |

## 5. 已知的额外价值

顺带修掉一个**既存缺陷**：云当前的 OIT 管线复用了地形的顶点布局（44 或 56 字节步长），而 `CloudGeometryBuilder` 写的是 7 float（28 字节）——**每 4 个顶点只有第 1 个落在正确偏移**，其余是错位数据。改成程序化生成后这条路径整体消失，缺陷不复存在。

## 6. 落地切片（每片可独立编译与验证）

| 片 | 内容 | 验收 |
| --- | --- | --- |
| **S1** | 面表构建器：从 `CloudMeshBuilder` 的四元组输出 3 个整数/面（含位域拼接） | 单测：给定四个四元组，面表内容的位域可逐项断言；面数 = 4 |
| **S2** | ~~接缝加只读存储缓冲 + 无顶点缓冲的绘制~~ **无需改动**：`Draw(pass, vertexCount, instanceCount, firstVertex, firstInstance)` 与 `BindingKind.ReadOnlyStorageBuffer` 均已存在且为真实实现，前者有 3 处无顶点缓冲的调用点，后者已用于 compute 阶段 | 已核查（见 §8）；残留待确认点：现有只读存储绑定都在 Compute 阶段，云的顶点阶段读取按 WebGPU 规范允许（仅可写存储禁止进顶点），留待 S3 实测 |
| **S3** | 新的 `cloud_procedural.wgsl`，先只做**主着色**路径（非 OIT），与现有云做像素对比 | 同机位逐像素 0 差异（用 T-824 的 `MC_PIXEL_COMPARE`），且面表数据量与预期一致 |
| **S4** | 把 OIT 三阶段（bounds / transmittance / accumulate）接到新生成的顶点上 | 三阶段的 `draw` 计数不变，像素 0 差异 |
| **S5** | 删除 `CloudGeometryBuilder` / `CloudDrawCache` 的顶点路径与相关测试 | 构建通过、测试全绿、无死代码 |

**顺序不可颠倒**：S3 之前必须先把主着色路径的像素等价证明出来，否则 OIT 三阶段接上去之后差异来源无法分离。

## 7. 已知刻意简化

| 简化 | 为什么 | 影响 | 状态 / 任务 |
| --- | --- | --- | --- |
| **`useTopColor` 标志恒为 0** | 本项目现有云的亮度只按方向取（`FaceBrightness[Direction]`），没有"侧面改用顶面色"这一档；S1 要保住现有画面，设上它会立刻改变外观 | 云侧面在顶部相邻格为空时不会变亮，与原版存在可见差异 | 随 T-827 的 S3（与原版像素比对）一并处理，不在 S1 内 |

另外，S1 只处理**面表**本身；当前实现每顶点由 CPU 烘入的雾强度，在程序化版本里会改成着色器按 `fog_spherical_distance` 计算——两者对雾的取值方式不同（逐顶点线性插值 vs 逐片元），这是 S3 像素比对要暴露的已知差异来源。

## 8. S2 的核查结论（无需实现）

S2 原本计划"给接缝加两样能力"，核查后发现两样都已存在且不是桩：

| 能力 | 位置 | 证据 |
| --- | --- | --- |
| 无顶点缓冲的绘制 | `RenderBackend.cs` 的 `Draw(pass, vertexCount, instanceCount, firstVertex, firstInstance)` | 直接转发 wgpu，已有 3 处调用点，均为无顶点缓冲的全屏三角形 |
| 只读存储缓冲绑定 | 同文件 `BindingKind.ReadOnlyStorageBuffer` | 映射到 `WGPUBufferBindingType.ReadOnlyStorage`；`BindGroupDescriptors` 已有按此类型建组的辅助方法 |

**残留待实测的点**：现有只读存储绑定全部声明在 **Compute** 阶段。云需要在**顶点**阶段读取，WebGPU 只禁止可写存储进入顶点/片元以外的阶段（顶点阶段允许 `read-only-storage`）。此点在 S3 建管线时用实际编译结果确认，不预先假设。

结论：S2 关闭，S3 直接建管线。

## 9. S3 的几何验证（已完成一半）

改写前先把**几何侧**证明掉，避免像素比对时同时面对两个未知（几何错还是着色错）：

| 验证 | 做法 | 结果 |
| --- | --- | --- |
| 角点表转录是否正确 | 把规格里的 24 个角点重新打包成三个 24 位常量，与原版的三个常量比对 | **完全一致**：`0xF03CC3` / `0x6666F0` / `0xC3F066` |
| 面表 + 解码能否复现现有几何 | 用 S1 的面表按本文的解码规则算出每个角点位置，与 `CloudGeometryBuilder.Build` 的输出逐坐标比对（5 个四元组 × 4 角 = 20 个顶点，含负坐标与内侧面） | **逐坐标相同（4 位小数）** |

测试：`tests/Sharpcraft.Client.Tests/Render/CloudProceduralGeometryTests.cs`。

**这意味着**：程序化着色器剩下的风险只在着色（亮度、雾）一侧，几何不再是未知量。剩下要做的 S3 部分是把已验证的解码搬进 WGSL，并接线。

**注意**：着色器文件在首次被管线编译之前不会得到任何校验（构建期不编译 WGSL，`ShaderLibraryTests` 只验证文件可加载），所以接线时若有语法错误只会在运行时暴露——这一点在接入时需以管线创建成功作为第一道检查。

## 10. 接线方案（S3 剩余部分，逐条可直接执行）

已核对的现状：

| 项 | 位置 | 现状 |
| --- | --- | --- |
| 云资源与绑定布局 | `src/Sharpcraft.Framework/Rendering/CloudOitResources.cs` | `CreateBindGroupLayout` 只有两个条目：binding 0（camera，Vertex 阶段）、binding 1（transform，Vertex\|Fragment） |
| transform 缓冲 | 同文件 | 80 字节（`TransformUniformSize`） |
| 顶点/索引缓冲 | 同文件 | 按需扩容（`MinCapacity` 起、逐次放大） |
| 顶点管线 | `WindowedRenderer.cs` 的 `CreateDepthBoundsPipeline` 等 | 写死 `VertexLayout.Stride` + `VertexLayout.Attributes()` |
| 接缝能力 | `RenderBackend.cs` | 无顶点缓冲的 `Draw` 与 `ReadOnlyStorageBuffer` 均已存在 |

**执行步骤**：

1. **布局**：在 `CloudOitResources` 的 `CreateBindGroupLayout` 条目列表末尾追加两条：
   - binding 2：`UniformBuffer(2, ShaderStages.Vertex)` —— CloudInfo
   - binding 3：只读存储缓冲，`ShaderStages.Vertex` —— 面表
2. **CloudInfo 缓冲**：大小 **48 字节**（`colour` vec4 16 + `offset` vec3 12→补齐 16 + `cellSize` vec2 8 + `thickness` f32 4，末项补齐到 16 的倍数）。字段顺序必须与着色器里的结构体一致。
3. **面表缓冲**：仿照现有顶点缓冲的做法按需扩容；上传 `CloudFaceTable.Build(quads)` 得到的 `int[]`，`BufferUsages.Storage | CopyDst`。
4. **绑定组条目**：把上述两个缓冲加进 `CreateBindGroup` 的条目列表，顺序与布局一致。
5. **管线**：新建一个用 `cloud_procedural.wgsl`、**不声明顶点缓冲**（`null` 或空的 buffer layout）的管线。**建管线成功是这份着色器唯一的语法检查。**
6. **绘制**：`backend.Draw(pass, vertexCount: faceCount * 4, instanceCount: 1, firstVertex: 0, firstInstance: 0)`；**恰好 4 个顶点一个面**，不要用索引缓冲。
7. **待实测**：第 1 步把只读存储绑到 `ShaderStages.Vertex` 时，WebGPU 规范允许（只禁止可写存储进顶点阶段），但项目里现有只读存储绑定全在 Compute 阶段，**这一步要用实际建管线的结果确认，不要假设**。

**注意 Vec3 对齐**：WGSL 里 `vec3<f32>` 按 16 字节对齐，`offset` 后面直接跟 `cellSize` 会出现 4 字节空洞。写 C# 侧时用 `Vector4` 或手工按 16 字节偏移填，不要按 12 字节紧凑排列——这是这类 uniform 最常见的错位来源。

## 11. 接线前的实地核查：三处更正与修正后的方案

### 更正一：云的损坏不是既存缺陷（本文件 §5 的判断是错的）

本文件 §5 与 §1 的对照表曾断定"云的 OIT 管线复用地形顶点布局而数据是 7 float，是既存缺陷"。**这个判断是错的。**

`CloudOitResources.UploadGeometry` 明确把缓存的 7 float 云顶点**逐顶点展开成 14 float 的地形 OIT 格式**（位置 3 + 亮度 3 + 雾进 uv.x + 其余填充），所以在顶点打包改动之前，布局与数据是自洽的，云没有错位问题。

损坏的真实原因就是那次顶点打包改动本身：`Stride` 从 56 改成 44、颜色改成 `unorm8x4`，而展开代码仍按旧的 14 float 写。该改动已回退。

**教训**：只看了 `CloudGeometryBuilder`（产出 7 float）与管线布局（声明地形布局），就断定中间没有转换——但中间恰恰有一步展开。判断"数据格式不匹配"之前必须先确认数据到达 GPU 前的最后一站。

### 更正二：存在一条经典回退路径

`cloudOit is null` 时云走 `cloudColour` 经典路径（`WindowedRenderer` 的 `cloudColour is not null && cloudOit is null`）。也就是说云本来就有两条绘制路径：OIT 三阶段与经典单遍回退，二者都消费同一个 `CelestialColorDraw` 的顶点数组。

**因此改成程序化生成必须同时覆盖这两条**，否则会新增第三条路径——这正是原则 14 禁止的双轨。

### 更正三：摆放参数不需要新的 uniform

`transform.model` 目前是纯平移。可以把**缩放烘进模型矩阵**：

```
model = Translation(origin) * Scale(cellSize.x, thickness, cellSize.z)
```

着色器里就只需格子单位坐标：

```
顶点 = 角点 + vec3(cellX, 0, cellZ)      // 单位是"格"
世界 = model * vec4(顶点, 1)
```

这样 `offset` 与 `cellSize`、`thickness` 都不必进 uniform。

### 仍需要的 uniform：`fogEnd`

`VertexOutput.alpha` 是**云自身的距离淡出**（`fs_accumulate` 里 `fragmentAlpha = cloudAlpha * alpha`），不是场景雾。当前由 CPU 算 `clamp(1 - 距离/fogEnd, 0, 1)` 后烘进 uv.x。程序化后没有 CPU 顶点，必须在着色器里算，因此 `fogEnd` 仍需要一个来源。

可选：把 `CelestialColorDraw` 增加可选字段承载（`Tint` 的 80 字节 transform uniform 无空位）。**注意**：`fogEnd` 若改用 `clip.w` 代替欧氏距离，屏幕边缘会有可见差异——这是已知差异，需在像素比对时确认量级后再决定是否接受。

### 修正后的执行顺序

| 步 | 内容 | 为什么在这个位置 |
| --- | --- | --- |
| 1 | `CelestialColorDraw` 增加可选的面表与摆放字段（默认 `null`） | 数据从 `FrameCommand` 到渲染器的通道 |
| 2 | `FrameCommand` 走 OIT 时产面表而非顶点；经典回退同样改 | 两条路径必须同步，否则是第三条路径 |
| 3 | `CloudOitResources` 增加 binding 2（`fogEnd` 等标量）与 binding 3（面表只读存储） | §10 已写明 |
| 4 | `cloud_oit.wgsl` 的 `vs_main` 换成程序化版本（**保持 `VertexOutput` 六个字段不变**） | 三个片元阶段无需改动 |
| 5 | 三条云管线改为不声明顶点缓冲 | 建管线成功即语法检查 |
| 6 | 经典回退管线的顶点来源同步 | 避免双轨 |
| 7 | 像素比对 | 与改动前的截图逐像素 |

**上一轮我犯的另一处错误**：曾新建 `cloud_procedural.wgsl` 打算作为独立着色器，那是双轨——正确做法是直接改 `cloud_oit.wgsl` 的顶点阶段，三个片元阶段原样保留。该文件已删除。