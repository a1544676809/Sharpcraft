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
| 整包图集（26.3） | **1024×1024，1301 个精灵，52 ms** | 1300 张方块 PNG（实测包里正是 1300 PNG + 105 `.mcmeta`）+ 1 个 missing 精灵 |

两个只有真机才暴露的事实：

1. **位深不止 8**：水面/岩浆这类是 4 位索引 PNG。首版"只支持 8 位"的解码器直接拒绝了 `water_still`，因此支持范围扩到 1/2/4/8/16 位 + 索引调色板 + `tRNS`。
2. **动画纹理是竖向条带**：`16×512` = 32 帧。图集只能取**首帧**（顶部 16×16），帧切换按 `.mcmeta` 的 `animation` 字段另行实现（不在本任务范围）。

### 图集语义：与原版对齐到什么程度

| 行为 | 原版 | 本项目 | 差异 |
| --- | --- | --- | --- |
| 精灵发现 | 客户端从资源栈按 `textures/` 目录声明发现 | 从 `AssetStack` 列 `assets/minecraft/textures/block/` | 目前只扫 `block`，`entity`/`item`/`gui` 后续按需加入 |
| 精灵尺寸 | 保留原始尺寸，不强制等宽格子 | 同样保留原始尺寸 | 一致 |
| 拼图算法 | `Stitcher`：按 height/width 排序，装不下则放大图集 | shelf 装箱，同样先按 height/width 排序，装不下则**翻倍**放大 | 结果布局不同，但不重叠、可复现、尺寸为 2 的幂 |
| missing 精灵 | 强制存在，缺失时直接抛错 | 内置紫黑格子，`SpriteAtlas.MissingSprite`，未知位置回落 | 一致（都保证存在） |
| 动画纹理 | 记录全部帧并逐帧推进 | 记录帧数，但**只拼首帧** | 帧推进待 `.mcmeta` 任务 |
| 覆盖优先级 | 包栈顺序，高层覆盖 | 同（有测试：资源包覆盖基础 pack） | 一致 |

写测试时抓到的一个真 bug：`ResourceLookup.TryParseUnderPrefix` 原本假设前缀只有 `assets/<ns>/<dir>/` 三层，遇到 `textures/block/` 这种更深的列举前缀就返回 false，于是**图集一个精灵都发现不了**。现在按"类型根目录 + 剩余路径"解析，深层前缀可用。

### 顶点布局与映射（实现进度）

网格顶点 12 个 float，变换后 13 个 float：

| 段 | 数量 | 含义 |
| --- | --- | --- |
| position | 3 | 世界坐标（入管线前做视图投影） |
| colour | 3 | 光照 × 朝向系数（已有） |
| tile | 2 | 该四边形覆盖多少个精灵重复（贪心合并后 4×2 的面就是 4,2） |
| sprite | 4 | 精灵在图集里的 `u0,v0,u1,v1` |

设计意图：贪心合并会把多个方块的面合成一张大四边形，纹理必须在**该四边形内重复**；把"重复次数"和"精灵矩形"一起传给着色器，就能用 `fract(tile)` 在图集单元内部平铺，不需要在图集里做 mipmap 或 padding（最近邻 + 无 mip 时不会渗色）。

已完成 / 未完成：

| 项 | 状态 |
| --- | --- |
| 顶点格式扩到 12/13 float，管线声明四个属性 | 完成 |
| `BlockSpriteMap`：方块 → 精灵（约定 `block/<名字>`，缺失回落 missing） | 完成（5 项测试） |
| 布局预计算：有 `BlockStateLayout` 时 state id → 精灵是数组下标，无每面开销 | 完成 |
| 着色器采样（绑定组、采样器、`fract` 平铺、半纹素内缩） | **完成**（本地世界已出带纹理的截图） |
| `registry_data` → `BlockStateLayout` 装配（真实世界的 state id → 方块名） | 未完成，见 T-039 |

### 带纹理的实机画面（2026-10-28 实测）

```
$ MC_FRAME_LOCAL=1 launcher frame local
sprites: 1024x1024 atlas, 1301 sprites, layout present, 55 ms
local demo: 2045 blocks, 1606 triangles
rendered on adapter: 101.7088
frame written to ...\minecraftcsharp-frame.png
```

- 画面上石头是石头纹理、底部泥土是泥土纹理，不再是"状态 id 哈希出的纯色"。
- 用 `MC_DEBUG_SPRITES=1` 打印 `state → 精灵矩形 + 图集中心像素`，实测映射正确：
  `0 air → missing(255,0,255)`、`1 stone → 848,336 rgba(143,143,143)`、`2 dirt → 688,176 rgba(121,85,58)`、`3 cobblestone → 160,144 rgba(181,181,181)`。
- 用 `MC_DEMO_STATE=1` 把整个世界刷成石头后采样渲染结果：`rgba(143,143,143)`，与图集里石头的像素**完全一致**。

### 这轮抓到的两个真 bug

1. **顶点颜色不能再用状态哈希色**。有了纹理之后，albedo 来自图集，顶点颜色只应承载光照；最初仍乘上 `ColourOf(state)`，结果石头被染成棕色。是靠"强制单一方块 + 采样渲染像素对比图集像素"这条诊断链定位的，肉眼看图只会觉得"颜色有点怪"。
2. **图集 UV 需要半纹素内缩**。最近邻采样落在格子边界会取到隔壁精灵的纹素，因此 `U0/V0` 向内缩 0.5 纹素、`U1/V1` 向内缩 0.5 纹素；对应测试断言了内缩后的坐标。

### 诊断开关

| 变量 | 作用 |
| --- | --- |
| `MC_FRAME_LOCAL=1` | 用本地合成世界出图，不联网、不认证 |
| `MC_DEBUG_SPRITES=1` | 打印精灵映射与图集像素，并采样渲染结果的若干像素 |
| `MC_DEMO_STATE=<id>` | 把本地世界所有方块刷成同一状态，用于隔离"某个方块纹理错"的问题 |

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
| 单元 | `tests/MinecraftCSharp.Client.Tests/SpriteAtlasTests.cs` | 图集：真实尺寸、不重叠、UV、missing 回落、动画首帧、可复现、包栈覆盖 |
| 单元 | `tests/MinecraftCSharp.Client.Tests/BlockSpriteMapTests.cs` | 方块 → 精灵：命名约定、缺失回落、布局预计算、无布局行为 |
| 单元 | `tests/MinecraftCSharp.Client.Tests/TexturedMesherTests.cs` | 带纹理网格：顶点颜色只承载光照、UV 在格子内平铺、精灵矩形进顶点流 |
| 单元 | `tests/MinecraftCSharp.Common.Tests/PngDecoderTests.cs` | 灰度/真彩/索引/带 alpha 解码、过滤器还原、损坏报错、与编码器往返 |
| 集成 | `launcher frame local` | 本地合成世界出图（不依赖网络） |
| 集成 | `launcher frame replay <file>` | 用已捕获的真实区块包回放，验证"真实数据能画出来" |
| 集成 | `launcher window <address>` / `window local` | 窗口呈现真实世界（180 帧，BGRA8UnormSrgb）并落盘 PNG |
| 集成 | `MC_WINDOW_SELFRESIZE=640x360 launcher window local` | 第 30 帧时窗口自缩到 640×360，验证交换链/深度/顶点三者一起重建 |
