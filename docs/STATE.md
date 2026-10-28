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

**M3 —— 资产管线 + 启动器闭环**（出口条件见 `docs/milestones/roadmap.md`）。代码侧 8 项全部落地；出口验收的离线部分已在本机复现，联网部分（真实认证 + 客户端进程启动）待 R-017 恢复。
M1（基础逻辑）与 M2（世界数据闭环）已达成并留有可执行证据。

## 进行中

- **T-037 完成**：纹理通路端到端（PNG 解码 → 图集 → UV → 着色器采样 → `blockstates`/`models` 按面取纹理）。2026-10-29 本机复验：官方 26.3 jar（41,483,720 字节 / SHA-1 `e877b6a0…`）→ pack 11,533 条目 / 5,854,134 字节 / 681 ms → 图集 1024×1024 / 1301 精灵 → `frame local` 出带纹理帧，石面像素 `rgba(143,143,143)` 与图集一致。
- **26.3 资产已就位（仓库外）**：`../mc-26.3-analysis/artifacts/26.3/{client.jar,26.3.json}`，SHA-1 与官方清单一致，assetIndex = 34。仓库内仍无任何资产，`git status` 干净。
- **M3 出口验收**：离线部分（jar 校验 → pack → 解析栈 → 图集 → 出图）已复现；联网部分（`launch` 全链路 + 客户端进程启动）待网络恢复后复跑（R-017）。
- **文档校准（2026-10-29）**：STATE / traceability / docs README / modules README / render.md / assets.md 与实现对齐；去掉 `Auth → Framework` 死引用并补架构门禁；帧输出改名 `sharpcraft-frame.png`。
- **T-043 完成（2026-10-29）**：`tools/Sharpcraft.Docs.Indexer` 接管 11 个目录 README 的索引段；`--check` 与 `--check-coverage` 进 CI 与架构测试。覆盖单位是**源码目录模块**（26 个），基线 `docs/src/.coverage-baseline.txt` 只允许缩小。

## 已完成（近期，按时间倒序）

1. T-040 模型驱动取纹理：`blockstates` → 模型 `parent` 链 → `#变量`，真实覆盖率 682 → **1019/1286（79%）**。
2. T-039 基线 `BlockStateLayout`：1,286 方块 / 35,723 状态，真实 state id 可解析为方块名。
3. T-037 纹理通路：图集拼装 + 顶点格式 12/13 float + 着色器采样；修掉"顶点颜色乘状态哈希色"与"UV 未做半纹素内缩"两个只有出图才暴露的 bug。
4. T-036 多账号与离线闸门（原则 7.1）：闸门关闭路径实机验证；未伪造任何正版账号记录。
5. T-035 `launch` 串起校验 → 打包 → 挂载 → 认证 → 装配 → 启动；令牌走会话文件不上命令行。
6. T-031 贪婪合并：真实世界三角形 110,868 → 51,402（-54%）。

## 阻塞项

- 无硬阻塞。
- **R-017**：XSTS 端点间歇 RST，`launch` 的联网验收待复跑（已加有界重试、IPv4 竞速、显式 HTTP/1.1、`MC_TRANSPORT_DEBUG=1`）。
- 本机未安装官方 `assets/objects`，语言/音频走索引的探针只能跳过（只警告，不阻断）。

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
