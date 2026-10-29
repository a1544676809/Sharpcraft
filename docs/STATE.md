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

最后更新：2026-10-29

## 当前里程碑

**M3 —— 资产管线 + 启动器闭环**（出口条件见 `docs/milestones/roadmap.md`）。代码侧 8 项全部落地；出口验收的离线部分已复现，联网部分（真实认证 + 客户端进程启动）仍待外部网络条件满足（R-017）。
M1（基础逻辑）与 M2（世界数据闭环）已达成并留有可执行证据。

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

- 无后台任务。

## 最近提交

- `9a40a4e` refactor: rename the project to Sharpcraft
- `963b835` feat(client): resolve face textures from blockstates and models
- `512f15d` feat(render): resolve state ids to blocks from the baseline registry layout
- `2c64044` feat(render): sample the sprite atlas so blocks show their real textures
