<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 MinecraftCSharp contributors -->

状态：[草稿]
基线：Minecraft 26.3
证据：`docs/architecture/boundaries.md`；`E:\mcanalysis\26.3\entries.csv`
最后更新：2026-10-28

# 模块：构建骨架（build scaffolding）

## 1. 职责

提供所有后续模块赖以存在的**工程底座**：解决方案结构、程序集划分、编译期约束、测试基础设施、边界自动校验。

负责：目录与程序集布局、构建属性、SDK 版本约束、测试工程组织、架构规则的可执行检查。
不负责：任何游戏逻辑、渲染、网络、认证实现。

## 2. 侧别归属

外围宿主层（工程基础设施）。不属于 Client / Server / Common 任何一侧，且**不得被游戏逻辑引用**（它只被构建系统使用）。

## 3. 程序集划分

依据 `docs/architecture/boundaries.md` 第 7 节的映射，落地为以下工程（目录 `src/` 与 `tests/`）：

| 工程 | 侧别 | 允许引用 | 说明 |
| --- | --- | --- | --- |
| `MinecraftCSharp.Common` | Common | Framework（仅抽象） | 标识符、注册表、数学、NBT、协议编解码、世界数据结构 |
| `MinecraftCSharp.Framework` | 外围（引擎） | 无（只依赖 BCL） | WGPU 抽象、窗口、输入、资源加载、音频后端、性能基建 |
| `MinecraftCSharp.Client` | Client | Common, Framework | 渲染表现、UI、输入、音频播放、多人会话 |
| `MinecraftCSharp.Server` | Server | Common | 世界模拟、刻推进、命令、存档（仅服务原版对齐与集成服务端） |
| `MinecraftCSharp.Auth` | 外围（在线服务） | Framework（存储抽象） | MSA / Xbox Live / XSTS / Minecraft 服务链路 |
| `MinecraftCSharp.Launcher` | 外围（宿主） | Auth, Framework | 版本与资产获取、启动编排、多账号 |
| `MinecraftCSharp.Modding` | 外围（mod API） | Common（契约类型） | 模组 API 契约、加载器、热加载宿主 |

目标框架统一 `net10.0`。

## 4. 依赖规则（构建期强制）

| 编号 | 规则 | 检查方式 |
| --- | --- | --- |
| B1 | `Client` 不得引用 `Server`，`Server` 不得引用 `Client` | 架构测试（反射读程序集引用） |
| B2 | `Common` 不得引用 `Client` / `Server` / `Launcher` / `Auth` | 架构测试 |
| B3 | `Framework` 不得引用任何 `MinecraftCSharp.*` 工程 | 架构测试 |
| B4 | `Common` 的公开 API 中不得出现 `System.Windows` / WGPU 类型 | 架构测试（类型扫描） |
| B5 | 所有源文件首部带 SPDX 头 | 构建期脚本检查（后续加入 CI） |

规则 B1–B3 在本轮即以测试形式生效，任何越界提交会直接失败。

## 5. 构建配置

- `global.json` 固定 SDK 10 系列并允许前滚到最新特性带，避免不同机器编译差异。
- `Directory.Build.props` 统一：`net10.0`、`Nullable=enable`、`ImplicitUsings=enable`、`LangVersion=latest`、`Deterministic=true`、`EnableNETAnalyzers=true`、警告级别 `AnalysisLevel=latest`。
- 不开启 `TreatWarningsAsErrors`（现阶段会与探索性代码冲突），待基础层稳定后再逐工程打开并逐步收紧。
- 不设置中央包管理（`Directory.Packages.props`）：当前测试依赖少，引入正式第三方依赖时再统一（见待决项）。

## 6. 测试基础设施

- `tests/MinecraftCSharp.Common.Tests`：Common 的单元测试（xUnit，由 SDK 模板生成版本，避免手写版本号导致还原失败）。
- `tests/MinecraftCSharp.Architecture.Tests`：架构规则测试，**不依赖第三方库**，直接读程序集 `ReferencedAssemblies` 断言。
- 命名：`<被测类型>Tests`；一个测试类只覆盖一个类型；断言用 xUnit 原生 `Assert`，不引入断言库。

## 7. 非目标（本轮不做）

- 不做 AOT / 单文件 / 裁剪配置（受原则 10 的可替换义务与待定的运行时形态约束）。
- 不做 CI 流水线（先本地绿灯，CI 在首批逻辑稳定后接入）。
- 不做性能基准工程（`benchmarks/` 在渲染与模拟落地后再建）。
- 不做代码签名、安装器、自动更新。

## 8. 验收标准

1. `dotnet build MinecraftCSharp.slnx` 全绿。
2. `dotnet test` 全绿，且架构测试能**实际拦截**越界引用（用一次故意的临时越界验证后删除）。
3. 解决方案内 7 个游戏/宿主工程 + 2 个测试工程，无空壳歧义（每个工程至少有一个带 SPDX 头的占位文件，说明其职责）。
4. `git status` 干净，无资产、jar、反编译产物。

## 9. 待验证 / 待决

- 是否启用中央包管理：引入 WGPU 绑定与登录相关依赖时一并决定。
- 架构测试是否升级为 Roslyn 分析器（可在编辑期报错而不是测试期）：基础层稳定后再评估。
- `Common` 是否需要拆分为 `Common.Core` / `Common.World` / `Common.Network`：等 Common 代码量超过单工程可维护阈值再定。
