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

## 2.1 包形态：目录与压缩包（已核对）

`FolderRepositorySource.discoverPacks` 遍历 `resourcepacks/` 下每个条目，由 `PackDetector` 判定：

| 条目 | 处理 |
| --- | --- |
| **目录** | `PathPackResources`（直接读文件系统） |
| **zip 文件** | `FilePackResources`（仅默认文件系统或 `LinkFileSystem`；其他 FS 记 `Can't open pack archive` 并拒绝） |
| 其他 | 记 `Found non-pack entry, ignoring` |
| 可疑符号链接 | `DirectoryValidator` 记 `ForbiddenSymlinkInfo` 并**忽略该包** |

`PathPackResources.getResource(type, location)` 解析为 `<root>/<assets|data>/<namespace>/<path>`；`getNamespaces` 枚举该根下的目录名并校验命名空间合法。
发现的包使用固定配置 `PackSelectionConfig(required=false, Position.TOP, fixedPosition=false)`。

## 3. 包元数据（已核对）

`pack.mcmeta` 的 `pack` 段 = `description` + **支持的格式区间**（`PackFormat.packCodec(packType)`），
并区分 `CLIENT_RESOURCES` / `SERVER_DATA` 两种包类型；缺 `pack` 段时使用 `FALLBACK_CODEC`（描述空、格式上限极大）。

## 4. 重载（已核对入口）

`ReloadableResourceManager.createReload(resourcePacks, backgroundExecutor, mainThreadExecutor, initialTask)`
→ `SimpleReloadInstance.create(...)`，监听器类型为 `PreparableReloadListener`（两阶段：准备 / 应用）。
`createReload` 会记录本次重载的包列表（`Reloading ResourceManager: …`）。

## 4.1 位置插入的精确语义（已核对）

`Pack.DefaultPosition.insert(list, value, converter, reverse)`：
- `BOTTOM`：从前往后跳过"固定位置且同为 BOTTOM"的条目，插入其后 ⇒ 位于列表**前部**；
- `TOP`：从后往前跳过"固定位置且同为 TOP"的条目，插入其后 ⇒ 位于列表**后部**。

由于查找从列表末尾向前，**TOP = 最高优先级，BOTTOM = 最低**。客户端来源：

| 来源 | 配置 | 结论 |
| --- | --- | --- |
| `vanilla` 内置包 | `required=true, BOTTOM, fixed=true` | 固定在**最低**优先级 |
| 其他内置包 | `required=false, TOP, fixed=false` | 位于固定包之后 |
| `resourcepacks/` 发现的包 | `required=false, TOP, fixed=false` | 同上；同组内**后加入者优先** |

⇒ 玩家安装的包覆盖内置包与 vanilla，符合直觉。

## 6. 包内目录叠加（overlays，已核对元数据部分）

`pack.mcmeta` 的 `overlays` 段（`OverlayMetadataSection`）：

```json
{ "overlays": { "entries": [ { "format": <格式区间>, "overlay": "<目录名>" } ] } }
```

- `entries[].overlay` 必须是 `[-_a-zA-Z0-9.]+` 的目录名，否则报 `is not accepted directory name`；
- `overlaysForVersion(currentPackVersion)` 只保留**格式区间覆盖当前包格式**的条目；`Pack.Metadata.overlays` 就是这个列表；
- 即：同一包里可以按 Minecraft 版本放多套资源，只有匹配当前版本的那几套生效。

**仍待核对**：这些 overlay 目录在读取时如何叠在包根之上（哪个类负责合并、叠加顺序）。本仓库不做猜测。

## 7. 重载失败语义（已核对入口）

`Minecraft.reloadResourcePacks`：`resourcePackRepository.reload()` → `openAllSelected()` → `resourceManager.createReload(...)`，
用 `LoadingOverlay` 展示进度；完成回调用 `Util.ifElse(…成功…, …失败…)`，失败走 `abortResourcePackRecovery()`
（下载包场景）或仅弹 `resourcePack.load_fail` 提示。

⇒ 一次重载要么整体成功应用、要么**整体失败并保持/放弃旧状态**，不存在"应用了一半"的中间态。
（监听器内部仍是两阶段：准备 → 应用，见 `PreparableReloadListener`。）

## 5. 未核对（不得据此实现）

| 项 | 说明 |
| --- | --- |
| `overlay` 目录的叠加实现 | 哪个类把 overlay 目录叠在包根之上、叠加顺序 |
| `SimpleReloadInstance` 并发细节 | 并发度与监听器依赖排序（失败语义已核对）|
| `KnownPacksManager`（服务端已知包） | 与客户端资源包栈的交互未核对 |
| 包类型格式号 | `PackFormat` 在客户端资源与服务器数据两侧的具体取值范围未核对 |

## 6. 与本项目的接口含义（供 ADR 讨论，不含实现）

- 我们当前只有一个"基础包 zip"（`AssetStack` + `ZipAssetSource`），**没有多包栈**，也没有重载。
- 要做到"兼容任意第三方资源包"，需要：包发现（目录/zip）→ 有序栈（后者覆盖前者）→ 按命名空间回退查找 → 两阶段重载。
- 与原则 2（模组热加载）同属"运行时资源替换"，两者共享同一套"原子替换 + 旧实例可回收"的要求。
