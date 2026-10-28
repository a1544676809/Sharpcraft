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
| 真实世界 | 9/9 区块 → 275,142 方块 / 110,868 三角形，3×3 区块岛屿 | `launcher frame mcf.ddger.top` 现场运行 |
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

### 5.2 光照与顶点亮度

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

- 无纹理：颜色由方块状态 id 哈希得到，稳定但不代表材质。
- 无光照传播：只消费服务端下发的光照数组，客户端不计算光照传播（挖方块后不会实时变亮/变暗）。
- 无环境光遮蔽与平滑光照：面亮度只按朝向与所在格光照，顶点之间不做插值。
- 窗口只有呈现：无输入系统；帧循环是"呈现 N 帧 / 到点关闭"，尚未接入正式主循环。
- resize 已支持（交换链 + 深度 + 顶点一起重建），但只做了被动跟随，没有最小尺寸、纵横比锁定等策略。
- 每帧重建设备与管线（未做资源复用）。
- 网格未合批优化：每个非空气方块最多 6 个四边形，未做贪婪合并。

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
| 集成 | `launcher frame local` | 本地合成世界出图（不依赖网络） |
| 集成 | `launcher frame replay <file>` | 用已捕获的真实区块包回放，验证"真实数据能画出来" |
| 集成 | `launcher window <address>` / `window local` | 窗口呈现真实世界（180 帧，BGRA8UnormSrgb）并落盘 PNG |
| 集成 | `MC_WINDOW_SELFRESIZE=640x360 launcher window local` | 第 30 帧时窗口自缩到 640×360，验证交换链/深度/顶点三者一起重建 |
