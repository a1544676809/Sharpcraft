<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

<!-- doc
title: 资源包栈（多包优先级与重载）
summary: 包列表顺序→按命名空间回退栈；后者覆盖前者；重载为两阶段原子替换
status: 部分核对（未核对项已列出）
updated: 2026-10-30
-->

# 资源包栈：优先级与重载

证据指针（均为 `ref://mc-26.3-analysis/src/` 下）：

- `net/minecraft/server/packs/resources/MultiPackResourceManager.java`
- `net/minecraft/server/packs/resources/FallbackResourceManager.java`
- `net/minecraft/server/packs/repository/PackRepository.java`
- `net/minecraft/server/packs/repository/Pack.java`
- `net/minecraft/server/packs/metadata/pack/PackMetadataSection.java`
- `net/minecraft/server/packs/resources/ReloadableResourceManager.java`
- `net/minecraft/client/resources/ClientPackSource.java`

## 1. 优先级（已核对）

`MultiPackResourceManager` 构造时按 **包列表顺序** 依次 `push` 到"每命名空间一个"的 `FallbackResourceManager`；
`FallbackResourceManager.getResource` 从 `fallbacks` **末尾向前**查找（`for (int i = size-1; i >= 0; i--)`）。

⇒ **列表中越靠后的包优先级越高**，同名资源被后面的包覆盖。

## 2. 包列表如何形成（部分核对）

- `PackRepository.selected` 是**有序列表**；`addPack(id)` 追加到末尾 ⇒ 新加入的包获得更高优先级。
- 必需包（`Pack.isRequired()`）由 `getDefaultPosition().insert(list, pack, …)` 按 `DefaultPosition`（`TOP`/`BOTTOM`）插入，
  而不是简单追加；`insert(..., reverse)` 决定同位置多包时的先后。
- 每包可有 `filter` 段（`ResourceFilterSection`）：按路径/命名空间过滤；只过滤不提供内容的包走
  `FallbackResourceManager.pushFilterOnly`（**不参与资源提供，只遮蔽路径**）。

## 3. 包元数据（已核对）

`pack.mcmeta` 的 `pack` 段 = `description` + **支持的格式区间**（`PackFormat.packCodec(packType)`），
并区分 `CLIENT_RESOURCES` / `SERVER_DATA` 两种包类型；缺 `pack` 段时使用 `FALLBACK_CODEC`（描述空、格式上限极大）。

## 4. 重载（已核对入口）

`ReloadableResourceManager.createReload(resourcePacks, backgroundExecutor, mainThreadExecutor, initialTask)`
→ `SimpleReloadInstance.create(...)`，监听器类型为 `PreparableReloadListener`（两阶段：准备 / 应用）。
`createReload` 会记录本次重载的包列表（`Reloading ResourceManager: …`）。

## 5. 未核对（不得据此实现）

| 项 | 说明 |
| --- | --- |
| 客户端包来源的注册顺序 | 内置（vanilla）包、`resourcepacks/` 目录包、服务器下发包各自加入 `PackRepository` 的**顺序与默认位置**尚未逐行核对 |
| `overlays`（包内目录叠加） | `pack.mcmeta` 的 overlays 段与目录叠加规则未核对 |
| `SimpleReloadInstance` 阶段细节 | 并发度、失败回滚（是否原子）与监听器依赖排序未核对 |
| `KnownPacksManager`（服务端已知包） | 与客户端资源包栈的交互未核对 |
| 包类型格式号 | `PackFormat` 在客户端资源与服务器数据两侧的具体取值范围未核对 |

## 6. 与本项目的接口含义（供 ADR 讨论，不含实现）

- 我们当前只有一个"基础包 zip"（`AssetStack` + `ZipAssetSource`），**没有多包栈**，也没有重载。
- 要做到"兼容任意第三方资源包"，需要：包发现（目录/zip）→ 有序栈（后者覆盖前者）→ 按命名空间回退查找 → 两阶段重载。
- 与原则 2（模组热加载）同属"运行时资源替换"，两者共享同一套"原子替换 + 旧实例可回收"的要求。
