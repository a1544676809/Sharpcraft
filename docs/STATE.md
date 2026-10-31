<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

<!-- doc
title: STATE —— 跨会话进度锚点
summary: 当前里程碑、进行中、阻塞、下一步、未提交改动
status: 活文档
updated: 2026-10-29
-->

# STATE —— 跨会话进度锚点

> 本文件只记录**易变状态**（AGENTS.md 4.2）。设计结论属于 `docs/modules/`，里程碑划分属于 `docs/milestones/roadmap.md`，索引属于 `docs/README.md`，决策属于 `docs/decisions/`。**不要在此复述它们。**
> 每次收工必须与代码同一提交更新本文件，并写清"下一步具体动作"。

最后更新：2026-10-31

## 当前里程碑

**规划顺序**：M1 → M2 → M3 → M4 → M5 → M6（定义与出口条件见 `docs/milestones/roadmap.md`，
该文件只写稳定内容，状态一律记在本节）。

| 里程碑 | 状态 | 出口证据 |
| --- | --- | --- |
| M1 基础逻辑地基 | **已完成** | 门禁断言、T-005…T-021、真实服务器到 play |
| M2 世界数据闭环 | **已完成** | T-024…T-031；9/9 区块、275,141 非空气方块、贪婪合并实测 |
| M3 资产管线 + 启动器闭环 | **已完成** | T-032…T-037；官方 jar 复验、pack 11,533 条目、带纹理窗口 |
| M4 实体系统根基 + 输入输出 | **已完成**（所有者 2026-10-29 确认） | 走进去、挖方块、地形变化；块更新与竞技场实测 |
| M5 协议深化 | **进行中** | 见下 |
| M6 模组与着色器包 | 未开始 | — |

**M5 分三段**（顺序由所有者 2026-10-29 定）：

| 段 | 范围 | 状态 |
| --- | --- | --- |
| 5.1 实体与玩家同步 | `docs/specs/network/entity-sync.md` | **完成**（T-088/T-089/T-090，静止逐位吻合、平地行走 ≤5e-4 格）|
| 5.2 界面层 | `docs/modules/ui.md`；P0 内核：几何 T-099、GPU 管线 T-100、声明式内核 T-102 已完成 | **进行中** |
| 5.3 渲染管线 | `docs/specs/render/pipeline.md`（已评审）| **P1 待开工** |

**T-110 纹理方向**：按原版 FaceBakery 逐面轴投影，六个面全部对齐（测试断言世界轴）。
**T-109 热加载判据**：五条自动化；结构校验前移到加载期（否则坏文档在渲染线程抛异常）。
**T-116 输入骨架**：按键边沿 + 字符回调 + 默认键位表（Tab/T/Enter/Escape）；顺带修复"未绑定动作被当成 W 按住"。
**T-117 玩家列表显示**：Tab 按住才显示（界面几何改为读输入状态）。
**T-115 帧尖峰**：已定位为单帧无上限上传；所有者裁决**接受为受控偏差**（ADR-0011，含复查条件）。
**T-118 聊天（发送侧）已完成**：`chat`/`chat_command` 构建、`ChatInput`、T 与 `/` 打开、Enter 发送、Esc 关闭、提示框样式与光标。

**T-119 完成**：`system_chat` 解析 + `ChatLog`（100 行、原版淡出曲线与折行、打开提示框时全亮）。
**T-122 GUI 画布已完成**：整数缩放（960×540 → 2 → 虚拟 480×270），布局用虚拟像素、投影与裁剪乘缩放。
**T-123 指针与画布**：`CursorInCanvas`（接口像素）、裁剪用整数缩放、提示框按画布放置（10 项测试）；
实测自缩放 960×540(scale 2 → 480×270) → 640×360(scale 1 → 640×360) 画布随之变化。
**T-114 已解决**：`ChunkSeamTests` 抖动根因是 `Build` 的 15 ms 墙钟预算在繁忙时提前结束，测试误当同步全量用；已改无上限预算，三次全量全绿。
**T-124 等效性已验证**：缩放（约 15 万组比对）、画布取整、投影四角/矩阵一致、聊天淡出（age 0–300）逐值一致；
**像素级对照仍缺 T-104**（离屏路径无界面 pass）。
**实测抓到两个真 bug 并修复**：①命令线上**不能带前导 `/`**（原版 `sendCommand(substring(1))`；带斜杠被服务器判"未知命令"）；
②**空格宽度**来自字体定义（`space` provider 的 4 px），否则词间空白消失。
**输入环境已确认**：远程**不产生按键事件**（WASD/开聊天按不出，官方同）但**字符输入正常**（两边都能打字）→ 环境限制，与原版一致；排障开关 `MC_INPUT_TRACE=1`。
**T-131 文本输入基础设施已完成**：`TextEditModel` + `TextEditRenderer`（两形状光标、300 ms 闪烁、选区、点击定位、编辑键）。
**T-129/T-130 已修**：空格绘制路径（度量与绘制统一走 `AdvanceOf`）、提示框关闭时丢弃字符（按 T 不再自带 `t`）、语言选择基础（候选链 + 回退）。
**T-128 组件样式已完成**：颜色（原版调色板 + 继承）与粗体（右移重绘）；聊天按**样式段**绘制。
**T-127 玩家消息已入站**：`player_chat` 解析 + 实测往返（`chat received: 名字: 内容`）；装饰待 `chat_type` 注册表。
**T-121 语言表已完成**：真实资源包验证（>1000 键、`command.unknown.command` → 英文）；`%s`/`%1$s`/`%%` 替换。
**下一步具体动作**（2026-10-30 校准）：
1. 资源包模块 A1–A11 已完成，**推送被网络阻断**（`github.com` 不可达、SSH 公钥未登记），恢复后第一件事 `git push`（117 提交）。
2. M5 5.2 聊天收口：已补 `change_page`/`open_file`、动作栏 HSV 颜色循环；`textBackgroundOpacity` 已可配置（`ChatLog.BackgroundOpacity`，默认 0.5）、`delete_chat` 的 `ChatLog` 侧完成（签名存取+删除，4 项测试）；
签名已由解析层透出并接线到会话删除 ⇒ **A 的聊天项全部完成**。
3. **5.3 渲染管线 P1 已开工**（所有者 2026-10-30 指示按 AGENTS.md 推进）：
   P1 范围锁定为**实体静态模型 + 名字牌 + 视锥剔除统计**（`docs/specs/render/pipeline.md` §12）。
   第一片已完成：`ViewFrustum`（六平面 + AABB 支撑点测试，5 项单测）。
   **下一步具体动作**：把视锥接线到 `WindowCommand` 的实体几何回调（相机 lambda 里缓存 view-projection），
   并输出剔除计数（QA-1.3 要求 ≥60%）；随后实体模型需由启动器从 jar 提取模型 JSON（仓库内不存资产）。
4. T-104/T-159（离屏界面 pass + 逐像素）改为 **5.3 的验收手段**（规格见 `docs/analysis/render/offscreen-ui-pass.md`），不再独立排期。
5. **着色器包资料已收集并核对官方文档（2026-10-30）**：`docs/analysis/render/shader-pack-anatomy.md`（真实包 468 条目、维度×阶段两层、
   16 类几何阶段、9 档 profile）+ `docs/specs/render/shader-pack-compatibility.md`（不支持项与 GLSL→WGSL 改写）。
   **已按官方文档修正三处认知**：①官方全序含 `setup/begin/prepare/shadowcomp`（样本只有子集）
   ②维度选择有 `dimension.properties`（含回落语义）与 `worldN` 遗留两套机制 ③`iris.features.required` 应先于编译检查。
   **下一步具体动作**：把四条待补项写入 `shader-packs.md` 正文（需所有者评审），再进 P3 接入点设计。
6. **已裁决（2026-10-30）**：**预留 Vulkan 接口、不实现**；WGPU 仍为唯一实现与工作中心。
   落地：`docs/specs/render/backend-interface.md` + `BackendCapabilities`（能力协商，3 项测试）⇒ T-200 完成。
   实现 Vulkan 后端仍**未获批准**，R-022 保留（P3 后按实测复核）。
7. **时域放缩前置已开工（T-201）**：`CameraJitter` 落地（周期 8、均值归零、投影平移 <1 px，4 项测试）；
   `pipeline.md` 新增 §13（jitter/运动矢量/深度为一等能力），`shader-packs.md` 新增 §9（阶段全序、维度双机制、
   特性检查、未识别阶段跳过）。**运动矢量数学已完成（T-202）**：`Reprojection` + `FrameHistory`，5 项测试（符号：当前→上一帧，像素）。
**像素级数学已完成（T-203）**：`ReconstructWorld` 与 `OfPixel`，3 项测试（含与解析解逐值一致）。
**下一步具体动作**：写 MotionVectors 的 WGSL pass（把 `OfPixel` 逐像素翻译，输出 RG16Float），并在窗口循环调用 `FrameHistory.Advance(viewProjection)`；验收方式为离屏出图后抽像素与 C# 结果比对。
随后才是"深度可采样"与 tonemap 时机。
8. **GPU 侧遮挡剔除已加入计划（T-204，所有者要求）**：规格 `docs/specs/render/occlusion-culling.md`（Hi-Z 两阶段、O1-O4 分期；QA-1.4：金字塔/剔除各 <=0.5 ms、剔除率 >=60%、**0 处误剔**）。
   **O1 已完成**：`OcclusionCulling`（保守 AABB 投影 + 遮挡判定），5 项测试。**下一步具体动作**：O2 —— Hi-Z 金字塔 build（compute），与 CPU 参考逐 texel 校验。
9. **T-205 帧比对工具已实现**：`PixelComparison`（5 项测试）+ `frame` 命令的 `MC_FRAME_COMPARE` 接线；`MC_FRAME_TOLERANCE` 控制容差。
   **端到端已跑通**（用显式地址 127.0.0.1:25565）：人为改 1 像素被准确抓到 first at (5,5)。
   **新发现（T-206）**：连续两次渲染差 **20315 像素**（首个 (448,222)）⇒ **离屏帧不可复现**（区块到达顺序、实体与相机随场景变化）。
   **T-206 进行中**：`SceneFingerprint` 已完成（排序区块键 + 粗采样，2 项测试，同世界同值/不同场景不同值）；
   **已接入真实路径**（`FrameCommand.RenderWorld`）：两次运行分别打印 `scene: chunks 9, fingerprint 0xE5A2…` 与 `0xAD10…`
   ⇒ **世界本身每次都不同**（生成点与区块集合由服务器决定），这就是不可复现的根源。
   **已达成（T-207）**：`SceneRecorder` 录制/回放 + `MC_FRAME_RECORD` / `MC_FRAME_REPLAY`。实测：录制 9 区块 3.38 MB；
   回放两次指纹**相同**（0x1579C2F0017F8D2A），逐像素 **`compare: identical (max channel delta 0, tolerance 0)`**。
   **T-208 进展（未修完）**：已修正 y 范围（`GetBlockState` 的 y 按 `y>>4` 分段，必须从 `MinSectionY*16` 起）——指纹范围修正后录制值变为 0xF26F…；
   并**排除布局原因**：录制与回放都报 `minSectionY -4 / 24 sections`，差异在**方块内容**。
   **已修完（T-208）**：首个差异诊断 `(0,-64,0) 88→0` 指出整世界载入为空；根因 **`SetBlockState` 只写入已存在的 section**，
   重建时 section 全为 null。改为载入时先 `SetSection` 创建。实测：`scene: roundtrip identical`、录制与回放指纹同为 `0xD9F5…`、`compare: identical`。
   ⇒ **中期目标条件一完全达成**（确定性 + 回放保真 + 逐像素 0 差异）。
11. **优先级补充（所有者 2026-10-30）**：**以还原原版着色器为标准**；着色器包降为次要，排在其后。
12. **T-209 天空与雾分析完成**：`docs/analysis/render/sky-and-fog.md`（26.3 雾是**属性驱动 + UBO(vec4+5 floats)**，
    距离按 `renderDistanceInChunks*16` 方块；天空盘半径 512、星星 1500、日出 16 段）。
    **T-210 已完成**：离屏路径新增**全屏渐变天空 pass**（三角形、无顶点缓冲、深度恒通过不写）。回放场景实测：
       y=5 (121,166,255)≈天顶(120,166,255)，y=400 (173,204,255) 接近地平线，y=520 被地形覆盖（顺序正确）。
    **T-211 距离雾已完成**：`Fog` uniform（颜色 + 起止方块，`MC_FOG_START/END`），顶点传 `clip.w`，片元线性 ramp；
       实测远处地形 (25,16,12)→(176,206,255)、天空不受影响。
    **T-212 窗口路径已接入**：同一着色器 ⇒ 补齐 binding 3（雾），新增天空管线先画；窗口出帧且**无 validation error**。
       **缺口**：本环境无可见窗口，窗口侧**像素未验证**（离屏侧已验证）。
    **T-213 像素探针完成**：`MC_FRAME_PROBE=x,y;x,y` 直接输出 rgba+argb（实测 480,5 → rgba(121,166,255,255)）。
    **T-214 半透明已规格化**：`pipeline.md` §15 给出 S1–S4（层分类 → 双网格 → 距离降序 → 共用雾），S1 依赖方块渲染层数据。
    **T-215 Hi-Z CPU 参考已完成**：`DepthPyramid`（逐级 2x2 取**最远**，`TryCoveringDepth` 选最细单 texel 覆盖层），4 项测试；
    这是 O2 的逐 texel 对照基准。
    **下一步具体动作**：①O2 —— 在离屏渲染器里加**第一个 compute 管线**做金字塔，回读与 `DepthPyramid` 逐 texel 比较；
    ②或先做半透明 S2/S3。二者都需要新的管线类型，建议先做 compute（Hi-Z 与后续 GPU 剔除共用）。
    **T-216 半透明 S3 排序已完成**：`TranslucentOrder.ByDistanceDescending`（距离降序 + 稳定），3 项测试。
    **待做**：S2 双网格（依赖方块渲染层数据）、S3 的绘制接线（混合开、深度写关）。
    **T-217 深度回读已完成**：深度纹理 `CopySrc` + 256 字节行对齐回读；实测 960x540 `nearest 0.9986 / farthest 1.0000`。
    **重要发现**：NDC 深度远处饱和（`1/z` 分布）⇒ O2 金字塔必须用**线性化深度**，否则"最远深度"在远处无区分度。
    **T-218 深度线性化已完成**：`DepthLinearisation`（4 项测试，含互为逆）；深度诊断现在同时打印方块距离，
    实测 `nearest 0.9986 → 70.3 blocks`、天空为 far(2000)。
    **T-219 着色器组织评估（本轮插入任务）**：原版 93 条目、`core/` 64 个"一管线一对文件"、`include/` 18 个共享片段；
    我方是 3 个 C# 文件里的内联字符串、无 include、无绑定单一来源 ⇒ **结论：不够好**，已出 G1–G5 改造计划。
    **T-220 着色器组织规范已定稿**：`docs/specs/render/shader-organisation.md`（目录/命名/include/绑定单一来源/
    启动自检/五条测试门禁/与原版对应关系）。**下一步具体动作**：按 G1-G5 实施（拆文件 → include 组合器 → 绑定表 →
    自检 → 条目表），完成后删除所有内联 WGSL 并以门禁测试防回退；随后回到 O2。
    **T-221 G2/G3/G4 已完成**：`ShaderIncludeComposer`（只展开一次/缺失报错/深度 4）、`ShaderBindings`（0-3 + 64B/32B）、
    `ShaderDescriptor.Validate`（binding 双向差异 + 入口缺失），6 项测试。
    **下一步具体动作**：G1/G5 —— 把地形/天空/UI 的内联 WGSL 移到 `Shaders/pipelines/*.wgsl`（嵌入式资源），
    用 `ShaderBindings` 生成布局，并加 §7 门禁测试（`Framework/Rendering/*.cs` 不得再有内联 WGSL）。
    **T-222 进展**：terrain/sky 已拆为 `Shaders/pipelines/*.wgsl`（嵌入式资源 + `ShaderLibrary`，含 include 组合），
    3 项测试；**重构前后帧逐像素 identical** ⇒ 无行为变化。**T-222 已完成**：五套着色器全部文件化（嵌入式资源），禁内联门禁 `ShaderSourceTests`(2) 生效，两次帧比对均 `identical`。
    **T-223 G5 已完成**：`ShaderPipelines.All`（terrain/sky/ui_panel/ui_sprite/ui_text，含入口与绑定）+ `EnsureValid()`
    在离屏与窗口启动时执行；3 项测试；帧仍 `identical`。**着色器组织改造到此收口（G1-G5 全完成）**。
    **T-224 O2.1 完成**：`depth_pyramid.wgsl`（compute，`cs_downsample`，binding 4/5/6）已登记并通过启动期校验；
    `DepthPyramid` 迁入 Framework 便于同处比对；入口识别改为"属性后看 4 行找 fn"（compute 的 `@workgroup_size` 在中间）。
    **T-225 O2.2 完成**：compute dispatch 跑通，**第 1 层与 CPU 参考逐 texel 全等**（480×270，mismatches 0，worst delta 0.000000）。
    途中解决四条平台约束（MAP 用途单向、缓冲不能既 MapRead 又 CopySrc ⇒ 深度拷贝两次、`target` 是 WGSL 保留字、
    金字塔必须在距离域比较）。
    **T-226 O2 完成**：GPU 完整 **9 级 mip 链**与 CPU 参考**逐 texel 全等**（total mismatches 0）。
    途中第五个坑：只有第 0 级需要 window→distance 转换（更深层已是距离），否则第 2–5 级出现 1929 方块的假差异。
    **T-227 O3.1 完成**：`FrameResult.Depth` 暴露深度；`MC_FRAME_CULL=1` 用真实深度金字塔对真实区块 AABB 判定，
    实测 `chunks 9, culled 0, visible 9` ⇒ **零误剔**（该场景本就无遮挡，0 是正确答案）。
    **O3 的现实约束**：①离屏路径一次 draw 画完所有区块 ⇒ 逐像素等价需先改按区块绘制（O3.2）
    ②剔除率在 9 区块无遮挡场景不可测，QA-1.4 的 ≥60% 需放大世界。
    **T-228 O3.2a 完成**：`FrameRequest.DrawRanges` + 离屏逐区块绘制（无 ranges 时仍单次 draw），帧比对 `identical`。
    **下一步具体动作（O3.2b，需三处改动）**：①`FrameCommand` 逐区块网格化并记录 `(FirstIndex, IndexCount)`
    ②把可见列表转成 ranges（被剔的给出 IndexCount=0）③构造**有遮挡场景**测剔除率，再跑 cull=0 与 cull=1 的逐像素比对。
    **T-229 O3.2b-1 完成**：离屏路径改为**逐区块网格化**（`MeshChunk` 循环 + `DrawRanges`），帧比对 `identical`
    ⇒ 拆分绘制与原整体绘制像素一致。**下一步**：把可见列表接上 ranges，再构造遮挡场景测剔除率并做 cull=0/1 比对。
    **（原计划）** `DepthPyramid` 改为接收**线性化距离**（或提供 `BuildFromWindowDepth(..., near, far)`），
    然后做 compute 版金字塔并与 CPU 结果逐 texel 比对。
10. **中期目标已立项**：`docs/milestones/midterm-goal.md` —— 确定性基线、五个阶段完整、Hi-Z 达 O3、
    真实着色器包（Complementary Unbound r5.9.3 主干子集）可加载且失败可回退、性能 p99 ≤16.7 ms。
10. 随后：T-104 离屏 UI pass（多 pass 骨架）→ 运动矢量 pass → Hi-Z O2；M6 模组热加载（先写 ADR）→ P3。
会打印 `chat received:`。
随后：T-120（`player_chat` 等剩余入站与组件样式）、T-108（热加载状态迁移）、T-104（离屏截图接 UI）。
随后：T-108（热加载状态迁移）、T-104（离屏截图接 UI）、T-114（`ChunkSeamTests` 并行抖动）、T-107（间歇 wgpu 断言，需完整 validation 文本）。
另：待所有者目视确认窗口内界面与玩家列表（Tab 按住）；T-096（会话持有注册表，需 ADR）、T-097（`walk` 二次登录）仍待排期。

## 进行中

- **T-037 完成**：纹理通路端到端（PNG 解码 → 图集 → UV → 着色器采样 → `blockstates`/`models` 按面取纹理）。2026-10-29 复验：官方 26.3 jar（41,483,720 字节 / SHA-1 `e877b6a0…`）→ pack 11,533 条目 / 5,854,134 字节 / 681 ms → 图集 1024×1024 / 1301 精灵 → `frame local` 出带纹理帧，石面像素 `rgba(143,143,143)` 与图集一致。
- **26.3 资产已就位（仓库外）**：`../mc-26.3-analysis/artifacts/26.3/{client.jar,26.3.json}`，SHA-1 与官方清单一致，assetIndex = 34。仓库内仍无任何资产，`git status` 干净。
- **M3 出口验收**：离线部分（jar 校验 → pack → 解析栈 → 图集 → 出图）已复现；联网部分（`launch` 全链路 + 客户端进程启动）待网络恢复后复跑（R-017）。
- **文档校准（2026-10-29）**：STATE / traceability / docs README / modules README / render.md / assets.md 与实现对齐；去掉 `Auth → Framework` 死引用并补架构门禁；帧输出改名 `sharpcraft-frame.png`。
- **T-043 完成（2026-10-29）**：`tools/Sharpcraft.Docs.Indexer` 接管 11 个目录 README 的索引段；`--check` 与 `--check-coverage` 进 CI 与架构测试。覆盖单位是**源码目录模块**（26 个），基线 `docs/src/.coverage-baseline.txt` 只允许缩小。AGENTS.md 3.5 / 3.6 已按项目所有者 2026-10-29 的明确授权写入对应规则。

## 已完成（近期，按时间倒序）

1. T-040 模型驱动取纹理：`blockstates` → 模型 `parent` 链 → `#变量`，真实覆盖率 682 → **1019/1286（79%）**。
2. T-039 基线 `BlockStateLayout`：1,286 方块 / 35,723 状态，真实 state id 可解析为方块名。
3. T-037 纹理通路：图集拼装 + 顶点格式 12/13 float + 着色器采样；修掉"顶点颜色乘状态哈希色"与"UV 未做半纹素内缩"两个只有出图才暴露的 bug。
4. T-036 多账号与离线闸门（原则 7.1）：闸门关闭路径实机验证；未伪造任何正版账号记录。
5. T-035 `launch` 串起校验 → 打包 → 挂载 → 认证 → 装配 → 启动；令牌走会话文件不上命令行。
6. T-031 贪婪合并：真实世界三角形 110,868 → 51,402（-54%）。

## 本轮工程治理补充（2026-10-29）

- **新写作规则**：仓库文档不得记录本机环境问题（网络故障、代理、本机路径、某次本地失败）；只写"换一台机器仍然成立"的经验。规则落在 `docs/README.md` 的写作规范里，并已按此清理 `docs/STATE.md`、`docs/risks.md`、`docs/modules/*`、`docs/analysis/**` 中的环境叙述。
- **工具集边界**：`Sharpcraft.Docs.Indexer` 属于工程工具，新增门禁断言"产品程序集不得引用它、它也不得引用任何产品程序集"，避免工具成为绕过分层的后门。

## 2026-10-29 认证续期补齐

- **T-044**：会话令牌缓存补齐。续期顺序改为 有效会话（零请求）→ 直连续期（不走 XSTS）→ 微软授权 + Xbox 链 → 交互登录；
  微软授权在 Xbox 链**之前**落盘。
- **T-045**：HTTP 代理支持。此前 `ConnectCallback` 会绕过代理，导致代理环境完全不可用。
- **R-018**：`/authentication/refresh` 的请求体契约仍属未实测假设，首次成功登录后必须复核。

## 2026-10-29 设备码体验与文档覆盖

- **T-046**：设备码自动开浏览器并预填码（`SHARPCRAFT_NO_BROWSER` / `SHARPCRAFT_BROWSER` 可控）。此前只打印在控制台，
  15 分钟一过就白费。
- **文档覆盖棘轮**：首个源码模块文档 `docs/src/sharpcraft-client/assets.md` 落地，基线 26 → 25。
- **索引器修正**：`docs/src/**/README.md` 是生成索引，不再被判为"不对应源码模块"（此前 `docs/src/` 为空，问题未暴露）。

## 2026-10-29 M3 出口达成

- `launcher frame <address>` 首次产出**真实服务器带纹理画面**：9/9 区块、275,142 方块、51,402 三角形、
  图集 1024²/1301 精灵、覆盖率 1021/1286。
- 本地与真实服务器两条路径都出带纹理 PNG，T-037 关闭。
- 会话令牌缓存生效，第二次运行不再要求设备码（T-044 证据补齐）。
- 剩余视觉缺口集中在非立方体方块（T-041，265 个），已计入里程碑后续项而非出口条件。

## 2026-10-29 窗口闭环

- `launcher window <address>` 首次在真实服务器上**呈现带纹理的窗口**（960×540，交换链 `BGRA8UnormSrgb`），
  同时写出同视角 PNG。M3 出口条件全部满足。
- 修掉两个只在 GPU 上暴露的缺陷：窗口管线缺 UV/精灵矩形属性、窗口渲染器从不绑定图集。根因是顶点布局两份拷贝，
  现已收敛为 `VertexLayout` 单一来源并由测试守护。
- 账号登记补齐：此前只有 `login`/`launch` 记录账号，`frame`/`window` 登录后不记录，导致离线闸门仍锁着的矛盾状态。
  现在任何成功登录路径都会刷新账号列表。

## 2026-10-29 合规缺口修补（一）

- **原则 2 模组热加载**：落地最小可用宿主（`ISharpcraftMod` / `IModContext` / `ModLoadContext` / `ModHost` / `ModHandle`），
  引用纪律与收集器语义有自动化测试；**ALC 的 GC 回收在本机观测不到**，已写成独立探针并登记 R-019，不得声称"热卸载已验证"。
- **原则 12 最小复现**：`tests/*/Repro/` 落地 5 个用例，并在写第一个用例时抓到"窗口渲染器仍自持顶点布局"的真实回归。
- **原则 7.1**：宪章按所有者裁决改为"约束启动器、客户端只被如实告知"，ADR-0009 记录，风险条目按裁决不写。

## 2026-10-29 合规缺口修补（二）

- **原则 11 着色器包接入点**：落地契约层（`TargetSize` / `RenderTargetDescription` / `PhaseDescription` /
  `ShaderPackDescription` / `ShaderPackValidator`），校验是纯函数、失败给出可诊断项、内置管线用同一份数据表达，
  因此"回退"是替换而不是第二条代码路径。Iris 具体格式仍未定稿（`[待定]`，见规格 §6）。
- 尚未接线到执行器：契约层目前被测试覆盖，但渲染器还没消费它（下一轮把 `OffscreenRenderer`/`WindowedRenderer`
  改成按包描述执行）。

## 2026-10-29 M4 第一切片：玩家物理

- `PlayerBody` 落地（`docs/modules/player.md`）与 9 项测试。修掉了两个真缺陷：落地悬空 0.055 格（拒绝整步而非钳制到接触面）、
  跳跃只有 0.91 格（先施加重力再移动，正确顺序是先移动再积分）。
- 物理常量标记 `[待实测确认]`：数值来自公开行为观测，尚未用对照实验复核，测试只断言量级与关系。
- **下一步**：`WindowHost` 还没有键盘/鼠标事件面，输入层无法接线。顺序是
  ① `WindowHost` 暴露按键集合 + 鼠标相对位移 + 捕获/焦点事件（`Framework.Platform`）；
  ② `Client/Input` 把物理键映射为动作并产出 `FrameInput`（配单元测试，不需要 GPU）；
  ③ 正式帧循环按时间步进调用 `PlayerBody.Tick`，相机取 `Eye`；
  ④ 手动验收：窗口里能用 WASD 走动、鼠标转视角。

## 2026-10-29 M4 范围修正（所有者）

- 原版中玩家是实体系统的一员，因此 M4 改为**实体系统根基 + 输入输出系统**，不再把"玩家物理"当特例。
- `docs/modules/entities.md` 落地：实体尺寸/眼高、重力 0.08、输入摩擦 0.98、跳跃 0.42、水平空气阻力 0.91、
  方块摩擦默认 0.6，以及 `Travel` 的真实顺序与**摩擦影响的速度系数 `0.21600002 / f³`**。
- 关键结论：地面移动是"输入累加到速度 + `f × 0.91` 阻尼"的加速模型，**不是**直接赋值速度；
  当前 `PlayerBody` 原型在这点上不等效，迁移时必须按公式重写。
- 实体层归属 **`Common`**（客户端预测与服务端权威共用同一算法），`Client` 只保留输入与相机。
- 输入输出系统口径见 `docs/modules/input.md` §2（音频/HUD 本阶段只留接口），若所有者要别的口径需改那节。

## 2026-10-29 等效性登记与输入输出系统

- **R-020 已登记**：移动/实体行为尚无行为对照实验，测试是自证的。所有者裁决：等系统成体系后再统一验证等效，
  在此之前不得声称已等效。三层验证协议写在 `docs/src/sharpcraft-common/entities.md` §6.1。
- 下一步：输入输出系统（`WindowHost` 键鼠/焦点事件面 → `FrameInput` → 固定 20 Hz tick 循环）。

## 2026-10-29 输入输出系统（一）

- `Silk.NET.Windowing` 2.x **没有**键鼠事件面（实测：`IWindow` 无 `KeyDown`/`MouseMove`/`Input`），
  因此改用其后端已依赖的 GLFW 回调（`IWindow.Native.Glfw`），不新增原生库；键码在 `WindowHost` 内翻译为平台无关的 `InputKey`。
- `Client/Input`：`KeyBindings`（不可变、原版默认布局）+ `FrameInput`（readonly struct）+ `InputMapper`，7 项测试，零分配。
- 下一步：固定 20 Hz tick 循环接上 `PlayerEntity`，相机取眼位，标题显示 FPS/坐标；然后手动验收 WASD + 鼠标。

## 2026-10-29 交互循环（1–3 完成）

- **相机矩阵搬到 GPU**：顶点改为世界空间（步长 48），视图投影矩阵进 uniform（64 字节/帧）。
  每帧重传 8 MB 顶点不可行，这是能动的前提。修掉一次多余转置（画面缩成一个小点，由数值测试定位）。
- **固定 20 Hz tick 循环**：TickLoop（单帧补跑上限 5，防死亡螺旋）+ CameraController（yaw/pitch，pitch ±89°）。
- **交互会话**：Launcher/InteractiveSession 把窗口输入、实体、相机串起来；标题显示坐标与落地状态；Esc 释放鼠标捕获。
- 手动验收：`MC_FRAME_LOCAL=1 window local` 应能 WASD 走动、鼠标转视角；接服务器用 `window <address>`。

## 2026-10-29 插值与同步

- **视角/位置插值完成**：`PlayerInterpolator` 在 tick 之间插值眼位（yaw 走短弧），传送时 `Snap` 丢弃旧样本。
- **移动同步规格就绪**（`docs/specs/network/play-movement.md`）：包 id 用三条已实测锚点交叉校准（`MovePlayerPos=30`、`AcceptTeleportation=0`、`Login=50`），`PlayerPosition=71`；字段布局照分析产物记录；吸附/平滑阈值 2 格标为待实测。
- 验证环境：本地服务端 **offline-mode**，官方 Fabric 客户端含 MCP 服务（`26.3-snapshot-8-Fabric 0.19.3`）。离线服务器不影响移动包格式，同步可完整验证。
- 下一步：实现 `PlayPackets` 的移动包构建/解析 + `PlayerSync` 的发送决策与偏差分级，再接进 `PlaySession` 收发循环。

## 2026-10-29 移动同步已实现

- `PlayMovementPackets`：四种移动包 + `AcceptTeleportation` + `TryReadPlayerPosition`，字段顺序照分析产物；`Relative` 位按**偏移**处理（当成绝对坐标会把玩家传到错位置）。
- `PlayerSync`：发送决策（位置/朝向/仅标志/不发）、1/4096 量化、传送吸附、小幅偏差每 tick 修正 20%、超过 2 格吸附。
- 已接进 `PlaySession`（可选 `player` + `orientation` 参数）：收到 `PlayerPosition` 时吸附并回 `AcceptTeleportation`，每收一包顺带发一次移动。
- **12 项测试**覆盖全部路径。待做：连本地 offline-mode 服务端实测（发包是否被接受、是否被 `moved too quickly` 拒绝）。

## 2026-10-29 真实服务端实测（阻塞）

- `walk 127.0.0.1:25565`（新命令）：登录、配置、进 play 全部通过；收到 `PlayerPosition`（**id 73**，实测确认）并吸附 190.32 格后回 `AcceptTeleportation` 成功。
- **阻塞**：发出 2–3 个 `MovePlayer*` 后服务端直接关闭连接（无 disconnect 包）。改发纯 3 字节的 `StatusOnly` 同样被关 → **与坐标无关，是包 id 或编码问题**。
- 已修正：`CommonPacketTypes` 也占链序号导致 keepalive 27 / MovePlayerPos 29 / Pong 44（原表 28/30/45 偏移 1）。修正后仍被拒，见 R-021。
- **需要**：服务端控制台/日志的踢人原因，或用带 MCP 的官方客户端抓一次真实移动包做对照。

## 2026-10-29 移动包被判非法的根因（已定位）

- **服务端权威 id 表已探明**（`docs/specs/network/play-movement.md` §6.1）：逐 id 发超长包体，服务端解码报错会点名包名。据表修正 `keep_alive=28`、`move_player_pos_rot=31`、`pong=45`。
- **已验证**：登录/进服、收 id 73 传送并回应、连接可稳定保持（不发移动包时 4 秒无异常）、`PosRot` 包体布局正确（探针显示服务端恰好读走 33 字节 + 1 标志）。
- **根因**：`walk` 的玩家踩在虚构平地（`y<0` 为实体），服务端世界在 y≈190 有真实地形；服务端按我们的增量自己重算一次，偏差超过 0.25 格即判非法。**不是坐标或 id 问题，是世界问题**。
- **下一步**：`walk` 改用服务端下发的区块构建真实世界（`frame` 已有此能力），玩家放到传送包位置，再测。

## 2026-10-29 移动同步实测（第二轮）

- **`walk` 已改用真实区块世界**：先收集区块再行走；玩家物理正确（`ground=True`，沿 −Z 稳定移动）。
- **两个节奏 bug 已修**：进服耗时被当成 tick 积压导致一帧爆发；以及未按服务端 tick 节流。
- **仍阻塞**：持续发位置包约 3 个后连接结束，服务端日志 `lost connection: Disconnected`，**无原因**。
  已排除：id 错、包体布局错、NaN 坐标、同 tick 多发、世界不一致。
- **下一步**：抓官方客户端 serverbound 序列对照，或服务端开 `--debug`。`MC_PROBE_ID` 与
  `PlayPackets.BuildClientInformation` 已备好作为实验基础。

## 2026-10-29 移动同步已在真实服务端验证通过

- **根因**：26.3 要求每个 tick 结束发送空的 `client_tick_end`（id 13）。服务端只在
  `handleClientTickEnd` 里重置 `receivedPositionThisTick`，不发该包则第二个位置包必被判
  `invalid_player_movement`。
- **实测**：`walk` 在本地原版服务端连续行走 12 秒，ground=True，z 从 7.83 到 −16.48；服务端多次下发
  位置修正，`PlayerSync` 正确吸附。**协议层验证通过。**
- **遗留**：本地 tick 偶尔快于 20 Hz，触发服务端 `moved too quickly!` 并回传修正 → 节奏打磨项。
- 新增测试 `TheTickEndPacketIsAnEmptyBodyWithItsId`（13 项移动同步测试全绿）。

## 2026-10-29 等效验证（L2）已跑

- **正式循环已接**：`LivePlaySession` 让窗口期间保持连接，每 tick 发位置包 + `client_tick_end`；实测 live 会话存活 14 秒、零踢人。
- **等效结果**：官方客户端 4.258 格/秒，我们 2.201 格/秒（低约 48%），10 秒 23 次服务端修正 → 协议等效**通过**，手感等效**未通过**，登记 R-022。
- **下一步**：把客户端 tick 固定为严格 20 Hz（单调时钟列队，位置包与 tick_end 成对每 tick 一次），平地上重测；目标差异 ≤5% 且修正≈0。

## 2026-10-29 tick 语义已对齐并重测

- **原版 tick 算法已核对**（`play-movement.md` §7.4）：`DeltaTracker.Timer` 用 50 ms/tick 累加，取整执行、
  小数残差只作渲染插值、单帧最多 10 tick，不靠固定延时凑 tick。我们的 `TickLoop` 语义一致。
- **实测修正**：之前 2.201 格/秒是单次采样被 23 次回拉污染；配对重测为官方 4.291、我们 4.479（+4.4%）。
- **剩余**：tick 速率 19.4–19.8/s（Windows `Task.Delay` 粒度），修正仍偏多。下一步用高精度等待锁 20.00/s 再测。

## 2026-10-29 tick 精度已修，速度对齐

- `TimerResolution`（Windows `timeBeginPeriod(1)`）+ 绝对截止时间 + `PreciseWait` 自旋：tick 速率到 **19.8/s**，
  位移 **4.399 格/秒**（官方 4.291，差 **+2.5%**）。
- **剩余**：服务端修正仍约 25 次/12 秒 → 每 tick 碰撞求解与原生不一致，登记 **R-023**。
- 下一步：分场景（平地/台阶/边缘/斜面）逐 tick 对比我们申报位置与服务端回拉残差，先修最常见的一类。

## 2026-10-29 R-023 已定位到机制

- 原版三道闸门（`play-movement.md` §7.10）：残差 >1/16 格**只记 WARN、不回拉**；只有"目标位置引入新碰撞体"
  才回拉；传送用带 id 的握手，20 tick 未确认则重发。
- 我们日志中**从无 `moved wrongly`** ⇒ `fail=false` ⇒ 每次回拉都来自 `isEntityCollidingWithAnythingNew`，
  即**申报位置让包围盒与新方块重叠**（最可能是贴地钳制留了微小穿透），与残差大小无关。
- 下一步：记录修正前后的目标位置与新旧包围盒重叠方块，确认是否恒为脚下方块；若是则修 `Entity.Move` 贴面钳制。

## 2026-10-29 R-023 已修复

- 根因不是碰撞精度，而是 **未下载区块被当成空气**（`GetBlockState` fallback 0），实体走进服务端有实地的
  区域后被判"新碰撞体"而回拉。
- 修复：`ClientWorld.IsChunkLoaded` + `EntityWorldAdapter.IsSolid` 对未加载区块返回 true（不可通行）。
- 实测：修正 **25 → 2 次/10 秒**，速度 4.3–4.5 格/秒未受影响；新增 `EntityWorldAdapterTests` 3 项。
- 下一步：做一次配对速度复测（官方 vs 我们），把 §7.6 的 +4.4% 更新为修复后的数字。

## 2026-10-29 区块流式加载已实现（R-023 最终解法）

- 按原版语义实现流式加载：`client_information`（26.3 末尾多一个 `particle_status` 字段）声明视距、
  `chunk_batch_received` 逐批确认、`level_chunk_with_light` 边收边并入世界。
- 未加载区块仍按**空气**处理（原版 fallback），不再有"隐形墙"。
- 实测：`walk` 20 秒区块 9 → **405**、修正 190+ → **2**；`window` 12 秒方块数 585,216 → **2,003,968**，无踢人。
- 踩坑记录：漏发 client_information（区块不下发）、少写 particle_status（进服即断）、区块解析传错切片（全部解析失败）。

## 2026-10-29 移动同步模块已收尾（等效通过）

- 最终配对实测：官方 4.281 格/秒、我们 4.443（**+3.8%**，≤5% 达标）；服务端修正 **2 次/15 秒**；
  行走期间区块流式加载到 405 个。
- 判定：协议等效、位移等效、纠偏行为三项**全部通过**。R-022 / R-023 关闭，T-061 / T-063 完成。
- 本模块结束。后续属于 M4 其他部分：相机跟随已接、区块更新重网格化（lock_update/section_blocks_update）仍待做。

## 2026-10-29 速度差异修正为 0.2%

- 用真实时间差重测：官方 4.307、我们 **4.317** 格/秒（理论值 0.2159×20），差 **0.2%**。
- 之前的 +3.8% 是**脚本错误**（位移除以采样点数而非实际耗时，且只剔除我方的加速段）。
- 逐 tick 精度已由 WalkingSpeedPrecisionTests 钉死（0.2159）；斜向同速已由 DiagonalWalkingIsNotFasterThanStraightWalking 钉死
  （原版只在 lengthSqr>1 时归一化，斜向不会更快）。

## 2026-10-29 已采纳服务端下发的朝向与速度

- 查明进服传送包 `ClientboundPlayerPositionPacket` 携带 position + deltaMovement + yRot + xRot，
  原版客户端直接 `setYRot/setXRot` 采纳并原样回执。
- 我们此前只应用位置、忽略朝向与速度，且 `Relative` 只判断 bit0。已改为**逐分量**合并
  （bit0/1/2=XYZ、bit3/4=yRot/xRot、bit5/6/7=速度、bit8=速度随旋转），4 项测试钉住。
- 待办：仅旋转包 `ClientboundPlayerRotationPacket` 尚未处理（T-065）。

## 2026-10-29 仅旋转包已实现（T-065）

- `ClientboundPlayerRotationPacket`（id 74）字段顺序为 `float yRot, bool relativeY, float xRot, bool relativeX`
  ——**布尔夹在浮点之间**，按"两 float 再两 bool"读会整包错位。
- 已实现解析与合并（yaw/pitch 各按自己的布尔决定绝对/相加），接进 `walk` 与窗口两条路径；3 项测试覆盖。
- **端到端已实测通过**：服务端控制台 `rotate Blue_Coast 90 0`，`walk` 日志输出
  `server rotation: yaw=90.00 pitch=0.00`（命令值即 90/0，若字段顺序读错会得到垃圾值）。
  同一次 120 秒运行的稳定性数据：tick 19.9/s、修正 **2 次**、区块 918 个、行走 961.66 格。
- `walk` 回归：8 秒 159 tick、修正 2 次、区块 367，功能未受影响。

## 2026-10-29 跳跃与下落已对齐

- 三个常量核对：起跳 0.42（原版是 `max(jumpPower, y)`，已修正）、重力 0.08、垂直阻尼 0.98。
- **终端速度不是常量**，是 `v=(v−0.08)×0.98` 的不动点 **3.92 格/tick（78.4 格/秒）**，且是**渐近**逼近。
- 实测：官方起跳顶点 1.2492（40 ms 采样）、我们 1.2522（逐 tick）；服务端连跳 10 秒仅 1 次修正。
- 教训：首版测试按"200 格必到终端"断言 ≤3.28，实测 3.3212 —— 代码对、物理前提错，已改为长/短两条断言。

## 2026-10-29 冲刺与潜行已对齐 → 移动等效性完成

- 冲刺 1.3×、潜行 0.3×（`Attributes.SNEAKING_SPEED`）、蹲伏盒 0.6×1.5 眼高 1.27（`Avatar.POSES`）。
- 三档实测对照：行走 4.307 vs 4.317、冲刺 5.623 vs 5.614、潜行 1.296 vs 1.296，跳跃 1.2492 vs 1.2522。
- 潜行此前只有输入动作位、实体层未实现；本轮补上速度、碰撞盒、眼高与 `FrameInput.Sneak` 接线，
  服务端实测潜行/冲刺各 10 秒均只有 1 次修正。
- **移动模块等效性到此收口**：水平（走/冲刺/潜行/斜向）、垂直（跳跃/下落）、朝向、传送、区块流式全部对齐。

## 2026-10-29 区块更新已打通（M4 剩余部分-1）

- 解析 `ClientboundBlockUpdate`(8) 与 `ClientboundSectionBlocksUpdate`(86)，位布局按源码常量实现并测试
  （`BlockPos` X26/Y12/Z26、`SectionPos` X22/Y20/Z22；section 内偏移 **X 在高 4 位**）。
- 变化写入 `ClientWorld`，标记几何脏；窗口下一帧重建网格，`WindowedRenderer` 支持运行时替换顶点/索引缓冲。
- 实测：服务端 `setblock` → 我们 `blockChanges=2`；窗口运行中出现第二次 `mesh:`，方块数 585,216 → 2,003,969。
- **不足**：目前整世界重建（~2M 方块），已登记 T-069 改为按 section 脏标记增量重建。
- M4 剩余：T-069（增量重网格化）、挖方块交互（打破/放置）、相机跟随已有。

## 2026-10-29 M4 验收闭环已打通（挖方块）

- 实现 `ServerboundPlayerAction`（id 41）：`VarInt action / BlockPos / byte face / VarInt sequence`；
  26.3 的 action 枚举与旧版不同（多了 `CHANGE_DESTROY_DIRECTION`，START=0、STOP=3）。
- 实测（创造模式，`MC_WALK_BREAK=1` 每 20 tick 破坏前方脚下那格）：目标 `state=9`（实体方块），
  `blockChanges=13` 回传并应用 → **"走进去 → 挖掉方块 → 地形变化"闭环成立**。
- 注意：`handlePlayerAction` 受 `hasClientLoaded()` 门控，进服最初约 3 秒内的交互会被忽略。
- 剩余：T-069（按 section 增量重网格化，替代整世界重建）。

## 2026-10-29 增量重网格化已落地（T-069）

- `ClientWorld` 脏区块集合（边界方块同时脏相邻区块）；`ChunkMeshCache` 每区块一份网格，只重建脏/新区块后拼接。
- 实测（329 区块）：整世界 13.8 s → 单点改动 **1 个区块 ~50 ms**（约 270×）。
- 区块流入即标记几何脏，窗口按批增量重建（`remesh:` 日志可见）。
- 遗留 T-071：单区块 37–50 ms、一批 80 个新区块仍会卡约 3 s，下一步按 section 细分并提速。

## 2026-10-29 帧率退化已修（常驻缓冲 + 快照无锁读）

- 贴图半像素：`SpriteAtlas` 是"纹素中心到中心"，着色器改为按纹素中心采样，整块面恢复 16 纹素。
- GPU 崩溃：销毁仍被提交引用的顶点/索引缓冲会 panic（报的是 swapchain 信号量，具有误导性）；
  改为常驻缓冲 + `Queue.WriteBuffer`（需 `BufferUsage.CopyDst`）。
- 卡顿：后台网格化曾长时间持世界锁 → 主线程加区块要等 40 ms。改为 `ConcurrentDictionary` +
  **不可变快照数组**，后台无锁读，且保持热路径零分配。
- 实测（25 秒）：最坏帧 7089.9 ms → **82.2 ms**，4443 帧（约 178 fps）。

## 2026-10-29 多线程区块加载已落地

- `ChunkWorkerPool`：一个池、两个队列（解析优先、网格化其次），worker 数 `clamp(ProcessorCount-1, 1, 8)`，
  构造函数参数可配置；世界只由主线程写，解析结果每帧限量安装 8 个；解析队列满时主线程就地解析。
- `FindChunk` 由线性扫描改为 `ClientWorld.GetChunk` 字典查找。
- 实测（25 秒）：worker 8（共 20 逻辑核），最坏帧 **82.2 ms → 42.9 ms**。
- 后续：worker 数接配置、网格化按 section 细分。

## 2026-10-29 几何竞技场已落地（放大化存档压力测试）

- 放大化存档暴露：全量重建/上传的代价正比于**总几何量**（约 36 MB/帧），主线程 42.3 ms/帧、20 秒上传 15.3 GB。
- `ChunkGeometryArena`：常驻顶点/索引缓冲 + 每区块一段，索引保持 0 基、用 `baseVertex` 绘制，
  追加时不动他人索引；局部更新原地改写；容量不足按倍增整体重写（20 秒 7 次）。
- 实测：主线程重建 **422 次 → 0 次**，上传 **15,286 MB → 1,040 MB**，帧数 422 → **3524**。
- 已知尖峰：7 次倍增各约 88 ms，改法是按 view distance 预分配容量（T-081）。

## 2026-10-29 竞技场预分配已完成（T-081）

- 容量决策抽为纯函数 `ArenaSizing.NextCapacity`：启动按 `view distance × 96 KiB` 估，满 16 个区块后按实测均值 ×1.3 预分配一次。
- `expectedChunks = (2×8+1)² = 289`，由客户端 `ViewDistanceChunks` 常量推导。
- 实测（放大化，20 秒）：整体重建 **7 → 2**，上传 **1278 → 588 MB**，最坏帧 **88.8 → 78.1 ms**。
- 剩余 2 次来自样本均值偏低与个别超大山区块。

## 2026-10-29 区块空洞已修（解析失败不再丢整块）

- 现象：个别区块永久空洞。计数器证据 `parsed 312 ok / 10 failed`，被拒的包服务端不会重发。
- 定位：地形段（24 个 section）用 dump 精确复刻验证**完全正确**；`Invalid` 出自**方块实体**段，
  失败点落在 compound 内部字段（`MaxNearbyEntities`，刷怪笼）——上一实体 NBT 少消费字节导致错位。
- 修正：方块实体走不下去时**保留区块**（地形可用），只丢方块实体与光照段，并用 `BlockEntitiesSkipped` 标记。
- 实测：失败 25 → **1**；全量测试绿；新增 `Repro_007` 两项回归（合成包，不提交服务器数据）。
- 遗留 T-084：剩余 1 例属另一种错位（NBT 起点落在字符串中间）；诊断静态字段需按线程隔离。

## 2026-10-29 方块实体 1 字节错位已修（T-084）

- 根因：`OPTIONAL_COMPOUND_TAG` 在 26.3 **没有布尔标志**，"没有 tag" 就是一个 `0x00`（End）；
  我们多读了 `TryReadBool`，把 NBT 的类型字节吃掉 → 每条方块实体记录错位 1 字节。
- 25 例与随后 1 例是**同一根因**的两种表象（`root tag Short` 与 `"onents"`）。
- 修正：`TryReadNetworkRoot` 把 End 视为"无 tag"；`SkipBlockEntity` 删除布尔。
- 实测：解析失败 25 → 1 → **0**（328 个包全通过），光照段恢复正常，缓冲区整体重建降到 1 次。

## 阻塞项

- 无硬阻塞。
- **R-017**：XSTS 端点间歇 RST，`launch` 的联网验收待复跑（已加有界重试、IPv4 竞速、显式 HTTP/1.1、`MC_TRANSPORT_DEBUG=1`）。
- 验证环境若未安装官方 `assets/objects`，语言/音频走索引的探针会跳过（只警告，不阻断）。

## 换机继续开发（交接清单）

换到另一台机器只需三件事，仓库里没有绑定任何本地路径或产物：

1. `git clone https://github.com/a1544676809/Sharpcraft` + 安装 **.NET 10 SDK**（`global.json` 固定 `10.0.100`，`rollForward: latestFeature`）；
2. `dotnet build Sharpcraft.slnx && dotnet test Sharpcraft.slnx`（会还原 `Brew.Wgpu` / `Silk.NET.Windowing` / `DnsClient`）；
3. 把**你自己的** 26.3 jar 路径传给启动器（`launcher assets <jar> <json>`），产出 pack 落在仓库外的用户数据目录。

不在仓库、也不需要在仓库里的东西：分析工作区（`../mc-26.3-analysis`，仅用于文档里的 `ref://` 证据指针）、资产 pack（`%TEMP%` 或 `%LOCALAPPDATA%`）、令牌缓存与账号文件（用户数据目录）、抓包回放文件。

## 下一步具体动作

1. **Phase 2 文档**：按 `docs/src/` 的 26 个模块补 L2 模块文档（`src/Sharpcraft.Client/Render/` → `docs/src/sharpcraft-client/render.md`），每篇 40–80 行，写完一个模块跑一次 `--update-baseline`。建议从 `Common/Network`、`Common/World`、`Client/Render` 开始。
2. T-042 `registry_data` → `BlockStateLayout` 动态装配（规格已就绪，见 `docs/modules/render.md` §6.1）；当前真实服务器路径仍用基线快照。
3. T-041 特殊渲染方块（水/岩浆/火把/箱子/活塞等 267 个）：先写流体与实体渲染的行为规格，再实现。
4. 网络恢复后复跑 `launch <jar> <version-json>` 全链路，补 M3 出口的联网证据。
5. 可选并行：定稿 `docs/specs/quality-attributes.md` QA-1 的阈值（候选值见 `performance-baseline.md` §5）。

## 未提交改动 / 后台任务

- 无后台任务。187 个提交未推送（等所有者指示）。

## 最近提交

- `9a40a4e` refactor: rename the project to Sharpcraft
- `963b835` feat(client): resolve face textures from blockstates and models
- `512f15d` feat(render): resolve state ids to blocks from the baseline registry layout
- `2c64044` feat(render): sample the sprite atlas so blocks show their real textures

## 2026-10-30 方块实体接入渲染路径（T-242）

- `BlockEntityRecord` 归入 `Common/World`，`LevelChunk` 存储；`ChunkPacketParser` 解析时保留 `(LocalX, Y, LocalZ, TypeId)`
  （packedXZ 为**一个字节**、高半字节 X / 低半字节 Z；Y 为**绝对 short**；type 为 VarInt——已按参考核实）。
- `BlockEntityPlaceholderMesher`：箱子 14/16、其余 12/16，落在方块面 +1/16；几何并入主顶点/索引数组。
- **实测**：`block entities: 1 boxes, 12 triangles`；与「关闭方块实体」帧比对 **329 像素不同、首个 (461,68)、最大通道差 166**。
- 两个真坑：方块实体原先埋在实心墙里；合并后的索引**不在任何 draw range 内**（帧完全一致），补 range 后才可见。

**下一步具体动作**：①补方块实体**数据包级**解析用例（T-240 收口）；②半透明**按距离降序**绘制 + 混合公式校验。

## 2026-10-30 纠错：混合从未启用（T-243）

- 实测：`alpha 0.7` 与 `alpha 1.0` 的探针像素**完全一致** ⇒ 透明层其实是不透明绘制。
- 根因：`CreateTranslucentPipeline` 里没有混合状态；第 29 轮的字符串替换**静默未命中**，我没回读就记成完成。
- 已加 `fs_translucent`（alpha 0.7）入口备用；`ColorTargetState` 只有单参构造、`BlendState` 名称不存在，需查绑定的真实混合类型。
- **下一步具体动作**：从绑定查出混合状态的正确类型/字段，启用混合，以 alpha 0.7 与 1.0 必须不同为验收；随后做按距离降序绘制。

## 2026-10-30 混合真正启用并数值验收（T-243 闭合 / T-244）

- 反射绑定得到真实形状：`ColorTargetState` 结构体（`Format`/`Blend`/`WriteMask`），`Blend` 是可空 `WGPUBlendState`（color+alpha 组件）。
- 透明管线：颜色 `SrcAlpha/OneMinusSrcAlpha`、alpha `One/OneMinusSrcAlpha`。
- **验收**：alpha 0.7 与 1.0 像素不同；且 `0.7w+0.3bg` 逐通道误差 ≤1（(324,114) 预期 (51.0,62.1,84.9) 实测 (51,62,85)）。
- **下一步具体动作**：半透明按区块距离**降序**绘制（`TranslucentOrder` 已就绪），再做最终复测。

## 2026-10-30 半透明按距离降序（T-245）

- 逐区块记录透明区间 + 中心，按到相机距离降序排序，渲染器逐段绘制；实测 `translucent ranges: 1, far to near`。
- 未启用透明层时主回放帧 `identical`（回归通过）。
- **未证明**：排序对画面的影响（场景只有 1 个透明区块）。要证明需两个重叠透明层。
- **下一步（最后一轮）**：全量复测 + 目标状态裁决。

## 2026-10-30 第 40 轮：最终复测与判定

六项指标全部重跑并复现（确定性 identical、金字塔 9 级全 0、运动矢量 0 误差、剔除 12/20 且 0 误剔、
半透明 10 三角形 97,344 像素且 alpha 0.7 与 1.0 探针不同、方块实体 329 像素）。
判定写入 `docs/milestones/midterm-goal.md`：**目标未达成**，保持 active。

**下一步具体动作（按顺序）**：
1. 方块实体**数据包级**解析用例（构造一段含 BlockEntityInfo 的区块包，断言坐标/类型）
2. 半透明**排序效果证明**：两个相互重叠的透明层，乱序 vs 有序像素必须不同
3. 实体**真实模型与纹理**（需 jar 提取模型 JSON/贴图；当前是 6 盒体占位）
4. 天空**天体**（日/月/星，常数几何，可出像素证据）
5. 剔除率余量：更大世界下重测，避免停在 60.0%
6. 上述收口后，再进着色器包接入

## 2026-10-30 最终全面验证

- 构建 **0 warning / 0 error**；测试 **1074 通过 / 0 失败**；文档门禁 ok；仓库无资产、无绝对路径；工作区干净。
- 六项测量全部重跑（确定性 identical、金字塔 9 级全 0、运动矢量 0 误差、剔除 12/20 且 0 误剔、半透明 10 三角形 97344 像素 + alpha 对照、方块实体 311 像素）。
- **原版等效性**发现了真偏差：箱子占位抬高 1/16（参考 `ChestModel` 贴地）→ 已修正，证据 329→311。
- 两次"假异常"记录在案：①剔除 0/20 是我漏了 `MC_FRAME_DEPTH=1`；②回归 14168 差异是我漏删 `MC_FRAME_CAM`。**测试条件写错会伪造结论**，与 `Replace` 静默失败是同类风险。
- **下一步具体动作**：方块实体数据包级用例 → 半透明排序效果证明 → 实体真实模型与纹理 → 天空天体 → 剔除率余量（更大世界）。

## 2026-10-30 移除基准用区块缓存挂钩（T-191/T-192 废弃）

- 删除 `Launcher/Benchmarking/ChunkBufferHarness`（`MC_DEV_BENCH`/`MC_BENCH_BUFFER`/`MC_BENCH_DELAY`/`MC_BENCH_ARM_FILE`）及其 3 项测试；
  `InteractiveSession.InstallChunk` 回到"收到即装入"。
- 该挂钩会让客户端**收到 arm 指令前不安装任何区块**，属于改变正常加载行为的临时测量代码，不应留在树里。
- 文档同步：`quality-attributes.md` 测量方法去掉开关、`benchmarks/chunk-mesh/README.md` 改为以传送时刻计时、模块文档 `docs/src/sharpcraft-launcher/benchmarking.md` 删除。
- **下一步具体动作**：重启窗口客户端验证半透明（`MC_FRAME_WATER=1 MC_FRAME_TRANSLUCENT=1`），并按用户意见把缺失资产（实体模型/纹理、天体）改为从 jar 提取。

## 2026-10-30 半透明目视验证（T-247）

- 窗口路径此前**根本看不到世界**：本地场景没有玩家位置，相机跟着 y=-722 的默认玩家点 → 只有天空；合成世界也**没有光照数据**（全黑）。
- 修好三处：①本地场景可用固定相机（`MC_FRAME_CAM`），不再跟随会话玩家；②`window`/`frame` 在有本地场景时**允许省略地址**；③新增 `LightData.FullSky`，合成场景补满天光（探针 `15/15`）。
- 另修一个真 bug：`uiBindGroup` 等 4 处 bind group 少给 fog 绑定，窗口一开就 panic（`bindings 3 != layout 4`）。
- **观察到的现象**：几何、光照正常；水柱呈**灰色**而非蓝——原版水贴图是灰度图 + 生物群系水色调制，我们还没接群系水色。
- **下一步具体动作**：①从 jar 取 `colormap/water.png` 与群系水色，按群系调制水贴图；②实体模型/纹理、天空天体同样从 jar 提取（用户已确认这些都能从 jar 拿到）。

## 2026-10-30 真实服务器目视验收

- 真实服务器（NeoForge）窗口出图：草/土/石/雪/树干正确显示，位置与区块网格正常 ⇒ **真实方块可显示**（用户的核心验收点通过）。
- 画面黑色碎斑的成因由所有者确认：**缺 `cutout`（镂空）渲染层** + **缺生物群系着色**（草/叶/水贴图是灰度，靠着色器上色）。**不是我先前猜的 id 错位或图集渗色。**
- **下一步具体动作**：①补 `cutout` 渲染层（按 blockstate/model 的 `render_type` 判定）；②接 biome tint（`colormap/grass.png`、`foliage.png`、`water.png` + 群系颜色）；③再回头收口方块实体数据包级用例与天空天体。

## 2026-10-30 新目标：A→B→C（所有者确认）

- **A** cutout 渲染层 + 生物群系着色 → **B** 收口第 2 条零散缺口（方块实体数据包级用例、天空天体、半透明排序证明）→ **C** 正式测第 5 条性能。
- A 的规格已完成：`docs/specs/render/block-layer-and-tint.md`（先读原版`ChunkSectionLayer`/`BiomeColors`/`ColorMapColorUtil`/`BlockColors`/`calculateBlockTint` 后才写，符合"先查原版再动手"）。
- 关键结论：**层不是按方块名判定，而是按贴图 alpha（部分透明→TRANSLUCENT，纯镂空→CUTOUT）**；着色按模型 `tintindex` 逐顶点、按群系颜色（256×256 colormap，索引 `y<<8|x`），并有 `biomeBlendRadius`（默认 2 → 25 点）平均。
- **下一步具体动作**：A1 —— 贴图 alpha 分析 + 区块网格三层拆分（先做 `Transparency` 与拆层，加单测：镂空贴图进 CUTOUT、部分透明进 TRANSLUCENT）。

## 2026-10-30 A1 完成：贴图 alpha → 渲染层

- `Transparency`（记录 + `Of`/`Or`/`IsOpaque`）、`SpriteAlpha.Compute`（按 sprite 矩形扫 alpha：0 → 镂空，0<α<255 → 半透明）、
  `BlockSpriteMap.TransparencyOf`（把方块各面贴图的透明度合并）、`BlockLayerRules.Of`（名称表 force-translucent 优先，其次按透明度）。
- `ChunkMesher` 拆出第三个 `cutout` 缓冲，面出口按层路由（`BlockLayerRules.Of`）。
- 7 项新测试；全量 **1078 通过 / 0 失败**，构建 0 warning。
- **下一步具体动作（A2）**：①WGSL 加 `fs_cutout`（`discard` alpha<0.5）；②离屏与窗口各加 cutout 管线（不混合、深度写开）；③`FrameRequest.Cutout*` + 两条路径产出 cutout 几何；④真实服务器同角度截图对比树叶不再黑斑。

## 2026-10-30 A2 代码就位但**未通过视觉验收**

- 已加：`terrain.wgsl` 的 `fs_cutout`（`sampled.a < 0.5` → `discard`）、离屏与窗口各一条 cutout 管线（不混合、深度写开）、
  `FrameRequest.Cutout*`、`FrameCommand` 两条路径产出 cutout 几何。构建 0 warning，全量 1078 测试通过。
- **真实服务器实测仍不合格**：黑斑仍在，且画面出现漂浮/破碎几何（截图 `%TEMP%\cutout-view.png`）。
- **原因判断**：分层依赖 `BlockStateLayout` 把 state 映射到方块名；真实服务器是 **NeoForge 模组服**，我们用的是**基线布局**，
  映射错位 ⇒ alpha/层判据建立在错误方块上。**这不是 A2 的算法问题，而是数据缺失（T-042 `registry_data` 未接）**。
- **下一步具体动作**：①先在**基线场景**（录制回放 / 本地演示）验证 A2：固定相机 A/B，cutout 三角形数 > 0 且树叶不再黑斑；
  ②再做 T-042 服务器注册表装配，然后回真实服务器复验；③再加分层三角形计数日志，便于定位。

## 2026-10-30 第 2 轮：按原版数据核对 cutout 判据

- **更正两条我先前的错误**：①该服务器是 **Fabric、基本无 mod**（我误称 NeoForge/模组服）；②黑斑**不是** state→方块映射错位。
- 从资产包核对：`models/block/leaves.json` **无 `render_type`**；`oak_leaves.png` 有 **84 个 alpha=0、0 个部分透明** ⇒ 原版就是"按贴图 alpha 判层"⇒ **A1 判据正确**。
- 已把判据顺序改为**alpha 优先、名称表兜底**（名称表只覆盖"贴图不透明但声明镂空"的模型，以及无图集时）。
- 观测：真实服务器 `cutout 4256 tri / translucent 1214 tri`；树叶仍呈**棋盘格** ⇒ 属 **sprite 解析/UV 取样**问题。
- **下一步具体动作**：①给 `BlockSpriteMap.ResolveFaces` 加"未命中→missingno"计数日志；②按 `FaceBakery` 的 UV 推导与图集 padding 逐项对照，修正 UV/取样；③修好后用真实服务器同角度截图验收（树叶应为带孔洞的绿色贴图）。

## 2026-10-30 第 3 轮：黑斑消失（mip 链 + 连续 UV）

- 原版对照确认：`CUTOUT_TERRAIN` 与 `SOLID_TERRAIN` 同一 snippet，仅多一个 `ALPHA_CUTOUT=0.5` 定义；图集拉链时带 **maxMipmapLevels**。
- 我方两个致命差异：①图集 `MipLevelCount=1`、采样器 `Nearest/Nearest/Nearest`、`LodMaxClamp=1`；②着色器把 UV **吸附到单个 texel**。
- 已修：新增 `AtlasMipChain`（4 级、2×2 均值，含 alpha）、上传各级、采样器 Linear/Linear/Linear、`LodMaxClamp=4`；UV 改为
  `atlasUv = rectMin + frac(uv) * (rectMax - rectMin)`。
- **实测（真实服务器同角度截图）**：原"黑斑/棋盘"消失，树叶呈现带孔洞的贴图块。
- **下一步具体动作（A3）**：生物群系着色 —— `colormap/{grass,foliage,water}.png` 载入、群系 temp/downfall、`tintindex` 逐顶点、25 点平均；
  验收：树叶变绿、草顶分层、水变蓝。

## 2026-10-30 第 4 轮：A3 基础件 + 两个真 bug

- 新增 `Colormap`（原版 `y<<8|x` 索引，已核对 `ColorMapColorUtil`）、`BiomeTint`（草/叶/常绿/白桦/水，参考常量），
  `BlockTintTable`（草方块只染顶面，叶/植物染所有面），网格里按 `tintindex` 近似逐面上色。6 项测试。
- **修掉两个真 bug**：
  1. `ChunkMeshCache` 原来把**所有方块**（含树叶、水）都写进实体网格，我们又在 cutout/translucent 里画一遍 ⇒ 双份几何，
     实体网格里的树叶透明像素显示为黑。现加 `LayerSinks`，按层分流。
  2. sinks 曾在每次 `BuildRequest` 结束时 `Dispose`，但 cache 长期持有引用 ⇒ 后续增量网格写入已释放缓冲，**地形大面积消失**。
     现改为与 cache 同寿命（在 `WindowCommand` 创建）。修复后地形恢复（真实服务器截图）。
- **仍不对**：树叶仍呈**黑白棋盘**（不是绿），草色也偏灰。下一步必须**逐项证明**是哪一环：
  ①用单方块探针场景（真实图集）打印 `map.ForState(oak_leaves)` 的 sprite 矩形是否等于 missingno；
  ②确认顶点色与 tint 乘子是否真的写进顶点（可 dump 一个 quad 的 12 个 float）；
  ③再用真实服务器截图对比。

## 2026-10-30 第 5 轮：定位到 tint 的 ARGB 哨兵 bug（已修）

探针链条（全部实测，不靠猜）：
1. `layer probe`：leaves sprite **(608,256,16×16)**，不等于 missingno (96,400)；`layer=Cutout`、`tint=Foliage` ⇒ 贴图/分层/着色类型都对。
2. `atlas probe`：该矩形内像素均值 **(96,96,96,171)**、首像素 (104,100,104,255) ⇒ 图集里是**灰度树叶贴图**（原版树叶就是灰度，靠 tint 上色）。
3. `vertex probe`：cutout 顶点色 **(0.750,0.750,0.750)**，而 `ToMultiplier(FoliageDefault)` 返回 **(1,1,1)** ⇒ **着色乘子根本没生效**。

**根因**：原版颜色是 **ARGB**，`FoliageDefault = -12012264`（0xFF48B518）作为有符号 int 是**负数**，而我把"负数"当成"无 tint"的哨兵，于是所有负色值都被丢弃。
**修复**：只有 `-1` 表示无 tint；其余按 `(c>>16)&0xFF` 等取通道。新增回归测试 `NegativeArgbIsStillATint`（断言 0x48/0xB5/0x18）。

**仍不对**：修后截图树叶**依然**是黑白棋盘，说明着色不是唯一问题；下一个必须证明的点是"棋盘到底来自哪张图"——
即在**同一帧**里用一个已知方块（石头）与树叶比对，并 dump 实际采样的 atlas texel。
**下一步具体动作**：①在同一帧内对石头/树叶各取一个面，打印其顶点 rect 与采样 texel（uv→atlas 坐标）；②若 rect 正确而画面仍是棋盘，则问题在
**mip 采样/alpha 测试**（mip 后 alpha 均值下降会被 `discard` 掉一半），需按原版 `MipmapStrategy` 处理 alpha；③再回截图验收。

## 2026-10-31 第 6 轮：按你选择移植原版 mipmap（覆盖率补偿）

- 依 `MipmapGenerator.java` / `ARGB.meanLinear` / `TextureUtil.solidify` 逐项实现，**按 sprite** 生成：
  透明像素先用最近不透明像素补色 → 每级 alpha 取四邻均值、颜色在**线性空间**取均值 → 有孔贴图每级做
  `scaleAlphaToCoverage`（测量 alpha 测试覆盖率并迭代缩放 alpha，参考阈值 0.5）。
- 架构调整：mip 链在**客户端**生成（因为只有那里知道 sprite 矩形），随 `TextureData.Mips` 交给渲染器上传；
  渲染器只负责按给定层数建纹理与设 `LodMaxClamp`。旧的 Framework 版 `AtlasMipChain` 已删除。
- 途中修掉三处自身错误：给**颜色渲染目标**错误地加了 mip 层（应 1 层，只有图集需要）、`TextureData` 记录体重复、
  以及一个不现实的测试夹具（纯棋盘让覆盖率在 0↔1 跳变，任何实现都无法匹配 0.5，故删除该用例，保留结构用例）。
- **待验证（不声称完成）**：真实服务器截图确认树叶/草色；`scaleAlphaToCoverage` 的匹配精度还需一个**真实贴图夹具**做数值验收。
- **下一步具体动作**：①真实服务器同角度截图（树叶应呈绿色带孔纹理、草顶分层）；②用真实 `oak_leaves.png` 做夹具，
  断言每级覆盖率与第 0 级误差 ≤0.05；③把 A 阶段收尾后进 B。

### 复测（休眠后重跑，2026-10-31）

- 关闭客户端、重新构建后同角度复测：**草方块顶面已呈绿色**（说明 tint 真正生效），土/石纹理正常。
- 树叶仍偏暗且带白色碎点（不是绿色），说明树叶这条链路仍有一环不对：可能是 mip 覆盖率补偿对其影响、或树叶贴图本身偏灰被 tint 压暗。
- **下一步（不猜）**：用一个真实 `oak_leaves.png` 夹具做数值验收 —— 计算第 0 级与各级的 alpha 测试覆盖率，断言误差 ≤0.05；
  同时在同一帧内对比"叶面采样 texel"与期望，再决定是 mip 问题还是 tint 强度问题。

### 2026-10-31 修正认知 + 一处关键遗漏

- **更正**：上一轮我说"草变绿了"是**错的**——侧面那条绿是 `grass_block_side` 贴图自带的，顶面当时仍是灰的。
- 找到遗漏：**worker 池**（`ChunkWorkerPool`）负责生成实体网格，但它的 `MeshChunk` 调用**没有 layout/cutout/translucent/tint**
  ⇒ 实体层的草方块永远不会被着色、树叶也会被重复写进实体网格。已给池加 `Layers`（Layout/Tint），worker 侧把其它层写进**一次性临时缓冲**
  （共享缓冲会被多线程同时写）。
- 修后实测截图：**草方块顶面变绿**（这次是真的，顶面绿色、侧面仍是贴图自带的绿条），高草/植物仍偏灰。
- **下一步具体动作**：①用同一套顶点探针对**植物**（`short_grass`）取一个 cutout 顶点，确认 tint 乘子是否进入顶点色；
  ②若没有，检查 cutout 几何走的 pass 是否拿到 tint；③再用真实 `oak_leaves.png` 夹具验收 mip 覆盖率（误差 ≤0.05）。

## 2026-10-31 第 7 轮：按你定的方案加图集 padding；并修掉一个崩溃

- 采样方案按你的选择执行：**图集 stitch 时每个 sprite 四周留 1 texel 边缘复制（padding）**，着色器回到**连续 UV + 硬件 LOD**。
  mip 链每一级也把边缘复制进 padding 环，保证远处采样不越界到邻居。
- **修掉一个真崩溃（重要）**：`SpriteAtlas.ComputeTransparency` 用了普通 `Dictionary` 作为缓存，而区块网格在 **8 个 worker 线程**上并发调用
  ⇒ `Collection was modified`/corrupted → **device lost**，窗口"未响应"后退出。已改 `ConcurrentDictionary.GetOrAdd`。
  （这也解释了上一轮"灰白糊成一片"和黑色大块——设备已经挂了。）
- 复测截图：草方块顶面绿、石/土纹理正常、白/绿植物可见，但**树叶整体偏黑**、方块边缘仍有**细黑虚线**。
- **下一步具体动作**：①量一个树叶面在屏幕上的实际颜色（截图取样）并与"灰度纹理 × 树叶 tint"预期值对比，判断是 tint 过暗还是纹理取值偏暗；
  ②黑虚线：确认 padding 是否覆盖 mip 各级与 UV 起点（打印一个方块面的 rectMin/rectMax 与相邻 sprite 是否重叠）；③再用真实 `oak_leaves.png` 夹具验收。

## 2026-10-31 第 8 轮：UV 改为覆盖完整精灵（ADR-0015），超平坦场景待验收

- 采纳用户确认的方向：精灵 UV 不再内缩半纹素，改为**覆盖整个精灵**；渗色由 padding 环负责（ADR-0015）。
- 同步更新 `Repro-003`（改为钉住"UV 覆盖精灵 + padding 复制边缘"）与 `SpriteAtlasTests`；全量 7 套通过、0 warning。
- 用户已搭好验收场景：**坐标 2, 190, -11，面朝南**（超平坦草地 + 一排树篱 + 高草）。
- **下一步具体动作**：用 `MC_FRAME_CAM` 固定相机在 (2.5,191.6,-11.5) 朝 (2.5,191,5)，截图与用户给的原版画面逐项比对：
  ①地面方块交界是否还有细黑线；②树篱（树叶）颜色是否与原版一致；③高草色调；④记录像素取样值。

### 2026-10-31 第 8 轮补充：图集/mip 已核对，黑线仍未定位

- **已数值确认（不是感觉）**：图集 padding 正确 —— 树叶 sprite (271,317,16,16) 的四周环分别等于其首/末行/列像素
  （左 (104,100,104)、右 (119,117,119)、上 (104,100,104)、下 (152,153,152)），说明 UV 覆盖 + padding 这条链路已经对了。
- mip 各级改为先由**全局降采样**铺底、再逐 sprite 覆盖，避免空白处留黑。
- 颜色取样（真实服务器截图）：树篱均值 **(23,38,18)**、地面均值 **(59,87,52)** —— 与你给的原版画面"看着差不多"一致，颜色不是当前问题。
- **仍存在**：平地上一圈 **1~2 像素、纯黑 (0,0,0)** 的方格线。纯黑说明不是贴图（贴图偏暗会是深绿），更像几何缝隙/深度问题，需要 GPU 侧证据。
- **下一步具体动作（需用户工具）**：用 RenderDoc 抓一帧，检查黑线处是①没有三角形覆盖（缝隙）还是②被深度测试挡掉；
  或在本机 VS2026 里附加调试。请用户确认用哪种方式。

### 2026-10-31 第 8 轮结论：CPU 侧网格无缝隙，黑线在 GPU/呈现侧

- 新增 `Repro-005`：16×16 平坦平面，按**索引缓冲**逐三角形求面积 —— **上表面 256 / 下表面 256，与 16×16 完全一致**
  ⇒ 合并后的面**严格铺满**平面，黑线不是"CPU 网格漏面"。
  （第一版用固定步长遍历顶点得出 128 的结论是错的，已改为按 index 计算并记录。）
- 图集 padding 加宽到 **2 纹素**（原因：UV 正好落在 sprite 边界时，双线性会读 padding 与"下一个纹素"，1 纹素 padding 时那个纹素属于邻居）。
- 复测：黑线仍在，且地面出现**成片淡蓝空洞**（应为天空透出）⇒ 需要 GPU 侧证据。
- **下一步**：按用户建议用 RenderDoc 抓帧（`renderdoccmd capture`），确认黑线/空洞处是①无 draw 覆盖、②被深度剔除，还是③纹理采样为黑；
  必要时用 VS2026 附加调试渲染线程。

### 2026-10-31 第 9 轮：黑线的排除法结论（结构化上报）

已做实验与结果（同一相机、离屏、960×540、逐像素统计）：

| 实验 | 结果 |
|---|---|
| 逐三角形求面积（`Repro-005`） | 上/下表面各 **256.0**（16×16 完整）⇒ **CPU 网格无缝隙** |
| 黑线像素的深度值 | 与左右邻居**完全相同**（如 0.98035 vs 0.98035）⇒ **该处几何已绘制** |
| 关闭图集（1×1 白贴图） | 黑线**仍存在**（数量不变）⇒ **不是纹理采样** |
| 强制顶点色为 1（`MC_FRAME_NO_SHADE`） | 黑线**仍存在** ⇒ **不是着色/光照** |
| 关闭生物群系着色 | 黑线**仍存在** ⇒ **不是 tint** |
| RenderDoc `renderdoccmd capture` | **未生成 .rdc**（hook 未生效，尽管应用正常出帧）|

⇒ 四个"最可能的原因"全部被排除；黑线是**几何已覆盖但颜色未被写入**这一类现象（深度有值、颜色为黑），
需要在 GPU 帧调试器里看具体的 draw/附件写入。RenderDoc 命令行 hook 失败，需要换方式。

**需要的输入**：①RenderDoc 用 GUI（qrenderdoc）手动抓帧后把 `.rdc` 路径给我；或②允许我用 VS2026 附加到渲染线程；
或③接受把它记为已知缺陷，先推进 B/C。

### 第 9 轮追加：按 pass 隔离（自建"抓帧"）

新增 `MC_FRAME_ONLY=sky|opaque|cutout|translucent` 与 `MC_FRAME_CLEAR=r,g,b`，逐 pass 单独渲染（清屏设为洋红以便识别"未覆盖"）：

| 只渲染 | 纯黑像素 | 细黑线（1–3px run） |
| --- | --- | --- |
| sky | **0** | 0 |
| opaque | **56,775** | 22 |
| cutout | 4,170 | 11 |

⇒ 黑线由 **opaque 通道**产生（cutout 也有同类现象）。
进一步：`opaque + 关闭图集（1×1 白）+ 顶点色强制 1` 仍然 **56,775** 个纯黑像素、细线仍在
⇒ **既不是纹理，也不是顶点色/光照**；只剩 **fog uniform / 附件写入** 这两类可能，需要真正抓到那一次 draw 的状态。

**需要用户配合**：RenderDoc GUI 在这台机器上用户可用；请用它抓一帧（F12），把 `.rdc` 路径给我，
我在事件列表里定位"有黑线的那次 draw"并读它的 pipeline state（fog uniform / depth / blend / storeOp）。

### 2026-10-31 第 10 轮：有真实抓帧了（Vulkan，frame 538）

- 用户用 RenderDoc GUI 抓到了 `Sharpcraft_2026.10.31_02.12.26_frame538.rdc`，后端是 **Vulkan**（说明本机 wgpu 走 Vulkan，不是 D3D12，
  这就是我用 `renderdoccmd capture` 无法 hook 的原因——那个 build 的 Vulkan layer 未注册成功）。
- 已把 `.rdc` 转成 `chrome.json`（命令流）与 `xml`（管线状态）：一帧内 1 次 sky `Draw(3)` + ~17 次 chunk `DrawIndexed` + cutout + 2 次 UI。
- **用户观察**：`EID 96` 开始绘制世界时，黑线就已经存在 ⇒ 黑线属于世界绘制阶段（不是 UI/后处理）。
- 关键排除（本次对话内）：用**常量颜色片元着色器**重渲染，纯黑像素 **0** ⇒ 黑线确实由我们的片元着色器写出；
  但要注意：离屏 `frame` 路径只加载了很少的 chunk（408–600 三角形），**与窗口画面不是同一场景规模**，之前的若干对照实验结论不能直接套用。
- **下一步（需用户 30 秒操作，最直接）**：在 RenderDoc 里对黑线像素做 **Debug this pixel / Pixel History**，读出：
  ①哪些 EID 命中该像素；②该 EID 的 shader 输出值。这能一次定死"哪次 draw 写的黑"。

### 2026-10-31 第 11 轮：按面决定分层 + 光照探针（仍未解决黑线）

- 依据用户抓帧的 Pixel History：**EID 96（vkCmdDrawIndexed(42)）的 Shader Out = (0,0,0,1)** ⇒ 黑像素是**片元着色器输出黑色**
  （同帧 EID 91 天空输出 (0.62,0.76,1.00) 正常）。结合 `base = sampled.rgb * vertexColour`，说明该 draw **采样到了黑色的图集纹素**
  或**顶点色为 0**。
- 已做两处修正（都更贴近原版，但**未解决**黑线）：
  1. 分层不再受 `MC_FRAME_TRANSLUCENT` 开关控制（分层是必需行为，不该是可选开关）；
  2. 分层改为**按面**判定（用该面自己的 sprite 的 alpha），对应原版"按材质/quad"而不是"按方块"。
- 光照探针：9 个 chunk 的 `maxSky` 全为 15 ⇒ **不是整块无光照**导致的黑。
- 复测：黑线仍在（窗口下纯黑像素 43,194，采样细线 97 处），并出现**矩形淡蓝空洞**（疑似被剔除/未网格化的区块露出天空）。
- **下一步**：①临时关闭遮挡剔除重测，确认矩形空洞是否由剔除造成；②仍需用户对**细线像素**做一次 Pixel History（上次那个像素在黑斑上）。

## 2026-10-31 第 12 轮：黑线定位并修好（ADR-0016）

- 用 `MC_NO_MIPS=1` 关掉 mip 链 → **黑线完全消失**（用户确认"这个版本正常"）⇒ 根因在 mip 链。
- 原因两条：①逐 sprite 写层级像素时 `x>>(level+1)` 在高 mip **重叠**，后写覆盖先写；②`scaleAlphaToCoverage` 把 alpha 放大到 4 倍，
  半透明 texel 通过 0.5 测试但颜色是"与透明黑平均"的暗色 ⇒ alpha 过、颜色近黑 ⇒ 细黑线，越远越宽。
- 改为**整幅 4 级盒式滤波 + alpha 加权颜色**（ADR-0016）。实测：**纯黑像素 0、细线 0**，与关 mip 一致，且远处仍平滑。
- 遗留：平坦地面上仍有**矩形淡蓝区域**（底部，带高草剪影），疑似水体或未网格化区块；下一步先确认那个位置在原版是不是水。

## 2026-10-31 第 13 轮：用户指出"透明方块下方的面被剔掉"——已修

- 用户判断正确：`TryDescribe` 原来只要**邻居不是空气**就剔掉这一面 ⇒ **水/玻璃/树叶下方的地面被剔除**，
  于是出现"成片矩形空洞露出天空"。已改为只有**真正遮挡**（该方块所有贴图都完全不透明）才剔除；同种方块相邻也剔除（避免水面/树叶内部重复面）。
- 新增 `Occludes(state, sprites, layout)`：用 `BlockSpriteMap.TransparencyOf(block)` 判定是否完全不透明；无图集/无布局时保持旧行为（安全默认）。
- 实测（超平坦场景，朝下看地面）：**地面恢复连续**，矩形淡蓝空洞消失；纯黑像素从 43,194 降到 **649**（剩余为树篱/植物缝隙）。
- 全量 7 套测试通过、0 warning。

## 2026-10-31 第 14 轮：先查原版"透明方块"渲染方式

- 结论写入 `docs/analysis/render/transparent-blocks.md`：
  ①地形管线（solid/cutout/translucent）在原版**都是单面剔除**（`GENERIC_BLOCKS_SNIPPET` 无 `withCull(false)`，默认 cull=true）；
  ②相邻面剔除由各族覆写 `skipRendering`：冰/粉雪=同方块剔除，树叶=同类且关闭镂空树叶时剔除，**流体=同流体剔除**；
  ③**流体不参与方块模型渲染**：`LiquidBlock.getRenderShape = INVISIBLE`，水/岩浆由独立流体渲染器绘制（我们目前缺这条路径）。
- ⇒ "看到背面"在原版**不是默认行为**。若确实需要，属于**有意偏差**，须先明确情形并登记。
- **待用户确认**：想要的是①从外面看玻璃能看到内壁，还是②潜入水中看到水面背面？确认后我再实现并登记偏差。

## 2026-10-31 第 15 轮：树叶"看不见内面"（原版按族 skipRendering）

- 用户澄清：玻璃单面正常，问题是**树叶**——树冠从里面应看得见面。
- 原版依据（已写入 `docs/analysis/render/transparent-blocks.md`）：`LeavesBlock.skipRendering` 只在**关闭精细树叶**时剔除同类；
  默认（精细树叶开）相邻树叶之间的面**不剔除**，所以树冠内部是实的。地形管线本身单面剔除，与这一条并不冲突。
- 实现：新增 `ShouldCullFace(state, neighbour, sprites, layout)`：
  ①不透明邻居 → 剔除；②`*_leaves` → **不剔除**（保留内面）；③玻璃/冰/流体等 → 仅**同方块/同流体**剔除；
  ④无图集/无布局时按"不透明"回退（保持既有 golden 指纹测试的语义）。
- 新增回归 `Repro-006`：3×3×3 树叶，三角面总面积必须是 **162**（27×6，含内部面）；实测通过。
- 注意：无布局时（`ChunkMesher.Mesh` 旧重载）无法读 alpha，故回退为"视为不透明"，真实渲染路径始终带 layout。
- 环境清理：清掉 16 个残留 MSBuild/编译器服务节点；仍有 2 个 `Sharpcraft.Launcher` 进程**拒绝访问无法结束**（疑似用户自己启动或提权进程），
  它们同时占用同一账号连接，导致新客户端 `chunks: 0 ok`——需要用户在任务管理器里结束。

## 2026-10-31 第 16 轮：新加载区块不显示树叶（已修）

- 现象：玩家位置同步正常（与原版同位），但**后到的区块只有树干、没有树叶**。
- 根因：分层接错了数据流。cutout（树叶/植物）被分流到 `LayerSinks` 的一次性缓冲，而窗口下的几何来自
  `ChunkMeshCache` 的**逐区块流式网格**（只画 solid）⇒ 初始批次之外到达的区块，其 cutout 面被写进临时缓冲后**从未被绘制**。
- 修法（等价而非照抄）：把 **cutout 合回实体网格**，在 `fs_main` 里做 **alpha 测试**（`sampled.a < 0.5 → discard`，
  与原版 `ALPHA_CUTOUT=0.5` 同规则），于是树叶与其它方块走同一条流式网格；只有**混合**（水/玻璃）仍需要独立 buffer。
- 实测：同一位置树木**树叶完整出现**（见截图），7 套测试全绿。
- **已知遗留（未修）**：translucent（水/玻璃）仍是一次性构建，**后加载区块的水/玻璃不会出现**；需要在缓存里按区块存
  三层网格并逐帧增量上传，留给后续。

## 2026-10-31 第 17 轮：转入"全量等效性验证"（停止加功能）

- 用户指出：本会话 debug/试验代码不止改了今天的内容，必须做**全量**等效性验证。已建
  `docs/specs/quality/vanilla-equivalence-matrix.md`：15 行特性 × 判据 × 检查方式 × 阈值 × 状态。
- 审计结果：本会话 **226 个提交**，大量直接改可观测行为；目前**只有一部分**有逐像素/逐数值证据。
- 明确列为**已知缺陷**：①新区块不继续加载（`updates 0`）；②半透明层不随区块流式更新；③流体渲染路径缺失。
- 已加"仅在 `MC_DIAG=1` 下输出"的 `stream:` 遥测（loaded/expected/pending），用于脚本断言区块流式加载；不改正常行为。
- **下一步**：写 `tools/verify/verify-streaming.ps1`，先量出当前流式加载的失败曲线作为基线，再修 ①②。

### 验证脚本与首批测量（同轮）

- `tools/verify/check-packet-ids.ps1`：把 `ProtocolConstants` 与**原版注册表 dump**（`packet-tables.txt`）逐条比对 ——
  结果 **checked=90, mismatches=0**（PASS）。这是"有原版依据"的检查。
- `tools/verify/verify-streaming.ps1`：自动走一段（`MC_WINDOW_AUTOWALK=1`），断言 loaded 增长并接近 expected。
  实测：**loaded 17 → 329（expected 289，113.8%）**，说明**区块确实到达**。
- 但同一次运行：`pool: parsed 68 ok, meshed 91`，窗口 `frame ... updates 0` —— **到达的区块大量没有进入网格/上传**，
  这才是"新区块看不见"的真实环节（不是网络层）。
- 另发现：`unhandled packet id=115 = set_time`（原版 id 正确，是我们没实现该包；无渲染影响，登记为待实现）。

## 2026-10-31 第 18 轮：调试代码必须"可一键分离"（用户新要求）

- 规则：**游戏程序集（Framework/Client/Common）不得读环境变量**；所有调试参数由 Launcher 变成选项传入。这样"删掉调试代码"= 删一个目录。
- 新增 `tools/verify/check-diagnostics-separation.ps1`：实测 **18 处违规**（Framework 12、Client 3、Common 3），已列入矩阵第 16 行。
- 回归定位进行中（同一测法，20s，`MC_WINDOW_AUTOWALK=1`）：
  | 提交 | pool meshed | updates |
  | --- | --- | --- |
  | 59fc550（已推送基线） | 2828 | 40 |
  | 0928375（最新代码提交） | 612 | — |
  | HEAD (d1f640a) | 91 | 0 |
  但**样本噪声大**（玩家漫游路线不同）⇒ 先要把探针做稳（固定机位、固定时长、不漫游），再下结论。
- **下一步**：①做稳定探针；②按 options 重构把 18 处违规清零（行为不变）；③再定位流式网格回归。

## 2026-10-31 第 19 轮：AGENTS.md 新增原则 13（用户明确授权）

- 新增「原则 13：调试代码必须易分离，全量移除后不影响游戏运行」（用户直接要求，符合 4.0 的明确授权）。
- 落地要求：调试代码集中在可整体删除的独立位置；游戏程序集不得读环境变量；由宿主层解析后以显式选项传入；
  `tools/verify/check-diagnostics-separation.ps1` + 架构测试断言违规数为 0。
- 当前违规 **18 处**（Framework 12 / Client 3 / Common 3），列为债务：先清零，再让架构测试进 CI 门禁。

## 2026-10-31 第 20 轮：原则 13 落地（18 处违规清零）

- 把游戏程序集（Framework/Client/Common）里 18 处 `GetEnvironmentVariable` **全部**搬走：
  - 新增 `src/Sharpcraft.Launcher/Diagnostics/HostDebugOptions.cs`（宿主层唯一解析点）+ `docs/src/sharpcraft-launcher/diagnostics.md`
  - `Common`：`MinecraftLoginClient.LoginNameOverride`、`PlaySession.ChunkDumpDirectory`；删掉 `PlayerChatPackets` 里遗留的字段追踪
  - `Client`：`PackWatcher.IsEnabled`、`ChatLog.BackgroundOpacity`、`ChunkWorkerPool(..., fifoMeshOrder:)`
  - `Framework`：新增 `FrameDiagnostics`（雾距/深度/金字塔/运动向量/alpha/缩放/输入追踪），`WindowHost.Create(..., guiScaleOverride, inputTrace)`
- 全部为**行为中性**改动：默认值等于"调试关闭"（雾 64/160、缩放自动、其余 false）。
- 自动化：`check-diagnostics-separation.ps1` → **violations=0 PASS**；新增架构测试 `DiagnosticsSeparationTests`（31 个架构测试全过）；全量 7 套 1096 用例通过。

## 2026-10-31 第 21 轮：纯逻辑等效性（不含渲染）

- 盘点结果：仓库**已有**5 份原版快照测试（1286 方块顺序 / 35723 状态 / 1455 状态采样 / 278 包 ID / 238 随机数向量），
  相关 **24 个测试全过**；已写入矩阵 §1.1。
- 新增活体协议检查 `tools/verify/verify-protocol-live.ps1`：连接真实服务器走 20s，断言计数器。实测（**当前为 FAIL**）：
  `ticks=399 corrections=2 distance=217.39 keepalives=0 batchacks=0 survived=True`
  - **KeepAlive 20s 内 0 次**（原版 15s 一次，应 ≥1）→ 待查（收包路径 vs 服务器行为）
  - **20s 走了 217 格**（应为 ~86 格）且 corrections=2 → 位置不稳/被传送，待查
  - batchacks=0 属预期（walk 阶段服务器不发区块；区块批处理由窗口流式路径覆盖）
- 结论：纯逻辑验证的**框架**已就位；这两个数字是它抓出的**真实缺陷**，不是脚本问题。

## 2026-10-31 第 22 轮：更正——上一轮报的两个"缺陷"是我自己的测量错误

- `distance=217`：基准点取成了实体出生占位坐标 (0.5,0,0.5)，把**服务器的 join 传送**算成了"走了 217 格"。
  改为在 tick 循环开始时取基准后：**distance=72.91**（20s）。
- `keepalives=0`：计数器加在了 **join 阶段** 的 switch 分支，20s 循环里的 KeepAlive 分支没计数。
  修好后：**keepalives=1**（20s，与原版 15s 周期一致 ✓）。
- 现在 `verify-protocol-live.ps1` = **PASS**（ticks=399, keepalives=1, distance=72.91）。
- 剩余需判定：72.91 vs 86.3（84%）。可能是起步/传送等待占用的时间，也可能是移动速度实现差异；
  下一步在日志里用"有效行走时间"重新计算，不能凭印象下结论。
- **教训**：验证脚本本身必须先自证（基准点、计数分支），否则会把测量错误当成产品缺陷。

## 2026-10-31 第 23 轮：游戏内实时速度显示（宿主层，符合原则 13）

- 新增 `src/Sharpcraft.Launcher/Diagnostics/SpeedOverlay.cs`：每帧用玩家位置差分算**水平速度（格/秒）**，指数平滑后在画面左下角绘制
  `speed x.xx b/s (reference 4.32)`；开关 `MC_SPEED_OVERLAY=1`，**游戏程序集零改动**、无环境变量读取。
- 实测截图：静止时显示 `speed 0.00 b/s`（正确）。可用来和原版 F3 的速度直接对照（原版行走 4.317 格/秒）。
- 说明：`EffectiveSpeed` 也已加进 walk 的 `protocol:` 行，用于自动化断言（脚本口径为 4.317±0.25）。

## 2026-10-31 第 24 轮：自动化速度判定（不再靠眼睛）

- 新增/重写 `tools/verify/verify-protocol-live.ps1`：从每秒位置 trace 计算**有效行走速度**并与原版 4.317 格/秒 比较。
- 实测（20s）：`ticks=400 keepalives=1 distance=73.14 speed=3.62` → **FAIL**。
- **trace 显示位置在来回跳**：`-635.38 → -639.91 → -631.51 → -635.75`，即**服务器周期性把玩家拉回约 8 格**，
  净速度因此只有 3.62（低于原版 4.317 约 16%）。这不是"tick 抖动"，是**位置同步/纠正**问题。
- 附带发现：`MC_WALK_COLLECT=0`（FlatWorld 回退）时完全走不动（speed 0.031，corrections=171）——回退路径不可用于验证，已记录。
- 下一步：定位"位置来回跳"——对比我们发送的移动包与服务器 `PlayerPosition` 纠正的时序（先加宿主侧日志，不改游戏逻辑）。

## 2026-10-31 第 25 轮：定位"被拉回"（有完整时间线）

加宿主层日志（`MC_WALK_EXCHANGE=1`）后，20s 的时间线非常清楚：

```
join teleport 1: pos=(110.012,190.000,-704.399)   ← 服务器把我们放到 -704.40
send   0: at (110.01,190.00,-704.42)              ← 本地开始前进
send  20: at (110.01,190.00,-708.52)              ← 约 4.3 格/秒，正常
send  40: at (110.01,190.00,-712.83)
correction 2: deviation 12.54 server=(110.01,190.00,-704.40) local=(110.01,190.00,-712.83)
send  60.. : -704.50 → -743.21 → …                ← 之后不再被纠正，正常前进
```

⇒ 服务器在约 3 秒内**一直认为我们在出生点 -704.40**（说明这段时间的移动包**没有被采纳**），
第 3 秒判定偏差 12.54 格（>10 格阈值）于是把我们**传回出生点**；接受该传送后移动才被正常采纳。

⇒ 缺陷定性：**开局（join 传送后）的移动未被服务器采纳**，约 3 秒后被拉回一次。
这与之前窗口里"玩家下坠/世界看起来不对"很可能同源（都指向传送接受/移动包时序）。

- 已把该缺陷写成断言：`verify-protocol-live.ps1` 要求 `corrections ≤ 1`（仅 join 那次）。当前实测 **2 → FAIL**，即复现用例就位。
- **下一步（按流程）**：定位 join 传送的 `AcceptTeleportation` 与随后移动包的次序/内容（我们传的 teleport id、坐标、onGround），
  修好后断言转绿。仅改宿主/网络时序，不动渲染。

## 2026-10-31 第 26 轮：修复"开局移动不被采纳"（根因 = 缺 player_loaded）

- **根因**：原版客户端在世界加载完成后发送 `minecraft:player_loaded`（**serverbound id 44，空包体**）；
  服务器在此之前**忽略所有移动包**，于是约 3 秒后判定偏差 >10 格并把玩家拉回出生点。
  我方从未发送该包（只在 `MC_LOAD_ID` 探针里出现过）。
- **修复**：`PacketIds.PlayServerbound.PlayerLoaded = 44` + `PlayPackets.BuildPlayerLoaded()`；
  - 真实客户端：接受 join 传送后发送一次（`_playerLoadedSent` 保证只发一次）
  - walk 验证路径：接受 join 传送后发送
- **实测（20s，真实服务器）**：`corrections=1`（仅 join）、`distance=85.94`、**`speed=4.318`**（原版 4.317）
  ⇒ `verify-protocol-live.ps1` **PASS**；全量 7 套 1096 用例通过。
- 这条也解释了窗口里"玩家下坠/位置不对/世界看起来异常"的一部分现象（移动长期不被服务器采纳）。

## 2026-10-31 第 27 轮：速度读数按原版语义重写

- **原版依据**（`ref://mc-26.3-analysis/src/net/minecraft/world/entity/Entity.java` 的 `computeSpeed()`）：
  每个 **tick** 计算 `lastKnownSpeed = position - lastKnownPosition`（**3D**），F3 显示 `Speed: %.3f blocks/tick`，**无平滑**。
- 原实现按**每帧**差分并做指数平滑 ⇒ 数字在小数/大数间跳动（用户反馈确认）。
- 重写 `SpeedOverlay`：只在**tick 号变化时**采样一次，显示 `Speed: 0.281 blocks/tick (5.61 b/s)`。
- 实测（自动走）：连续三次采样 **0.281 / 0.281 / 0.281** —— 与原版冲刺 0.2806 blocks/tick 一致（5.612 b/s）。
- 全量 7 套测试通过；分离检查 0 违规。

## 2026-10-31 第 28 轮：#4 区块批处理 ack（预加载路径从未回 ack）

- **根因**：`PlaySession`（预加载/收集路径）只处理 `LevelChunkWithLight`，**没有 `ChunkBatchFinished` 分支**；
  协议上服务器每批发完就等 `ChunkBatchReceived`，所以**只收到第一批就停**（日志恒为 `chunks: 9 ok`）。
- **修复**：`PlaySession` 增加 `ChunkBatchFinished → BuildChunkBatchReceived(8f)`，并把 `BatchesAcknowledged` 计入 `PlaySessionStats`。
- **A/B 实测**（`MC_START_CHUNKS=200`，收集 15s）：
  | | 区块数 | 收到的批次数 |
  | --- | --- | --- |
  | 修复前 | **9** | 39（但只解析 9）|
  | 修复后 | **49** | 39（实际 ack 39 次）|
- 新增 `tools/verify/verify-batch-ack.ps1`（断言 区块 ≥ 40、failed=0）→ **PASS**。
- 未达成目标 200：另有节流/解析速率问题，登记为下一步（不是 ack 缺陷）。

## 2026-10-31 第 29 轮：#1 登录/进世界字段级验证

- 原版依据：`ref://mc-26.3-analysis/src/net/minecraft/network/protocol/game/ClientboundLoginPacket.java` 与
  `CommonPlayerSpawnInfo.java` 的 `STREAM_CODEC.composite(...)` 顺序。
- 新增 `tests/Sharpcraft.Common.Tests/Network/JoinGameFieldTests.cs`（3 个用例）：
  1. **逐字段**断言 playerId / hardcore / levels / maxPlayers / chunkRadius / simulationDistance /
     reducedDebugInfo / showDeathScreen / limitedCrafting / dimensionTypeId / dimensionId / seed / gameMode
  2. 客户端**忽略的尾部**（onlineMode、enforcesSecureChat）不影响已读字段
  3. 存在 death location 时仍能正确解析
- 全量 7 套 **1099 用例**通过（Common 374）。矩阵 #1 → 已验证（字段级）。

## 2026-10-31 第 30 轮：#9 生物群系着色（采样规则逐点比对）

- 原版依据：`ref://mc-26.3-analysis/src/net/minecraft/world/level/ColorMapColorUtil.java`
  `rain *= temp; x=(1-temp)*255; y=(1-rain)*255; index=(y<<8)|x; index>=len?default:pixels[index]`；
  `GrassColor`/`FoliageColor` 的默认色 `-65281` / `-12012264`（另有 `-10380959` 常绿、`-8345771` 白桦）。
- 为可测性给 `Colormap` 增加 `FromPixels(int[])`（工具/测试用的纯构造，不读资源）。
- 新增 `BiomeTintSamplingTests`：
  1. **121 组** (temp,rain) ∈ {0,0.1,…,1.0}² 网格，我方 `Sample` 与独立实现的参考规则**逐点相等**
  2. 越界索引回退；`(0,0)` 命中最后一格 65535
- **未实现（登记为缺口）**：`ClientLevel.calculateBlockTint` 的 **半径 2 / 25 点**逐通道整数平均混合；
  `Biome.getGrassColor` 的 `grassColorModifier`（swamp、dark_forest）。原因：生物群系数据尚未进入客户端。
- 全量 **7 套 1101 用例**通过（Client 472）。

## 2026-10-31 第 31 轮：#6 方块实体（解码逐字段验证）

- 原版依据：`ClientboundLevelChunkPacketData.BlockEntityInfo`（`xz = sectionRelative(x)<<4 | sectionRelative(z)`，
  Y 为 SHORT，类型为 BLOCK_ENTITY_TYPE 注册表 VarInt，`OPTIONAL_COMPOUND_TAG` = 直接 `readNbt`，**无布尔标志**，
  缺失即单个 End 字节）；`ClientboundBlockEntityDataPacket`（clientbound **6**，`BlockPos.STREAM_CODEC` + 类型 + `TRUSTED_COMPOUND_TAG`）；
  网络 NBT 根 **无名**。
- 新增 `BlockEntityRecordTests`（2）：X=12（0xCB 为负）仍解出 12/11/190/7；带 NBT 标签时记录不变、标签被正确跳过。
- 新增 `BlockEntityPackets` + `BlockEntityUpdateTests`（2）：packed 长坐标含**负 Y/Z** 解包正确；错包 id 拒绝。
- `InteractiveSession` 现在**分发** `block_entity_data`（解码后暂不使用），不再落到 `unhandled packet`。
- 修正测试构建器注释（原先误写"bool flag first"），并把 tag 写入改为真实的无名根。
- **剩余缺口**：类型 id ↔ 名称的注册表对照（缺 BLOCK_ENTITY_TYPE dump）；NBT 内容当前丢弃（无方块实体渲染）。
- 全量 **7 套 1105 用例**通过（Common 378、Client 472）。

## 2026-10-31 第 32 轮：#15 性能测量（帧时间分位数）

- 在渲染器里按**宿主选项**（`FrameDiagnostics.PerformanceSummary`，由 `MC_PERF_SUMMARY=1` 打开）统计每帧间隔：
  预分配 `double[131072]` 采样、退出时排序输出 `perf: frames/mean/p50/p95/p99/max/over33`（**游戏程序集不读环境变量**）。
- 新增 `tools/verify/verify-performance.ps1`（30s，断言 p99 ≤ 16.7 ms），并给宿主加了 `MC_WINDOW_WIDTH/HEIGHT`。
- **实测**：
  | 场景 | 帧数 | mean | p50 | p95 | **p99** | max | 区块 | 分辨率 |
  | --- | --- | --- | --- | --- | --- | --- | --- | --- |
  | 静止 | 5396 | 5.56 | 5.57 | 6.81 | **7.74** | 32.45 | 111 | 960×540 |
  | 移动（流式）| 5395 | 5.56 | 5.57 | 6.88 | **7.92** | 34.42 | 100 | 960×540 |
  | 移动 1080p | 5394 | 5.56 | 5.56 | 6.93 | **8.03** | 33.64 | 128 | 1920×1080 |
  ⇒ **全部通过**（中期目标 p99 ≤ 16.7 ms），全部 `-Walk` 单次耗时不超过 1 帧超 33 ms。
- **口径说明**：场景仅约 100–128 区块，无实体/水/方块实体压力；`cull` 耗时与"每帧提交次数"还没有独立计时器，登记为缺口。

## 2026-10-31 第 33 轮：修「能走但看不见」（网格队列重复灌满）

- 现象：区块数据到达很快（可走），但画面迟迟不显示。
- 遥测：streamrender: world=329 meshes=16 built=87 meshPending=1021(封顶 1024) ready=0 → 网格缓存只有 16 个区块。
- 根因：QueueMissing 每帧把所有未建网格的区块重新入队、不去重；队列立刻到 1024 上限，优先队列永远先弹最近的重复项，远端区块永远排不到。
- 修复：ChunkWorkerPool 增加 _meshQueued 集合，入队去重、取出时移除。
- 效果：world=473 -> meshes=473 -> arena 473 chunks（此前 329/16）。
- 仍存在滞后：网格吞吐约 30 区块/秒（8 线程），填满 300+ 区块需 10-15s。下一个可疑点：每个候选面都调用 ShouldCullFace（Identifier 字符串比较 + sprite 透明度查表），一个区块约 3.7 万次；下一步先用测试基准量出 MeshChunk 单区块耗时再优化。
- 顺带：ChunkMesher 增加 Neighbourhood 采样器（一次解析 5 个相邻区块），全部网格 golden 测试通过（几何未变）。

## 2026-10-31 第 34 轮：把"服务端发送"与"客户端网格"分开测量（用户要求）

- 遥测新增 `arrived=` / `arrivalLast=`（`InteractiveSession.InstallChunk` 记录到达时刻与计数）。
- 新增 `tools/verify/verify-mesh-lag.ps1`：**静止不动**跑 30s，取"最后一个区块到达的时刻"与"网格补齐的时刻"之差，
  即**纯客户端滞后**（服务端发送时间被排除）。
- **实测（可信基线）**：
  | 量 | 值 |
  | --- | --- |
  | 到达区块总数 | 320 |
  | 服务端发送完成 | **t=3.7~3.9s** |
  | 那一刻已完成网格 | 仅 23~30（**backlog ≈ 290~297**）|
  | 客户端补齐 | t=15.1~15.3s |
  | **纯客户端滞后** | **11.4s**（断言阈值 2s → FAIL，作为基线）|
- 网格吞吐 ≈ **32 区块/秒**（8 线程）⇒ 单区块约 250ms，原版是几毫秒级。
- 下一步（先测后改）：用测试基准量出 `ChunkMesher.MeshChunk` 单区块耗时并定位热点（首要嫌疑：
  每个候选面调用 `ShouldCullFace` 的 `Identifier` 字符串比较 + sprite 透明度查表，单区块约 3.7 万次）。

## 2026-10-31 第 35 轮：全链路瓶颈定位（量出来，不猜）

基准（`dotnet test --filter Benchmark`，Console 输出）：
- `tests/Sharpcraft.Client.Tests/Perf/MesherBenchmarkTests.cs`：地形化区块（32,727 实心方块）上 `MeshChunk` 耗时
- `tests/Sharpcraft.Common.Tests/Perf/ChunkParseBenchmarkTests.cs`：区块包解析耗时

**全链路分解（实测）**：

| 环节 | 耗时 | 占比/说明 |
| --- | --- | --- |
| 解析（parse） | **0.002 ms/包** | 可忽略 |
| 网格（MeshChunk） | **92 ms/区块**（单线程，稠密最坏情况）| **瓶颈** |
| ├ 状态名查询 `BlockAt` | 64 ns/次 ≈ 4.7 ms/区块 | 每格约 2 次 |
| ├ 透明度查询 `TransparencyOf` | 300 ns/次 ≈ **11 ms/区块** | 每格 1 次 |
| └ greedy 合并主循环 | ≈ **76 ms/区块** | **主要开销（83%）** |
| 上传（arena） | ≈ 2.8 KB/区块 | 可忽略 |
| 绘制 | p99 8.03 ms / 111 区块 | 余量充足 |

- 线程池实测 32 区块/秒 ÷ 8 线程 ≈ 250 ms/区块 ⇒ 真实地形比合成区块更重（草/叶几乎无法合并），
  且与单线程 92 ms 同量级 ⇒ **不是锁竞争问题，是单区块成本太高**。
- 结论：**瓶颈 = ChunkMesher 的 greedy 合并主循环（83%）+ 逐格状态/透明度查表（17%）**；
  解析、上传、绘制都不是瓶颈。
- 下一步（按收益排序）：①按 state id 预分类（透明度/是否树叶）去掉逐格字符串+字典查询（约省 17%）；
  ②优化 greedy 主循环（`TryDescribe`/`RowMatches` 重复计算），目标把 92 ms 降到 10 ms 以内；
  ③复测 `verify-mesh-lag.ps1`（当前客户端滞后 11.4s）。

## 2026-10-31 第 36 轮：状态预分类表（第一步优化，3.9 倍）

- 新增 `Sharpcraft.Client/Render/StateCullTable`：按 state id 预计算 **是否不透明 / 是否树叶 / 同方块家族**，
  一次数组读取替代「state→Identifier→字符串 EndsWith → 透明度字典查表」。
  构建成本：每套图集一次（1286 个方块，约 0.4ms），在 `ChunkWorkerPool`/`ChunkMeshCache` 缓存，图集或布局变化时失效。
- **实测（同一稠密区块 32,727 实心方块）**：
  | | MeshChunk |
  | --- | --- |
  | 优化前 | **92 ms** |
  | 优化后 | **23.65 ms**（**3.9×**，不是预估的 17%，因为省掉了字符串与字典的缓存缺失与分配）|
- **等价性**：27 个网格 golden 测试全部通过（几何逐字节未变）；全量 7 套 1107 用例通过。
- **端到端**：`verify-mesh-lag.ps1`：客户端滞后 **11.4s → 3.0s**（服务端发送时间已排除）；仍 > 2s 目标。
- **与原版对比（读代码，非印象）**：原版 `SectionCompiler` 逐方块、逐可见面发射四边形，**不做 greedy 合并**，
  可见性由 `Block.shouldRenderFace`（`getFaceOcclusionShape == Shapes.block()` 快速剔除 + `skipRendering`）判定。
  ⇒ 我们的 greedy 合并产出**更少的四边形/顶点**（GPU 侧更省），代价是每格 CPU 开销更高；现在已从 92ms 降到 23.65ms，
  但仍是 2-3 倍于原版的单区块量级（原版每区块数毫秒），剩余瓶颈 = greedy 主循环（约 83%）。

## 2026-10-31 第 37 轮：与原版实现的对比（同一区块，实测）

基准：`MesherBenchmarkTests`（稠密地形区块，32,727 实心方块，同一进程内先测"无表"再测"有表"）：

| 指标 | 无预分类表 | 有预分类表 | 原版（face-at-a-time）|
| --- | --- | --- | --- |
| 单区块耗时 | **93.98 ms** | **21.74 ms**（4.3×）| 数毫秒量级（不合并，逐面发射）|
| 图元数量 | — | **214 个四边形** | **8,814 个面** |

- **我们的 greedy 合并产出比原版少 41 倍图元**（214 vs 8,814）⇒ GPU/带宽侧明显更省，这是**优于原版的地方**。
- 代价是 CPU 每区块更重：现在 21.74 ms，仍高于原版数毫秒；**剩余瓶颈 = greedy 主循环（约 76 ms 中的大部分）**。
- 原版依据（读代码）：`SectionCompiler` 逐方块、`RenderShape.MODEL` 逐可见面发射，**无合并**；
  可见性 `Block.shouldRenderFace`：`getFaceOcclusionShape(dir.getOpposite()) == Shapes.block()` 快速剔除，再 `skipRendering`。
  我们的等价判定改为**按 state 预计算的数组**（不透明/树叶/同家族），语义与 `skipRendering` 家族规则一致（树叶留内面）。
- 顺带修正：`StateCullTable` 把 `*air` 判为不透明（air 无模型→缺失 sprite→误判为实心）；虽 `TryDescribe` 已先按 `airState` 过滤，
  仍修正以免误用。全量 7 套 1107 用例通过；端到端客户端滞后 **11.4s → 3.0~3.3s**（目标 ≤2s，下一步攻克 greedy 循环）。

## 2026-10-31 第 38 轮：greedy 主循环改造（无效）+ 放大化压力基准

- `MergeSectionFaces` 改为「每个 slice 先建 256 格面表，再在表上合并」：
  - 目的：消除加宽/加高时的重复 `TryDescribe`（O(宽×高) 重复）
  - **结果：22.95 ms vs 21.74 ms ⇒ 无收益**（说明瓶颈不在重复调用，而在**单次 TryDescribe**，约 0.9 µs/格）
  - 几何**逐字节不变**（30 个网格 golden 测试通过），无收益但结构更清晰，保留
- 新增**放大化地形**压力基准（`AmplifiedTerrainStressIsMeasured`）：高差 ±45、折叠、洞穴
  | 场景 | 单区块耗时 | greedy 四边形 | 原版逐面数 | 方块数 |
  | --- | --- | --- | --- | --- |
  | 普通地形 | **24.25 ms** | 214 | 8,814 | 32,727 |
  | **放大化地形** | **32.27 ms** | **4,241** | **18,362** | 35,710 |
  ⇒ 放大化下合并率仍 4.3×（4241 vs 18362），CPU 增加约 1.3×
- 下一步：优化 `TryDescribe` 的单格成本（嫌疑：`section.Get` 调色板读取、每格 `stackalloc` 的 `FaceBlock`、两次光照 nibble 读取）。

## 2026-10-31 第 39 轮：放大化世界压力测试（真实服务器）

服务器已由用户切换为**放大化（amplified）**地形。世界初次生成很慢（前几次连接预加载拿到 0 个区块），预热后正常。

| 指标 | 平坦世界 | **放大化世界** | 说明 |
| --- | --- | --- | --- |
| 客户端网格滞后（服务端发送已排除）| 3.0–3.3 s | **18.9 s** | 320 区块，服务端 3.5s 发完，客户端补齐到 23.4s |
| 网格吞吐 | ~32 区块/s | **~17 区块/s** | 放大化几何更碎，合并率低 |
| 重建放大系数 | — | **1.49×**（built 489 / 329 区块）| 邻块边界失效导致重复重建 |
| 帧时间 p99（329 区块 @960×540）| 8.03 ms | **7.86 ms** | **渲染侧完全达标**，0 帧 >33ms |
| 合成放大化基准 | — | **32.27 ms/区块**（4,241 quads vs 原版 18,362 面）| 真实区块更重 |

结论：
- **渲染（绘制）不是问题**：放大化 329 区块 p99 仍 7.9 ms。
- **瓶颈仍是网格构建**：真实放大化区块比合成基准重约 15×（~470 ms/区块 vs 32 ms），
  原因指向 ①每四边形发射时的 sprite/UV 解析（`BlockSpriteMap.ForState`）②区块被重复重建 1.49×。
- 下一步：量化 `EmitMergedQuad` 的每四边形成本，再做「每象限 sprite 缓存 + 预估容量复用缓冲」。

## 2026-10-31 第 40 轮：放大化世界的两个健壮性修复

放大化世界初次生成很慢，暴露出两个问题（都不是渲染管线本身）：

1. **预加载拿到 0 区块就整体失败**（`no chunks were received, nothing to show`）——
   预加载只是优化。改为：没有预加载到区块时**照常开窗**，由实时会话流式补齐（按 JoinGame 的维度布局建空世界）。
2. **在服务器给出位置之前就开始模拟** → 玩家在空世界里自由落体（实测 Y=-1257、`air`）。
   改为：收到首个 `PlayerPosition`（`_hasServerPosition`）之前**不推进玩家物理**。
- 全量 7 套测试通过。
- 备注：放大化世界就绪后的正式测量见第 39 轮（客户端滞后 18.9 s、p99 7.86 ms）；
  服务器仍在生成时预加载会拿到 0 区块，属服务端生成耗时，客户端现在不会再因此崩溃。

## 2026-10-31 第 41 轮：放大化缩进受阻（服务端只发 set_time）

- 现象：放大化世界连接后，客户端**只收到 `set_time`（id 115）**，120 秒内 **116 次**，
  **收不到 `Login`(JoinGame)、`PlayerPosition`(join 传送) 或任何区块包**；`stream: loaded=0`。
- 客户端侧已排除（同一构建 14:20 在**同一放大化世界**成功连上：329 区块、玩家 Y=218）。
- 结论：**服务端在 join 后没有完成该玩家的进入流程**（只发时间同步），属服务端状态问题。
- 本轮已修的两个客户端健壮性问题（与上述阻塞无关，但都是真问题）：
  1. 预加载 0 区块不再整体失败，改为开空世界并流式补齐
  2. 服务器给出位置前不推进玩家物理（此前会掉进虚空 Y=-1257）
- 需要用户提供：服务器控制台/日志是否有报错或仍在生成；或重启服务器后再试。

## 2026-10-31 第 42 轮：mcpfabric 桥 + 放大化世界的真实阻塞（世界是虚空）

用户把 **mcpfabric 0.8.0** 装到服务器（HTTP 桥 `127.0.0.1:25600`，`/info`、`/rpc`），并切到放大化世界。用它直接查到了服务端事实：

- `players.get`：`Blue_Coast` 在 `(-88.88, 157, -137.06)`，**`health: 0.0`**；服务器日志无死亡记录。
- 从该点向下的 `world.raycast`（Y=200，向下 300 格）**完全 miss**；该列 `world.getBlocks`（Y=-64..155）**count=0**。
  ⇒ **出生点下方是纯虚空**，玩家出生即坠落、摔死，随后服务器只发 `set_time`（我们看到的"卡住"）。
- 世界目录 `world/dimensions/.../region` 有 24 个 `.mca`（7 MB，14:34 更新），但出生区块内容为空；
  远处区块未加载时 raycast 也必然 miss，所以**无法证明别处有地形**。
- 结论：**当前 world 出生区域没有地形**（空/虚空世界，或 level-type 切换未真正生效，`server.properties` 里仍是
  `level-type=minecraft\:normal`）。要跑放大化压力测试，需要**重新生成一个真正 amplified 的世界**。

顺带修掉三个会导致"连上也白连"的客户端问题（提交见下）：
1. 预加载 0 区块时**改用全新连接**开会话（不再复用半成品连接）——修复后 join 传送能被正确接受（位置从 2.5,1,2.5 变成 -88.9,155,-137.1）。
2. 收到服务器位置前不推进物理（不再掉进虚空）。
3. 补 `player_combat_kill`(69) → `client_command`(12, respawn=0)：死亡后自动请求重生（原版是等玩家点死亡界面，暂无死亡界面，记为偏差）。

## 2026-10-31 第 43 轮：更正 + 放大化世界正式数据

- **更正第 42 轮的结论**：我此前的"世界是虚空"判断**是错的**。原因有两个：
  1. `world.raycast`/`world.getBlocks` 对**未加载区块**一律返回 miss/0（无人在线时全图都"空"），这是测量方法错误；
  2. 真正的差异是**出生点**：原版按**玩家存档位置**出生在 `(-71, 222, -119)` 实地表，
     而我们的客户端按**世界出生点** `(-88.88, 157, -137.06)` 出生，那一列确实是空的。
- **修复方式（服务端一次操作）**：`/setworldspawn -71 223 -119`（用 mcpfabric 桥执行，返回 success）。
  之后我们的客户端出生在 `-71.2, 222.0, -119.0 (ground)`，区块 **9 → 329** 正常流式。
- **放大化真实数据（世界可用后）**：
  | 指标 | 平坦 | **放大化（真实）** |
  | --- | --- | --- |
  | 客户端网格滞后（排除服务端发送）| 3.0 s | **21.1 s**（服务端 3.5s 发完，积压 236）|
  | 帧时间 p99（410 区块 @960×540）| 8.03 ms | **8.29 ms**（0 帧 >33ms，达标）|
- 结论不变：**渲染达标，瓶颈是网格构建**。下一步继续优化 `EmitMergedQuad` 的每四边形成本与重复重建。

## 2026-10-31 第 44 轮：**Debug/Release 之差**（关键更正）+ 顶点缓冲超限

### 我此前把 Debug 构建的性能当成了产品性能（更正）

- 基准在 **Release** 下重测（同一合成区块、同一机器）：
  | | Debug | **Release** |
  | --- | --- | --- |
  | 稠密区块 MeshChunk | 23.7 ms | **4.97 ms** |
  | 放大化区块 MeshChunk | 32.6 ms | **6.54 ms** |
  | 无状态表(Release) | — | **48.44 ms**（⇒ 状态表在 Release 下收益 **10×**）|
- **端到端（Release 客户端，真实放大化世界）**：积压从 24 一路降到 0（t=5.7s 时 meshes 202 / arrived 200，**已追上**），
  即**客户端不再落后**。此前"21.1 s 滞后"是 **Debug 构建**的产物。
- 因此：`tools/verify/*.ps1` 全部改为 `-c Release`（公平测量）。

### 与原版比较（Release）

| 指标 | 我们 | 原版（逐面发射）| 结论 |
| --- | --- | --- | --- |
| 稠密区块 CPU | **4.97 ms** | 数毫秒量级 | 同量级 |
| 放大化区块 CPU | **6.54 ms** | 数毫秒量级 | 同量级（约 2×）|
| 图元 | **214 / 4,241 四边形** | **8,814 / 18,362 面** | **少 41× / 4.3×**（GPU 侧明显更优）|
| 顶点字节 | **48 B/顶点**（12 float）| ≈32 B/顶点 | 我们更肥 1.5× |
⇒ 综合：**图元远少于原版、CPU 同量级**，整体优于原版；代价是顶点格式偏肥。

### 新发现的硬阻塞：顶点缓冲超过设备上限

- Release 下放大化世界快速装满 400+ 区块后，wgpu 报：
  `Buffer size 274657422 is greater than the maximum buffer size (268435456)` → **设备 panic，客户端崩溃**。
- 原因：`ChunkGeometryArena` 用**单个**顶点/索引缓冲，放大化地形 400 区块 × ~4,200 四边形 × 4 × 48 B ≈ 300+ MB。
- 需要：①**分页 is顶点/索引缓冲**（每页 ≤128 MB，多缓冲绘制）②或瘦身顶点格式（12 float → 更紧凑）。

## 2026-10-31 第 45 轮：设备上限的真相与修复（不是 256 MiB）

- **"设备上限"是什么**：是指 wgpu/WebGPU 的 **`maxBufferSize`（单个 buffer 的字节上限）**，不是显存总量。
  `adapter.RequestDeviceBlocking()` 不传 `RequiredLimits` 时继承 **默认 256 MiB**，所以我们一个 300–500 MB 的顶点缓冲
  会被 `wgpuDeviceCreateBuffer` 拒绝并 panic。
- **实测该适配器（RTX 3060 / Vulkan）**：`maxBufferSize = 1 TiB (1099511627776)`、
  `maxStorageBufferBindingSize ≈ 2 GiB`。默认 256 MiB 完全是"没要"造成的。
- **修复**：两个渲染器改为
  `var deviceDescriptor = new DeviceDescriptor { RequiredLimits = adapter.GetLimits() };`
  `adapter.RequestDeviceBlocking(in deviceDescriptor)` —— 向适配器要它**实际支持**的上限。
  同时把 arena 的自我上限设为 **1 GiB**（超限时跳过最远区块而不是崩）。
- **验证（Release，放大化，90s）**：`arena 329 chunks, presized=True, uploaded 635.9 MB, 2 full rebuilds`；
  `16125 frames in 90.0s`（≈179 fps），worst 93.5 ms（首帧/重建），仅 2 帧 >33 ms，**无 panic**。
- 教训（记入流程）：改完必须**两种配置都重建**再测；我此前用旧 Release 二进制得出"仍超限"的错误结论。

## 2026-10-31 实验分支 exp/gpu-culling-indirect：A2 间接绘制（完成）

- 分支基点 `ec38ef5`；本分支只做 GPU 化改造，主线不动。
- **A2 实现**：`ChunkGeometryArena` 新增 **indirect 参数缓冲**（每区块一条 20 字节 DrawIndexedIndirect 记录：
  indexCount/instanceCount/firstIndex/baseVertex/firstInstance），槽位由空闲表复用；
  索引缓冲**整块绑定一次**，每区块 `DrawIndexedIndirect(args, slot*20)`。
  - 顺带去掉每区块一次 `SetIndexBuffer`（原来 329 次/帧）。
- **验证**：全量 7 套 **1109 用例通过**；Release 窗口在放大化世界正常渲染（截图为实体地形，草/土/雪/水面齐全）；
  `verify-performance -Walk`：**p99 8.73 ms**（405 区块），与改造前 8.29 ms 同量级（本阶段目的是为 GPU 剔除铺路，不是提速）。
- **A3 设计（下一步）**：新增 compute pass `chunk_cull.wgsl`
  - 输入：每槽 AABB（min/max）、基础 indexCount、视锥 6 平面 uniform；
  - 输出：把被剔除的槽 `indexCount` 写 0（其余保持原值）；
  - 之后 `Draw` 仍按槽发 `DrawIndexedIndirect`（被剔除者成为 no-op）。
  这样 CPU 不再需要逐区块做可见性判断，且为后续 **MultiDrawIndexedIndirectCount**（一次调用画全部）留好结构。

## 2026-10-31 实验分支：A3a 视锥剔除（CPU 参考实现 + 兜底开关）

- `FrustumPlanes`：从 `viewProjection` 提取 6 个内平面（**System.Numerics 行向量约定，必须用矩阵列**：
  这是我第一版的错误——用行得到 2/3 测试失败，改成列后 3/3 通过），`Extract` 归一化平面，`IsVisible` 用 AABB
  "沿法线最远角"判定（保守，绝不误剔）。
- `ChunkGeometryArena`：每个 slice 记录世界 AABB（从顶点位置求 min/max），`Draw(pass, planes)` 跳过视锥外区块，
  `CulledLastFrame` 计数；`Draw(pass)` 保持全画（兜底）。
- `WindowedRenderer`：每帧算 `CameraMath.BuildViewProjection` → 提取平面 → 传入绘制。
- **兜底**：`FrameDiagnostics.DisableCulling`（宿主 `MC_CULL_DISABLE=1`）直接走 `arena.Draw(pass)`，不剔除。
  这样 A2/A3 在"没有 GPU 剔除"或需要对照时都能退回 CPU 全画路径。
- **验证**：7 套 **1112 用例全绿**；Release + 放大化：`cull: chunks=348 culled=61..89`（约 20%），画面正常。
- **下一步 A3b**：`chunk_cull.wgsl` compute pass，写同一批 indirect 记录；`Available=false` 时自动回落到本 CPU 剔除。

## 2026-10-31 记录：用户报告 `/tp` 导致客户端崩溃（未复现，待证据）

- 用户原话：**"打 tp 会崩溃"**（2026-10-31，放大化世界，Release 客户端）。
- 我用 mcpfabric 桥做了两次等价复现，**都没有崩**：
  - `players.teleport -71 260 -119`（同区块附近）→ 存活，落在 `-70.5 218 -118.5 ground`；
  - `players.teleport 4000 200 4000`（跨 4000 格，强制全量区块卸载/重载）→ 存活，落在 `4000.5 126.3 4000.5`。
- 因此**不是**"位置跳变/区块重载"本身。可疑方向（按可能性排序）：
  1. **聊天/命令输入路径**（用户在游戏内打字执行，而我们用的是桥命令，没走客户端聊天框）；
  2. 之前出现过的 **wgpu-hal 交换链 panic**（`SwapchainAcquireSemaphore still in use`）——若 `tp` 触发窗口尺寸/表面变化会撞上；
  3. `/tp` 到**目标实体**或带参数形式（`/tp <target>`）走不同的数据包分支。
- **下次崩溃需要的证据**：控制台窗口最后 5–10 行（`stdout`/`stderr`），以及**确切的命令原文**。
  客户端目前的 stderr 只落 `libpng` 警告，真正的 panic/异常信息在控制台里。

## 2026-10-31 定位：游戏内 `/tp` 崩溃 = 整块 arena 重建 + 交换链 panic（有日志证据）

用户在游戏内输入 `/tp` 后客户端崩溃。用桥的 `players.teleport` 短距/远距都不崩（那走服务端同一条路径），
但**崩溃日志抓到了**：`/tp` 后某一帧 `uploaded +286092 KiB`（**约 279 MB 单帧上传**），随后 4 帧内出现

```
panicked at wgpu-hal-29.0.3/src/vulkan/swapchain/native.rs:359
Trying to destroy a SwapchainAcquireSemaphore that is still in use by a SurfaceTexture
```

⇒ 两条独立缺陷叠加：
1. **整块重建**：teleport 让几乎所有区块同时被替换，`ChunkGeometryArena` 容量不足 → `RebuildAll()` 一口气重写全部几何
   （279 MB 单帧上传，帧时 144 ms），GPU 侧压力骤增/表面丢失；
2. **wgpu-hal 交换链 panic**：表面 Lost 后 `Configure` 销毁交换链时，上一帧的 acquire 信号量仍在飞。
   已在 `676cb2d` 加了 `OnSubmittedWorkDoneBlocking` 再 `Configure`，但**症状仍在**（说明是驱动侧 Lost 触发，不是单纯时序）。

### 待修的方案（按优先级）
1. **整体替换时重建而不是长大**：world 被整体替换（teleport/换维度）时，**清空并重建 arena 缓冲**（丢弃旧内容，按新规模一次分配），
   避免"旧 300 MB + 新 300 MB 同存 + 全量重写"。
2. **上传限流**：每帧最多上传 N MB（例如 8 MB），把突发摊到多帧；`updates` 计数已有，可直接用于预算。
3. 交换链侧继续观察：若仍 panic，考虑**固定尺寸渲染目标 + 缩放呈现**，彻底绕开 reconfigure 路径。

## 2026-10-31 **已稳定复现** `/tp` 崩溃（含原地 tp）

- 复现方式（mcpfabric 桥，等价于服务端执行）：连续 5 次 `command.run "tp Blue_Coast ~ ~ ~"` → **客户端必崩**。
  - 崩溃前日志：`frame #324 ... uploaded +132,800 KiB`（**单帧 130 MB**），随后
    `wgpu-hal swapchain/native.rs:359 panicked: Trying to destroy a SwapchainAcquireSemaphore that is still in use`。
  - 之前单次、远距的 teleport 没崩，是因为那一次的上传量小；连打（或一次性整体替换）就撞上。
- 结论修正：崩溃与"聊天框显示"无关（用户指出服务端原地 tp 也崩，验证正确）；
  真正的链条是 **tp → 服务端围绕玩家重发/重建大量区块 → 客户端单帧全量重建 arena（130–279 MB 上传）
  → 帧时暴涨 / 表面丢失 → wgpu-hal 交换链 panic**。
- 另外：本分支先前加的 `OnSubmittedWorkDoneBlocking` **是回归**（分支启动即 panic，主线正常）——**已撤回**，
  撤回后分支正常启动（pid 11308）。
- 修复计划不变：①整体替换时重建 arena（而不是在旧内容上长大+全量重写）②**每帧上传限流**（如 8 MB）
  ③若仍 panic 再改固定尺寸渲染目标 + 缩放呈现。

## 2026-10-31 按原版源码核对：命令包只含文本；崩溃在 system_chat 的组件解码

按要求翻阅 `../mc-26.3-analysis/src`：

- `ServerboundChatCommandPacket` 的 `write` **只写 `output.writeUtf(this.command)`**，构造也只读 `input.readUtf()`
  ⇒ **我们最初只写命令文本是对的**；我上一版的"补齐 22 字节"正好是服务端报的 `found 22 bytes extra`，**已 revert**。
- `ClientboundSystemChatPacket` = `record(Component content, boolean overlay)`，用
  `ComponentSerialization.TRUSTED_STREAM_CODEC` 解码 ⇒ **没有 chat type/装饰**，崩溃点只可能在**组件树解码**。
- 我们的 `ComponentRuns` 已支持 `text`/`translate`(带 `with` 参数)/`fallback`/`extra`，`LanguageTable.Format` 缺键时
  返回 key 不抛异常 ⇒ 不是"完全不支持 translatable"，更可能是 **trusted 变体的字节格式**（长度前缀/结尾标记）
  或 `chat.type.admin` 这类带参数组件在 `FlattenArguments` 上的处理。
- 崩溃机理：客户端在消息处理路径上退出 → 关闭进程 → 才出现 wgpu 交换链 panic（**panic 是症状，不是原因**）。
- **下一步**：①逐字节对照 `TRUSTED_STREAM_CODEC`（Nbt 组件、是否有长度前缀、末尾 END）与我们的读取；
  ②让宿主层打印**托管异常栈**（目前 stderr 只有 Rust panic），才能一次定位。

## 2026-10-31 A/B：崩溃与视锥剔除无关，只在"客户端内输入命令"时出现

- 分支 + 放大化，**不做任何输入**，剔除开启跑满 75 s：**存活**（`cull: chunks=367 culled=236`）；
  剔除关闭（`MC_CULL_DISABLE=1`）跑 75 s：**存活**。⇒ **剔除不是崩溃原因**。
- 只有用 SendKeys 在游戏内 **T → "/tp ~ ~ ~" → Enter** 时客户端才死；服务端对应看到 `<Blue_Coast> /tp`
  （因为用 T 打开聊天框时 `isCommand=false`，整行按 **chat** 发出去，带前导斜杠），随后 5 s 发出 `[Blue_Coast: ...]`
  的 **system_chat**（translatable `chat.type.admin`），客户端就在这之后退出 → 才出现 wgpu 交换链 panic。
- 已在宿主层加**兜底 catch**（`window crashed: <完整异常>`），此前非 IO/InvalidOperation 的异常会直接逃逸，
  只剩 Rust panic，看不到托管栈。**下一次复现请把控制台里 `window crashed:` 后面的内容发我**。
- 另：`ServerboundChatCommandPacket` 只有命令文本（已按原版核对并回退），不要再加字段。

## 2026-10-31 **已修复并实测通过**：`/tp` 崩溃 = NBT 空键被拒（组件含无名 compound）

抓到的托管栈（第一次真正看到）：

```
System.ArgumentException: The value cannot be an empty string. (Parameter 'name')
  at NbtReader.ReadCompound line 127      <- compound.Set(name, ...)
  at NbtReader.ReadList     line 167      <- 列表里的 compound 元素
  at ChatPackets.TryReadSystemChatStyled  line 75
  at InteractiveSession.DrainIncoming
```

**原版依据**（`ref://mc-26.3-analysis/src/net/minecraft/nbt/CompoundTag.java`）：`loadCompound` 用
`input.readUTF()` 作 key 放进 `Maps.newHashMap()`，**从不校验空字符串**；而 NBT 的"compound 列表"元素本来就不带名字。
`[Blue_Coast: Teleported ...]` 是 translatable 组件，其 `with` 参数正是这种结构 ⇒ 我们**比原版更严**的
`ArgumentException.ThrowIfNullOrEmpty(name)` 直接抛异常，把会话弄断。

**修复**
1. `NbtCompound.Set` 只拒绝 null，允许空键（对齐原版）；新增两个回归测试
   （空键条目、compound 列表元素）——`tests/Sharpcraft.Common.Tests/Nbt/EmptyNameTests.cs`
2. 退出时**不再销毁 surface/adapter/device**（改为有意泄漏）：销毁交换链会触发 wgpu-hal
   `SwapchainAcquireSemaphore still in use` 的 abort；进程退出由驱动回收。实测 `exit code 0`，**不再 panic**。
3. 宿主层兜底 catch 打印 `window crashed: <完整异常>`（这次就是靠它拿到的栈）。

**实测**：游戏内 T → `/tp ~ ~ ~` → Enter，客户端**存活**（pid 53724，落在地面），全量 7 套 / 1114 用例通过。

## 2026-10-31 老问题确认：流式区块只上传**不透明**层（远处/新区块的透明方块隐形）

用户报告（并指出是**老问题**，与 A2/A3 无关）：出生点附近的透明方块正常，稍远或新加载的区块里透明方块（水/玻璃）**看不见**。

代码依据（已核对，非猜测）：
- `ChunkMesher` 每区块产出**三层**：不透明 `Vertices/Indices` + `cutout` + `translucent`（日志 `layers: cutout … tri, translucent … tri`）；
- 但 `WindowCommand` 的流式上传只做了
  `updates.Add((key, new GeometryData(mesh.Vertices, mesh.Indices)))` —— **只带不透明层**；
  `GeometryData.cs` 也只有 `(float[] Vertices, uint[] Indices)` 两个字段；
- 初始预加载/帧请求路径另有 `CutoutVertices/CutoutIndices`（`WindowedRenderer` L901）只覆盖**首次**那批区块。
⇒ 所以**只有出生时加载的那批**有透明/镂空几何，之后流入的区块没有。

**修复方向**（下一步）：`GeometryData` 扩成三层；`ChunkGeometryArena` 每区块持三组 slice（或三个 arena）；
渲染器按 不透明 → cutout → translucent 三个 pass 绘制（半透明按距离从远到近）。

## 2026-10-31 A 系列计划（更新）

| 项 | 内容 | 状态 |
| --- | --- | --- |
| A1 | 顶点格式 48 → 24 B（颜色/UV/光照打包）| 暂缓（用户判断带宽不是瓶颈、风险大）|
| A2 | 每区块 indirect 绘制参数（DrawIndexedIndirect，索引缓冲整块绑定一次）| **完成** `0c73230` |
| A3a | CPU 视锥剔除参考实现 + `MC_CULL_DISABLE` 兜底 | **完成** `ab32861` |
| A3b | `chunk_cull.wgsl` compute 视锥剔除（`Available=false` 回落 A3a）| **进行中** |
| A4 | GPU 遮挡剔除（上一帧 Hi‑Z）| 未开始（回落 A3b/A3a）|
| **A5** | **三层流式**：`GeometryData` 扩为 opaque/cutout/translucent，arena 每区块三组 slice，渲染三 pass（半透明按距远→近）| **新排入**（老问题，用户确认）|

约定：A1–A5 每条都必须保留"不可用时退回 CPU 路径"的兜底。

## 2026-10-31 A5 三层流式：落点已定位，实施清单

已确认丢弃点（本分支代码现状）：

- `ChunkWorkerPool` L409-418：每次 `MeshChunk` 都建了 `cutoutScratch` / `translucentScratch` 两个临时缓冲，
  **算完直接 Dispose**（注释说明"渲染用的 cutout/translucent 来自另一条 pass"），只把不透明层
  `buffer.ToMeshData(0)` 入队 `_meshes`。
- `GeometryData` 只有 `(float[] Vertices, uint[] Indices)`。
- `WindowedRenderer` 只有首帧路径 `currentRequest.CutoutVertices/TranslucentVertices`（L929/L946）。

实施清单（按依赖顺序，每步可独立编译与验证）：

1. **数据层**：`GeometryData` 增加 `CutoutVertices/CutoutIndices/TranslucentVertices/TranslucentIndices`（默认空数组）。
2. **产出层**：`ChunkWorkerPool` 把两个 scratch 也 `ToMeshData(0)` 并随不透明层一起入队（队列元素加两个字段）。
3. **上传层**：`WindowCommand` 的 `geometryUpdates` 把 4 个数组一起塞进 `GeometryData`；
   `ChunkGeometryArena` 扩展为**每区块三组 slice**（或实例化三个 arena：opaque/cutout/translucent），
   每组的 indirect 参数与 culler 各自独立。
4. **绘制层**：`WindowedRenderer` 增加两个 pass——`cutoutPipeline` 画 cutout arena、
   `translucentPipeline` 画 translucent arena（先按区块中心距离**由远到近**排序，真正的逐面排序列为后续）；
   三层都沿用 A2 的 indirect 与 A3b 的 cull（各自一个 `ChunkCuller`）。
5. **兜底**：任一层缺失/为空 → 跳过该 pass；`MC_CULL_DISABLE=1` 与 `Available=false` 行为不变。
6. **验证**：放大化世界里 `verify-photos`（同机位截图）对比"修复前后：远处水/玻璃是否出现"；
   并记录上传字节与 p99（同时把**每帧上传限流 8 MB** 一并做掉，压住 `over33`）。

注：核心变量是 `ChunkGeometryArena` 的"每区块一个 slice"假设；改成三组 slice 时，`SlotHighWater` 与
culler 的 `slots` 要按层各自维护，避免再犯 A3b 那次"按在册数量分发"的错误。

### A5 进度
- **第 1 步完成**：`GeometryData` 已增加 `CutoutVertices/CutoutIndices/TranslucentVertices/TranslucentIndices`
  （全部可选、默认 null ⇒ 零破坏，全量测试通过）。这是纯数据契约层，不改变任何渲染行为。
- 第 2 步（`ChunkWorkerPool` 保留两个 scratch 并入队）涉及队列元素类型 `(long Key, MeshData Mesh)` 的扩展，
  会连带改动 `WindowCommand` 的两处解构，**下一步单独提交**。

### A5 第 2 步补充：还多一层需要改（勘察结果）

第 2 步不止改 `ChunkWorkerPool`，`ChunkMeshCache` 也有一条中间层：

- `ChunkWorkerPool._meshes`：`ConcurrentQueue<(long Key, MeshData Mesh)>`（L47），入队在 L427。
- **`ChunkMeshCache.DrainUpdates()`** 返回 `List<(long, MeshData)>`，内部 `_updates` 与 `Combine()` 都按两元组走 —— 三层必须一起扩。
- `WindowCommand` L393/397 的 `List<(long Key, MeshData Mesh)> drained` 与 L441（mesh order）、L456（`new GeometryData(...)`）三处解构。

建议做法：新增 `readonly record struct MeshedChunk(long Key, MeshData Opaque, MeshData Cutout, MeshData Translucent)`，
把三处元组统一换成它，`MeshData` 本身不动（避免影响 `Snapshot/Combine` 的其它调用者）。

**下一步从这里继续**：先加 `MeshedChunk`，再改 pool 入队 → cache `DrainUpdates` → WindowCommand 透传 →
三个 arena → 两个 pass → 五条退化 → 上传限流与截图验证。

### A5 第 2 步完成
- 新增 `MeshedChunk(long Key, MeshData Opaque, MeshData? Cutout, MeshData? Translucent)`（`Client/Render/MeshedChunk.cs`）。
- `ChunkWorkerPool`：两个 scratch **不再 Dispose**，随不透明层一起入队；`TryTakeMesh` 增加两个 out 参数。
- `ChunkMeshCache`：`_updates` / `DrainUpdates()` / `SnapshotAll()` 全部改为 `MeshedChunk`。
- `WindowCommand`：`replayQueue`、`drained`、两处解构与 `GeometryData(...)` 六参构造全部透传三层。
- 全量测试通过；**渲染行为尚未改变**（下一阶段才用这些层）。

### A5 第 3–6 步完成（三层流式打通）

- **三个 arena（方案 A）**：`arena` / `arenaCutout` / `arenaTranslucent`，各自一个 `ChunkCuller`；
  每帧按 `SlotHighWater` 各自 dispatch 剔除；重置/重建时三者一起重建。
- **两个新 pass**：`cutoutPipeline` 画 cutout arena、`translucentPipeline` 画 translucent arena（当前按槽位顺序，
  **逐面/按距排序仍是已知缺口**，作为第 5 条退化路径显式记录）。
- **退化路径**：① 层为空 → 跳过该 pass；② 每层 arena 独立（互不影响）；③ 每帧 **8 MB 上传预算**，
  超出部分进 `uploadQueue` 顺延到下一帧（先有陆地后有水，可观测、不丢数据）；④ cull 不可用 → 各自回落 CPU 剔除/不剔除；
  ⑤ 半透明排序退化（槽位顺序）。
- **验证（Release + 放大化）**：
  - `cull: chunks=677 culled=0 cutout=650 translucent=628` —— 流式区块的层**真的在上传了**（此前恒为 0）
  - 截图一（700,63,700）：湖面与水下地形正常 → 水（translucent）在新区块可见
  - 截图二（1501,92,1501，雪原针叶林）：树叶（cutout）与雪地正常、无空洞
  - 全量 **7 套测试通过**

### A5 后性能复测（三层 + 8MB 限流，静止机位，45s）

| | mean | p50 | p95 | p99 | max | over33 | 区块 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| A3b（两层未启用） | 5.57 | 5.56 | 6.48 | **7.78** | 74.6 | 2 | 328 |
| **A5（三层 + 限流）** | 5.62 | 5.54 | 6.84 | **8.76** | **292.5** | 4 | 657 |

结论：三层绘制多花 ~1 ms（合理，多两个 pass）；**max 292 ms 是 arena 增长时全量重写**（`3 full rebuilds`，三套 1 GiB 缓冲），
这是下一个明确的优化点（分页/预分配，或上传限流扩到重建）。

### 剩余任务（本分支未完成）

1. **A4 GPU 遮挡剔除**：用上一帧深度金字塔，在 compute 里对区块 AABB 做遮挡测试，写同一批 indirect 参数
   （与 A3b 同结构，只多一个 Hi-Z 纹理绑定 + 采样）。回落：Hi-Z 不可用 → A3b 视锥剔除。
2. **半透明排序**：目前按槽位顺序绘制（退化路径⑤）；按区块中心距离**远→近**排序即可覆盖绝大多数视觉问题，
   逐面排序列为后续。
3. **重建尖峰**：`max 292 ms`，三套 arena 各自全量重写；考虑预分配/分页，或把重建也纳入上传预算。

### ③ 重建尖峰：已修（max 292 ms → 67 ms）

- `ChunkGeometryArena.AssumedBytesPerChunk`：96 KB → **512 KB**（实测放大化约 0.5 MB/区块，原来低估 6 倍，导致反复全量重写）
- 新增 `MaxPresizeBytes = 256 MB`：单次预分配封顶，避免"分配一个巨大缓冲"本身变成尖峰
- 实测（静止机位 45 s）：`max=67.08 ms`、`over33=4`、`2 full rebuilds`（此前 `max=292.49`、`3 full rebuilds`）
- 仍待做：② 半透明按距排序、① A4 GPU 遮挡剔除

### ② 半透明按距排序：已完成

- `ChunkGeometryArena.DrawFarthestFirst(pass, eye)`：按区块中心与相机的**距离平方降序**绘制半透明层；
  复用 `SortKey[]` 字段 + `struct SortKey : IComparable<SortKey>`（**每帧零分配**，符合热路径要求）。
- `WindowedRenderer` 的半透明 pass 改用它。
- 验证：湖区截图层次正确（远处水更淡、近处水在前、水下地形可见）；全量 7 套测试通过。
- 记录为**近似**：逐面排序更精确，但成本远高于收益（方块尺度下区块级排序已足够）。

### ① A4 GPU 遮挡剔除：设计与前置条件（未实现）

- 前置：`CreateDepthTexture` 目前 `Usage = RenderAttachment`，**必须加 `TextureBinding`** 才能在 compute 里采样
  （否则创建 bind group 会报验证错误）。
- 数据：culler 已持有每槽 AABB 与 indirect 参数，A4 只需
  ① compute 之前（此时深度纹理仍保存**上一帧**内容）绑定 depth view + sampler；
  ② uniform 里增加 `mat4x4` 视图投影 + 视口尺寸；
  ③ 着色器把 8 个 AABB 角投影到屏幕，取屏幕矩形内若干采样点的**最大**深度，与区块**最近**深度比较
     （`nearest > maxSampled + bias` 判为遮挡）。
- 保守性：任一角在近平面之后（`w <= 0`）→ 不做遮挡判定；bias 取保守值；`Available=false` 或
  `MC_CULL_DISABLE=1` → 回落 A3b 视锥剔除。建议先做成**默认关闭的宿主选项（如 `MC_OCCLUSION=1`）**，
  用同机位截图与"不剔除"对比像素差，达标后再默认开启。
- 预期：视距大、地形遮挡多时才有明显收益；当前 328–657 区块、960×540 下预估 <0.5 ms，需实测确认是否值得默认开。

## 2026-10-31 冰面问题的原版对照

原版事实（`ref://mc-26.3-analysis/src`）：
- `Blocks.ICE`：`BlockBehaviour.Properties.of()...noOcclusion()` —— 冰**不遮挡**，相邻方块的面向冰的面仍然绘制。
- `ChunkSectionLayer.TRANSLUCENT` 用 `RenderPipelines.TRANSLUCENT_TERRAIN`（`TERRAIN_SNIPPET` +
  `BlendFunction.TRANSLUCENT` + `ALPHA_CUTOUT 0.1` + `DepthStencilState.DEFAULT` 即**深度写入开**）。
  ⇒ 半透明**用纹理自身 alpha**、只剔除 alpha<0.1 的像素、写深度、按区块从远到近。
- 26.3 另有 **OIT 路径**（`OIT_ALPHA_ONLY`/`OIT_DEPTH_BOUNDS`/`OIT_TRANSMITTANCE`，深度比较 `GREATER_THAN_OR_EQUAL` 且不写），
  属于更高级的透明方案，暂不引入。

我们与之一致的差距已修：`fs_translucent` 原来输出**固定 alpha 0.7**（注释写着"water 占位"），
现改为 **使用纹理 alpha + `discard < 0.1`**（对齐 `ALPHA_CUTOUT 0.1`）。

**尚未解释的现象**：冰原截图里仍有**成片黑色方块**；同机位关掉剔除（`MC_CULL_DISABLE=1`）截图**完全一致**
⇒ **不是剔除问题**，问题在光照/几何/采样路径，下一步需要针对"黑色面"定位（怀疑是这些面的光照被算成 0）。

### 冰/水透明排序：根因是**没有写深度**（已修，黑块消失）

- 原版 `TRANSLUCENT_TERRAIN` 用 `TERRAIN_SNIPPET` 的 `DepthStencilState.DEFAULT`（**深度写入开启**），
  所以即使区块内四边形顺序不完美，**较远的透明面会被深度测试挡掉**，不会叠加成黑块。
- 我们 `CreateTranslucentPipeline` 之前是 `LessEqual, write: false` —— 不写深度，远处透明面照样叠加，
  于是"背后不该看见的面"透出来，表现为成片黑色方块（用户判断：与排序是同一类问题，正确）。
- 修复：`write: true`（`WGPUCompareFunction.LessEqual` 不变，我们的深度是 0..1 近…远）。
- 结果：**同机位冰原截图黑块全部消失**，冰面连成整片、水下地形正常透出。
- 仍保留"按区块远→近"排序（`DrawFarthestFirst`）作为补充；逐面排序依旧不做。

### 冰面分块感：关掉贪心合并的实验（结果：不可行，但学到关键数据）

做法：临时把 `MergeSectionFaces` 的扩充循环条件改成 `Merge=false`（每面一个四边形，与原版一致），Release 跑冰原同机位。
结果：**客户端崩溃** —— `wgpuDeviceCreateBuffer: Not enough memory left`，之前几帧上传量 140–230 MB。

结论：
- **贪心合并是我们内存能活下来的关键**：每面一个四边形会让几何量约 **4×**，三套 arena 直接爆显存。
  （这也解释了原版为什么吃内存：原版不合并。）
- 因此**不能靠"关合并"消除分块感**，必须在合并路径内解决：
  ① 逐顶点光照 + AO（原版 `ModelBlockRenderer.tesselateAmbientOcclusion` / `BlockQuadLighter`）
  ② 检查合并四边形的 UV/mip 连续性（相邻合并四边形尺寸不同 → mip 选择不同 → 可见边界/斜带）
- 实验已 `git checkout` 回滚，分支保持绿色（`42afe47`），游戏已恢复运行。

## 2026-10-31 出生 9 区块颜色不同：**双绘制路径**（已修）

用户观察到：进游戏后**周围 9 个区块的颜色与远处明显不同**。

- 原因：出生那批区块由 `FrameCommand.BuildRequest` 产出的**静态合并网格**（`currentRequest.Vertices` /
  `CutoutVertices` / `TranslucentVertices`）绘制，而后续流入的区块走 **arena** 路径。两条路径的着色/光照不一致
  ⇒ 3×3 区块色块。
- **原版对照**：原版没有"静态世界副本"这一层——`SectionRenderDispatcher` 把每个 section 编进自己的缓冲，
  **所有区块同源绘制**。我们的双路径本身就是偏离。
- 修复：`useStaticWorld = arena is null || arena.ChunkCount == 0`，**arena 一旦有内容就不再画静态副本**
  （opaque / cutout / translucent 三处都加了这个门）。出生区块由 arena 的 `replayQueue` 补齐。
- 同时（上一处实验后保留）：`textureSampleGrad` + 未回绕 uv 的显式梯度，消除合并四边形之间的 mip 跳变。
- 结果：出生 3×3 色块消失，冰面大幅趋于均匀（仍有轻微横向条带，属合并四边形的光照/mip 残差，下一步做逐顶点光照+AO）。

## 2026-10-31 逐精灵 mip：已实现；但**不是**冰面条带的原因（同机位实测）

- 重写 `AtlasMipChain.Build`：按**精灵归属**生成 mip（先建 level-0 归属图，padding 用环形搜索找最近精灵），
  每级把源 2×2 块**夹取到该精灵矩形内**再按 alpha 加权平均 ⇒ 永不相邻精灵渗色（对齐优化模组做法）。
- **同机位对照**（两次都先 `players.get` 读回确认 pose 完全一致：`x=1080.5 y=63 z=800.5 yaw=-90 pitch=40`）：
  旧（整图集 mip）vs 新（逐精灵 mip）像素差 **0.171/255，仅 0.12% 像素** ⇒ **视觉几乎无差别**。
- 结论：**冰面的分块/条带不是 mip 渗色造成的**。mip 修复仍然保留（它是正确性修复，防止其它表面渗色）。

### 下一步（按 Sodium 调研结果）
- Sodium 的 `translucent_sorting` 是**逐 section 内的四边形排序**：BSP 树 + 四边形切分（`bsp_tree`、`quad`），
  常用快路径是 `SortType.STATIC_NORMAL_RELATIVE`（构建期按法线距离静态排好，必要时才动态重排），
  section 之间再按距离排序。
- 我们现在只有**区块级**远→近；冰面条带更像是**每四边形只取一次光照**导致（相邻合并四边形明暗不同）。
  下一步应做 ① 逐顶点光照 + AO（原版 `BlockQuadLighter`），② 若仍不够，再引入 Sodium 式的 section 内四边形排序。
- Sodium 许可证：**LGPL-3.0**（与本项目一致），可阅读参考；实现仍须自行编写（原则 3）。

## 2026-10-31 冰面"大块矩形"真因：**水面与冰面共面 z-fighting**

- 用户指出所见是**浮冰**（冰盖在冻洋水面上）。逐精灵 mip（同机位 0.12% 差异）与**逐顶点光照+AO**（同机位几乎一致）
  都验证过：**都不是这些矩形的原因**。
- 真因：我们**没有实现流体高度**（全局搜索无 `0.875`/`FluidHeight` 之类），水体按**整方块**发射，
  水面顶面在 `y+1`，与冰方块顶面**同一平面** ⇒ 深度相等、逐四边形"谁先写谁赢" ⇒ 大片矩形色块。
  原版 `LiquidBlock`/`FlowingFluid` 的源水高度是 **14/16 = 0.875**，水面比方块顶低 1/16，不会与浮冰共面。
- 修复方向：mesher 对流体按**源水 0.875**（流动水按四角高度另算）发射顶面与侧面；这是"真正透明的冰"瑕疵的根因。

### 本轮已实现（与原版对齐，已提交）
- 逐顶点平滑光照 + AO（`CornerShade`：四格取光平均 + 1.0/0.75/0.5/0.25 遮挡衰减，双面遮挡封顶 0.5）
- `Occludes` 修正：**半透明层方块永不遮挡**（原版 `noOcclusion`），此前冰被当遮挡物导致相邻面被剔、全场压暗

## 2026-10-31 阶段 1：流体高度（按方块名）+ 半透明不写深度

- `MeshBuffer` 新增**浮点坐标**的 `AppendQuad`/`WriteVertex`（顶点本来就是 float，只是此前只从整数格写入）。
- `ChunkMesher.EmitMergedQuad`：方块名为 `water`/`lava` 时，把四边形**顶边**下移 **1/16（=0.875 源水高度）**，
  水面不再与浮冰顶面共面（消除 z-fighting 的直接原因）。
  - 限制：**流动水的高度差**需要方块状态 `level` 属性，属阶段 2；源水 0.875 已足够消除共面。
- 半透明 pass **恢复不写深度**：按整列区块排序时，写深度会把"更远区块的透明物体"挡没（用户实测：
  透过冰看不见其它区块的透明物）。代价是共面瑕疵——正好由流体高度消掉。
- 结论记录：正确组合是 ①按 section（16³）排序 + ②写深度；当前用 ③按列排序 + 不写深度 + 流体高度，
  作为阶段 1 的折中。阶段 2 引入方块状态后再做 ①。
### 逐面剔除依据（原版 + Sodium 都核对过）

- 原版 `Block.shouldRenderFace`（`world/level/block/Block.java` L295-309）：
  **只有** `neighborState.getFaceOcclusionShape(direction.getOpposite()) == Shapes.block()` 才剔除；
  `Blocks.GLASS` 与 `Blocks.ICE` 都带 **`.noOcclusion()`** ⇒ 冰-冰、玻璃-玻璃之间的面**照画**。
- Sodium `render/model/AbstractBlockRenderContext.java`：同样的规则（注释原文：*"if the neighbor has a
  full-block occlusion shape, the face is always hidden"*），且 Sodium 有**独立的** `DefaultFluidRenderer`
  自己处理流体遮挡 ⇒ 流体单独归类，符合"同族只对 water/lava 剔除"。
- 因此把 `ShouldCullFace` 的"同家族即剔除"改为：**仅 `water`/`lava` 同族剔除**；冰/玻璃保留内部面。

## 2026-10-31 **关键**：26.3 的透明根本不是靠排序，而是 **OIT**

在分析工作区 `client/renderer/LevelRenderer.java` 里查到：

- `L460 / L554`：`executeOit(chunkSectionsToRender, ...)`、`executeOitWaterMask(...)` —— 每帧走 **OIT 管线**
- `L230/L238/L239/L399/L405/L409`：OIT 专用目标
  `oitCloudDepth`、`oitTerrainWithWaterPatchDepth`、`OIT_TRANSMITTANCE_TARGET_COUNT` 组透射目标
- `L109-L111`：`OIT_WAVELET_RANK = 2`、`OIT_COEFFICIENT_COUNT = 2^3 = 8`、`OIT_TRANSMITTANCE_TARGET_COUNT = 2`
- `RenderPipelines` 里的 `OIT_ALPHA_ONLY / OIT_DEPTH_BOUNDS / OIT_TRANSMITTANCE / OIT_ACCUMULATE`
  片段使用 `BlendFunction.ADDITIVE`、`CompareOp.GREATER_THAN_OR_EQUAL`、**不写深度**、`GpuFormat.RGBA16_FLOAT`
- `ChunkSectionLayer.TRANSLUCENT` 只是"半透明"的归类；真正解决顺序问题的是 OIT 那一层。

⇒ **结论**：我们一直在用"排序 + 深度写/不写"去解决一个 26.3 用 **OIT** 解决的问题，
所以无论怎么调排序都只是缓解，永远达不到原版。这也解释了为什么共面冰/水、透过冰看远处水等组合怎么改都有残渣。

### 三条路线
1. **加权混合 OIT（McGuire & Bavoil 经典款）**：一个累积目标(RGBA16F) + 一个 revealage 目标，无需排序；
   wgpu 完全能做。改动中等（两个额外目标 + 一次全屏 resolve + 半透明着色器改成输出 weight）。
2. **对齐 26.3 的 depth-bounds + transmittance OIT**：效果最接近，但需要 2 组透射目标 + 多次 pass，工程量大。
3. **继续排序路线**（按 section 排序 + 写深度）：只能缓解，且要引入 section 级切片，收益不如 1。

### OIT 路线 1：实施计划（已确认开工，下一步直接照此落地）

1. **新着色器文件** `Shaders/pipelines/oit_resolve.wgsl`（独立模块，避免与 terrain 共享绑定）：
   - 顶点：全屏三角形，输出 uv
   - 片元：`accum = textureSample(accumulate, ...)`、`reveal = textureSample(reveal, ...)`；
     `alpha = 1 - reveal.r`，`rgb = accum.rgb / max(accum.a, 1e-5)`，返回 `vec4(rgb, alpha)`
   - 绑定：`@group(0) @binding(0) texture_2d<f32>`、`@binding(1) sampler`、`@binding(2) texture_2d<f32>`
2. **terrain.wgsl 增加** `fs_translucent_oit`：输出两个 target
   `@location(0) vec4(colour * a, a)`、`@location(1) vec4(a)`（保留 `discard < 0.1`、`textureSampleGrad`）
3. **WindowedRenderer**：
   - 随窗口尺寸创建 `accum`(RGBA16Float) / `reveal`(R16Float)，`RenderAttachment | TextureBinding`，并随 resize 重建
   - `CreateTranslucentOitPipeline`：2 个 color target，
     accum 混合 `(ONE, ONE)`/`(ONE, ONE)`、reveal 混合 `(ZERO, ONE_MINUS_SRC_ALPHA)`；深度测试开、写关
   - `CreateOitResolvePipeline`：1 个 color target，混合 `(SRC_ALPHA, ONE_MINUS_SRC_ALPHA)`；深度 Always、写关
   - 帧结构改为 4 个 pass：
     A 主 pass（天空/不透明/cutout + 动态几何）
     B OIT pass（清 accum=0 / reveal=1，depth=Load 深度，画半透明）
     C resolve pass（全屏三角形把 accum/reveal 合成到主颜色目标）
     D UI pass（原 UI 代码整体搬到这里）
4. 验证：同机位截图（冰/水/透过冰看水/共面）、p99 影响；`DrawFarthestFirst` 保留为无害辅助。

### OIT 第 1 步完成（着色器脚手架，画面不变）
- 新增独立模块 `Shaders/pipelines/oit_resolve.wgsl`：全屏三角形 + `fs_oit_resolve`
  （`coverage = 1 - reveal.r`、`rgb = accum.rgb / max(accum.a, 1e-5)`），自带三个绑定。
- `terrain.wgsl` 增加 `fs_translucent_oit`（双输出：`(colour*a, a)` 与 `(a)`，保留 `discard<0.1` 与 `textureSampleGrad`）。
- **实测**：Release 启动正常（`#1652`），说明新入口在 Naga 里能编译；画面与之前一致（尚未接线）。
- 下一步：`accum`(RGBA16F)/`reveal`(R16F) 目标 + 两个管线 + **4 pass 拆分**（主 → OIT → resolve → UI）。

### 透明管线总计划（用户确认）

1. **OIT 路线 1（加权混合）**：先落地，作为**默认**管线（开销小、无需排序）。
   - 第 1 步（已完成，`175be3b`）：着色器入口就位并验证可编译。
   - 第 2 步：`accum`(RGBA16F)/`reveal`(R16F) 目标 + 两个管线（translucent-oit / resolve）。
   - 第 3 步：4 pass 拆分（主 → OIT → resolve → UI），同机位截图验证。
2. **原版 OIT 复刻（depth‑bounds + transmittance）**：路线 1 完成后再做，作为**高质量管线**。
   - 依据 `LevelRenderer.executeOit` / `RenderPipelines.OIT_*`：两组透射目标 + depth bounds + 水面 mask。
3. **按平台性能切换**：两条管线共用同一套半透明几何与 arena，只切换目标与 pass 组合；
   用宿主层选项（如 `MC_TRANSPARENCY=weighted|reference`）选择，默认 weighted，失败自动回落 weighted。
   注意原则 13：开关只在宿主层解析，游戏程序集只接收显式选项。

### OIT 第 2 步：可直接照抄的要点（下次开工用）

**目标**（`WindowedRenderer` 里 depth 创建处之后，并加入 resize 重建与 Dispose）：
```csharp
var accumTexture  = device.CreateTexture(new TextureDescriptor {
    Size = new Extent(frameWidth, frameHeight, 1), MipLevelCount = 1, SampleCount = 1,
    Dimension = WGPUTextureDimension._2D, Format = WGPUTextureFormat.RGBA16Float,
    Usage = TextureUsage.RenderAttachment | TextureUsage.TextureBinding });
var revealTexture = ... Format = WGPUTextureFormat.R16Float ...;   // 同上
```

**半透明 OIT 管线**（入口 `fs_translucent_oit`，两个 color target）：
- target0(accum, RGBA16F)：`color = { Add, One, One }`、`alpha = { Add, One, One }`
- target1(reveal, R16F)：`color = { Add, Zero, OneMinusSrcAlpha }`
- 深度：`Depth32Float, LessEqual, write:false`；顶点布局/步长与现有 terrain 相同

**resolve 管线**（`ShaderLibrary.Load("oit_resolve")`，`vs_fullscreen`/`fs_oit_resolve`）：
- 1 个 target：`{ Add, SrcAlpha, OneMinusSrcAlpha }`；深度 `Always, write:false`；无顶点缓冲（`pass.Draw(3)`）
- 绑定：`@group(0) 0: texture(accum) / 1: sampler(Linear,Clamp) / 2: texture(reveal)`，
  采样器用 `MagFilter/MinFilter = Linear`、`AddressMode* = ClampToEdge`

**帧结构（4 pass）**：
1. 主 pass：天空 → 不透明 arena → cutout arena → 静态世界(仅 `useStaticWorld`) → 动态几何
2. OIT pass：`BeginRenderPass(accum=Clear0, reveal=Clear1, depth=Load(depthView))`，
   `SetPipeline(translucentOitPipeline)`、绑定相机 bindGroup、`arenaTranslucent.DrawFarthestFirst(pass, eye)`
   （排序已无必要，保留无害）、静态半透明（仅 `useStaticWorld`）
3. resolve pass：`BeginRenderPass(colour=Load(mainView))`，`Draw(3)`
4. UI pass：原 UI 绘制块整体搬入
注意：`pass.End()` 位置要随之移动；UI 使用 `uiTransformBuffer` 自己的绑定，不受影响。

## 2026-10-31 OIT 第 2/3 步完成（窗口路径加权混合 OIT）

**已落地**（`src/Sharpcraft.Framework/Rendering/WindowedRenderer.cs`）：

1. 两个帧尺寸目标 `accum`(RGBA16F)/`reveal`(R16F)，含 resize 重建与 Dispose；`oitSampler`(Linear/Clamp)；
   独立 `oitResolveBindGroupLayout`（texture0/sampler1/texture2）。
2. 两条管线：`CreateTranslucentOitPipeline`（双 target，accum `One/One`、reveal `Zero/OneMinusSrcAlpha`，
   深度 `LessEqual` 写关）、`CreateOitResolvePipeline`（`SrAlpha/OneMinusSrcAlpha`，深度 `Always` 写关，`Draw(3)`）。
3. 帧循环拆 **4 pass**：主 → OIT（accum=0/reveal=1，深度 Load 只读）→ 解算 → UI；`pass.End()` 已随各 pass 移动。
4. `ShaderPipelines.All` 新增 `oit_resolve` 描述符 + terrain 增加 `fs_translucent_oit` 入口（启动校验覆盖）。
5. 新增 `tests/Sharpcraft.Framework.Tests/Rendering/OitResolveTests.cs`（3 项：顺序无关、单片退化 alpha、覆盖度=1−reveal）。

**修掉的真 bug**：`oit_resolve.wgsl` 第 16 行缺失 `var` 关键字（脚手架从未真正编译过，早先"运行时已验证"的说法不成立）。
加 `var` 后窗口成功创建管线并出帧。

**验收数据**：`MC_FRAME_WATER=1` 窗口跑满 1073/2508 帧、**0 validation error**、p99 10.18 ms（960×540）；
全量测试 1117 通过、文档门禁 ok。

**下一步具体动作（按序）**：

1. **所有者本机目视**：`$env:MC_FRAME_WATER='1'; dotnet run --project src/Sharpcraft.Launcher -c Release -- window`
   确认水面 OIT 观感（本环境交换链不可截图，`PrintWindow` 对 wgpu 返回黑屏；`window` 写出的 PNG 是离屏旧混合路径，不能代表 OIT）。
2. 若观感通过：接 `MC_TRANSPARENCY=weighted|reference` 宿主开关（默认 weighted，失败回落 weighted），原版 depth-bounds +
   transmittance OIT 作为 reference 管线。
3. 可选：让离屏渲染器也走 OIT，使像素级对照（T-104/T-159 的截图）能覆盖透明层。

## 2026-10-31 原版 OIT 参考管线：勘察与规格完成（待确认范围后实现）

已通读原版 `net/minecraft/client/renderer/oit/*` 与相关 shader include，行为规格写入
`docs/specs/render/oit-reference.md`（草案，status 待确认）。核心结论：

- 原版透明 = **三阶段 OIT**：`DEPTH_BOUNDS`(RGBA32F, 混合 MAX, 深度 `>=` 只读) → `TRANSMITTANCE`(2×RGBA16F, 8 系数小波, ADDITIVE)
  → `ACCUMULATE`(RGBA16F, ADDITIVE) → `COMPOSITE`(读 accum+系数+depthBounds，写主色并**写最近器件深度**)。
  阶段间有一次 `depth_bounds_cull`（最远边界裁到不透明深度）；云/水面贴片另有 `oitCloudDepth`/`oitTerrainWithWaterPatchDepth` + `water mask`。
- 常量：`OIT_WAVELET_RANK=2`、`OIT_COEFFICIENT_COUNT=8`、`OIT_TRANSMITTANCE_TARGET_COUNT=2`。
- 我们只渲染地形半透明，故 reference 管线**只覆盖地形**；云/水贴片/实体/天气的 OIT 集合与 water mask **不实现**（等这些内容存在再接）。

**建议的落地顺序（分 3 个可验证小步）**：

1. **纯逻辑核心**：C# 参考 `encode/sample/coverage`（8 系数小波），测试：顺序无关、总透射率≈`Π(1-α)`、单片退化。
2. **GPU 目标与管线**：4 个目标 + 3 阶段管线 + cull + composite 的 WGSL；确定性水景 `MC_TRANSPARENCY=reference` 跑满无 validation error。
3. **宿主开关**：`MC_TRANSPARENCY` 仅在宿主层解析（原则 13），默认 weighted，失败回落。

**第 1 步已完成（2026-10-31）**：`src/Sharpcraft.Framework/Rendering/OitWavelet.cs`（8 系数小波 encode/sample/coverage 的
CPU 参考，行为对齐 `oit_add_transmittance.glsl`/`oit_sample.glsl`，独立实现）+ `OitWaveletTests`（5 项）。
**实测通过**：单片覆盖度≈α（误差<0.02）、双层≈`1−(1−a₁)(1−a₂)`（<0.03）、编码顺序无关（系数逐位相等）、
空系数覆盖度=0、absorbance↔transmittance 往返。⇒ 算法理解与实现已由纯逻辑验证，可以上 GPU。

**第 2/3 步已完成（2026-10-31，GPU 三阶段 + composite + 开关）**：

- 新增 WGSL：`oit_reference.wgsl`（`vs_main` + `fs_depth_bounds`/`fs_transmittance`/`fs_accumulate`，含 8 系数编解码）、
  `oit_depth_bounds_cull.wgsl`、`oit_composite.wgsl`（`fs_composite` 写 `frag_depth`）。已登记进 `ShaderPipelines` 启动校验。
- `WindowedRenderer`：4 个目标（depthBounds / depthBoundsCulled / transmittance×2 / accumulate）+ 5 条管线 + cull + composite，
  帧循环 `if (useReference)` 分支（其余原 weighted 分支保留），resize 重建目标、退出 Disposal、静态半透明 3 次绘制。
- 开关：`FrameDiagnostics.TransparencyMode` + 宿主 `HostDebugOptions` 解析 `MC_TRANSPARENCY=reference`（默认 weighted，符合原则 13）。
- **修掉的编译/运行时坑**：WGSL `1 << power` 需 `1i << u32(power)`；`texelFetch`→`textureLoad`；
  **RGBA32F 在本后端不可混合** ⇒ 深度边界改用 RGBA16F（哨兵 `-65504`，线性深度在远处约 ±1–2 方块精度，reference 可接受）。
- **运行时实测**：确定性水景 reference 模式 **703 帧 / 0 validation error**；resize（960×540→640×360）后仍 0 error；
  weighted 默认模式回归正常（535 帧）。全量测试 1122 通过、文档门禁 ok。

**已知限制 / 下一步**：
1. **窗口像素仍无法在本环境截图**（交换链不可拷贝），reference 观感需所有者本机 `MC_TRANSPARENCY=reference` 目视。
2. 深度边界用 RGBA16F（RGBA32F 不可混合）带来的远处精度损失，若目视有问题再考虑分通道/两目标方案。
3. 未做：water mask、云/水面贴片深度、实体/粒子/云/天气的 OIT 集合、`OIT_ADDITIVE`（见 spec §11）。

## 2026-10-31 修：浮冰/蓝冰被误判为半透明（所有者截图为证）

现象：一整座浮冰/蓝冰山看起来像半透明雾。根因：`BlockRenderLayers` 的临时半透明表把 `minecraft:packed_ice`、
`minecraft:blue_ice` 也列入了半透明层 ⇒ 走深度写关的半透明路径。原版核实：`PACKED_ICE` 无 `.noOcclusion()`（实心），
`BLUE_ICE` 的 `HalfTransparentBlock` 只覆盖 `skipRendering`（同类面剔除），两者渲染层都是 solid；只有 `ICE` 带
`.noOcclusion()` 是半透明。修复：从半透明表移除二者，补 `frosted_ice`；`BlockRenderLayersTests` 增加 `packed_ice`/
`blue_ice` 不透明与 `frosted_ice` 半透明断言。全量 1125 通过。

## 2026-10-31 加权 OIT 封存，reference 成为唯一透明路径

所有者目视裁决：**加权混合 OIT 观感明显更差**。处理：
- 从工作树移除加权 OIT 的全部可编译产物：`accum`/`reveal` 目标、`translucentOitPipeline`/`oitResolvePipeline`、
  `oit_resolve.wgsl`、`terrain.wgsl` 的 `fs_translucent_oit`、`OitResolveTests`、`TransparencyMode` 与 `MC_TRANSPARENCY` 开关。
- 窗口透明**唯一走 reference OIT**（depth-bounds + 8 系数小波）；`CreateReferenceTargets` 变为无条件创建/重建。
- 加权实现保留在 git 历史（`25bc36a` 等），文档标注"封存、不编译"。
- 实测：reference-only 构建水景 540 帧 0 validation error；全量 **1122 通过**（去 3 条加权数学测试）、文档门禁 ok。

## 2026-10-31 平滑光照对齐原版 + 光照感知合并 + 合并开关

所有者截图指出冰面"诡异阴影"。核对原版 `BlockModelLighter`/`CardinalLighting`：原版与 Sodium 都**不贪婪合并**，我们的是自加优化。
本轮（先完善合并路径）：
- 面朝向亮度改原版基数（上1.0/下0.5/南北0.8/东西0.6）；AO 改 4 格 `0.2/1.0` 平均（去掉自创"两边都挡=0.5"）。
- **光照感知合并**：合并矩形（含外扩一圈）必须全"无遮挡且同一光照"才合并，否则逐面；合并输出逐值等于不合并。
- `MeshChunk(..., merge:)` 两路径完整；宿主 `MC_MESH_NOMERGE=1` 走逐面（窗口/离屏均已接）。
- 新增 `ChunkMesherMergeLightingTests`（3 项，含"多格 quad 四角颜色必须一致"）。全量 **1125 通过**、文档门禁 ok。

**下一步（未做，待所有者定）**：① 在真实场景量 `MC_MESH_NOMERGE` 与默认的面数/显存/p99 做 A/B；
② 若要更贴近原版，AO 仍用 `Occludes`（冰不遮挡）而非原版 `isCollisionShapeFullBlock`（冰会遮挡），属已知偏差；
③ 每顶点光照仍用自研 `Brightness()` 近似而非原版 lightmap。

## 2026-10-31 修：地形在 gamma 空间乘光照导致阴影过暗（露天也黑）

光照探针（`MC_LIGHT_PROBE=1`，宿主层）实测：地表 `sky=15 block=0` 满天空光 ⇒ **数据正确**，不是没读到光。
根因：图集 `RGBA8Unorm` 存的是 sRGB 编码值，硬件不解码；我们又直接乘线性光照再写非 sRGB 的 `BGRA8Unorm` ⇒
**满光(L=1)正常、任何 L<1 的面被过度压暗**（阴影/侧面发黑），与原版"线性乘光→sRGB 输出"不一致。
修复：新增 `litColour`（解码 2.2 → 乘线性光 → 编码），用于 `terrain.wgsl`（main/cutout/translucent）与
`oit_reference.wgsl`（accumulate）。水 demo 出图：石侧由近黑变中灰。全量 **1125 通过**、文档门禁 ok。

**下一步/未做**：原版 `Lightmap` 曲线（`light_brightness(x)=x/(4-3x)` + 环境光 + 天空/方块光颜色 + 时间 darkening）。
现在 `Brightness()` 仍是线性近似（在满光=1、中高光比原版偏亮，不会再偏暗）。若要逐值对齐再做。

## 2026-10-31 接入原版 lightmap（草案已实现，配等效性测试）

- 新增行为规格 `docs/specs/render/lighting.md`：lightmap 公式（`x/(4-3x)` 曲线、天光/方块光颜色、环境光、notGamma、亮度混合）、overworld 白天默认参数、色彩空间、等效性测试计划、已知偏差。
- 新增 `src/Sharpcraft.Client/Render/VanillaLightmap.cs`（§3 纯函数 + `LightmapParameters`，白天 overworld 默认）；`ChunkMesher.CornerShade` 改用它替换旧线性 `Brightness`，删除 `MinimumBrightness`。
- 等效性单测 `VanillaLightmapTests`（手算值：满光=白、无光=黑、sky12≈0.71875、方块光暖色）。全量 **1136 通过**、门禁 ok。
- 行为变化：无光面现在按原版为**黑**（旧 0.12 地板移除）；中高光比旧线性偏暗更接近原版曲线。
- 未做：夜晚/天气/维度 timeline 插值、夜视/Darkness/Boss、亮度滑块接入。
### 修复：顶点光照采样越界一格（正法线面）
`CornerShade` 的 `corner` 已是面顶点坐标，却又加了一次法线 ⇒ +X/+Y/+Z 面多采一格（顶面读到上方方块的光）。改为 `corner + min(n,0)`。全量 1136 通过。
### 根因修复：省略的天空光段默认 15（整体发黑 / 海平面附近反常亮）
`LevelChunk.ApplyLight` 只为"带数据的天光段"赋值，忽略了协议语义：未下发、也不在 emptySkyMask 的段应默认 **15**。结果露天大片读 0 → 整体偏暗，只有少数带数组的地表段正常 → 海平面附近反而亮。
修复：非空掩码且无数组的段填整段 15（共享 0xFF 数组）。新增 `ChunkLightTests.SectionsThePacketOmitsAreFullSkyUnlessMarkedEmpty`。全量 **1137 通过**、门禁 ok。行为规格见 `docs/specs/render/lighting.md` §2。
- 新增宿主开关 `MC_NO_AO`（关 AO）用于 A/B；AO 通过 Mesh/MeshChunk 显式传参，游戏层不读环境变量。

- AO 实机对比远比原版暗 → 游戏**默认关闭 AO**（`MC_AO=1` 可开）；AO 强度校准列为待办。全量 1137 通过、门禁 ok。

### AO 真正根因修复：空气被当成遮挡物
`Occludes` 对空气(state 0)走"无布局→不透明"分支 → AO 4 格平均塌到 0.2 下限 → 全世界发暗；关 AO 就正常。修复：`Occludes` 对空气短路为不遮挡。AO 抽成纯函数 `ChunkMesher.AmbientOcclusion`（4 格 0.2/1.0 平均），新增 `AmbientOcclusionTests`（含孤立方块顶面应全亮的集成测试）。**AO 恢复默认开启**，`MC_NO_AO=1` 可关。全量 1141 通过、门禁 ok。仍缺原版拐角柔化（待校准）。- AO 等效性：所有者确认"现在 AO 看起来非常像原版"（2026-10-31）。AO 默认开启；拐角柔化为可选优化。- B：AO 加入原版拐角柔化（两邻边都实心时对角用边格值）；AmbientOcclusionTests 增加该规则用例。全量 1142 通过。
- A（日夜/天气/维度）：明确**暂未实现**，已在 docs/specs/render/lighting.md §6 写明两个硬前提（缺时间包解析、lightmap 需移到着色器）与落地步骤。- C（半透明水/冰亮度）：查证——水有生物群系色染色（BiomeTint.Water 默认 0x3F76E4 蓝），reference OIT 的单层 accum/composite 数学上等价于标准 alpha 混合（accum.rgb/accum.a 归一后 coverage=alpha）。未发现系统性压暗点；残余可能是水雾（water fog）未实现与 OIT 顺序无关近似的差异。**待固定机位对照再定**，暂不改动。## 2026-10-31 方块状态模块评审通过 + 流体状态解码（步骤 A）
- `docs/modules/block-state.md` 评审通过（[已评审]，含 §9 模组热重载规划）。
- 新增 `Sharpcraft.Common/World/FluidState.cs`：`FluidState`（Kind/Amount/Falling/IsSource/OwnHeight=amount/9）+ `FluidStateCodec.FromBlockState(stateId, layout)`（level=id-base，0=源/1..7 流动/8..15 下落）。`FluidStateTests` 9 项通过。
- 全量 1142+ 通过、门禁 ok。下一步：写 `docs/specs/render/fluid-rendering.md` 并实现流体网格（四角高度/flow-UV/水色/逐面光/overlay）。## 2026-10-31 流体渲染：规格评审通过 + 几何核心（静态帧第 1 步）
- `docs/specs/render/fluid-rendering.md` 评审通过（OIT 纠正为 26.3 原版机制，非偏差）。
- 调查：图集**只拼接动画首帧**（`TakeFirstFrame`）⇒ 流动动画需先做图集动画；水雾=WaterFogEnvironment。
- 新增 `Sharpcraft.Client/Render/FluidSurface.cs`（ownHeight=amount/9、原版 calculateAverageHeight 四角加权），`FluidSurfaceTests` 5 项通过。
- 全量 **1156 通过**、门禁 ok。
- **下一步**：给 MeshBuffer 加浮点坐标 `AppendQuad`（现仅整数）→ 实现流体几何发射（四角高度/面剔除/flow-UV/染色/逐面光/overlay）→ 接半透明层 → 离屏水面出图；再补水下雾与图集动画。- 流体几何（静态，进行中）：`ChunkMesher` 的水/岩浆液面高度由固定 14/16 改为原版 **amount/9**（源水 8/9、流动更低、上方同种流体=1.0），用 `FluidStateCodec` 解 level；`FluidWaterHeightTests` 2 项（源水/流动高度）通过。全量 1158 通过。
- 待续：四角高度（`FluidSurface` 已备但未接线）、flow-UV、per-face 光、overlay、不合并的独立流体网格；水下雾；图集动画。### 流体几何：关键教训（2026-10-31）
尝试"水不合并 + 顶面四角高度"时发现：**不做"同种流体面剔除"就逐方块发射水，会把整段水的内部面全发出来**（一个 16³ 水段 60 合并 quad → 7710 quad，性能灾难）。结论：流体必须走**独立通道并做同种流体面剔除**（相邻同种流体→跳过该面），再谈四角高度/不合并。该尝试已回退，保持全量 1158 绿。下一步：实现含同种流体剔除的 `FluidMesher` 独立通道。### 流体几何：独立通道落地（2026-10-31）
`ChunkMesher` 新增 `EmitSectionFluids`：逐水块发射、**同种流体面剔除**（相邻水→跳过该面）、四角高度（`FluidSurface`）、still 贴图、生物群系水色、**逐面光**（max(本格,上方)）、按朝向着色；方块面通道在提供 translucent 缓冲时**跳过流体**。`FluidMesherTests`（满水段外表面 6×16×16、单块 6 面）+ LayerSplit 修正 + 水高测试；全量 **1160 通过**、门禁 ok。
- 待续：flow 向量与旋转 UV、overlay、反向顶面；水下雾；离屏水面固定机位像素验收（窗口不可截，需离屏场景或所有者目视）。### 流体目标现状（第 10 轮，未完成，如实记录）
已完成：独立流体通道（同种流体面剔除、四角高度、still 贴图、水色、逐面光）+ 接入半透明层；`FluidMesherTests` 满水段外壳 1536 / 单块 6。
未完成（objective 仍需继续）：
1. flow 向量 + 旋转 flow UV —— **阻塞点**：`BlockSpriteMap` 只解析 `_still`，未暴露 `_flow`；需先加 `_flow` 精灵查询接口。
2. overlay（相邻半透明/树叶用 water_overlay）——需 `_overlay` 精灵查询。
3. 反向顶面 shouldRenderBackwardUpFace。
4. 离屏固定机位水面场景 + 像素探针验收（窗口不可截）。
5. 水下雾（WaterFogEnvironment：WATER_FOG_COLOR/距离/sky-cloud end）——需把"相机在水中"从宿主传入 fog uniform。
结论：目标**保持 active，不标记完成**。下一步建议先做 `BlockSpriteMap` 的 `_flow`/`_overlay` 查询（资产层小改），再做 flow-UV/overlay；随后水下雾与离屏验收。- 流体 flow：`BlockSpriteMap.FluidFlow/FluidOverlay` 增加 `_flow`/`_overlay` 查询；`EmitSectionFluids` 计算 flow 向量（仅同种水邻居的高度差），顶面流动时用 `_flow` 精灵按 `atan2(flowZ,flowX)-π/2`、半径 0.25 旋转 UV，静水用 still。全量 1160 通过。
- 待续：overlay 应用（相邻半透明/树叶）、反向顶面、水下雾、离屏固定机位像素验收。- 流体侧面修正（对照原版 FluidRenderer）：侧面顶部改用与顶面一致的**四角高度**（hNW/hNE/hSW/hSE 映射与 NORTH/SOUTH/WEST/EAST 取法逐一核对一致），并在法线轴上做 **±0.001 内缩**避免共面 z-fighting。消除水面上"硬台阶/薄缝"。全量 1160 通过。- 流体落差缝修正（对照 FluidRenderer.getHeight）：**上方有同种流体时该格四角高度一律取 1.0**（原版 `hasSameAbove ? 1.0 : ownHeight` 且 `heightSelf>=1 → 四角=1`），消除水台阶/竖直水体的顶边缝。全量 1160 通过。- 流体"流动块↔满格块"接缝（等效性修正）：`CellHeight` 计算邻居高度时，若该邻居**上方也是同种流体**则返回 1.0（原版 `getHeight=hasSameAbove?1.0:ownHeight`）。这样流动块的共享角会抬到 1.0 与满格块相接（`h>=1→角=1`，`FluidSurfaceTests.FullEdgeNeighbourLiftsTheCornerToOne` 已覆盖该数学）。消除不完整/完整流体块之间的缝。全量 1160 通过。- 流体角落缝（对照 calculateAverageHeight）：补上"两条边之一有流体"分支内 **对角格≥1.0 → 角直接取1.0**（此前漏了这条，对角是满格水柱时角没抬到1 → 高低差块间缝）。`FluidSurfaceTests.FullDiagonalColumnLiftsTheCornerToOne` 覆盖。全量 1161 通过。- 流体侧面贴图（对照 FluidRenderer）：侧面改用 **`_flow` 精灵**，V 坐标按角高度 `v=(1-h)*0.5`、底部 `v=0.5`（呈向下流动），U 取 0/0.5。此前侧面用 still 且 V=0..1。全量 1161 通过。## 2026-10-31 流体静态几何收口 + block-shape 模块文档（暂缓）
- 流体静态几何核心已交付并在所有者处目视确认：独立通道 + 同种流体面剔除 + 四角高度(含 getHeight/对角≥1/边≥1 规则) + 侧面四角+内缩 + 侧面 `_flow` 高度映射 V + 顶面 still/flow 旋转 UV + 水色 + 逐面光；1161 测试通过。
- **遮挡判定**：原版 `isFaceOccludedByState` 依赖方块**遮挡/碰撞形状(VoxelShape)**，非模型几何；该数据与**方块实体/碰撞行为**强相关（未开发）→ 按所有者指示**暂缓实现，先建文档**：`docs/modules/block-shape.md`（[规划]/暂缓）。流体暂用 `IsSolid` 近似，记为受控偏差。
- **流体 backlog（未来）**：overlay、反向顶面、flow 下落偏置/下方回退、水下雾、精确遮挡形状(block-shape)、离屏固定机位像素验收、纹理动画(图集动画)。
- 下一步：本目标视为收口，转下个模块（M5.3 渲染剩余：天空天体/实体模型/半透明排序证据/方块实体；或 5.2 UI）。- 修复回归：流体通道只处理 **water**（`IsFluidState` 仅 water）；此前把 lava 也跳过块通道却没在流体通道发射 → 岩浆不可见。lava 恢复为不透明方块路径。新增 `FluidMesherTests.LavaStaysOnTheOpaqueBlockPath`。全量 1162 通过。- 岩浆流体：原版 FluidRenderer 对任意流体通用，仅**渲染层**不同（水=半透明、岩浆=solid 不透明）、水有生物群系色、岩浆无。改为**岩浆也走同一流体通道**（`EmitSectionFluids` 泛化：按液体种类分流—water→translucent、lava→opaque；同种流体剔除/四角高度/侧壁/flow 复用；岩浆无 tint）。此前岩浆落到 `EmitMergedQuad` 旧分支（每格降高、无侧壁）→ 空洞/悬浮。全量 1162 通过。- 岩浆空洞根因（读 FluidRenderer.addFace 的 addBackFace）：**原版流体面是双面的**（侧面 addBackFace=!isOverlay、顶面=shouldRenderBackwardUpFace）。我们单面：水走 OIT 不剔除背面所以没事；**岩浆走不透明通道（背面剔除）→ 面被剔光 → 空洞**。修：岩浆面双面发射（`MeshBuffer.AppendQuadDoubleSided`），水保持单面（避免 OIT 叠加变暗）。此前的测试崩溃是本地函数 `Emit` 自递归，已修。全量 1162 通过。- 岩浆接缝根因（真读 FluidRenderer.tesselate 62-117）：邻居高度按 **`fluidType.isSame`** 取（同型流体邻居贡献 ownHeight）。我们泛化时漏了：`CellHeight`/`AddFlow` 仍写死 `Path=="water"` → 岩浆邻居被当"实心 -1"忽略 → 相邻不同液位岩浆块四角不接、共享侧又被同种流体剔除 → 缝隙/悬浮。修：改用 `fluidPath`。新增 `FullLavaSectionEmitsADoubleSidedOuterShellIntoOpaque`（2×6×16×16）。全量 1163 通过。## 2026-10-31 天空天体（A）规格 + 依赖
- 流体岩浆已修（`d4fc7f4`）：`CellHeight`/`AddFlow` 按流体质心类型泛化；岩浆双面外壳。所有者确认"岩浆差不多"；遮挡精确形状留方块实体后（block-shape backlog）。
- 新增 `docs/specs/render/sky-celestial.md`（待评审）：读 SkyRenderer 常量/变换/几何（盘r512、星1500@100、日30@100、月20@100、日出16段、月相8）。
- **硬依赖**：天体角度/月相/亮度/天空色/日出色均来自环境属性（时间轴驱动）→ 需**世界时间**（未解析，与 lighting.md §6 日夜同缺口）。
- 下一步（待所有者定）：①先补世界时间包解析（可同时解锁日夜光照）；或②用宿主注入时间/角度先验证天体渲染正确性。## 2026-10-31 时间系统模块文档（待评审）
- 新增 `docs/modules/time.md`（待评审）：`set_time` 新结构（long gameTime + Map<WorldClock,ClockNetworkState{totalTicks,partialTick,rate}>）；dayTime=overworld.totalTicks%24000；环境属性由 Timelines 关键帧/easing 求值（SUN/MOON/STAR_ANGLE 优先）。含 packet id 待核对、测试、落地步骤与风险。评审通过后按步骤实现（packet→clock→timeline→消费）。## 2026-11-01 时间系统实现（第一片：包→时钟→dayTime）
- `PacketIds.PlayClientbound.SetTime=115`（经 GameProtocols clientbound 注册序校准，+1 在 0..127 已知 id 全吻合）。
- `SetTimePacketCodec`：VarInt id + long gameTime + VarInt count + 每时钟(VarInt id + VarLong totalTicks + Float partial + Float rate)；单测 2 项。
- `ClientWorld`：每时钟 `ClockNetworkState`；`GameTime`、`DayTime=overworld( id0 ).totalTicks%24000`、`ApplyTime`、`AdvanceClocks`；单测 4 项。
- `InteractiveSession` 分发新增 `SetTime` → `world.ApplyTime`。
- 全量 1168 通过、门禁 ok。**待办**：Timelines 关键帧/easing 求值（SUN/MOON/STAR_ANGLE 等）→ 再接天空天体与日夜光照；`AdvanceClocks` 目前按 ceil?（rate 取整）每 tick 推进，后续补 partial 精度。## 2026-11-01 时间系统第二片：Timelines 角度求值
- 新增 `Common/World/SkyTimelines.cs`：`CubicBezierEasing`（复刻 EasingType.CubicBezier：Newton 4 次、步长±0.25、二分回退）、`FloatKeyframeTrack`（复刻 KeyframeTrackSampler：period 环绕+环绕段+lerp(ease(alpha))）、`SkyTimelines.SunAngle/MoonAngle/StarAngleDegrees`（SUN/STAR 6000→360/0、MOON 6000→540/180，easing symmetric(0.362,0.241)）。
- `SkyTimelinesTests` 4 项（端点/中点0.5/单调/锚点：Sun 6000=0、18000=180、30000=0；Moon 18000=360）。全量 1173 通过、门禁 ok。
- 待办：`SKY_LIGHT_FACTOR/COLOR` 等其余属性求值；`AdvanceClocks` partial 精度；再回接天空天体渲染与日夜光照。## 2026-11-01 时间系统第三片：其余环境属性
- `FloatKeyframeTrack` 支持线性 easing（Func）；`SkyAttributeTracks`：**STAR_BRIGHTNESS**（MAX 修饰→采样值）、**SKY_LIGHT_FACTOR**（日1.0/夜0.24）、**MOON_PHASE 索引**（按 totalTicks，period 24000*8）。
- 测试 +3（星亮度 6000=0/18000=0.5；天光系数 6000=1/18000=0.24；月相 0/3/8 环绕）。全量 1176 通过、门禁 ok。
- 待办：SUNRISE_SUNSET_COLOR（33 关键帧 RGBA）、SKY_COLOR 修饰、SKY_LIGHT_COLOR；再回接天空天体渲染与日夜光照。AdvanceClocks partial 精度。## 2026-11-01 时间系统第四片：日出/日落色 + 天空光色
- `SkyAttributeTracks` 增：**SUNRISE_SUNSET_COLOR**（33 关键帧 ARGB32 → 逐通道线性 RGB + 线性 alpha，4 条线性轨）；**SKY_LIGHT_COLOR**（白×颜色修饰：昼 (1,1,1)、夜 (linear(0.48),linear(0.48),1)）。
- 测试 +2（日出正午 alpha=0；天空光色昼白/夜蓝）。全量 1178 通过、门禁 ok。
- 时间数据层基本齐全（角度/星亮度/天光系数/日出色/天光色/月相）。下一步：回接天空天体渲染 + 日夜光照消费。