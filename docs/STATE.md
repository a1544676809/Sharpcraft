<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 MinecraftCSharp contributors -->

# STATE —— 跨会话进度锚点

> 本文件只记录**易变状态**（AGENTS.md 4.2）。设计结论属于 `docs/modules/`，索引属于 `docs/README.md`，决策属于 `docs/decisions/`。**不要在此复述它们。**
> 每次收工必须与代码同一提交更新本文件，并写清"下一步具体动作"。

最后更新：2026-10-28

## 当前里程碑

M1 —— **基础逻辑**（Common 地基 + 工程防线）。渲染、网络、客户端表现靠后。

## 已完成（可视为稳定）

- 项目宪章 AGENTS.md（11 条原则 + 3.6 工具链 + 4.2 交接）、LGPL-3.0-or-later 落户。
- 文档体系与颗粒度规范；jar 第一轮架构分析（overview / package-map）。
- 三侧边界规则 R1–R6 + 证据（`docs/architecture/boundaries.md`）。
- 工程骨架：7 个 src 工程 + 2 个测试工程，`MinecraftCSharp.slnx` 作为唯一构建与调试入口（分组模板已就位）。
- 构建防线：`TreatWarningsAsErrors=true`；架构守卫 11 项（含 csproj 声明引用检查，已实测可拦截）；热路径机制 3 项（`[HotPath]` + 分配测试，已实测有区分度）。
- 分析工作区迁移至 `../mc-26.3-analysis`；依赖扫描脚本可 `MC_ANALYSIS_ROOT` 覆盖且输出可复现。
- 治理设施（原则 12）：追踪矩阵 `docs/traceability.md`、风险登记册 `docs/risks.md`、ADR（`docs/decisions/`，含 0001）、可测质量属性 `docs/specs/quality-attributes.md`、缺陷模板 `.github/ISSUE_TEMPLATE/bug_report.md`、复现库约定 `docs/repro/README.md`、CI `.github/workflows/ci.yml`。
- 文档漂移门禁已可执行：SPDX 头、索引完整性、断链、绝对路径、STATE 必备章节、矩阵状态词表，共 7 项断言。

## 进行中

- T-005 已完成：`Identifier` + 18 项测试（`src/MinecraftCSharp.Common/Identifiers/`、`tests/.../IdentifierTests.cs`）。
- T-006 已完成：`LegacyRandomSource`、`Xoroshiro128PlusPlus`、`XoroshiroRandomSource`、`SeedSupport`、`RandomSource` 工厂；向量回放 840 值全绿，三项分配测试通过。

## 阻塞项

- 无。四项开工前置 + 注册表顺序均已闭环：
  - `docs/analysis/common/identifier.md`：等值/排序/长度/空串。
  - `docs/analysis/common/random.md`：LCG、xoroshiro128++、种子升级、位置化随机、WorldgenRandom 混合器、RandomSequence。
  - `docs/analysis/common/registry-order.md`：迭代顺序 = 注册顺序；内置表顺序 = 代码顺序；数据包表排序后注册；方块状态全局 id 的四级链。
- 参考向量已取真值并入库（纯数值），生成器与原始输出留在 `ref://mc-26.3-analysis/vectors/`。

## 下一步具体动作

1. M1-a 已完成（T-007 / T-008 / T-010 / T-011）。
2. M1-b 网络协议底座：T-012 编码原语、T-013 握手/状态包与帧缓冲/压缩、T-014 状态查询客户端 + SRV 解析已完成。
3. 规格已落盘：`docs/specs/network/{README,primitives-varint,primitives-strings,protocol-phases}.md`。
4. `fork()` / `consumeCount` 的链式派生向量仍未取；`coordinates.md` 还有两项待验证（ChunkPos 主构造器、超范围饱和）。
5. **链路已打通**：对 `mcf.ddger.top` 实测得到 protocol=777 / version=26.3 / RTT=21ms（证据见 `protocol-phases.md` §8）。SRV 解析已实现。
6. T-015 登录加密原语已完成（服务端哈希 / RSA / AES-CFB8，向量来自 JVM 参考实现）。
7. T-016 登录报文已完成；**真实服务器验证了加密通道**（收到并解密 unverified_username 断开包）。
8. T-017 认证模块（设备码流程）已完成：4 个阶段客户端 + 19 项离线测试；真实登录需人工输入设备码，属手工验收项。
9. T-018 启动器 CLI 已完成：`login` / `probe` / `help`；`probe mcf.ddger.top` 实测通过（protocol 777）。
10. **真实账号登录已验收成功**（2026-10-28，设备码 → MSA → XBL → XSTS → 会话 → 档案）。身份端点踩坑已固化：MSA 应用必须走 `login.live.com` 且用表单编码。
11. T-019 全阶段包 id 表已入库（260 条）并与硬编码常量交叉校验。
12. **真实进服已打通**：`play mcf.ddger.top` 到达 play 阶段（首个包 `minecraft:login`）。令牌缓存于仓库外。
13. T-022 play 阶段最小闭环已完成：JoinGame 解析 + keep-alive/ping 回复，45 秒实测存活（2 次 keep-alive）。
14. T-023 世界存储层与客户端世界骨架已完成（调色板容器 / 区块段 / 区块列 / ClientWorld）。
15. T-024 NBT 读写已完成（网络/存储两种根 + 深度与长度上限）。
16. T-025 完成：`registry_data` 同步 + `level_chunk_with_light` 解析 → `ClientWorld`；实测 8/9 区块、243,819 非空气方块。
17. 区块与光照解析全部通过（9/9 区块、97 个光照数组）。
18. T-026 渲染已完成：绑定切到 `Brew.Wgpu 29.0.0.5`（Silk.NET 原生包的回读是未实现桩，已废弃），`launcher frame` 产出 960x540 PNG，清屏色 140,184,242、方块面颜色正确。
19. 深度缓冲已加（`Depth32Float` + `Less`），相机改为按网格实际包围盒取景；`launcher frame replay <file>` 用已捕获的真实区块包出图（现场网络到 xsts 不可用时仍可验收）。
20. 文档已批准：`docs/modules/render.md` → `[已评审]`，`docs/decisions/0004-window-layer.md` → `[已接受]`；`AGENTS.md` §5 已按授权写入绑定与窗口层定稿结论。
21. **真实世界出图已达成**：`launcher frame mcf.ddger.top` 现场跑通——9/9 区块、275,142 方块、110,868 三角形，输出 3×3 区块的岛屿（草地/沙滩/水体/树木），证明"能画出自己收到的世界"。
22. **窗口呈现已达成**：`launcher window <address|local>` 打开真实窗口（默认停留 8 秒，`MC_WINDOW_SECONDS` 可调），呈现 180 帧（BGRA8UnormSrgb）并落盘同一帧 PNG；现场跑真实服务器时显示 9/9 区块、275,142 方块、110,868 三角形——M2 四步全部有可执行证据。
23. 版本核对结论：`Silk.NET.Core` 实际解析为 **2.23.0**（与 Windowing 同版本）；此前说的"2.22 偏斜"是早期 Silk.NET.WebGPU 试验残留的包目录，不在依赖图里，无需处理。
24. 窗口缺口补齐：投影按 framebuffer 像素算、resize 时交换链/深度/顶点三者一起重建（`MC_WINDOW_SELFRESIZE` 可确定性验证），两套库的接口约定写入 `docs/modules/render.md` §5.1，ADR-0004 追加修订记录。
25. 性能基线套件已落地：`benchmarks/MinecraftCSharp.Benchmarks`（自研计时，无额外依赖），实测 P50/P95 与分配量写入 `benchmarks/results/2026-10-28-baseline.md`，口径见 `docs/specs/performance-baseline.md`。当前最大瓶颈是网格生成（P95 152 ms / 14.8 MB 分配）。
26. 几何路径零分配改造完成：`MeshBuffer`（ArrayPool）+ 遍历已加载区块 + `ClipSpaceTransform` 的 Span 重载。实测网格 P95 132.9→**22.2 ms**、分配 14.8 MB→**0**；变换 P50 1.89→1.47 ms、分配 4 MB→0。等效性由 `ChunkMesherGoldenTests` 的顺序无关指纹锁定。热路径此后一律使用池化重载。
27. 光照与背面剔除已落地：`ChunkLight` 保存 nibble 光照，网格按"面朝向格光照 × 朝向系数（上 1.0 / 侧 0.75 / 下 0.5，下限 0.12）"着色；管线开启 `CullMode.Back` + `FrontFace.CW`（WebGPU 在 framebuffer 空间判定正面，只改 culling 会把可见面剔掉）。实测代价：网格 +13%。未做 AO 与平滑光照。
28. 贪婪合并完成：同段同朝向、状态与光照一致的面合并成矩形；真实世界三角形 110,868→**51,402**（-54%），CPU 网格生成 23.5→35.1 ms（+49%，换 GPU 每帧开销）。过程中修掉两个只有渲染才暴露的 bug：掩码需每切片重置、四边形坐标漏加段基址（表现为石层浮在地形上方）。绕序改为按 `Cross(U,V)==外法线` 推导的显式表，管线 `FrontFace` 相应改成 CCW。
29. 待你确认：`quality-attributes.md` QA-1 的阈值仍为 `[待定]`，建议值在 `performance-baseline.md` §5；确认后我把数值转成回归判定线。
30. 下一步（M3 候选）：① 光照数组→顶点亮度；② 背面剔除与按区块合批；③ 帧循环接入输入与 resize；④ 跨平台窗口验收（R-011）。
## 未提交改动 / 后台任务

- 本次提交内容见下；无运行中的后台任务。

## 最近提交

- `725d1d6` feat(network): protocol codec primitives and network spec (M1-b start)
- 本次提交：T-013 握手/状态包 + 帧缓冲 + 压缩
