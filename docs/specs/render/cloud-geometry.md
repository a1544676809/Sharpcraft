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

## 12. 接线落地记录（S3 完成）

### 与规格的一处重要偏差：每面 6 个顶点，不是 4 个

规格 §2 按原版写的是"顶点数 = 面数 × 4"。**本项目不能照搬这个数**：原版四个顶点能成一个四边形，说明它用的是三角形带（或扇）；本项目管线用 `PrimitiveState.Default`，即**三角形列表（TriangleList）**。

按 4 顶点 + 三角形列表走的第一版直接渲染成满天乱网——因为 4 个顶点只能出一个三角形，第 4 个被丢弃，且顶点跨面边界被重新分组。改为**每面 6 个顶点**（角序号 0,1,2 / 0,2,3，与 `CloudGeometryBuilder` 的索引缓冲顺序一致）后正常。

这是"结构对齐、拓扑不同"的典型陷阱：**对齐原版的数据结构之前，必须先确认自己的管线拓扑与它一致**。

### 落地内容

| 项 | 位置 | 内容 |
| --- | --- | --- |
| 数据通道 | `FrameRequest.cs` 的 `CelestialColorDraw` | 新增 `CloudFaces` / `CloudFaceCount` / `CloudFogEnd` / `CloudOrigin`（均可选，天体绘制不设置） |
| 面表生产 | `FrameCommand` | 两条分支都产面表；`CloudOrigin` 与顶点路径的摆放保持一致 |
| 面表缓存 | `CloudDrawCache` | 与顶点/索引同生命周期缓存，避免每帧重建四元组 |
| 资源 | `CloudOitResources` | 加 binding 2（`vec4`：淡出距离）、binding 3（面表只读存储）；无顶点缓冲绘制 `Draw(faceCount * 6)` |
| 着色器 | `cloud_oit.wgsl` | `vs_main` 改为程序化；**`VertexOutput` 六字段与三个片元阶段完全未改** |
| 管线 | `WindowedRenderer` 三个云辅助函数 | `positionColor` 参数此前**声明了却从未使用**（顶点布局写死为 `VertexLayout`），现在真正生效：为真时步长 0、无顶点属性 |

### 已验证

- 构建通过；8 个程序集测试全绿（含 `CloudFaceTableTests` 17 项、`CloudProceduralGeometryTests` 2 项）
- 管线创建成功（`stderr` 无验证错误）——**只读存储缓冲可以绑在顶点阶段**，这是 §10 里的待实测点，现已确认
- 画面：云恢复为平坦云层，无损坏

### 尚未验证（下一步）

- **与改动前的逐像素比对未做**。已知差异来源有两处：①淡出距离由欧氏距离改为视深度 `clip.w`；②`useTopColor` 仍未接管。做比对时应先单独量化这两项，再看剩余差异。
- 经典回退路径（`session.CloudUseOit` 关闭时）仍在用显式顶点，未改为程序化。

### A/B 对比结果（同机位、同配置）

用改动前的提交建独立 worktree，与当前实现各跑一次同机位截图比对：

| 项 | 结果 |
| --- | --- |
| 像素差异 | 24.74%（阈值 24/765） |
| 差异来源 | **云随时间滚动**：两次运行相隔约 40 秒，云层位置不同 |
| 结构一致性 | 一致——同样的云体分布、同样的地形透出、同样的 UI 与右侧山体 |

**一处误判的更正**：改动后的截图中间有一大片白色云雾，我一度怀疑是新实现把云渲染坏了。A/B 显示**改动前的基线也有同样一片**——那是相机处在云层内部，云本来就渲染成近不透明的白色板。**没有回归。**

判读这类对比时必须先取得基线，不能凭"看起来不对"下结论。

## 13. 实测结论：这项改动**没有性能收益**

> **更正**：本节第一版是在默认的垂直同步（`PresentMode.Fifo`）下测的，整帧被限帧拉到 5.5 ms、`acquire` 占 1.3 ms，任何 CPU 侧差异都会被等待淹没。**帧级对比必须加 `MC_PRESENT_MODE=immediate`**，否则测到的是送显节流而不是开销。下表已用 immediate 重测。

同机位、同配置（858×527、视距 12、`MC_FRAME_CPU`、`MC_PRESENT_MODE=immediate`），各取样 45 秒取最后三行：

同机位、同配置（858×527、视距 12、`MC_FRAME_CPU`）各跑 55 秒取样：

| 指标 | 基线（显式顶点） | 程序化 | 判定 |
| --- | --- | --- | --- |
| `clouds`（云阶段 CPU） | 0.009–0.011 ms | 0.013–0.015 ms | 程序化略高约 0.004 ms，属**噪声量级**，不构成退化 |
| `frame`（整帧 CPU） | 2.202–2.576 ms | 2.463–2.622 ms | 区间重叠，无差异 |
| `oit` | 0.330–0.358 ms | 0.355–0.369 ms | 无差异 |

**顺带得到的一个真实数字**：这台机器在这个场景下的整帧 CPU 是 **2.2–2.6 ms**（不是限帧下的 5.5 ms）。云阶段本身只有 **10–15 微秒**，占整帧 0.5%。

**为什么没有收益**：`CloudDrawCache` 命中时，两条路径都只是返回已缓存数组——每帧本来就不重建几何。原先担心的"每帧构建 112 字节/面的顶点并上传"实际上只在缓存失效时发生，属于低频路径。省掉的 CPU 工作量小到测不出来。

### 这项改动真正换来了什么

| 项 | 收益 |
| --- | --- |
| 内存与上传 | 每面 **12 字节** vs 112 字节（缓存失效时才上传，因此是低频收益） |
| 结构 | 与参考实现同构，几何只有一个生成来源（OIT 三条管线共用顶点阶段） |
| **消除了一个脆弱点** | 原路径的"7 float 展开成 14 float"正是 T-826 顶点打包改动踩坏的地方；程序化后这段代码不再存在 |
| 测试 | 面表解码与现有几何逐顶点等价（20 顶点）；亮度表由测试钉住两份不得漂移；每面 6 顶点由测试钉住 |

### 尚未完成

- 经典回退路径（`session.CloudUseOit` 关闭）仍用显式顶点，是当前**唯一残留的第二几何来源**
- `useTopColor` 仍未接管（基线也没有，属与原版的差异而非与原实现差异）

### 结论

**这是一次结构对齐，不是性能优化。** 若项目的判据只有帧时间，它可以回退；保留它的理由是删掉了一段已知脆弱且与参考不同构的路径，并顺带把几何正确性用测试锁住。

## 14. 顶点打包（T-826）在云解耦后重做

T-826 当初被回退，是因为云 OIT 路径消费 `VertexLayout`，改步长会让它的"7 float 展开成 14 float"代码失配（见 §11 更正一）。**T-827 之后云不再消费任何顶点属性**，这条耦合消失。

加了结构性测试钉住这个前提：`TheCloudVertexStageConsumesNoVertexAttributes` 读 `cloud_oit.wgsl`，断言 `vs_main` 的参数里**不含任何 `@location(`**、只含 `@builtin(vertex_index)`。所以以后再动顶点布局，云不会被牵连。

### 改动

| 项 | 之前 | 现在 |
| --- | --- | --- |
| 步长 | 56 字节 | **44 字节**（−21%） |
| 颜色 | `vec3<f32>` 12 字节 | `unorm8x4` **4 字节** |
| 光照 | `vec2<f32>` 8 字节 | `unorm8x4` **4 字节**（用 `.rg`） |
| 位置 / uv / 精灵矩形 | 12 / 8 / 16 | 不变 |

每帧顶点读取量 230 MB → 170 MB。

### 实测（immediate、视距 12、同机位）

| 指标 | 打包前 | 打包后 |
| --- | --- | --- |
| `frame` | 2.202–2.576 ms | 2.42–2.47 ms |
| `scene-terrain` | 0.230–0.255 ms | **0.211–0.223 ms** |
| `oit` | 0.355–0.369 ms | 0.339–0.347 ms |
| `submit` | 0.55–0.60 ms | 0.531–0.542 ms |

CPU 侧各项都有小幅下降，但**区间仍与基线重叠**——真正的收益应在 GPU 侧（顶点带宽），需用 RenderDoc 复测主场景 pass 的微秒数才能确认，**尚未做**。

### T-826 的收益无法用 CPU 帧时间证实

同机位各跑 3 次（每次 45 秒、immediate、视距 12），用 `frame-cpu` 的 `frame=` 采样：

| | 均值 |
| --- | --- |
| 基线（56 字节） | 3.877 ms |
| 打包（44 字节） | 3.852 ms |
| 差异 | **0.025 ms（0.7%）** |

**这个数字不能用**，两个原因：

1. **采样污染**：每次运行开头都有 380–425 ms 的首帧和 8 ms/4 ms 的加载尖峰，直接进了均值。
2. **场景未稳定**：视距 12 下 45 秒内区块仍在流入，帧时间在 1.9–3.1 ms 之间漂移（能看到先升到 3.0 再回落到 2.0 的段落）。漂移幅度**远大于**待测的效应。

**结论**：21% 的顶点数据减少属于带宽侧，只能靠 GPU 计数器确认；用 CPU 帧时间在这个噪声水平上分辨不出来。

**获取 GPU 计数的阻碍**：`renderdoccmd` 没有 python 子命令，计数器只能走 `qrenderdoc --python`，而它会拉起 GUI 并阻塞（本会话此前两次尝试都被卡住）。要拿到这个数字，需要先解决 `qrenderdoc` 的无界面调用方式。

## 15. GPU 计数器的打通与失败结论

### 打通了什么

RenderDoc 的 Python 入口此前一直卡在"`qrenderdoc --python` 会拉起 GUI 并阻塞"。查官方文档后确认**参数名是 `--py`**（`--python` 不存在）：

> `--py` runs the script early in initialisation **before the UI has been created or shown** and can be used for headless execution.

另外两个坑，都记在 `tools/verify/rdoc-gpu-timings.py` 里：

| 坑 | 表现 | 解法 |
| --- | --- | --- |
| qrenderdoc 是 GUI 子系统程序 | 脚本 `print` 的内容**看不到**（没有控制台） | 输出写文件，路径走环境变量 |
| 计数器单位是**秒** | 按微秒打印全是 `0.00` | 乘 1e6 |
| `rd.ReplayProgress` 不存在 | `AttributeError` | `OpenCapture(rd.ReplayOptions(), None)` |
| `CounterValue` 是联合体 | `float()` 报类型错 | 按 `d`/`f`/`u64` 依次取分量 |

脚本要显式 `sys.exit()`，否则 qrenderdoc 进程不退出。F12 触发抓帧用桌面工具发送（`keybd_event` 不可靠）。

### 失败结论：这个测量方法的分辨率不够

同一份捕获**重复分析**，整帧 GPU 时间在 **1381–3141 µs** 之间波动（约 2 倍）。两次 A/B 的方向甚至相反：

| 轮次 | 56 字节 | 44 字节 |
| --- | --- | --- |
| 第一次（各 3 次） | 中位数 2494 µs | 中位数 1839 µs |
| 第二次（各 4 次） | 中位数 **1388 µs** | 中位数 **2966 µs** |

两次捕获的几何量已对齐到同一量级（3.05M 三角形），所以差异不是场景不同造成的。

**结论：`EventGPUDuration` 的 RenderDoc 重放值不能用来分辨 20% 量级的差异。** 顶点打包的收益仍然**未被证实**，也未被否证。

### 要真正回答这个问题，需要其中一条

1. **应用内 GPU 时间戳**——最准，但当前 wgpu 绑定没有 `TimestampQuery` 特性（T-800 已确认）
2. **合成基准**：在一次捕获里把同一批几何重复绘制 N 次，把待测开销放大到噪声之上
3. **更严的统计**：每边几十次采样取中位数，成本高但可行

## 16. 稳定态下的新发现：`prep-entities` 是几何上传，占整帧 18%

世界加载完成、静置 100 秒后连测三帧（immediate、视距 12）：

| 阶段 | 耗时 |
| --- | --- |
| **`prep-entities`** | **0.437–0.466 ms** |
| `submit` | 0.525–0.558 ms |
| `oit` | 0.342–0.355 ms |
| `swap` | 0.243–0.268 ms |
| `scene-terrain` | 0.212–0.228 ms |
| `frame` | 2.330–2.531 ms |

### 两个问题

**一、名字又是错的。** 这个区间（`WindowedRenderer` 中 `Cpu(FrameCpuTimings.PrepEntitiesStage)` 之前那段）实际做的是：

1. `dynamic` 几何的 `WriteBuffer`（顶点 + 索引）
2. **每个实体图层**的 `WriteBuffer`（顶点 + 索引）
3. 区块几何的 `arena.Apply` / `Remove`

**都是几何上传，没有一件是"实体准备"。** 这已是本项目第 6 次出现"标签与实测内容不符"，前 5 次记录在会话历史中。

**二、上传是无条件的。** 两条路径每帧都执行 `WriteBuffer`，整个 `WindowedRenderer` 里**一处 `ReferenceEquals` 都没有**。稳定态下几何基本不变，这些上传是白做的。

### 为什么不能简单加"引用没变就跳过"

`DynamicGeometry` 采用**原地复用**（`Clear()` / `Reserve()` 后复用同一数组），实体图层的 `Geometry` 也是每帧往同一数组里重填。所以：

- `ReferenceEquals` 恒为真 → 会**错误地跳过**已经变化的几何
- 多帧不更新时画面会冻结

**正确的做法**是给 `DynamicGeometry` 增加一个变更版本号（在 `Clear`/`Reserve` 时递增），上传端比较版本号。这需要先确认实体几何的填充路径**每帧都调用** `Clear`/`Reserve`——若如此则版本号每帧都变、优化无效；若不是才有收益。

**这个确认尚未做。** 在确认之前不动这段代码，避免"优化了一个本来就不产生开销的路径"。

### 分段探测的结果与失败

用"移动标记位置"的零风险办法探测 `prep-entities` 的构成：

| 标记位置 | `prep-entities` | 整帧 |
| --- | --- | --- |
| dynamic 上传之后 | **0.000 ms** | — |
| 实体图层之后 | 1.074–1.259 ms | 3.218–3.793 ms |
| 原始位置（区块更新之后） | 0.437–0.466 ms | 2.330–2.531 ms |

**唯一可信的结论：dynamic 上传是 0.000 ms**（同一次运行内测得，标记就在它后面）——这个场景下 `dynamicGeometry()` 返回空，那条路径根本不产生开销。

**其余数字不可用**：把标记提前后读到的数反而更大，而且整帧从 2.4 ms 涨到 3.2–3.8 ms——**两次运行的场景不同**（可见实体数量不一样）。跨运行的分阶段对比在这里不成立。

### 教训（这是本项目反复出现的同一个坑）

前面已经记过"跨进程测量噪声 ±1.2 ms"，这次又踩了一次。**分阶段耗时只有在同一次运行内、或用连续 A/B 切换时才可比。** 要拆解 `prep-entities`，必须做成**同进程内的 A/B**（例如一个开关在同一会话里切换"上传/跳过上传"，连续采样对比），而不是"改代码再跑一遍"。

### 下一步的正确做法

1. 给 `DynamicGeometry` 加变更版本号（`Clear`/`Reserve` 递增）
2. 加一个**运行时开关**（宿主机解析，遵循原则 13），在同一次会话内交替"无条件上传 / 版本号命中则跳过"
3. 用连续采样对比两边同一指标

## 17. 同进程 A/B 装置建成，并给出第一个可信结论

### 装置

复用已有的帧组机制（`MC_AB_TOGGLE`，按帧数交替组 0/组 1），新增 `MC_AB_SKIP_UPLOAD`：**组 1 跳过实体图层的 `WriteBuffer`，但仍然照常记录图层与索引数**，所以两组画的是同一份几何、唯一差异就是那次上传。

`FrameCpuTimings` 增加 `AddAbSample(group, ticks)`，在同一个 `WriteLine` 窗口里按组累加，打印：

```
ab-cpu: n=30/30 normal=1.020 toggled=0.985 delta=-0.035
```

两组样本在同一进程、同一世界、连续帧内交替采集——**这正是前三种方法缺的东西**。

### 结果（381 个窗口，每组 30 帧，约 11,400 帧）

| 指标 | 值 |
| --- | --- |
| `normal` 均值 | **0.970 ms** |
| `toggled` 均值 | **0.930 ms** |
| **delta 均值** | **−0.0405 ms（−4.2%）** |
| delta 中位数 | −0.033 ms |
| delta < 0 的窗口占比 | **269 / 381 = 70.6%** |

**结论：实体图层的无条件上传是真实开销，约 40 微秒（整帧约 1.7%）。** 方向明确、比例 70.6% 显著偏离 50%，不是噪声。

**但也要注意它否定了什么**：这个装置测的区间是 "实体图层上传 + 区块 arena 更新"，而跳过上传只拿回 40 µs——说明**区间里另外约 0.9 ms 不是实体上传**。按 §16 的怀疑去优化上传，最多只能拿回 4%。

### 方法论结论

三种不可靠方法（跨进程、GPU 重放、改代码再跑）之后，**同进程交替是唯一给出可重复结论的路子**。这条应作为后续所有渲染性能对比的默认做法：

1. 宿主解析一个开关（原则 13）
2. 用 `MC_AB_TOGGLE` 分帧组
3. `AddAbSample` 按组累计
4. 看 delta 的均值、中位数与**符号比例**（比例比均值更能抵抗离群值）

## 18. 用同一装置定位到真凶：**2 个可见实体，0.78 ms**

把 A/B 开关逐层扩大，每一轮都在同一进程内交替采样，得到干净的归因链：

| 跳过的内容 | normal | toggled | delta | 负比例 |
| --- | --- | --- | --- | --- |
| 实体图层 `WriteBuffer` | 0.847 | 0.805 | −0.043 | 109/150 |
| ＋区块 `arena.Apply` | 0.790 | 0.753 | −0.037 | 64/100 |
| ＋**整个 `entityLayers()` 回调** | 0.752 | **0.034** | **−0.717** | **100/100** |

**结论：那 0.75 ms 几乎全部来自 `entityLayers()` 回调**，上传与 arena 更新合计只占 0.04 ms（约 5%）。

### 更关键的对比

打开实体剔除统计后：

```
entity cull: candidates=14 visible=2 culled=12
prep-entities = 0.778–0.789 ms
```

**只有 2 个可见实体，却花了 0.78 ms**——平均**每个可见实体约 390 微秒**。

这不是"实体太多"，而是**单个实体的模型网格构建极慢**。`entityLayers()` 每帧做：

1. `session.EntityStates(partialTick)` 收集状态
2. 视锥剔除
3. `modelMesher.BuildLayers(...)` 构建模型几何

第 3 步是嫌疑最大的一环：若每个实体每帧都从头展开模型的所有立方体与 UV（而不是缓存静态部分、只更新动画变换），成本就会是百微秒级。

### 这个数字的分量

- `frame` ≈ 2.4–2.8 ms
- `entityLayers()` ≈ 0.78 ms
- **约占整帧 28–32%**

**这是本次会话找到的最大一处开销，而且买一个明确的下一步**：查 `BuildLayers` 是否每帧重建了本可缓存的部分。

### 与原版的对照，以及一个被自己否定的假设

**架构一致**：原版 `ModelPart.render` 同样是 push pose → 应用该部件的平移/旋转 → 逐立方体把顶点写进 `VertexConsumer`（`compile`）。也就是说"每帧逐立方体写顶点"本身就是原版的做法，我们的 0.78 ms **不是架构选错**，是实现效率问题。

**假设一（已否定）**：`EntityModelMesher` 里有一处每面调用的局部函数

```csharp
transforms.TryGetValue(part.Name + "." + face, out var value)
```

看起来很像"热路径里做字符串拼接 + 复合键查找"——本项目以前就栽过一次类似的每顶点字典探查。但追到调用方发现：`BuildLayers` 的两处调用**根本没有传 `transforms`**，参数是 `null`，`transforms is not null` 直接短路，那段代码**不会执行**。

**记录在这里是为了防止下一个人重走这条路。** 每次怀疑都要回到调用点确认参数，不能只读函数体。

### 尚未排除的方向

按每实体约 390 微秒的量级，剩余嫌疑：

1. `PrepareAnimationPoses(entities)` —— 每实体计算骨骼矩阵
2. `HasModelBackedLayers` / `BuildLayerPlan` —— 每帧建计划与分配
3. 每部件 `partTint`、精灵/UV 解析
4. 每帧 `new List<EntityGeometryLayer>(plan.Count)` 等分配带来的 GC 压力

**这些都是可以用同一套装置逐项否定的**：给每一项加 A/B 跳过开关，看 delta 落在谁身上。

### 二分到函数内部：不是量的问题，是每顶点慢了 30–70 倍

在 `BuildLayers` 里加临时打点（`MC_DIAG=1` 时每 60 帧打印，`Trace` 为假时完全惰性，可整体删除）：

```
entity-build: path=model(idx=648,vf=6048,parts=3) entities=1 layers=1 pose=0.011 rest=0.29–0.38 total=0.30–0.39
```

| 项 | 值 | 判定 |
| --- | --- | --- |
| `PrepareAnimationPoses` | 0.011 ms | **排除**，可忽略 |
| `BuildModelBackedLayers` | 0.29–0.38 ms | **全部成本在这里** |
| 产出规模 | 648 索引 = 108 四边形、3 个部件 | **量很小** |
| 每四边形成本 | **约 2.8 微秒** | 正常应为几十纳秒 |
| 每顶点成本 | **约 350–700 纳秒** | 正常应为 5–20 纳秒 |

**结论：慢 30–70 倍，与几何量无关。** 3 个部件、108 个四边形不该花 300 微秒。

已排除的项：`PrepareAnimationPoses`（0.011 ms）、字符串拼接式字典查找（调用方传 `null`，见上文）。文件里其余每顶点操作只有 `new Vector3`（结构体）与 6 项字典查找，都不足以解释这个量级。

**下一步必须是函数内部二分**：把 `BuildModelBackedLayers` 拆成"模型解析 / 分组 / 顶点发射"三段各自计时。现在的打点只到函数边界，再往里没有数据。

### 再往下一层：发射阶段吃掉全部，且是每顶点开销

在 `BuildModelBackedLayers` 内加三段计时（临时打点，已删除）：

| 段 | 耗时 |
| --- | --- |
| 计划构建（含 `ResolveEntityModel`） | 0.012–0.020 ms |
| 分组（含逐层 `ResolveEntityModel`） | 0.035–0.055 ms |
| **顶点发射**（`BuildInto` + `AppendGeometry`） | **0.24–0.38 ms** |

1 个实体、108 个四边形、432 个顶点、3 个层——**发射阶段 0.3 ms ≈ 每顶点 700 纳秒**。

逐个排除了发射路径上的每顶点函数：

| 函数 | 结论 |
| --- | --- |
| `WriteVertex` | 14 次数组赋值，廉价 |
| `TransformUv` | 纯算术，廉价 |
| `DynamicGeometry` 扩容 | `BuildInto` 里已有 `Reserve`，不会逐次扩容 |
| `entityTransform.Apply` | **确实每次新建旋转矩阵**（`CreateRotationY`/`CreateRotationX`），是真低效，但按 432 次算只有十几微秒，**不足以解释 300 µs** |

**所以 300 µs 仍未被归因到具体某行。** 链路的下一层需要更细的打点（例如 `AddFace` 调用计数与累计耗时），本次未做完。

### 诊断代码的处理（原则 13）

本次所有打点（`Trace` 开关、`TraceEntityBuild`、三段计时、`entity-split` 打印）都是**临时**的，已**全部删除**：

- `EntityModelMesher` 与 `WindowCommand` 恢复原状，全仓 grep 无残留
- 删除后构建通过、8 个程序集测试全绿、四项门禁通过——即原则 13 要求的"全量移除后行为不变"
- 结论只保留在文档里，不留在代码里

### 发射路径逐项排除后，仍差最后一层

继续沿 `BuildCompiledEntity`（`model.Compiled` 的快速路径）逐项检查，全部**读代码排除**：

| 项 | 结论 |
| --- | --- |
| `BuildInto` 的前置计数循环 | 只数立方体给 `Reserve` 用，几十次迭代 |
| `Reserve` / 数组扩容 | 已按需预留，不逐次扩容 |
| `skinAtlas.Get(path)`（每立方体 2 次） | `Dictionary.TryGetValue`，~50 ns，216 次约 11 µs |
| `new EntityModelPart(...)`（每立方体 1 次） | **`readonly record struct`**，无堆分配 |
| `ResolveEntityModel` | 静态查表，计划段实测仅 15 µs |
| `PrepareAnimationPoses` | 实测 11 µs |
| `WriteVertex` / `TransformUv` | 纯赋值与算术 |
| `entityTransform.Apply` | **确实每次新建旋转矩阵**，是真实低效点，但 432 次只有十几微秒 |

**300 µs 仍未被归因。** 上述每一项单独都只值几微秒到十几微秒，加起来远不到 300。这说明要么成本分散在尚未检查的调用里，要么存在一处我没读到的热点。

### 已确认可以独立优化的点（与瓶颈归因无关）

`EntityModelTransform.Apply` 每次调用都执行 `Matrix4x4.CreateRotationY(X)`——**旋转矩阵对同一个实体是常量**，应提到每次实体构建之外只算一次。虽然只值十几微秒，但改动极小、无风险。

### 下一步（明确、可执行）

在 `BuildCompiledEntity` 内部加计数器：`Emit` 被调用次数、`node.Cubes` 总数、以及 `foreach (var cube in node.Cubes)` 循环内的累计耗时。**打点查完即删**，与本次同一处理方式。

现在的状态是：链路已确认到函数（`BuildCompiledEntity`），函数内 300 µs 的去向未知。

## 19. VS/ETW 采样定位：是 CPU 侧，且成本在 `Emit` 的函数体内

### 先回答"CPU 还是 GPU"

用 `dotnet-trace`（`Microsoft-DotNETCore-SampleProfiler`，15 秒）采样，转 Speedscope 后按线程拆分（`tools/verify/speedscope-threads.py`）：

**GPU 侧不是问题**：`DrawIndexed` 0.81%、`CommandEncoder.Finish()` 0.75%、`Queue.WriteBuffer` 0.28%、`Surface.Present()` 0.43%。

**CPU 侧是问题**，且混线程视图被误导过：直接 `topN` 的头部全是 `SemaphoreSlim.WaitCore`(46%)、`IOCompletionPoller.Poll`(12%)、`Winsock.accept`(6%)——那是 8 个区块工作线程与 MCP 线程的空等，不是渲染。必须按线程拆分才看得见。

### 实体网格构建的独占时间

| 函数 | 独占 | 含子调用 |
| --- | --- | --- |
| **`Emit`**（`BuildCompiledEntity` 内的局部函数） | **3146 ms** | 17575 ms |
| `BuildLayers` | 2486 ms | 22782 ms |
| `BuildModelBackedLayers` | 2484 ms | 20282 ms |
| `BuildInto` | 1785 ms | 13801 ms |
| `BuildCompiledEntity` | 1720 ms | 11735 ms |
| `ResolveEntityModel` | 415 ms | 2896 ms |
| `AppendGeometry` | 204 ms | 816 ms |
| `AddPart` / `AddFace` / `WriteVertex` / `TransformUv` | 21 / 12 / 4 / 4 ms | — |

**`Emit` 的自身代码独占时间最大**，超过它所有子调用的总和。这说明成本不在被调用的函数里，而在 `Emit` **函数体内联展开的那段代码**：每个立方体构造一次 30 字段的 `EntityModelPart` 结构体、计算 `min`/`max`、做两次图集查找、调用一次 `TextureSelector` 委托。

这与之前"逐个排除子函数都只有几微秒"的观察**完全吻合**——真正的时间在**每立方体重复构建静态数据**上，而 `EntityModelPart` 是结构体，构造被内联，所以独占时间全落在 `Emit` 名下。

### 优化方向（明确）

`EntityModelPart` 的**绝大多数字段来自模型静态数据**（名称、纹理坐标、尺寸、父节点、Uv 尺寸等），每帧不变的只有 `Bottom + yOffset` 与姿态矩阵。

**应当在模型解析时把每个 (node, cube) 的 `EntityModelPart` 预先算好并缓存**，每帧只更新随帧变化的少数字段，而不是把 30 个字段从头拷一遍。

### 工具

- `tools/verify/speedscope-threads.py`：按线程汇总 Speedscope profile 的独占/含子时间
- 采样命令：`dotnet-trace collect -p <pid> --providers Microsoft-DotNETCore-SampleProfiler --duration 00:00:15`，再 `dotnet-trace convert --format Speedscope`

### 原版的做法（为了等效，不是为了复刻）

读 `ModelPart.java`（`ref://mc-26.3-analysis/src/net/minecraft/client/model/geom/ModelPart.java`）确认的数据结构：

| 结构 | 性质 |
| --- | --- |
| `ModelPart.cubes` | `final List<Cube>`，**构造时建好** |
| `Cube.polygons` | `final Polygon[]`，**构造时建好** |
| `Polygon` | `record (Vertex[] vertices, Vector3fc normal)`，**构造时建好** |
| `Vertex` | `record (x, y, z, u, v)`，存**模型空间坐标与 UV**，**构造时建好** |

每帧 `Cube.compile` 的全部工作：

```
for (Polygon polygon : polygons)
    normal = pose.transformNormal(polygon.normal)
    for (Vertex vertex : polygon.vertices)
        pos = matrix.transformPosition(vertex.x, vertex.y, vertex.z)
        builder.addVertex(pos, color, vertex.u, vertex.v, ...)
```

**即：每帧只做"用姿态矩阵变换预建顶点并发射"，不重建任何数据。**

### 对照我们的实现，差异在三点

| 项 | 原版 | 我们 |
| --- | --- | --- |
| 立方体描述 | 构造时建好、持有 | **每帧每立方体重建 30 字段结构体** |
| 纹理选择 | 每实体解析一次（在渲染器层） | **每立方体调用一次 `TextureSelector`**，其中装备层转发到 `ResolveEquipmentTexture`（注册表查找 + 资源路径解析） |
| 每帧工作 | 变换 + 发射 | 变换 + 发射 **+ 上述重建** |

### 所以优化方向是"把静态数据搬出每帧循环"，与等效一致

这不是为了更快而改变行为——**原版本来就是这么组织的**。改成：

1. 每实体每帧解析一次上下文（纹理、色调、yOffset、姿态矩阵）
2. 每 (node, cube) 的静态描述**缓存**，模型解析时建一次
3. 每帧只更新随帧变化的少数字段并变换发射

三项差异会同时被消掉：结构体重建、重复图集查找、重复选择器调用。**这正是 `Emit` 独占 3146 ms 覆盖的那段代码。**

### 待办

实现上述缓存重构。改动集中在 `EntityModelMesher.BuildCompiledEntity` 与其缓存结构；验收用既有的同进程 A/B（`MC_AB_TOGGLE` + `AddAbSample`）对比 `prep-entities`，并保证画面逐像素不变。

## 20. 缓存实现与实测：9% 的改善，以及一个新线索

### 实现

`EntityModelPartNode` 增加惰性缓存 `PartsFor(count)`，`Emit` 的立方体循环改为：

- `foreach` → 索引 `for`（顺带消除接口枚举器）
- `EntityModelPart` 命中缓存则复用，只在未建过或 `yOffset != 0` 时重建

等效性依据：`EntityModelPart` 的全部字段来自不可变的节点与立方体，唯一逐实体变化的是 `yOffset`，因此 `yOffset == 0` 时缓存值恒等于重新构造的值；非零时走原构造路径。

### 实测（跨运行，按可见实体数归一化）

| 配置 | 可见实体 | `prep-entities` | **每实体** |
| --- | --- | --- | --- |
| 有缓存 | 2 | 0.706–0.783 ms | **0.368 ms** |
| 无缓存 | 3 | 1.163–1.252 ms | **0.404 ms** |

**约 9% 改善。** 注意对比必须按实体数归一化——最初"缓存后 0.71 比改动前 0.47 更差"的表象，其实是那一次场景里有 2 个可见实体而非 1 个。

### 这否定了什么

结构体构造只占那 300 µs 的九分之一。**即便缓存后，每实体仍需 0.37 ms**，成本在 `Emit` 体内的其他地方。

### 新线索：接口枚举器装箱

`Emit` 的立方体循环原本是 `foreach (var cube in node.Cubes)`，而 `Cubes` 声明为 `IReadOnlyList<EntityModelCube>`——**通过接口 foreach 会装箱枚举器**，每节点每帧一次堆分配。

全文件还有 **11 处**同类 `foreach`（`model.Parts`、`node.Children`、`compiled.Cubes`、`model.RenderLayers` 等）。改为具体数组类型（`EntityModelCube[]` / `EntityModelPartNode[]`）后 `foreach` 走数组快路径、零分配，且行为不变——属于"比原版更激进但等效"的许可范围。

**这是下一步最明确、最省力的改动**：把上述集合的声明类型从 `IReadOnlyList<T>` 换成 `T[]`。

### 枚举器装箱：改了，但收益不可测

做法：`EntityModelPartNode` 保留 `IReadOnlyList<T>` 属性不变（避免波及 25 处调用点），另加 `CubeArray` / `ChildArray` 两个数组类型属性指向同一份数据；热循环改用数组属性，`foreach` 走数组快路径、不再装箱枚举器。

**实测：无差异。** 1 个可见实体时 `prep-entities` 为 0.48–0.53 ms，与改动前基线 0.47–0.53 ms 重叠。

**结论**：每帧十几个小对象的装箱不足以在毫秒级指标上体现。改动保留是因为零风险且消除了稳态垃圾，但**不把它算作性能收益**。

### 至此，`Emit` 内 300 µs 的归因状态

已消除：结构体重建（−9%）、枚举器装箱（约 0）。
仍未知：其余约 90%，在 `Emit` 体内剩下的代码里（`min`/`max` 向量运算、两次图集查找、`AddPart` 调用链）。

**下一步唯一可靠的办法**是在 `Emit` 体内**逐行分段计时**（不是按函数），例如把 `min`/`max` 计算段与精灵解析段各自包起来。这需要一个能开关的细粒度探针，用完即删。

## 21. 【重要更正】此前所有性能结论都是在 Debug 构建上测的

### 事实

本文件 §16–20 的全部测量，运行的都是 `src/Sharpcraft.Client/bin/Debug/net10.0/Sharpcraft.Client.exe`。
`Directory.Build.props` 只在 `Release` 配置下设置 `<Optimize>true</Optimize>`，因此那些数字来自**未优化的构建**。

用 Release 重测（同一场景、同一 `MC_PRESENT_MODE=immediate`、VD12、1 个可见实体）：

| 指标 | Debug（此前全部结论） | **Release** | 倍率 |
| --- | --- | --- | --- |
| `frame` | 2.4–2.8 ms | **1.80–2.23 ms** | 1.4× |
| `prep-entities` | 0.47–0.53 ms | **0.142–0.172 ms** | **3.3×** |
| `submit` | 0.52 ms | **0.708 ms** | — |
| `oit` | 0.34 ms | **0.15 ms** | 2.3× |

### 更正什么

- **"`entityLayers()` 占整帧 30%"是错的。** Release 下它占约 **8–10%**。
- **"每顶点 350–700 纳秒、比正常慢 30–70 倍"是 Debug 代码生成的产物。** Release 下这块成本回落到合理区间。
- §19 的采样占比（`Emit` 独占 3146 ms）同样来自 Debug 进程，**不能当作 Release 的热点分布**。

### 保留下来的结论

以下结论与构建配置无关，仍然成立：

- GPU 侧不是瓶颈（Debug 下占比已 <1%，Release 只会更低）
- 原版实体模型的组织方式（静态数据构造一次、每帧只变换发射）——这是**设计对照**，不是性能结论
- 立方体描述符缓存（−9%）——该比例在 Debug 下测得，Release 下需重测，但改动本身无风险
- 跨运行对比不可靠、同进程 A/B 才可信——方法论结论不受影响

### 教训

**性能测量必须先确认构建配置。** 这次错误让整条排查链的方向偏了两轮：所谓"每实体 390 微秒"在 Release 下是约 150 微秒/帧（含多实体），量级完全不同。

今后所有性能数字 MUST 标注构建配置，且对比原版时 MUST 用 Release。

### Release 下的真实分布：帧是等 GPU 的

同一 Release 进程的完整阶段表（`frame=2.233` 的样本；典型 `frame≈1.80`）：

| 阶段 | ms | 性质 |
| --- | --- | --- |
| **`submit`** | **0.821** | |
| ├ `sub-finish` | **0.601** | **等待 GPU 完成** |
| └ `sub-queue` | 0.218 | CPU 提交 |
| **`swap`** | **0.396** | 呈现 / 等待 |
| `oit` | 0.190 | CPU 记录 |
| `prep-entities` | 0.172 | CPU 构建 |
| `scene-terrain` | 0.137 | CPU 记录 |
| `scene-entities` | 0.083 | CPU 记录 |
| `prep-pump` / `prep-ui` / `cel-build` / `acquire` / `camera` | 0.05–0.08 | CPU |

**`sub-finish` + `swap` ≈ 1.0 ms，占整帧约 55%，都是"等 GPU"。**

也就是说：

- 整帧 **不是 CPU 受限**，而是**GPU / 呈现受限**
- 全部 CPU 准备工作加起来约 0.5–0.6 ms
- 我花了大半轮追的 `entityLayers()` 是 **0.17 ms，约 8%**

### 这对后续方向的意义

此前把"GPU 计数器太吵、放弃 RenderDoc"当作结论是**在 Debug 构建上做的判断**，而那时 CPU 侧被放大、GPU 等待被掩盖。**Release 下 GPU 才是主项，应重新用 RenderDoc 看 GPU 侧各 pass 的耗时分布。**

下一步：在 Release 构建上做 RenderDoc 捕获，看哪一类 pass（地形 / OIT / 天空 / UI）吃掉那 ~1.2 ms 的 GPU 时间。

### RenderDoc 捕获（Release）：GPU 共 1.53 ms

捕获文件：`src/Sharpcraft.Client/bin/Release/net10.0/Sharpcraft_frame18063.rdc`（228.9 MB）。
用 `tools/verify/rdoc-gpu-timings.py` 读取 GPU 计时。

| 指标 | 值 |
| --- | --- |
| **GPU 总时长** | **1526.56 µs（1.53 ms）** |
| 索引总数 | 9,132,834（**3,044,278 三角形**） |
| 平均每三角形 | 0.50 ns |
| CPU 帧时长（同配置 VD12） | 1.75 ms |

**GPU 1.53 ms 对 CPU 1.75 ms——帧确实是 GPU 受限**，与 §21 的阶段表（`sub-finish` 0.44 ms + `swap` 0.26 ms）互相印证。

### 有意思的是耗时分布

| 事件 | 索引数 | 耗时 |
| --- | --- | --- |
| 1545 | 218,526 | 67.58 µs |
| 1306 | 218,526 | 62.46 µs |
| 1067 | 218,526 | 58.37 µs |
| 1607 | 558 | **48.13 µs** |
| 1557 | **3** | **44.03 µs** |
| 1055 | 3 | 19.46 µs |
| 818 | 3 | 12.29 µs |
| 721 | 56,508 | 10.24 µs |
| 后续多个 | 25k–65k | 7–8 µs |

两个反差：

1. **`idx=3` 的全屏 pass 花 44 µs**，而 21.8 万索引的地形绘制只要 60 µs——全屏 pass 是**填充率受限**，地形是几何量受限。
2. **前 15 个事件合计约 372 µs，只占 1.53 ms 的 24%**，其余 76% 分散在大量小绘制里（多为 7–8 µs 的区块绘制）。

### 待办

- 给捕获加调试标记（RenderDoc markers），才能把每个 pass 与代码对应起来；当前 `customName` 为空，只能按索引数与耗时推断
- 确认区块绘制次数与单次成本，判断是否值得做更激进的合批

### 按着色器分组：GPU 成本集中在大量区块绘制

捕获里没有调试标记、着色器资源也没有名字，但**着色器资源 ID 是每次绘制唯一稳定的身份**。用 `tools/verify/rdoc-event-detail.py` 按（顶点着色器, 片元着色器）分组统计 GPU 时长：

| 着色器对 | 时长 | 占比 | 绘制数 | 最大索引 |
| --- | --- | --- | --- | --- |
| **1140 / 1141** | **704.5 µs** | **46.4%** | **231** | 57,594 |
| 1144 / 1145 | 233.5 µs | 15.4% | 92 | 65,064 |
| 1286 / 1287 | 93.2 µs | 6.1% | 57 | 13,848 |
| 1283 / 1284 | 86.0 µs | 5.7% | 40 | 11,556 |
| 1280 / 1281 | 69.6 µs | 4.6% | 50 | 13,848 |
| 1276 / 1277 | 67.6 µs | 4.5% | 1 | 218,526 |
| 1271 / 1272 | 62.5 µs | 4.1% | 1 | 218,526 |
| 1266 / 1267 | 59.4 µs | 3.9% | 1 | 218,526 |
| 1292 / 1293（`vs_fullscreen`） | 43.0 µs | 2.8% | 1 | 3 |
| 1635 / 1636 | 37.9 µs | 2.5% | 1 | 558 |
| 1289 / 1290（`vs_fullscreen`） | 19.5 µs | 1.3% | 1 | 3 |
| 1305 / 1306（`vs_fullscreen`） | 12.3 µs | 0.8% | 1 | 3 |

**全部 501 次绘制合计 1518 µs。**

### 结论：地形是主项，且被切成大量小绘制

前两组（231 + 92 = **323 次绘制**）合计 **61.8%** 的 GPU 时间，单次最大索引只有 5.7 万——即**地形被切成三百多次绘制**。而索引 21.8 万的三个大绘制各只要 60 µs 左右。

`vs_fullscreen` 的三个全屏绘制（各 3 个索引）合计 74.8 µs，是填充率受限的后期 pass。

**优化杠杆是合批，不是减少几何。**

### 可靠性与已知限制

- **分组结构稳定**（哪些着色器、各有多少绘制），这部分可信
- **单个事件的微秒值在两次重放间剧烈波动**（同一次 55k 索引的绘制，一次测 71 µs、另一次 801 µs），与 §16 记录的"GPU 计数器噪声 ±2×"一致。**因此上表的百分比只能看量级，不能当作精确预算。**
- 着色器源码未保留在捕获中，只能读到入口名，无法自动把 ID 映射到 `terrain.wgsl` / `oit_*.wgsl`。要精确命名需要渲染器推 RenderDoc 标记（属于诊断代码，需按原则 13 隔离）。

### 工具

`tools/verify/rdoc-event-detail.py`：按着色器对聚合 GPU 时长、列出最贵事件及其管线详情。环境变量 `MC_RDOC_CAPTURE` / `MC_RDOC_OUTPUT` / `MC_RDOC_TOP`。

### 渲染器自报的每帧数字（Release，VD12，退出汇总）

```
window: 44930 frames in 85.0s, worst frame 452.4 ms at #1, 2 frames over 33 ms
window: arena 637 chunks, presized=False, memory 405.0 MB, uploaded 453.1 MB, 0 full rebuilds
window: cutout 635 chunks, memory 130.5 MB
window: translucent 627 chunks, memory 63.0 MB
window: draws/frame 1037 (solid/cutout/translucent stores)
window: binds/frame 311.2 (vertex+index), pages 90/29/14, live 543.8 MB of 598.5 MB in 133 pages
window: cpu per frame over 44930 frames: frame=1.883 ... prep-entities=0.207 ...
```

| 指标 | 值 |
| --- | --- |
| **draws/frame** | **1037** |
| 区块 | solid 637 / cutout 635 / translucent 627 |
| binds/frame | 311.2 |
| 页 | 90 / 29 / 14（133 页，543.8 MB） |
| 帧时长 | **1.883 ms（约 528 fps）** |

### 这是 GPU 成本的来源

1037 次绘制 / 帧，按捕获的 1.52 ms GPU 分摊约 **1.5 微秒/绘制**。**几何量不是瓶颈，绘制次数是。**

`ChunkGeometryStore` 的设计是"多区块共享少数页、绑定页一次、每区块一个区间发一次绘制"——页绑定已经省掉了，但**绘制次数仍等于可见区块数**。相邻区块若在同一页且索引连续，理论上可以合并成一次 `DrawIndexed`（不透明几何与顺序无关，合并安全）；代价是失去逐区块视锥剔除粒度。

**这是目前最明确的 GPU 侧优化方向：把页内相邻区间合并成更少的绘制。**

### 与"304 万三角形"的关系

捕获里 304 万三角形 vs 网格统计的 34 万，两者**不是同一时刻的镜头**（捕获时区块数与本次不同，捕获那次 501 次绘制、本次 1037 次）。**不能据此断言重复绘制。** 要确认需在同一帧上同时取两个数——目前做不到，因为三角形统计只在退出时打印一次。

## 22. 原版怎么解决"区块绘制太多"：遮挡剔除，不是合批

读 `ref://mc-26.3-analysis/src/net/minecraft/client/renderer/SectionOcclusionGraph.java`（500 行）。

### 原版的机制

**每个区块节点记录 6 个方向的可见性位掩码**：

| 字段 | 含义 |
| --- | --- |
| `sourceDirections` | 该节点被哪些方向"看到"（进入可见集的方向） |
| `directions` | 从该节点能继续传播出去的方向 |
| `step` | BFS 距离（用于排序/预算） |

**传播规则**（`runUpdates` 内的 BFS 队列）：

```
对当前节点，遍历 6 个方向：
  取该方向的相邻区块
  若邻居存在，且（不启用 smartCull 或 该方向未被标记为遮挡）：
      若节点有来源方向：
          对每个来源方向 i：
              若 sectionMesh.facesCanSeeEachother(来源方向的反面, 目标方向)
                  则邻居可见
```

`facesCanSeeEachother` 来自区块网格自身记录的"哪些面之间是通透的"。因此**可见性是沿"能看穿的面"洪泛传播的**，不是逐区块独立判断。

配套：

- `emptySections`：空区块把可见性**原样透传**，不打断传播链
- 高级剔除的启用距离：`MINIMUM_ADVANCED_CULLING_SECTION_DISTANCE = blockToSectionCoord(60)`（60 格以内不做）
- `CEILED_SECTION_DIAGONAL = ceil(sqrt(3) × 16)`（对角线长度，用于保守距离判断）

**所以原版不是把多次绘制合并成一次，而是让大量区块根本不可见、连绘制都不发。** 它同样保持"每可见区块每层一次绘制"的结构。

### 我们的现状：主渲染路径只有视锥剔除

| 项 | 原版 | 我们 |
| --- | --- | --- |
| 视锥剔除 | 有 | ✅ 有（`ChunkGeometryStore.Draw(pass, frustumPlanes)`） |
| **遮挡剔除（主窗口路径）** | **有（可见性图洪泛）** | ❌ **无** |
| 遮挡剔除（离屏帧路径） | — | 有（`OcclusionCulling`，仅 `FrameCommand` 使用） |

`ChunkGeometryStore` 第 212 行只有 `FrustumPlanes.IsVisible(...)`；`OcclusionCulling.TryProjectBox / IsOccluded`（89 行，CPU 侧投影 + 深度比较）**只在 `FrameCommand.cs` 的确定性帧路径里被调用 4 次**，主窗口渲染完全没用。

**这意味着：被墙挡住、被地形遮住的区块，我们现在照样逐个发绘制。** 这正是 1037 次绘制/帧的来源之一。

### 下一步（与原版等效）

把已有的 `OcclusionCulling` 接入主渲染路径，或按原版思路做可见性传播。两条路都算等效——原则 11 允许"等价或更优的管线组织"，可观测结果（哪些区块被画出来）一致即可。

优先做侵入最小的那条：**在 `ChunkGeometryStore.Draw` 的视锥剔除之后加一层遮挡判断**，用上一帧的深度做遮挡源（原版也是基于渲染结果反馈的）。

### 1037 次绘制的真正来源：参考 OIT 把透明层画了三次

`MC_CULL_STATS=1` 的实测：

```
cull: chunks=637 culled=385 cutout=635/drawn=239 translucent=627 translucentDrawn=186 translucentCulled=441 drawn=252 tri=1664176
window: oit commands per frame bounds=186d/1b/1p trans=186d/2b/1p accum=186d/2b/1p
```

| 项 | 数量 |
| --- | --- |
| solid 可见（637 中剔除 385） | **252 次绘制** |
| cutout 可见（635 中） | **239 次绘制** |
| translucent 可见（627 中剔除 441） | 186 次绘制 |
| **OIT：bounds / transmittance / accumulate** | **186 × 3 = 558 次绘制** |
| 合计 | **252 + 239 + 558 ≈ 1049**，与 `draws/frame 1037` 吻合 |

**视锥剔除本身工作正常**（solid 剔除 385/637 = 60%）。绘制次数被放大的主因是**参考版 OIT 把透明几何画了三次**（三遍：边界、透射率、累积）。

### 这改变了优化优先级

| 方向 | 潜在收益 | 代价 |
| --- | --- | --- |
| **减少 OIT 三遍** | 一次去掉 **约 372 次绘制**（558 → 186） | 需要真正的 OIT 实现（或在特定条件下退化到单遍排序透明） |
| 遮挡剔除 | 减少 252 + 239 + 186 中被墙挡住的那些 | 需要深度源或可见性图，多文件改动 |

**OIT 三遍是更大、更明确的一块**，而且它本身已经是登记在案的"参考实现"（日志里标注 `(reference oit)`），属于原则 15 意义上的刻意简化。

### 另一个待核实线索

`tri=1664176` 是 252 次 solid 绘制合计，平均**每区块 6600 个三角形**；而 16³ 区块理论最多约 3072。差 2 倍多。

网格器确实做了邻块面剔除与贪心合并（`Neighbourhood` / `Occludes` / `MergeSectionFaces`），所以这不像"漏剔除"，更可能是**区块尺寸大于 16³**或当前镜头下的区块普遍很密。**未核实，仅登记为线索。**

### 【更正】OIT 三遍不是冗余，那就是原版的算法

上一节把"透明层被画三次"当成可优化的绘制膨胀，**这是错的**。

对照原版 `ref://mc-26.3-analysis/src/net/minecraft/client/renderer/oit/OitPipelineSet.java`：

```java
public record OitPipelineSet(RenderPipeline depthBoundsPipeline, RenderPipeline transmittancePipeline,
                             RenderPipeline accumulatePipeline, ...)
    case DEPTH_BOUNDS  -> depthBoundsPipeline
    case TRANSMITTANCE -> transmittancePipeline
    case ACCUMULATE    -> accumulatePipeline
```

**原版的 OIT 就是三个阶段**：深度边界 → 透射率 → 累积，外加合成。

我们的实现（`ReferenceOitPasses` 的类注释）：

> depth bounds, the opaque-depth cull, the cloud sub-passes, wavelet transmittance, accumulate, and the composite

**逐阶段一一对应。** 运行期统计也吻合：`bounds=186d` / `trans=186d` / `accum=186d`。

**所以那 558 次绘制不是浪费，是算法固有成本，原版同样付出。** 类名里的 "reference" 指的是"参考实现（原版）的 pass 序列"，不是"参考用的临时实现"——这是我上一轮读错的词。

### 连带更正

- ❌ "1037 次绘制里超过一半是 OIT 浪费" → ✅ 是原版同款三阶段
- ❌ "先做 OIT 单遍化，能省 372 次绘制" → ✅ 单遍化会**改变渲染结果**，不再等效，属于原则 9 的"性能驱动的行为偏差"
- ✅ 视锥剔除正常（solid 637 剔除 385）

### 目前的真实位置

| 项 | 我们 | 原版 |
| --- | --- | --- |
| OIT | 三阶段 + 合成 + wavelet | 三阶段 + 合成 |
| 区块绘制 | 每可见区块每层一次 | 同 |
| 视锥剔除 | 有 | 有 |
| 遮挡剔除 | **主窗口路径无** | 有（`SectionOcclusionGraph`） |
| 帧时长（858×527, VD12） | 1.88 ms | ~1.6 ms |

**唯一确认的结构性差距仍然是遮挡剔除**，不是 OIT。剩下约 0.3 ms 的差距，量级上也与"被遮挡的区块仍在绘制"相符。

## 23. 原版抓帧对照：绘制次数我们更少，三角形我们多 2.3 倍

对照捕获：用户提供的原版 Java/OpenGL 抓帧（文件名 `苦力怕启动器_2026.11.11_13.46.36_frame7336.rdc`，位于 RenderDoc 的默认捕获目录）。

用同一套 `tools/verify/rdoc-*.py` 读取：

| 项 | 我们（Release） | 原版（Java/OpenGL） |
| --- | --- | --- |
| **绘制次数** | **约 1037** | **1905** |
| **三角形** | **3,044,278** | **1,345,018** |
| 索引总数 | 9,132,834 | 4,035,055 |
| 单绘制最大索引 | 218,526 | 219,660 |
| GPU 时长 | 1.53 ms | 94 ms（不可用，见下） |

### 这推翻了"原版靠减少绘制次数取胜"

**原版的绘制次数比我们还多**（1905 vs 1037）。它并没有把区块合批成更少的绘制——反而在用 `glMultiDrawElementsIndirect` 的同时仍发大量独立绘制。

**真正的差距是几何量：我们画了原版 2.3 倍的三角形。**

### 原版 OIT 也有三个阶段

原版捕获里可见三组大面积绘制（661 / 166 / 571 次，各自对应一个着色器对），与 `OitPipelineSet` 的 DEPTH_BOUNDS / TRANSMITTANCE / ACCUMULATE 三阶段吻合——**再次印证 §22 的更正：三遍是算法，不是浪费。**

### 可靠性说明

- **原版捕获的 GPU 时长不可用**：总计 94 ms，其中三个事件分别 30/23/22 ms，那明显是管线停顿或着色器编译，不是稳态帧。因此**只能采信绘制次数与三角形数**，不能比较 GPU 时间。
- 两次捕获的**视角、可视距离、场景并不相同**，所以"2.3 倍"是量级参考，不是精确比值。

### 待核实

我们的网格器是 16³ section + 贪心合并，理论上应比原版**更少**三角形，实测却多 2.3 倍。

`cull:` 统计报 `drawn=252 tri=1664176`，即每"区块"6604 个三角形，而 16³ section 理论最多约 3072。**但 `chunks=637` 的统计口径尚未核实**——637 更接近 VD12 的**区块列数**（625），而不是 section 数。因此这个"每区块 6604"可能是把多个 section 混算，**不能据此断定网格器有问题**。

**下一步应先核实 `ChunkGeometryStore` 的 `ChunkCount` / `TrianglesLastDraw` 究竟按什么单位统计**，再判断三角形数是否需要追。

## 24. 核实结果：剔除粒度是"整根区块列"，原版是"单个 section"

### 统计口径核实

| 事实 | 证据 |
| --- | --- |
| 一个 entry = **一整根区块列** | `ChunkWorkerPool` 里 `var (chunkX, chunkZ) = ClientWorld.Unpack(chunkKey);` —— key 只含 x/z，不含 y |
| `ChunkCount` = entry 数 | `public int ChunkCount => _entries.Count;` |
| `TrianglesLastDraw` = 被绘制 entry 的 `IndexCount / 3` 之和 | `TrianglesLastDraw += entry.IndexCount / 3;` |
| 列高 = 24 sections × 16 = **384 格** | `sectionCount: 24, minSectionY: -64`（Y 从 -64 到 320） |

**因此"每区块 6604 三角形"实际是"每根 384 格高的列 6604 三角形"，完全合理。§23 里对网格器的怀疑不成立——网格器没有多生成几何。**

### 真正的问题：剔除粒度差一个数量级

| | 我们 | 原版 |
| --- | --- | --- |
| 剔除单位 | **区块列：16×384×16** | **section：16×16×16** |
| 体积比 | **24 倍** | 1 |
| 后果 | 列内 24 个 section 只要有**任何一个**进入视锥，**整列 24 个全部绘制** | 只绘制进入视锥的 section |

这直接解释了 §23 的观测：**我们画了原版 2.3 倍的三角形**。相机水平看出去时，整列中位于地表以下、高空、以及视锥垂直范围之外的 section 全都被画了出来。

`ChunkGeometryStore.Draw` 的剔除只有一处：

```csharp
if (planes.Length >= 6 && !FrustumPlanes.IsVisible(planes, entry.Minimum, entry.Maximum))
```

而 `entry.Minimum/Maximum` 是**整列的包围盒**。

### 这是等效性问题，不只是性能问题

原版按 section 剔除，所以"哪些几何被画"由 16³ 的可见性决定。我们按列剔除，多画的 section **虽然不影响最终像素（被深度遮挡或不透明）**，但确实改变了提交给 GPU 的几何集合。

按原则 9，减少这部分几何不需要登记为行为偏差——**它是在向原版靠拢**。

### 修复方向

把剔除单位从"列"细化到"section"：

1. 网格仍在列级别构建（合并邻块查询方便），但**记录每个 section 在列网格中的索引区间**
2. `Entry` 保存这些区间
3. `Draw` 时对每个 section 做视锥测试，只对通过的区间发绘制（或合并相邻可通过区间以控制绘制次数）

收益预估：三角形数从 304 万降到接近原版的 135 万量级，即 **GPU 侧约可减半**。绘制次数会增加，但原版本来就发 1905 次，不构成问题。

## 25. 用确定性计数对照（可信）：我们每次绘制背 9 倍几何

GPU 时长计数器在两次重放间波动剧烈（同一个 55k 索引的绘制，一次 71 µs、一次 801 µs），**但 `numIndices` / `numInstances` 是确定性计数**，可以采信。按着色器对聚合后：

### 我们（Release，858×527，VD12）

| 着色器对 | 时长 | 占比 | 绘制 | **索引** |
| --- | --- | --- | --- | --- |
| 1140 / 1141 | 721.92 µs | 46.2% | 230 | **4,775,214** |
| 1144 / 1145 | 239.62 µs | 15.3% | 95 | 1,352,088 |
| 1286 / 1287 | 96.26 µs | 6.2% | 54 | 282,060 |
| 1283 / 1284 | 93.18 µs | 6.0% | 45 | 216,180 |
| 1280 / 1281 | 79.87 µs | 5.1% | 59 | 298,950 |
| 1276 / 1277 | 70.66 µs | 4.5% | 1 | 218,526 |
| 1271 / 1272 | 62.46 µs | 4.0% | 1 | 218,526 |
| 1266 / 1267 | 60.42 µs | 3.9% | 1 | 218,526 |
| 1292 / 1293（全屏） | 49.15 µs | 3.1% | 1 | 3 |

### 原版（Java/OpenGL）

| 着色器对 | 时长 | 占比 | 绘制 | **索引** |
| --- | --- | --- | --- | --- |
| 1534 / 1535 | 33600.51 µs | 35.4% | 661 | 1,528,524 |
| 1689 / 1690 | 29962.24 µs | 31.6% | 166 | **207,984** |
| 1507 / 1508 | 25175.04 µs | 26.5% | 571 | 1,217,760 |
| 1098 / 1099 | 1828.86 µs | 1.9% | 166 | 207,984 |
| 1732 / 1733 | 1197.06 µs | 1.3% | 1 | 219,660 |

### 结论

| 指标 | 我们 | 原版 | 比值 |
| --- | --- | --- | --- |
| 绘制次数 | **515** | **1903** | 0.27× |
| 索引总数 | **7,581,318** | 4,034,899 | **1.88×** |
| 三角形 | 2,527,106 | 1,344,966 | 1.88× |
| **单次绘制平均索引** | **14,721** | **2,120** | **6.9×** |

**我们的绘制次数只有原版的 27%，但每次绘制背着约 7 倍的几何。** 这与 §24 的结论互为印证：**剔除单位是 384 格高的整列，所以每次绘制都是一整根列**，而原版每次绘制是一个 16³ section。

### 另一处发现：原版的 1689/1690 是填充率受限

原版该组只用 **207,984 索引**（166 次绘制）却占 **31.6%** 的 GPU 时长——平均每次绘制 1,253 索引，却和 661 次绘制共 153 万索引的组耗时相当。这说明它是**填充率或着色器复杂度受限**的 pass，而非几何受限。

**我们的对应组没有出现这个特征**（46.2% 集中在 478 万索引的地形组）。若后续要再压 GPU 时间，这一组原版的构成值得单独对照。

### 可靠性

- **索引/绘制/实例数是确定性计数，可采信**（同一捕获重复读取结果一致）
- **GPU 时长仍不可采信**：原版总计 94943 µs 中三个事件占 87 ms，是管线停顿；我们的 1563 µs 与另一轮 2518 µs 也相差 60%
- 两次捕获的**场景与可视距离不同**，所以 1.88 倍是量级参考

## 26. 按 section 剔除：第一步已落地（mesher 记录区间）

### 已实现

`MeshBuffer` 现在记录每一段的起始索引：

| 成员 | 作用 |
| --- | --- |
| `ReserveSections(int)` | 网格化前按 section 数一次性预留，避免热路径分配 |
| `MarkSection()` | 记录当前索引数作为该 section 的起点 |
| `ToMeshData` | 末尾补上总索引数，得到 `starts[i]..starts[i+1]` 的区间 |
| `MeshData.SectionStarts` | 随网格返回，可空 |

`ChunkMesher.MeshChunk` 在 section 循环**每次迭代开头**调用 `MarkSection()`，在跳过空 section **之前**——这样下标与 section 索引严格对齐，空 section 自然得到一个零长度区间。

测试 `ChunkMeshSectionRangesTests`（4 项）：区间数为 `sectionCount + 1`、单调不减、首元素为 0、空 section 产生零长度区间、有几何的 section 区间非空。

### 踩到的两个门禁（都已修正，值得记下）

1. **`MeshingIntoAWarmBufferDoesNotAllocate` 失败**：最初用 `List<int>` 记录边界，在热路径分配。改成预分配数组后又用了 `ArrayPool<int>.Shared.Rent`，**该测试时过时不过**——池空时 Rent 仍会分配。最终改为 `ReserveSections(count)` 显式预留，连续三轮稳定通过。
2. **`DocumentsContainNoAbsoluteWorkspacePaths` 失败**：本文档里写了 RenderDoc 捕获的绝对路径。改为文件名形式。

**这两个门禁的存在价值正在于此**：它们把"看起来能跑"的改动挡了下来。

### 未完成（下一步）

第二片尚未实现，回退后未提交：

1. `GeometryData` 增加 `SectionStarts` 与 `SectionMinY`（后者必要，因为 `BoundsOf` 给的是几何实际包围盒，不等于 section 边界）
2. `WindowCommand` 在构造 `GeometryData` 时透传 `entry.Opaque.SectionStarts` 与 `chunk.MinSectionY * 16`（`MeshedChunk` 上没有 `MinSectionY`，需要从 `ClientWorld` 取）
3. `ChunkGeometryStore.Entry` 保存区间，`Draw` 对每个 section 单独做视锥测试，并把相邻可见区间**合并**成一次绘制以控制绘制次数

第 3 步的绘制方式是安全的：同一列内各 section 的索引都相对该列的顶点基址，因此只需把区间起点加到索引偏移上，`baseVertex` 不变。

## 27. 按 section 剔除已启用：三角形降 37%

### 实测（Release，VD12，同场景）

| 指标 | 整列剔除 | **按 section 剔除** | 变化 |
| --- | --- | --- | --- |
| solid 绘制次数 | 252 | **335** | +33% |
| **solid 三角形** | **1,664,176** | **1,042,118** | **−37%** |
| `meanMs` | 1.836–2.028 | 2.0 | 噪声范围内 |

**三角形按预期下降 37%**，绘制次数上升 33%（合并相邻可见 section 之后仍然更多），帧时长**没有可分辨变化**。

这说明：**当前帧不是三角形受限，而是绘制次数/提交受限**——与 §21 阶段表里 `submit` 为最大项一致。继续加绘制换几何并不划算。

### 实现

`ChunkGeometryStore.Entry` 保存 `SectionStarts` 与 `SectionMinY`；`Draw` 在列级视锥检查通过后，若存在区间则逐 section 测试 16³ 包围盒，并把**相邻可见的 section 合并成一次绘制**：

```
_entries / SectionStarts: starts[i]..starts[i+1] 是 section i，末尾是总索引数
```

同列内各 section 索引都相对该列顶点基址，所以切片只需把区间起点加到索引偏移上，`baseVertex` 不变。

### 过程中踩的坑（值得记下）

按行号编辑 `ChunkGeometryStore` 时**把 `page.Chunks.Add(chunkKey);` 整行弄丢了**，导致 `Draw` 里的 `foreach (var page.Chunks)` 永远为空：`drawn=0`、`culled=0`、什么也不画。

**用已提交的干净版本对照才定位到**——回退客户端参数后症状不变，说明问题在 store，而不是新传的数据。

教训：这类结构性编辑应当用文本锚点整体替换，不要用行号逐行拼接；改完必须跑一次真实渲染，因为单元测试覆盖不到"页有没有登记 chunk"。

### 已知未做

- cutout 与 translucent 仍然按整列剔除（它们的绘制次数没变：239 / 186）。两者合计 425 次绘制，若同样细化，可能再降一部分三角形，但同样会增加绘制次数——在"绘制受限"的现状下**未必划算**，需实测再定。

## 28. 剔除粒度：逐段循环是回归，解析求区间才是解

### 第一版（逐段包围盒）是回归

每列对 24 个 section 各做一次视锥包围盒测试，637 列 ≈ **每帧 1.5 万次测试**：

| 指标 | 整列剔除 | 逐段循环 |
| --- | --- | --- |
| 绘制次数 | 252 | 410 |
| `scene-terrain` | 0.115 | **0.292** |
| frame（同进程 A/B，6 个窗口） | **1.69** | 1.87（6/6 为正，delta ≈ +0.18） |

同进程 A/B 确认是真实回归：几何少 37%，但 CPU 测试与绘制次数涨得更多。

### 改进：解析求每列可见 Y 区间

视锥平面 `(a,b,c,d)`（内部朝向、已归一化），对固定 XZ、Y 可变的一列：

- `b > 0`：最远角点在顶面 → 该平面给出**下界** `y1 ≥ -(maxX+maxZ+d)/b`
- `b < 0`：最远角点在底面 → 给出**上界** `y0 ≤ -(maxX+maxZ+d)/b`
- `|b| ≈ 0`：与 Y 无关，整列保留或整列剔除

6 个平面取交集 → **一个连续 Y 区间**。落在区间内的 section 必然连续，所以**一列仍然只发一次绘制**，且每列只需 6 次平面运算而不是 24 次盒测试。

### 实测（同位置、同分辨率、同视距）

| 指标 | 整列剔除 | 逐段循环 | **解析区间** |
| --- | --- | --- | --- |
| 绘制次数 | 252 | 410 | **219** |
| 三角形 | 1,663,176 | 1,033,468 | 少 33% |
| `scene-terrain` | 0.115 | 0.292 | **0.144–0.160** |
| frame | 1.69–1.88 | 1.87–1.92 | **1.630–1.776** |

**与原版同位置对照：原版 545 fps = 1.83 ms，我们 1.72 ms。**

### 原版为什么能按 section 剔除而不吃亏

读 `ref://mc-26.3-analysis/src/net/minecraft/client/renderer/LevelRenderer.java` 的可见 section 循环：

- 可见性按 **16³ section** 判定（视锥 + `SectionOcclusionGraph` 洪泛）
- 几何存在**共享 buffer 的 section 切片**里
- 提交时按「顶点缓冲 + 索引缓冲 + 索引类型」的 hash 把 section **分组累积 `IndexedDraw`**，最后合并成多绘制；section 的世界偏移作为**逐实例数据** `ChunkSectionInfo` 传入

**即原版不为每个 section 付一次绘制的固定开销。** 我们的 `DrawIndexed` 是逐次调用，所以按 section 切分必然增加绘制次数——这正是逐段循环回归的根因。

解析区间法绕开了这一点：它同样按 section 粒度决定画什么，但因为可见段连续，绘制次数反而比整列剔除更少。

### 已知未验证

两组使用不同剔除算法时，像素对比报告差异 **0.0723%**；两组使用相同算法（对照组）时差异 **0.0478%**。**差值在流式加载噪声量级内，既未证明正确也未证明有误。** 若要作为等效性证据，需要在静止场景（无区块加载）下重跑像素对比。

## 29. OIT 目标格式：32 位浮点改 16 位，水域场景反超原版

### 定位过程

用户报告：看向大面积水域、窗口最大化时，原版约 245 fps，我们不到 165 fps。

用 `MC_SKIP_OIT` + `MC_AB_TOGGLE` 做同进程 A/B（2534×1334）：

| 组 | 帧时 |
| --- | --- |
| A（有 OIT） | 6.53–6.73 ms ≈ 150 fps |
| B（跳过 OIT） | 1.93–2.35 ms ≈ 450 fps |

**OIT 占整帧 4.2–4.8 ms**，而 `frame-cpu` 里 `oit=0.103` 只是 CPU 记录成本——那 4.7 ms 全是 **GPU 填充**。

### 根因：目标格式比原版宽一倍

读 `ref://mc-26.3-analysis/src/net/minecraft/client/renderer/LevelRenderer.java` 的目标创建：

| 目标 | 原版格式 |
| --- | --- |
| `depth_bounds_culled` | RGBA32_FLOAT |
| `transmittance` × 2 | **RGBA16_FLOAT** |
| `accumulate` | **RGBA16_FLOAT** |

我们的 `oitTargetFormat` 是**一个格式用于全部目标**，在支持 FP32 混合的设备上是 `Rgba32Float`——于是 transmittance 与 accumulate 每条像素写 16 字节而非 8 字节。**这两个正是每个片段都要写的最重 pass。**

### 改动

新增 `oitColorFormat`（`Rgba16Float`，低精度模式仍为 `Rgba8Unorm`），只用于：

- transmittance 管线（参考 + 云端）
- accumulate 管线（参考 + 云端）
- `refTransmittance0/1Texture`、`refAccumulateTexture`

`depthBounds` 与 `depthBoundsCulled` 保持 `oitTargetFormat`（FP32），与原版一致。

着色器读取侧用 `texture_2d<f32>`，**与格式无关**，无需改动。

### 实测

| 场景 | 改前 | 改后 |
| --- | --- | --- |
| 2534×1334，水边场景 | 6.53–6.73 ms | **2.06–2.10 ms** |
| 2534×1334，水域视角 | — | **3.685–3.791 ms ≈ 268 fps** |

**水域视角：原版 245 fps，我们约 268 fps。**

### 这是一处等效修正，不是性能偏差

目标格式是**内部实现细节**，不改变可观测结果（混合结果在 FP16 精度内，原版本来就用 FP16）。按原则 9，这不属于"性能驱动的行为偏差"，而是向原版对齐。

### 已知风险

FP16 的 transmittance 可能带来色带。本次实测画面未见异常（水域、雾、天空均正常），但**未做逐像素与 32 位版本的比对**。若要作为严格等效性证据，应补一次像素对比。

## 30. 雾必须影响透射率，而不只是颜色

### 症状

半精度 OIT 改动后画面与原版基本一致，唯一差异是：**浓雾里能透过水看到后面的东西**，而原版看不到。

### 原因

`oit_reference.wgsl` 里雾只做了颜色混合：

```wgsl
let fogged = clamp((fogDistance - fog.range.x) / span, 0.0, 1.0);
let colour = mix(base, fog.colour.rgb, fogged);
```

但片段自身的透射率仍然只取 `1 - alpha`（水体纹理 alpha），**与雾无关**。于是水再浓的雾里仍保持原来的透明度，背后的地形就透出来了。

### 原版的机制

`oit_add_transmittance.glsl` 的 `addTransmittance(absorbance)` 把**透射率**编码进小波系数，`oit_composite.fsh` 里 `coverage = 1 - sampledTransmittance`——**覆盖度完全由透射率决定**。也就是说，雾如果要遮住背后的东西，就必须体现在透射率上。

### 修正

新增 `fogAmount(distance)` 辅助函数，并在**两个 pass 里使用同一份 coverage**：

```wgsl
let coverage = mix(alpha, 1.0, fogAmount(input.fogDistance));
```

- `fs_transmittance`：`absorbance = toAbsorbance(1.0 - coverage)`
- `fs_accumulate`：`sampleTransmittance(..., 1.0 - coverage)` 与 `accum = vec4(colour * coverage, coverage) * sampledTransmittance`

物理含义：雾是**表面之前的介质**，不是表面上的颜色。表面被雾淹没后，它自身的透明度不再重要——挡住背后几何的是雾而不是水。

### 踩到的坑

1. 把辅助函数插在 `@fragment` 属性与 `fn fs_depth_bounds` 之间，导致 `@fragment` 落到了辅助函数头上、入口点消失。**项目自带的 `ShaderPipelines.EnsureValid()` 在启动时立刻报错**（`entry point 'fs_depth_bounds' is missing`），没有变成运行时黑屏。
2. 修好源码后单元测试仍失败，因为 `--no-build` 用的是输出目录里**过期的着色器副本**。重建即通过——**改资产文件后不能只跑 `--no-build` 的测试**。

### 未验证

未与修正前的画面做逐像素比对，也未与原版同视角做数值比较；本次是目视确认（远处水体被雾覆盖，不再透出背后地形）。