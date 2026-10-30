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

最后更新：2026-10-30

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
    **下一步具体动作**：O3 —— 剔除 compute（用金字塔的 covering 查询判定 AABB），产出可见列表，再与"不剔除"参考帧逐像素比对。
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

- 无后台任务。15 个提交未推送（等所有者指示）。

## 最近提交

- `9a40a4e` refactor: rename the project to Sharpcraft
- `963b835` feat(client): resolve face textures from blockstates and models
- `512f15d` feat(render): resolve state ids to blocks from the baseline registry layout
- `2c64044` feat(render): sample the sprite atlas so blocks show their real textures
