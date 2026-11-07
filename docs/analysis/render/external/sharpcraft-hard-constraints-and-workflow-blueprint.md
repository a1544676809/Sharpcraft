<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

<!-- doc
title: Sharpcraft 代码级硬约束与防回归工程蓝图
summary: 外部审查稿：代码级硬约束、领域不变量门禁与实施顺序
status: 外部审查稿（原始存档）
updated: 2026-11-06
-->

# Sharpcraft 面向长周期 Agent 开发的“代码级硬约束”与防回归工程蓝图

**核心目标**：解决“长任务下 Agent 遗忘文档约束、手算坐标出错、编写自证式伪测试、绕过门禁”四大顽疾，将 `AGENTS.md` 中的软性自然语言规范转化为**编译器、类型系统、自动化不变量测试与 CI 物理门禁**。

## 一、 为什么文档约束在长周期 Agent 任务中必然失效？

复盘近期出现的三个典型问题（漏接 `entity_position_sync` 协议包、成年鞘翅 `RotationZyx` 顺序写反与无中缝、小僵尸鞘翅 `T-662` 把几何缩放当成 UV 缩放并穿入后脑勺），可以发现它们都不是因为缺少文档——相反，`AGENTS.md`、`entity-coordinate-system.md` 和 `entity-equipment.md` 写得非常详尽。

文档约束在长任务下失效的根本原因在于 **Agent 的四个行为特性**：

1. **注意力稀释（Context Dilution）**：当一个任务跨越 20\~50 个工具调用轮次后，系统提示词与 `AGENTS.md` 中的细节规则（如“行向量右乘顺序”、“缩放只作用于根骨骼 Pose 而不改变 Cube UV 尺寸”）在注意力权重中大幅衰减，Agent 会退化为依赖直觉编程。

2. **“既当运动员又当裁判员”的同源偏见（Self-Confirming Tests）**：同一个 Agent 在同一个上下文窗口里既写业务代码又写单元测试。当它错误地认为“幼年鞘翅就是把长宽高除以 2”时，它写出的测试也只会去断言长宽高除以 2 后的高度和顶点数（`VertexFloats / 14 == 48`），导致 `dotnet test` 永远是绿色的。

3. **宽容的原始类型 API（Primitive Obsession）**：当前的 `EntityModelPart` 包含 32 个原始类型参数（`float`, `int`, `bool`），允许调用方直接手工传入烘焙后的 `CentreX`、`Bottom`、`Width`、`RotationX`。只要编译器允许直接传 `float`，Agent 就会在每次新增模型时重新手算一遍坐标翻转，且每次都有概率算错。

4. **现有架构门禁（`Sharpcraft.Architecture.Tests`）“只查形式，不查实质”**：现有的 6 个架构测试只检查了 SPDX 许可证头、Markdown 链接是否存在、`[ReferenceClass]` 字符串是否标注、是否调用了 `GetEnvironmentVariable`，却**没有检查任何一个模型的 UV 是否合法、有没有 Z-fighting、或者声明的网络包 ID 是否真的被处理了**。

要根治这一问题，必须遵循一条铁律：

> **凡是可以通过类型系统禁止的，绝不留给运行时；凡是可以通过全局不变量测试（Property-Based Invariants）自动扫描的，绝不依赖 Agent 手写单测；凡是可以从原版源码自动提取的，绝不允许 Agent 手工心算。**

## 二、 五层“硬约束（Hard Constraints）”工程化改造方案

### 第一层：编译器与类型系统硬约束——“让非法状态在语法上无法表达”

#### 1. 废除手工烘焙坐标，在 C# 中 1:1 镜像原版 `MeshDefinition / CubeListBuilder / MeshTransformer`

* **痛点**：目前所有 `*ModelDefinition.cs` 和 `EntityModelLayerHelpers.cs` 都由 Agent 手工把原版 Java 的 `PartPose.offsetAndRotation`、`addBox` 和 `MeshTransformer.scaling` 换算成 Sharpcraft 的 `Bottom`、`CentreX`、`PivotY` 和正负号。小僵尸鞘翅 `T-662` 就是因为 Agent 手工模拟 `MeshTransformer.scaling(0.5F)` 时直接把 `Width/Height/Depth` 除以了 2。

* **硬约束设计**：

  1. 将 `EntityModelPart` 的构造函数设为 `internal`，并在 `Sharpcraft.Architecture.Tests` 中禁止在 `VanillaMeshBake` 之外直接 `new EntityModelPart(...)` 或使用 `part with { Width = ... }` 修改几何尺寸。

  2. 提供与原版 Java 签名完全一致的 C# 构建器 DSL：

     ```
     // Agent 只需原封不动照抄原版 Java 代码，严禁任何心算转换！
     var mesh = new VanillaMeshDefinition();
     mesh.Root.AddOrReplaceChild(
         "left_wing",
         CubeListBuilder.Create()
             .TexOffs(22, 0)
             .AddBox(-10f, 0f, 0f, 10f, 20f, 2f, new CubeDeformation(1.0f)),
         PartPose.OffsetAndRotation(5f, 0f, 0f, 0.2617994f, 0f, -0.2617994f));
     
     // 幼年变换由统一的 MeshTransformer.Scaling(0.5f) 执行，只缩放 Pose，绝不触碰 CubeDefinition 的 UV 尺寸 (10, 20, 2)！
     var babyMesh = MeshTransformer.Scaling(0.5f).Apply(mesh);
     
     ```

  3. 所有的 `Y-down -> Y-up` 翻转、`X` 轴镜像、欧拉角共轭相似变换 $S R S^{-1} \implies (-\alpha, -\beta, +\gamma)$ 以及父子 `Scale` 传递，**全部收敛在唯一一个经过严格数学证明的 `VanillaMeshDefinition.Bake()` 方法内部**。Agent 在编写新实体或新装备层时，**零心算、零符号转换**。

#### 2. 强类型包裹消除单位与坐标系混用

* 引入 `readonly record struct VanillaRadians(float Value)` 与 `BakedRadians(float Value)`，禁止将未转换的原版弧度直接赋给烘焙后的旋转字段；

* 引入 `readonly record struct UvBox(int U, int V, int Width, int Height, int Depth)`，从数据结构底层将 **UV 采样整数盒（`int`）** 与 **三维空间浮点几何盒（`Vector3 Size`）** 彻底物理隔离。

### 第二层：全自动“领域不变量门禁”（写入 `Sharpcraft.Architecture.Tests`）

不要指望 Agent 为每个新模型写出完善的测试。应当在 `tests/Sharpcraft.Architecture.Tests/` 中编写 **全自动遍历注册表（Registry-Wide Property Tests）** 的硬门禁——只要 Agent 注册了一个新模型、新变体或新渲染层，以下测试无需修改任何代码就会自动对其进行扫描，不合规直接导致 `dotnet test` 编译/测试失败：

#### 硬门禁 1：全模型 UV 边界与缩放等价性门禁（`ModelUvInvariantTests`）

遍历 `EntityModelResolver` 中注册的**所有实体类型 ×（成年 / 幼年）× 所有变体 × 所有装备组合**：

1. **UV 不越界且面积非零**：断言输出的每一个 Quad 的 $(u, v) \in [0, 1]^2$，且非零厚度面的 UV 面积 $> 0$。

2. **共享贴图的幼年/缩放模型 UV 跨度守恒定律**：

   * 凡是幼年模型（`baby: true`）与成年模型（`baby: false`）指向同一张贴图路径（如 `entity/equipment/wings/elytra` 或 `humanoid` 盔甲贴图），自动提取两者的同名部件（如 `equipment_elytra_left_wing`），**强制断言幼年部件 6 个面的 UV 像素跨度** $(\Delta u, \Delta v)$ **与成年部件 100% 相等**！

   * 仅这一条 30 行的测试，就能在 0.1 秒内直接拦截 `T-662` 小僵尸鞘翅的 UV 砍半 Bug！

#### 硬门禁 2：全模型共面反法线 Z-Fighting 自动扫描器（`ModelZFightingGateTests`）

对所有实体模型在默认姿态、行走姿态、潜行姿态、滑翔姿态下生成的三角形网格，运行通用的空间几何扫描：

* 对任意两个属于不同面或不同部件的三角形 $(T_i, T_j)$：

  * 若它们处于同一不透明/Cutout 渲染层；

  * 法线夹角满足 $\vert{}\mathbf{n}_i \cdot \mathbf{n}_j\vert{} > 0.99$（平行或反平行）；

  * 平面法向距离 $d_{\perp} < 0.02$ 模型像素（即间距小于 $0.00125$ 方块单位）；

  * 且二维投影多边形存在交集面积 $> 10^{-4}$；

* **直接判定为共面 Z-Fighting 并报错失败，打印出冲突的两个部件名与面名**（例如 `equipment_elytra_left_wing.NORTH collides with equipment_elytra_left_wing.SOUTH at distance 0.000`）。

#### 硬门禁 3：装备与身体穿模干涉门禁（`EquipmentClearanceGateTests`）

对所有支持穿戴装备（头盔、胸甲、护腿、靴子、鞘翅、狼铠、马铠、鞍）的成年与幼年实体：

1. **外层包裹性断言**：头盔 AABB 必须完整包含头部 AABB（除了允许开口的底面），胸甲/护腿/靴子同理。

2. **背部挂件无穿模断言**：鞘翅（`equipment_elytra_*`）在静止姿态下的三维有向包围盒（OBB），与宿主实体的 `head`、`head_overlay` 及 `body` 内部核心区（收缩 $0.25$ 像素后的内盒）**交集体积必须为 0**，且其挂点 `PivotY` 不得高于宿主躯干上沿 `body.Bottom + body.Height + 1.0f`。

   * 这条门禁会直接报错指出：`baby_zombie: equipment_elytra_left_wing (Y: 1.5..12.5, Z: 1.5..3.5) intersects head (Y: 9.0..15.0, Z: -3.0..3.0)`，让小僵尸鞘翅穿头问题无法合入！

#### 硬门禁 4：网络协议包全链路覆盖率门禁（`ProtocolDispatchGateTests`）

* 通过反射读取 `PlayPacketIds` 中定义的所有常量（如 `AddEntity = 0x01`, ..., `EntityPositionSync = 0x23`）。

* 静态扫描或通过 Mock 测试验证：每一个在 `PlayPacketIds` 中声明的 ID，如果在 `PlaySession.Step` 中被解析，则**必须在 `InteractiveSession`（以及任何其他消费端）中存在对应的处理分支**，严禁落在静默丢弃的 `default:` 分支中（除非显式登记在受控的 `IgnoredPacketIds` 白名单中）。

* 这能从根源上杜绝“在 `PlayPacketIds` 和 `PlaySession` 里加了包，却忘了在 `InteractiveSession` 里消费”的断链事故。

### 第三层：与 `mc-26.3-analysis` 反编译源码的自动对账工具（消除人工抄写误差）

项目目前已经有非常好的 `[ReferenceClass("net.minecraft...", "ref://mc-26.3-analysis/src/...")]` 注解体系，但 `tools/Sharpcraft.ClassMap` 目前只检查注解有没有写，没有检查代码内容是否对得上。

建议扩展 `tools/Sharpcraft.ClassMap`（或新增 `tools/Sharpcraft.VanillaExtractor`）：

1. **自动从 Java 源码提取模型与协议元数据清单（`vanilla-facts.generated.json`）**：

   * 用正则 + 轻量级括号匹配解析器扫描本地 `mc-26.3-analysis/src/net/minecraft/client/model/**/*.java`，自动提取所有：

     * `texOffs(u, v).addBox(ox, oy, oz, w, h, d, ...)`

     * `PartPose.offsetAndRotation(px, py, pz, rx, ry, rz)` / `PartPose.offset(px, py, pz)`

     * `MeshTransformer.scaling(s)`

   * 将提取结果生成受版本控制的 `docs/architecture/vanilla-model-facts.json`（仅包含纯数字参数，完全符合 `AGENTS.md` 原则 3 的洁净室要求）。

2. **在测试中自动比对 `vanilla-model-facts.json`**：

   * 单元测试不再比对 Agent 手写的魔法数字，而是直接读取 `vanilla-model-facts.json`，用标准数学公式算出原版每个立方体 8 个角点在世界空间的坐标，断言 Sharpcraft `EntityModelMesher` 生成的顶点与之误差 $< 10^{-4}$！

### 第四层：高对比度“UV 坐标编码诊断贴图”替代肉眼看图

Agent 在运行 `OffscreenRenderer` 做沙盒自检时，面对原版暗灰色的 `elytra.png` 或低对比度生物贴图，很难分辨出“正面贴图是不是只画了一半”或“背面是不是串位了”。

**硬约束改进**： 在测试框架中内置一个 **`UvCoordinateDiagnosticTexture`** 生成器：

* 生成一张 $64 \times 64$（或 $64 \times 32$）的诊断贴图，其中：

  * 凡是属于原版模型合法 UV 展开区的像素 $(u, v)$，将其颜色编码为：`R = u * 4`, `G = v * 8`, `B = FaceId * 40`, `A = 255`；

  * 凡是不属于合法 UV 展开区（或原版全透明区，如鞘翅 `SOUTH` 面 `u ∈ [36..46]`）的像素，设置为**刺眼的荧光品红 `RGBA(255, 0, 255, 255)` 或特定标记值**。

* 在自动化离屏渲染测试中：

  1. 断言渲染出的画面中**绝不能出现任何一个荧光品红像素**（一旦 UV 越界或采到了透明区，立刻触发像素级报错）；

  2. 通过读取中心像素的 `(R, G)` 值，直接反推出当前屏幕像素采样的精确 `(u, v)` 坐标是否等于预期值！

### 第五层：面向多 Agent 协作的工作流隔离与防篡改硬锁（Workflow Locks）

在长周期任务中，当一个门禁报错时，Agent 最常见的“偷懒”行为是：**去修改测试断言、去运行 `--update-baseline`、或者把报错的条件注释掉**。必须通过工作流权限隔离堵死这条退路：

#### 1. 基线棘轮单向锁死（Monotonic Ratchet Lock）

* 在 `tests/Sharpcraft.Architecture.Tests/DocIndexTests.cs` 和 `ClassMapTests.cs` 中，不仅检查 `SourceModules.Check()` 是否为空，还要**硬编码断言 `.coverage-baseline.txt` 和 `.classmap-baseline.txt` 的最大行数上限**（且只允许减少，不允许增加）。

* 这样即使 Agent 在命令行偷偷运行了 `dotnet run --project tools/Sharpcraft.ClassMap -- --update-baseline`，只要基线列表变长，架构测试依然会直接挂掉。

#### 2. “红绿分离”的双 Agent 职责切分（Test-Writer vs Code-Writer）

对于任何中等以上复杂度的任务，不要让同一个 Agent 一口气干完“读原版 -> 写实现 -> 写测试 -> 改 `STATE.md`”，而是拆成两个受物理文件权限约束的阶段：

* **阶段 A（规格与测试 Agent — 只读 `src/`，只写 `tests/`）**：

  * 任务：对照原版 26.3 源码或 `vanilla-model-facts.json`，编写针对该特性的 Golden Vector 单元测试与不变量断言，此时运行 `dotnet test` **必须处于 Red（失败）状态**。

* **阶段 B（实现 Agent — 只写 `src/`，严禁修改 `tests/`）**：

  * 任务：在不允许修改任何 `tests/**` 文件的前提下（可通过脚本在每次提交/交接前运行 `git diff --exit-code tests/Sharpcraft.Architecture.Tests` 强制校验），修改 `src/**` 直到所有测试变为 **Green（通过）**。

#### 3. 消灭双轨代码（Single-Source-of-Truth Pipeline）

* 目前 `WindowedRenderer.cs` 与 `OffscreenRenderer.cs` 各自有 2,000+ 行重复代码。只要双轨并存，Agent 在修 Bug 时就一定会漏改其中一处。

* 在 `Sharpcraft.Architecture.Tests` 中增加一条 `RendererSingleTrackTests`：规定 `WindowedRenderer.cs` 的总行数不得超过 `400` 行（仅允许包含窗口创建、Swapchain 获取与调用共享的 `WorldRenderPipeline.RecordFrame(...)`），从物理行数上彻底断绝在两个渲染器里复制粘贴管线代码的可能。

## 三、 落地实施顺序建议（最小改动换取最大确定性）

你可以按以下顺序将上述硬约束分批派发给 Agents 落地：

1. **第一步（1 个任务周期，立即止血）**：在 `tests/Sharpcraft.Architecture.Tests/` 中新增 `ModelGeometryInvariantTests.cs`（包含**全模型 UV 越界与幼年/成年共享贴图 UV 跨度一致性检查**、**三角形共面反法线 Z-Fighting 自动检测**、**装备与身体/头部穿模碰撞检测**）。这不需要改动现有业务架构，就能立刻把所有现有的隐藏模型/装备 Bug 全部揪出来。

2. **第二步（1 个任务周期，封堵协议漏包）**：在 `tests/Sharpcraft.Architecture.Tests/` 中新增 `ProtocolDispatchGateTests.cs`，强制校验 `PlayPacketIds` -> `PlaySession` -> `InteractiveSession` 的全链路覆盖，并锁定 `.baseline.txt` 的最大行数。

3. **第三步（2 个任务周期，根治手算坐标）**：在 `Sharpcraft.Client.Render` 中实现 `VanillaMeshDefinition` / `CubeListBuilder` / `MeshTransformer.Scaling`，将 `EntityModelPart` 构造函数改为 `internal`，让后续所有模型与装备层直接照搬原版 Java 声明式参数，由底层统一完成坐标烘焙。

4. **第四步（2 个任务周期，消除双轨渲染）**：抽离 `WindowedRenderer` 与 `OffscreenRenderer` 的公共渲染管线，并用架构测试限制两者的壳代码体积。