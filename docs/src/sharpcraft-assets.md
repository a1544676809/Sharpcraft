<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

<!-- doc
title: Sharpcraft.Assets —— 资源包、格式与 IO
summary: 底层资源包栈、pack 格式、索引对象与只读来源；只依赖 Sharpcraft.Common
status: 已实现
side: Common
updated: 2026-11-06
-->

# Sharpcraft.Assets —— 资源包、格式与 IO

把"资源从哪来、按什么优先级取"从渲染里彻底剥出来，成为只依赖 `Sharpcraft.Common` 的低层库。
渲染侧（图集、方块贴图解析、字体）仍留在 `Sharpcraft.Client/Assets`（ADR-0007、ADR-0027）。

## 1. 职责范围

负责：

- 资源路径规则（`ResourcePath` / `ResourceKind` / `ResourceLookup`）：`assets/<namespace>/<kind>/<path>.<ext>` 的拼装与反解。
- 有序资源栈（`AssetStack` / `IAssetSource`）：后压入者优先、逆序扫描、缺失策略（告警/抛错）、`filter` 屏蔽语义。
- 资源包（`ResourcePack` / `ResourcePackStack` / `ResourcePackSource`）：目录或 zip、`pack.mcmeta`、overlay、格式范围。
- 包发现与格式（`ResourcePackDiscovery` / `ResourcePackFormats` / `PackFormatVersion` / `ResourcePackOverlays`）。
- 来源实现（`AssetSources`）：`ZipAssetSource`、`DirectoryAssetSource`、`IndexedAssetSource`、`BuiltInFallbackSource`。
- 会话级 `ResourceManager`：单一资源管理器 + 重载监听。

明确不负责：

- 不读 jar、不校验 jar（启动器资产管线，原则 3）。
- 不做图集拼接、模型解析、字体度量（渲染侧，`Sharpcraft.Client/Assets`）。
- 不做窗口/GPU（`Sharpcraft.Framework`）。

## 2. 侧别归属

- 归属：外围共享的资产 IO 层；**不是** Client/Server 游戏逻辑，也不属于宿主。
- 依赖方向：只依赖 `Sharpcraft.Common`；**不得**引用 `Framework` / `Client` / `Launcher`（架构测试守护）。
- `Sharpcraft.Client` 与 `Sharpcraft.Launcher` 均可引用它，从而让启动器的打包/自检无需装载游戏程序集。

## 3. 关键类型

| 类型 | 作用 |
| --- | --- |
| `AssetStack` | 有序来源列表；`Read(kind, id)`、`ReadMetadata`、`ListAll`、`OpenFile` |
| `IAssetSource` | `TryOpen(kind, id)`、`List(prefix)`、`TryOpenPath`、`Blocks` |
| `ResourcePack` | 目录或 zip 的一只包；`pack.mcmeta`、filter、overlay、格式范围 |
| `IndexedAssetSource` | 参考等价 `IndexedAssetSource.createIndexFs`：索引逻辑路径 → `objects/<h2>/<sha1>` 只读 |
| `ResourceManager` | 会话唯一资源管理器；`AddListener` 定义重载顺序 |

## 4. 索引叠加（reference-equivalent）

`IndexedAssetSource.Open(indexPath, expectedIndexId)`：

- 解析 `objects` 映射（`path -> { hash, size }`），逻辑路径相对客户端资源根。
- 读取时拼 `objects/<hash 前两位>/<hash>`，`File.OpenRead` 只读，**不复制**。
- `TryOpenPath` 支持 `pack.mcmeta`、`sounds.json`、`icons/*` 等非 kind 路径（去掉 `assets/` 前缀即索引键）。
- 缺失对象只计数并返回 false，不抛异常；由上层（内置兜底/jar 包）回答。

## 5. 生命周期与线程

- 栈在会话启动时构建一次；`ResourceManager.Reload` 先构建新栈再整体替换，失败保留旧栈。
- 包/索引句柄随 `ResourceManager.Dispose` 释放；只读，不写盘。

## 6. 与原版对应

| 本仓实现 | 26.3 原版 |
| --- | --- |
| `AssetStack` / `ResourceManager` | `MultiPackResourceManager` / `ReloadableResourceManager` |
| `ResourcePack` / `ResourcePackStack` | `Pack` / `ResourcePackStack` |
| `IndexedAssetSource` | `IndexedAssetSource.createIndexFs` |
| `ResourceFilter` / `ResourcePackOverlays` | `pack.mcmeta` 的 `filter` / `overlays` |

<!-- BEGIN GENERATED: class-map -->

| Project | Sharpcraft type | Kind | Status | Reference class | Reference path | Note |
| --- | --- | --- | --- | --- | --- | --- |
| Sharpcraft.Assets | Sharpcraft.Assets.IndexedAssetSource | Mirror | Partial | net.minecraft.server.packs.resources.IndexedAssetSource | ref://mc-26.3-analysis/src/net/minecraft/server/packs/resources/IndexedAssetSource.java | createIndexFs equivalent: index maps logical path -> objects/<h2>/<sha1>; read-only overlay |
| Sharpcraft.Assets | Sharpcraft.Assets.ResourceManager | Mirror | Partial | net.minecraft.server.packs.resources.ResourceManager | ref://mc-26.3-analysis/src/net/minecraft/server/packs/resources/ResourceManager.java | auto-resolved by name; verify |

<!-- END GENERATED: class-map -->
