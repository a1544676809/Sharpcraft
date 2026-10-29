<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

状态：[已评审]
基线：Minecraft 26.3
证据：ADR-0003（WGPU 绑定与首帧路径）；实测输出 `sharpcraft-frame.png`
最后更新：2026-10-29

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
- 光照传播（客户端不计算光照传播，只消费服务端下发的数组）。
- 特殊渲染方块（流体、火把、箱子等，见 T-041）。
- 原版着色器语义的对齐（原则 11，见 `docs/specs/render/`）。

## 2. 侧别归属

- 归属：网格生成与相机数学属于 `Client`（表现层）；WGPU 调用属于引擎层 `Sharpcraft.Framework`。
- 判定依据：AGENTS 原则 5、6——游戏逻辑不得持有 WGPU 句柄，所有图形调用收敛在引擎层。

## 3. 依赖关系

| 方向 | 对象 | 说明 |
| --- | --- | --- |
| 依赖 | `Brew.Wgpu` | 托管绑定与 `wgpu_native`，二者同版本（ADR-0003） |
| 依赖 | `Sharpcraft.Common`（PNG 编码） | `Common/Imaging/PngEncoder` |
| 被依赖 | `Sharpcraft.Launcher`（`frame` 命令） | 组装链路并落盘 |
| 不依赖 | `Client` / `Auth` / `Launcher` | 引擎层不得反向引用（架构门禁 `FrameworkMustNotReferenceGameOrHostAssemblies`） |

## 4. 对外契约

- `ChunkMesher.Mesh(ClientWorld, sprites) -> MeshData`：每顶点 12 个 `float`（位置 3 + 光照颜色 3 + 平铺次数 2 + 精灵矩形 4），`uint[]` 索引；无精灵映射时后 6 个分量为零。
- `CameraMath.BuildViewProjection(eye, target, fov, aspect)` / `DefaultCamera(centre, radius)`。
- `ClipSpaceTransform.Build(FrameRequest, Matrix4x4)`：裁剪空间顶点（位置 4 + 颜色 3 + 平铺 2 + 精灵矩形 4）。
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

### 5.4 每面 UV 约定（2026-10-29 对齐原版）

石头的纹理看不出上下左右，所以这里的约定**靠测量**而不是靠看：网格把每个角点**投影到该面的 uv 轴**上算出
纹理坐标，轴取自原版模型烘焙器的规则（`ref://mc-26.3-analysis/src/net/minecraft/client/renderer/block/model/FaceBakery.java`）。

| 面 | u 轴 | v=0 所在方向 |
| --- | --- | --- |
| SOUTH (+Z) | +X | 顶部（+Y）|
| NORTH (-Z) | -X | 顶部 |
| EAST (+X) | -Z | 顶部 |
| WEST (-X) | +Z | 顶部 |
| UP (+Y) | +X | -Z |
| DOWN (-Y) | +X | +Z |

两条推论，都是踩过的坑：

- **侧面的 v 一律"顶部为 0"**，否则整个方块上下颠倒（本轮修的第一个症状）。
- **相对的两个面 u 必须反向**（+Z 是 +X、-Z 是 -X）。用"哪个角点先出现"之类启发式猜 u 方向，
  会让**一半的面变成镜像**——石头看不出，告示牌一眼就出来（本轮修的第二个症状）。

`MeshTextureOrientationTests` 用六个参数化用例断言这六行，比较的是**世界轴方向**，因此换成任何对称纹理
都不会漏掉回归。

## 6. 当前限制

- 纹理只覆盖立方体模型链：267 个特殊渲染方块（水/岩浆/火把/箱子/活塞等）仍是 missing 精灵，见 T-041。
- 动画纹理只取首帧（`.mcmeta` 的 `animation` 尚未驱动帧切换）。
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
| `registry_data` → `BlockStateLayout` 装配（真实世界的 state id → 方块名） | 未完成，见 T-042（当前用 `VanillaBlockRegistry` 的基线快照） |

### 带纹理的实机画面（2026-10-29 复验）

```
$ MC_FRAME_LOCAL=1 launcher frame local
  sprite coverage: 4/4 blocks textured, 0 missing
sprites: 1024x1024 atlas, 1301 sprites, layout present, 82 ms
local demo: 2045 blocks, 1606 triangles
rendered on adapter: 101.9150
frame written to ...\sharpcraft-frame.png
```

复验链路：从官方 `piston-meta` 取 26.3 版本清单与 client.jar（41,483,720 字节，SHA-1 与清单一致），`launcher assets` 重建 pack（11,533 条目 / 5,854,134 字节 / 681 ms），再出图。像素采样 `top-mid (480,270) = rgba(143,143,143)`，与图集里石头的像素一致。

- 画面上石头是石头纹理、底部泥土是泥土纹理，不再是"状态 id 哈希出的纯色"。
- 用 `MC_DEBUG_SPRITES=1` 打印 `state → 精灵矩形 + 图集中心像素`，实测映射正确：
  `0 air → missing(255,0,255)`、`1 stone → 848,336 rgba(143,143,143)`、`2 dirt → 688,176 rgba(121,85,58)`、`3 cobblestone → 160,144 rgba(181,181,181)`。
- 用 `MC_DEMO_STATE=1` 把整个世界刷成石头后采样渲染结果：`rgba(143,143,143)`，与图集里石头的像素**完全一致**。

### 真实服务器的带纹理画面（2026-10-29 实测）

```
$ launcher frame <address>
Signed in as <profile>
chunks: 9 ok, 0 failed
sprite coverage: 1021/1286 blocks textured, 265 missing
sprites: 1024x1024 atlas, 1301 sprites, layout present, 56 ms
mesh: 275142 blocks, 51402 triangles
frame written to sharpcraft-frame.png
```

- 一次完整链路：真实登录 → 握手 → 配置阶段注册表 → 9 个区块 → 图集 → 网格 → 离屏渲染 → PNG。
- 与本地演示世界相比，真实世界的 **275,142 个方块 / 51,402 个三角形**是数量级的差别，也第一次验证了"真实 state id → 方块名 → 每面纹理"这条链在真实数据上成立。
- 会话令牌缓存生效：再次运行直接 `Signed in as …`，**没有再次要求设备码**（见 `docs/modules/auth.md` §5.1）。

### 带纹理的窗口（2026-10-29 实测）

```
$ launcher window <address>
window: surface ready on <adapter>
window: framebuffer 960x540, logical 960x540
window: swapchain configured (BGRA8UnormSrgb, 960x540)
window: first frame presented
window closed after presenting frames; surface format BGRA8UnormSrgb
sprites: 1024x1024 atlas, 1301 sprites, layout present, 24 ms
mesh: 275141 blocks, 51396 triangles
```

至此 M3 的出口条件"一条命令从本地 jar 走到带纹理的窗口画面"成立：登录 → 握手 → 配置阶段 → 区块 → 图集 →
网格 → 交换链呈现，同一条链既出窗口也出 PNG（截图仍走离屏路径，因为交换链纹理不保证可拷贝）。

窗口路径曾有两个**只有 GPU 才能暴露**的缺陷，都已修：

1. **管线顶点属性缺两项**：加纹理时只改了离屏管线的属性列表，窗口管线仍声明 `position + colour`，
   于是 `wgpuDeviceCreateRenderPipeline` 报
   `Location[2] Float32x2 … is not provided by the previous stage outputs`。
2. **没有绑定图集**：修好属性后报
   `The current set RenderPipeline … expects a BindGroup to be set at index 0`——窗口渲染器从未上传图集、也没建绑定组。

根因是**同一条顶点布局存在两份拷贝**。现在它只有一份：`Framework/Rendering/VertexLayout`（`Stride` + `Attributes()`），
两条管线共用，并由 `VertexLayoutTests` 钉死"偏移首尾相接、location 递增、格式顺序为 4/3/2/4、步长 52 字节"。
这类"改一处漏一处"的缺陷靠代码评审很难发现，靠共享定义 + 测试才守得住。

### 流体：`<名字>_still`

流体的方块状态文件指向的模型只声明 `particle`，没有任何面变量（`models/block/water.json` 只有 `particle: block/water_still`），
因此模型链解析不出面纹理。流体没有独立模型，原版由流体渲染器画，静态画面用的就是 `_still` 那一帧。
解析顺序因此在"与方块同名"之前增加一步：`<名字>_still` 命中时三面同用该精灵。

效果：`water` / `lava` 从紫黑格子变成真实贴图（覆盖率 1019 → 1021），剩余 265 个未解析方块集中在
`chest`、`wall_torch`、`fire`、`redstone_wire`、作物等**非立方体或实体渲染**的方块（T-041）。

### 这轮抓到的两个真 bug

1. **顶点颜色不能再用状态哈希色**。有了纹理之后，albedo 来自图集，顶点颜色只应承载光照；最初仍乘上 `ColourOf(state)`，结果石头被染成棕色。是靠"强制单一方块 + 采样渲染像素对比图集像素"这条诊断链定位的，肉眼看图只会觉得"颜色有点怪"。
2. **图集 UV 需要半纹素内缩**。最近邻采样落在格子边界会取到隔壁精灵的纹素，因此 `U0/V0` 向内缩 0.5 纹素、`U1/V1` 向内缩 0.5 纹素；对应测试断言了内缩后的坐标。

### 诊断开关

| 变量 | 作用 |
| --- | --- |
| `MC_FRAME_LOCAL=1` | 用本地合成世界出图，不联网、不认证 |
| `MC_DEBUG_SPRITES=1` | 打印精灵映射与图集像素，并采样渲染结果的若干像素 |
| `MC_DEMO_STATE=<id>` | 把本地世界所有方块刷成同一状态，用于隔离"某个方块纹理错"的问题 |

### 基线布局与真实数据（2026-10-28）

真实服务器发来的是**数字 state id**，要变成方块名必须有注册表布局。基线布局可以离线重建：仓库里已有 26.3 的
方块注册顺序与状态数快照（T-007 抓自 jar，只有标识符与计数，不含任何资产），因此：

| 项 | 值 |
| --- | --- |
| 方块数 | 1,286 |
| 全局方块状态数 | 35,723 |
| 抽样校验 | `minecraft:air` = 0 且 1 个状态；`minecraft:redstone_wire` = 1,296 个状态（与原版一致） |

这条布局由 `VanillaBlockRegistry`（`Common/Registries/`，嵌入式快照）提供，`frame replay` 与真实服务器路径默认使用它。
**注意**：它只是基线；服务器若增删或重排方块，必须以运行时 `registry_data` 为准（T-039 的动态部分仍未做）。

按面取纹理：`BlockSpriteMap` 现在区分顶/侧/底。规则顺序是
① 与方块同名的纹理 → ② `<名字>_top` / `<名字>_side` / `<名字>_bottom` → ③ missing 精灵。
`grass_block` 因此能正确取到 `grass_block_side`（此前整块是紫黑格子）。

**实测覆盖率**——用真实抓包（`failed-0-47760.bin`，真实服务器区块）跑 `frame replay`：

```
replay: chunk (7,7) 2 block entities, 11 light arrays
sprite coverage: 682/1286 blocks textured, 604 missing
    no texture: minecraft:oak_wood / minecraft:water / minecraft:lava / ...
mesh: 31321 blocks, 7354 triangles
```

链路接通后覆盖率（同一抓包、同一命令）的变化：

| 阶段 | 已纹理方块 | 比例 |
| --- | --- | --- |
| 仅名字约定 | 682 / 1286 | 53% |
| **加入 `blockstates` + `models` 解析** | **1019 / 1286** | **79%** |

剩余 267 个是**特殊渲染**的方块，不属于立方体模型链：`water` / `lava`（流体，用 `_still`/`_flow` 贴图与专属渲染）、
`moving_piston`、`wall_torch`、`fire` / `soul_fire`、`chest`（实体或位移渲染）。它们需要各自的渲染实现，
不是"模型没解析对"。

模型解析的行为规格见 `docs/analysis/assets/block-models.md`：解析顺序是
① blockstate → 首个变体 → 模型 → `parent` 链合并 `textures` → 递归解析 `#变量`；② 与方块同名的纹理；
③ `<名字>_top/_side/_bottom`；④ missing。

### 片二：模型驱动（已完成，T-040）

用 `blockstates/<id>.json` → `models/block/<model>.json`（含 `parent` 链与 `textures.all` / 按面覆盖）决定每个面用哪张纹理，取代片一的显式表。真实抓包回放：已纹理方块 682/1286 → **1019/1286（79%）**。

### 不在本任务范围

- 动画纹理（`.mcmeta` 的 `animation`）：本任务只记录元数据存在，不驱动帧切换。
- mipmap、各向异性过滤、纹理数组。
- 透明度排序（alpha 混合的树叶/玻璃留在后续）。

## 7. 后续

下一阶段（实体模型、天空与雾、半透明排序、方块实体、后处理与着色器包接入）的管线设计见
`docs/specs/render/pipeline.md`（草稿，待评审）。

1. 正式帧循环与输入系统（M4），渲染从"呈现 N 帧 / 到点关闭"改成按时间步进。
2. 每帧重建设备与管线改为资源复用；按区块合批。
3. 特殊渲染方块（T-041）与运行时 `registry_data` 装配（T-042）。
4. 环境光遮蔽、平滑光照与动画纹理帧推进。

## 8. 测试与验收

| 类型 | 位置 | 覆盖 |
| --- | --- | --- |
| 单元 | `tests/Sharpcraft.Client.Tests/ChunkMesherTests.cs` | 面数、跨区块剔除、方块上限、颜色稳定性 |
| 单元 | `tests/Sharpcraft.Framework.Tests/CameraMathTests.cs` | 近/远平面映射、目标居中、默认相机取景、参数校验、顶点布局 |
| 单元 | `tests/Sharpcraft.Common.Tests/PngEncoderTests.cs` | 与独立实现对齐的 CRC、IHDR 结构、扫描线往返 |
| 单元 | `tests/Sharpcraft.Client.Tests/SpriteAtlasTests.cs` | 图集：真实尺寸、不重叠、UV、missing 回落、动画首帧、可复现、包栈覆盖 |
| 单元 | `tests/Sharpcraft.Client.Tests/BlockSpriteMapTests.cs` | 方块 → 精灵：命名约定、缺失回落、布局预计算、无布局行为 |
| 单元 | `tests/Sharpcraft.Client.Tests/TexturedMesherTests.cs` | 带纹理网格：顶点颜色只承载光照、UV 在格子内平铺、精灵矩形进顶点流 |
| 单元 | `tests/Sharpcraft.Common.Tests/PngDecoderTests.cs` | 灰度/真彩/索引/带 alpha 解码、过滤器还原、损坏报错、与编码器往返 |
| 集成 | `launcher frame local` | 本地合成世界出图（不依赖网络） |
| 集成 | `launcher frame replay <file>` | 用已捕获的真实区块包回放，验证"真实数据能画出来" |
| 集成 | `launcher window <address>` / `window local` | 窗口呈现真实世界（180 帧，BGRA8UnormSrgb）并落盘 PNG |
| 集成 | `MC_WINDOW_SELFRESIZE=640x360 launcher window local` | 第 30 帧时窗口自缩到 640×360，验证交换链/深度/顶点三者一起重建 |
