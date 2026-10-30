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

## 2.2 filter 的匹配语义（已核对）

`pack.mcmeta` 的 `filter.block[]` 每项是 `{ namespace?: 正则, path?: 正则 }`（`IdentifierPattern`）：
- 缺省的字段**恒真**（`Optional.empty()` → `r -> true`），所以 `{}` 遮蔽**一切**；
- 正则用 `Pattern.asPredicate()`，即 `matcher(x).find()` ⇒ **子串匹配**，不是整串锚定；我们同样用 `Regex.IsMatch`（find 语义）；
- 命中即"从该包向下遮蔽"，因此即便下层包有该资源也必须返回缺失——实现上是让扫描**就地停止**（`IAssetSource.Blocks`）。

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

## 8. overlay 叠加的实现（已核对）

`PathPackResources.PathResourcesSupplier.openResources`：primary 加上每个 overlay 目录（`content.resolve(overlay)`）各一个
`PathPackResources`，包成 **`OverlayedPackResources`** 返回。其构造函数：

```java
stack.addAll(Lists.reverse(overlayPackResources));   // overlay 逆序在前
stack.add(primaryPackResources);                     // primary 最后
```

`getResource` **正向**遍历该栈取第一个命中 ⇒ **overlay 全部优先于包根**，且多个 overlay 中**列表中靠后者优先**。
`getNamespaces` 取并集；`getMetadataSection` 只查 primary（元数据不受 overlay 影响）。

## 9. 重载调度（已核对）

`SimpleReloadInstance.prepareTasks`：为每个 `PreparableReloadListener` 建立**串行屏障链**（前一个的 future 作为下一个的 `PreparationBarrier`）；
**准备阶段**跑在 `taskExecutor`（后台），**应用阶段**跑在 `mainThreadExecutor`（主线程，`countingReloadExecutor`）；
带进度计数（准备权重 2、重载 2、单监听器 1）。失败由 future 链向外传播，配合 §7 的"整体失败"语义。

## 10. 包格式（已核对）

`PackFormat(int major, int minor)`；格式码接受**整数**或 `[major, minor]` 列表。
`lastPreMinorVersion`：`CLIENT_RESOURCES = 64`、`SERVER_DATA = 81`（用于区分"次版本号引入之前"的取值范围）。

## 11. KnownPacksManager（已核对定位，本项目暂不需要）

客户端侧维护"服务端已知包"的集合（`KnownPack`），用于握手时避免重复下发；它读取 `PackRepository` 的可用/已选包。
**与本项目的本地资源包栈无直接关系**，实现资源包栈时不需要它；如将来做服务器资源包下发再处理。

## 5. 未核对（不得据此实现）

在本次研究范围内**已无未核对项**：包形态、优先级与插入语义、元数据、overlays（元数据 + 叠加实现）、
重载失败与调度、包格式取值均已核对到源码行。范围外（有意不研究）：服务器下发资源包、`KnownPacks` 握手细节。

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

## 8. overlay 叠加的实现（已核对）

`PathPackResources.PathResourcesSupplier.openResources`：primary 加上每个 overlay 目录（`content.resolve(overlay)`）各一个
`PathPackResources`，包成 **`OverlayedPackResources`** 返回。其构造函数：

```java
stack.addAll(Lists.reverse(overlayPackResources));   // overlay 逆序在前
stack.add(primaryPackResources);                     // primary 最后
```

`getResource` **正向**遍历该栈取第一个命中 ⇒ **overlay 全部优先于包根**，且多个 overlay 中**列表中靠后者优先**。
`getNamespaces` 取并集；`getMetadataSection` 只查 primary（元数据不受 overlay 影响）。

## 9. 重载调度（已核对）

`SimpleReloadInstance.prepareTasks`：为每个 `PreparableReloadListener` 建立**串行屏障链**（前一个的 future 作为下一个的 `PreparationBarrier`）；
**准备阶段**跑在 `taskExecutor`（后台），**应用阶段**跑在 `mainThreadExecutor`（主线程，`countingReloadExecutor`）；
带进度计数（准备权重 2、重载 2、单监听器 1）。失败由 future 链向外传播，配合 §7 的"整体失败"语义。

## 10. 包格式（已核对）

`PackFormat(int major, int minor)`；格式码接受**整数**或 `[major, minor]` 列表。
`lastPreMinorVersion`：`CLIENT_RESOURCES = 64`、`SERVER_DATA = 81`（用于区分"次版本号引入之前"的取值范围）。

## 11. KnownPacksManager（已核对定位，本项目暂不需要）

客户端侧维护"服务端已知包"的集合（`KnownPack`），用于握手时避免重复下发；它读取 `PackRepository` 的可用/已选包。
**与本项目的本地资源包栈无直接关系**，实现资源包栈时不需要它；如将来做服务器资源包下发再处理。

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

## 12. 实现进度

| 部分 | 状态 |
| --- | --- |
| 包模型（目录 / zip，zip 保持单个句柄）| 已实现 `Client/Assets/ResourcePack` |
| 发现（跳过重解析点/非包条目；zip 需含 `pack.mcmeta`）| 已实现 `ResourcePackDiscovery` |
| 有序栈 + 逆序回退 + 命名空间并集 | 已实现 `ResourcePackStack`，5 项测试 |
| `filter` 遮蔽（`pushFilterOnly`）| 已实现：`ResourceFilter`（namespace/path 正则、缺省为恒真、**子串匹配**）、`IAssetSource.Blocks` 让扫描就地停止 |
| UI 文档（`ui:` 加载的 JSON）接入栈 | 待做 |
| **统一资源管理器**（`ResourceManager`：一次发现、全局共用，对应原版单一 `ResourceManager` 角色）| 已实现；字体/贴图集/语言表/字体引用全部改为从它读取；端到端验证：日志 `resource packs: 1 installed`（由两行变一行），红色覆盖环仍在 |
| 目录选择与启动器接线 | 暂用 `MC_RESOURCE_PACKS` 环境变量；启动器选项待做 |
| 两阶段重载核心（T-173）：`ResourceManager.Reload` + 有序监听器 + 失败保留旧栈 | 已实现（4 项测试）；字体/贴图集监听器与触发源待接 |
| `overlays` 叠加（A4）| 已实现：`ResourcePackOverlays` + `ResourcePack.Open`（overlay 逆序优先、primary 最后、`pack.mcmeta` 不叠加），4 项测试 |
| 文件监听（T-174/A9）| 已实现：`PackWatcher`（`MC_RESOURCE_PACK_WATCH=1`、300ms 静默去抖、关闭时不创建任何监听器），2 项测试 + 端到端 |
| 重载监听器（T-175/A8）| 已完成：语言表接入；字体/贴图集经 `UiTextureSource` 版本号触发渲染器**重新上传**（端到端：红环→绿环）|
