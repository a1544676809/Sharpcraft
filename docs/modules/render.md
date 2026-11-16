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
- 旗帜 phase 固定为 0（`FlagPhaseZeroRotationX`）；`DyeColor.getTextureDiffuseColor()` 的 RGB **只作为 flag 的 tint**，pole/bar body 保持白色（源 `submitModel` 两者 diff=-1，底色 tint 属图案层）。
- `BlockEntityRenderState.BannerAttachment/BannerRotation/BannerColor` 由 `FrameCommand` 从方块状态读取；`Handles(20)`；banner 使用 solid pipeline。
- 未实现图案层与 phase 动画，分别由 `SIM-BE-BANNER-PATTERNS` / `SIM-BE-BANNER-PHASE` 跟踪；`ChestAtlas.Textures` 维持 chest-only 22 条，banner 纹理单独加入 block entity sheet。

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
- **验收证据**：`tests/Sharpcraft.Client.Tests/Render/DecoratedPotMesherGoldenTests.cs` 覆盖脖子两盒与 top/bottom 平面 world bounds、四个侧面在四个水平朝向下的位置/法线/UV、6 组 `FacingDegrees` 真值、单 pot 72 顶点 / 108 索引、双 pot 批量偏移、32/16 纹理归一化与注册表 `Handles(41)`；接入图案后需补 sherds 选择真值与单方块像素对比。

<!-- simplification:SIM-BE-POT-NO-PATTERNS task:T-47 -->

### SIM-BE-POT-NO-WOBBLE：饰纹陶罐不播放 wobble 动画

- **简化了什么**：`DecoratedPotMesher` 只输出静态几何；原版 `DecoratedPotRenderer.submit` 根据 `DecoratedPotBlockEntity` 的 `wobbleProgress` 与 `wobbleStyle` 对模型施加 POSITIVE 的 X/Z 小幅倾斜或 NEGATIVE 的 Y 旋转衰减，本实现未接入。
- **为什么**：wobble 状态来自方块实体逐刻动画（`wobbleProgress` / `wobbleStyle`），当前 `BlockEntityRenderState` 没有对应的动画字段与数据通道。
- **对等效性的影响**：静止外观一致；被玩家或投掷物击中后的陶罐晃动动画缺失。
- **状态**：受控偏差，计划由 T-47（#47 批次 1）后续动画切片跟进。
- **验收证据**：`tests/Sharpcraft.Client.Tests/Render/DecoratedPotMesherGoldenTests.cs` 覆盖静态几何与朝向；接入 wobble 后需补 `wobbleProgress` / `wobbleStyle` 真值向量与单方块像素对比。

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

### SIM-BE-BANNER-PATTERNS：Banner 只画底色旗面

- **简化了什么**：`BannerRenderer` 只绘制 `BannerModel` 的 pole/bar 与 `BannerFlagModel` 的旗帜底色；不解析 `patterns` NBT，也不叠加 `BANNER_PATTERN_BASE` 或各图案图层。
- **为什么**：本切片先落地 standing/wall 静态模型、transform、tint 与 atlas；图案层需要 `BannerBlockEntity` 的 pattern 数据与对应 sprite/颜色叠加通道。
- **对等效性的影响**：无图案的 banner 底色、杆/横梁与旗帜几何按源实现；带图案的 banner 缺少图案视觉。
- **状态**：受控偏差，由 #49 后续 pattern 切片跟进（Issue #89）。
- **验收证据**：`tests/Sharpcraft.Client.Tests/Render/BannerRendererTests.cs` 覆盖 ground/wall 最终顶点/UV、tint 与 atlas；接入 pattern 后补真值与单方块像素对比。

<!-- simplification:SIM-BE-BANNER-PATTERNS task:T-49 -->

### SIM-BE-BANNER-PHASE：Banner 旗帜 phase 固定为 0

- **简化了什么**：旗帜模型使用 `BannerFlagModel.setupAnim(0)` 的固定 X 旋转；不读取 `BannerRenderer` 由世界时间与方块坐标计算的 phase，也没有波动动画。
- **为什么**：phase 依赖 world game time 与相机 partial tick，当前 `BlockEntityRenderState` 没有该动画字段；首切片先固定静态姿态。
- **对等效性的影响**：旗帜静态几何与 phase=0 的源姿态一致；没有随时间摆动的动画。
- **状态**：受控偏差，由 #49 后续动画切片跟进（Issue #89）。
- **验收证据**：`BannerRendererTests` 覆盖 phase=0 的最终顶点/UV；接入 phase 后补时间序列真值与单方块像素对比。

<!-- simplification:SIM-BE-BANNER-PHASE task:T-49 -->




### SIM-BE-SKULL-VARIANTS：Skull 头型无剩余变体

- **简化了什么**：`SkullRenderer` 覆盖 `SKELETON` / `WITHER_SKELETON` / `ZOMBIE` / `CREEPER` / `PLAYER` / `PIGLIN` / `DRAGON` 的静态头模型。
- **为什么**：本切片已补齐所有已知的 skull 头型；后续只保留动画/皮肤缓存等独立简化。
- **对等效性的影响**：七种支持头型的方块头/墙头静态外观按源模型；无剩余因头型缺失导致的不可见分支。
- **状态**：已验证，无剩余变体（Issue #75、#77、#79、#81）。
- **验收证据**：`tests/Sharpcraft.Client.Tests/Render/SkullRendererTests.cs` 覆盖七种头型的 ground/wall、Player head+hat、Piglin head 与 Dragon 7-box 最终顶点/UV。

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