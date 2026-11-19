<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

状态：[已评审]
基线：Minecraft 26.3
证据：ADR-0003（WGPU 绑定与首帧路径）；实测输出 `sharpcraft-frame.png`
最后更新：2026-11-16

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
纹理坐标，轴取自原版模型烘焙器的规则（`ref://mc-26.3-analysis/src/net/minecraft/client/resources/model/cuboid/FaceBakery.java`）。

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

当前世界顶点 **24 字节**（`VertexLayout.Stride`，T-832）：

| 偏移 | 格式 | 字节 | 含义 |
| --- | --- | --- | --- |
| 0 | `Float32x3` | 12 | 世界坐标（入管线前做视图投影） |
| 12 | `Unorm8x4` | 4 | 染色 × 朝向明暗 × AO |
| 16 | `Float16x2` | 4 | 瓦片内 UV（贪心合并后按瓦片平铺） |
| 20 | `Uint32` | 4 | `bits 0..15` 图集矩形下标；`bits 16..23` 天空光；`bits 24..31` 方块光 |

图集矩形不再逐顶点存储，而是移入只读存储缓冲（绑定 5），由顶点着色器按下标查表还原——一个 Quad 的四个顶点取值恒等，原先每个 Quad 白付 48 字节。**片元着色器一行未改**，因此这是一次无损改写。

`uv` 用 half：整数 UV（贪心合并的全部情形）可精确表示，截面浮点（流体斜坡）误差 ≤ 0.008 瓦片单位。

**逐像素验证（离线）**：`--frame` 离屏单帧渲染输出 PNG，14 字节与 24 字节两版在同一世界、同一位置、`MC_FORCE_DAYTIME` 固定光照下各出一帧，几何统计完全相同（2,000,115 方块 / 280,296 三角形），图像 **480,000 像素最大差值 0，无任何像素不同**。离屏路径是确定性的，因此这是可重复的等效性证据，而非目测。实机窗口另行确认纹理正确（草顶绿、侧泥土、石灰色），**609 FPS / 1.9 ms**。

**这不是帧时间优化**：当前瓶颈是片元着色（OIT 占整帧一半以上），顶点带宽并非瓶颈，故本项只计**显存与带宽**收益（44 → 24 字节，减少 45%），不得当作提速证据引用。

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

## 9. M5.3 P1 实体视锥剔除（2026-11-01，T-344）

- 相机 lambda 每帧用 `CameraMath.BuildViewProjection(eye, target, 70, width, height)` 生成视图投影；
- `ViewFrustum.FromViewProjection` 提取六平面；
- `WindowCommand` 的实体几何回调对 `EntityRenderState` 按玩家（0.6×1.8）或普通生物（0.9×0.9）的 AABB 做 `IsBoxVisible`；
- 通过后的实体交给 `EntityPlaceholderMesher.Build`，未通过的不生成几何；
- `MC_DIAG=1` 时每 120 帧输出 `entity cull: candidates=... visible=... culled=...`。

**状态**：视锥接线已完成，全量测试通过。**未完成**：真实实体静态模型、名字牌投影、以及用实际实体场景验证 QA-1.3 的剔除率 ≥60%。
- **名字牌投影（2026-11-01，T-346）**：`NameplateProjection.TryProject` 将实体脚点 + 高度 + `HeadOffset` 投影到归一化屏幕坐标并返回深度；仅提供投影，UI 文本绘制待接。
- **名字牌 UI（2026-11-01，T-347）**：`EntityTracker.TryGetUuid` + `InteractiveSession.EntityDisplayName` 从玩家列表解析名字；`WindowCommand` 的 UI 回调把 `NameplateProjection` 结果乘画布尺寸并 `UiScreenLayout.AddText` 绘制玩家名（缩放/裁剪交给 UI）。

### 6.2 流体顶面贪心合并（T-831，待实施）

#### 现状

`ChunkMesher.MergeSectionFaces` 会把不透明面贪心合并成大四边形，但 `EmitSectionFluids` **没有合并**：它是 16×16×16 的三重循环，每个流体方块独立发射面。海面因此每 section 顶面是 256 个 1×1 四边形，而半透明几何每帧要过 `DEPTH_BOUNDS` / `TRANSMITTANCE` / `ACCUMULATE` 三个 pass，全部重复支付顶点变换与小三角形光栅化开销。

实测依据：水域视角 2534×1334 下整帧 3.72 ms，其中 OIT 2.12–2.21 ms；半透明几何合计约 1.1M 索引 / 191 次绘制。

#### 为什么 UV 平铺天然成立

顶点里**不需要 `tile` 属性**（§6.1 描述的那个方案后来没有落地）。着色器统一使用：

```wgsl
let atlasUv = rectMin + (input.uv - floor(input.uv)) * (rectMax - rectMin);
```

`uv` 在小数部分回绕，因此一个覆盖 W×H 个方块的合并四边形只要把四角 uv 设为 `(0,0)`、`(0,H)`、`(W,H)`、`(W,0)`，在图集单元内的采样结果与 W×H 个独立四边形**逐像素一致**。连续未折返的 `uv` 还让 `dpdx/dpdy` 得到正确的 mipmap 梯度。

#### 可合并判定（全部满足才合并）

1. `!aboveSame`——本格必须真的存在顶面
2. `!flowing`——`flowSprite is null`，即静止水面。流水有旋转 UV，语义不同
3. 四角高度全部等于 `selfHeight`——表面水平。任一角被 `FluidSurface.AverageCorner` 拉低/抬高就不可合并，否则会把斜面拍平
4. `(skyLevel, blockLevel)` 相同——光照是逐顶点烘焙的
5. 群系水色乘子 `(tr, tg, tb)` 相同
6. 同一个 `topSprite`（`sprites?.ForState(state, BlockFace.Top)`）

任一条不满足，该格退回现有的逐格发射路径。

#### 实施方式

在每个 y 层内对 16×16 的顶面候选做二维贪心：

1. 先按上述 6 条算出每格的"合并键"，不合格的标为不可合并
2. 逐格扫描，遇到可合并且未被占用的格作为矩形左上角，先沿 x 扩展，再沿 z 逐行扩展（要求整行键相同且未被占用）
3. 发射一个四边形，把矩形覆盖的格标记为已占用
4. 主循环跳过已占用格

已占用标记用每 y 层一个 `bool[256]`（或用 `[InlineArray]` 结构体避免堆分配）；该缓冲区与现有网格构建缓冲一样可复用。

#### 验收

- 视觉：合并前后对同一机位截图，水面区域**逐像素一致**（水域视角、岸边与瀑布附近各一次）
- 几何：记录半透明索引数与顶点数，海面 section 顶面应从 1024 顶点降到个位数量级
- 性能：`MC_FRAME_CPU` 同机位对比整帧时间；必须用 `-c Release`

#### 实施结果与实测（T-831）

**落地的判定**：方块状态相同、表面水平、非流水、烘焙光照相同、群系水色相同。边缘格因邻接空气导致四角高度不齐，因此不参与合并——这正是需要的保守行为。

**一个必须记下的坑**：最初用**浮点精确比较**判断"四角都等于 `selfHeight`"，结果**没有一个格子通过**。原因是 `CellCorners` 的四角高度来自对邻格的加权平均，水平面并不会逐位复现自身高度。症状极具欺骗性：关闭合并只让半透明三角形从 459,966 变到 459,910，而我最初看到的那 6% 帧时差异其实是噪声。改为 `LevelTolerance = 0.001f`（与发射时的深度内缩同量级，远低于一个像素的解析能力）后合并才真正生效。

| 指标 | 合并关闭 | 合并生效 |
| --- | --- | --- |
| 半透明三角形 | 459,966 | **348,300**（−24%） |
| 整帧（水域 1400×754） | 2.105 ms | **1.884 ms**（−10.5%） |
| 逐像素最大差值 | — | **1 / 255**，无像素差超过 1 |

逐像素比对排除了标题栏、HUD 与云区（云在两次运行间漂移），比对面积 688,560 像素，只有 134 个像素存在 1/255 的差异，属合并四边形边缘的插值舍入。

**几何证据**：`FluidMesherTests.FullWaterSectionEmitsOnlyItsOuterShell` 现在按结构断言——五个面仍逐格发射、顶面边缘环 60 格无法合并、14×14 内部塌缩为 1 个四边形，总数 1341 而非未合并时的 1536。该测试是合并生效的永久回归保护。

统计行新增 `triTranslucent=`，用于直接观察半透明层的三角形数（此前 `tri=` 只覆盖不透明层）。

**岸边机位验证（已完成）**：同一机位逆时针转 90°（yaw −91.6）即沙滩、崖壁与水陆交界。对比合并开关两次运行：71.3 万像素中最大差 **1/255**、平均 0.00012，**水陆交界带逐像素完全相同（最大差 0）**，说明坡面没有被合并拍平。该机位帧率 574–602（合并）vs 585（关闭），差异在噪声内——岸边大量水体较浅且高度不齐，本就不参与合并，收益集中在开阔水面。

**瀑布机位验证（已完成）**：`-57.26 / 70 / 26.83`，朝向 yaw −167.7、pitch −21.2，正对一整面流动水柱。地平线以下 60.3 万像素**最大差 1、无像素超过 1**。注意这次的第一版比对曾得出"2% 像素差 70"的错误结论——原因是漏排了天空区，而云在两次运行间漂移；按 y 分区后差异全部落在 y < 350 的天空，地面与水柱本身一致。**比对必须显式排除云所在区域**，否则会误判。

**未做到的验证**：想为"斜面不被合并"补一个专门测试时失败——`ChunkMesher.Mesh(world)` 使用默认布局，水方块不被识别为流体（拿不到水面顶点），而 `MeshChunk` 返回的 `MeshBuffer` 只暴露 `IndexCount`、不暴露顶点，无法断言高度。因此斜面的正确性目前只由**代码条件本身**保证：合并键里 `heightOf[probe] != originHeight` 直接排除不同高度的格子，同一矩形内不可能出现两个水面高度。岸边与瀑布机位也未单独做逐像素比对（开阔海面已比对通过）。这两点属未闭环项。

### 6.3 Composite 早退（T-831）

`fs_composite` 是全屏 pass。原先是先读完 `accumulate` 与两张系数纹理、跑完整小波重构，最后才判断"本像素没有半透明物体"并 `discard`。现在把判断提到读完 `accumulate` 之后。

**等效性**：`accumulated.a` 是所有片元 `coverage × transmittance` 之和，为 0 即无任何贡献，总透射率为 1，`coverage = 1 − 1 = 0`，原本末尾也会 `discard`。rgb 项必须保留在判断里，因为**加性混合**会产生有光但无覆盖度的像素。理论上唯一的边界是片元覆盖度在 FP16 累积目标中下溢成 0，量级 1e-8，不可见。

**实测**：陆地视角 1400×754，截断到 4 阶段（不含 composite）1.498–1.635 ms，全阶段 1.549–1.589 ms——**composite 本身就在噪声内，≈0 ms**。加上早退后同视角 1.543–1.772 ms，无可测变化。

**保留理由**：等效、零成本、意图更清晰（先判断能否跳过再干活）。但与 §31 的合批一样，**不得当作性能优化引用**——它没有测出收益，保留是因为不亏。
### 6.4 方块实体 per-renderer cull（Issue #45 cull 部分）

方块实体不再共用一条 no-cull 管线。每个 `IBlockEntityRenderer` 通过 `RenderMode` 声明 `NoCull` 或 `Solid`；dispatcher 仍然只拼接一次几何，但先写 solid 部件、再写 no-cull 部件，并记录 `BlockEntityGeometry.SolidIndexCount`。`BlockEntitySubmission` 在同一组顶点/索引缓冲上先画 `[0, SolidIndexCount)`（`fs_solid` + `CullMode.Back`），再画 `[SolidIndexCount, IndexCount)`（`fs_cutout` + `CullMode.None`）。

`solid` 的 shader 语义对应源 `entitySolid`：`fs_solid` 采样同一 atlas/sprite/light/fog，但不做 alpha 测试、输出 alpha 1；`no-cull` 继续使用带 0.5 alpha discard 的 `fs_cutout`。二者不是同一条着色器路径。

- Solid：Bell、DecoratedPot、CopperGolemStatue（源 `entitySolid`）。
- NoCull：Chest / ShulkerBox / Shelf / Campfire / Lectern / EnchantTable / Skull / Brushable。
- 测试：`BlockEntityRenderTypesTests` 验证注册表声明、solid/no-cull 索引范围、混合批次索引守恒与顶点重基；真实 Bell/Pot/CopperGolem 几何的 `SolidIndexCount` 大于零，Chest 为零。
- Z-offset / special pass 不在本切片；Skull 的 `entityCutoutZOffset` 仍由 `SIM-BE-SKULL-ZOFFSET` 跟踪，属于 Issue #45 剩余部分。

### 6.5 CopperGolemStatue 四种 Pose（Issue #66）

`CopperGolemStatueModelDefinition` 提供四个模型层，按 `CopperGolemStatueBlock.POSE` ordinal 选择：standing（复用现有 `CopperGolemModelDefinition.Definition`）、sitting、running、star。三个新模型层逐项取自 `CopperGolemModel.createSittingPoseBodyLayer` / `createRunningPoseBodyLayer` / `createStarPoseBodyLayer` 的 pivot、rotation、cube UV 与 grow；四个定义都开启 `ApplyRootPose`，由 `CopperGolemStatueMesher` 应用与 `CopperGolemStatueModel.setupAnim` 对应的 `root.y=0`、`root.zRot=π`。

- `BlockEntityRenderState.Pose` 由 `FrameCommand` 从方块状态 `pose` 读取；缺失/未知回退 standing。
- Mesher 使用 4 pose × 4 oxidation 的固定数组分组，不按帧分配动态字典；`Handles(47)` 不变。
- 测试：`CopperGolemStatueMesherTests` 覆盖 4 Pose × 2 氧化 × facing 的最终顶点/UV、每 Pose 顶点/索引、无 pose fallback 与既有站立回归。

### 6.6 Banner 静态底色旗面（Issue #89）

`BannerModelDefinition` 声明 `BannerModel.createBodyLayer(standing)` 的 pole/bar 与 `BannerFlagModel.createFlagLayer(standing)` 的 flag；四个模型（ground body/flag、wall body/flag）共用 `entity/banner/banner_base` 64×64 纹理。`BannerRenderer` 复用 `EntityModelMesher.BuildModel`，只做源 `BannerRenderer` 的 ground/wall 模型变换：

- `MODEL_TRANSLATION` 的 block 平移为 `(0.5,0,0.5)`，`MODEL_SCALE` 源为 `(2/3,-2/3,-2/3)`；`EntityModelMesher` 的 root bake 已带 X 镜像与 +24px y-up baseline，因此项目矩阵为 `scale(-s,+s,-s)`，并把 baseline 折入 `translate(0.5,-1.0,0.5)`（`-24*s/16=-1`，s=2/3）。该矩阵行列式为正，winding 不交换；ground 使用 `RotationSegment.convertToDegrees(rotation)` 再取负，wall 使用 `Direction.toYRot` 再取负。
- 旗帜 phase 为 `BannerRenderState.phase` 同公式：`(floorMod(x*7 + y*9 + z*13 + gameTime,100)+partialTick)/100`；`BannerRenderer.PhaseRotation` 将 `(-0.0125+0.01*cos(2π*phase))*π` 写入 flag 的 `EntityPose`，body 不受 phase 影响。
- `DyeColor.getTextureDiffuseColor()` 的 RGB **只作为 flag 的 tint**，pole/bar body 保持白色（源 `submitModel` 两者 diff=-1，底色 tint 属图案层）。
- `BlockEntityRenderState.BannerAttachment/BannerRotation/BannerColor/BannerPhase` 由 `FrameCommand` 从方块状态与公式读取；`Handles(20)`；banner 使用 solid pipeline。
- 未实现图案层，由 `SIM-BE-BANNER-PATTERNS` 跟踪；`ChestAtlas.Textures` 维持 chest-only 22 条，banner 纹理单独加入 block entity sheet。

### 6.7 Conduit 静态外壳（Issue #93）

`ConduitModelDefinition.Shell` 声明 `ConduitRenderer.createShellLayer()` 的单个 `(-3,-3,-3,6,6,6)` 立方体；`entity/conduit/base` 为 32×16。`ConduitRenderer`（id 25）复用 `EntityModelMesher.BuildModel`，只实现 inactive 分支：

- `ConduitModelDefinition.Shell` 使用 `rawSourceFrame: true`：cube/pivot/rotation 为源 y-down 字面值，`EntityModelMesher` 的 raw 分支按源 `ModelPart.Cube` 六面 vertex order/UV 发射，不做 `swapBodyDownUp`、WEST/EAST 交换或 X/Y 旋转共轭。
- 源 `DEFAULT_TRANSFORMATION` 平移 `(0.5,0.5,0.5)`，并沿 Y 旋转 conduit 的 `activeRotation`；raw 模型无需 baseline 补偿。Conduit 的 ModelProbe 样本也确认不需要额外的渲染器 X/Y flip，否则首顶点会落到对侧角。
- 使用 `entitySolid` → `RenderMode.Solid`；本切片 `ConduitRotation` 固定 0（源 activeRotation 动画未接入），active 与 inactive 都绘制同一静态 shell。
- #112：raw exemplar 的 24/24 顶点位置与 UV 已按样本顺序逐点验证，六个面（DOWN/UP/WEST/NORTH/EAST/SOUTH）的 face vertex order/UV 在 raw 路径成立；legacy 路径尚未切换，不宣称全量修复。
- raw 暂不支持 non-unit `PartPose` scale：`EntityModelDefinition.FromTree` 对 raw + scaled 模型抛 `NotSupportedException`；legacy 路径保留该能力。
- `BlockEntityRenderState.ConduitRotation/ConduitActive` 由 `FrameCommand` 三路构造；`Handles(25)`。
- `BoxProbeTests` 增加 `rawSourceFrame: true` 断言：单盒 24 顶点按源 y-down 值、顶点顺序与 UV 直接匹配 26.3 样本，零 modelHeight/flip 换算；legacy 路径测试保持原样。
- active cage/wind/eye/isHunting/tickCount 不做，登记 `SIM-BE-CONDUIT-ACTIVE`。

### 6.8 raw NodeFrame 单位与 T/R 规格（Issue #128）

- **源单位**：`ModelPart.translateAndRotate` 先把 part 的 `x/y/z` 按 `1/16` 转成块位移，再应用 `rotationZYX(z,y,x)`，最后应用 `xScale/yScale/zScale`；cube `Vertex` 坐标仍是模型像素。`root.visit` 的 ModelProbe 输出因此是混合单位 `R * v_pixels + T_blocks`；真实渲染顶点是 `R * (v_pixels/16) + T_blocks`。
- **raw px**：`px = (1/16) * modelScale`。raw 几何顶点与 part/root pose 位移统一使用该 `px`，即生产和 probe 的**实际渲染单位**都是 `R * (v*px) + T*px`；不存在额外的 translation scale 参数。Book probe 若要和 raw 比，必须先只把旋转顶点项除以 16、位移保持块单位，不能把整个混合样本除以 16。
- **T/R 顺序**：源是 `T * R`（顶点先旋转再加位移）；row-vector 表示为 `R_row * T_row`。raw NodeFrame 的 rotation 使用源字面 ZYX（`x/y/z` 相加，不做 legacy X/Y 共轭），平移在 R 之后；base pivot 的 cube 扣除使用 `px`。legacy 保持原共轭/px 语义。
- **pivot 基准**：root child cube 是绝对源像素，先扣除 base pivot（`px`），再 R，再加源位移；child cube 保持 parent-local。`PartWorldMatrix` 的 `Walk` 每层都把 `rawSourceFrame` 传给 `NodeFrame`（raw flag 不只作用于 rootFrame）；`PartMatrix`/`PartFrame` 的 flat 分支同样逐层传播该 flag。
- **判定（Issue #128）**：`modelScale=1` 下生产 raw 的 Position+Rotation 已与源 `T*R` 等价；Book 报告的 `(0.364,...)` 是 `modelScale=16` probe 把混合单位整段比较造成的，不是 T/R 缺陷。`RawNodeFramePoseTests` 使用 Book `right_pages` 源样本（`x=0.997491, yRot=-1.5`, local first vertex `(5,-4,0.99)`），对单 cube 的 **24/24** 顶点逐点比较位置+UV（独立公式 `R*(v/16)+T_blocks`），并用同一 cube 的 Position+Rotation part 验证 `PartWorldMatrix(rawSourceFrame:true)`；不按实现反推。

## 已知刻意的简化

### SIM-BE-BELL-NO-SHAKE：钟只渲染静态外观

- **简化了什么**：`BellMesher` 按原版 `BellModel.createBodyLayer()` 的静态盒定义绘制钟体与钟座；原版 `BellRenderer` 还会用 `BellBlockEntity.ticks` 与 `clickDirection` 驱动 `BellModel.State`，让钟体绕 X/Z 轴摆动，本实现未接入该动画。
- **为什么**：摆动来自方块实体刻与点击方向，当前客户端还没有钟的动画数据通道；静态几何可以直接按模型真值落地，不必等该子系统。
- **对等效性的影响**：静止外观（两个盒的尺寸、位置、纹理与 UV）与原版一致；被敲击后的摆动动画缺失，钟保持静止。
- **状态**：受控偏差，计划由 T-47（#47 批次 1 的钟渲染器）跟进。
- **验收证据**：`tests/Sharpcraft.Client.Tests/Render/BellMesherGoldenTests.cs` 覆盖两个盒的 world bounds、每钟 48 顶点 / 72 索引、双钟批量偏移、32×32 纹理 UV 与注册表 Handles；接入动画后需补 `STATE.tick` / shake 真值向量与单方块像素对比。

<!-- simplification:SIM-BE-BELL-NO-SHAKE task:T-47 -->

### SIM-BE-POT-NO-PATTERNS：饰纹陶罐只画空白侧面

- **简化了什么**：`DecoratedPotMesher` 始终使用 `entity/decorated_pot/decorated_pot_side` 空白侧面，不读取 `BlockEntityRecord.Tag` 中的 sherds / `PotDecorations` 来选择每面的陶片图案。
- **为什么**：陶片图案来自方块实体 NBT 的四个 sherd 物品与 26.3 图案资源映射；本切片先落地静态基底与几何，图案选择需要额外的物品组件与图案注册表解析。
- **对等效性的影响**：任意有图案的陶罐会显示空白侧面；基底、四个侧面平面的位置、法线、朝向与 UV 与原版一致。
- **状态**：受控偏差，计划由 T-47（#47 批次 1）后续陶片图案切片跟进。
- **验收证据**：`tests/Sharpcraft.Client.Tests/Render/DecoratedPotMesherGoldenTests.cs` 覆盖脖子两盒与 top/bottom 平面 world bounds、四个侧面在四个水平朝向下的位置/法线/UV、6 组 `FacingDegrees` 真值、单 pot 112 顶点 / 168 索引（raw base 96 + sides 16）、双 pot 批量偏移、32/16 纹理归一化与注册表 `Handles(41)`；接入图案后需补 sherds 选择真值与单方块像素对比。task-207 新增 `decorated-pot-base-raw-vectors.reference.txt`（96 行）与 `decorated-pot-sides-raw-vectors.reference.txt`（16 行）作为源真值；评估结论为 **raw 迁移 blocker**：side 源使用 `EnumSet.of(Direction.NORTH)` 每 cube 仅 1 面 4 顶点，现有 `EntityModelPart` 无 VisibleSides/面掩码，raw 固定 6 面 24 顶点，无法表达；custom mesher 保持并从源值核对（base 源 96 vs 当前 72，top/bottom 需完整 cube 面）。**task-218 已新增 `[Flags] EntityModelVisibleSides` 面掩码 API（源 `CubeListBuilder.addBox(..., visibleFaces)` 语义）：`EntityModelPart`/`EntityModelCube.VisibleSides=All`（默认不改 legacy），raw `AddPart` 按掩码只发选中面（DOWN/UP/WEST/NORTH/EAST/SOUTH 序），keyed 测试 `VisibleSidesTests` 验证 North 4/6、Down|Up 8/12、镜像+子集。task-222 已用其完成 Chest raw 迁移（Single/DoubleLeft/DoubleRight，双半源掩码剔除 JOIN 面）；task-224 完成 DecoratedPot raw（base 96 + sides 4×NORTH 16，专用 keyed strict；pattern/state/NBT 不变）。**

<!-- simplification:SIM-BE-POT-NO-PATTERNS task:T-47 -->

### task-224 DecoratedPot raw 迁移

- **静态 raw**：DecoratedPotModelDefinition.Base（neck 2 cube + top/bottom，96 顶点）与 Sides（4 个 14×16×0 cube，源 EnumSet.of(Direction.NORTH)，16 顶点）；专用 DecoratedPotRawReferenceSampleTests 按 part/cube/polygon/vertex 1e-5 + UV half 对比 decorated-pot-base/sides-raw-vectors。
- **production**：DecoratedPotMesher 使用 raw 静态几何，base 96 + sides 16 = 112 顶点/168 索引/单 pot；pattern sprite、facing、wobble 状态保留在 renderer，未改 NBT/图案。
- **golden**：原 golden bounds/UV/朝向/批量/零分配断言保留，仅按源 raw 布局与面序更新 offset/count。

<!-- simplification:SIM-BE-POT-NO-PATTERNS task:T-47 --><!-- simplification:SIM-BE-POT-NO-PATTERNS task:T-47 -->

### SIM-BE-POT-NO-WOBBLE：饰纹陶罐不播放 wobble 动画

- **简化了什么**：`DecoratedPotMesher` 只输出静态几何；原版 `DecoratedPotRenderer.submit` 根据 `DecoratedPotBlockEntity` 的 `wobbleProgress` 与 `wobbleStyle` 对模型施加 POSITIVE 的 X/Z 小幅倾斜或 NEGATIVE 的 Y 旋转衰减，本实现未接入。
- **为什么**：wobble 状态来自方块实体逐刻动画（`wobbleProgress` / `wobbleStyle`），当前 `BlockEntityRenderState` 没有对应的动画字段与数据通道。
- **对等效性的影响**：静止外观一致；被玩家或投掷物击中后的陶罐晃动动画缺失。
- **状态**：受控偏差，计划由 T-47（#47 批次 1）后续动画切片跟进。
- **验收证据**：`tests/Sharpcraft.Client.Tests/Render/DecoratedPotMesherGoldenTests.cs` 覆盖静态几何与朝向；接入 wobble 后需补 `wobbleProgress` / `wobbleStyle` 真值向量与单方块像素对比。

<!-- simplification:SIM-BE-POT-NO-WOBBLE task:T-47 -->

### task-226 ShulkerBoxMesher raw 迁移（原 task-211 blocker 已解）

- **源 probe**：SHULKER_BOX bind pose **48 行**（lid/base 各 24）；lid cube (-8,-16,-8,16,12,16) texOffs(0,0)，base cube (-8,-8,-8,16,8,16) texOffs(0,28)。
- **运行时状态**：源 ShulkerBoxModel.setupAnim(progress) 将 lid 设为 y = 24 - progress*8 并绕 Y 旋转 270*progress°；外层还按 Direction 施加 modelTransform（0.9995 scale + 方向旋转 + y/z 翻转）。
- **结论**：`ShulkerBoxModelDefinition` 已替换 custom 静态几何：lid/base bind pose 共 **48 行** raw 顶点；`setupAnim(progress)` 的 lid `y -= 8*progress` / `yRot = 270*progress°` 与 per-direction `modelTransform` 由 renderer 在 raw 几何上外置施加，未烘焙、未改 openness/朝向语义。ShulkerBox blocker 解除，custom 静态几何已移除。
- **证据**：tests/Sharpcraft.Client.Tests/Repro/Data/shulker-box-raw-vectors.reference.txt；`ShulkerBoxRawReferenceSampleTests`（48 行）。

<!-- simplification:SIM-BE-POT-NO-WOBBLE task:T-47 -->

### task-222 Chest raw 迁移（VisibleSides + openness 分离）

- **静态 raw**：ChestModelDefinition.Single/DoubleLeft/DoubleRight，源 ChestModel bottom/lid/lock：single 全 6 面，double left 剔除 WEST、right 剔除 EAST；strict 复用 chest-single-bone 72 行、chest-double-left/right-bone 60 行，1e-5 + UV half。
- **openness**：源 ChestModel.setupAnim(open) 只旋转 lid/lock 的 xRot；raw 静态几何保留，ChestMesher 在渲染时按 OpenAngle(open) 对 lid/lock 顶点绕源 hinge (0.5, 9/16, 1/16) 施加，再按 facing 旋转/平移，不烘焙进度、不丢状态。
- **结果**：single 72 / double half 60 顶点，源双半 JOIN 面剔除；golden/extent/winding 测试按 raw 面序更新。

<!-- simplification:SIM-BE-POT-NO-WOBBLE task:T-47 -->

### SIM-BE-LECTERN-STATE：讲台书页状态固定

- **简化了什么**：`LecternRenderer` 使用参考讲台固定的 `BOOK_STATE = BookModel.State.forAnimation(0, 0.1, 0.9, 1.2)`，不读取 `LecternBlockEntity` 的逐页状态，也不播放书页翻页/打开进度动画。
- **为什么**：首切片先落地 `BookModelDefinition`、讲台静态生成本体和 `has_book`/facing 变换；讲台书页动画需要 `LecternBlockEntity` 的页面数据与逐刻状态通道，后续任务跟进。
- **对等效性的影响**：`HAS_BOOK=false` 时不绘制；`HAS_BOOK=true` 时书本使用固定的参考初始页状态，静态外观和姿态与参考一致，但翻页/打开动画缺失。
- **状态**：受控偏差，计划由 T-47（#47 批次 1）后续讲台动画切片跟进（Issue #63）。
- **验收证据**：`tests/Sharpcraft.Client.Tests/Render/LecternRendererTests.cs` 覆盖静态书页姿态、facing/UV、`HasBook`、`Handles(29)` 与 64×32 纹理；接入动态页后需补页面状态真值与单方块像素对比。

<!-- simplification:SIM-BE-LECTERN-STATE task:T-47 -->

### SIM-BE-ENCHANT-BOOK-STATE：附魔台书本使用固定静止状态

- **简化了什么**：`EnchantTableRenderer` 固定 `time=0`、`flip=0`、`open=0`、`yRot=0`，因此书本不随时间漂浮、不随玩家旋转、也不播放翻页/开合动画；`BOOK_STATE` 为 `BookModel.State.forAnimation(0, 0.1, 0.9, 0)`。
- **为什么**：首切片先复用 `BookModelDefinition` 落地附魔台静态书本与源 pose；漂浮、旋转和翻页需要 `EnchantingTableBlockEntity` 的逐刻状态通道，后续任务跟进。
- **对等效性的影响**：书本的静态几何、纹理和源 `translate(0.5,0.75,0.5)` / Y / Z 80° pose 与原版静止默认状态一致；动态漂浮、旋转和翻页缺失。
- **状态**：受控偏差，计划由 T-47（#47 批次 1）后续附魔台动画切片跟进（Issue #68）。
- **验收证据**：`tests/Sharpcraft.Client.Tests/Render/EnchantTableRendererTests.cs` 覆盖静态顶点/UV、`Handles(13)` 与 64×32 纹理；接入动态状态后需补时间/flip/open 真值与单方块像素对比。

<!-- simplification:SIM-BE-ENCHANT-BOOK-STATE task:T-47 -->

### SIM-BE-SHELF-ITEMS：Shelf 物品槽静态快照

- **简化了什么**：`ShelfRenderer` 只读取方块实体 NBT 的 `Items` 固定槽位快照并绘制；不接实时容器更新，也不播放 Shelf 的移动/交互动画。
- **为什么**：`BlockEntityRenderState` 首切片使用固定槽位字段，物品几何复用已有 item-model 管线；容器实时同步需要后续的方块实体数据更新通道。
- **对等效性的影响**：静态槽位物品的模型、`ON_SHELF` 显示变换、槽位偏移、0.25 缩放、`align_items_to_bottom` 和 facing 按源实现；运行时新增/移除物品和移动动画可能延迟到下一次快照。
- **状态**：受控偏差，计划由 T-47（#47 批次 1）后续 Shelf 容器同步切片跟进（Issue #70）。
- **验收证据**：`tests/Sharpcraft.Client.Tests/Render/ShelfCampfireRendererTests.cs` 覆盖 NBT 槽位、facing、空/满槽和最终顶点/UV；接实时容器后补动态真值。

<!-- simplification:SIM-BE-SHELF-ITEMS task:T-47 -->

### SIM-BE-CAMPFIRE-FLAME：Campfire 火焰/火花/烹饪状态未接入

- **简化了什么**：`CampfireRenderer` 只绘制四个固定槽位的物品；火焰、火花、烹饪进度和火焰动画不绘制/不驱动。
- **为什么**：当前方块实体渲染状态只有物品槽与 facing，没有 Campfire 的火焰/火花/烹饪状态通道；本切片先完成物品管线。
- **对等效性的影响**：物品模型、`FIXED` 显示变换、四个槽位和 facing 按源实现；火焰与火花等动态视觉缺失，静态火光外形由后续切片补齐。
- **状态**：受控偏差，计划由 T-47（#47 批次 1）后续 Campfire 火焰切片跟进（Issue #70）。
- **验收证据**：`tests/Sharpcraft.Client.Tests/Render/ShelfCampfireRendererTests.cs` 覆盖物品槽位、facing 和最终顶点/UV；接入火焰后补单方块像素对比。

<!-- simplification:SIM-BE-CAMPFIRE-FLAME task:T-47 -->

### SIM-BE-BRUSHABLE-FX：Brushable 物品静态快照

- **简化了什么**：`BrushableRenderer` 只绘制 `dusted` 方块状态、`hit_direction` 和 NBT `item` 决定的静态物品；刷子粒子、音效、击中进度动画不接入。
- **为什么**：本切片先完成源 BrushableBlockRenderer 的静态 item pose 与显示上下文；粒子和进度需要额外方块实体状态通道。
- **对等效性的影响**：`FIXED` 显示变换、direction translation、Y 75°、east/west 额外 101° 和 scale 0.5 按源实现；动态刷子特效缺失。
- **状态**：受控偏差，计划由 #50 后续 Brushable 动态切片跟进（Issue #83）。
- **验收证据**：`tests/Sharpcraft.Client.Tests/Render/BrushableRendererTests.cs` 覆盖 dust/direction 最终顶点/UV、空 item/零 dust/无方向不绘制；接入粒子后补单方块像素对比。

<!-- simplification:SIM-BE-BRUSHABLE-FX task:T-47 -->

### SIM-BE-VAULT-SPIN：Vault 展示物品不旋转

- **简化了什么**：`VaultRenderer` 只把 `shared_data.display_item` 画在源位置 `translate(0.5,0.4,0.5)`；`VaultClientData.previousSpin/currentSpin` 的插值旋转固定为 0。
- **为什么**：spin 来自客户端动画状态，当前 `BlockEntityRenderState` 没有该字段；首切片先验证展示物品的 NBT 读取、`GROUND` 显示变换与 atlas。
- **对等效性的影响**：静态位置、物品模型和 UV 一致；激活时展示物品不旋转，运动视觉缺失。
- **状态**：受控偏差，由 #52 后续 Vault 动态切片跟进（Issue #86）。
- **验收证据**：`tests/Sharpcraft.Client.Tests/Render/VaultRendererTests.cs` 覆盖多组最终顶点/UV、空 item 和 atlas 集成；接入 spin 后补时间序列真值。

<!-- simplification:SIM-BE-VAULT-SPIN task:T-52 -->

### SIM-BE-VAULT-COUNT：Vault 多物品 cluster 未实现

- **简化了什么**：`VaultRenderer` 每个 Vault 只画一个展示物品；源 `ItemClusterRenderState.count` / `seed` 与多个 count 的物品堆叠布局未实现。
- **为什么**：当前共享 `BlockEntityItemMesher` 只接受单个固定槽位，多物品布局需要 `ItemClusterRenderState` 的计数、随机种子与位置算法。
- **对等效性的影响**：`count > 1` 时只显示一个物品，堆叠轮廓与源不同；单物品展示一致。
- **状态**：受控偏差，由 #52 后续 Vault 物品 cluster 切片跟进（Issue #86）。
- **验收证据**：`VaultRendererTests` 覆盖单物品最终顶点/UV；补 count 真值后更新。

<!-- simplification:SIM-BE-VAULT-COUNT task:T-52 -->

### SIM-BE-CONDUIT-ACTIVE：Conduit active 分支只复用静态外壳

- **简化了什么**：`ConduitRenderer` 对 active 与 inactive 都绘制同一个 shell；active cage/wind/eye、isHunting、tickCount 与 activeRotation 动画均未接入，`ConduitRotation` 固定 0。
- **为什么**：当前客户端没有 conduit active 状态、tickCount 与 active 模型贴图通道；首切片先落地源静态 shell 几何、transform、atlas 与 render mode。
- **对等效性的影响**：未激活 conduit 的外观按源实现；激活 conduit 仍显示静态外壳，缺少 cage/wind/eye 与旋转/浮动。
- **状态**：受控偏差，由 #51 后续 active 切片跟进（Issue #93）。
- **验收证据**：`tests/Sharpcraft.Client.Tests/Render/ConduitRendererTests.cs` 覆盖 shell bounds/UV、active/inactive 都产出 shell、`Handles(25)` 与 atlas；接入 active 后补状态/动画真值与单方块像素对比。

<!-- simplification:SIM-BE-CONDUIT-ACTIVE task:T-51 -->

### SIM-BE-BANNER-PATTERNS：Banner 只画底色旗面

- **简化了什么**：`BannerRenderer` 只绘制 `BannerModel` 的 pole/bar 与 `BannerFlagModel` 的旗帜底色；不解析 `patterns` NBT，也不叠加 `BANNER_PATTERN_BASE` 或各图案图层。
- **为什么**：本切片先落地 standing/wall 静态模型、transform、tint 与 atlas；图案层需要 `BannerBlockEntity` 的 pattern 数据与对应 sprite/颜色叠加通道。
- **对等效性的影响**：无图案的 banner 底色、杆/横梁与旗帜几何按源实现；带图案的 banner 缺少图案视觉。
- **状态**：受控偏差，由 #49 后续 pattern 切片跟进（Issue #89）。
- **验收证据**：`tests/Sharpcraft.Client.Tests/Render/BannerRendererTests.cs` 覆盖 ground/wall 最终顶点/UV、tint 与 atlas；接入 pattern 后补真值与单方块像素对比。

<!-- simplification:SIM-BE-BANNER-PATTERNS task:T-49 -->

### SIM-BE-SKULL-VARIANTS：Skull 头型无剩余变体

- **简化了什么**：`SkullRenderer` 覆盖 `SKELETON` / `WITHER_SKELETON` / `ZOMBIE` / `CREEPER` / `PLAYER` / `PIGLIN` / `DRAGON` 的静态头模型。
- **为什么**：本切片已补齐所有已知的 skull 头型；后续只保留动画/皮肤缓存等独立简化。
- **对等效性的影响**：七种支持头型的方块头/墙头静态外观按源模型；无剩余因头型缺失导致的不可见分支。
- **状态**：已验证，无剩余变体（Issue #75、#77、#79、#81）。
- **验收证据**：`tests/Sharpcraft.Client.Tests/Render/SkullRendererTests.cs` 覆盖七种头型的 ground/wall、Player head+hat、Piglin head 与 Dragon 7-box 最终顶点/UV。task-203 已将 Dragon skull 切 raw：`DragonHeadModel` 6 个 head box + jaw、`PartPose.scaled(0.75)` 几何、预缩放 UV 维度 pin；`dragon-skull` jar 样本 168 行 strict 1e-5 + UV half（48 mirror/120 checked/0 degenerate）通过。

<!-- simplification:SIM-BE-SKULL-VARIANTS task:T-47 -->

### SIM-BE-SKULL-PLAYER-SKIN：Player 头固定默认皮肤

- **简化了什么**：`SkullType.Player` 始终使用 `entity/player/slim/steve` 默认皮肤和 `createHumanoidHeadLayer` 的 head + hat overlay；不读取 `SkullBlockEntity` 的 profile/皮肤缓存。
- **为什么**：默认皮肤可以先验证 humanoid head/hat overlay 的模型与 atlas 路径；自定义玩家皮肤需要档案解析与皮肤缓存通道，后续任务跟进。
- **对等效性的影响**：玩家头的 head+hat 几何、64×64 UV 和默认 SLIM Steve 纹理一致；自定义/profile 皮肤不会显示。
- **状态**：受控偏差，计划由 #48 后续玩家皮肤切片跟进（Issue #77）。
- **验收证据**：`tests/Sharpcraft.Client.Tests/Render/SkullRendererTests.cs` 覆盖 Player head+hat bounds/UV、纹理/rect 映射；接入 skin cache 后补 profile 真值与单方块像素对比。

<!-- simplification:SIM-BE-SKULL-PLAYER-SKIN task:T-47 -->

### SIM-BE-SKULL-DRAGON-ANIM：Dragon 颌部动画未接

- **简化了什么**：`SkullModel.setupAnim` 的龙颌 `jaw.xRot` 摆动未应用；静态模型使用默认 jaw pose。
- **为什么**：本切片先固定静态 head/jaw 几何；动画需要 SkullBlockEntity 的 animationPos 状态通道。
- **对等效性的影响**：Dragon 头的静态轮廓/位置一致；被动画驱动时 jaw 不摆动。
- **状态**：受控偏差，计划由 #48 后续 Dragon 动画切片跟进（Issue #81）。
- **验收证据**：`tests/Sharpcraft.Client.Tests/Render/SkullRendererTests.cs` 覆盖 Dragon 7 box 静态 bounds/UV；动画接入后补时间序列真值。

<!-- simplification:SIM-BE-SKULL-DRAGON-ANIM task:T-47 -->

### Scaled PartPose 烘焙范围

- `VanillaPartPose` 的 `ScaleX/Y/Z` 与 `Scaled(factor)` 保留；当前只有 Dragon 静态头使用非单位 scale。
- `VanillaMeshBake.Bake` 的绝对烘焙路径只接受 **identity root group（无自有 cuboid、零位移/旋转、单位 scale）**，且 scaled subtree 必须 **无旋转、无 root `MirrorX`、`modelHeight=0`**；零 scale 以及 `Grow`/`GrowXyz`/`OverlayLayers` 也会被拒绝，其他输入抛 `NotSupportedException`，不会返回近似结果。
- `VanillaMeshBakery.Build` 对非单位 scale 明确拒绝（需要 tree/compiled 路径时另行实现完整 `scale * translate` 语义）。
- 默认 scale=1 走原 legacy 路径；`EntityModelBakeStabilityTests` 以 294 个注册定义的 legacy parts/tree 聚合 SHA-256 做字节级回归。




### SIM-BE-SKULL-ZOFFSET：Skull 不应用 entityCutoutZOffset

- **简化了什么**：Skull 的 `entityCutoutZOffset` 深度偏移未应用。
- **为什么**：完整 Z-offset 依赖 Issue #45 的渲染层/深度偏移基础；本切片明确为静态、无 Z-offset 首切片。
- **对等效性的影响**：头像可能的深度顺序/微小深度偏差与参考不同，几何与 UV 不受影响。
- **状态**：受控偏差；Issue #45 的 cull 部分已按 renderer 区分，Z-offset 仍待偏移基础。
- **验收证据**：Issue #75 的静态顶点/UV 测试；Z-offset 接入后补像素/深度对比。

<!-- simplification:SIM-BE-SKULL-ZOFFSET task:T-47 -->

### SIM-BE-SKULL-ANIM：Skull 不播放头部动画

- **简化了什么**：`SkullRenderer` 不读取 `animationProgress`，也不应用 `setupAnim` 的 x/y 头旋转。
- **为什么**：本切片固定静态头模型；动画需要后续 SkullBlockEntity 状态通道。
- **对等效性的影响**：静态朝向与位置一致；受到动画驱动时的头部转动缺失。
- **状态**：受控偏差，计划由 T-47（#48 批次）后续 Skull 动画切片跟进（Issue #75）。
- **验收证据**：静态 ground/wall 顶点/UV 测试；动画接入后补时间序列真值。

<!-- simplification:SIM-BE-SKULL-ANIM task:T-47 -->

### SIM-BE-CONDUIT-FACE-UV：潮涌核心外壳的面 ↔ UV 归属与参考不一致（Issue #112）

- **偏差内容**：inactive 外壳（`ConduitRenderer` 的 `createShellLayer`）六个面里，四个面的 UV 归属与 26.3 参考不同：DOWN↔UP、WEST↔EAST 互换，NORTH/SOUTH 一致。位置 24/24 逐点相同（最大误差 0），全局 UV 值集合也相同，只有面到 atlas cell 的映射错位。
- **为什么**：`EntityModelMesher.AddPart` 的 `swapBodyDownUp` 与 WEST/EAST u-range 交换属于面 UV 约定校准，与参考原版 `ModelPart.render` 的面 UV 归属不同；修复需改渲染核心，超出本次纯机械取样范围，Issue #112 单独跟进。
- **对等效性的影响**：仅影响对称模型的面 UV 归属（面纹理朝向/贴图分区）；几何位置、顶点数、UV 值集合不变。
- **状态**：`阻塞于评审`，直到 Issue #112 修复；修复渲染核心后须把量化断言改回严格比较并关闭该 Issue。
- **验收证据**：`tests/Sharpcraft.Client.Tests/Repro/Data/conduit-vectors.golden.txt`（原版真值，inactive shell @ 零旋转）与 `ConduitGoldenTests` 的 raw 24/24 逐顶点严格断言；legacy 路径仍保留该简化，全面切换后关闭 Issue #112。
- **范围**：金值只取样 inactive 外壳；active cage/wind/eye 属已登记的 `SIM-BE-CONDUIT-ACTIVE` 简化，其变换依赖 tickCount / animPhase / camera orientation，取样器语法无法忠实表达，本次不取。

### SIM-BE-BANNER-TILT-SIGN：旗帜静止倾角符号（Issue #111）——已由 raw 源帧修正

- **偏差内容（历史）**：legacy 烘焙路径下 `BakeChild` 对 `RotationX` 取负、phase 姿态增量又反一次，导致旗帜 phase-0 倾角渲染成 +0.45° 而非源 `BannerFlagModel.setupAnim(0)` 的 -0.45°；旧真值样本记录的最大逐点偏差 0.026213。
- **修正**：Banner 定义切 `RawSourceFrame`（不做 X/Y 旋转共轭），flag 不烘焙 idle 旋转，`BannerRenderer` 按源 `setupAnim(phase)` 字面 xRot 施加姿态。修正后 flag z 范围与源样本逐值相同（`standing [0.541665, 0.596421]`、`wall [0.104165, 0.158921]`）。
- **状态**：`已解决`（`refactor/raw-block-entities` 分支）；Issue #111 已 comment 证据，待项目所有者在 PR 合并后关闭。本文件残留的旧缺陷描述仅为历史记录。
- **验收证据**：`tests/Sharpcraft.Client.Tests/Repro/Data/banner-vectors.golden.txt`（36 组：16 ground + 4 wall + 8 phase×2，4 组 jar 锚点 + 源公式派生）与 `BannerGoldenTests.EveryVertexMatchesTheReference`（逐顶点位置+UV 全元组严格比较）；旧的 `TheFlagTiltIsInvertedUntilIssue111IsFixed` 量化钉死测试已删除。详见 `docs/modules/render-block-entities.md`。


### SIM-ENTITY-RAW-HELD-ITEM：持有物手持挂点帧口径（raw humanoid helper，已解决）

- **结论（task-110 / #120/#134）**：`PartWorldMatrix` 的 raw 分支仍不接手持物通用语义；新增仅 held-item 使用的 `EntityModelMesher.LegacyHandWorldMatrix`，由 `ItemInHandMesher` 在 raw 模型上调用。raw 本体几何、通用 `PartWorldMatrix` 与 legacy 实体路径不变。
- **公式**：源 root child pivot `(x,y,z)` 经 legacy bake 共轭为 `(-x, modelHeight-y, z)`；旋转仍按 legacy `X-poseX, Y-poseY, Z+poseZ`。`modelHeight` 取 `definition.SourceFrameBaselinePx`。raw child arm（如 CopperGolem body→right_arm）保持现有 source-pivot 帧，回退到 `PartWorldMatrix(rawSourceFrame:false)`；其 jar probe `HeldItemHandFrameProbe` origin `(-0.25,0.8125,0)×0.9=(-0.225,0.73125,0)` 已验证。
- **为什么需要**：raw humanoid 的 `right_arm` 是 root child，raw pivot `(-5,2)` 直接用于 legacy 手帧会得到 `(-0.28125,0.1125)`；jar 期望 legacy 共轭 pivot `(5,22)×0.9/16=(0.28125,1.2375)`。helper 只转换 root child pivot，避免破坏已拟合的 `ReferenceFrame.HeldItemAnchor`。
- **验收**：`LegacyHandFrameTests` 对 raw Zombie/Skeleton/Player 的 rest+raised hand frame 与 legacy bake 矩阵 1e-5；`ItemInHandPlacementComparisonTests` 4 个 pose×display 组合、`TheFrameMap...` 保留原 jar 期望；raw child CopperGolem fallback 保持 `(-0.225,0.73125,0)`。
- **resolver 接线**：`EntityModelResolver.Decorate`、`baseLayerModel` 与 body 派生定义保留 `RawSourceFrame`/`BaselinePx`/`SourceFrameBaselinePx`；armor 本体已按族切 raw（Player/humanoid/baby/piglin/Husk/Giant/Horse/Nautilus/Creeper/Wolf），elytra 由 task-229 切 raw（baseline 0 / raw true）。trim/glint/crackiness 材质层与 held 手部适配仍使用各自 legacy 帧；`ResolverRawFlagTests` 同时断言 body raw 与这些 adapter 层。
- **状态**：`已解决`（Issue #120 维持“设计选择已源验证”；#134 的 raw humanoid 迁移使用该 helper）。
<!-- simplification:SIM-ENTITY-RAW-HELD-ITEM task:T-41 -->

### SIM-ENTITY-RAW-MIRROR：raw mirror 语义（task-130 / #121/#156 已解决）
- **结论**：源 `CubeListBuilder.mirror()` 在 `ModelPart.Cube` 内交换 `minX/maxX`（把立方体关于自身中心反射），随后 `Polygon` 构造函数把四个顶点顺序反转；UV 始终绑定在顶点上，所以镜像面是“反射后的四边形 + 顶点序反转”，不是单纯 U 翻转。此前 raw 只做 FlipH/WEST-EAST UV 交换，导致 DOWN 等面出现 `[t0,t1,l1,l0]` vs `[l1,l0,t0,t1]` 的角点配对错误。
- **修法（仅 raw 路径，legacy 不动）**：raw 面发射现在先按源反射后的 `minX/maxX` 取四角（镜像时交换 X 边界），再用源 `ModelPart.Cube` 的六面顺序发射；mirror 部件把四个顶点和对应 UV 一起反序。非 mirror 面保持源顺序，位置/UV 均不变。
- **验收证据**：`RawFaceOrderTests` 对 sheep 四条腿（源 `QuadrupedModel.createLegs`，右腿 mirror=true、左腿 mirror=false）先断言每腿**恰好 24 顶点**（无额外/重复面），再按源 `part|polygon|vertex` 身份严格比较 24/24 顶点的位置与 UV（half 量化后 1e-5），共 4/4；`RawMirrorSourceTests` spider 4 个真实 Mirror 左腿 4/4 仍通过；`LivingRawReferenceSampleTests` 现有非 mirror 样本全绿。
- **状态**：`已解决`（风险 R-027、Issue #121/#156）。
<!-- simplification:SIM-ENTITY-RAW-MIRROR task:T-41 -->

### SIM-ENTITY-RAW-SCALE：raw non-unit PartPose scale（task-152 核心已实现）
- **规格**：源 `PartPose.scaled(factor)` 把 pivot offset 和 bind scale 同乘 factor；`ModelPart.translateAndRotate` 顺序为 translate(pivot) → rotate → scale(bind)。raw compiled tree 的 `EntityModelPartNode` 保存 `ScaleX/Y/Z`，`NodeFrame` 在源旋转/平移语义中应用 bind scale。
- **实现**：`EntityModelDefinition.FromTree` 不再对 raw + non-unit scale 抛异常；`VanillaMeshBakery.Build` 允许 raw，legacy 仍拒绝；raw scaled 使用 compiled tree。源证据 `RawNonUnitScaleTests`：synthetic `PartPose.Offset(1,2,3).Scaled(0.5)` 24/24 顶点；以及非零 xRot/yRot/zRot 的 scaled parent + child，按源 `pivot + R*(cube*factor)`（child 含 parent scale）48/48 顶点 1e-5，并用 `PartWorldMatrix(child)` 验证 parent/child 帧。
- **限制/blocker**：raw scaled 的 legacy flattened `Parts` 暂为空（`FromTree` 仅 compiled tree 承载），依赖 `Parts` 的旧消费方需迁移；**task-160 已将 EnderDragon 切 raw compiled tree（源 1560 顶点 strict 1e-5/UV），task-229 已迁 elytra、task-236 已迁 adult Player 护甲。**legacy scaled 路径保持原受限实现。
- **状态**：核心 raw scale 已实现并有源证据；生产族已按 task-160/229/236/241/243/245/247/249/252 迁移；R-028 跟踪剩余非模型 scale 特例。
<!-- simplification:SIM-ENTITY-RAW-SCALE task:T-41 -->


### task-229 Elytra raw 差异修复（#122）

- **根因**：旧 elytra 设备层仍走 legacy BuildElytraWings（baseline 24、face transforms、flattened 旧 pivot/UV），源 ElytraModel.createLayer 的 raw pivot(±5,0,0)、xRot ±π/12、zRot ∓π/12、10×20×2 grow1 及 UV 未按 raw NodeFrame 叠层；adult/baby 差异来自 baseline/layer yOffset 与 cube UV。
- **修复**：EntityModelLayerHelpers.BuildElytraSourceLayer 用 raw DSL 构造源层，WithElytraSelectors 保留节点 scale/parent，WingsLayer translate(0,0,2px) 由 raw pivots 外置；resolver elytra layer baselinePx:0、rawSourceFrame:true、sourceFrameBaselinePx:0。
- **验收**：ElytraRawSampleTests adult/baby 各 48 行 keyed 1e-5 + UV half；ElytraWingGeometryTests 保留原精确 golden：逐翼绝对 world min/max、逐面四角 UV、baby y 1.492..12.492px、crouch adult 3px / baby 1.5px、standing 左右翼前后关系，无排序/无上界替代。production layer 对齐源 raw：adult y −24px、baby −30.004px（−24 − 12.008×0.5）、z +2px WingsLayer，crouch 位移按 1/(1+node scale) 归一回源 3/1.5px；full 3,614 + 三门禁通过。

<!-- simplification:SIM-ENTITY-RAW-SCALE task:T-41 -->

### task-236 Adult Player armor raw（本体，不含 trim/glint）

- **源样本**：RawRenderUnitProbe/PlayerArmorRawProbe 采 PLAYER_ARMOR 四 slot：helmet 48（head+hat）、chestplate 72（body+双臂）、leggings 72（body+双腿）、boots 48（双腿）。head/chest/feet 用 outer grow 1.0px，leggings 用 inner grow 0.5px（legs 再 extend -0.1）。
- **raw 定义**：PlayerArmorModelDefinition 四 slot 使用源 pivot/cube/UV，baselinePx:0、rawSourceFrame:true、sourceFrameBaselinePx:0、无 FaceTransforms；生产 resolver 对 adult Player 走 raw layer；后续 task-238/241/243/245/247/249/252 已扩到 Zombie/Skeleton/baby/piglin/Husk/Giant/Creeper/Wolf/Horse/Nautilus。
- **边界**：复用本体 raw 实体变换，不套 held-hand matrix；trim/glint/crackiness 材质层不混入本任务，baby/piglin 保持后续。
- **验收**：PlayerArmorRawReferenceSampleTests 四 slot keyed 1e-5 + UV half；production raw flag/vertex count、Husk raw 回归、full 3,619+、三门禁。

### task-238 Humanoid adult armor raw 扩展（Zombie/Skeleton）

- **源证据**：`LayerDefinitions` 中 `ZOMBIE_ARMOR`、`SKELETON_ARMOR` 与 `PLAYER_ARMOR` 都来自同一个 `HumanoidModel.createArmorMeshSet(INNER, OUTER)`（player 仅额外附加空 overlay 组），逐 cube 的 pivot/cube/UV 相同；Zombie/Skeleton 没有独立 armor mesh。
- **复用**：`ZOMBIE_ARMOR`/`SKELETON_ARMOR` 四 slot 新增 keyed 样本（48/72/72/48），与 player armor 12 个样本一起严格匹配同一 `PlayerArmorModelDefinition`，位置 1e-5 + UV half；production resolver 对 adult Player/Zombie/Skeleton 复用 raw 定义，不复制 mesh。
- **边界**：baby/piglin/Husk/Giant 等已由后续 task 按各自源定义切 raw；trim/glint/crackiness 材质层不变；legacy armor 零回归由全量测试覆盖。

### task-241 Baby humanoid armor raw（baby piglin 后续）

- **probe 扩展**：`RawRenderUnitProbe` 遇到 `<SET>_ARMOR` 字段时反射 `ArmorModelSet`，逐个 `.head()/.chest()/.legs()/.feet()` 取 slot ModelLayerLocation 后 bake，输出 layer 名 `<SET>_HELMET/CHESTPLATE/LEGGINGS/BOOTS`；baby zombie/zombie villager 各 slot 样本 24/72/72/48。
- **源定义**：`BabyHumanoidArmorModelDefinition` 使用 baby outer deformation `(-0.1,0.5,0.3)` / inner `(-0.1,0.3,0.3)`，legs 的 waist、legs 与 cube extend(-0.1)；LEGS 含 waist，FEET 为 `left_leg/right_foot` + `right_leg/left_foot`；texture 64×64、raw baseline 0、无 FaceTransforms。
- **验收**：8 个 keyed 样本（baby zombie + baby zombie villager）位置 1e-5 + UV half（waist half UV 逐值量化）；production resolver 对 baby humanoid 四 slot 走 raw layer；baby piglin 由 task-243 另有 raw 定义；adult/skeleton/piglin 与 trim/glint/crackiness 零回归；full 3,627+、三门禁。


### task-245 Husk / scaled humanoid armor raw

- **源公式**：HUSK_ARMOR 是共享 humanoid armor 经 MeshTransformer.scaling(1.0625)：root PartPose 缩放 1.0625 并平移 24.016*(1-1.0625) px；立方体/UV 不变。HuskArmorModelDefinition 将源公式静态烘焙为 pivot/cube 坐标缩放 + 根 yOffset（UV 维度保留原值），不依赖 non-unit node scale 路径。
- **验收**：Husk 四 slot keyed 样本 48/72/72/48，位置 1e-5 + UV half；production resolver Husk raw layer（scale 1，baseline 0）；Drowned/Bogged 等已由 task-247 切 raw，Parched 已由 task-257 接入；adult/baby/piglin/材质层零回归；full 3,657+、三门禁。
### task-265 held 所需 render state 状态通道

- **审计**：现有 `SwingHand`/`SwingType`/`TicksUsingItem`/`UseItemHand` 已从 swing packet 和 `DATA_LIVING_ENTITY_FLAGS`（index 8 bit0/1）解析；本次只补缺失通道，不改 hand frame/adapter。
- **新增字段**：`MainArm`（Avatar `DATA_PLAYER_MAIN_HAND` accessor 15，HUMANOID_ARM serializer **42**，LEFT=0/RIGHT=1；非 Avatar 由 `IsLeftHanded` 派生）、`LeftTicksUsingItem`/`RightTicksUsingItem`（float；源 `HumanoidRenderState.ticksUsingItem(arm)` 派生，保留 partial tick）、`SwingDurationTicks`/`IsSwinging`（SwingDescription payload）、`HeldOnHeadItemId`（源 `AvatarRenderer` 仅在 `useItem.is(Items.SPYGLASS)` 时填 heldOnHead，其它 item -1）。
- **验收**：默认值、Player main-arm metadata roundtrip、错误 index/serializer 不误写、using-item 分臂 ticks + heldOnHead、swing duration/type 测试；full 3,722+、三门禁。

### task-268 humanoid held source-frame adapter

- **实现**：新增 `HumanoidHeldHandFrame.Source`，按 `ItemInHandLayer.submitArmWithItem` 源链 `translateToHand(root→arm) → Rx(-90)·Ry(180) → T_off`（成人 `(±1,2,-10)/16`，baby `(0,1,-4.5)/16`）合成；left hand `tx/ry/rz` 取负由源 arm 数据自然给出。`SourceWorld` 再乘 model scale（adult 0.9 / baby 0.5）映射到 production raw 模型帧，再由 entity transform 处理 flip/baseline。
- **接入**：`ItemInHandMesher` 对 `RawSourceFrame && HumanoidHeldHandFrame.IsHumanoid(typeId)` 使用 source frame 且跳过 legacy `AnchorCoreFor`；CopperGolem/non-humanoid 仍走 `LegacyHandWorldMatrix + anchor`。STAB/use/item display 顺序保持 `D · use · STAB · hand · entity`；trident/shield/banner special 图层不变。
- **真值**：`humanoid-held-hand-source-vectors.reference.txt` 12 状态（right/left × rest/attack/use × adult/baby）；`HumanoidHeldHandSourceFrameTests` 断言 **12 checked / 0 skipped** 全部矩阵 1e-5 通过（attack 期望为 `STAB * Source`）；`HumanoidHeldHandProductionTests` 用真实 `ItemInHandMesher.Build` 12/12 输出级验证：expected 直接来自样本 24 顶点 × model scale × entity transform（独立公式，不经 helper）；CopperGolem 仍走 legacy `anchor × LegacyHandWorldMatrix` 输出级回归。
- **结论**：attack 不需要改生产矩阵；干净 `d67dc28b` 的 `world = STAB * (Source * scale) * entity` 与 sample `STAB * Source` 的 scale 后结果一致，trace 逐顶点差 <4.2e-7。

### task-270 glint TextureTransform

- **源**：`TextureTransform.setupGlintTexturing(scale)`：`millis=(long)(Util.getMillis()*glintSpeed*8)`，`o0=millis%110000/110000`，`o1=millis%30000/30000`，`M=T(-o0,o1,0)·Rz(π/18)·S(scale)`；scale 分类 item 8.0 / entity 0.5 / armor 0.16，glintSpeed 默认 0.5。
- **实现**：`GlintClock` 注入 clock（默认 wall-clock，测试可固定 source millis，不读环境变量、无 per-vertex 分配）；`GlintTextureTransform` 按源矩阵与源 `%` 符号/`fract` wrap 计算 UV；`EntityRenderLayerDefinition.GlintScale` 标记 glint 层，`EntityModelMesher` 每帧在 build 后改写 half-UV，几何不变、不静态烘焙；armor glint 层使用 0.16，item/entity scale 作为独立 helper 分类，不串用。
- **shader**：glint 片元只把已变换的 tile UV 映射到 sprite rect，不再重复做时钟矩阵。
- **验收**：`glint-texture-transform.reference.txt` 固定 millis {55000, 110000, 137500, -55000} × item/entity/armor 的 UV 样本；`GlintTextureTransformTests` production zombie armor glint `BuildLayers` 顶点 UV strict，helper 覆盖三分类与负 millis。
- **限制/blocker**：task-270 当时只有 **armor glint production 层**（`glint_HumanoidArmor_*`）；item glint 已由 task-271 接入 held item production。entity glint 仍只有 scale 0.5 helper + 源样本，**没有 entity glint production 层可接线**，不能声称 glint 全部完成。

### task-274 thrown trident 实体与 glint

- **registry**：`tools/Sharpcraft.Equivalence.Audit/truth/EntityTypeRegistryProbe.java` 真实 `BuiltInRegistries.ENTITY_TYPE.getId(EntityTypes.TRIDENT)` = **139**；样本 `entity-type-registry.reference.txt`，测试从该 probe 输出读取 id 后与常量比对，不只消费常量。
- **模型**：`ThrownTridentModelDefinition` 复用源 `TridentModel` raw 32×32（`SpecialItemModelDefinition.Trident`，120 顶点/24 cube/96 面）；base cutout + `trident_glint` 层。
- **变换**：`EntityModelTransform.ThrownTrident` 按源 `ThrownTridentRenderer`：`rotateY(yRot-90)`、`rotateZ(xRot+90)`，不做 living-entity raw flip；实体位置 `(X,Y,Z)`，模型像素按 production `ModelScale`。
- **glint**：`trident_glint` 与 base 共用同一几何，纹理 `misc/enchanted_glint_item`，`VisibleWhen=state.IsFoil`，`GlintScale=EntityScale(0.5)`，复用 `GlintTextureTransform`/`GlintClock`；固定 millis=55000 production 测试。
- **foil 状态**：ThrownTrident accessor 12（Entity 0-7 + AbstractArrow 8-10 + Loyalty 11），Boolean serializer 8 → `EntityRenderState.IsFoil`；非 foil 无 glint。
- **验收**：`ThrownTridentTests` 验证 type id 139、metadata、base 位置/UV half 对应 `special-trident-raw-vectors`、glint UV 经 0.5 matrix 且位置与 base 一致、非 foil 无 glint。
- **blocker**：仅渲染状态/模型/glint；thrown trident 的飞行/回旋/命中模拟不在本任务（无实体物理路径）。

### task-273 state matrix 覆盖基线

- **工具**：`tools/Sharpcraft.Equivalence.Audit --suite statematrix`，输出 type/state/layer/sample/status TSV 与三态/每层 counts。
- **行**（Issue #219 二次修正）：`EntityModelResolver.Definitions` / `VanillaBlockEntityTypes` 机器枚举；`CapabilitiesFor(type)` 从 resolver/metadata 推导状态（baby=`TryResolve(type,baby:true)`、pose=`Parts>0` 实体几何、held/glint/energy/powered=`held`/`GlintScale`/`UvOffset` 层标记）；`AllowedCombinations(type)` 从每层标记推导允许的 `(state,layer)` 对（无手写 `StateLayerAffinity` 表）；layer 由真实 `RenderLayers`（`Model != null`）决定。`SourceRelevant` 为 allowed 集合成员判断（非恒 true），不允许组合在 builder 判 false 后剔除；`IsSourceRelevant` 公开供测试。非 humanoid×装备层、非 energy 宿主×powered/energy、无 baby×baby 伪行已删除；block 只用 `base`。
- **四态**：`SOURCE_SAMPLE` 引用真实 Repro/Data 文件与 key；`SOURCE_CONSTRAINT` 引用 energy/glint 公式样本；`NO_SOURCE`（A 类真实缺口，源有行为无样本）明确未覆盖且不标 strict；`UNKNOWN`（C 类，方块实体有 renderer 但源行为无法确认）不默认当 NO_SOURCE。
- **A/B/C 分类**：B 伪行从矩阵移除；A 真实缺口保留并按优先级输出（当前以 `pose/base` 动画逐顶点样本为主）；C 标 UNKNOWN 待逐 renderer 判定。
- **校验**：`StateMatrixBaseline.Build` 自检 strict 行文件/key 存在；`StateMatrixBaselineTests` 以元数据驱动复验——每个真实 render layer 至少覆盖一次、每个可表达状态进入矩阵、无 allowed 集合外行、`SourceRelevant` 来自成员判断且 disallowed 组合为 false、`UNKNOWN` 不是 `NO_SOURCE`、strict 行文件/key 真实；禁止 golden hash 自证。
- **语义**：覆盖基线，不是全状态等效证明；`NO_SOURCE`/`UNKNOWN` 不计入覆盖通过。修正后约 420 行（原 1,880），`SOURCE_SAMPLE≈136`/`CONSTRAINT≈40`/`NO_SOURCE≈199`/`UNKNOWN=45`。

### task-271 held item glint 生产层
### task-272 special held foil glint

- **源**：held trident/shield 由 `TridentSpecialRenderer` / `ShieldSpecialRenderer` 提交，glint 用 `RenderTypes.entitySolidGlint`，即 **entity glint scale 0.5**，不是 normal item 的 8.0；glint sampler 为 `misc/enchanted_glint_item`。
- **实现**：`AddSpecialHandLayers` 为 trident/shield 的 left/right special 层各追加 companion `held_special_*_glint`，复用同一 `EntityModelDefinition` 几何，`VisibleWhen` 取对应手 `MainHandGlint/OffHandGlint`，`GlintScale=EntityScale`，纹理 selector 为 glint 纹理；`ResolveHeldItemSpecialGlintTexture` 预置 atlas sprite；不复制几何/不烘焙。
- **验收**：`ItemGlintTests.SpecialFoilTridentAndShieldGetEntityScaleGlint` 固定 millis=55000，production `EntityModelMesher.BuildLayers` 的 trident right / shield left glint 层 UV = base UV 经 scale 0.5 矩阵，位置与 base 完全一致；非 foil 无 glint；item scale 8.0 路径不混用。
- **未改**：normal item glint / armor / entity glint 路径；EnergySwirl。
- **实体缺失 blocker**：thrown trident 实体当前不存在，Sharpcraft 无该生产路径；这是实体缺失，不是 glint 缺口。
- **源核对结论**：只有 `TridentSpecialRenderer` / `ShieldSpecialRenderer` 调用 glint；其余 11 个 `SpecialModelRenderer` 虽接收 `hasFoil` 但 submit 零 glint 调用，因此 banner/skull/chest/shulker 等没有 glint 是**源正确行为**，不是缺口，原“special glint 未接入”blocker 关闭。


- **foil 依据**：`ItemComponentInfo.GlintOverride ?? ItemComponentInfo.HasEnchantments`（`ItemStack.hasFoil()` 同序；override=false 压过 enchantment）；`EntityTracker.Interpolate` 填 `EntityRenderState.MainHandGlint/OffHandGlint`，不按 item id 猜。
- **glint pass**：`ItemInHandMesher` 对 foil hand 复用**同一批 `ItemModelQuad`** 追加 `_glint` geometry，纹理 `misc/enchanted_glint_item`，scale `8.0`，`GlintTextureTransform` + `GlintClock` 变换 half-UV；不复制模型/不烘焙，非 foil 不生成 glint layer。
- **atlas**：`ResolveTextures` 为 foil hand 追加 glint 纹理，glint layer 使用 item atlas、`EntityGeometryLayerKind.Glint`。
- **验收**：`ItemGlintTests` 固定 millis=55000 的 production `ItemInHandMesher.Build` glint UV = base UV 经 item scale 源矩阵且位置与 base 相同；非 foil 无 glint layer；tracker foil 由 enchantment/override 驱动。
- **未改**：entity/armor glint 路径、EnergySwirl、其它材质。
- **special foil 后续**：trident/shield 的 held special foil glint 已由 task-272 接入（`held_special_*_glint`，entity scale 0.5）；其余 11 个 SpecialModelRenderer 源不提交 glint，故 banner/skull/chest/shulker 无 glint 为正确行为。

### task-261 trim/glint/crackiness 跟随 raw armor 几何

- **审计**：`rawArmor` 已为 raw 时，trim/glint 仍用 `equipmentTree`（legacy 24px compiled）且 `baselinePx:24/rawSourceFrame:false`；wolf crackiness 仍用 `EquipmentModelTrees` 的 legacy wolf armor tree。结果材质层与 armor 层几何/基线不一致。
- **修复**：material layers 的 compiled 改为 `rawArmor.Compiled`（glint 仅套 `WithEmissiveTexture`），scale 用 `rawArmor.Scale`，`baselinePx:0/rawSourceFrame:true/sourceFrameBaselinePx:0`；wolf crackiness 用 `WolfArmorModelDefinition.Definition.Compiled`。材质/纹理/染色/可见性选择器全部保留；非 raw armor 仍走原 `equipmentTree` + 24px 分支。
- **验收**：Player helmet/leggings 的 trim/glint 与 armor 层逐点位置/数量 strict 1e-5；Wolf crackiness 与 `equipment_WolfBody_6` 逐点一致；legacy Stray 仍 baseline24/raw false；full 3,718+、三门禁。

### task-259 ArmorStand 实体 + 姿态 + armor

- **type id**：`BuiltInRegistries.ENTITY_TYPE` 的 `minecraft:armor_stand` 注册 id = **5**（总 161 项；Java registry probe）。
- **body/small**：`ArmorStandModel.createBodyLayer` raw，64×64，**264** 顶点；small = 同 mesh 经 `HumanoidModel.BABY_TRANSFORMER`（head 特例，其余 .5/y24）。body 模型 hat 按源隐藏；arms/base plate 分别按 flags 可见。
- **同步数据**：ArmorStand 继承 LivingEntity，accessor 15=`DATA_CLIENT_FLAGS`（Byte），16-21=`HEAD/BODY/LEFT_ARM/RIGHT_ARM/LEFT_LEG/RIGHT_LEG`（ROTATIONS serializer id **9**，三个 float）。`EntityDataPackets` 解码三 float；`EntityVariantSpecs[5].ArmorStandPoseAccessorIndex=16`、Flags=15；tracker→`EntityRenderState` 六组 Vector3。
- **姿态应用**：`ArmorStandAnimationProvider` 将六组角度按 source setupAnim 写入对应 part；body pose 同步驱动两根 body stick 和 shoulder stick，base plate `-yRot`。
- **small/sets**：`IsSmall` 由 flags bit1 驱动，选 `armor_stand_small` variant；armor 复用 task-252 的 `ArmorStandArmorModelDefinition` adult/small 四 slot，production raw。
- **验收**：body/small 264 strict 1e-5 + UV half；armor 四 slot strict；flags/pose tracker roundtrip 与 provider part 旋转断言；full 3,713+、三门禁。

### task-257 Parched entity + armor 接入

- **type id**：`BuiltInRegistries.ENTITY_TYPE` 的 `minecraft:parched` 注册 id = **98**（总 161 项；Java registry probe，非猜测）。`EntityVariantSpecs[98]` 显式默认项，`EntityAnimationProvider` 复用 `SkeletonAnimationProvider`。
- **body**：`SkeletonModel.createSingleModelDualBodyLayer` raw，64×64，312 顶点；包含 body 三层（base/waist/0.025 outer）、head 双层（0.2）和四肢双层 cube。
- **outer**：`PARCHED_OUTER_LAYER` = `HumanoidModel.createMesh(CubeDeformation(0.25),0)` raw，64×32，168 顶点；源 `ParchedRenderer` 只挂 body+armor（没有 SkeletonClothingLayer），因此该 outer 定义/样本就绪但 production 不挂层。
- **armor**：`PARCHED_ARMOR` 与 `humanoidArmor` 同源，复用 `PlayerArmorModelDefinition` 四 slot（48/72/72/48），production raw。
- **验收**：body/outer/armor 全部 jar 样本 strict 1e-5 + UV half；resolver [98] 可渲染、animation provider 存在；full 3,706+、三门禁。

### task-254 Creeper powered 状态 + armor production

- **状态通道**：`Creeper.DATA_IS_POWERED` 为 Boolean synced accessor；继承链 `Entity 0-7 + LivingEntity 8-14 + Mob flags 15 + Creeper swell 16 / powered 17 / ignited 18`，Boolean serializer=8。`EntityVariantSpecs[32].PoweredAccessorIndex=17` → `EntityTracker.ApplyEntityData` 校验 serializer → `TrackedEntity.IsPowered` → `EntityRenderState.IsPowered`；非 Creeper 不受影响。
- **armor**：production resolver 给 Creeper 挂 `creeper_power` 层，model 为 task-249 的 raw `CreeperArmorModelDefinition`，`VisibleWhen: state.IsPowered`；非 powered 不绘制，base creeper 几何/材质不变。Layer kind 采用现有 additive `Emissive` 路径；EnergySwirl 的每帧 UV 偏移由 task-269 接入。
- **验收**：powered true/false tracker→render-state regression，layer 存在性 + VisibleWhen，144 顶点 raw 样本 strict 保持；full 3,702+、三门禁。

### task-269 CreeperPowerLayer EnergySwirl UV 滚动

- **源**：`EnergySwirlLayer` 以 `ageInTicks` 计算纹理偏移：`u = xOffset(t) % 1`，`v = t * 0.01F % 1`；`CreeperPowerLayer.xOffset(t) = t * 0.01F`，texture `entity/creeper/creeper_armor`，mesh `CREEPER_ARMOR` grow2，`visible = isPowered`。
- **实现**：`EnergySwirlUv.Creeper(t)` 复刻源公式（C# `%` 保留被除数符号，负 tick 与 Java 余数一致）；`EntityRenderLayerDefinition.UvOffset` 按帧提供 layer UV 偏移，`EntityModelMesher` 在 build 后用 half-UV 重新打包偏移，几何仍 static、不烘焙缓存；分组键包含 offset，不同 tick 不合并。Creeper `creeper_power` 层只挂此 offset；glint 未改。
- **验收**：`creeper-energy-swirl-uv.reference.txt` 记录 t={0,50,100,137} 的 u/v，production `ItemInHandMesher` 同路径的 `BuildLayers` 输出顶点 UV strict 1e-5（t=50 u=v=0.5），位置与 t=0 相同；负 tick `%` 单测。

### task-252 Horse / Nautilus / ArmorStand armor raw

- **Horse**：`HORSE_ARMOR` = equine body grow0.1 + `MeshTransformer.scaling(1.1)`，`UNDEAD_HORSE_ARMOR` 同 mesh 无 1.1；`HorseArmorModelDefinition` 静态烘焙 living scale/yOffset（body 固定 0.05、ears -0.001），各 288 顶点 strict 1e-5 + UV half。production 的 Horse/ZombieHorse/SkeletonHorse `HorseBody` 层走 raw（undead 用无 1.1 定义）。
- **ArmorStand**：`ARMOR_STAND_ARMOR` 为 head pivot y=1、legs y=11、独立 hat 的 humanoid 四 slot；small 用 `HumanoidModel.BABY_TRANSFORMER`（head 特例：translate y=16 + scale0.75；其余 translate y=24 + scale0.5）静态烘焙。adult/small 各 48/72/72/48 keyed 样本 strict。production 已由 task-259 接入实体、body/small 模型、姿态与四 slot armor。
- **Nautilus**：`NAUTILUS_ARMOR` 为 base body/mouth + 3-cube shell（grow0.01，第三 cube depth=0 按 R-029），192 顶点 strict；production 的 Nautilus/ZombieNautilus `NautilusBody` 层走 raw。
- **验收**：full 3,690+、三门禁；其他 armor/材质层零回归。

### task-249 Creeper / Wolf armor raw

- **Creeper**：`CREEPER_ARMOR` = `CreeperModel.createBodyLayer(new CubeDeformation(2.0F))`，是 charged-creeper 全身 overlay，不是四 slot 装备；`CreeperArmorModelDefinition` 复用 creeper body/head/legs grow2 raw 树，keyed 样本 144 顶点 strict 1e-5 + UV half。production 已由 task-254 接入 `DATA_IS_POWERED`（accessor 17/serializer 8）与 `creeper_power` 层，`VisibleWhen IsPowered`。
- **Wolf**：`WOLF_ARMOR` = `AdultWolfModel.createBodyLayer(new CubeDeformation(0.2F))`，`WolfArmorModelDefinition` 复用 adult wolf 全身 grow0.2 raw 树，keyed 样本 264 顶点 strict；resolver type 152 的 `WolfBody` 层走 raw，crackiness 材质 overlay 保持 legacy；humanoid/其他 armor 与材质层零回归。
- **验收**：full 3,687+、三门禁。

### task-247 Humanoid-derived armor sets raw

- **复用**：`DROWNED_ARMOR`/`BOGGED_ARMOR`/`PARCHED_ARMOR` 均 `putFrom(humanoidArmor)`，`PLAYER_SLIM_ARMOR` 与 `PLAYER_ARMOR` 同 `playerArmor`，`DROWNED_BABY_ARMOR` 与 `humanoidBabyArmor` 同源；这些 set 直接复用现有 Player/Baby raw 定义，不复制 mesh。
- **Giant**：`GIANT_ARMOR` = `humanoidArmor.map(layer -> layer.apply(MeshTransformer.scaling(6.0)))`；`GiantArmorModelDefinition` 静态烘焙 6× pivot/cube + 源 yOffset，UV 维度保持原值，sample strict 通过。
- **验收**：Drowned adult/baby、Bogged、Parched、PlayerSlim、Giant 共 24 个 keyed 样本（48/72/72/48、baby 24/72/72/48、Giant 48/72/72/48）位置 1e-5 + UV half；resolver 覆盖 Drowned/Bogged/Giant/PlayerSlim，Parched armor 已由 task-257 接入实体与 production raw；其余 armor/材质层零回归；full 3,662+、三门禁。

### task-243 Piglin / baby piglin armor raw

- **源证据**：`PIGLIN_ARMOR`/`PIGLIN_BRUTE_ARMOR`/`ZOMBIFIED_PIGLIN_ARMOR` 均 `putFrom(piglinArmor)`（`PiglinModel.createArmorMeshSet(INNER, 1.02)` + `removeEars` 空组）；`PIGLIN_BABY_ARMOR`/`ZOMBIFIED_PIGLIN_BABY_ARMOR` 均 `putFrom(piglinBabyArmor)`（`AbstractPiglinModel.createBabyArmorMeshSet(0.7,0.7, armOffset(0.5,-0.5,0))`）。鼻子 cube 不属于 armor mesh，猪灵耳朵是空组。
- **定义**：`PiglinArmorModelDefinition` adult outer 1.02 / inner 0.5（legs extend -0.1）、baby uniform 0.7（legs/waist extend -0.1）+ arm offset；texture adult 64×32、baby 64×64，raw baseline 0、无 FaceTransforms，保留空耳组。
- **验收**：Piglin/ZombifiedPiglin adult+baby 共 16 个 keyed 样本（48/72/72/48、24/72/72/48）位置 1e-5 + UV half；production resolver 覆盖 Piglin/Brute/ZombifiedPiglin；adult/baby humanoid、材质层零回归；full 3,636+、三门禁。

<!-- simplification:SIM-ENTITY-RAW-SCALE task:T-41 -->

### SIM-ENTITY-RAW-DEGENERATE：raw 零面积部件退化面（已解决）
- **修复（task-145/174/181 / raw only）**：raw 路径遇到零 depth/height 或零宽度的 cube 不再走 legacy flat/cull，按源 `ModelPart.Cube` 六面发射 24 顶点；位置/UV 与源 polygon 顺序一致，legacy 路径不变。
- **chicken-baby 验收**：源样本 192 顶点全部纳入 `LivingRawReferenceSampleTests` 严格逐点比较（位置+UV half 1e-5），常量 `192/0/192/0`。
- **task-174 fish/bee 验收**：`includeRawDegenerate` 不再按维度排除，零面积组全部按源 24 顶点发射并纳入 strict：cod/salmon/tropical/pufferfish/tadpole/bee/bee-baby/parrot/allay；常量全源覆盖（bee 含 mirror 24）。raw UV 对源负值/超范围值直通（不做 UV 归一化）。
- **task-181 salmon 验收**：`right_fin` 源 `texOffs=-4`；DSL 原写成 `28`（32px wrap），改为源字面 `-4` 后 raw UV 与 24 行样本 0/负值严格一致；`SalmonModelDefinition` 结构测试同步源值。salmon 常量 `192/0/192/0`，不再排除。
- **状态**：`已解决`（chicken-baby + fish/bee + salmon 全部 strict）。
- **验收证据**：`LivingRawReferenceSampleTests` + full 3,546 + 三门禁。
<!-- simplification:SIM-ENTITY-RAW-DEGENERATE task:T-41 -->

### 实体 raw 切片的登记口径（docs-only 限制 / baby N/A / ribcage flatten）

- **docs-only 口径**：以上四个 `SIM-ENTITY-RAW-*` 条目是"raw 尚未覆盖的路径"的文档登记，不是代码中的 `[Simplification]` 特性：它们描述的是**未实现的 raw 分支**，没有对应的生产代码可用特性标注；其可审计性由 `docs/risks.md` R-026/R-027/R-028/R-029 + `check-simplifications.ps1` 的 `simplification:` marker + 本 Issue #102 的 comment 登记共同提供。这与 active code simplification（有 `[Simplification]` 标记、`active=25` 计数）口径不同，特此说明。独立审计任务统一挂在 task-41 / Issue #102。
- **baby 变体 N/A**：Creeper（typeId 32）/ Spider（127）/ WitherBoss（149）在原版没有独立的 baby 模型定义（baby 仅存在于猪/牛/鸡等哺乳类族），因此这三族的 reference sample 只有 adult bind pose，baby 覆盖不适用（N/A）。`LivingRawReferenceSampleTests.BabyVariantsAreNotApplicableToTheseFamilies` 显式核查 resolver 无 `baby_` 模型。哺乳类的 baby 覆盖在其各自迁移批次处理。
- **Wither ribcage flatten 限制**：源 `WitherBossModel` 的 `ribcage` 是**一个 ModelPart 含四个 cube**（body + 三条 rib），本仓 DSL 将其拆为 `ribcage`/`rib_1..3` 独立节点。按 part 隔离的 model-space 源样本比对无法重建父 part 的 bind-pose 旋转，故 `/ribcage` 的 cube>0 组在该比对中排除；这些 cube 由 `RawModelFinalWorldMatchesLegacyTwin`（真实全模型 final world 顶点逐点）覆盖，不属于静默跳过。此限制并入 R-027 的同类"结构差异"审计。

### task-62 水生/飞行实体族 raw 迁移（#102）

将 14 族（Cod/Salmon/TropicalFish/Pufferfish/Tadpole/Squid/Dolphin/Axolotl/Turtle/Frog/Bat/Bee/Parrot/Allay，含 warm/cold/baby 变体）切到 `rawSourceFrame:true`；依赖 main 的 #128（`PartWorldMatrix` raw 逐层传播 / NodeFrame T-R 修复，PR #130）。

- **原版基准样本（jar e877b6a0，RawRenderUnitProbe）**：21 个样本落盘 `tests/.../Repro/Data/`（含 tropical-fish small/large、pufferfish small/medium/large、axolotl/turtle/bee baby、glow-squid 等变体）。样本为 **#128 归一化渲染单位** `R*(v_px/16)+T_blocks`（源 `ModelPart.translateAndRotate` + `Vertex.SCALE_FACTOR=16`，探针独立公式，非生产反推），测试用 `modelScale:1f`。`LivingRawReferenceSampleTests.Families` 以硬编码 `(total, mirror, checked, degenerate)` 常量独立断言，配方 `total = mirror + degenerate + checked`，非 sample 自洽；源顶点按 `part|cube|polygon|vertex` 键严格比位置+UV，**位置与 UV 容差均为 1e-5**（与 Book 标准一致），UV 先量化到 half-float（与几何缓冲存储一致）再比。为达到 1e-5，Squid 触手 pivot（源 `cos/sin(π/4)*5`）与 Wither tail pivot（源 `6.9+cos(0.20420352)*10` 等）改用源全精度字面值（原先 4 位截断），并同步 legacy twin / 定义测试的期望值；生产 raw bake 的全精度化见 #132。
- **旋转字面值**：改为源原版字面值（如 Turtle body `+π/2`、Parrot body `+0.4937`、Cod 鳍 `-π/4`、Dolphin `+π/3`）；源 literal 与迁移前 legacy DSL 的共轭关系逐族核对，非一刀切取反（Parrot feather 等保持原值）。
- **多 cube 拆flat 映射**：源一个 ModelPart 含多 cube、DSL 拆为独立 part 的（Turtle body+belly、frog body/head+layer、axolotl body+body_fin/body_layer、bee-baby bone+antenna_left、allay body+body_inner 等）在 `FlatPartsForSampleCube` 按源 `addBox` 顺序显式映射；源文件 cube 顺序已逐族核对。
- **blocker**：零面积部件（鱼鳍、蝙蝠翼、蜂脚、Turtle egg_belly 等）的退化面按 R-029 / Issue #126 显式登记——这些顶点未纳入严格 UV 比较，位置也未被独立 oracle 覆盖（显式缺口）；mirror 部件按真实 `Mirror` 标记排除（R-027 / #121）。squid 触手等无退化部件的族全顶点严格通过。
### task-116 raw humanoid attack-arm pose frame（#134）

- **根因**：`HumanoidModel.setupAttackAnimation` 把 `rightArm.x/z` 设为**绝对源 part 位置**（源 bind pivot `(-5,2,0)`）；legacy `ToOffset` 把位置 X/Y 翻转以适配 legacy bake，raw 路径若沿用会得到反向的 arm delta；同时源模型是 y-down，而 mesher 旋转矩阵按 y-up 语义使用，raw 帧中 pose rotation 的 X/Z 需要 Y 反射共轭。
- **公式（task-123 定稿）**：`PoseOffset` 存**源字面值** position `(PosX, PosY, PosZ)` + rotation `(RotX, RotY, RotZ)`；living raw 的 mesher 合成时做 X/Z 共轭：`rotation = (baseX - poseX, baseY + poseY, baseZ - poseZ)`，位置直接 `pivot + pose`。legacy 保持 `(-PosX,-PosY,PosZ)` + `(RotX,RotY,RotZ)`；block raw（Banner/BoxProbe 等）保持作者源字面，不做 animation 共轭。attack 源 delta：`dx = 5 - cos(bodyYRot)*5`、`dz = sin(bodyYRot)*5`（源 pivot -5）。
- **修复**：`HumanoidModelPose.ToOffset(raw)` 不再取负 X/Z；`NodeFrame`/`PartFrame`/`PartWorldMatrix` 在 living raw 路径合成 `-X/+Y/-Z` pose 共轭，block 路径不变；legacy 分支不变。raw 帧的 attack 源 delta 按 `dx = 5 - cos(bodyYRot)*5` 重建；分支回归用测试内直接构造的 raw Zombie/Skeleton 定义 + `HumanoidModelPose.Apply(..., rawSourceFrame:true)`，不依赖当前 registry 的迁移状态。
- **验证**：`RawHumanoidAttackArmTests` 2/2（raw Zombie 几何 world X delta ≤0.08；raw Skeleton 源 pivot delta ≤0.08 + 源 absolute position 公式）；legacy `HumanoidAttackArmTests` 2/2 期望未改。held-item/spear/elytra 定向 27/27；全量 3,493 passed / 0 failed（Client 2,530）；三门禁绿。
- **集成状态（历史记录，已由 task-121/128 取代）**：本 task-116 落地时 HumanoidMob/Player 定义仍 legacy；随后 task-121 迁移 7 个 HumanoidMob 族、task-128 迁移 Player/player-slim 并解耦 Piglin，raw registry 已覆盖这些族。当时 Player/Player-slim 的 `FlatPartsForSampleCube` harness 缺口已在 task-128 用独立 overlay child part 解决。

### task-95 humanoid hat 独立 cube（#134）——部分解阻塞

task-95 已把 **Illager 4 族（vindicator/evoker/pillager/illusioner）+ Villager + WanderingTrader** 切 raw 并 1e-5 严格验收：hat/hat_rim 从单一 `Grow` OverlayLayer 改为源独立子 cube（用现有 `AddOrReplaceChild`/多 cube DSL，无需新增 API）、源字面 arms 旋转（−0.75）、layer scale 0.9375 + `MeshTransformer.scaling` 的 root yOffset（`BuildModel` 路径内 24.016*(1-scale)/16）。`LivingRawReferenceSampleTests` 对每组 288/264 行逐 `part|cube|polygon|vertex` 严格比较（剥离 `VisibleWhen` 以匹配 bind-pose 样本、携带 definition.Scale）。held-item/arm/elytra + RawLayerScaleOffset 回归 37/37 绿。（当时其余族后来已由 task-121/128/173/178/#176/185 切 raw；task-189/190/198/199 又迁移 22 个 baby，现已全部 raw。）

### task-79 humanoid / 村民 / 灾厄 / Piglin 批次：阻塞登记（#102 / #134）

> **当前状态（R-031 修正，取代本节历史"22 族"计数）**：Player/player-slim、HumanoidMob 8 族、Witch、ZombieVillager、Bogged、Piglin×3 均已切 raw（R-035/#150 全清）。**22 个 baby 全部迁移（22/22）**：task-189 的 7 个（Humanoid 4 + Odd 3）+ task-190 的 9 个（Nether/Equine/Extra）+ task-198 的 3 个（Misc goat/panda/polar bear）+ task-199 的 3 个（Final happy_ghast/nautilus/glow_squid）。已 raw 的 baby 共 34 个（含早期 12 个：pig/cow/chicken/squid/bee/dolphin/axolotl/turtle/llama/hoglin/zoglin/armadillo）。**无剩余 blocker，Issue #134 关闭。**

以下历史原因适用于当时尚未迁移的族；Illager/Villager 6 族已按上段解决，其余非 baby 族亦已由后续 task-121/128/173/178/#176/185 解决：

- 源 hat/overlay 是独立 cube（不同尺寸/pivot/deformation，如 illager hat 8×12×8+0.45 vs head 8×10×8+0.45），仓内 DSL 用单一 `Grow` 近似，无法源正确表达；
- 源 `MeshTransformer.scaling(factor)`（`MeshTransformer.java:7-9`）在 baked root 加 `yOffset=24.016*(1-factor)`；**规格与 living 路径实现已由 #141/R-034 解决**（见下节）。humanoid 仍受独立 hat/overlay DSL 缺口（#134）阻塞；
- 已生成 28 个 #128 归一化 jar 样本（render-unit，1e-5）落盘 `tests/.../Repro/Data/`，作为后续解阻塞的验收真值；不放宽容差、不按样本拟合、不排除顶点。
- 验收条件：DSL 扩展支持独立 overlay cube 维度/pivot/deformation + 补 layer-scale root 偏移并同步 #120 held-item frame，然后逐族严格 1e-5 通过后再切 raw。
### task-89 剩余 quadruped 批次：阻塞登记（#102 / #137）

本批 15 成年族（Sheep/Wolf/Fox/Cat/Horse/Camel/Goat/Panda/PolarBear/Rabbit/Armadillo/Llama/TraderLlama/Hoglin/Ravager）：**15/15 全部切 raw**（task-135: Sheep/PolarBear；task-137: Fox/Wolf/Cat/Horse；task-143: Camel/Goat/Panda/Rabbit；task-150: Llama/TraderLlama/Hoglin/Ravager/Armadillo），每族严格 1e-5 jar 样本通过，基于 main 的 #156 face order + #141 offset + #121 mirror；独立源 bug 修正含 Cat tail_g=-0.02、Horse head_parts +π/6、Wolf 右腿 Mirror、Goat nose +0.9599、Rabbit body -0.3927、Ravager 源 Mirror right_horn+left legs、Armadillo zero-depth ears 等，全部 coat 变体 raw + 变体 flag 断言。**baby 变体**（baby-llama/hoglin/armadillo，task-158）也已切 raw：各自 Baby*Model 几何（非成年 mesh），严格 1e-5 样本（264 行）通过；baby_llama coat 变体 raw + flag 断言。task-190 再切 9 族 baby raw（piglin/zombified_piglin/horse/camel/donkey/cat/wolf/fox/rabbit），包含零深度 source 支持、DSL pose/映射修正（Horse head_parts +0.6109、Cat/Wolf tail 负号、多 cube head 映射）与严格 1e-5 样本；本批 9 族完成，剩余 3 个 baby 模型见 R-031。见 Issue #137 / R-032：

- 剩余族源 jar 几何与 DSL 在 1e-5 下不符（自定义几何 / multi-cube head 拆 flat / layer-scale），需逐族按源 probe 修正。
- 已生成 27 个 #128 归一化 jar 样本（render-unit，1e-5）落盘 `tests/.../Repro/Data/` 作为验收真值；不放宽容差、不按样本拟合、不排除顶点。
### task-94 special/large 第一组（#102 / #140）

最终结果：**6/6 族源正确并切 raw，Issue #140 已解决**。Shulker/Blaze/Endermite/HappyGhast 4 族早期迁移（HappyGhast Scale=4 由 #141 解锁）。task-109 修复并迁移 **5 族**：Ghast（tentacle pivot 整数除法→源浮点除法）、Guardian（spike 呼吸因子 `cos(i)*0.01`）、IronGolem/Creaking/Breeze（multi-cube head/body 映射、零面积部件显式登记），每族 jar 样本严格 1e-5 通过。**Giant 由 task-138 迁移**：复用 raw Zombie 几何（含 #134 已解决的独立 hat cube）+ Scale=6 root offset（#141），严格 1e-5 样本 168 行通过，不再 legacy、不再归 #134/#140 blocker。10 个 #128 归一化 jar 样本落盘 `Repro/Data/` 作验收真值；不放宽容差、不拟合、不排除顶点（退化面显式登记）。
### task-97/98 layer-scale root offset 拆分（#102 / #141）

Blaze production definition 补齐 `rawSourceFrame:true`/`baselinePx:0`（此前只在 bake 调用上有，真实 living 渲染仍走 legacy）；新增测试 `Task94MigratedProductionDefinitionsCarryRawSourceFrameFlags` 防止隔离定义误绿。

HappyGhast（Scale=4）在 #141/R-034 规格与 raw living 实现落地后，由 task-104 完成 raw 迁移：production definition 带 `rawSourceFrame:true`/`baselinePx:0`/Scale=4，严格 1e-5 样本 + production 定义 live world（全 240 顶点）均通过。此拆分仅为过程记录；当前状态以 task-94 段为准。

### raw layer-scale root offset 规格（#141 / R-034）— 已解决

- **源**：`MeshTransformer.scaling(f)` 只改 root `PartPose`：`yOffsetPx = 24.016*(1-f)`，`pose.translate(0, yOffsetPx/16, 0)` 后 `scale(f)`；root 旋转不变。`RootFrame` jar probe：`HAPPY_GHAST root.y=-72.048, scale=4`；`VILLAGER/PILLAGER root.y=+1.501, scale=0.9375`；`CAVE_SPIDER root.y=+7.2048, scale=0.7`。
- **单位/作用点**：`yOffsetPx` 是源像素，root 平移为 `yOffsetPx/16` 块；它**不被 layer scale 乘**，且在 entity flip 之前施加。raw bake 已把 `f` 折进几何/ pivot 的 `px`，所以 offset 的模型空间量是 `24.016*(1-f)/16 * modelScale`（base px，不含 `f`）。
- **BuildModel（factory != null，无 raw flip）**：entity translation 加 `+offset`（既有行为，不重复）。
- **living（factory == null）**：offset 先进入 raw model y（等价于 `Translation.Y -= offset * entityScaleY`），再由 `ForRawSourceFrame` 翻转；最终世界贡献是 `-offset`。baseline 是**源 flip 常量**、在 layer scale 之外：`RawBaselineWorld = 24.016/16 * modelScale`（源 `LivingEntityRenderer.scale(-1,-1,1)` + `translate(0,-1.501,0)`；B-1/#228 校正前误用 24/16），**不能**再乘 `Scale`（旧实现乘 `px`，CaveSpider 因此差 `0.405` 块）。
- **验收**：`RawLayerScaleOffsetTests`：HappyGhast factor=4 全 **240/240** 源样本顶点 live world 1e-5；Villager/Pillager `.9375` 首 root 顶点；CaveSpider `.7` 四朝向源样本驱动；`BuildModelDoesNotApplyTheLivingOffsetTwice`。`LivingRawReferenceSampleTests` 的 CaveSpider legacy-twin 已替换为源 sample 驱动（旧 twin 保留错误坐标，不再作为 oracle）。
### task-108/121 剩余 humanoid 批次：部分迁移 + 阻塞（#102 / #150）

task-121 用 jar 源 probe（`HumanoidAnimProbe`，源 `head.xRot=state.xRot*PI/180`、`head.y+=4.2F` 等字面值）配合 core PR #153（PoseOffset 存源字面值、共轭移入 raw mesher）解除动画阻塞。结果：

- **7 族已切 raw 并全绿**：Skeleton/Stray/WitherSkeleton/Husk/Drowned/Enderman/Zombie。严格 1e-5 样本 + 11 个源动画断言（crouch/swim/head pose/attack）全通过；crouch head `+4.2` 断言按源字面值更新（PR #153 后 raw pose 存源值）。定义/resolver/tree 期望按独立源字面 `RawModelPartFactory` + 独立 hat 子 part 更新，bake hash 仅变更检测器刷新。
- **后续（task-121 当时仍阻塞，已由 task-128 更新）**：Player/player-slim 当时因 parts 复用耦合保持 legacy；task-128 已用 Piglin 独立 legacy 身体 DSL 解耦并迁移 Player/player-slim，当前以 task-128 段为准。task-121 当时其余 blocker：Bogged（头蘑菇零深度多 cube）、Witch/ZombieVillager/Piglin×3（ears + 衣服 overlay / 多 cube head DSL 缺口）。28 个 #128 样本保留作验收真值；不放宽容差、不拟合、不排除顶点。
### task-128 Player raw + Piglin 解耦（#102 / #150）

Player/player-slim 切 raw：hat/jacket/sleeve/pants 转为独立 inflated child cube（源 `PlayerModel.createMesh` 结构），严格 1e-5 样本 55/55，held/attack/elytra 回归由 core PR #147/#148/#149 覆盖。Piglin 改用独立 legacy 身体 DSL（5 身体 part 源字面，不引用 Player DSL/baked parts），保持现有 11-part legacy 结构与行为不变。

**已知简化（历史）**：源 `AdultPiglinModel.createBodyLayer`（jar probe）含 jacket/sleeve/pants overlay 子 part，legacy Piglin 未建模——既有结构简化，登记 R-035 / #150，本次不改（改会破坏 legacy 行为）。Bogged/Witch/ZombieVillager/Piglin×3 后续分别由 task-178 / PR #178 / task-173 / task-185 迁 raw；本段保留 task-128 当时历史。
### task-144 剩余 humanoid 6 族：逐族源 probe 结论（#102 / #150）

task-144 用 jar `HeadTree` probe 逐族分析 Bogged/Witch/ZombieVillager/Piglin×3，全部保持 legacy，不半迁移：
- **Bogged（task-178 已解决）**：源 head 下 6 个零深度 mushroom 子 part（6×4×0 平面，pivot/rotation 源字面），已加入 Bogged DSL（hat 独立 cube 保留）；312 源行 strict 1e-5 + UV half 全通过，零深度 24 顶点由 task-145 raw 支持发射；R-035 仅剩 Witch/Piglin×3。
- **Witch**：head cube UV 不符（`/head cube=0 poly=0 v=2` 源 v=0.0625 vs 本地 0.125），需逐面 probe 求源 UV 校准；hat→hat_rim/hat2→hat3→hat4 多 cube。已由 PR #178 解决。
- **ZombieVillager**：nose cube（`/head cube=1`）pose 换算差：源 (0.0625,-0.1875,-0.25) vs 本地 (0.25,-0.625,0.25)，多 cube head 拆 flat 的 pose 换算。已由 task-173 解决。
- **Piglin×3**：源含 jacket/sleeve/pants overlay 子 part，legacy 11-part 为既有结构简化（task-128 已登记）；补齐会破坏 legacy 行为，故不源正确、不迁移。已由 task-185 以源 16-part 定义解决。

28 个 #128 样本 + jar probe 保留作验收真值；不放宽容差、不拟合、不排除顶点。R-035 / Issue #150 同步。
### task-151 Warden/Phantom raw（#102）

两个大型特殊族切 raw，独立 jar probe（`RawRenderUnitProbe`/`HeadTree`）采源几何+UV，落 #128 归一化样本：Warden 240 行（144 checked + 96 degenerate，零深度肋骨/触须面）、Phantom 192 行（48 mirror + 144 checked）。严格 1e-5 位置 + UV half 全通过。
- **源修复**：Phantom body `PartPose.rotation(-0.1F,0,0)`（DSL 原为 +0.1，符号错误）；Warden 复用 DSL 原几何本即源正确，仅 production 定义补 raw flags。
- 定义/reference/分层测试改用独立源字面 `RawModelPartFactory` + raw oracle；bake hash 仅变更检测器刷新。无容差放宽、不拟合、不排除顶点。
### task-159 Silverfish/Slime/MagmaCube raw（#102）

三族切 raw，独立 jar probe（`RawRenderUnitProbe`/`HeadTree`）采源几何+UV，落 #128 归一化样本：Silverfish 240、Slime 96、MagmaCube 216 行。严格 1e-5 位置 + UV half 全通过。
- **源修复**：Silverfish seg4/5/6 与 layer1 的 zPlacement 按源累积公式 `(depth_i+depth_i+1)/2` 修正（原 6.5/8.5/10.5 → 源 7/9.5/11.5）；Slime/MagmaCube DSL 几何本即源正确，仅补 production raw flags。
- 定义测试改用独立源字面 `RawModelPartFactory`；bake hash 仅变更检测器刷新。无容差放宽、不拟合、不排除顶点。本批无剩余 blocker。

### Issue #229 移除生产 flat Parts 网格路径

- **生产改动**：`EntityModelMesher.Transforms.cs` 的 `BuildInto` 改为 compiled-only —— 删除 legacy flat `Parts` 网格循环（约 70 行）及其私有 helper `PartFrame`/`PartMatrix`/`PosePartName`；`model.Compiled is null` 由静默回退改为显式 `InvalidOperationException`（可观测失败，原则 16）。生产几何只剩 compiled tree 一条路径（原则 14 单轨）。`PartWorldMatrix`/`LegacyHandWorldMatrix`/定义 `Parts` 数据均保留（数据与动态适配器不在本次范围）。
- **测试迁移**：`VisibleSidesTests`/`RawMirrorSourceTests`/`RawFaceOrderTests`/`LivingRawReferenceSampleTests` 原先以 `Compiled=null` 的 part-only 定义命中 flat 路径；现统一经 `CompiledPartHelper`（测试辅助）把烘焙 part 数据原样构造成 compiled tree（含真实 Parent 链以合成祖先帧），再用生产 compiled Emit 出图。jar 源样本仍是独立 oracle；源 strict 结果不变。无把 flat mesher 复制到测试自证。
- **等价验证**：helper 只把已烘焙 part 数据构造成 compiled tree，由生产 compiled Emit 出图；jar 源样本仍是独立 oracle。完整生产 compiled 模型对 jar 样本 spider/head/sleeve 24/24、armadillo 48/24 等全匹配。4 个迁移测试类随仓库 full suite 运行。
- **审计漏项修复（复审）**：首次审计只枚举 `EntityModelResolver.Definitions`（298 个实体定义，全有 compiled），漏掉块实体/回退等**不注册在 resolver** 的定义。新增 `EntityModelDefinitionAuditTests` 按 **static-field graph, reference-deduped** 口径扫描：`Sharpcraft.Client` 所有类型的静态字段（含 nested/private），字段类型为 `EntityModelDefinition`、`EntityModelDefinition[]`、`IEnumerable/IReadOnlyList<EntityModelDefinition>`、`Dictionary/IReadOnlyDictionary<,EntityModelDefinition>`；展开容器元素并按引用去重，再并入 `EntityModelResolver.Definitions`。隔离运行实测基线 **386**（不是外部探针的 642）；full suite 因测试侧会向 client 静态缓存注册派生定义而观测到 **437**，故门禁断言 **≥386** 且每条 `Compiled != null`、`FLAT_NO_COMPILED=0`。审计发现 6 个 `Compiled=null` 的 flat 定义被生产 mesher 命中：`chest_single`/`chest_double_left`/`chest_double_right`（`ChestModelDefinition`）、`shulker_box_raw`（`ShulkerBoxModelDefinition`）、`dragon_skull`（`SkullModelDefinition`）、`placeholder`（`EntityModelResolver`）。逐个迁移：chest/shulker 传 `compiled: VanillaMeshBakery.Build(同一 DSL mesh, ...)`；dragon 特殊——其 flat parts 带缩放烘焙后的 UV pin（`PinDragonUv`），编译树对同名 cube 施加同样的 `UvWidth/Height/Depth` pin；placeholder 用同一 part 数据构造编译树。修复后全仓反射 `FLAT_NO_COMPILED=0`。
- **等价验证（仓库内）**：迁移后 full `dotnet test` **3,763 passed / 0 failed**（修复前 34 failed，均为 "no compiled tree" 异常）。`EntityModelDefinitionAuditTests.MigratedSixDefinitionsRemainCoveredByKeyedSamplesOrStructure` 显式复跑 6 定义的仓库内证据：chest 3 族 keyed `ChestRawReferenceSampleTests`、shulker keyed `ShulkerBoxRawReferenceSampleTests`、dragon skull keyed `LivingRawReferenceSampleTests.RawBakeMatchesSourceVertices`（256/256，168 total/48 mirror/120 checked），placeholder 无源样本但编译树确定性出 24 顶点。另有 `ChestMesherGolden/Geometry`、`BoxWindingTests`、`SkullRendererTests`、`Repro_042/850` 全绿。`DragonSkullRawTests` 更新过期断言（原 `Assert.Null(Compiled)` → `Assert.NotNull`，缩放头 UV pin 仍由 golden 验证）。
- **遗留**：A 类 flat 循环生产不可达已删且所有生产定义已 compiled；B 类中 83 个 raw=false 变体已由 B-1（Issue #228）迁移，saddle/llama decor/happy ghast harness 已由 B-2（#228）迁移，stray/wither_skeleton/zombie_villager armor 已由 B-3（#228）迁移（见下三节），resolver 的 legacy 24px 装备层回退已删除；held_special/held_potion 仍按 #228 后续推进；C 类动态适配器保留。

### Issue #231 移除 EntityModelDefinition.Parts 扁平数据

- **生产改动**：`EntityModelDefinition` 删除 `Parts` 构造参数与属性；`FromTree` 不再 flat bake（`VanillaMeshBake.Bake` 只在测试辅助保留），`TexturePaths` 改扫 `Compiled.Cubes`。`HasEmissive`/`ChestMesher.lidStart` 已改读 compiled（`f363a272`）。约 156 处构造点删去 flat parts 实参；变体构造（`VillagerModelDefinition.VariantMap`、`HumanoidMobModelDefinition.WithClothing`）中不再使用的 flat parts 形参/死代码一并移除。`EntityModelPart` 保留为 mesher 的逐 cube 生产描述符（224 处生产引用），按裁决不放进测试程序集。
- **测试迁移**：~71 个纯 flat 形状自检（烘焙逐字段 / count / 对象同一性）删除，其源等效由 `LivingRawReferenceSampleTests.RawBakeMatchesSourceVertices`（jar keyed 样本 1e-5 + UV half）覆盖；其余结构/层级/可见性断言迁到 `definition.Compiled`。test-only `CompiledStructuralView` 仅作结构数据视图（node pose + cube geometry），**非源等效证据**；龙 skull 的源 `PartPose.scaled(0.75)` 预缩放头 box 在 oracle 中由编译节点 + 源 UV pin 重建，非自证。无源样本家庭若有无法保留的具体断言则删除并记为 blocker（见 #231）。
- **门禁**：新增 `EntityModelPartEncapsulationTests.EntityModelDefinitionHasNoFlatPartsApi`（反射断言无 `Parts` 属性/构造参数 + 静态扫描生产无 `definition.Parts` 数据访问）。三门禁绿、build 0/0、diff 干净。
- **验证**：逐切片 commit（`f363a272`/`07521345`/`7788f594`/`94b4af88`/`6a058cea`/`5ae1df9e`/`3da8aa34`/`191e61b5` 及最终生产删除），每片受影响类直接 harness 全绿；full `dotnet test` **3,691 passed / 0 failed**（含新增 2 个 TexturePaths 测试；≥3,689）。`TexturePaths` 已改为单次扫描 `Compiled.Cubes` 并用有序集合去重，新增 `EntityModelTexturePathTests` 断言注册定义无重复路径及 main/emissive/additional/cube 路径只 yield 一次；`Compiled` XML 与类 summary 已改为 compiled-only/null 显式异常，不再描述 legacy flat parts。

### Issue #228 B-1：83 个 raw=false 变体迁移

- **范围**：`EntityModelResolver.Definitions` 298 个定义中 83 个 `RawSourceFrame=false`（shulker×16、baby×41、parrot×4、axolotl×4、tropical fish×12、salmon×2、frog×2、dolphin、turtle）。这些定义的 `Compiled` 早已由 `VanillaMeshBakery.Build(..., rawSourceFrame: true)` 烘焙，但变体构造漏传 `rawSourceFrame`/`baselinePx`，导致 living 路径跳过 `EntityModelTransform.ForRawSourceFrame`：整模型丢失源 `scale(-1,-1,1)` 翻转与 baseline，渲染成错误帧。源侧核对：各 family 的 `ModelLayers` 模型与 `ShulkerRenderer.getTextureLocation(state.color)` 等均为 raw source-frame；shulker 16 色是纹理变体、模型层不变（`EntityVariantSpecs.cs:130` 已按 accessor 18 选色）。
- **修复**：逐族（8 个 family 定义文件 + 4 个 baby 批次文件）补 `baselinePx: 0f, rawSourceFrame: true`，保留纹理/scale/geometry；并把 `EntityModelDefinition` 的 `sourceFrameBaselinePx` 默认值与 `EntityModelTransform.RawBaselinePx` 从 24px 校正为 `VanillaMeshBake.ReferencePivotHeight`（24.016px = 1.501 块），使 raw living 世界坐标与源 `translate(0,-1.501,0)` 一致（此前 24/16=1.500，与源差 0.001 块）。
- **源样本**：`tests/Sharpcraft.Client.Tests/Repro/Data/parrot-blue-world-frame.reference.txt`（PARROT 264 行）与 `dolphin-world-frame.reference.txt`（DOLPHIN 192 行），由 `ref://mc-26.3-analysis/vectors/b-class-source/LivingWorldFrameProbe.java` 生成：world = `S(-1,-1,1)·T(0,-1.501,0)·(part pivot translate/rotationZYX · v_px/16)`，排除实体 yaw/平移/缩放。
- **验证**：`VariantRawFrameTests`（4 组）——production `BuildLayers`（variants=Builtin、bind pose、yaw 180）归一化 `EntityPlaceholderMesher.ModelScale` 后与两组 world 样本 keyed 1e-5（位置一对一消费 + UV half multiset）；源 world 样本与各族既有 model-space 样本按 `part|cube|polygon|vertex` 对 `S·T(1.501)` 公式 1e-5 对拍；全定义 `RawSourceFrame`/`BaselinePx` 断言（无 legacy 帧）；shulker 16 色纹理变体保留。既有 model-space strict 测试（`LivingRawReferenceSampleTests`）不变，legacy twin 与层缩放期望按 24.016 baseline 同步校正（twin 注释说明）。
- **blocker**：其余 81 个变体/族只有各族 model-space 源样本，没有独立 world-frame 样本；补齐需按族重采（不影响已验收的 flip/flip 数学与定义断言）。登记 R-040。

### Issue #228 B-2：鞍具 / 装饰 / harness raw 迁移

- **范围**：equine saddle（Horse/Donkey/Mule/SkeletonHorse/ZombieHorse）、camel saddle、nautilus saddle、llama decor（adult/baby）、happy ghast harness。此前 `EquipmentModelTrees` 用 `VanillaMeshBakery.Build(mesh, w, h)` 烘焙（legacy 帧），resolver 对这些层固定 `baselinePx: 24f, rawSourceFrame: false, sourceFrameBaselinePx: 24f`，与源 `EquipmentLayerRenderer` 的 raw `ModelPart` 帧不一致。
- **修复**：`EquipmentModelTrees` 中上述 6 个 tree 加 `rawSourceFrame: true`（几何/纹理/pivot 不变）；新增 `LlamaBabyDecor`（baby llama mesh grow 0.2，`BabyModelDefinitionsEquine.BuildLlamaMesh(0.2f)`，64×64）。resolver 对 `EquipmentModelTrees.IsRawSourceFrame(kind)` 的层用 `baselinePx: 0f, rawSourceFrame: true, sourceFrameBaselinePx: 0f`（层相对已摆放实体，同 armor raw 层），trim/glint 的 `rawMaterial` 同步；baby 非 humanoid 装备层仅对 `LlamaBody` 放行（`babyDecorLayers`），其余 baby 装备层维持原行为。
- **源样本**：`tests/Sharpcraft.Client.Tests/Repro/Data/{equine-saddle,camel-saddle,nautilus-saddle,llama-decor,happy-ghast-harness}-raw-vectors.reference.txt`（`RawRenderUnitProbe`，model-space raw render units；equine 文件含 5 个 source layer，llama 文件含 adult/baby 两 layer）。llama decor 的 `u=1.046875` 越界 UV 是源 `texOffs` 真值，保留不改。
- **验证**：`MountEquipmentRawReferenceSampleTests` 20 cases——production `BuildLayers` 层按 `layer|part|cube|polygon|vertex` 逐 key 位置 1e-5 + UV half 1e-5（每个 sample cube 映射到唯一 compiled cube，`onlyPart` 隔离后按 polygon/vertex 顺序比较，位置与 UV 同 key 绑定）；full layer 顶点数覆盖；`EntitiesWithoutEquipmentEmitNoEquipmentLayer` 断言非 saddled 无层；`LlamaAdultAndBabyDecorUseDistinctRawTrees` 断言 adult 128×64/ baby 64×64、compiled tree 不共享。成人 llama decor 用 node 内多 cube 顺序，camel `/body/head` 与 baby llama `/head` 用显式 source cube→node 映射（不猜）；donkey/mule/llama 的 chest 与 equine saddle line/camel reins 由 `HasChest`/`IsVehicle` 状态打开，与 jar probe 的整层烘焙一致。
- **未改**：其它 B 类（humanoid armor 回退）、held_special/held_potion。`EquipmentModelParts` legacy 结构树已由 #246（R-047 清理）删除（见文末）。pig/strider/camel_husk saddle 已由 #234 补齐（见下节）。

### Issue #228 B-3：stray / wither skeleton / zombie villager armor raw 迁移

- **范围**：humanoid armor 回退的最后 3 个 type id——stray（131）、wither skeleton（150）、zombie villager（157）。此前这些 type 的 4 个 armor slot 走 legacy 层（`baselinePx: 24f, rawSourceFrame: false`，默认 FaceTransforms）。
- **源事实**：stray armor 与 player armor 逐 cube 相同（样本 maxDelta 0）→ 直接复用 `PlayerArmorModelDefinition`；wither skeleton armor = 源 `humanoidArmor.map(layer -> layer.apply(MeshTransformer.scaling(1.2)))`（uniform 1.2 scale + yOffset 24.016*(1-1.2)，UV 不变）→ 新增 `WitherSkeletonArmorModelDefinition`（同 Husk 的静态 raw bake 方式）；zombie villager armor 是独立的 `ZombieVillagerModel.createArmorMeshSet`（head y=-10、body/legs `g.extend(0.1)`、leg 偏移 ±2、hat 下空 `hat_rim` 组，helmet/chest/boots outer 1.0、leggings inner 0.5）→ 新增 `ZombieVillagerArmorModelDefinition`。baby zombie villager 仍走 `BabyHumanoidArmorModelDefinition`（源 `createBabyArmorMeshSet`，不在 B-3 范围）。
- **resolver**：stray 加入 `rawAdultHumanoidArmor` 复用 player raw 定义；wither skeleton / zombie villager 各接独立 raw 定义（`!baby` 限定）。三者 armor 层及其 trim/glint 材质层全部 `baselinePx: 0f, rawSourceFrame: true, sourceFrameBaselinePx: 0f`；装备层 legacy 24px 分支删除。
- **源样本**：`tests/Sharpcraft.Client.Tests/Repro/Data/{stray,wither-skeleton,zombie-villager}-armor-{helmet,chestplate,leggings,boots}-raw-vectors.reference.txt`（12 个，`RawRenderUnitProbe`）。
- **验证**：`HumanoidArmorSetRawReferenceSampleTests` 扩展 stray/wither skeleton（定义级 8 样本 + 生产层逐 key 8 cases）；`CreatureArmorRawReferenceSampleTests` 扩展 zombie villager（定义级 4 样本 + 生产层逐 key 4 cases）；另有 `ProductionMigratedArmorMaterialLayersFollowRawGeometry` 断言三类 4 slot 的 armor/trim/glint 层 raw、baseline 0、几何顶点数一致。生产层比较按 `layer|part|cube|polygon|vertex` 位置 1e-5 + UV half 1e-5（每 sample part 映射到唯一 compiled cube，`onlyPart` 隔离后按 polygon/vertex 顺序，位置与 UV 同 key 绑定），world 反变换 `sample = world / ModelScale`。
- **未改**：其它 armor/实体、baby zombie villager 的 baby armor、held_special/held_potion。`EquipmentModelParts` legacy 结构树已由 #246（R-047 清理）删除（见文末）。

### Issue #234：pig / strider / camel_husk 鞍具

- **源确认**（registry probe + 源渲染器）：pig = 101、strider = 132、camel_husk = 20（`BuiltInRegistries.ENTITY_TYPE`）；三者均为 `SimpleEquipmentLayer` 的 SADDLE 槽装备层，仅 saddled 时 `state.saddle` 非空才渲染。纹理路径 `entity/equipment/{pig_saddle,strider_saddle,camel_husk_saddle}/saddle`（`EquipmentClientInfo.Layer#getTextureLocation`）。
- **几何**：pig saddle = `PigModel.createBodyLayer(new CubeDeformation(0.5F))`（整 pig mesh，head part 含 head+snout 两 cube，grow 0.5）；strider saddle = `AdultStriderModel.createBodyLayer()`（整 strider body，无变形）；camel husk saddle = `CAMEL_HUSK_SADDLE` 与 `CAMEL_SADDLE` 同为 `CamelSaddleModel.createSaddleLayer()`，直接复用 `EquipmentModelTrees.CamelSaddle` tree。
- **接入**：新增 `EquipmentAssetKind.CamelHuskSaddle` 与 catalog 路径/LayerType；`EquipmentModelTrees` 新增 `PigSaddle`/`StriderSaddle` raw tree，`CamelHuskSaddle` 复用 camel tree；`EquipmentLayers` 增加 20/101/132 的 SADDLE 槽映射；`CamelHuskModelDefinition`（id 20，复用 camel compiled mesh、纹理 `entity/camel/camel_husk`）注册进 resolver 与 camel 动画 provider。三者的 saddle 层与 glint 材质层均为 rawSourceFrame/baseline 0。
- **样本 / 验证**：`MissingSaddleRawReferenceSampleTests` 10 cases——每个类型 2 组源样本（raw render-unit + living world-frame，共 6 文件，`ref://mc-26.3-analysis/vectors/b-class-source/{pig,strider,camel-husk}-saddle-{raw-vectors,world-frame}.reference.txt`）；production `BuildLayers` 逐 key（layer/part/cube/polygon/vertex）位置 1e-5 + UV half 1e-5（world 反变换 `/ModelScale`）；world 样本与 raw 样本逐 key 做 `S(-1,-1,1)*T(0,-1.501,0)` 位置绑定与 UV 绑定，并对每个 world key 用 production `BuildLayers` 比较位置与 UV half（equipment 层 baseline0，world 帧扣除实体 living translate 后 ×ModelScale）；saddled 仅出现对应 `equipment_*_7` 层、unsaddled 无层；camel husk 与 camel saddle tree 逐 cube 相同；glint 材质层 raw/baseline0。
- **未改**：其它实体/装备层与 pig/strider/camel_husk 本体几何（camel husk 仅新增 base definition 与纹理映射）。

### Issue #228 B-4 T2：held special trident/shield 迁到 raw 手持链

- **范围**：`held_special_trident_{right,left}` / `held_special_shield_{right,left}` 及其 `_glint` 层。此前它们由 resolver 生成独立层，模型内嵌伪 `right_arm/left_arm` pivot，shield 再用 `LocalTransform` 补偿 `scale(1,-1,-1)·display·anchor`；trident 缺 display/hand 旋转。
- **修复**：`ItemInHandMesher` 新增 special 几何发射：`world = specialLocal · display · stab · anchor · handFrame · entityMatrix`（row-vector，源序 `specialTransform·display·stab·handFrame·entityMatrix`），其中 `specialLocal = scale(1,-1,-1)`（items/{trident,shield}.json `transformation`），`display` 取 base item model（`trident_in_hand`/`trident_throwing`、`shield`/`shield_blocking`）对应 hand 的 display，`handFrame` 复用 `HumanoidHeldHandFrame.SourceWorld`（含 bind/animation pose），glint 用同一几何 + `GlintTextureTransform.EntityScale`。正则 `EntityModelResolver` 删除这两类的 `AddSpecialHandLayers` 调用、伪 pivot 模型字段与 `ApplyShieldHeldTransforms` 补偿；其余特殊层（chest/skull/shulker/banner/pot/conduit）保持不变。
- **样本 / 验证**：`HeldSpecialWorldReferenceSampleTests` 共 **15 tests**，其中 **keyed world 几何 12 cases**（6 bind：`trident-held-world.reference.txt` 120 顶点 × RIGHT/RIGHT_USE/LEFT + `shield-held-world.reference.txt` 48 顶点 × RIGHT/RIGHT_USE/LEFT；6 real-pose：`trident-held-world-stab.reference.txt`（STAB 0.15，SPEAR+`thirdPersonAttackHand`+`thirdPersonAttackItem`）、`trident-held-world-throw-use.reference.txt`（THROW_TRIDENT）、`shield-held-world-use.reference.txt`（ticksUsingItem 10 + BLOCK）、`shield-held-world-block.reference.txt`、**`trident-held-world-left-stab.reference.txt`（offhand trident，SwingHand OFF_HAND + SPEAR 左臂）**、**`shield-held-world-left-use.reference.txt`（offhand shield，UseItemHand OFF_HAND + BLOCK 左臂）**）；**2 个 foil case 只验 glint/base 几何+UV，不计入 keyed 12**；另 1 个 `held_special_head_right/left` 注册测试。production `ItemInHandMesher.Build` 逐 key（layer/part/cube/polygon/vertex）位置 1e-5 + UV half 1e-5（held 层工作在源手帧，world 样本经 `S(-1,-1,1)`、`T(0,-1.501,0)` 反变换后 ×ModelScale）；非 foil 无 `_glint`，foil 的 `_glint` 与 base 位置一致且 UV 按 `EntityScale` 变换。左手 real-pose 覆盖 `EmitHeldSpecial` 的 offhand 路径与 `HumanoidModelPose` 左摆臂分支。`ItemGlintTests` 改为在 mesher 层断言；`SpecialItemModelDefinitionTests` 断言 special 模型为 raw root space、无伪 arm pivot。
- **blocker（A 方案不可行）**：special raw 几何是 cube 模型，item quad 是另一个几何域；无法与 item quad 共几何缓冲（也不应硬塞）。已结构化上报：保留独立几何缓冲但使用同一 raw 手链，满足 raw hand frame 要求；后续如需单缓冲需引入 cube→quad 或统一 vertex 域的独立设计。
- **未改**：普通 held item 路径、其它 special renderer、witch potion；bind 样本为 PLAYER bind pose，real-pose 样本已覆盖左右手 STAB 与 use/block 手臂姿态（`HumanoidModelPose` 补 `thirdPersonAttackHand` STAB 弧；左侧 body 扭转符号、arm pivot、bob 与 BLOCK/SPEAR 左臂值均按源），display 按探针 `ItemTransform` 右值 + leftHand fix 约定（fixture 左 display 同右值）。`AddSpecialHeadLayers` 恢复调用并有注册测试（`held_special_head_right/left`）。

### Issue #228 B-4 T3：held chest/skull/banner 迁到 raw 手持链

- **范围**：`held_special_chest_{right,left}`、`held_special_head_{right,left}`（skull）、`held_special_banner_{right,left}` 及 `held_special_banner_flag_{right,left}`。shulker/pot/conduit/end cube/witch potion 与其它 special 未改。
- **修复**：`ItemInHandMesher.EmitHeldSpecial` 泛化为按 special 类型发射：kind = trident/shield/chest/head/banner/banner_flag，每类左右手各一个几何槽；帧与 T2 相同（`specialLocal · display · stab · anchor · handFrame · entityMatrix`）。`specialLocal` 改为从 item JSON `transformation` 读取：`ItemModelResolution.LocalTransform` 在 condition/select/range 分支上按 row-vector 组合传播（修复了此前 special 局部变换在 condition/select 内层丢失的问题），不再硬编码 trident 常量；chest identity、skull `T(0.5,0,0.5)`、banner `T(0.5,0,0.5)·S(0.6667,-0.6667,-0.6667)` 均来自源 item JSON。display 仍取 base item model（`item/chest`、`template_skull`、`template_banner`）；纹理走 `ResolveHeldItemSpecialTexture`（chest `entity/chest/*`、skull 按 `kind`、banner `entity/banner_base`），并纳入 `ResolveTextures` 的 atlas 预热。banner 的 flag plane 用同一帧/纹理作为相邻几何槽发射；banner pattern overlay 仍留在 resolver（见 R-045）。
- **几何**：新增 `SpecialItemModelDefinition.ChestRaw`/`SkullRaw`（仅 held 链使用的 raw 源帧变体，baseline 0）；item-entity 的 `Chest`/`Skull` 保持原 authored 语义与 extents。删除 `HeldChest*`/`HeldSkull*` 伪 arm pivot 模型与 `BuildChestMesh(held)`/`BuildSkullMesh(held)` 分支；resolver 删除 `AddSpecialHeadLayers`、`AddSpecialChestLayers` 及 banner body/flag 的 `AddSpecialPair`。
- **样本 / 验证**：复制 `chest-held-world.reference.txt`（CHEST_RIGHT/LEFT，每手 72 顶点）、`skull-held-world.reference.txt`（SKULL_RIGHT/LEFT，每手 24）、`banner-held-world.reference.txt`（BANNER_RIGHT/LEFT，每手 48 body 顶点）入仓；`HeldSpecialWorldReferenceSampleTests` 新增 6 个 keyed case（RIGHT/LEFT），production 逐 key 位置 1e-5 + UV half 1e-5，非 foil 无 glint。banner flag 无 world 样本，仅结构同帧（R-045）；banner patterns 仍 resolver。
- **未改**：shulker/pot/conduit/end cube、其它 special renderer、witch potion、普通 held item。

### Issue #246 / R-047：清理 EquipmentModelParts legacy 装备重载

- **审计**：全仓（src/tests/tools/docs）静态核对 `EquipmentModelParts.For(kind)` 与 `EntityModelLayerHelpers.WithEquipment` 两个 legacy 重载（L617 `CompiledEntityModel`/kind 版、L757 `IReadOnlyList<EntityModelPart>` flat 版）的调用者：唯一生产 `WithEquipment` 调用（`EntityModelResolver.cs:479`）使用现代重载 L580 `WithEquipment(CompiledEntityModel model, CompiledEntityModel equipmentModel, ...)`；L617/L757 除定义自身外**零调用者**，`EquipmentModelParts` 消费者仅这两个死重载 + `EquipmentModelPartsTests`。与 R-047 登记结论一致。
- **删除**：`EntityModelLayerHelpers` 的 L617/L757 两 legacy 重载；`src/Sharpcraft.Client/Render/EquipmentModelParts.cs`（DSL 数据 + flat bake，纯测试专用数据不留游戏程序集）。更新 class-map baseline（删该文件条目）。
- **测试迁移**：`EquipmentModelPartsTests` 从逐字段 flat `AssertPartsMatch` 改为 `EquipmentModelTrees.For(kind)` 编译树结构断言（horse/camel/nautilus-armor/nautilus-saddle/happy-ghast 五类装备树含预期 box）；严格 jar 源等效仍由 `MountEquipmentRawReferenceSampleTests`（equine/camel/nautilus-saddle/llama/happy-ghast）与 `MountArmorRawReferenceSampleTests`（nautilus-armor）keyed 1e-5+UV half 覆盖。
- **验证**：build 0 error/0 warning；三门禁绿（含 class-map baseline 更新）；受影响测试 + keyed 样本 63/63 harness 全绿；diff 干净。R-047 风险登记标记为已解决。
