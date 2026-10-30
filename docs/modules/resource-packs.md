<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

状态：[已评审]（所有者 2026-10-30 确认：范围按草案；A1–A11 全部通过后整模块一次推送）
基线：Minecraft 26.3
证据：`docs/analysis/assets/resource-pack-stack.md`（§1–§12，均带 `ref://` 指针）、`docs/decisions/0015`、`docs/decisions/0016`、`docs/specs/deviations.md` D-001
最后更新：2026-10-30

# 资源包（Resource Packs）

让玩家安装的第三方资源包按原版语义参与资源解析，并支持运行期重载；**默认行为与原版一致**。

## 1. 职责范围

**负责（本模块完成判据的组成）**

1. **包发现**：`resourcepacks/` 下的目录包与 zip 包；跳过可疑链接、非包条目、坏 zip、自定义文件系统上的 zip。
2. **包元数据**：`pack.mcmeta` 的 `pack`（描述 + 支持格式区间）、`filter`（遮蔽）、`overlays`（按版本叠加）。
3. **有序栈**：列表靠后者覆盖靠前者；`filter` 命中即向下遮蔽；命名空间为并集。
4. **统一管理器**：会话级唯一 `ResourceManager`，所有加载器共用一次发现；支持按原始路径读取（`.mcmeta` 等）。
5. **格式兼容**：按 `pack_format` 区间判定包是否适用于当前基线；不适用则**不加载并给出诊断**（不静默）。
6. **两阶段重载**：先建新栈后换引用；失败保留旧栈；监听器按固定顺序（语言/字体 → 贴图集 → UI）。
7. **文件变更热加载（开关）**：`MC_RESOURCE_PACK_WATCH=1` / `--watch-resource-packs`，300ms 去抖，目录包逐文件、zip 包整包；默认**关闭**。
8. **回收**：旧栈及其纹理句柄可释放；重载 N 次后旧实例可被 GC（自动化验证）。
9. **诊断**：包被跳过、格式不适用、重载失败都有一条可读日志。

**明确不负责（不进本模块）**

- 服务器下发资源包与 `KnownPacks` 握手（范围外，见分析文档 §11）。
- 资源包的下载、校验签名、加密、CDN 与断点续传。
- 资源包选择/排序 UI、启动器里的拖拽排序界面。
- 官方资产索引的补全下载（属 `assets` 模块与 ADR-0005 待决项）。
- 单个资源的增量替换（只做全快照重建）。

## 2. 侧别归属

- 读取与解析：**外围宿主层**（与 `assets` 模块同层，游戏逻辑不读 jar 也不读包）。
- 名字与语义对齐原版（`ResourceManager` / `FallbackResourceManager`），但实现独立。

## 3. 依赖关系

| 方向 | 对象 | 说明 |
| --- | --- | --- |
| 依赖 | `Sharpcraft.Client.Assets.AssetStack` | 已有的有序源栈（逆序查找）|
| 依赖 | `System.IO.Compression` | zip 包 |
| 依赖 | `System.IO.FileSystemWatcher` | 目录包监听（开关打开时）|
| 被依赖 | UI（字体、贴图集）、本地化（语言表）| 都经统一管理器读取 |

## 4. 验收判据（每项须有证据）

| # | 判据 | 证据形式 |
| --- | --- | --- |
| A1 | 目录包与 zip 包都能覆盖基础包 | 单测 + 端到端截图（已达成：红色边框环）|
| A2 | 优先级：后加入者胜；关闭时与原版一致 | 单测 |
| A3 | `filter` 命中即遮蔽下层包 | 单测（已达成）|
| A4 | `overlays` 按版本区间叠加，元数据只取 primary | ✅ `ResourcePackOverlayTests`（4 项）|
| A5 | `pack_format` 不适用的包被跳过并记诊断 | ✅ `ResourcePackFormatTests`（5 项）|
| A6 | 原始路径读取（`.mcmeta`）与贴图来自同一个包 | 单测（已达成）|
| A7 | 重载：换栈生效、失败保留旧栈、监听器按序 | 单测（已达成）|
| A8 | 重载后画面确实变化（改贴图 → 下一帧生效）| 端到端截图 |
| A9 | 热加载开关：关闭时不创建监听线程；开启时改文件生效；去抖只触发一次 | 单测 + 端到端 |
| A10 | 重载 N 次后旧 `ResourcePack`/纹理句柄可回收 | 自动化回收测试 |
| A11 | 全量测试绿 + 文档/矩阵/STATE 同提交 | 命令输出 |

## 5. 推送边界

- **本模块"完成"的定义**：A1–A11 全部有证据、矩阵各行状态为 `已完成`、`STATE.md` 更新、`dotnet test` 全绿。
- **推送时机**：达成上述条件后**整模块一次性推送**，不中途推送半成品（遵循所有者"完成一整个模块再推送"的规矩）。
- 当前进度：A1/A3/A4/A5/A6/A7 已达成；A2 部分；A8/A9/A10 未达成。

## 6. 未决

- **`pack_format` 取值**：客户端格式 = **97.1**（26.3 正式版，Minecraft Wiki `Template:Resource_pack_format`；
  本机目录名里的 snapshot 8 是升级后未改名，不可据此推断）。三种声明写法都解析；不匹配的包跳过并诊断；
  `MC_PACK_FORMAT` 可覆盖。详见 `docs/analysis/assets/resource-pack-format.md`。
- 热加载是否需要"仅在开发构建可用"之外的产品化开关（当前只做环境变量 + 命令行）。
