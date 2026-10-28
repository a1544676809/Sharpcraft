<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 MinecraftCSharp contributors -->

# STATE —— 跨会话进度锚点

> 本文件只记录**易变状态**（AGENTS.md 4.2）。设计结论属于 `docs/modules/`，里程碑划分属于 `docs/milestones/roadmap.md`，索引属于 `docs/README.md`，决策属于 `docs/decisions/`。**不要在此复述它们。**
> 每次收工必须与代码同一提交更新本文件，并写清"下一步具体动作"。

最后更新：2026-10-28

## 当前里程碑

**M3 —— 资产管线 + 启动器闭环**（出口条件见 `docs/milestones/roadmap.md`）。
M1（基础逻辑）与 M2（世界数据闭环）已达成并留有可执行证据。

## 进行中

- **T-036 完成**：多账号与离线闸门（`accounts` 系列与 `launch --offline`）。闸门关闭路径已实机验证（两条命令均退出码 4）；客户端侧已验证离线启动与 UUID 派生（`Notch` → `b50ad385-…`、`jeb_` → `a762f560-…`，参数中无任何令牌）。闸门放行后的实机验证待一次成功的正版登录（`xsts` 仍在 RST，见 R-017）；未伪造任何正版账号记录。
- **T-035 完成**：`launch <jar> <version-json>` 串起校验 → 打包 → 挂载 → 认证 → 装配 → 启动；令牌走会话文件不上命令行；客户端独立可执行并校验交接（退出码 0/2/3）。**复跑结果**：21:20 曾完整登录成功（`Blue_Coast`），随后该端点开始返回 RST；同一时刻 `curl` 与独立探测程序可达，故判定为链路层间歇故障（R-017）。已加有界重试、IPv4 竞速连接、显式 HTTP/1.1 与 `MC_TRANSPORT_DEBUG=1` 诊断；完整 `launch` 待网络稳定后复跑。
- **T-034 完成**：有序来源栈（内置兜底 → 官方索引 → 基础 pack → 资源包/模组），真实安装自检通过——纹理与模型来自 pack（stone 151 字节、model 98 字节），`lang/zh_cn` 562,928 字节来自官方索引 34，`.mcmeta` 兄弟规则生效，列举纹理 3,964 / 语言 144。
- **M3 文档阶段完成**：`docs/modules/assets.md` `[已评审]`、ADR-0005 `[已接受]`（两项裁决：索引只读+缺失警告、pack 保留原始层级）、`docs/analysis/assets/jar-entry-map.md` 已补实测证据。可以开始编码。
- 实测事实：本机 26.2 用资产索引 **32**（5,057 条 / 约 458 MB），索引内含 `sounds/`、`sounds.json`、全部 `lang/*.json`，**不含** `textures/block/`——印证"音频与语言走索引、纹理与模型打包"。

## 已完成（近期，按时间倒序）

1. T-031 贪婪合并：真实世界三角形 110,868 → **51,402**（-54%）；修掉两个只有出图才暴露的 bug（掩码需每切片重置、四边形坐标漏加段基址），绕序改为按 `Cross(U,V)==外法线` 推导的显式表，管线 `FrontFace` 改为 CCW。
2. T-030 光照→顶点亮度 + 背面剔除：nibble 光照存进 `ChunkLight`，面亮度 = 采样格光照 × 朝向系数（上 1.0/侧 0.75/下 0.5，下限 0.12）；管线 `CullMode.Back`。代价 +13%。
3. T-029 几何路径零分配：`MeshBuffer`（ArrayPool）+ 遍历已加载区块 + `ClipSpaceTransform` 的 Span 重载；P95 132.9 → **22.2 ms**、分配 14.8 MB → **0**。
4. T-028 性能基线套件：`benchmarks/MinecraftCSharp.Benchmarks`，读数入 `benchmarks/results/`，口径见 `docs/specs/performance-baseline.md`。
5. T-026 / T-027 渲染与窗口：`Brew.Wgpu` 绑定 + `Silk.NET.Windowing`（ADR-0003 / ADR-0004 已接受）；`launcher frame|window` 可对真实服务器出图与呈现。
6. T-025 区块与光照解析：真实会话 9/9 区块、275,141 非空气方块、97 个光照数组。

## 阻塞项

- 无硬阻塞。
- **待你提供**：26.3 客户端 jar 的本地路径（原则 3：只在你本地、不进仓库）。缺它只影响"真实提取验收"，不影响 T-032/T-033 的编码与合成 jar 测试。

## 下一步具体动作

1. T-032 **已完成**：`VersionManifest`（id / assetIndex.id+sha1 / downloads.client.sha1）+ `JarVerifier`（流式 SHA-1、尺寸预检、篡改不可绕过、报错含期望与实际哈希）。
2. T-033 **已完成**：`launcher assets <jar> <version-json>` 产出单文件 pack。真实 26.3 jar 实测 11,533 条目 / 5.6 MB / 768 ms，复用 164 ms；跳过 net 11,267、data 10,449、com 690、META-INF 5。
3. T-037 **进行中**：PNG 解码器已完成（1/2/4/8/16 位、索引调色板、五种过滤器），真实资产已解码；下一步是图集打包与 GPU 采样（顶点加 UV、管线加采样器）。
4. T-037：纹理接入渲染（图集、UV、采样器、着色器采样）——画面质变的一步。
5. 待网络恢复：复跑 `launch` 的完整链路（含真实认证与客户端进程启动）。
6. 可选并行：把 `quality-attributes.md` QA-1 的 `[待定]` 阈值定稿（候选值见 `performance-baseline.md` §5）。

## 未提交改动 / 后台任务

- 无后台任务。

## 最近提交

- `6bcfea6` perf(mesh): greedy merge coplanar faces into single quads
- `e70be53` feat(render): light-sampled vertex shading and GPU back-face culling
- `80c4bd3` perf: add a reproducible baseline suite for the world pipeline
