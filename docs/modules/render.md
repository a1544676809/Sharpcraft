<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 MinecraftCSharp contributors -->

状态：[已评审]
基线：Minecraft 26.3
证据：ADR-0003（WGPU 绑定与首帧路径）；实测输出 `minecraftcsharp-frame.png`
最后更新：2026-10-28

# 渲染层（Framework / Client.Render）

把 `ClientWorld` 的方块数据变成 GPU 可绘制的几何，并产出可验证的图像。

## 1. 职责范围

负责：

- 网格生成：把方块状态转换为三角形（面剔除、跨区块邻居查询、确定性配色）。
- 相机与投影数学（右手系、WebGPU 的 0..1 裁剪深度）。
- 顶点裁剪空间变换（CPU 预变换，着色器直通）。
- 离屏渲染：纹理、清屏、索引绘制、深度测试、回读、PNG 编码。
- 帧输出的可判定检查（尺寸、若干像素颜色）。

明确不负责：

- 窗口系统、输入、交换链（后续里程碑，见 §7）。
- 纹理 / 图集与光照传播（当前只做状态配色，见 §6）。
- 原版着色器语义的对齐（原则 11，见 `docs/specs/render/`）。

## 2. 侧别归属

- 归属：网格生成与相机数学属于 `Client`（表现层）；WGPU 调用属于引擎层 `MinecraftCSharp.Framework`。
- 判定依据：AGENTS 原则 5、6——游戏逻辑不得持有 WGPU 句柄，所有图形调用收敛在引擎层。

## 3. 依赖关系

| 方向 | 对象 | 说明 |
| --- | --- | --- |
| 依赖 | `Brew.Wgpu` | 托管绑定与 `wgpu_native`，二者同版本（ADR-0003） |
| 依赖 | `MinecraftCSharp.Common`（PNG 编码） | `Common/Imaging/PngEncoder` |
| 被依赖 | `MinecraftCSharp.Launcher`（`frame` 命令） | 组装链路并落盘 |
| 不依赖 | `Client` / `Auth` / `Launcher` | 引擎层不得反向引用（架构门禁 `FrameworkMustNotReferenceGameOrHostAssemblies`） |

## 4. 对外契约

- `ChunkMesher.Mesh(ClientWorld) -> MeshData`：`float[]`（位置 3 + 颜色 3）与 `uint[]` 索引。
- `CameraMath.BuildViewProjection(eye, target, fov, aspect)` / `DefaultCamera(centre, radius)`。
- `ClipSpaceTransform.Build(FrameRequest, Matrix4x4)`：裁剪空间顶点（位置 4 + 颜色 3）。
- `OffscreenRenderer.Render(FrameRequest) -> FrameResult`：`byte[]` RGBA8 + 适配器名。
- `PngEncoder.EncodeRgba(width, height, rgba) -> byte[]`。

## 5. 关键行为与实测

| 项 | 行为 | 证据 |
| --- | --- | --- |
| 面剔除 | 邻居非空气则该面不生成；邻居跨区块时查邻块 | `ChunkMesherTests`（7 项） |
| 深度测试 | `Depth32Float`、`DepthCompare.Less`、清 1.0 | 单区块回放图：顶面遮挡侧面，无穿模 |
| 顶点变换 | CPU 乘以视图投影矩阵，着色器直通 | `CameraMathTests`（6 项） |
| 回读 | 行按 256 字节对齐后逐行裁剪 | 960×540 PNG，清屏色 140,184,242 与请求值一致 |
| 真实世界 | 9/9 区块 → 275,142 方块 / **51,402 三角形**（合并前 110,868） | `launcher frame mcf.ddger.top` 现场运行 |
| 合并正确性 | 每四边形都是轴对齐矩形、面积 ≤ 256、总面积等于逐面计数 | `ChunkMesherQuadTests`（2 项）；`ChunkMesherBoundsTests`（1 项） |
| 取景 | 以网格实际包围盒的水平足迹定半径、瞄准顶面 | 384 格高的维度不再把主体压成细条 |
| 绑定选型 | `Brew.Wgpu`（Silk.NET 原生包回读为未实现桩） | ADR-0003 对照表 |

### 5.1 窗口呈现

`WindowedRenderer.Run(request, frames, maxDuration, onFrame)` 打开窗口、按 framebuffer 物理像素配置交换链、每帧清屏并绘制同一批次后 `Present()`。截图仍由离屏路径产出（交换链纹理不保证可拷贝）。平台句柄由 `WindowHost` 统一给出，渲染器只消费 `NativeSurfaceSource`。

**尺寸相关的三件事一起重建**：交换链配置、深度纹理、顶点缓冲（裁剪空间顶点烘焙了宽高比）。任一者在 framebuffer 尺寸变化时不同步，就会出现拉伸、深度错位或每帧 `Outdated`。相机投影统一用 `CameraMath.BuildViewProjection(eye, target, fov, width, height)` 的**像素重载**，与交换链取同一个尺寸源。

**两套库的接口约定**（换掉任一方时的对照清单）：

| 项 | 约定 | 违反的后果 |
| --- | --- | --- |
| 窗口图形 API | 必须是 `GraphicsAPI.None`（不建 GL/Vulkan 上下文） | 两套图形栈争用同一窗口 |
| 尺寸来源 | 一律 framebuffer 物理像素，不用逻辑尺寸 | DPI 缩放下每帧 `Outdated`、画面不上屏 |
| 句柄形态 | Win32 `(Hwnd, HInstance)` / X11 `(Display, Window)` / Wayland `(Display, Surface)` / macOS `MetalLayer` | 表面创建失败或崩溃 |
| 表面输出生命周期 | 重配前必须先释放已获取的纹理 | wgpu-native 直接 abort |
| 纹理用途 | 交换链纹理只当渲染目标，不做拷贝源 | 截图走离屏路径的原因 |
| 表面状态判定 | 显式比较 `Outdated` / `Lost`，不用 `NeedsReconfigure` | `SuccessOptimal` 被误判，无限重配 |

### 5.2 贪婪合并（greedy meshing）

同一段内、同一朝向的相邻面，若**方块状态**与**采样到的光照**都相同，则合并成一个更大的四边形：

- 合并键 = `(state, sky, block)`。加入光照是必须的：光照不同的面合并后会得到一个单一亮度，破坏明暗。
- 每个平面（该朝向的一个切片）用 16×16 的位掩码记录已消费的格子；掩码**每切片重置**（跨切片复用会让后面的平面漏掉格子，并吐出错切片位置的四边形）。
- 先沿 u 轴扩展宽度，再逐行沿 v 轴扩展高度；每次扩展前重查键与掩码。
- 顶点由"平面原点 + width×U + height×V"生成，坐标需加上区块与段的基址（漏加会让非零基址的段跑到错误位置——实测表现为石层浮在地形上方）。

**朝向与绕序**：每个方向的顶点顺序按 `Cross(U, V) == 外法线` 推导，推导不成立的三个方向（+X、+Y、-Z）改为 V 优先。落表：

| 方向 | 顶点顺序 | 依据 |
| --- | --- | --- |
| -X | U 优先 | `Cross(Z, Y) = -X` |
| +X | V 优先 | `Cross(Y, Z) = +X` |
| -Y | U 优先 | `Cross(X, Z) = -Y` |
| +Y | V 优先 | `Cross(Z, X) = +Y` |
| -Z | V 优先 | `Cross(Y, X) = -Z` |
| +Z | U 优先 | `Cross(X, Y) = +Z` |

管线侧对应 `CullMode = Back` + `FrontFace = CCW`（WebGPU 在 framebuffer 空间判定正面，因此这一组取值是"我们推导的逆时针"的镜像补偿；改任何一方都要重新出图验证）。

### 5.2.1 光照与顶点亮度

光照来自区块包自带的 nibble 数组（`ChunkLight`，每段 2048 字节，低半字节为偶数索引），按掩码顺序装入 `LevelChunk.Light`。网格生成时对**每个可见面采样它所朝向的空气格**的光照，取 `max(sky, block)` 归一到 0..15，再乘朝向系数：

| 朝向 | 系数 |
| --- | --- |
| 上 (+Y) | 1.00 |
| 水平 (四个侧面) | 0.75 |
| 下 (-Y) | 0.50 |
| 最低亮度（避免纯黑） | 0.12 |

亮度直接乘进顶点颜色，因此**顶点格式不变**（仍是位置 3 + 颜色 3），代价是同一方块不同朝向的面颜色不同——这与原版"面朝向决定明暗"的观感一致。

**已加载边界处理**：位于加载区域边缘的面会采样到不属于任何区块的格子。此时回退到方块自身格子的光照，而不是按 0 处理（否则边缘墙面会是纯黑）。

与原版的关系：这是**等效优先的近似**，不追求逐像素对齐。未实现：环境光遮蔽（AO）、天空亮度随时间的曲线、平滑光照（smooth lighting）的顶点插值、方块的发光等级（light emission）。

### 5.3 背面剔除

管线开启 `CullMode = Back`。绕序在网格侧保证"从方块外部看为逆时针"（`ChunkMesherWindingTests` 用 Newell 法逐面验证，并检查每个四边形的两个三角形一致）。

**容易搞错的一点**：WebGPU 在 **framebuffer 空间**（Y 轴向下）判定正面，而我们的投影是 Y 向上。若只开背面剔除不改 `FrontFace`，结果是把可见面剔掉、露出内部——表现为"地形炸开成碎片"。因此管线同时设置 `FrontFace = CW`。

## 6. 当前限制

- 无纹理：颜色由方块状态 id 哈希得到，稳定但不代表材质（片一完成后此项被替换）。
- 无光照传播：只消费服务端下发的光照数组，客户端不计算光照传播（挖方块后不会实时变亮/变暗）。
- 无环境光遮蔽与平滑光照：面亮度只按朝向与所在格光照，顶点之间不做插值。
- 窗口只有呈现：无输入系统；帧循环是"呈现 N 帧 / 到点关闭"，尚未接入正式主循环。
- resize 已支持（交换链 + 深度 + 顶点一起重建），但只做了被动跟随，没有最小尺寸、纵横比锁定等策略。
- 每帧重建设备与管线（未做资源复用）。
- 合并只发生在单个段内：跨段的同状态平面不会合并（原版同样如此）。
- 合并键不含面朝向之外的信息（例如环境光遮蔽），后续做 AO 时需要把 AO 采样并入键。

## 6.1 纹理通路设计（T-037）

目标：方块面从"颜色由状态 id 哈希"改为"采样原版纹理"。分两片推进，避免一次把模型解析和 GPU 采样搅在一起。

### 片一：单纹理端到端

| 步骤 | 归属 | 说明 |
| --- | --- | --- |
| 解码 PNG | `Common.Imaging.PngDecoder` | 支持 8 位、非隔行的灰度/真彩/索引/带 alpha；其它形态给可诊断报错 |
| 选取纹理 | 启动器 | 按 `assets/minecraft/textures/block/<名字>.png` 取一小批（方块 id → 纹理由一张显式表决定，暂不解析模型） |
| 打包图集 | **客户端** | 从包栈发现精灵并拼图（ADR-0007）；用户资源包替换纹理后只需重建图集，不需要重跑启动器 |
| 上传与采样 | `Framework.Rendering` | 顶点格式增加 UV（stride 7 → 9），管线增加采样器与纹理绑定，着色器按 UV 采样 |
| 缺失兜底 | 渲染层 | 表里没有的方块用内置紫黑格子（与资产解析层的兜底一致） |

- 图集与 `atlas.json` 都是**派生产物**，不进仓库、可随时重建（与 ADR-0005 的 pack 同一原则）。
- 纹理坐标约定：图集格子内留 0 边距（不做 mipmap 时会渗色，先用"最近邻 + 无 mipmap"规避；后续需要时再加 padding 或 mip 链）。

### 片一实测（真实 26.3 资产）

| 纹理 | 解码结果 | 说明 |
| --- | --- | --- |
| `block/stone` | 16×16，256 不透明 | 普通方块纹理 |
| `block/grass_block_top` | 16×16，256 不透明 | 同上 |
| `block/water_still` | **16×512**，8192 不透明 | 动画纹理：竖向 32 帧条带；且是 **4 位索引** PNG |

两个只有真机才暴露的事实：

1. **位深不止 8**：水面/岩浆这类是 4 位索引 PNG。首版"只支持 8 位"的解码器直接拒绝了 `water_still`，因此支持范围扩到 1/2/4/8/16 位 + 索引调色板 + `tRNS`。
2. **动画纹理是竖向条带**：`16×512` = 32 帧。图集只能取**首帧**（顶部 16×16），帧切换按 `.mcmeta` 的 `animation` 字段另行实现（不在本任务范围）。

### 片二：模型驱动

用 `blockstates/<id>.json` → `models/block/<model>.json`（含 `parent` 链与 `textures.all` / 按面覆盖）决定每个面用哪张纹理，取代片一的显式表。

- 需要的行为规格：`parent` 继承与覆盖顺序、`cube_all`/`cube` 这类基础父模型、`uv` 缺省时的推导、模型里 `elements` 的旋转支持范围。
- 依据：证据指针见 `docs/analysis/assets/resource-resolution.md` 与 jar 内的模型文件；具体继承规则在片二开工前补一节行为规格。

### 不在本任务范围

- 动画纹理（`.mcmeta` 的 `animation`）：本任务只记录元数据存在，不驱动帧切换。
- mipmap、各向异性过滤、纹理数组。
- 透明度排序（alpha 混合的树叶/玻璃留在后续）。

## 7. 后续

1. 深度已就位后引入背面剔除与按区块的合批。
2. 光照数组映射为顶点亮度（`LightData` → 顶点颜色），需要先定义"亮度进颜色"的行为规格。
3. 窗口 / 交换链：另立 ADR，并把 `FrameRequest` 扩展为持续帧循环。
4. 纹理图集属于原版资产，必须由启动器从用户自备 jar 提取（原则 3），渲染层只消费提取结果。

## 8. 测试与验收

| 类型 | 位置 | 覆盖 |
| --- | --- | --- |
| 单元 | `tests/MinecraftCSharp.Client.Tests/ChunkMesherTests.cs` | 面数、跨区块剔除、方块上限、颜色稳定性 |
| 单元 | `tests/MinecraftCSharp.Framework.Tests/CameraMathTests.cs` | 近/远平面映射、目标居中、默认相机取景、参数校验、顶点布局 |
| 单元 | `tests/MinecraftCSharp.Common.Tests/PngEncoderTests.cs` | 与独立实现对齐的 CRC、IHDR 结构、扫描线往返 |
| 单元 | `tests/MinecraftCSharp.Common.Tests/PngDecoderTests.cs` | 灰度/真彩/索引/带 alpha 解码、过滤器还原、损坏报错、与编码器往返 |
| 集成 | `launcher frame local` | 本地合成世界出图（不依赖网络） |
| 集成 | `launcher frame replay <file>` | 用已捕获的真实区块包回放，验证"真实数据能画出来" |
| 集成 | `launcher window <address>` / `window local` | 窗口呈现真实世界（180 帧，BGRA8UnormSrgb）并落盘 PNG |
| 集成 | `MC_WINDOW_SELFRESIZE=640x360 launcher window local` | 第 30 帧时窗口自缩到 640×360，验证交换链/深度/顶点三者一起重建 |
