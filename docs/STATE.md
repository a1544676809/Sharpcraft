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

最后更新：2026-11-03

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
- 时间数据层基本齐全（角度/星亮度/天光系数/日出色/天光色/月相）。下一步：回接天空天体渲染 + 日夜光照消费。## 2026-11-01 天空天体：几何等效核心（目标 A）
- 新增 `Client/Render/CelestialGeometry.cs`：日/月四边形（XZ -1..1）在原版矩阵链 **Scale(size,1,size)→Translate(0,height,0)→RotX(angle)→RotY(-90)** 下的顶点；日出三角扇（中心(0,100,0)+16环，r120/z40）。
- `CelestialGeometryTests` 3 项手算等效（角度0/90°顶点、日出扇形中心/环点）通过。全量 1181 通过（一次偶发 `ChunkSeamTests` 抖动，复跑全绿）、门禁 ok。
- 待办：天体贴图集（sun/moon）从 pack 取；天空 pass 用 `CelestialGeometry` 绘制盘/日月/星/日出；`DayTime`→角度/亮度驱动；离屏像素验收。## 2026-11-01 天空天体：贴图切片核心
- `Client/Assets/CelestialSprites.cs`：天体贴图资源位于 `textures/environment/`（sun、moon_phases，独立于方块图集）；`SliceMoonPhases` 把 4×2 月相表切成 8 个 row-major 矩形。测试 2 项（切片坐标、非整格报错）。全量 1183 通过、门禁 ok。
- 待办：GPU 侧把 sun/moon 贴图上传 + 天空 pass 用 `CelestialGeometry` 画盘/日月/星/日出（`DayTime`→角度/亮度），离屏出图+像素验收。这是较大的渲染切片。## 2026-11-01 天空天体：网格构建 + 着色器
- `Client/Render/CelestialMeshBuilder.cs`：把 `CelestialGeometry` 四边形打包成 position3+uv2 顶点 + 三角索引（UV 顺序同原版 buildCelestialQuad），测试 1 项手算。
- `Framework/.../pipelines/celestial.wgsl`：`vs_celestial/fs_celestial`，uniform=相机 viewProjection + `CelestialTransform{model,tint}`，采样天体纹理×tint。
- 全量 1184 通过、门禁 ok。
- 待办：设备侧建 celestial 纹理/管线/uniform、在天空 pass 后绘制（先离屏太阳最小验证，再月/星/日出），像素验收。## 2026-11-01 天空天体：太阳设备接线 + 26.3 贴图路径更正
- **更正（对照 pack 实读 + SkyRenderer）**：26.3 天体贴图在 `textures/environment/celestial/`——`sun.png` 与 8 张独立月相 `moon/<phase>.png`（`MoonPhase` 枚举 0..7：full_moon/waning_gibbous/third_quarter/waning_crescent/new_moon/waxing_crescent/first_quarter/waxing_gibbous），**不是** 4×2 月相表。`CelestialSprites` 重写：`Sun=environment/celestial/sun`、`MoonPhase(i)=environment/celestial/moon/<name>`；删除 `SliceMoonPhases`/`CelestialSprite`。上方 10-31 "moon_phases 4×2" 旧表述作废。
- **设备接线（离屏）**：`FrameRequest` 增 `CelestialTextures`/`CelestialDraws`（每 draw=纹理索引 + pos3/uv2 顶点 + 索引 + model + tint）；`OffscreenRenderer` 新增 `CelestialResources`：上传天体纹理、`celestial.wgsl` 管线（顶点布局收敛到 `VertexLayout.CelestialAttributes`）、每 draw 相机 + model/tint uniform + 绑定组；天空渐变之后、地形之前绘制，深度恒通过不写。**合成用原版 `BlendFunction.OVERLAY`（颜色 SrcAlpha/One、alpha One/Zero）**——黑色/透明像素不贡献，太阳贴图透明角不再画成黑块。
- **角度驱动**：`FrameCommand.AttachCelestial` 用 `world.DayTime`→`SkyTimelines.SunAngleDegrees`（`MC_SUN_ANGLE` 可覆盖验证）；`model=translate(eye)` 让天体相对相机（视作无穷远）。已接入 `RenderWorld`（真服/回放）与 `RenderLocal`。
- **GPU 等价验收（离屏，`MC_SUN_TEST=1`）**：纯白贴图时太阳四边形质心投影到画面正中，中心像素 `rgba(255,255,255,255)`；改用 pack 真 `sun.png`（32×32）后中心 `rgba(255,255,255,255)`、可见暖白光晕且**无黑块**。出图 `%TEMP%\sharpcraft-sun-test.png`。
- 修复架构门禁 `Repro_001`（`VertexAttribute` 只允许在 `VertexLayout.cs`）：天体顶点属性移入 `VertexLayout.CelestialAttributes()`。
- 全量 **1184 通过**、0 警告。**待办**：月亮（按 `MOON_PHASE` 选贴图）、星空（1500）、日出条带（三角扇+顶点色）、天空盘/暗盘；亮度接 `STAR_BRIGHTNESS`/`RAIN_BRIGHTNESS`；镜像到 `WindowedRenderer`；随后日夜光照。## 2026-11-01 天空天体：月亮接入
- `AttachCelestial` 增月亮：按 `SkyAttributeTracks.MoonPhaseIndex(overworld.totalTicks)` 选 `environment/celestial/moon/<phase>`，角度 `MoonAngleDegrees`、尺寸 20 / 高度 100；与太阳共用 celestial 管线（多纹理 / 多 draw，`CelestialResources` 已支持）。
- 验收：`MC_MOON_TEST=1` + pack 渲染 full_moon（32×32）中心 `rgba(255,255,255,255)`、无黑块；本地帧日志 `celestial: 2 texture(s), 2 draw(s)`；全量 **1184 通过**。
- 待办：星空、日出条带、天空盘/暗盘、`RAIN_BRIGHTNESS` 亮度、镜像 `WindowedRenderer`、日夜光照。## 2026-11-01 天空天体：日出条带 + 星空
- **新增 position-colour 管线** `Framework/.../pipelines/position_color.wgsl`（端口 core/position_color：clip=Proj·ModelView·Pos，顶点色×ColorModulator，α=0 丢弃）；顶点布局 `VertexLayout.PositionColorAttributes`（pos3+color4）；两种混合：日出 `TRANSLUCENT`、星空 `OVERLAY`。`ShaderPipelines.All` 补 `celestial`/`position_color` 描述符（启动自校验）。
- **日出**：`CelestialGeometry.SunriseFanWorld(theta, alpha)`（RotX90·RotZ·ScaleZ 展开，18 顶点）；颜色中心白 α=1 / 环白 α=0；α≤0.001 不画。
- **星空**：`Client/Render/StarField.cs`——`LegacyRandom`（java.util.Random 兼容 48 位 LCG）+ `buildStars` 复刻（seed 10842、候选 1500、长度∈(0.010000001,1)、距离 100、随机自旋）；`CelestialGeometry.SkyRotate` 按 STAR_ANGLE 旋转；`OVERLAY` 色调=STAR_BRIGHTNESS。
- **等价性验证**：纯逻辑 `StarFieldTests`（Java Random(0) 向量 nextInt=-1155484576 / nextFloat=0.73096776；星点确定性、均落在 r=100 球面）、`CelestialGeometryTests`（SunriseFanWorld / SkyRotate 手算）。GPU：`MC_COLOR_TEST` 同帧加/不加 colour draws → 改变 **45145 像素**（780 颗星 + 红色日出扇）；本地帧 `celestial: 2 textured draw(s), 2 colour draw(s) … sunrise a=0.445, stars 0.055`。
- 全量 **1188 通过**、0 警告。待办：天空盘/暗盘、`RAIN_BRIGHTNESS`、镜像窗口、日夜光照。## 2026-11-01 日夜光照（离屏路径）
- `LightmapParameters.OverworldAt(dayTime)`：`SkyFactor=SkyLightFactorAt`、`SkyLightColor=SkyLightColorAt`（昼 1/白、夜 0.24/(0.48,0.48,1) 线性），其余昼默认。
- `ChunkMesher` 贯穿 `LightmapParameters`（Mesh → MeshChunk → MergeSectionFaces → EmitMergedQuad → CornerShade，及 EmitSectionFluids）；`FrameCommand` 以 `EffectiveDayTime(world)`（`MC_FORCE_DAYTIME` 可覆盖）驱动网格与天体角度。离屏每帧重建网格 ⇒ 顶点色随 DayTime 更新，**无需改顶点格式/着色器**。
- **等价性**：`VanillaLightmapTests` +2（正午全白天体=1；夜全白天体≈(0.088,0.088,0.453)，R=G<B）；本地帧地形 top-mid 昼 `rgba(136,136,136)` → 夜 `rgba(45,45,95)`。
- 全量 **1190 通过**、0 警告。**未做**：窗口路径仍昼参数（缓存网格；正确做法=着色器侧每帧 UBO lightmap，需改顶点格式）；天气/维度；天空盘/暗盘、`RAIN_BRIGHTNESS`。## 2026-11-01 日夜光照接入实时路径（缓存按参数重建）
- `ChunkMeshCache.Lighting` / `ChunkWorkerPool.Lighting`（默认昼）：记录所用 `LightmapParameters`；变化时缓存整体失效、按新参数重网格化（`ChunkMeshCache.Build` 内 `_appliedLighting` 比较后 `_meshes.Clear()`）。
- `WindowCommand`：启动与每帧用 `LightmapParameters.OverworldAt(world.DayTime)` 设置两者；参数变化即 `meshCache.Clear()+QueueMissing(world)` 分片重建。
- **验证**：`ChunkMeshCacheTests.ChangingTheLightmapParametersRemeshesEveryChunk`（同参 0 重建、变参全重建）；离屏日夜像素证据见上一条。全量 **1191 通过**、0 警告。
- **遗留（需 ADR）**：真正零重建=着色器侧每帧 UBO lightmap，须改 12-float 顶点契约（跨模块），按原则 12 待所有者确认 + ADR 后再做。天气/维度、天空盘/暗盘、`RAIN_BRIGHTNESS` 未做。## 2026-11-01 顶点契约变更：着色器侧 lightmap（ADR-0019，所有者“可以修改”授权）
- **契约**：顶点 12→14 float（pos3 + base3(tint×shade×AO) + uv2 + spriteRect4 + light2(sky,block)）；`FrameRequest.FloatsPerVertex=14`、`VertexLayout.Stride=56`（新增 `Float32x2`@48 loc4）。占位实体/方块实体网格补 light=(1,0)。
- **着色器**：`terrain.wgsl`/`oit_reference.wgsl` 加 light 属性 + `Lightmap` UBO（binding4，64B），片元逐片元 `lightmapColour(sky,block)` 精确复刻 `VanillaLightmap.Colour`（brightness/parabola/notGamma/brightnessFactor）；`ChunkMesher` 不再烘焙光图（`CornerShade` 返回 base+等级，`MeshBuffer` 写 light2，流体同步）。
- **渲染器**：离屏与窗口都建 lightmap UBO 并按帧写 `request.Lightmap`；`LightmapUniformCodec` 编码；`FrameCommand.ToLightmapUniform(LightmapParameters.OverworldAt(EffectiveDayTime))`。
- **等价验证**：纯逻辑 `VanillaLightmapTests`（昼 1 / 夜≈(0.088,0.088,0.453)）+ 出图：本地**同一网格仅换 UBO**，地形 top-mid 昼 `rgba(136,136,136)`→夜 `rgba(45,45,95)`，夜空见星。全量 **1191 通过**、门禁 ok。
- **窗口运行时像素未验收**（本环境无可见窗口）：窗口渲染器已同源接线（binding4/UBO）但仅编译通过。未做：天气/维度、夜视/暗化、方块光闪烁；模块 A 仍欠天空盘/暗盘、`RAIN_BRIGHTNESS`。## 2026-11-01 天体/光照 sub-tick 插值（对照原版 EnvironmentAttributeProbe）
- **实读原版**：`EnvironmentAttributeProbe.ValueProbe.get` = `partialTickLerp.apply(partial, lastValue, newValue)`——在**上一 tick 与当前 tick 采样值**间线性插值，**不是** t+partial 重采样。
- 落地：`ClientWorld.DayTimePartial`（overworld clock PartialTick，clamp 0..1）；`SkyTimelines`/`SkyAttributeTracks` 增 `(int dayTime, float partial)` 重载（采样 t-1 与 t 后按 partial 线性插值）；`LightmapParameters.OverworldAt(int, float)` 对 skyFactor/skyColor 同法插值；`FrameCommand.EffectiveDayTimePartial`（`MC_FORCE_PARTIAL` 可覆盖）驱动天体角/星亮度/日出色/日夜光照。
- **等效验证**：`SkyTimelinesTests` 两条（lerp(t-1,t,partial) 端点/中点；扫描跨 tick 太阳角>0.5° 处断言中点严格介于两者）+ `VanillaLightmapTests` 两条（skyFactor 跨 tick 插值；逐片元 vs 烘焙 0.3952 vs 0.5）；端到端同 tick(6000) partial 0/0.5/1 → 太阳 360°/180°/0°。全量 **1195 通过**、门禁 ok。窗口像素未验收。## 2026-11-01 天空盘/暗盘：几何 + 判定（渲染仍渐变，受控偏差）
- **实读原版**：`buildSkyDisc(yy)` = 中心(0,yy,0)+i=-180..180/45 环（x=sign(yy)·512·cos、z=512·sin）；`renderSky` 顺序 SkyDisc(SKY_COLOR)→Sunrise→SunMoonStars→(shouldRenderDarkDisc) DarkDisc；`shouldRenderDarkDisc` = `eyeY−horizon<0 && !underwater`。
- 落地（纯逻辑）：`CelestialGeometry.SkyDisc(float y)`（10 顶点，上下盘 ±16）、`SkyVisibility.ShouldRenderDarkDisc(eyeY,horizonY,underwater)`；测试 `CelestialGeometryTests.SkyDiscMatchesTheBuildSkyDiscFormula`、`SkyVisibilityTests`(4)。
- **未接渲染（受控偏差）**：`SKY_COLOR` = 生物群系温度色 × `MULTIPLY_RGB` 关键帧 × 天气灰度（`OverworldBiomes.calculateSkyColor`、`WeatherAttributes`、`Timelines.NIGHT_SKY_COLOR_MULTIPLIER`），依赖群系+天气 → 暂不用盘替换全屏渐变。`RAIN_BRIGHTNESS` 同样阻塞于天气。
- 全量 **1200 通过**、门禁 ok。模块 A 收口：日月星/日出/日夜光照/sub-tick 均完成并验证；天空盘/暗盘仅几何/判定就绪、盘颜色与 `RAIN_BRIGHTNESS` 阻塞于「生物群系+天气」。## 2026-11-01 天气/群系天空色：模块设计文档（[待评审]，未编码）
- 新增 `docs/modules/weather-and-sky-color.md`（状态 [待评审]）：实读原版——**方块调色**（`GrassColor/FoliageColor/DryFoliageColor` colormap 索引 `y<<8|x`、`grassColorModifier` NONE/DARK_FOREST/SWAMP、`calculateBlockTint` 按 `biomeBlendRadius`(默认2) 逐通道整数平均、per-biome `waterColor`）+ **天空色** `calculateSkyColor`(HSV) 与 gaussian 空间混合(kernel 0,1,4,6,4,1,0)、时间轴 `MULTIPLY`、`WeatherAttributes.RAIN/THUNDER` 按 `(rain-thunder)/thunder` 权重重叠、`ClientboundGameEventPacket`(type byte+float；RAIN_LEVEL_CHANGE=7/THUNDER=8) 同步、`RAIN_BRIGHTNESS=1-rain`。含 §9 纯逻辑等效测试、§10 分期、§7 风险；并登记现有 `BiomeTint/BlockTintTable` 为全局近似、需升级为逐生物群系。
- **未编码**：按 AGENTS 3.5 先文档、待所有者评审（[待评审]→[已评审]）后再实施。硬前置=chunk biome 数据与 biome registry 温度（客户端未解析）。## 2026-11-01 天气/群系色 P1 纯逻辑（第一步，文档已[已评审]）
- 模块文档转 **[已评审]**（所有者 2026-11-01 通过）。按 §10 分步实施，P1 纯逻辑（Common，无数据/资产）。
- **P1.1** `Common/World/ColorMapColorUtil.Get`：colormap 索引 `rain=downfall*temp; x=(1-temp)*255; y=(1-rain)*255; index=y<<8|x`，越界返回 default（复刻 `ColorMapColorUtil.java`）。
- **P1.2** `Common/World/SkyColour.FromTemperature`：`t=clamp(temp/3,-1,1)`，HSV `(0.62222224-0.05t,0.5+0.1t,1.0)` → opaque ARGB（复刻 `calculateSkyColor`+`Mth.hsvToArgb`）。
- 单测 6（`ColorMapColorUtilTests` 3、`SkyColourTests` 3，均手算）。全量 **1218 通过**、门禁/架构 ok。
- **P1.3** ✅ `Common/World/ColourBlend`（greyscale `0.3/0.59/0.11`、scaleRGB clamp、srgbLerp、BlendToGray）+ `WeatherBlend`（`RainBrightness=1-rain`、`SkyLightFactor`/`ApplySkyColour` 按 `rain−thunder`/`thunder` 权重重叠）；`WeatherBlendTests`(6)。
- **P1.4** ✅ `BlockTint.Calculate`（`(2r+1)²` 逐通道整数平均，r=0 单点）+ `GaussianWeights`（7 抽头 `{0,1,4,6,4,1,0}`、3D 乘积、总和 4096）；`BlockTintTests`(3)+`GaussianWeightsTests`(3)。**P1 纯逻辑全部完成**。
- **下一步 P2（天气同步，Client）**：`ClientboundGameEventPacket` 解析（id 锚点校准）+ `WeatherState`（rain/thunder + 按 partial 插值）+ 把 `RainBrightness` 接到日月/星 alpha；随后接 `SkyLightFactor`/`ApplySkyColour` 到日夜光照与天空。P3（biome 数据，硬前置）另起。## 2026-11-01 P2 天气同步（Client）
- **P2.1** `PlayClientbound.GameEvent=39`（GameProtocols clientbound 序 index 40，id=index−1；锚点 SetTime 116→115、Login 51→50）；`GameEventPacket`(byte type + float param)+`GameEventPacketCodec`；`GameEventPacketTests`(2)。实读确认 START/STOP 为占位（0/1），真实值由随后 `RAIN_LEVEL_CHANGE`/`THUNDER_LEVEL_CHANGE` 给出。
- **P2.2** `ClientWorld` 天气状态：`SetRainLevel/SetThunderLevel`(o=current)、`RainLevel(partial)`/`ThunderLevel(partial)`(thunder×rain，照 `Level.getThunderLevel`)、`ApplyWeather`(START 0/STOP 1 quirk + 7/8 level)；`InteractiveSession` 分发 `GameEvent`；`ClientWorldWeatherTests`(4)。
- **P2.3** `RAIN_BRIGHTNESS=1-rain` 接日月 tint alpha；星亮度按 `(1−rainWeight)(1−thunder)` 衰减；`MC_FORCE_RAIN/THUNDER` 验证。本地夜 rain 0→stars 0.500/alpha 1.00，rain 1→stars 0.000/alpha 0.00。
- 全量 **1224 通过**、门禁/架构 ok。**下一步**：P3（chunk biome 数据 + registry，硬前置；接方块调色逐群系与天空盘替换）或先把天气叠加进 `LightmapParameters`/`SKY_LIGHT_*`（可纯逻辑测）。P3 工作量最大，建议单独一轮。## 2026-11-01 P4.1 天气叠加进 lightmap（SKY_LIGHT_FACTOR）
- `FrameCommand.EffectiveLighting(world)`：`LightmapParameters.OverworldAt(dayTime,partial)` 之后按 `WeatherBlend.SkyLightFactor(base,rain,thunder)` 叠加天气（`EffectiveRain/Thunder` 复用，含 env 覆盖）；`BuildRequest` 与 `RenderLocal` 统一用它。
- 日月/星 alpha 与光照共用同一 rain/thunder（去掉重复内联解析）。
- **验证**：`WeatherBlendTests`(+1：day base1 rain1→0.7625、night 0.24 不变)；本地白天 rain 0→top-mid `rgba(136,136,136)`、rain 1→`rgba(128,128,128)`（变暗）。全量 **1225 通过**、门禁 ok。
- **未做（P4 其余）**：`SKY_LIGHT_COLOR` 天气 alphaBlend、`FOG_COLOR`/`CLOUD_COLOR`、`SUNRISE_SUNSET_COLOR` 天气乘子（均可纯逻辑，需读 `ARGB.alphaBlend` 精确语义）；**P3（biome 数据）** 仍是接方块调色与天空盘的硬前置，建议单独一轮。## 2026-11-01 P4.2 FOG/SUNRISE 天气乘子
- `WeatherBlend.ApplyFog`/`ApplySunrise`：`ARGB.multiply` 分量乘 + 按 `(rain−thunder)/thunder` 权重 lerp；FOG 因子 rain(0.5,0.5,0.6)/thunder(0.25,0.25,0.3)，SUNRISE 同因子（alpha 保持 1）。
- 日出条带颜色接 `ApplySunrise`（`AttachCelestial`）。`WeatherBlendTests`(+2)。
- 全量 **1227 通过**、门禁 ok。**仍未做**：`SKY_LIGHT_COLOR`/`CLOUD_COLOR` 天气 alphaBlend（需读 `ARGB.alphaBlend` 全body 与 `vector4fFromARGB32`），`FOG_COLOR` 基色（维度/群系）；**P3（biome 数据）** 待单独一轮。## 2026-11-01 P4.3 SKY_LIGHT_COLOR 天气 alphaBlend
- `ColourBlend` 增 Vector4：`Greyscale/ScaleRgb/BlendToGray(Vector4,保留 alpha)/AlphaBlend`（复刻 `ARGB.alphaBlend`+`alphaBlendChannel`，含 sa=0/1 短路）。
- `WeatherBlend.ApplySkyLightColor(Vector3,rain,thunder)`：基色 alphaBlend 向夜间天光色 (122,122,255)/255、alpha 79/255(rain)/134/255(thunder)，按 `(rain−thunder)/thunder` 权重重叠；接进 `EffectiveLighting.SkyLightColor`。
- `WeatherBlendTests`(+2)。全量 **1229 通过**、门禁 ok。
- **仍未做**：`CLOUD_COLOR` 天气灰度（无云渲染消费，暂缓）；`FOG_COLOR` 真正基色（维度/群系）；`SKY_COLOR`/方块调色逐群系→**P3（biome 数据 + registry）** 为硬前置，建议单独一轮。## 2026-11-01 P3 数据格式调查（文档）
- 新增 `docs/analysis/common/biome-data.md`（草稿）：区块每 section「方块状态容器 + 生物群系容器（4×4×4=64、`BiomeMaxLocalBits=3`、全局调色板=群系 id、索引 `(y<<4)|(z<<2)|x`)」；`ChunkPacketParser` 现已读入并**丢弃**，P3 改为存 `int[64]`。
- 群系注册表（`Biome.NETWORK_CODEC`）：`has_precipitation`、`temperature`、`temperature_modifier`(默认 NONE)、`downfall`、可选 `attributes`（含 `visual/sky_color`）、`effects`{`water_color` 必填、`foliage_color`?、`dry_foliage_color`?、`grass_color`?、`grass_color_modifier`?}；颜色 `"RRGGBB"`。
- 颜色派生与采样：草/叶/干叶=override??colormap(temp,downfall)+modifier，水=water_color；`SKY_COLOR` 首版用 `calculateSkyColor(temp)`（原版主世界精确等价）；位置→section quart `(>>2&3)`，环境属性 7 抽头 gaussian 空间混合（核 `{0,1,4,6,4,1,0}`）。
- 模块文档 §11 指向分析文档。**下一步 P3 实施**：①`ChunkSection` 存 biome id[64]＋解析；②registry_data 取群系条目（或专用解码）＋`BiomeColors` 数据；③`ColorMapColorUtil`+温度→草/叶/水逐群系 tint（接 `ChunkMesher`）；④`SKY_COLOR`=calculateSkyColor+空间混合替换天空盘。工作量最大，分小步。## 2026-11-01 P3.1 区块 biome id 存储 + 解析捕获
- `ChunkSection`：新增 `int[64] Biomes`（`BiomeVolume=64`）、`GetBiome(x,y,z)`、`HasBiomes`、纯函数 `BiomeIndex = ((y>>2&3)<<4)|((z>>2&3)<<2)|(x>>2&3)`；ctor 增可选 `int[]? biomes`。
- `ChunkPacketParser`：生物群系容器由“读后丢弃”改为 `biomes.Get(b)` 存入每 section `int[64]`（字节消费不变，`ChunkPacketTests` 仍绿）。
- 测试 `ChunkSectionBiomeTests`(3)。全量 **1232 通过**、门禁 ok。
- **下一步 P3.2**：biome registry 条目解码（`has_precipitation`/`temperature`/`temperature_modifier`/`downfall`/`effects` 与 `"RRGGBB"` 颜色）＋ `BiomeRegistry`（Common）＋合成字节单测；再 P3.3 逐群系 tint 接 `ChunkMesher`、P3.4 天空盘。## 2026-11-01 P3.2 biome registry 解码 + BiomeRegistry
- `NbtCompound` 增 `GetFloat`/`GetBool`（`NbtFloat`/`NbtByte`）。
- `Common/World/BiomeRegistry`：`From(DynamicRegistries)` 取 `minecraft:worldgen/biome` 条目→`BiomeEntry(Id, Climate{HasPrecipitation,Temperature,Downfall}, Effects{WaterColor,FoliageColor?,DryFoliageColor?,GrassColor?,GrassColorModifier})`；`Parse` 解 NBT；`ParseColour` 解 `"RRGGBB"`（`#` 可选）→0xFFRRGGBB；`ById` 位置查找；颜色缺省 water=0x3F76E4。
- 复用现有 `RegistryDataPackets`/`DynamicRegistries`（无需新包解析）。测试 `BiomeRegistryTests`(4)。全量 **1236 通过**、门禁 ok。
- **下一步 P3.3**：`BiomeColorSource`：位置→`ChunkSection.GetBiome`→`BiomeRegistry`→草/叶/水 tint（`ColorMapColorUtil`+温度/湿度、`grassColorModifier`、`biomeBlendRadius`）接入 `ChunkMesher`；单测 + 离屏出图。## 2026-11-01 P3.3 逐群系方块调色接入
- `ClientWorld.GetBiomeAt(x,y,z)`：chunk→section(`y>>4`)→`ChunkSection.GetBiome(x&15,y&15,z&15)`，未加载返回 -1。
- `BiomeTint` 增 `WithBiomes(biomeIdAt, registry, blendRadius)` 与 `ColourAt(kind,x,y,z)`：位置→群系→`BiomeColour`（草=override??colormap+modifier、叶=override??colormap、水=effects.water_color、evergreen/birch 固定），经 `BlockTint.Calculate` 按 blendRadius 平均；`FromColormaps` 测试工厂。`DARK_FOREST` 精确、`SWAMP` 缺 `BIOME_INFO_NOISE` 返回基色（受控偏差）。
- `ChunkMesher`：方块面 tint 用 `origin` 世界坐标、流体用水格 `bx,by,bz` 调 `ColourAt`。`FrameCommand.RenderWorld` 增 `BiomeRegistry?`，真服路径传 `BiomeRegistry.From(probe.Registries)`（回放/本地无 registry→默认 tint）。
- 测试 `BiomeTintTests`(+3：按群系草/水、DARK_FOREST 公式、blendRadius 平均)。全量 **1239 通过**、门禁 ok。
- **注**：无真实服务器 registry 环境，逐群系**离屏像素**未单独验证；已用纯逻辑单测覆盖（等价性以公式/平均为准）。
- **下一步 P3.4**：`SKY_COLOR`=`calculateSkyColor(temperature)` 经 7 抽头 gaussian 空间混合，绘制天空盘（`CelestialGeometry.SkyDisc`）替换/叠加全屏渐变；离屏出图+像素验证。## 2026-11-01 P3.4 SKY_COLOR 计算 + 天空盘绘制验证
- `SkyAttributeTracks.SkyColorMultiplierAt(dayTime[,partial])`：Timelines `SKY_COLOR` MULTIPLY 乘子（133/11867 白=1 → 13670/22330 黑=0）。
- `SkyColourSource.ColorAt(biomeIdAt, registry, x,y,z)`：以 `calculateSkyColor(temperature)` 逐群系、按 quart 采样 + 7 抽头 gaussian（核 `{0,1,4,6,4,1,0}`，总权重 4096）空间混合（未加载群系不计权重）。
- 诊断 `MC_SKY_DISC_TEST`：用 `CelestialGeometry.SkyDisc` 绘制盘、tint=calculateSkyColor(temp)，中心像素 `rgba(123,164,255)`=`0xFF7BA4FF`(temp0.5)、覆盖 518400 像素 ⇒ 盘几何+颜色管线正确。
- 测试 `SkyColourSourceTests`(3)、`SkyTimelinesTests`(+1)。全量 **1243 通过**、门禁 ok。
- **替换天空盘（opt-in，T-281）**：`MC_SKY_DISC=1` 时 `FrameRequest.SkipGradientSky` 跳过 `sky.wgsl`，`AppendSkyDisc` 画顶盘(SKY_COLOR)+地平线下底盘(黑，`SkyVisibility`)；真服路径用 `SkyColourSource`、本地用 `MC_BIOME_TEMP` 合成。验证：本地 sky 像素渐变 `rgba(121,167,255)` → 换盘后渐变跳过、写盘 colour (0.482,0.643,1.000)。**默认仍渐变**（缺参考帧，目视调优留待，受控偏差）。P3 全链（P3.1–P3.4）已交付。## 2026-11-01 真服实机验证 + 窗口逐群系 tint
- **真实服务器连通**（127.0.0.1:25565，登录 Blue_Coast）：离屏 `frame` 收 49 区块、1.6M 方块；`probe.Registries` 群系表 **67** 条，采样群系 id 53 解析成功、SKY_COLOR 计算 `(0.490,0.635,1.000)`。
- **`WindowCommand` 接逐群系 tint**：从 `live.Registries` 建 `BiomeRegistry`，`BiomeTint.WithBiomes` 重建 `LayerSinks` 并 `meshCache.Clear()+QueueMissing` 重网格化；日志 `biomes: 67 ... per-biome block tint enabled`。
- **窗口实机**：`window 127.0.0.1:25565` 成功 presented（960x540，frame #1800+ 持续渲染，无 validation error）。窗口像素不可截（swapchain），目视由所有者确认。
- **诊断修复**：离屏相机常在世界外，天空盘采样回退到已加载区块列，避免取群系得 -1→全 0。
- 备注：离屏真服帧"发白"是距离雾（相机远、fog 64–160），非 P3 bug；`MC_FOG_START/END` 推远后正常显示绿色植被（tint 生效）。全量 **1243 通过**。## 2026-11-01 修复：紫色草方块（群系色越界）
- **现象**：实机草地顶部呈品红/紫。对照原版 `getGrassColorFromTexture` 发现：查 colormap 前需把 `temperature`/`downfall` **clamp 0..1**；我们直接传原始群系温度（常 <0 或 >1），索引越界返回 colormap 默认色 `-65281`（品红）。
- **修复**：`BiomeTint.BiomeColour` 对 temperature/downfall clamp 0..1 再查表；`BiomeTintTests.TemperatureIsClampedBeforeColourMapLookup`（temp=2→index0，断言非 -65281）。
- 重启实机 `window`（pid 52704）复验。全量 1244 通过、门禁 ok。
- **tint 等效性复核（`MC_BIOME_PROBE`）**：真服群系温度/降水解析正确，草色与原版名色一致——`beach` 0.8/0.4→**0x91BD59**（原版平原/沙滩草地 #91BD59）、`forest` 0.7/0.8→0x82BE59、`river` 0.5/0.5→0x8EB971。紫色已消除；同群系若仍偏淡属**光照/贴图/描边**差异而非 biome tint。## 2026-11-01 修复：地形光照色彩空间（litColour 直接相乘）
- **代码证据**：原版 `block.vsh`（`vertexColor = Color * sample_lightmap`）、`block.fsh`（`color = texture * vertexColor * ColorModulator`）**全程无 pow/gamma**；`lightmap.fsh` 亮度 `get_brightness(l)=l/(4-3l)` 我们已 1:1 复刻。→ 正确为 sRGB 域直接相乘。
- **修复**：`terrain.wgsl`/`oit_reference.wgsl` 的 `litColour` 改为 `clamp(texel*light)`（`light=vertexColor*lightmapColour`）。此前 `pow(texel^2.2*light)^(1/2.2)` 把 tint 当线性，导致染色方块（草/叶/水）过亮偏淡。
- 规格 `docs/analysis/render/lighting-color-space.md`；全量 **1244 通过**、门禁 ok。真服离屏帧正常（顶面受光/侧面变暗）。重启 `window` 实机复验。## 2026-11-01 修复 #8 块光闪烁 + #13 世界时钟（每帧光照）
- `Client/Render/BlockLightFlicker`：复刻原版 `flicker += (r1−r2)·r3·r4·0.1; flicker *= 0.9`，`blockFactor=1.4+flicker`；随机源可注入以便单测。
- `InteractiveSession`：每 tick `_world.AdvanceClocks()`（修 #13：此前从未推进→时间/亮度冻结）+ `_blockLightFlicker.Tick()`；新增 `CurrentLightmap()` = `EffectiveLighting(world)` 覆盖 `BlockFactor=flicker`。
- `WindowedRenderer.Run` 增 `lightmapPerFrame` 回调，每帧重写光照 uniform（此前窗口固定 `LightmapUniform.Day`，昼夜/闪烁都不会变）。
- `BlockLightFlickerTests`(3)。全量 **1247 通过**、门禁 ok。重启窗口实机复验闪烁与昼夜。
- 仍待：**#10 BlockLightTint**（仍用 `Srgb24ToLinear`，应改原样 `v/255`）；**#3** 平滑光照算法近似（待定是否移植 smoothBlend/smoothWeightedBlend）。## 2026-11-01 修复：世界时钟被空 set_time 清空 / gameTime 兜底
- **诊断（`MC_DIAG`）**：真服 `set_time` 约每 20 tick 一次但 **clockUpdates 恒空**（只有 gameTime）；旧 `ApplyTime` 每次 `_clocks.Clear()` → 时钟被清、`DayTime` 恒 0（暗）；`/time set day` 先亮、下一空包即复原。
- **修复**（对齐原版 `ClientClockManager`）：`ApplyTime` 先 `AdvanceClocks(gameTime)` 再**仅覆盖包内时钟、不清空**；`AdvanceClocks(long)` 用 `Δ·rate`（carry partial→total）；`AdvanceTick()` 每 tick `GameTime++` 并推进时钟；无 overworld 时钟时 `DayTime=GameTime%24000` 兜底。
- `ClientWorldTimeTests`(7)。全量 **1250 通过**、门禁 ok。实机 `day` 从 ~4890 起每秒 +20 推进（白天亮度正常）。重启窗口复验昼夜与 `/time set`。## 2026-11-01 C：抓包核实时钟来源（结论：该服不下发时钟）
- 抓包（`MC_PACKET_LOG`）：**配置阶段**只有 custom_payload/ping/update_enabled_features/select_known_packs/registry_data×N/update_tags/finish_configuration；**游戏阶段**唯一时间包是 `set_time`(id115) 且 **clockUpdates 恒空**（原始字节 `73 + 8B gameTime + 00`）。**全流程无任何携带世界时钟的包**。
- 原版核实：昼夜由时钟驱动的 Timeline 属性决定（`EnvironmentAttributeSystem.addDynamicLayers`），只吃 `ClockManager` 时钟；**无 gameTime 兜底**。故该服上原版时间应为时钟默认 0。
- **修正**：移除先前的 `GameTime%24000` 兜底，`DayTime` 严格只取 overworld 时钟（缺失=0）；`ApplyTime` 仍“tick 后只覆盖包内时钟、不清空”（`/time set` 会带时钟→生效且持久）。`ClientWorldTimeTests` 改为“无时钟=0”。全量 1250 通过。
- 仍待：**#10 BlockLightTint**（`Srgb24ToLinear` 应改 `v/255`）；**#3** 平滑光照近似。另：`Diagnostics/` 的 `MC_PACKET_LOG`/hex 打印为宿主诊断，可整体删除不影响游戏。## 2026-11-01 修复：预热阶段丢弃进服全量时钟/天气包（T-287）
- 服务端 = Fabric（就是 127.0.0.1:25565）；原版/Fabric 服务端进服会用 `createFullSyncPacket` 下发**全量时钟**（`PlayerList.java:659`），例行 `set_time` 空 map（`MinecraftServer:1145`）。
- 我们的窗口流程先 `PlaySession.RunAsync` 预热收区块，其 `default:` **丢弃**了进服全量 `set_time`（及 `GameEvent` 天气），之后才 `Adopt` 建 live 会话 → 时钟/天气初值丢失。
- **修复**：`PlaySession` 把 `SetTime`/`GameEvent` 暂存进 `PlaySessionStats.DeferredPackets`；`LivePlaySession.Adopt` 注入队列；离屏 `frame` 直接应用。
- 实机首包 `set_time len=32 clocks=[1:t=0,r=0, 0:t=109000,r=0] day=13000` → 该服**时钟 rate=0、时间冻结在 13000(夜)**；`/time set day` 会带单时钟更新→生效并保持（不再复原）。全量 **1250 通过**、门禁 ok。## 2026-11-01 修复 #10 BlockLightTint（原样通道）
- 原版 `BLOCK_LIGHT_TINT = ARGB.vector3fFromRGB24(-10100)` = `from8BitChannel(v)=v/255`（无 gamma）= `(255,216,140)/255`；我们误用 `Srgb24ToLinear` → 块光色偏暗/偏色。
- 改 `VanillaLightmap.Rgb24ToVector`（原样 `v/255`），`DayOverworld.BlockLightTint` 与 `FrameRequest.Day.BlockLightTint` 同步；删除 `Srgb24ToLinear`。
- `VanillaLightmapTests.BlockLightTintIsRawSrgbChannels`。全量 **1251 通过**、门禁 ok。
- 光照等效项：#8 已实现、#10 已修、#13 已修；**仅剩 #3**（平滑光照算法近似，缺 `smoothBlend/smoothWeightedBlend` 的 3邻+center 特判与 AO 双线性权重）。## 2026-11-01 修复 #3 完整方块面平滑光照（smoothBlend）
- 实读 `BlockModelLighter.prepareQuadAmbientOcclusion`：完整方块面走 **else 分支**，每顶点=`LightCoordsUtil.smoothBlend(3邻+center)`（无 `smoothWeightedBlend`，那只在 partial 非立方模型）。
- 新增 `Client/Render/SmoothLighting.Blend`：`sky>2||block>2` 时 `neighbor==0→center`、`sky==0→继承 center sky`，再 `(sum)>>2`；`ChunkMesher.CornerShade` 用其替换简单 4 格整数平均（center=index0）。
- `SmoothLightingTests`(4)。全量 **1255 通过**、门禁 ok。光照对照表全部 ✅（#3/#8/#10/#13 + litColour/时钟/预热丢包）。## 2026-11-01 修复：雾色随时间/天气（夜间不再套白昼蓝）
- 症状：夜里冰面/水面/普通方块偏蓝——雾色此前是**静态白昼蓝**(0.75,0.85,1.0)。
- 原版 `visual/fog_color` 为 multiply 曲线：133/11867 `#ffffff` → 13670/22330 `#0f0f16`。加 `SkyAttributeTracks.FogColorFactorAt(dayTime[,partial])`。
- `FrameRequest.FogColour`（默认白昼雾）；`OffscreenRenderer`/`WindowedRenderer` 使用；窗口新增 `fogPerFrame` 每帧重写雾 uniform；`FrameCommand.EffectiveFogColour` 与 `InteractiveSession.CurrentFogColour` = base×(晨昏因子)×weather（`WeatherBlend.ApplyFog`）。
- `SkyTimelinesTests.FogColorFactorIsWhiteByDayDarkAtNight`。全量 **1256 通过**、门禁 ok。
- **仍待**：**天空渐变仍静态**（`sky.wgsl` 硬编码 horizon/zenith，无 uniform）→ 夜天偏蓝的另一半；需给 sky pipeline 加 tint uniform 并按 `SKY_COLOR` 时间曲线×weather 每帧更新。## 2026-11-01 修复：夜间方块发蓝（SKY_LIGHT_COLOR gamma bug）
- 诊断（`MC_DIAG` 打实际 Lightmap uniform）：夜 `skyCol=(0.195,0.195,1.000)`，而原版 `#7a7aff` 应为 `(0.478,0.478,1.0)`。根因：`SkyTimelines.NightSky = SrgbToLinear(122)` 误做 gamma 解码（0.195）；`SUNRISE_SUNSET_COLOR` 的 R/G/B 同样误线性化。
- 修复：改回原样 `v/255`（对齐 `ARGB.vector3fFromARGB32`），删除 `SrgbToLinear`。实机确认 `skyCol=(0.478,0.478,1.000)`。
- `SkyTimelinesTests` 更新为 `122/255`。全量 **1256 通过**、门禁 ok。
- **仍待**：天空渐变静态（`sky.wgsl` 无 uniform）；且雾基色当前用 `base×(fog因子)` 近似，原版是 `FOG_COLOR(默认黑) 与 SKY_COLOR 混合`，后续按原版统一到 SKY_COLOR。## 2026-11-01 天空/雾改由 SKY_COLOR 驱动（T-292）
- 原版：天空= `SKY_COLOR`（群系 `calculateSkyColor` × 昼夜乘子 × 天气）；雾基色=`FOG_COLOR`(默认黑) 与 `SKY_COLOR` 混合（`AtmosphericFogEnvironment.getBaseColor`）。
- 实现 `FrameCommand.EffectiveSkyColour`（`SkyColourSource` 空间混合 × `SkyColorMultiplierAt` × `WeatherBlend.ApplySkyColour`）。
- 窗口：`SkipGradientSky=true` + `clearColourPerFrame`=天空色；`fogPerFrame`=`CurrentFogColour`（有 biomes 时用天空色）；`InteractiveSession.SetBiomes/CurrentSkyColour`。离屏 `RenderWorld` 同（设置 ClearColour/FogColour/SkipGradientSky）。
- 窗口 diag：`skyCol=(0.478,0.478,1.000)`、`day` 正常。全量 **1256 通过**、门禁 ok。
- **注意**：离屏单帧 `frame` 预热的 deferred 偶尔未包含进服 full-sync（该次 day=0）；窗口路径正常。后续可把 full-sync 在更早阶段接入以修正离屏。## 2026-11-01 可配置视距（默认 12，T-293）
- `InteractiveSession.ViewDistanceChunks` 改为宿主配置：`MC_VIEW_DISTANCE`（clamp 2..32，默认 **12**）；配置阶段 `ConfigurationProbe.SendClientInformation`、play `ClientInformation`、`WalkCommand` 均用它（此前硬编码 8/2）。
- 全量 **1256 通过**、门禁 ok。窗口以默认 12 重启。
- **已知视觉差距（后续可选）**：①**云层未实现**（原版 CloudRenderer）；②天空为纯 `SKY_COLOR` 平色，缺地平线渐变；③雾色简化为 `SKY_COLOR`（原版 `FOG_COLOR↔SKY_COLOR` 混合）；④夜间亮度可能偏低。## 2026-11-01 云层模块设计（待评审）
- 实读 `CloudRenderer`/`clouds.{vsh,fsh}`/`EnvironmentAttributes`/`day.json`，写 `docs/modules/clouds.md`（草稿）：cell=12、云高 192.33、`CLOUD_FOG_END=2048`、`visual/cloud_color` 昼白→夜 `#191926`、滚动 0.6 块/s、面亮度 1/0.7/0.8/0.9。
- 拟用"云纹理水平贴图平面近似"（受控偏差：无逐 cell 侧面剔除/阴影），§3 单测（颜色因子/滚动/常量）、§4 离屏对比。
- **按 AGENTS 3.5 需 `[已评审]` 后再编码**；当前等待所有者评审 `docs/modules/clouds.md §5`。## 2026-11-01 云方案定稿：逐 cell（已评审）
- 所有者决定：**逐 cell 还原原版**（云开销可接受）；无 clouds.png 不画；绘制半径跟 `MC_VIEW_DISTANCE`。`docs/modules/clouds.md` → `[已评审]`。
- **关键发现/前置**：`WindowedRenderer` **没有 celestial/云 pass**（太阳/月亮/星星/云只在离屏绘制）→ 窗口夜黑、无云的另一根因。需先给窗口补 `celestial+cloud pass`。
- 实现顺序：① 纯逻辑 `CloudTexture`/`CloudMeshBuilder`/`CloudColorFactorAt`（+单测）→② 窗口 celestial+cloud pass →③ 离屏复用 →④ 对照帧。
- **① 已完成（T-295）**：`CloudTexture`(alpha<10 空/四邻/pack)、`CloudMeshBuilder`(原版 buildMesh/buildExtrudedCell 面规则)、`CloudColorFactorAt`；`CloudTextureTests`(2)+`CloudMeshBuilderTests`(4)+云色单测。全量 **1263 通过**。
- **下一步 ②（T-296）**：给 `WindowedRenderer` 补 celestial+cloud pass（现仅离屏有），再接入云四边形；每帧 `CloudColor/Offset/CellSize`、顶/底/侧向面、alpha 混合与 CloudFogEnd 淡出。## 2026-11-01 T-296 步骤①：窗口 celestial pass（已完成）
- `OffscreenRenderer.CelestialResources/CelestialColorResources` 提为共享 `internal` 并参数化颜色目标格式（窗口 `Bgra8Unorm` vs 离屏 `Rgba8Unorm`）；`CreatePositionColorPipeline` 加 format 参数。
- `WindowedRenderer` 复用两者，天空之后/地形之前绘制；`WindowCommand` 调 `FrameCommand.AttachCelestial` 填充 draws/textures。窗口启动无 GPU validation error，日月星显现。
- 全量 **1263 通过**、门禁 ok。**下一步 ②（T-297）**：逐 cell 云接入（CloudMeshBuilder→position-colour，每帧 CloudColor/Offset/CellSize，alpha + CloudFogEnd），并处理天体随相机（当前 model 用初始相机，行走时需按位移增量更新）。## 2026-11-01 T-297 进展：逐 cell 云离屏渲染（窗口待）
- `CloudGeometryBuilder`（面顶点表/亮度 1/0.7/0.8/0.9/厚度 4/雾 alpha）→ position-colour；`FrameCommand.BuildCloudDraw`（云滚动 `gameTime% (w*400)*0.03`、相机侧、`radiusCells=ceil(view*16/12)`、`CloudColorFactor×天气`）；离屏 `RenderWorld` 追加到 `CelestialColorDraws`。
- 修复：离屏相机在世界外时天空采样群系失败→全黑；改为**回退到已加载区块列**采样 `EffectiveSkyColour`（与天空盘一致）。
- `CloudGeometryBuilderTests`(2)；离屏日间出图见蓝天空+太阳+云块。全量 **1265 通过**、门禁 ok。
- **待做**：窗口每帧云接入（需每帧更新云几何/偏移与天体随相机位移）；离屏相机远时云在相机附近可能不在取景内，窗口验证更直观。## 2026-11-01 T-297 完成：窗口每帧云
- `CelestialColorResources.SetDraws`（延迟一帧释放旧缓冲）支持每帧更新云 draws；`WindowedRenderer` 新增 `cloudsPerFrame`，天空/日月星之后、地形之前以 position-colour 绘制云。
- `InteractiveSession` 持 `_cloudTexture`（`SetCloudTexture`）并提供 `CurrentCloudDraw()`= `FrameCommand.BuildCloudDraw(world, texture, eye)`；`WindowCommand` 传入 `cloudsPerFrame`。
- 窗口运行无 GPU validation error（frame ~40–90ms）。全量 **1265 通过**、门禁 ok。
- **已知偏差**：日月星仍用**初始相机** model（行走时会偏移）；云按当前眼位生成所以正确。后续可给 CelestialResources 加相机位移增量更新。T-296 目标基本达成（窗口 celestial+cloud pass 已有）；如需严格，再补天体随相机。## 2026-11-01 T-296 收尾：天体随相机
- `CelestialResources`/`CelestialColorResources` 增 `UpdateCamera(Vector3 delta)`：每帧按 `eye - request.CameraPosition` 左乘平移写回天体 transform，日月星不再随移动偏移（云本就用当前眼位）。
- `WindowedRenderer` 每帧在采样相机后调用（仅天体，不对云资源调用）。窗口运行无 GPU validation error。全量 **1265 通过**、门禁 ok。
- **T-296/T-297 完成**：窗口已具备 celestial（日月星/日出）+ 逐 cell 云 pass；离屏/窗口共用绘制资源。云纹理缺失时不画云。## 2026-11-01 修复：窗口每帧云 GPU 缓冲 OOM（T-298）
- 症状：窗口运行一段时间后 `wgpuDeviceCreateBuffer: Not enough memory` 崩溃（客户端起不来/退出）。
- 原因：`CelestialColorResources.SetDraws` 每帧重建云缓冲；60fps 下分配爆炸。
- 修复：容量内**原地 `WriteBuffer` 复用**，仅容量/结构变化时重建（旧缓冲延迟一帧释放）。窗口连续 2469 帧稳定无 validation/OOM；全量 1265 通过。
- 当前客户端运行 pid（子进程）~57780。剩余视觉差距：天空无地平线渐变、雾基色近似、云无 OIT/精确 cloud-fog。## 2026-11-01 修复：几何 arena 显存 OOM（T-299）
- 崩溃：`wgpuDeviceCreateBuffer: Not enough memory`（960×540、视距12、约1900帧）。
- 根因：`ChunkGeometryArena` 初始 `expectedChunks(625)×512KB×2×3层 ≈ 1.4GB`；且 `RebuildAll` 以 `MaxPresizeBytes` 当硬上限，实际几何超限时每帧全量重建（~100MB/帧）→ OOM。
- 修复：初始改 1MB 起；每层增长上限 `MaxBufferBytes=96MB`（presize 16MB）；达上限跳过远处区块（不再重建）；`RebuildAll` 增长上限用 MaxBuffer。视距 12 连续 45s+ 无 OOM/validation（进程 ~819MB）。全量 1265 通过。
- **另一主因（频繁关闭）**：`Start-Process` 启动的客户端会在工具调用结束时被清理；改用**持久终端会话**（`terminal_open`/`terminal_send`）后持续运行 **11647 帧**稳定（pid 48892，RSS ~866MB）。
- 仍待：超大窗口(2560×1334)时 OIT 目标与重传仍可能吃紧；可视需要再降或分页缓冲区。## 2026-11-01 新目标：视距 12–32 无跳过（T-300 / ADR-0020）
- 所有者要求：视距 **12 且不跳过远处区块**是硬性，原版可到 **32**。单缓冲 arena + 256MB 上限于此不可能，必须换几何存储。
- 已立目标 `goal-6e779d0d`；写 ADR-0020：**每区块独立 vertex/index 缓冲 + CPU 视锥剔除**（原版模型），显存随实际网格增长，取消上限/跳过。
- 实施分步：①ADR（完成）→②`ChunkGeometryStore`（每区块缓冲、Apply/Remove、内存统计）→③`WindowedRenderer` 三层绘制改每区块缓冲+CPU 剔除（translucent 距离排序）→④视距 12/24/32 长跑与显存测量→⑤测试/门禁/traceability 收尾。
- 现状：旧每帧云/天体 pass 不回退；当前客户端仍可用（旧 arena 方案，视距12 稳定但高内存时会跳过远区块）。## 2026-11-01 T-301：每区块几何存储 + 窗口绘制（完成）
- 新增 `ChunkGeometryStore`：每区块独立 vertex/index 缓冲，`Apply` 同尺寸原地重写、尺寸变化重建、`Remove` 释放；`MemoryBytes/UploadedBytes` 统计；`Draw(planes)` CPU 视锥剔除、`DrawFarthestFirst` 排序。
- `WindowedRenderer` 三层（solid/cutout/translucent）改用 store；移除 `ChunkCuller`/间接绘制/`SlotHighWater`/达上限丢弃逻辑；保留 disableCulling 调试开关。
- 视距 12 运行 **6996+ 帧**无 OOM/validation（RSS ~812MB，GPU dedicated ~1.6GB）；全量 1265 通过。待压测 24/32；区块卸载（ClientWorld 未移除离视区块）与旧 arena 清理列为后续。## 2026-11-01 T-302：视距 32 压测通过（每区块存储）
- 视距 **32**：窗口统计 **3890 区块**、**上传 3.26GB**、**0 次 full rebuild**、**8302 帧**无 OOM/validation（GPU dedicated 峰值 ~4.3GB，960×540）。
- 证明"每区块缓冲 + CPU 剔除"支持大视距且**不跳过远处区块**；旧单 arena 上限问题解除。
- 旧 `ChunkGeometryArena`/`ArenaSizing` 暂留（仍被旧单测覆盖），后续可删；`ClientWorld` 目前不会移除离视区块，长时间大范围移动会累积内存（后续做区块卸载 + 缓冲释放）。
- **剩余渲染缺口已登记** `docs/specs/render/known-gaps.md`：区块卸载、旧 arena/绘制调用、天空盘/渐变、雾精确混合、夜亮度、云 OIT/精确 cloud-fog、天体细节像素等效、高分辨率显存、适配器选择；traceability T-303。## 2026-11-01 T-304：天体/云排查（月亮 UV 修复）
- 实读原版 `SkyRenderer`：太阳尺寸30/高度100、月亮20/100、星空1500、日出18顶点扇形；日出 alpha 经 PoseStack 后乘 scale 作用在旋转前局部 Z（我们的 `SunriseFanWorld` 公式正确）。
- **已修正**：原版月亮四边形相对太阳使用反向 UV（`buildMoonPhases` → 180°），我们此前正向 → 月相方向错误；`AddBody(..., flip: true)` 传 `(1,1,0,0)`，加 `CelestialGeometryTests`。
- 云 shader 实读：`vertexColor = faceColors[dir] * CloudColor`，alpha *= `1-linear_fog_value(vertexDistance,0,FogCloudsEnd)`；面亮度/雾与我们实现一致；**缺 OIT 云 pass**（已知缺口）。
- 下一步：需要用户给出太阳/月亮/星星/云的具体可见问题（位置/大小/颜色/闪烁/排序），以定位剩余偏差；或继续按已知缺口做云 OIT/天空盘。## 2026-11-01 T-305：窗口天体每帧重建 + 云位置修正
- 根因1：窗口的 celestial 只在启动时 `AttachCelestial` 一次；`/time` 变化后太阳/月亮/星星几何不更新 → 太阳不动、夜里无星星。现 `WindowedRenderer` 增 `celestialPerFrame`，宿主每帧重建 day/night 天体（PNG 解码静态缓存、纹理数组固定顺序，`CelestialResources.SetDraws` 复用纹理/管线）。
- 根因2：云 `BuildCloudDraw` 的 origin 重复加了相机坐标；原版 shader `pos = cell*CellSize + face*CellSize + CloudOffset` + 相机 uniform，origin 应为 `(cellX*12-xInCell, bottomY, cellZ*12-zInCell)`。已修。
- 验证：离屏 `MC_SUN_ANGLE=0` 太阳在视野、90/180 消失且星星可见（白点）；全量 1266 通过。剩余需窗口实测与云 OIT/排序。## 2026-11-01 T-306：星星随相机 / 云相机相对坐标
- 现象：玩家走路会靠近星星；云跨区块位置突然跳变/渲染异常。
- 根因1：每帧 `celestialPerFrame` 的 `SetDraws` 在 `UpdateCamera` 之后执行，新 draw 的 model 仍是初始相机 → 星星固定在世界原点。已改为先 `SetDraws` 再 `UpdateCamera(delta)`。
- 根因2：原版云是**相机相对**（vsh 的 cell 是 relative cell，pos=relative*12+CloudOffset，相机在原点）；我们 origin 之前含 centerCell 绝对坐标 → 跨 cell 跳变。已改为 `origin=(eye.X-xInCell, bottomY, eye.Z-zInCell)`；`CloudGeometryBuilder` 继续用相对 CellX。
- 待窗口实测：跨区块行走云连续性、星星不随移动靠近；全量测试后提交。## 2026-11-01 T-307：高空天空/雾黑屏修复
- 现象：玩家 Y 高于世界高度上限时地面/天空变黑（截图二黑色大块为雾/天空把地形染色）。
- 根因：`CurrentSkyColour` 用相机 Y 采样群系；Y 超出已加载 section 时 `GetBiomeAt` 全 -1 → `SkyColourSource` 权重 0 → sky=(0,0,0)，而 `CurrentFogColour` 直接取 sky → 雾/地面黑。
- 修复：按原版 `ChunkAccess.getNoiseBiome` 将采样 quartY **clamp 到世界 [minY,maxY]**；极端缺失再回退 y=64。离屏高空相机帧全蓝、无黑色；全量 1267 通过。
- 待窗口实测高空；云在夜间偏暗属正常色（若过黑另查）。## 2026-11-01 T-308：云范围/顺序/深度（进行中）
- 原版依据：`CloudRenderer.prepare` 的 `range` 来自 `Options.cloudRange`，默认 **128 区块**（2048 方块）；我们误用 `MC_VIEW_DISTANCE=12`→192 方块，而云 fog 到 2048 才消失 → 云在 192 处硬切“冒出来”。
- 原版顺序：`executeClassicTransparency` 中 **半透明地形之后**才 `cloudRenderer.render`；fancy 走 `renderOit` + 深度。我们此前云在地形之前、深度 Always不写 → 地形/云互相穿透（“透视”）。
- 实现：`CloudDrawCache`（仅中心 cell/相机侧/垂直偏移变化时重建相机相对几何，每帧只更新 model 平移）；`BuildCloudDraw` 半径=`ceil(128*16/12)`；`WindowedRenderer` 云 pass 移到地形/OIT 之后，创建 cloud pipeline 深度 `LessEqual`；`CelestialColorResources` 未变数组跳过重传。
- 构建/1267 通过；窗口运行中（pid 54384，RSS ~1.16GB）。**未做**：云 OIT（当前普通 alpha + 深度）；严格像素等效待后续。## 2026-11-01 T-309：云配置开关
- 新增宿主开关（不污染游戏程序集环境变量读取，经显式选项传入）：`MC_CLOUD_MODE=off|fast|fancy`（默认 fancy）与 `MC_CLOUD_RANGE`（默认 128 区块，对应原版 `Options.cloudRange`）；`MC_CLOUD_MODE=off` 跳过云，`fast` 用仅 DOWN 面（`CloudMeshBuilder.BuildFlat`）。
- `CloudDrawCache` 缓存 fancy 云几何；`CloudMeshBuilderTests.FastModeBuildsOnlyTheDownFace`、`CloudDrawCacheTests` 加回归；全量 **1269** 通过。
- **完整 cloud OIT 仍待实现**：需要新增 position-color 版 OIT 着色器/三阶段 pipeline 并接入现有 OIT 目标；计划以显式 `CloudUseOit` 选项接入，不影响当前路径。## 2026-11-01 T-310：跳跃云上下抖动修复
- 现象：跳跃时云上下颤抖。
- 根因：`CloudDrawCache` 为实现雾 alpha 把 `relativeBottomY` 四舍五入进几何；跳跃时该值每帧变化 → 几何重建且云块垂直位置被量化到整块，产生上下跳。
- 修复：缓存几何不再含垂直偏移（fog 距离按水平相对位置），缓存键不含垂直；每帧 model 平移 y 固定为绝对 `bottomY`（水平仍为 eye-(xInCell,zInCell)），与 shader `CloudOffset.y` 等价。
- 全量测试通过；Cloud 测试 11。## 2026-11-01 T-311：cloud OIT（参考三阶段复用）
- 原版：`CloudRenderer.renderOit` 按 OitStage 复用 `OitRenderPassProvider` 的 depth bounds/transmittance/accumulate 目标，fancy 使用 OIT 管线。
- 实现：`CloudOitResources` 将云缓存几何展开为地形 14-float 顶点（position+colour+uv+spriteRect+light），用 **256 级 alpha 渐变纹理**让现有 OIT shader 的 `sampleAlpha` 得到逐顶点云 alpha；云几何在现有 OIT 的 bounds/transmittance/accumulate 三个 pass 中额外绘制（复用现有 pipeline 与 group1 绑定），composite 一并解析。
- 开关：`MC_CLOUD_OIT=0` 回退普通 alpha+深度；默认开启。窗口运行 3586+ 帧无 GPU 校验错误；构建通过。
- 待：像素级对照、性能（128 区块 + OIT），以及与原版独立 cloud depth OIT 的差异记录。## 2026-11-01 渲染优化计划（T-312）
- 计划文档 `docs/specs/render/optimization-plan.md`：阶段1 cloud OIT 常驻几何+专用 shader；阶段2 云几何/自适应范围；阶段3 OIT 目标生命周期；阶段4 独立 cloud depth + 像素对照；阶段5 天体/天空/绘制路径。
- 下一步从阶段1开始：新增 `cloud_oit.wgsl`（7-float 顶点 + transform uniform），缓存云几何仅在 cell/侧别变化时重建，每帧仅更新 uniform。- 2026-11-01 阶段1完成：`cloud_oit.wgsl`（7-float+transform）+ `CloudOitResources` 仅在缓存数组变化时上传，每帧只更新 transform；窗口 2194+ 帧无校验错误。
- 2026-11-01 阶段2完成：`CloudRangePolicy` + `MC_CLOUD_ADAPTIVE`（默认关闭）按帧时间在 32–128 调云范围。
- 2026-11-01 阶段3完成：OIT 目标按需创建（有半透明/云 OIT 才分配），无内容场景不占目标显存。
- 2026-11-01 阶段5部分：天体几何按 dayTime/雨雷缓存，减少每帧重建（相机仍每帧 UpdateCamera）。
- 2026-11-01 阶段5部分：`MC_SKY_GRADIENT=1` 开关（默认平色 SKY_COLOR）。阶段4独立 cloud depth 仍需单独 OIT passes。
- 2026-11-01 阶段4设计完成：独立 cloud depth OIT（cloudDepthTexture/三阶段/独立 composite，`MC_CLOUD_DEPTH`）；实现待下一提交。
- 2026-11-01 阶段3余项完成：`MC_OIT_LOW_PRECISION=1` OIT 目标降为 RGBA8Unorm（默认 16F）。
- 2026-11-01 阶段4对照流程已登记 `benchmarks/render/cloud-oit-compare.md`；独立 cloud depth 实现仍待下一提交。
- 2026-11-01 阶段4实现：`MC_CLOUD_DEPTH=1` 独立 cloud depth + 专用 OIT targets/三阶段/composite；默认 0。
- 2026-11-01 阶段4修复：补 cloud depth-bounds cull pass，修复云不透明白块。
- 2026-11-01 阶段4 cloud OIT 最终验收：blit_depth、独立 cloud depth、bounds-only 深度写、夜间 alpha/内部面修正完成；全量1272通过，用户确认昼夜正常。
- 2026-11-01 阶段4最终：Float32Blendable→RGBA32F OIT（无特性回退16F），玻璃/云条纹消失；全量1272通过。
- 2026-11-01 区块卸载+显存释放完成（T-327）：视距+2 外移除 world/chunk cache/GPU store；known-gaps OIT/卸载项关闭；全量1272通过。
- 2026-11-01 高分辨率 OIT 压测（T-328）通过：2560×1334 视距12 40s 无 OOM。
- 2026-11-01 天空盘切片范围记录：上/下盘实现；水下暗盘延后到水下渲染。
- 2026-11-01 天空盘上盘实现（T-330）：opaque 通道 + SkyDiscDraws，y=+16 SKY_COLOR 于日月星前；水下暗盘延后。
- 2026-11-01 白雾混合（T-331）按 AtmosphericFogEnvironment 公式接入当前雾色。
- 2026-11-01 天空盘 fog 渐变（T-332）：顶点按距离 SKY_COLOR→FOG_COLOR，地平线白化。
- 2026-11-01 天空盘地平线缝隙：窗口 clear 改为雾色（T-333）。
- 2026-11-01 天空盘渐变改为 16环×64段细分（T-334），地平线过渡更顺滑。
- 2026-11-01 天空盘 Vanilla 逐像素 shader 默认，fast 顶点渐变回退；FOV 默认70（T-335）。
- 2026-11-01 天空盘改为源码等效：逐像素 fog + FogSkyEnd=min(renderDistanceBlocks,512)，撤销视觉调参。
- 2026-11-01 Vanilla天空盘相机平移修复（T-337）：全量1272通过；天空盘观感正常。
- 2026-11-01 固定机位天空/雾采样通过：Sharpcraft (136,169,255) vs vanilla (137,170,255)。
- 2026-11-01 视距32统计：154FPS、solid arena 1770chunks/1040MB；draw call非瓶颈，显存优先（T-339）。
- 2026-11-01 2560视距32统计（T-340）：162FPS，几何显存≈1.22GB；不引入paged arena。
- 2026-11-01 删除旧 arena 死代码（T-341）；全量1267通过。
- 2026-11-01 加入 DrawIndexed 计数（T-342）；视距32约4700 draws/frame但仍160FPS。
- 2026-11-01 古城室内压测（T-343）：155FPS、约1902 draws/frame、几何≈1.46GB；不引入paged arena。
- 2026-11-01 M5.3 P1 实体视锥剔除接线完成（T-344）；全量1267通过，剔除计数走 MC_DIAG。
- 2026-11-01 环境问题：后台残留 16 个 `dotnet` host（21:01:28 启动，RSS≈2.3GB）、1 个 `VBCSCompiler`（≈350MB）、2 个无法结束的 `Sharpcraft.Launcher` 空壳；文档此前仅有历史清理记录，需在后续收尾时统一回收，避免多次 build/test 累积。
- 2026-11-01 补充 M5.3 P1 实现文档（docs/modules/render.md §9），此前只有规格与 traceability。
- 2026-11-01 增加 `MC_PRESENT_MODE`（默认Fifo）；视距32 immediate≈156FPS，此前Fifo数据需标注口径（T-345）。
- 2026-11-01 M5.3 P1 名字牌投影完成（T-346，4测试）；UI 文本绘制待接。
- 2026-11-01 M5.3 名字牌 UI 接线（T-347）：玩家名从 player list 映射并绘制；全量1271通过。
- 2026-11-01 修复 PlayerInfoUpdate：固定 bitset mask + nullable chat session + 预加载延迟；双客户端名字牌实测显示 Blue_Coast（T-348）。
- 2026-11-02 世界空间文字调查完成（T-349，docs/analysis/render/world-space-text.md）；名字牌/告示牌/文本展示统一世界文字契约已记录，待实现 WorldTextRenderer。
- 2026-11-02 世界空间名字牌第一片（T-350）：billboard/世界尺度0.025、背景alpha0.25、文字alpha0.5、无阴影；告示牌/文本展示待接。
- 2026-11-02 世界文字 pass 第一版（T-351）：WorldTextResources + WorldTextMeshBuilder；名字牌改走 glyph 世界四边形，修正 font.Scale；告示牌/文本展示待接。
- 2026-11-02 名字牌背景按原版 `markBackground` 修正上下各一像素、去掉右侧多余一像素（T-352）。
- 2026-11-02 名字牌高度偏差登记（T-353）：待完整实体模型/碰撞高度切片解决，不在当前切片硬调。
- 2026-11-02 M5.3 P1 实体渲染设计文档完成（T-354）：RenderState 解耦、玩家模型尺寸/UV 证据、P1 静态姿态范围；下一片实现模型定义与网格。
- 2026-11-02 玩家静态模型定义（T-355）：部件尺寸/偏移按 HumanoidModel；占位网格已使用模型部件表，UV/皮肤下一步。
- 2026-11-02 玩家皮肤资源/UV 元数据（T-356）：皮肤加载器与部件纹理偏移已加；下一步实体皮肤网格/绑定。
- 2026-11-02 玩家皮肤模型（T-357）：默认 skin 纹理加载、部件 6 面 UV、实体 skin bind group 接入；双客户端实测模型带皮肤。
- 2026-11-02 皮肤 UV 修正（T-358）：SOUTH/NORTH UV 按 ModelPart.Cube；wide/slim 选择待补。
- 2026-11-02 实体皮肤 UV 上下方向修正（T-359）：双客户端模型皮肤正常。
- 2026-11-02 玩家皮肤面序/U 方向与手臂中心修正（T-360）；双客户端验证中。
- 2026-11-02 实体纹理坐标依据核实（T-361）：禁止全局 skin V 翻转；按 ModelPart.Polygon 顶点顺序。
- 2026-11-02 wide 玩家 left_arm/left_leg 纹理偏移按 PlayerModel 修正（T-362）。
- 2026-11-02 玩家面内 UV 180° 修正（T-363）：双客户端皮肤方向正常。
- 2026-11-02 玩家逐面 UV 表实现（T-364）：wide 模型皮肤方向正常；slim 选择待后续。
- 2026-11-02 玩家面顺序对齐渲染器剔除（T-365）；双客户端验证中。
- 2026-11-02 独立实体模型沙盒（T-366）：`tools/Sharpcraft.EntityModelSandbox` 输出正面 PNG，UV 正常；游戏内路径继续按沙盒结果收敛。
- 2026-11-02 实体模型沙盒多视角（T-367）：front正常、left/back/肩膀仍不对；后续仅在沙盒收敛。
- 2026-11-02 沙盒五视角（T-367）：front/肩膀正常，left/back 仍不符；后续用 UV 调试纹理定位侧面。
- 2026-11-02 沙盒最近邻与箭头皮肤（T-368）：用于逐面方向定位。
- 2026-11-02 侧面 V 方向修正 + 面基诊断（T-369）：侧面 V->-Y；法线全部朝外；待游戏回归。
- 2026-11-02 Face UV Studio（T-370）：网页面朝向调节器已可用；待用户标定后回写 mesher。
- 2026-11-02 Face UV Studio 3D/最近邻（T-371）：网页面可逐面操作并 3D 预览。
- 2026-11-02 Face UV Studio/沙盒同步（T-372）：网页操作 -> 沙盒 /state -> /frame.png 实时预览；继续用模板校准。
- 2026-11-02 沙盒窗口 + 左键轨道相机（T-373）：`--window` 可用，鼠标左键拖动旋转。
- 2026-11-02 网页控制驱动沙盒窗口（T-374）：`--window` 同时提供页面和实时 3D；可边调面边看。
- 2026-11-02 转到面按钮（T-375）：网页可让沙盒窗口相机直接对准当前面，解决看不见面导致“无响应”。
- 2026-11-02 沙盒窗口内键盘面编辑（T-376）：无网页/HTTP；标题显示当前面与 rot/flip。
- 2026-11-02 沙盒选中面绿色高亮（T-377）：当前面在 3D 中可见，标题同步状态。
- 2026-11-02 沙盒部件隔离/隐藏（T-378）：Home 隔离当前部件、End 隐藏当前部件。
- 2026-11-02 body DOWN/UP 纹理互换（T-379）。
- 2026-11-02 全部部件 DOWN/UP 纹理互换（T-380）。
- 2026-11-02 全部部件 WEST/EAST 纹理互换（T-381）。
- 2026-11-02 沙盒面状态导出（T-382）：每次改面/按 T 写 `%TEMP%\sharpcraft-face-state.json`。
- 2026-11-02 面 UV 校准结果固化（T-383）：DOWN/UP 180°，侧面 FlipH；游戏路径使用默认变换。
- 2026-11-02 模板皮肤支持（T-384）：template.png 可作逻辑 64×64 沙盒纹理。
- 2026-11-02 模板 UV 逻辑尺寸修正（T-385）：1600 模板按 64×64 UV，文字可见。
- 2026-11-02 游戏模板皮肤启动（T-386）：`MC_PLAYER_SKIN` 覆盖，1600 模板按 64×64 UV，双客户端已启动。
- 2026-11-02 左右肢体几何镜像（T-387）：左右臂/腿中心 X 取反，纹理不变。
- 2026-11-02 通用实体模型基础（T-388）：`EntityModelDefinition` + `EntityModelResolver`，玩家已通过 resolver。
- 2026-11-02 Zombie 模型转译（T-389）：HumanoidModel 部件定义与 resolver 注册完成，mob 纹理/渲染待接。
- 2026-11-02 生物模型移植清单与进展文档（T-390）：按复杂度排序，记录 Player 完成、Zombie 定义完成/纹理未接。
- 2026-11-02 非玩家模型回退（T-391）：未注册实体使用 Placeholder，不再消失。
- 2026-11-02 模型纹理路径元数据（T-392）：Player/Zombie 定义带 TexturePath。
- 2026-11-02 实体 skin 图集/Zombie 纹理链路（T-393）：player+zombie 图集、按 TexturePath 选区域；待游戏观察。
- 2026-11-02 空世界文字 draw 崩溃修复（T-394）：无名字牌时不再创建空 buffer。
- 2026-11-02 实体纹理统一资源层（T-395）：EntitySkinLibrary 按 TexturePath 从 ResourceManager 取纹理。
- 2026-11-02 `EntityModelPart.Mirror` 生效（T-396）：`EntityModelMesher` 对 mirror 部件所有面 U 取反，Zombie 左臂/左腿待目视。
- 2026-11-02 Skeleton 模型转译（T-397）：Humanoid definition 注册 type id 118，待游戏观察。
- 2026-11-02 Skeleton 模型修正（T-398）：2×12×2 四肢、64×32 纹理；待游戏观察。
- 2026-11-02 实体无背面剔除（T-399）：entity pipeline `CullMode.None`，待游戏观察骷髅。
- 2026-11-02 沙盒统一资源/模型切换（T-400）：MC_SANDBOX_MODEL=player/zombie/skeleton，texture 走 EntitySkinLibrary。
- 2026-11-02 Husk 模型转译（T-401）：ZombieModel + 1.0625 scale，纹理 entity/zombie/husk，沙盒已验证。
- 2026-11-02 人形双层纹理/overlay（T-402）：hat/jacket/sleeves/pants 定义与 mesher 扩边输出，待沙盒/游戏观察。
- 2026-11-02 Player 透明/可见性（T-403）：实体 pipeline alpha blend，外层 show* 过滤。
- 2026-11-02 Drowned 模型转译（T-404）：base + drowned_outer_layer，overlay 独立纹理路径；沙盒已验证。
- 2026-11-02 Drowned overlay reserve 修正（T-405）：按 parts+overlays 预留几何，沙盒 drowned 纹理正确。
- 2026-11-02 Enderman 模型转译（T-406）：独立 yOffset/长肢/64×32 texture，沙盒已验证。
- 2026-11-02 实体特殊层文档（T-407）：记录 Enderman eyes/Player translucent/Drowned outer 等待补项。
- 2026-11-02 Creeper 模型转译 + CentreZ（T-408）：六 flat parts，沙盒已验证。
- 2026-11-02 Slime 模型转译（T-409）：inner cube + eyes/mouth + outer grow cube，沙盒已验证。
- 2026-11-02 实体 transluent alpha 修复（T-410）：fs_entity_translucent 保留 sampled.a，Slime 半透明生效。
- 2026-11-02 Slime outer cube 修正（T-411）：独立 outer_cube 8×8×8，纹理缺块修复。
- 2026-11-02 Magma Cube 模型转译（T-412）：8 segments + inside cube，沙盒已验证。
- 2026-11-02 Part pivot/rotation + Spider（T-413）：spider 8 腿旋转，沙盒已验证。
- 2026-11-02 Villager 模型转译（T-414）：hat/nose/jacket/交叉手臂/腿，scale 0.9375，沙盒已验证。
- 2026-11-02 Villager 手臂左右修正（T-415）：left_arm +X/right_arm -X。
- 2026-11-02 Villager 左右臂语义修正（T-416）：left_arm=-X mirror true，right_arm=+X mirror false。
- 2026-11-02 Villager 手臂 Y 修正（T-417）：Bottom=15，手臂回到肩部。
- 2026-11-02 Villager 手臂 X 旋转符号修正（T-418）：RotationX=+0.75。
- 2026-11-02 实体坐标系统调查（T-419）：地图/相机已统一，实体模型/朝向仍未统一；下一步先做 EntityModelTransform。
- 2026-11-02 EntityModelTransform（T-420）：实体根变换统一，mesher 应用 Translation/Yaw/Pitch/Scale；后续迁移模型 y-down 坐标。
- 2026-11-02 已有实体路径迁移（T-421）：placeholder mesher 也使用 EntityModelTransform。
- 2026-11-02 Player 模型定义迁移（T-422）：FromVanilla 工厂，Player 使用原版 pivot/cube 坐标；其余模型继续迁移。
- 2026-11-02 全部实体模型定义迁移（T-423）：现有模型统一 FromVanilla raw 坐标。
- 2026-11-02 Pig 模型转译（T-424）：head/snout + rotated body + 四腿，variant 纹理 pig_temperate。
- 2026-11-02 Sheep 模型转译（T-425）：Quadruped + wool overlay，沙盒已验证。
- 2026-11-02 Sheep wool head 修正（T-426）：wool_head 独立 part，脸/腿恢复。
- 2026-11-02 Sheep 基础腿/羊毛组修正（T-427）：4×12×4 base legs + 独立 wool parts，脸/腿恢复。
- 2026-11-02 Cow 模型转译（T-428）：头/鼻/角、body+udder、四腿，沙盒已验证。
- 2026-11-02 Chicken 模型转译（T-429）：头/喙/肉垂 + 旋转 body + 腿/翅，沙盒已验证。
- 2026-11-02 Wolf 模型转译（T-430）：real_head/body/upper_body/四腿/tail，沙盒已验证。
- 2026-11-02 Cat 模型转译（T-431）：head/body/tail/legs，scale0.8，沙盒已验证。
- 2026-11-02 Ocelot 模型转译（T-432）：复用 Feline 几何 + ocelot 纹理。
- 2026-11-02 Fox 模型转译（T-433）：head/body/legs/tail，沙盒已验证。
- 2026-11-02 Fox tail 修正（T-434）：body 子变换合成，tail 回到身体后方。
- 2026-11-02 Polar Bear 模型转译（T-436）：head/body/四腿，scale1.2。
- 2026-11-02 Panda 模型转译（T-437）：head/body/legs，沙盒已验证。
- 2026-11-02 Goat 模型转译（T-438）：head/body/legs，沙盒已验证。
- 2026-11-02 Snow Golem 模型转译（T-439）：head/body/双臂 scale deformation，Rabbit 暂缓（嵌套层级）。
- 2026-11-02 Snow Golem 手臂方向修正（T-440）：双臂朝下。
- 2026-11-02 Squid 模型转译（T-441）：body + 8 tentacles。
- 2026-11-02 Glow Squid 模型转译（T-442）：复用 Squid + glow texture。
- 2026-11-02 Mooshroom 模型转译（T-443）：复用 Cow + mooshroom_red。
- 2026-11-02 Horse 模型转译（T-444）：AbstractEquineModel 主体，沙盒已验证。
- 2026-11-02 Horse 耳朵/尾巴修正（T-445）：head 子耳 + body 子 tail 合成。
- 2026-11-02 Donkey 模型转译（T-446）：复用 Equine + donkey texture，scale0.87。
- 2026-11-02 Mule 模型转译（T-447）：复用 Equine + mule texture，scale0.92。
- 2026-11-02 Zombie Horse/Skeleton Horse（T-448）：复用 Equine 变体。
- 2026-11-02 Llama 模型转译（T-449）：head/body/chest/legs。
- 2026-11-02 Llama chest 可见性（T-450）：HasChest 控制，默认隐藏。
- 2026-11-02 Llama chest 开口修正（T-451）：rotationY=-π/2，开口朝外。
- 2026-11-02 撤销 Camel 近似（T-452）：不交付近似模型，待层级变换后重做。
- 2026-11-02 实体模型近似审计（T-453）：Horse/Fox/Wolf/Rabbit/Camel 标记为待层级化，不再新增近似。
- 2026-11-02 Skeleton 变体（T-454）：Stray/Bogged/Wither Skeleton。
- 2026-11-02 Zombie Villager（T-455）：复用 Villager + zombie_villager。
- 2026-11-02 Zombie Villager 独立几何修正（T-456）：Humanoid 几何，不再复用 Villager。
- 2026-11-02 Skeleton clothing overlay（T-457）：Stray/Bogged 外层补齐。
- 2026-11-02 Skeleton outer hat（T-458）：Stray/Bogged outer_hat 补齐。
- 2026-11-02 实体渲染层差距登记（T-459）：合并 part 不具原版等效性，暂停新模型，先做多 layer 架构。
- 2026-11-02 Endermite 模型转译（T-460）：4 segments root parts。
- 2026-11-02 Silverfish 模型转译（T-461）：7 segments + 3 layers。
- 2026-11-02 Iron Golem 模型转译（T-462）：root parts，沙盒已验证。
- 2026-11-02 Illager 变体（T-463）：Vindicator/Evoker/Pillager。
- 2026-11-02 Wandering Trader（T-464）：复用 Villager + wandering_trader。
- 2026-11-02 Blaze 模型转译（T-465）：head + 12 rods，沙盒已验证。
- 2026-11-02 Turtle 模型转译（T-466）：root parts + HasEgg。


## 2026-11-02 上下文压缩前交接

**当前分支/工作目录**
- 工作目录：仓库内的 `m5.3-render-p1` worktree（不在文档里记本机绝对路径）
- 最近提交到 `T-466`；Client 测试最近一次为 **620 通过**。

**当前模型状态**
- 已完成/可用：Player、Zombie、Husk、Skeleton、Stray、Bogged、Wither Skeleton、Drowned、Enderman、Creeper、Slime、Magma Cube、Pig、Sheep、Cow、Chicken、Spider、Villager、Zombie Villager、Snow Golem、Llama、Wolf、Fox、Cat、Ocelot、Polar Bear、Panda、Goat、Horse、Donkey、Mule、Zombie Horse、Skeleton Horse、Mooshroom、Endermite、Silverfish、Iron Golem、Vindicator、Evoker、Pillager、Wandering Trader、Blaze、Turtle。
- 注意：部分“有外观但未严格等效”的项已登记在 `docs/analysis/render/entity-model-approximations.md` 和 `entity-render-layers.md`：Horse/Fox/Wolf/Rabbit/Camel 的层级近似，以及 Skeleton clothing/Player overlay/Drowned outer/Slime outer 的独立 RenderLayer 差距。

**接下来优先做：变体纹理系统（不引入层级近似）**
- 现在 `EntityModelResolver` 只按 `TypeId` 解析；
- 增加 `EntityRenderState.Variant`（或 VariantId）；
- `EntityModelResolver` 支持 `(TypeId, Variant) -> EntityModelDefinition`；
- ~~变体仅切换纹理，不改变几何~~ **已更正（2026-11-03）**：Pig/Cow/Chicken 的 warm/cold 同时更换几何
  （ColdPig/WarmCow/ColdCow/ColdChicken）；Cat/Horse/Llama 才是纯纹理；
- 首批目标：Pig/Cow/Chicken warm/cold、Llama 颜色、Cat 毛色、Horse 毛色；
- 完成后可安全增加大量外观而不违反层级/图层原则。

**之后必须补的基础设施**
1. `Parent` + 局部 `PartPose` 递归变换：解决 Horse/Fox/Wolf/Rabbit/Camel。
2. `EntityRenderLayerDefinition` / `EntityGeometryLayer` 多 layer 提交：解决 Stray/Bogged clothing、Player overlays、Drowned outer、Slime outer。
3. Illager `armPose` 可见性：当前 arms group 与正常 arms 同时显示，原版只显示一套。
4. Enderman eyes、Slime/Guardian 等 emissive/beam 特殊层。
5. `EntityPlaceholderMesher` 已接入 `EntityModelTransform`；后续模型定义继续迁移到 raw y-down/PartPose 坐标。


## 2026-11-03 实体变体（variant）系统首批完成（T-467…T-469）

- **数据链路已通**：`set_entity_data`（play clientbound **101**）由 `Common/Network/EntityDataPackets` 解码；
  变体访问器索引按继承链累计（Pig 19、Cow 18、Chicken 18、Cat 20、Horse 19、Llama 20），见 `entity-variants.md` 2.1。
  基础字段按 serializer 宽度跳过；遇到未建模 serializer（ITEM_STACK 等）**停止扫描并回退默认变体**，不猜长度。
- **注册表优先**：configuration 阶段 `registry_data` 的 `pig_variant/cow_variant/chicken_variant/cat_variant`
  条目顺序即 id；`EntityVariantTable` 优先用它，缺失时回落原版 bootstrap 顺序（沙盒/离屏可测）。
- **纹理变体**：Cat 11、Horse 7、Llama 4；Horse 值高位是 markings，用 `&0xFF` 取毛色。
- **几何变体**：Pig cold = body 加 0.5 外层（UV 28,32）；Cow warm = 头 4 个耳/角盒（2 个 mirror），
  cow cold = body 0.5 外层（UV 20,32）+ snout UV 9,33 + 两根旋转 90° 的角；Chicken cold = body 0 宽鳍（UV 38,9）
  + head 额外层（UV 44,0）。全部是平移/单轴旋转，未引入父子嵌套近似。
- **未知变体契约（用户裁决）**：渲染路径回落默认并记录 `UnknownVariants`；`ResolveVariantStrict`/`ResolveStrict`
  抛 `KeyNotFoundException`（工具/测试走严格路径）。见 `docs/analysis/render/entity-variants.md` §6。
- **未覆盖**：幼年模型与 baby 纹理、Wolf（tame/angry）、Fox（睡眠态）——已在分析文档 §5 登记。
- **门禁**：全量 `dotnet test Sharpcraft.slnx` 通过（Common 446 / Client 635 / Framework 107 / Launcher 91 /
  Auth 25 / Modding 4 / Architecture 31）；`Docs.Indexer --check --check-coverage` ok；顺带把 14 行
  traceability 的 `完成` 归一为 `已完成`、清掉 2 处文档里的本机绝对路径（文档漂移门禁曾因此失败）。
- **下一步具体动作**：
  1. 沙盒目视验收：`MC_SANDBOX_MODEL=pig|cow|chicken MC_SANDBOX_VARIANT=warm|cold`（以及 cat/horse/llama 的各色），
     与 `mc-26.3-analysis` 源码几何逐部件对照；真服务器再走一次 `set_entity_data` 看变体切换计数。
  2. 补 `Parent` + 局部 `PartPose` 递归变换，再解 Horse/Fox/Wolf/Rabbit/Camel 的层级近似。
  3. 多 layer 提交（`EntityRenderLayerDefinition`）解决 Stray/Bogged clothing、Player overlays、Drowned/Slime outer。

### 2026-11-03 冷牛角符号修正（用户目视校验）

- 用户反馈寒带牛“角的位置不太对”。复查 `ColdCowModel`：两只角是 head 的子部件，
  `right_horn` 局部 (-4.5,-2.5,-3.5)/`left_horn` (5.5,-2.5,-5.0)，`xRot = +90°`。
- **根因**：本仓库把原版 y-down 空间的 X、Y 取反烘焙进部件（等价绕 Z 转 180°），单轴旋转必须取反：
  `rotationX = -原版 xRot`。我先前写成 `+1.5708`，导致角在 Z 方向偏 3 px（陷在头里而非从头顶前方伸出）。
  已改为 `-1.5708`；用已有模型交叉验证该约定（牛/猪 body `+π/2 → -π/2`、猫 tail `+0.9 → -0.9`、
  马 head `+π/6 → -π/6`）。
- 新增回归 `TheColdCowHornsSitAtTheVanillaPositionAfterTheRotationSignConversion`：独立算出角的世界包围盒
  （右侧 x[4,6]、左侧 x[-6,-4]、y[22,24]、z[-16,-10] 模型像素）并逐轴断言，Client **636 通过**。
- 用户复看沙盒确认“现在看起来正确了”。

## 2026-11-03 父子层级变换（T-470）

- 新增 `EntityModelPart.Parent` 与 `PlayerModelDefinition.FromVanillaChild`：子部件按父原点表达 pivot/cube/rotation；
  mesher 用矩阵递归 `R·T(pivot)·PartFrame(parent)`，根部件仍存绝对 cube（左乘 `T(-pivot)` 等价绕 pivot 旋转）。
- 空 group 节点（0×0×0）只承载 pivot，不发射几何（Wolf 的 head/tail）。
- Horse / Fox / Wolf 改为层级定义，删除手工合成的“近似 pivot”；`entity-model-approximations.md` 状态更新。
- 验收：`EntityModelHierarchyTests` 用 head_parts→head→right_ear 三层链，与独立手算包围盒逐轴一致；
  ColdCow 角测试继续覆盖“根部件 + 旋转”。全量 Client 637 通过（`ChunkMeshCacheTests` 的 lightmap 用例是**既有偶发**
  失败，单独重跑 3 次中 1 次通过，与本次改动无关，已记录待查）。
- 未迁移：Rabbit（定义存在、未注册）、Camel（近似撤销，待重做）；下一步按同样方式补这两个。

## 2026-11-03 A 收尾：Rabbit + Camel 层级化（T-471）

- **Rabbit** 重做：body(root, xRot -22.5°) → tail / head(+22.5°) → 左右耳；body → frontlegs(group) → 前后腿(+22.5°)；
  body → backlegs(root group) → 左右后腿 group → haunch（yRot ±22.5°）。注册 type id **111**。
- **Camel** 新建：body(root) → hump / tail / head(group) → head_lower/head_upper/head_snout + 左右耳；四腿为 root。
  纹理 `entity/camel/camel`（128×128）。type id **19** —— 由 `EntityTypes.java` 的注册序推导，并用已有 Pig 101 / Cow 30 /
  Turtle 141 等 id 反向校验（全部吻合）后才固化。
- 空 group 节点沿用上一轮机制；Rabbit 的 frontlegs/backlegs/两条 hind_leg、Camel 的 head 都是 group（不发射几何）。
- 验收：`EntityModelHierarchyTests` 新增 rabbit head（body+head 双旋转）与 camel head_lower 的逐轴包围盒，与独立手算一致；
  `EntityModelRegistrationTests` 钉住 part 数与 Parent 关系、以及 type id。Client **642 通过**。
- 沙盒目视：camel（长腿/驼峰/长颈）、rabbit（竖耳/蜷身）形态正常。
- **A（父子层级）到此收口**：Horse/Fox/Wolf/Rabbit/Camel 5 个全部层级化并注册。

## 2026-11-03 B 第一/二步：实体多 RenderLayer 提交（T-472，ADR-0021）

- 客户端：`BuildLayers` 生成 Cutout/Translucent 两层（`Build` 仍合并）；Player overlays、Drowned outer、Slime outer
  标为 Translucent，基础模型与 Skeleton clothing 保持 Cutout。
- 框架：`WindowedRenderer.Run(..., entityLayers:)`，每层一份常驻 vertex/index 缓冲（≤4 层），按 `Order` 绘制；
  新增 `fs_entity_cutout`（alpha 0.5、无混合、深度写），Translucent 复用 `fs_entity_translucent`（混合、深度写关）。
- 验收：`EntityLayerTests` 4 项（无 overlay 仅 Cutout / Player 与 Drowned/Slime 产生 Translucent / 合并总量等于各层之和）；
  沙盒窗口 6 秒运行无 shader validation error；全量 Client **646 通过**。
- **未做**：Emissive（Enderman eyes）——独立深度 `>=`/不写/加色状态，留作下一步。
- 注意：沙盒仍用合并 `Build` 路径，因此只验证了 shader/pipeline 编译；逐层像素验收需要真服务器窗口或把沙盒切到 layered 路径。

### 2026-11-03 沙盒统一到分层路径（T-473）

- `SandboxWindow` 不再把合并几何塞进 `FrameRequest.Vertices`，改为 `BuildLayers` + `entityLayers:`；部件高亮叠加在 Cutout 层。
- 沙盒与真实窗口因此走同一条多层提交路径；Player（overlay 半透明）、Drowned（outer）、Slime（outer 半透明）目视确认。
- 全量测试与 docs 门禁通过后提交。

### 2026-11-03 Emissive 眼睛层（T-474）

- `EntityModelDefinition.EmissiveTexturePath` 声明眼睛纹理（Enderman/Spider 已接）；`TexturePaths` 自动纳入皮肤图集。
- Mesher 的 Emissive pass 用**同一几何 + 眼睛纹理**生成第三层；`fs_entity_emissive` 输出无光照样本（alpha 0.1 测试），
  pipeline = 半透明混合 + 深度 `GREATER_THAN_OR_EQUAL` + 不写深度（对齐 `RenderTypes.eyes`）。
- 沙盒目视：Enderman 品红眼、Spider 红眼发光正常；无 pipeline/validation error。
- 全量 Client **648 通过**（含 2 项新眼睛层测试）。

### 2026-11-03 真服务器逐层验收（T-472/T-474 验收）

- 本地离线服务器（127.0.0.1:25565）实测：窗口客户端正常进入，329 区块流式加载，无 pipeline/validation error。
- 通过聊天命令 `summon` 生成实体：
  - Enderman：`entity cull candidates=1`，截图中品红眼睛发光 → Emissive 层在真实窗口路径生效。
  - Drowned：截图可进入世界（同账号还存在其它客户端残留玩家实体，画面被近距离玩家模型遮挡），外层半透明层已在沙盒确认。
- 结论：分层路径（Cutout/Translucent/Emissive）在真实窗口 + 沙盒两条路径均可用；逐层“像素级”差异仍以沙盒固定机位为准（真服务器场景不可复现）。

### 2026-11-03 下一个：Bat 模型（T-475）

- `BatModelDefinition`：body+feet、两段式翅膀（body→wing→wing_tip）、独立 head+双耳；纹理 `entity/bat/bat`，type id 10（由 `EntityTypes` 注册序）。
- 层级用 `Parent` 表达；静态 bind pose（BatAnimation 未移植）。
- 注册于 resolver；`EntityModelRegistrationTests` 增加 bat 用例（9 部件、wing_tip/ear 的 Parent）；沙盒目视正常。

### 2026-11-03 Bee 模型（T-476）

- `BeeModelDefinition`：`bone`(空 group)→body(+stinger、左右触角)、两翅（yRot ±15°、左翅 mirror、grow 0.001）、前/中/后腿平面；纹理 `entity/bee/bee`，type id 11。
- 注册 + 注册测试（10 部件、Parent 关系）；沙盒目视正常（黄身、蓝眼、薄翅、三对腿）。
- **已知缺口**：BeeRenderer 的 angry / nectar 状态纹理（4 种，另有 baby 4 种）未接；扇翅动画未移植。

### 2026-11-03 状态纹理基础设施（T-477）

- 渲染状态增 `IsTame/IsAngry/IsSleeping/HasNectar`；`EntityDataItem` 支持 LONG（anger end time）；tracker 以 `GameTime` 比较判定 angry。
- `EntityModelDefinition.TextureSelector` + `AdditionalTexturePaths`：所有可能纹理先入皮肤图集，打包时按实体状态选路径。
- 已接：Wolf 9 变体 × wild/tame/angry；Fox red/snow × idle/sleep；Bee angry/nectar 四态（均按原版 `getTexture` 规则）。
- 沙盒 `MC_SANDBOX_STATE=tame|angry|sleeping|nectar` 可强制状态；截图确认 Wolf tame、Bee nectar、Fox snow-sleeping 正确。
- 测试：状态选择器 + tracker flag/anger 用例；全量 Client **653 通过**。

### 2026-11-03 Dolphin 模型（T-478）

- `DolphinModelDefinition`：body→back_fin、左右侧鳍（x/zRot）、head→nose、tail→tail_fin；纹理 `entity/dolphin/dolphin`，type id 36。
- 注册 + 注册测试（8 部件、tail_fin/nose 的 Parent）；沙盒目视正常。

### 2026-11-03 Guardian / Elder Guardian 模型（T-479）

- `GuardianModelDefinition`：head 主体 + 4 块壳、12 根 spike（生成式，按原版 SPIKE_X/Y/Z 与 π*旋转表）、eye、tail0→tail1→tail2（tail2 为 group + body/fin 两 cube）。
- Elder 复用几何，`scale 2.35` + `entity/guardian/guardian_elder`；type id 64 / 41。
- **未移植**：spike 伸缩/尾摆动动画、Guardian 光束（不是模型部件）。
- 注册 + 注册测试（23 部件、12 spike、Parent 关系、Elder scale）；沙盒目视正常（绿身橙刺红眼）。

### 2026-11-03 Frog / Tadpole 模型（T-480）

- Frog：`root`(group)→body(+body_layer)/head(+head_layer、eyes→双眼)/croaking_body/tongue/双臂(+hand)/双腿(+foot)；注册 `frog_variant`（temperate/warm/cold，accessor 18，type id 56）。
- Tadpole：body + 平面 tail，type id 134，纹理 16×16。
- 静态 bind pose；跳跃/鼓鸣/舌/尾动画未移植。沙盒确认 warm（米白）、tadpole 外观正常。

### 2026-11-03 Axolotl 模型（T-481）

- `AxolotlModelDefinition`：body(+body_layer 背鳍)/head(+top/left/right gills)/四条扁腿/tail；`grow 0.001` 与 UV 按原版。
- 5 个变体（lucy/wild/gold/cyan/blue，整数 accessor 18，type id 7），各换纹理 `entity/axolotl/axolotl_<name>`。
- 静态 bind pose；游动/爬行/装死动画未移植。沙盒 blue 变体目视正常。

### 2026-11-03 Strider 模型（T-482）

- `StriderModelDefinition`：right/left_leg + body + 6 根 bristle（zRot 按原版）；`strider` / `strider_cold` 纹理由 `DATA_SUFFOCATING` 布尔（accessor 19）选择；type id 132。
- **图集修复**：`EntitySkinAtlas` 改为**变高堆叠**（总高 = 各纹理高之和），否则 64×128 的 strider 纹理把行距抬到 128，条目一多就超过 8192 纹理上限。
- 注册 + 注册测试（9 部件、6 bristle 均挂 body）；沙盒 cold 状态目视正常（紫红身体+须+双腿）。

### 2026-11-03 Hoglin / Zoglin 模型（T-483）

- `HoglinModelDefinition`：body(+mane)、head(root, xRot 0.8727, 左右耳 zRot ±2π/9, 左右角)、四腿（前腿 14、后腿 11 高）。
- Zoglin 复用几何、`entity/hoglin/zoglin` 纹理；type id 65 / 153。静态 bind pose；头撞/行走动画未移植。
- 注册 + 注册测试（11 部件、Parent、Zoglin 纹理）；沙盒目视正常。

### 2026-11-03 Piglin 系模型（T-484）

- 几何从 `PlayerModel.createMesh(false)` 出发：保留玩家 body/arms/legs 与其外层，替换 head 为猪头（10×8×8 + 鼻/两鼻孔 + 左右耳 ±π/6），hat 清空。
- Piglin / Piglin Brute / Zombified Piglin 共用几何，纹理分别为 `piglin` / `piglin_brute` / `zombified_piglin`；type id 102/103/158。
- 静态 bind pose；Piglin 的 arm pose（dancing/crossbow/admiring）与动画未移植。
- **已知差异（记录）**：原版 Piglin 主模型 RenderType 是 `entityTranslucent`，我们与玩家模型一致走 Cutout（alpha=1 纹理观感相同，但渲染状态不完全等效）。
- 注册 + 注册测试（11 部件、鼻/耳 Parent、三种纹理）；沙盒目视正常。

### 2026-11-03 Allay / Vex 模型（T-485）

- Allay：root(group)→head/body(+body_inner grow-0.2)/双臂/双翅；整体标为 Translucent（对应原版 `entityTranslucent`）；type id 2。
- Vex：同构（root pivot -2.5、body/head pivot 20、臂 grow-0.1、左翅 mirror）；`vex`/`vex_charging` 由 DATA_FLAGS 充电位（accessor 16, bit0）选择；type id 142。
- 静态 bind pose；飞行/舞动/充能动画与手持物品层未移植。沙盒 allay 与 vex charging 目视正常。

### 2026-11-03 Parrot 模型（T-486）

- `ParrotModelDefinition`：body/tail/双翅/head(+head2/beak1/beak2/feather)/双腿；pivot 与旋转按原版（root 部件手动取反，子部件由 FromVanillaChild 处理）。
- 5 变体（red_blue/blue/green/yellow_blue/gray，accessor 20，type id 99；gray 文件名是 `parrot_grey`）。
- 静态 bind pose；站/飞/坐/肩/派对姿势与扇翅动画未移植。沙盒 blue 目视正常。

### 2026-11-03 Shulker 模型（T-487）

- `ShulkerModelDefinition`：lid / base / head 三部件（bind pose 盖子合上，head 在壳内）。
- 16 个染料颜色变体（byte accessor 18，type id 115；值 16 = 无颜色 → 默认纹理），纹理 `shulker` / `shulker_<color>`。
- 静态 bind pose；peek/开盖与头部动画未移植，原版 `entityCutoutZOffset` 的深度偏移未复刻。沙盒 light_blue 目视正常。

### 2026-11-03 Cod / Salmon 模型（T-488）

- Cod：body/head/nose/左右鳍/tail_fin/top_fin；type id 27，`fish/cod`。
- Salmon：body_front/body_back(+back_fin、top_back_fin)/head/top_front_fin/左右鳍；3 个尺寸变体 small/medium/large（scale 0.5/1.0/1.5，accessor 18，type id 113）。
- 静态 bind pose；摆尾动画未移植。沙盒 cod 与 salmon(large) 目视正常。
### 2026-11-03 Pufferfish 模型（T-489）

- 三个膨胀阶段各自独立几何：small(6 部件)/medium(11)/large(13)，共用 `entity/fish/pufferfish` 纹理。
- puff state 0/1/2（int accessor 18，type id 110）；base 定义名为 `pufferfish`，`FindTypeId` 可用。
- 静态 bind pose；鳍摆动画未移植。沙盒 large 目视正常。

### 2026-11-03 Nautilus 模型（T-490）

- `NautilusModelDefinition`：root(group)→shell(主体/中层/背板)、body(2 盒)→上/内/下嘴；纹理 `entity/nautilus/nautilus`，type id 89。
- 静态 bind pose；游动动画与 armor/saddle 层未移植；Zombie Nautilus 另属独立模型。
- **Tropical Fish 暂缓**：它需要「每层独立 tint（baseColor/patternColor）+ pattern 纹理层」，当前多 layer 契约没有 tint；先记为缺口，待 tint 基础设施。

### 2026-11-03 Cave Spider / Trader Llama（T-491）

- CaveSpider：复用 Spider 几何，scale 0.7，纹理 `entity/spider/cave_spider`，type id 22。
- TraderLlama：复用 Llama 几何与 body 纹理 `entity/llama/llama_brown`，type id 138；**trader 地毯**是 `equipment/llama_body/trader_llama` 装饰层（装备渲染），未移植。
- 两者均静态 bind pose；沙盒目视正常（发现并修正过一次 TraderLlama 误用不存在的 `entity/llama/trader_llama` 纹理 → 棋盘格）。

### 2026-11-03 Cave Spider 眼睛层修正（T-492）

- 原版 `CaveSpiderRenderer` 继承 `SpiderRenderer`，两者都加 `SpiderEyesLayer`，纹理同为 `entity/spider/spider_eyes` → 洞穴蜘蛛与普通蜘蛛眼睛**相同**。
- 此前 `CaveSpiderModelDefinition` 漏了 `EmissiveTexturePath`，导致眼睛层缺失；已补上，现与蜘蛛一致。
- 顺带补回归测试：蜘蛛腿旋转符号（mirrorX:false 的局部约定）与洞穴蜘蛛 = 0.7× 蜘蛛几何的逐轴断言。

### 2026-11-03 Emissive 眼睛穿透修复（T-493）

- 现象：从背后也能看到眼睛。根因是原版 `RenderPipelines.EYES` 的 `GREATER_THAN_OR_EQUAL` 建立在原版深度约定上；我们的投影是标准 0..1 深度，照抄比较方向反了（远处片元通过 → 穿透）。
- 修正：emissive pipeline 改为 **`LESS_EQUAL` + depth write off**；正视眼睛正常，背视 180° 不再出现眼睛。
- 沙盒新增 `MC_SANDBOX_YAW`（调试工具）用于固定正面/背面机位验收；已用 3.14159 验证背视干净。
- 该修复同时作用于 Enderman/Spider 眼睛层。

### 2026-11-03 Witch 模型（T-494）

- 复用 Villager body/jacket/arms/legs；替换 head 为 8×10×8，nose/mole 挂在 head/nose 下，帽子四级链 hat→hat2→hat3→hat4（原版 offset/rotation 全部保留）。
- 纹理 `entity/witch/witch`（64×128），scale 0.9375，type id 148。
- 静态 bind pose；WitchItemLayer（手持物品）与鼻子摆动动画未移植。沙盒目视帽子/袍子正常。

### 2026-11-03 Witch 身体修正（T-495）

- 对照原版 `VillagerModel.createBodyModel()`：`body`(8×12×6, UV16,20)、`jacket` 是 **body 的子级、grow 0.5**（8×20×6, UV0,38）、`arms` 是**单个组**（pivot 0,3,-1, rotX -0.75，三个盒：左右臂 + 中间横臂）。
- 旧 Witch 直接复用项目 Villager 部件，`jacket` 为 root 且无 grow，与 body 同面产生 z-fighting，正面会混进 body 贴图（看起来像村民衣服）。现改为 Witch 自带精确结构，紫色长袍+绿色前襟显示正确。
- **遗留**：项目通用 `VillagerModelDefinition` 也存在 jacket 无 grow、缺 hat_rim 的偏差，本次未动；后续单独修（避免影响已验收的村民）。

### 2026-11-03 Villager 模型补全（T-496）

- 对照原版 `VillagerModel.createBodyModel()` 修正此前偏差：`hat`/`hat_rim` 改为 head/hat 子级（hat grow 0.51；hat_rim 16×16×1 rotX -π/2）；`jacket` 改为 body 子级并 **grow 0.5**（旧实现 root 且无 grow → 与 body 共面 z-fighting）；`arms` 改为单组（rotX -0.75）下含左右臂 + 中间横臂三盒；`nose` 挂 head。
- 纹理 `entity/villager/villager`（64×64），scale 0.9375；WanderingTrader 复用同一几何与 64×64 `wandering_trader` 纹理。
- 静态 bind pose；村民原本的转头/走路动画未移植。沙盒目视正常（长袍/围裙/鼻子/交叉手臂）。

### 2026-11-03 Giant 模型（T-497）

- GiantZombieModel 使用 humanoid 基础几何 + `MeshTransformer.scaling(6.0)`；我们复用 Zombie 几何并设 `Scale = 6`（等价，脚底锚点一致），纹理 `entity/zombie/zombie`，type id 60。
- 静态 bind pose；僵尸手臂动画（animateZombieArms）与装备层未移植。
- 沙盒调试工具新增 `MC_SANDBOX_DIST` / `MC_SANDBOX_TARGET_Y`，便于给巨型模型取景。

### 2026-11-03 Illusioner 模型（T-498）

- 复用 `IllagerModel` 几何（与 Evoker/Vindicator/Pillager 同一套），纹理 `entity/illager/illusioner`，type id 69。
- 静态 bind pose；施法/隐身分身等渲染特例未移植。沙盒目视正常。

### 2026-11-03 Sniffer 模型（T-499）

- 结构：`bone`(group)→`body`（主体、grow 0.5 外层、平底）、六条腿（前/中/后 ×左右）、`head`（主体+平顶、左右薄耳、nose、lower_beak）。
- 纹理 `entity/sniffer/sniffer`（192×192），type id 122；静态 bind pose；sniff/dig/walk 动画未移植。幼年 `snifflet` 留待 baby 阶段。

### 2026-11-03 Creaking 模型（T-500）

- 结构：root / upper_body 两层 group；head 4 盒（主体/顶盖/左右侧板）、body 2 盒、right_arm 2 盒、left_arm 3 盒、左右腿 2/3 盒，共 18 部件。
- 纹理 `entity/creaking/creaking`，眼睛层 `creaking_eyes`；type id 31。
- 静态 bind pose；walk/attack/invulnerable/death 动画未移植。**已知差异**：原版眼睛层只在 `eyesGlowing` 时渲染且仅 head；我们始终渲染整个 emissive 头层（贴图其余透明，观感近似）。

### 2026-11-03 Ghast 模型（T-501）

- `body`（16×16×16）+ 9 条 `tentacle`（长度按原版 seeded `Random(1660)` 序列：8/13/9/11/11/10/12/9/12；pivot 由原版公式计算），整体 `Scale = 4.5`。
- `ghast` / `ghast_shooting` 纹理由 boolean charging（accessor 16）选择；type id 58。
- 静态 bind pose；触须摆动 `animateTentacles` 未移植。

### 2026-11-03 Ghast 贴图尺寸修正（T-502）

- 现象：恶魂身体像“少了几面”。根因：`entity/ghast/ghast.png` 实际是 **128×64**，Definition 误写 `64×32`，UV 被按一半尺寸采样，命中透明像素。
- 修正为 128×64；六面完整、脸部正常。以后新增模型须先从 pack 确认纹理实际尺寸（不要按模型 UV 最大值猜）。

### 2026-11-03 实体 UV 归一化修正（T-503）

- 真正根因：`EntityModelMesher` 用**贴图 PNG 实际分辨率**归一化 UV；原版用 **模型声明的纹理尺寸**（`ModelPart.Cube` 的 xTexSize/yTexSize）。恶魂声明 64×32，但 PNG 是 128×64（2× 分辨率），按 128×64 归一化后 UV 落进透明区，正面/底面消失。
- 修正：`BuildInto` 现在一律用 `model.TextureWidth/Height` 归一化（贴图 sprite 只决定采样矩形，不影响 UV 归一化）；游戏与沙盒共用此路径。恶魂六面完整、脸部（眼/嘴/泪痕）正确。
- 注意：T-502 里“把纹理尺寸改成 128×64”是错误方向，已回退为模型声明值 64×32；T-503 才是根因修复。

### 2026-11-03 Happy Ghast 模型（T-504）

- 成年模型：`body` 16³ + 9 条触须（body 子级，长度 5/7/4/5/5/7/8/8/5），整体 `Scale = 4.0`；纹理 `entity/ghast/happy_ghast`（声明 64×64，PNG 128×128）。
- 静态 bind pose；harness/ropes 层与幼年模型（0.2375 scale + inner_body）留待后续。

### 2026-11-03 Phantom 模型（T-505）

- `body`(+`head`)、`tail_base`→`tail_tip`、左右 `wing_base`→`wing_tip`（右翼 mirror）；纹理 `entity/phantom/phantom`，眼睛层 `phantom_eyes`，type id 100。
- 静态 bind pose；扇翅/摆尾动画未移植。沙盒目视正常（蓝色翼膜 + 绿色发光眼）。

### 2026-11-03 Ravager 模型（T-506）

- 结构：neck→head（主体+snout、左右角 rotX 1.0996、mouth）、body（上/下两块板，rotX π/2）、四条 37 高的腿（前腿 UV64,0 / 后腿 UV96,0，左腿 mirror）。
- 纹理 `entity/illager/ravager`（128×128），type id 112；静态 bind pose；行走/攻击/咆哮动画未移植。

### 2026-11-03 部件可见性能力 + Armadillo（T-507/T-508）

- 新增 `EntityModelPart.VisibleWhen`（按 `EntityRenderState` 判定是否发射该部件），mesher 在几何生成前跳过；已记录于 `docs/modules/entity-render.md` §11。
- `EntityRenderState.IsHidingInShell` + `EntityTracker.StateStartTick`：按原版 `ArmadilloState.shouldHideInShell` 的 tick 阈值（IDLE 永不 / ROLLING >5 / SCARED 立即 / UNROLLING <26）计算。
- Armadillo 模型：body（grow0.3 外层 + 内层）、tail、head（head_cube + 左右耳各一层旋转盒）、四腿、cube；norm 显示身体，shell 只显示 cube。纹理 `entity/armadillo/armadillo`，type id 4。
- 静态 bind pose；滚动/探头动画未移植。沙盒 norm/shell 双状态验收通过。

### 2026-11-03 Copper Golem 模型（T-509）

- 结构：body、head（face grow0.015、nose、antenna、antenna_tip）、双臂、双腿，共 9 部件。
- 4 级氧化变体（UNAFFECTED/EXPOSED/WEATHERED/OXIDIZED；`WeatheringCopper.WeatherState`，accessor 18，serializer 38，type id 28），每级 body texture + eye texture（emissive 层）。
- 静态 bind pose；idle/交互动画、天线方块（BlockDecorationLayer）、装备层未移植。沙盒 unaffected/oxidized 目视正常。

### 2026-11-03 Breeze 模型（T-510）

- body/head/eyes/rods（3 根 rods 用原版 pivot 与三轴旋转）；wind_body→wind_bottom→wind_mid(三层)→wind_top(三层)，独立 `breeze_wind`（128×128，Translucent）；eyes 层 `breeze_eyes`（emissive-only，32×32），body `breeze`（32×32）。type id 17。
- 新增部件级 `TextureWidth/Height`（不同 layer 纹理尺寸不同）、`IncludeInEmissive`、`EmissiveOnly`，见 `docs/modules/entity-render.md` §12。
- 静态 bind pose；idle/shoot/slide 动画、wind UV 滚动、风层排序未移植。沙盒目视正常（紫色身体+白色风壳+发光眼）。

### 2026-11-03 Zombie Nautilus 模型（T-511）

- TEMPERATE：复用 Nautilus 部件 + `entity/nautilus/zombie_nautilus`（128×128）；WARM：再追加 corals（yellow/pink/blue/red 共 12 个部件）+ `entity/nautilus/zombie_nautilus_coral`。
- 变体注册表 `zombie_nautilus_variant`（accessor 18，serializer 32，type id 156；内建键 temperate/warm）；静态 bind pose。
- 未移植：游动动画、armor/saddle 装备层；**珊瑚层在原版有 body armor 时会隐藏**，当前始终显示（装备层未做，已登记）。
- 沙盒 warm 目视正常（白壳 + 粉/黄/蓝/红珊瑚）。

### 2026-11-03 Wither 模型（T-512）

- 结构：shoulders、ribcage（主柱 + 3 根肋板，rotX 0.20420352）、tail（pivot y=16.692228,z=1.527873，rotX 0.83252203）、center/right/left 三头。
- 纹理 `entity/wither/wither`（64×64），type id 149；静态 bind pose。
- 未移植：三头/肋骨/尾动画、invulnerable 纹理切换与 WitherArmorLayer。

### 2026-11-03 Wither 静态姿势校正（T-513）

- 修正 root 部件 xRot 符号（应为 `-vanilla`）；此前 ribcage/tail 用了正号。
- 静态姿势固定为 `setupAnim(age=0)` 帧：`ribcage.xRot = -(0.065+0.05)*π = -0.36128316`，tail pivot `y=16.25444, z=3.034748`、`tail.xRot=-(0.265+0.1)*π=-1.14668132`。尾巴仍是原版单个 3×6×3 盒子，只是摆动动画未实现。

### 2026-11-03 Warden 基础几何（T-514，进行中）

- 基础结构：bone→body（左右 ribcage、head→左右 tendril、左右 arm）、左右 leg，共 11 部件；纹理 `entity/warden/warden`（128×128），type id 146。
- **未完成**：原版还有 bioluminescent / pulsating spots（两帧）/ heart / tendrils 四个 `LivingEntityEmissiveLayer`，需要“部件子集 + 每层独立 emissive 纹理”的能力；当前只有单 `EmissiveTexturePath`。动画全部未移植。
- 下一步建议：先扩展 emissive 层为部件可指定 `EmissiveTexturePath`（或独立 layer 定义），再补 Warden 四层与 EnderDragon。

### 2026-11-03 Wither 静态姿势回到 bind pose（T-515）

- 用户澄清：静态姿势只需和**原版模型定义**一致，不必烘焙动画帧。已恢复 `createBodyLayer` 的 PartPose：`ribcage.xRot=-0.20420352`、`tail pivot y=16.692228/z=1.527873`、`tail.xRot=-0.83252203`（负号来自 y-down→y-up 转换）。T-513 的 age=0 烘焙不再采用。

### 2026-11-03 多 emissive 层能力 + Warden 完成（T-516/T-514）

- `EntityModelPart.EmissiveTexturePath`：部件级 emissive 贴图；`EmissiveOnly` 部件只走 emissive pass；`EntityModelDefinition.TexturePaths` 自动收录部件纹理与 emissive 纹理；mesher 在 emissive pass 按部件切换 sprite。
- Warden 完整四层：bioluminescent 5 部件 / pulsating spots 6 部件（固定 spots_1 帧）/ heart 1 / tendrils 2（复用 `warden` 纹理），共 25 部件。纹理 128×128，type id 146；静态 bind pose，动画与 spots 两帧脉冲未移植。
- 已记录于 `docs/modules/entity-render.md` §13。

### 2026-11-03 Ender Dragon 模型（T-517）

- 结构：head（upperlip、upperhead、左右 scale、左右鼻孔、jaw）、5 段 neck、12 段 tail（每段 spine + scale）、body + 3 个背刺、左右翼 base/tip（bone + skin）、四肢各三段（leg→tip→foot）；共 66 部件。
- 纹理 `entity/enderdragon/dragon`（256×256），type id 44；静态 bind pose；飞行/颈部/尾部/翼动画与眼睛层未移植。

### 2026-11-03 Tropical Fish + tint（T-518/T-519）

- 顶点 tint：`EntityModelPart.TintSelector`、`EntityRenderState.Tint/PatternTint`，mesher 写入顶点色；packed variant 通过 `EntityVariantSpec.KeySelector` 解 pattern，DyeColor texture diffuse 解 base/pattern 颜色。
- Tropical Fish：small(5 部件)/large(6 部件) + 同几何 pattern 复制层（12 种 pattern 纹理，`PatternTint`），type id 140，纹理 32×32；静态 bind pose（摆尾/旋转/世界旋转未移植）。
- 沙盒新增 `MC_SANDBOX_TINT` / `MC_SANDBOX_PATTERN_TINT`（十六进制 RRGGBB）用于颜色验收。

### 2026-11-03 幼年模型基础设施 + Pig/Cow/Chicken（T-520/T-521）

- `EntityRenderState.IsBaby` 来自同步 index16（AgeableMob 白名单）；resolver/variant table 支持 baby 选择；Definitions 收录幼年纹理；沙盒 `MC_SANDBOX_BABY=1`。
- 已实现 BabyPigModel（7 件, 32×32）、BabyCowModel（9 件, 64×64）、BabyChickenModel（8 件, 16×16），含 temperate/warm/cold 幼年纹理；静态 bind pose。
- 后续按同一路径继续其余 Baby*（cat/wolf/horse/llama/fox/rabbit/camel/goat/panda/polar bear/squid/turtle/bee/dolphin/axolotl/strider/hoglin/piglin/zombie/villager/sniffer/armadillo/nautilus 等）。

### 2026-11-03 Baby Cat / Wolf / Fox / Rabbit（T-522）

- BabyFelineModel 11 部件（32×32，猫 11 变体含 all_black/ocelot 纹理命名规则）；BabyWolfModel 11 部件（9 变体，tame/angry 幼年纹理）；BabyFoxModel 10 部件（red/snow，sleep 幼年纹理）；BabyRabbitModel 17 部件（默认 brown_baby）。
- 均走 `IsBaby` 选择与 `*_baby` 纹理；静态 bind pose（动画未移植）。**rabbit 的成年/幼年变体键尚未接入**（项目 Rabbit 定义当前只有单几何）。

### 2026-11-03 幼年 Wolf/Rabbit 代码级复核（T-523）

- BabyWolf：几何逐项核对通过；修正 **tame/angry 幼年纹理命名**（应为 `wolf_<variant>_tame_baby` / `wolf_<variant>_angry_baby`，旧实现拼成 `..._baby_tame`）；BabyFox red 补 `fox_sleep_baby` 选择器。
- Rabbit：原项目只有单 `rabbit_brown`，补齐成年 7 变体（brown/white/black/white_splotched/gold/salt/evil；evil 用 `rabbit_caerbannog`），并补幼年 `*_baby` 对应纹理；`EntityVariantSpecs[111]` 用 KeySelector 处理 sparse id 99。
- 逐项 AABB/部件数复核：BabyCat 11、BabyWolf 11、BabyFox 10、BabyRabbit 17；父子/UV/mirror/旋转与原版一致。

### 2026-11-03 Baby Wolf 尾巴方向修正（T-524）

- 现象：小狼尾巴朝前。根因：`tail_r1` 是“负 minY”的尾部盒，通用 child 旋转取反（存储 +3.1）会让尾尖绕到前方；改为 `rotationX: 3.1f`（存储 -3.1）后尾巴朝后，符合原版姿势。
- 仅改 BabyWolf tail_r1；其他部件/UV/父子不变。

### 2026-11-03 Baby Wolf 尾巴方向（T-525，替代 T-524）

- 对照 `WolfModel.setupAnim`：每帧 `tail.xRot = state.tailAngle`；`Wolf.getTailAngle()` 野生默认 `π/5`。静态姿势应使用该默认朝向（存储 `-π/5`），`tail_r1` 保持原版 `-3.1`（存储 +3.1）。
- 此前用 `createBodyLayer` 的 bind `-0.5236`（存储 +0.5236），导致尾巴朝前。已改为 `-MathF.PI/5f`；侧视确认尾巴在**后方**向下。

### 2026-11-03 静态姿势统一策略：bind pose（T-526）

- 用户决定：所有模型的静态姿势以原版 `createBodyLayer` 的 **bind pose** 为准，`setupAnim` 的默认帧/位移等动画系统实现时再统一烘焙。
- 因此撤回 T-525：BabyWolf tail 组恢复 `offsetAndRotation(0,19,3,-0.5236)`（y-up 存储 +0.5236），`tail_r1` 保持原版 -3.1。
- 已知观感差异：狼尾在 bind pose 下与游戏默认动画帧不同（游戏里 `tail.xRot=tailAngle` 且 y/z 有位移），这是“无动画”的预期结果，已记录。
