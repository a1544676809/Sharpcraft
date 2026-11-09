<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

<!-- doc
title: 原版架构模式采纳缺口（26.3 基线）
summary: 逐条比对 26.3 中被反复使用、而我们尚未采纳的 10 个结构性约定，给出 ref 证据行号、本仓对照与最小落地路径。
status: 草稿
updated: 2026-11-09
-->

# 原版架构模式采纳缺口

基线：Minecraft 26.3（protocol 777）。本仓对照分支：`feature/item-registry`。

## 0. 判据与写法约定

1. 先打开原版文件确认用法，再数使用点（`Select-String` 全库计数），最后才称「模式」。
2. **模式门槛**：同一结构性约定在原版必须 **≥2 处独立使用**；只有一处用法的能力单列为「单例」（§12），不参与排序。
3. 本仓对照一律指向具体文件与行；「部分实现」必须写明缺的是哪一项能力，不写「基本对齐」这类无法核查的结论。
4. **证据指针写法**：完整形式为 `ref://mc-26.3-analysis/src/` 加包路径、加 `:<行号或行区间>`，其中包路径以 `net/minecraft/` 或 `com/mojang/` 起头。摘要表（§1）的每一格都写完整形式；正文为了可读，**每个文件的首次引用写完整形式**，同一小节内后续行号写 `<文件名>:<行>`，视为同一个 `ref://` 路径的续引。
5. 影响分级只看三件事：是否改变可观测行为、是否落在每帧/大批量路径上、是否阻塞原则 11（着色器包）或原则 2（模组扩展点）。

## 1. 摘要表

| 模式 | 原版证据（ref + 行） | 本仓现状 | 缺失能力 | 影响 | 优先级 |
| --- | --- | --- | --- | --- | --- |
| P1 纹理对象三段式家族 | `ref://mc-26.3-analysis/src/net/minecraft/client/renderer/texture/AbstractTexture.java:11-53`、`ref://mc-26.3-analysis/src/net/minecraft/client/renderer/texture/ReloadableTexture.java:13-46`、`ref://mc-26.3-analysis/src/net/minecraft/client/renderer/texture/TickableTexture.java:1-5`、`ref://mc-26.3-analysis/src/net/minecraft/client/renderer/texture/Dumpable.java:1-9`；9 个类型 / 9 个文件 | 4 个互不相关的持有者，各自「存图 + 换图」 | 抽象基类、自带 sampler、未初始化 fail-fast、元数据驱动采样模式、mipmap 层级、正交能力接口 | 采样状态无法按纹理区分，wrap 只能靠几何补偿 | **高** |
| P2 采样器 / 管线状态去重缓存 | `ref://mc-26.3-analysis/src/com/mojang/blaze3d/systems/SamplerCache.java:10-76`（29 处使用）、`ref://mc-26.3-analysis/src/com/mojang/blaze3d/pipeline/PipelineCache.java:13-52` | 6 处手写 `CreateSampler`（2 处在绘制路径内现建现毁）；27 处 `CreateRenderPipeline` 无缓存 | 键化缓存、描述符等价性、回收时机 | 每帧原生对象分配；同一状态多份实例 | **高** |
| P3 不可变渲染状态对象 + 记忆化工厂 | `ref://mc-26.3-analysis/src/net/minecraft/client/renderer/rendertype/RenderType.java:17-123`、`ref://mc-26.3-analysis/src/net/minecraft/client/renderer/rendertype/RenderSetup.java:22-110`、`ref://mc-26.3-analysis/src/net/minecraft/client/renderer/rendertype/PreparedRenderType.java:17-56`、`ref://mc-26.3-analysis/src/net/minecraft/util/Util.java:1054-1084`（48 处使用） | `Shaders/ShaderPipelines.cs:17-31` 只是「着色器文件清单」；管线在渲染器内联创建 | 固定功能状态描述符、按描述符取编译产物、可被外部包覆盖的状态声明 | 无法缓存编译；原则 11 的稳定契约层缺载体 | **高** |
| P4 两阶段重载 + 顺序链 + 全有或全无 | `ref://mc-26.3-analysis/src/net/minecraft/server/packs/resources/PreparableReloadListener.java:10-52`、`ref://mc-26.3-analysis/src/net/minecraft/server/packs/resources/SimpleReloadInstance.java:46-111`、`ref://mc-26.3-analysis/src/net/minecraft/server/packs/resources/ReloadableResourceManager.java:35-46`；25 个实现者 | `Sharpcraft.Assets/ResourceManager.cs:46-112` 有「有序监听器 + 整栈原子替换」 | prepare/apply 分离、后台准备、跨监听器共享状态、失败上抛、进度 | 同步单阶段；监听器异常被吞，界面可能半更新 | 中 |
| P5 烘焙分层 + 共享操作缓存 + 驻留表 + missing 回退 | `ref://mc-26.3-analysis/src/net/minecraft/client/resources/model/ModelBakery.java:93-256`、`ref://mc-26.3-analysis/src/net/minecraft/client/resources/model/ModelBaker.java:9-30`、`ref://mc-26.3-analysis/src/net/minecraft/client/resources/model/UnbakedModel.java:9-34`、`ref://mc-26.3-analysis/src/net/minecraft/client/resources/model/ResolvableModel.java:5-11`（28 处） | `VanillaMeshDefinition` / `VanillaMeshBakery` / `VanillaMeshBake`（新旧两套并存） | 阶段化接口、按操作键去重、向量与材质驻留、missing 结构预烘焙复用 | 重复烘焙；迁移期双轨（与原则 14 冲突） | 低 |
| P6 图集 / 精灵：mip 推导 + padding 派生 + 每精灵元数据 + 动画 | `ref://mc-26.3-analysis/src/net/minecraft/client/renderer/texture/SpriteLoader.java:46-106`、`ref://mc-26.3-analysis/src/net/minecraft/client/renderer/texture/Stitcher.java:24-43`、`ref://mc-26.3-analysis/src/net/minecraft/client/renderer/texture/SpriteContents.java:63-165`、`ref://mc-26.3-analysis/src/net/minecraft/client/renderer/texture/MipmapGenerator.java:99-153` | `Client/Assets/AtlasMipChain.cs:19`（固定 4 级）、`SpriteAtlas.cs:116`（`Padding = 2`） | mip 级别按最小精灵推导、padding 随 mip/各向异性派生、`MipmapStrategy` 五档、alpha-to-coverage 归一 | 小精灵被迫降 mip 无告警；cutout 纹理远处发灰 | 中 |
| P7 SynchedEntityData：访问器 + 序列化器 + 继承链 id 派生 + 脏集合 | `ref://mc-26.3-analysis/src/net/minecraft/network/syncher/SynchedEntityData.java:16-130`、`ref://mc-26.3-analysis/src/net/minecraft/util/ClassTreeIdRegistry.java:6-37`、`ref://mc-26.3-analysis/src/net/minecraft/network/syncher/EntityDataSerializers.java:54-90`；`defineId` 231 处 | `Common/Network/EntityDataPackets.cs:42-346` 手写 serializer 分派；`Client/World/EntityTracker.cs:1113-1160` 硬编码索引与掩码 | 访问器对象、写时脏标记、继承链自动分配 id、类型安全取值 | 每种实体重复记录「索引 + 掩码」知识，错了无告警 | 中 |
| P8 BlockState 属性表 + 邻居转移表 + 声明式编码 | `ref://mc-26.3-analysis/src/net/minecraft/world/level/block/state/StateHolder.java:16-133`、`ref://mc-26.3-analysis/src/net/minecraft/world/level/block/state/StateDefinition.java:36-133`、`ref://mc-26.3-analysis/src/net/minecraft/world/level/block/state/BlockState.java:10-16` | `Common/Registries/BlockStateLayout.cs:17-92`、`BlockStateDefinition.cs:17-83`、`BlockStateProperty.cs:14-33` | 不可变状态对象、类型化属性、预计算 `neighbors[p][v]`、codec | 取属性要线性查字符串；无常数时间状态转移 | 中 |
| P9 注册表 + 声明式序列化 + 数据包驱动的注册表加载 | `ref://mc-26.3-analysis/src/net/minecraft/resources/RegistryDataLoader.java:98-197,199-268,283-305`；1526 处 `Codec` / `MapCodec` 声明 | `Common/Registries/Registry.cs:22-102`（有序 + 冻结）、`IdMapper.cs:15-80` | codec 组合、按注册表声明的加载管线、错误聚合 | 每加一个数据驱动结构就手写一遍解析与错误处理 | 低 |
| P10 实体渲染器注册表 + provider 工厂 + 覆盖校验 | `ref://mc-26.3-analysis/src/net/minecraft/client/renderer/entity/EntityRenderers.java:21-60`、`ref://mc-26.3-analysis/src/net/minecraft/client/renderer/entity/EntityRenderDispatcher.java:42-113`、`ref://mc-26.3-analysis/src/net/minecraft/client/model/geom/builders/LayerDefinition.java:5-25`；`EntityRendererProvider` 183 处 / 158 文件 | `Client/Render/EntityModelResolver.cs`（1231 行、3 个静态可变属性、按 id 分派）、`EntityRenderLayerDefinition.cs:13-22` | provider 注册表、按类型取渲染器、启动期「每类型都有渲染器」校验 | 缺失渲染器静默退化占位；静态可变态阻塞热卸载 | 低 |

## 2. P1 纹理对象三段式家族（T-784 已登记）

**原版结构。** 基类 `ref://mc-26.3-analysis/src/net/minecraft/client/renderer/texture/AbstractTexture.java:11-53` 是一个 `abstract … implements AutoCloseable`，持 `texture` / `textureView` / `sampler` 三个可空字段：`close()`（第 17-20 行）收敛为 `releaseTextures()`，后者（第 22-32 行）做「有则关、关则置空」；两个 getter（第 34-48 行）在未初始化时抛 `IllegalStateException`，即**「没初始化就不许取」是被契约强制的**；第 14-15 行在字段初始值里就向全局 `SamplerCache` 取默认 sampler，即**采样状态由纹理自己携带**。`ref://mc-26.3-analysis/src/net/minecraft/client/renderer/texture/ReloadableTexture.java:13-46` 追加「资源 id + 取内容 + 应用内容」：第 32-36 行把 `.mcmeta` 的 `clamp()` / `blur()` 翻译成 address mode 与 filter mode 再换 sampler；第 38-44 行先 `close()` 旧资源再建新的（mip 层级上限 5 写死）。能力变体：`ref://mc-26.3-analysis/src/net/minecraft/client/renderer/texture/MipmappedTexture.java:29-59` 覆盖 sampler（开 mip、min 固定 LINEAR）并按最小边与最低置位推导级数；`ref://mc-26.3-analysis/src/net/minecraft/client/renderer/texture/SimpleTexture.java:7-16` 只实现内容加载；另有 `CubeMapTexture`。直接派生基类的有 `DynamicTexture`、`TextureAtlas`（第 36 行同时 `implements TickableTexture, Dumpable`）、`FontTexture`（第 21 行）、`PalettedTextureManager` 的内部类（第 190 行）。正交能力用只有一两个方法的窄接口表达（`TickableTexture` 只有 `tick()`，`Dumpable` 只有 `dumpContents`），共 4 处实现，不进继承链。底层资源本身是接口：`GpuTexture`、`GpuTextureView`、`GpuSampler`。

**本仓对照。** 我们有 4 个互不相关的持有者。`src/Sharpcraft.Framework/Rendering/AtlasTextureSource.cs:15-55` 与 `UiTextureSource.cs:15-72` 是**CPU 侧的「图像 + 版本号」容器**：换图只 bump 一个 `int Version`，让渲染器发现后重传；既不持 GPU 句柄，也不持 sampler。`src/Sharpcraft.Framework/Rendering/EntitySkinTextureGpu.cs:15` 自己 `: IDisposable` 管 GPU 资源（第 45 行 `MipLevelCount = 1`）。`src/Sharpcraft.Client/Assets/CloudTexture.cs:13` 又是一种。GPU 纹理与 view 的创建散在 `WindowedRenderer.cs`（8 处）与 `OffscreenRenderer.cs`（4 处）。缺的正是 T-784 已登记的五项中的后四项：无抽象基类（无共享生命周期与 fail-fast）、sampler 外置、无 `.mcmeta` 的 `texture` 段解析、无 mip 层级。fail-fast 目前是各处临时写法——`src/Sharpcraft.Framework/Rendering/FramePresentation.cs:59` 与 `:66` 抛 `InvalidOperationException("the frame target was not created")`，语义对但不是契约，新写的持有者可以不做。

**缺口后果。** 采样状态成了绘制时的参数而不再是纹理的属性，于是「这张要 clamp、那张要 repeat」无法表达；P3 里 `RenderSetup` 那种「采样器覆盖」也无处安放，只能靠 UV 切分 / 几何补偿（T-783 的动机）。资源替换没有「先释放再建」的确定顺序，泄漏与重复释放全靠调用方自律。

**最小落地路径。** ① 在 `Sharpcraft.Framework.Rendering` 加抽象基类：三个可空字段 + `Release()` + 两个 throw 的 getter；② 先只迁 3 个已有持有者（atlas、UI、皮肤），新能力用派生类表达（从资源重载 / 动态写回）；③ `.mcmeta` 的 `texture` 段解析放 `Sharpcraft.Client/Assets`，只产出 `(clamp, blur)` 两个布尔给派生类选 sampler；④ tick / dump 用接口后补。

## 3. P2 采样器与管线状态去重缓存

**原版结构。** `ref://mc-26.3-analysis/src/com/mojang/blaze3d/systems/SamplerCache.java:10-76` 在 `initialize()`（第 13-32 行）里把 address U × V × filter min × mag × mip 开关的**全部组合**一次建成 `GpuSampler[32]`，键由第 63-76 行的位编码给出（4 个枚举各占 1 位 + mip 标志占 bit4）；第 15 行显式校验枚举规模仍是 2，扩枚举即硬失败而不是静默错位。`getSampler`（第 34-38 行）退化为数组下标，第 40-54 行再套 4 个常用组合的便捷重载。使用面 29 处：纹理基类默认值（`AbstractTexture.java:14-15`）、元数据切换（`ReloadableTexture.java:35`、`MipmappedTexture.java:33`）、图集（`TextureAtlas.java:82` 与 `:151`）、状态准备（`RenderSetup.java:94` 与 `:98`）、`RenderTypes.java:534`，以及 `CloudRenderer` / `GameRenderer` / `LevelRenderer` / `OitRenderPassProvider` 等一批绘制入口。`ref://mc-26.3-analysis/src/com/mojang/blaze3d/pipeline/PipelineCache.java:13-52` 是同一思路在管线上的版本：`Reference2ReferenceOpenHashMap<RenderPipeline, CompiledRenderPipeline>`（第 16 行）按**不可变描述符对象**做键，miss 时第 33 行后台编译并缓存，第 42-45 行 `clear()` 随重载整体释放；`RenderSystem.java:82-113` 维护 current / fallback 两个缓存，第 231-236 行在关闭时逐个 `close()`。

**本仓对照。** 没有任何键化缓存。`CreateSampler` 共 6 处、全是字面量描述符：`src/Sharpcraft.Framework/Rendering/WindowedRenderer.cs:162-173`（**在绘制路径里 `using var`**）、同文件 `:321`、`OffscreenRenderer.cs:556`、同文件 `:1158`、`FramePresentation.cs:40-51`、`WorldTextResources.cs:53`。`CreateRenderPipeline` 共 27 处（`WindowedRenderer` 15、`OffscreenRenderer` 6、`UiRenderer` 3，`FramePresentation` / `SkyDiscResources` / `WorldTextResources` 各 1），全部内联，没有任何地方记录「这个管线是否已建过」。

**缺口后果。** 绘制路径内现建现毁原生对象，使原则 9 的「稳态零分配」在采样器这一项上直接不成立；同一组状态在不同文件里复制多份，改一处漏一处时表现为难以定位的深度/混合差异；重载时也没有统一「把编译产物全部释放」的位置。

**最小落地路径。** ① 先做 sampler 表：`readonly record struct SamplerKey(AddressMode U, AddressMode V, FilterMode Min, FilterMode Mag, bool Mipmaps)` 为键，容量 32 的数组或字典，随设备生命周期创建/释放；把 6 处改成查表，删掉绘制路径里的 `using`。② 再做管线表：把散落的 `CreateRenderPipeline` 参数抽成不可变记录，用 `Dictionary<记录, RenderPipeline>` 缓存，重载时整体释放并清空。

## 4. P3 不可变渲染状态对象 + 记忆化工厂

**原版结构。** 一套状态被拆成三层。`ref://mc-26.3-analysis/src/net/minecraft/client/renderer/rendertype/RenderSetup.java:22-110` 是 `final` 类、十余个 final 字段（第 25-34 行）、用 builder 装配（第 129-225 行），第 85-110 行 `prepareTextures` 把「纹理名 → (view, sampler)」解析成列表：**不可变描述**。`ref://mc-26.3-analysis/src/net/minecraft/client/renderer/rendertype/RenderType.java:17-35` 在构造时就把派生量算完——outline 目标（第 30-33 行）与 `hasBlending`（第 50-58 行由 pipeline 的混合状态推出）；第 60-77 行 `prepare()` 只叠加「此刻」的动态量（modelview、scissor、纹理）。`ref://mc-26.3-analysis/src/net/minecraft/client/renderer/rendertype/PreparedRenderType.java:17-56` 是纯快照 record，`draw`（第 37-56 行）只做绑定与 `drawIndexed`，不再判断任何状态。**按纹理缓存**由 `ref://mc-26.3-analysis/src/net/minecraft/util/Util.java:1054-1084` 提供：单参版（第 1054-1068 行）把任意函数包进 `ConcurrentHashMap` + `computeIfAbsent`，二元版（第 1070-1084 行）以 `Pair` 为键；`RenderTypes.java` 里 36 个工厂全部经它包过（`(纹理 id, 是否剔除)` → 实例），全库 48 处、11 个文件在用。

**本仓对照。** `src/Sharpcraft.Framework/Rendering/Shaders/ShaderPipelines.cs:17-31` 是一张「名字 → 文件 → 入口点 → binding 号」的清单，第 36-56 行只做「着色器源码声明的 binding/entry 与清单是否一致」的校验（`Shaders/ShaderDescriptor.cs:78-104`）。它回答「有哪些 shader 文件」，**不回答「一次绘制要哪些固定功能状态」**：混合、深度写/比较、顶点布局、拓扑、目标格式都不在描述符里，而是直接写在 27 处 `CreateRenderPipeline` 的实参中。也没有 `RenderType` 那种「构造时算完派生量、prepare 只叠动态量」的两段式——是否需要排序、是否影响轮廓、是否走 OIT 这些派生判断散在调用方。

**缺口后果。** （a）原则 11 要求「着色器包 ↔ 引擎管线的稳定契约层，新增 pass 只需包侧描述」；现在 pass 的定义散在渲染器代码里，包既无法描述也无法覆盖。（b）没有描述符就没有键，P2 的管线缓存无从落地。（c）派生量被重复计算或重复判断，是每帧路径上的常驻开销。

**最小落地路径。** ① 定义不可变 `RenderPassState` 记录（pipeline 引用 + 纹理槽声明 + lightmap/overlay 开关 + 混合/深度/拓扑 + 轮廓与排序标记），只增不改；② 每个已有 pass 改为「静态构造一次 → 进清单」，清单即原则 11 的契约层；③ `prepare` 只叠加每帧动态量并产出快照记录交给绘制层；④ 复用先做 `Util.memoize` 的等价工具（`ConcurrentDictionary` + `GetOrAdd`）。

## 5. P4 两阶段重载、顺序链与全有或全无

**原版结构。** `ref://mc-26.3-analysis/src/net/minecraft/server/packs/resources/PreparableReloadListener.java:10-52` 只有一个方法 `reload(sharedState, taskExecutor, preparationBarrier, reloadExecutor)`，语义全由参数表达：准备丢给 `taskExecutor`（后台），`preparationBarrier.wait(值)` 表示「我准备完了，但请等全体准备完再应用」，应用在 `reloadExecutor`（主线程）。第 18-19 行 `prepareSharedState` 允许在任何准备开始前把跨监听器中间结果写进共享状态；第 30-49 行 `SharedState` 用 `IdentityHashMap<StateKey<?>, Object>` 存，第 51-52 行 `StateKey` 是不带内容的类型化钥匙——共享是**按类型键控、编译期配平**，不是字符串键。`ref://mc-26.3-analysis/src/net/minecraft/server/packs/resources/SimpleReloadInstance.java:46-94` 把监听器串成链：每个监听器的 barrier 与「全体准备完成」`allPreparations`、以及第 N-1 个监听器的完成信号做 `thenCombine`（第 96-111 行），于是**准备可并行、应用按注册顺序串行**；第 93 行 `sequenceFailFast` 决定任一失败即整体失败。`ResourceManagerReloadListener`（第 9-26 行）与 `SimplePreparableReloadListener`（第 8-24 行）是两个免费适配层。`ref://mc-26.3-analysis/src/net/minecraft/server/packs/resources/ReloadableResourceManager.java:35-46` 只负责「登记监听器 + 换栈 + 起一次 reload」。全库 25 个实现者（`TextureManager`、`ModelManager`、`AtlasManager`、`ShaderManager`、`SoundManager`、`FontManager`、`LanguageManager` 等）。

**本仓对照。** `src/Sharpcraft.Assets/ResourceManager.cs:46-51` 的 `AddListener(id, order, Action<ResourceManager>)` 已对齐两点：有序（第 97 行 `OrderBy(Order)`）与整栈原子替换（第 86-95 行先建新栈、锁内换引用、再 `Dispose` 旧的，注释第 57-61 行明确说这等价于原版的 all-or-nothing）。差四点：① **单阶段同步**——准备与应用混在同一个 `Action` 里，既无 `prepare`/`apply` 之分也无 `taskExecutor`；② 无跨监听器共享状态；③ **异常被吞掉**（第 99-108 行刻意 `catch (Exception)` 只打一行日志，与 `sequenceFailFast` 语义相反）；④ 无进度。当前 3 处注册且顺序依赖真实存在：`src/Sharpcraft.Client/Game/WindowCommand.cs:333`（language，order 10）、`:386`（font，20）、`:162`（atlas，30）。

**缺口后果。** 吞异常是有意为之（注释写明「半更新的界面比一张过期的图集更糟」），代价是**任何监听器失败都不可观测**，而这正是原则 15 要防的「未登记简化」的滋生地：一次失败重载留下的半新半旧状态没有任何信号。重载又在主线程同步跑，pack 变多就体现为掉帧。

**最小落地路径。** ① `AddListener` 拆成 `Prepare(manager)`（可后台）与 `Apply(prepared)`（主线程）两个委托，保留现有签名作便捷重载；② 加按类型键的最小共享上下文，让 font 拿到 language 的产物；③ 把「吞异常」改成「收集全部失败 → 整体失败 → 保持旧栈 → 把失败列表交回调用方」，同时满足原则 15 的可诊断要求。

## 6. P5 烘焙分层、共享操作缓存与驻留表

**原版结构。** 三层接口：`ref://mc-26.3-analysis/src/net/minecraft/client/resources/model/UnbakedModel.java:9-34` 全是 default 方法，描述「声明的形状」；`ref://mc-26.3-analysis/src/net/minecraft/client/resources/model/ResolvableModel.java:5-11` 只有 `resolveDependencies(Resolver)`，让模型声明自己依赖谁（全库 28 处）；烘焙产物是 `SimpleModelWrapper` / `BlockStateModel`。`ref://mc-26.3-analysis/src/net/minecraft/client/resources/model/ModelBakery.java:93-138` 先烘焙一套 missing 模型（第 95 行）、再建 `ModelBakerImpl`，然后用 `ParallelMapTransform.schedule` 并行烘焙方块状态与物品模型，**单条失败只告警返回 null**（第 99-105、118-121 行）——与 P4 相反，因为对象多、不能一错全废。`ModelBaker.Interner`（`ModelBaker.java:20-24`）对 `Vector3fc` 与 `BakedQuad.MaterialInfo` 做**强驻留**，实现见 `ModelBakery.java:151-164`。`ModelBakery.java:217-218,253-255` 是 `ConcurrentHashMap<SharedOperationKey<Object>, Object>` + `computeIfAbsent`：把「一次可复用的子烘焙」包成键（`ModelBaker.SharedOperationKey` 是函数式接口），相同键只算一次。`ModelBakery.java:166-211` 把 missing 模型做成一次性烘焙、全局共享的结构，缺资源时所有调用点退到它。

**本仓对照。** 我们有 `src/Sharpcraft.Client/Render/VanillaMeshDefinition.cs`（声明树）、`VanillaMeshBakery.cs`（保留树结构烘焙）、`VanillaMeshBake.cs`（旧的扁平烘焙），运行时结构是 `CompiledEntityModel` / `EntityModelPartNode` / `EntityModelCube`。`VanillaMeshBakery.cs:8-14` 的文档注释自认「legacy one-cube-per-part `VanillaMeshBake.Bake` 路径 remains only for existing callers until migration completes」——**双轨并存**，与原则 14「严禁双轨分叉」直接冲突。缺：按操作键去重、向量/材质驻留、预烘焙共享的 missing 结构（现在是 `EntityPlaceholderMesher.cs` / `BlockEntityPlaceholderMesher.cs` 两条占位路径）。

**缺口后果。** 六十余个 `XxxModelDefinition.cs` 各自烘焙，重复的立方体向量与材质信息无共享（内存与 GC 压力，原则 9）；双轨意味着任何几何修正都要改两处，必然漂移。

**最小落地路径。** ① 先把 `VanillaMeshBake` 的调用点全部迁到 `VanillaMeshBakery` 并删除旧路径（优先于引入缓存）；② 加按 `(定义, 纹理尺寸)` 键的烘焙结果字典；③ 立方体向量按值驻留（`Dictionary<Vector3, Vector3>` 即可，不必引第三方）；④ missing 模型做成一条预烘焙共享实例链。

## 7. P6 图集与精灵：mip 推导、padding、每精灵元数据、动画

**原版结构。** `ref://mc-26.3-analysis/src/net/minecraft/client/renderer/texture/SpriteLoader.java:46-106` 先扫一遍精灵，用「最小边长」与「最小边的最低置位」联合推导**全局可用 mip 级别**（第 49-75 行），某精灵把级别拉低时在第 56-61、68-70 行明确告警——即「mip 级别由最小精灵决定，且降级必须可见」。padding 不是常数：`ref://mc-26.3-analysis/src/net/minecraft/client/renderer/texture/Stitcher.java:24-43` 的 `padding = 1 << mipLevel << clamp(anisotropyBit - 1, 0, 4)`，第 42-43 行按「精灵尺寸 + padding×2」算 slot，故 padding 随 mip 与各向异性联动。每精灵内容在 `ref://mc-26.3-analysis/src/net/minecraft/client/renderer/texture/SpriteContents.java:63-165`：第 63-84 行从 `TextureMetadataSection` 读 `mipmapStrategy` 与 `alphaCutoffBias`（缺省 `AUTO` / 0），第 86-99 行 `increaseMipLevel` 失败时构造带上下文（精灵名、尺寸、帧数、mip 级数、原图尺寸）的崩溃报告，第 113-165 行从动画元数据建帧表并对「时长 0」「索引越界」「有帧未被引用」分别告警后剔除。`ref://mc-26.3-analysis/src/net/minecraft/client/renderer/texture/MipmapGenerator.java:99-153` 把 `AUTO` 解析成「含透明像素 → CUTOUT，否则 MEAN」，cutout 参考 alpha 取 0.5（`STRICT_CUTOUT` 取 0.3），第 139 行单独处理 `DARK_CUTOUT`，第 153 行做 alpha-to-coverage 归一——**mip 链策略是每精灵元数据驱动**的。`ref://mc-26.3-analysis/src/net/minecraft/client/renderer/texture/TextureContents.java:30-36` 是 `clamp` / `blur` 的唯一读取点。

**本仓对照。** `src/Sharpcraft.Client/Assets/AtlasMipChain.cs:19` 把级数写死 `Levels = 4`；第 28-116 行按「目的 texel 归属哪个精灵」做矩形钳制的 alpha 加权盒式滤波——方法上比原版全局盒式滤波更严防串色（**这是我们的主动改进，应保留**），但缺三样：① 级别不由最小精灵推导（常数 4，小精灵会被采到不存在的细节）；② 无 `MipmapStrategy` 概念；③ 无 alpha-to-coverage 归一。padding 是固定常量 `Padding = 2`（`src/Sharpcraft.Client/Assets/SpriteAtlas.cs:116`，第 109-115 行给出「uv 恰好落在边界、双线性要读两个 texel」的理由），不随 mip / 各向异性变化；顺带记一笔：同文件第 250 行注释仍写「One texel of padding on every side」，与实际值 2 不符，属行内注释漂移（原则 12 的漂移检查覆盖不到注释）。mip 生成位置也不在图集加载处，而在 `src/Sharpcraft.Client/Game/FrameCommand.cs:603-611` 的 `WithMips`，由请求对象携带（`Framework/Rendering/FrameRequest.cs:200-206`）。动画帧完全未实现。

**缺口后果。** 视觉上 cutout 方块（树叶、铁栏杆、草）远处发灰发暗、边缘出现半透明晕；小尺寸精灵在下层 mip 失真。结构上图集的「尺寸决策」与「内容决策」分裂在 `Client/Assets` 与 `Client/Game` 两处，将来加动画无处安放。

**最小落地路径。** ① 把 `Levels` 改为按最小精灵与最低置位推导，降级时告警（几乎零成本，先做）；② 每精灵记录 `MipmapStrategy`，先只实现 `MEAN` / `CUTOUT`，`AUTO` 由「该精灵是否含 alpha < 0.5 的像素」决定；③ cutout 分支复用现有归属钳制，把「平均」换成「按 0.5 二分后再平均颜色」；④ 动画等有 `.mcmeta` 动画需求时再做，但先在类型上留出「精灵内容 + 帧表」的位置。

## 8. P7 SynchedEntityData：访问器、序列化器与继承链 id 派生

**原版结构。** 每个字段是 `EntityDataAccessor<T>`，由 `defineId(所属类, serializer)` 声明（`ref://mc-26.3-analysis/src/net/minecraft/network/syncher/SynchedEntityData.java:29-46`）：id 来自全局 `ClassTreeIdRegistry`，后者（`ref://mc-26.3-analysis/src/net/minecraft/util/ClassTreeIdRegistry.java:32-37`）按**类继承链**分配——`define` 取父类最大 id + 1，`getLastIdFor`（第 10-26 行）沿 superclass 向上找并缓存；因此「`Entity` 占 0-7、`ItemEntity` 从 8 开始」是自动结果而非手工约定，上界 254 在第 41-43 行硬校验。`set`（第 56-68 行）只在值真的变了（或强制）时才写、才回调 `onSyncedDataUpdated`、才置脏；第 74-90 行 `packDirty` 只在有脏项时打包并清标志——**下发的永远是差集**。第 92-106 行 `getNonDefaultValues` 处理「首次全量只发非默认值」。第 118-131 行 `assignValue` 校验收到的 serializer 与本地注册的一致，不一致直接抛并给出字段号与新旧类型。配套的 `ref://mc-26.3-analysis/src/net/minecraft/network/syncher/EntityDataSerializers.java:54-90` 把「类型 → 编解码器」注册成一等对象。全库 `defineId` 231 处（`Entity` 8、`LivingEntity` 7、`ArmorStand` 7、`Panda` 6、`Cat` / `Wolf` 各 5……）。

**本仓对照。** `src/Sharpcraft.Common/Network/EntityDataPackets.cs:42-346` 已能解码 `set_entity_data`，且两点做得好：第 84-97 行以 255 结尾，第 106-110 行遇到未建模 serializer 就**停止扫描并报告不完整**（注释解释了为什么不猜长度），第 47-48 行给了上限。缺的是「访问器模型」：消费侧 `src/Sharpcraft.Client/World/EntityTracker.cs:1113-1160` 用硬编码常量取值——第 1129 行 `ItemEntityItemAccessorIndex`（注释在第 1123-1124 行解释「`Entity` 定义 8 个访问器故 `ItemEntity` 从 8 开始」），以及 `EntityVariantSpecs` 的 `AccessorIndex` / `ValueMask` / `MarkingsShift`（第 1138-1157 行）。也就是说原版用 231 处声明自动推出的知识，我们在消费侧**手工重编码**，且没有任何机制校验它与 serializer 声明一致。

**缺口后果。** 每新增一种同步字段要同时改三处（spec 表、索引常量、掩码），错了不会告警——正是 T-640「整包漏接」的近亲。无脏标记也就无差集下发，将来做集成服务端得重写整条上行路径。

**最小落地路径。** ① 先只做**只读访问器表**：`(实体类型 id, 字段名) → (accessor 索引, serializer id)`，一次集中写清，消费侧改查表、删掉散落常量；② 加启动期一致性校验：收到的 serializer id 与表不符时告警（对应原版第 118-131 行）；③ 脏标记与差集打包留到集成服务端阶段。

## 9. P8 BlockState 属性表、邻居转移表与声明式编码

**原版结构。** 状态是**不可变对象**：`ref://mc-26.3-analysis/src/net/minecraft/world/level/block/state/StateHolder.java:16-30` 持 owner、`Property<?>[] propertyKeys`、`Comparable<?>[] propertyValues` 与一个 `S[][] neighbors`。取值走 `valueIndex`（第 68-76 行，按引用线性找属性下标）；改值走 `setValue`（第 104-125 行），第 118-124 行先用 `property.getInternalIndex(value)` 把值映射成序号，然后**直接查 `neighbors[propertyIndex][valueIndex]` 返回既有状态实例**——不新建、不重算，常数时间。`neighbors` 由 `ref://mc-26.3-analysis/src/net/minecraft/world/level/block/state/StateDefinition.java:36-133` 在构造时预计算并回填（`StateHolder.java:127-133` 的 `initializeNeighbors`），且为「无属性 / 单属性 / 多属性」三条路径分别建表（第 69-73、81-101、103-133 行），单属性路径第 88-97 行把邻居数组填成一维。序列化声明式：`ref://mc-26.3-analysis/src/net/minecraft/world/level/block/state/BlockState.java:10-16` 用一个 `Codec` 表达「要么是默认状态的简写（只写 id），要么是带 `properties` 的完整形式」，实现见 `StateHolder.java:148-162` 的 `dispatch("id")` + `lenientOptionalFieldOf("properties")`；属性名还受 `StateDefinition.java:27` 的 `^[a-z0-9_]+$` 校验。

**本仓对照。** 我们有三个纯数值辅助类、**没有状态对象**：`src/Sharpcraft.Common/Registries/BlockStateLayout.cs:17-92` 记录「方块 → 全局 id 区间」，`BlockAt`（第 67-78 行）用线性扫描反查（注释自认「used for classification at load time, not per frame」）；`BlockStateDefinition.cs:17-83` 存属性数组并用 `IndexOf(IReadOnlyList<string>)`（第 59-83 行）按基-radix 折叠出偏移；`BlockStateProperty.cs:14-33` 的值是**字符串列表**，`IndexOf`（第 20-32 行）逐字符串比较。于是状态本身只是一个 `int`，读属性要「先知道属性名、再线性找下标、再线性找值」，全程无缓存。

**缺口后果。** 区块网格化时每个方块都在做字符串比较——`StateVisualCache` 用 `ConcurrentDictionary<int, QuadVisual>` 按 state id 缓存**正是为了掩盖这一点**（缓冲而非消除）；没有转移表，「把 facing 从北改到南」只能重建字符串列表重算；没有 codec，数据侧（测试语料、未来的资源包、调试工具）无法表达状态。

**最小落地路径。** ① 属性值改为「序号 + 名字表」：`BlockStateProperty` 内部存 `int` 序号与 `string[]` 名字，查找走一次 `Dictionary<string,int>`（这一步即可消掉热路径字符串比较）；② 为每方块预计算转移表 `int[propertyCount][maxValues] → 新全局 id`（本质是把 `neighbors` 压平成 `int[][]`），`setValue` 退化为两次数组下标；③ 先不做 codec——我们的状态解析入口只有一处，代价低于收益。

## 10. P9 注册表、声明式序列化与数据包驱动的注册表加载

**原版结构。** 注册表是显式对象（`Registry` / `MappedRegistry`，含 freeze、数值 id、按名 codec），而**所有数据驱动结构都用 `Codec` / `MapCodec` 声明**：全库 1526 处 `public static final (Map)Codec<…>` 字段（`ExtraCodecs` 41、`SlotDisplay` 11、`ClickEvent` 10、`Biome` 7、`EquipmentClientInfo` 6……）。`ref://mc-26.3-analysis/src/net/minecraft/resources/RegistryDataLoader.java:98-197` 用「注册表 + codec」配对声明四张清单（`WORLD_REGISTRIES` / `DIMENSION_REGISTRIES` / `RELOADABLE_REGISTRIES` / `SYNCHRONIZED_REGISTRIES`），第 199-268 行按清单统一加载（`RegistryOps` 提供跨注册表引用解析），第 283-305 行把**所有**加载错误聚合成一份报告而不是遇错即停。

**本仓对照。** `src/Sharpcraft.Common/Registries/Registry.cs:22-102` 已对齐「保序、拒绝重复、冻结、数值 id 与迭代序一致」，并且比原版更明确地把「注册序即迭代序」写成唯一契约（见 `docs/analysis/common/registry-order.md`）；`IdMapper.cs:15-80` 是对原版 `IdMapper` 的忠实镜像（引用相等、id 不复用、默认 -1）。缺的是序列化层，而且各加载器的**数据来源本来就不一样**：`ItemRegistry.LoadEmbedded()` 读内嵌的注册序快照文本（`vanilla-item-order.txt`）、`TrimRegistryTable` 取自网络 config 的 `registry_data`、贴图与标签来自提取后的资源包——于是「遍历 + 解析 + 校验 + 错误信息」在每个加载器里各写一遍。

**缺口后果。** 每加一个数据驱动结构就新增一份解析与错误处理（重复、易漂移），错误信息风格不统一，也无法用同一机制做「全有或全无」加载（与 P4 相关）。

**最小落地路径。** 不引入通用 codec 框架（成本远超收益）。① 把「读一个目录下的 N 个条目」抽象成一次调用（输入：来源、每项解析函数、注册表目标；输出：成功项 + 聚合错误列表），替换各加载器重复的遍历与 `catch`；② 新增数据驱动结构**一律**走该入口，并写进对应模块文档作为约定。

## 11. P10 实体渲染器注册表与覆盖校验

**原版结构。** `ref://mc-26.3-analysis/src/net/minecraft/client/renderer/entity/EntityRenderers.java:21-60`：第 21-27 行一张 `Map<EntityType<?>, EntityRendererProvider<?>>` 加 `register`，第 29-39 行 `createEntityRenderers` 在有 `Context` 后统一实例化、单条失败包装成「Failed to create model for <实体名>」，第 51-60 行 `validateRegistrations()` 遍历注册表，**对每个没有渲染器的实体告警**（player / mannequin 例外）——「漏注册」是可发现的。`ref://mc-26.3-analysis/src/net/minecraft/client/renderer/entity/EntityRenderDispatcher.java:42-113`：第 43-45 行持实例表与按 `PlayerModelType` 分开的玩家/假人两张表（同类型实体的渲染器依赖实体自身皮肤模型），第 90-96 行 `getRenderer(entity)` 先特判玩家/假人再 `renderers.get(entity.getType())`，第 104-113 行还提供按**渲染状态**取渲染器的重载，让提取阶段与提交阶段共用同一张表。几何侧 `ref://mc-26.3-analysis/src/net/minecraft/client/model/geom/builders/LayerDefinition.java:5-25` 是「网格定义 + 纹理尺寸」的不可变描述，第 18-20 行烘焙成 `ModelPart` 树；`EntityModelSet` 集中烘焙并缓存各层（全库 127 处引用）。`EntityRendererProvider` 出现 183 次、涉及 158 个文件——原版客户端最大的注册面之一。

**本仓对照。** 没有注册表。`src/Sharpcraft.Client/Render/EntityModelResolver.cs`（1231 行）是静态类，第 19-25 行有三个 `public static … { get; set; }` 可变全局（`Items`、`ItemModels`、`ItemTags`），模型按实体类型 id 在代码里分派。`EntityRenderLayerDefinition.cs:13-22` 是每层一个记录（名字、几何种类、顺序，外加可选的模型/纹理选择委托），方向是对的（不可变描述 + 声明顺序），但没有「类型 → 渲染器」这层，也没有任何启动期「某类型缺模型」的校验——缺失由 `EntityPlaceholderMesher.cs` / `BlockEntityPlaceholderMesher.cs` 静默兜底。

**缺口后果。** ① **静态可变状态与原则 2 冲突**：三个 `static … { get; set; }` 会强引用加载上下文里的对象，模组热卸载后 ALC 无法回收；② 缺渲染器的实体一律静默变占位模型，与原则 15「简化必须登记且可诊断」相反，也重演了原版 `validateRegistrations` 想避免的问题。

**最小落地路径。** ① 加 `Dictionary<int, EntityRendererEntry>`（实体类型 id → 记录：图层定义、模型来源、纹理选择），启动时一次构建并冻结，`EntityModelResolver` 退化为查表；② 启动期遍历实体注册表，缺项**逐条告警**并可导出清单（同时给出原则 15 要求的登记入口）；③ 三个静态可变全局改为构造注入的上下文对象，消除热卸载障碍。

## 12. 单例（不计为模式，仅登记）

以下在原版只见到一处或极窄范围的使用，不构成模式，但作为「原版有此能力、我们未采纳」登记备查：

- `ref://mc-26.3-analysis/src/net/minecraft/util/SingleKeyCache.java:16-23`（只缓存最后一个键）仅 2 个文件在用（`MultiLineTextWidget`、`Util.java:1050-1052` 的工厂本身），价值不足以单列。
- `ref://mc-26.3-analysis/src/net/minecraft/util/StaticCache2D.java:14-33` 被 9 个文件使用，但集中在区块生成的 status task 上，属服务端侧，当前不在我们范围内。
- `ref://mc-26.3-analysis/src/net/minecraft/client/renderer/ProjectionMatrixBuffer.java:1-74` 负责投影矩阵 uniform 的复用；我们的等价物是 `src/Sharpcraft.Framework/Rendering/CameraUniform.cs`，职责相近，不单列。

---

备注：本文件新增后，`docs/analysis/architecture/README.md` 的生成索引需由 `tools/Sharpcraft.Docs.Indexer -- --write` 重新生成（本次分析按任务约束未运行构建，索引保持旧状态，提交前须补上）。

## 13. 建议优先处理的 3 项

### 13.1 P1 + P2：纹理对象家族与采样器缓存（合并为一次落地）

理由：这是唯一一条**同时**改善正确性、热路径分配与原则 11 承载面的组合，且前置分析已就绪（T-784 已登记，T-783 正被它阻塞）。两者必须一起做：采样器缓存的价值要由「纹理自带 sampler」来消费，否则只是把 `CreateSampler` 换成查表，绘制点仍各自选 sampler。落地顺序固定：抽象基类 + 采样器表 → 迁移 atlas / UI / skin 三个持有者 → 删掉绘制路径里 6 处现建现毁 → `.mcmeta` 的 `clamp` / `blur` 解析。风险低：只动 GPU 资源所有权，不动几何与着色器。

### 13.2 P3：不可变渲染状态对象与管线缓存

理由：这是原则 11 能否成立的**结构性前提**，也是 P2 在管线上那一半的落点。当前 27 处内联 `CreateRenderPipeline` 意味着：外部着色器包无法「声明并覆盖一个 pass」（没有描述符可覆盖）、管线编译产物无法复用、状态判断散在绘制循环里。最小切片是先只做「不可变记录 + 集中清单 + 键化缓存」，**不改 pass 划分**——对外行为零变化，只把「pass 是什么」从代码顺序变成数据。

### 13.3 P4：两阶段重载与失败可观测

理由：这是当前**最便宜的一次正确性提升**。资源包热重载已是既有功能（3 个监听器、顺序依赖真实存在），而监听器异常被静默吞掉——既无法诊断，也违反原则 15 的可诊断要求。改造面很小（`ResourceManager.Reload` 一个方法 + 三个注册点），收益是「失败不再产生半新半旧的界面」并回报给调用方。prepare / apply 的线程分离可以第二批做，先把语义（收集失败、整体回滚、可上报）纠正。

**为什么不选 P7 / P8：** 它们的结构缺口更大，但当前**没有可观测的错误**（P7 的索引常量目前是对的，P8 的字符串查找已被 `StateVisualCache` 遮住），且改造覆盖网络与网格化两条主路径；按原则 14 需先补真值向量再动，故排在 13.1-13.3 之后、各自独立立项。
