<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

# Sharpcraft

用 **.NET 10** 重新实现 Minecraft 客户端，行为基线为 **Minecraft 26.3**（协议 777）。不是移植补丁、不是 Fork、也不是反编译产物的再编译。

- 项目宪章（最高约束）：[`AGENTS.md`](AGENTS.md)
- 文档总索引：[`docs/README.md`](docs/README.md)
- 当前进度与下一步：[`docs/STATE.md`](docs/STATE.md)
- 里程碑与出口条件：[`docs/milestones/roadmap.md`](docs/milestones/roadmap.md)
- 追踪矩阵（规格 ↔ 测试 ↔ 代码）：[`docs/traceability.md`](docs/traceability.md)

## 现在能做什么

| 能力 | 状态 |
| --- | --- |
| 真实服务器握手 / 登录 / 配置阶段 / 进入 play | 已验（26.3 基线） |
| 区块与光照解析 → `ClientWorld` | 已验（真实会话 9/9 区块、275,141 非空气方块） |
| WGPU 渲染（窗口 + 离屏出图） | 已验 |
| 资产管线：jar 校验 → 单文件 pack → 资源栈 → 图集 → 纹理采样 | 已验（图集 1024² / 1301 精灵） |
| 方块状态 → 纹理（`blockstates` + `models` 解析） | 1019/1286 方块有纹理（79%） |
| 多账号与离线登录闸门（原则 7.1） | 已实现，闸门关闭路径已验 |

尚未实现：输入与移动、实体、聊天、流体与特殊方块渲染（详见 `docs/STATE.md`）。

## 环境要求

- **.NET 10 SDK**（`global.json` 固定为 `10.0.100`，`rollForward: latestFeature`）
- Windows 上首次构建需要还原 NuGet 包：`Brew.Wgpu`（含 wgpu-native）、`Silk.NET.Windowing`、`DnsClient`
- 运行需要**你自己的** Minecraft 26.3 客户端 jar（本仓库不含任何 Minecraft 资产，见下方法律声明）

```bash
dotnet build Sharpcraft.slnx
dotnet test  Sharpcraft.slnx
```

## 快速上手（需要你有 26.3 客户端 jar）

```bash
# 1) 校验 jar 并打包资产（输出在仓库外，默认 <userData>/Sharpcraft/packs）
dotnet run --project src/Sharpcraft.Launcher -- assets <26.3.jar> <26.3.json>

# 2) 用本地合成世界出一张带纹理的截图（不联网、不认证）
MC_FRAME_LOCAL=1 dotnet run --project src/Sharpcraft.Launcher -- frame local

# 3) 从真实服务器抓区块并出图（需要正版账号）
dotnet run --project src/Sharpcraft.Launcher -- frame <address>
```

常用诊断开关：`MC_TRANSPORT_DEBUG=1`（认证/传输异常链）、`MC_DEBUG_SPRITES=1`（精灵映射与覆盖率）、`MC_FRAME_REPLAY=<packet>`（用抓包重放）。
详见 `docs/modules/render.md` §6.1 与 `docs/modules/launcher.md`。

## 目录结构

```
src/Sharpcraft.Common/      跨侧共享：协议、注册表、世界数据、数学
src/Sharpcraft.Client/      客户端：渲染、资源栈与图集、客户端世界
src/Sharpcraft.Server/      服务端侧（集成服务端，当前为空壳）
src/Sharpcraft.Auth/        MSA / Xbox Live / XSTS / Minecraft 服务认证
src/Sharpcraft.Launcher/    独立启动器：认证、资产管线、启动编排
src/Sharpcraft.Framework/   引擎层：WGPU、窗口、相机（不引用任何 MinecraftCSharp.* 程序集）
src/Sharpcraft.Modding/     模组 API 契约与热加载宿主（预留）
docs/                       分析与规格、模块设计、ADR、里程碑、状态锚点
tests/                      单元、架构（边界/文档漂移）与分配测试
benchmarks/                 性能基线套件与结果记录
```

## 法律与合规声明（重要）

- 本仓库**不包含任何 Minecraft 资产**（纹理、音效、语言文件、模型、jar 或任何反编译产物），也**不会**包含。
- 资产只能由你在本地用启动器从**你自备的**官方 jar 中提取到**仓库之外**的目录；提取产物被 `.gitignore` 排除。
- 许可证 **LGPL-3.0-or-later**（见 [`LICENSE`](LICENSE)）只覆盖本项目代码，**不构成对 Minecraft 任何内容的授权**。
- 本项目与 Mojang / Microsoft 无隶属或背书关系；"Minecraft" 是其各自权利人的商标。

## 参与贡献

提交前请先读 [`AGENTS.md`](AGENTS.md)：文档先行、规格即矩阵、测试列非空才算完成、热路径零分配、提交内同步 `docs/STATE.md`。
新增文档必须登记进 `docs/README.md`，否则文档漂移检查会让构建失败。
