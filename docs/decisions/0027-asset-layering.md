<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

<!-- doc
title: 0027 资源分层与 Sharpcraft.Assets 拆分
summary: 参考原版的资产分层（内置 jar 包 + 外部索引只读叠加 + 用户资源包），并把底层 pack/格式/IO 类型下沉为独立库 Sharpcraft.Assets
status: 已接受
updated: 2026-11-06
-->

# 0027 资源分层与 Sharpcraft.Assets 拆分

- 状态：已接受（2026-11-06）
- 基线：Minecraft 26.3
- 关联：ADR-0005（资产 pack 与官方索引）、ADR-0007（资源归客户端）、ADR-0026（启动器/客户端解耦）、R-025、原则 3 / 5 / 8 / 12 / 13

## 1. 背景

参考实现在构造资源栈时的分层是固定的（`ref://mc-26.3-analysis/src/net/minecraft/client/main/Minecraft.java`、
`ref://mc-26.3-analysis/src/net/minecraft/client/gui/screens/...` 与 `GameConfig.getExternalAssetSource`）：

1. **内置 jar 资源包**（底部）：`VanillaPackResourcesBuilder.pushJarResources()` 把客户端 jar 的 `assets/minecraft`
   （11,811 条：models / blockstates / items / textures / particles / shaders / equipment / atlases / font /
   post_effect / texts / en_us 语言）作为最低优先级来源。
2. **外部资产索引只读叠加**：`VanillaPackResourcesBuilder.pushAssetPath(CLIENT_RESOURCES, externalAssetRoot)`，
   由 `IndexedAssetSource.createIndexFs(assetsDir, assetIndex)` 把 `indexes/<assetIndex>.json` 的逻辑路径
   （`minecraft/sounds/...ogg`、`minecraft/lang/<locale>.json`、`minecraft/sounds.json`、`icons/*`、
   少量 `minecraft/textures`、`minecraft/font`、`pack.mcmeta`）映射到 `objects/<h2>/<sha1>`。它覆盖在 jar 包**之上**，
   提供全部音频（约占索引 96%）与**所有**语言（jar 只有 `en_us`/`deprecated`）。
3. **用户资源包**（顶部）：玩家选择的资源包按列表顺序压栈，后压入者胜。

在此之前，我们只挂了 (1) 与 (3)：语言只能取到 jar 的 `en_us`，音频完全缺失；同时启动器为了复用"资源栈与图集自检"，
直接 `ProjectReference` 了 `Sharpcraft.Client`，这与 R-025（启动器不得依赖客户端游戏库）冲突。

## 2. 决策

1. **新增低层项目 `src/Sharpcraft.Assets`（`net10.0`，只引用 `Sharpcraft.Common`）**，把 pack/格式/IO 类型迁入，
   命名空间统一为 `Sharpcraft.Assets`：
   `ResourcePath` / `ResourceKind` / `ResourceFilter` / `AssetStack` / `AssetPackManifest` / `ResourcePack` /
   `ResourcePackFormats` / `PackFormatVersion` / `ResourcePackSource` / `ResourcePackStack` / `ResourcePackDiscovery` /
   `ResourcePackOverlays` / `AssetSources`（zip / 目录 / 索引对象 / 内置兜底）/ `ResourceManager` 及其辅助类型。
   渲染侧类型（`SpriteAtlas`、`SpriteAtlasBuilder`、`SpriteImageCache`、`BlockSpriteMap`、`BlockModelResolver`、
   `BitmapFontDefinition`、`Colormap`、`CloudTexture`、`CelestialSprites`、`AnimationFrameSize`、`AtlasMipChain`、
   `SpriteAlpha`、`Transparency`、`PackWatcher`）**保留在 `Sharpcraft.Client`**。
2. **新增 `IndexedAssetSource`**（参考等价 `IndexedAssetSource.createIndexFs`）：解析 `indexes/<id>.json` 的
   `objects` 映射，把逻辑 `ResourcePath`（及 `pack.mcmeta`/`sounds.json`/`icons/*` 等原始路径）解析为
   `objects/<h2>/<hash>` 的只读流；不复制任何资产。
3. **客户端资源栈分层固定为**：内置兜底 < `--pack` jar 包 < 外部 `--assetsDir`+`--assetIndex` 索引叠加 <
   `--resourcePackDir` 用户包。语言加载（`FrameCommand.PrepareLanguage`）经同一 `ResourceManager` 读取，
   因此非 `en_us` 语言从索引叠加命中；索引缺失时降级为 jar 的 `en_us`，不崩溃。
4. **启动器边界**：`Sharpcraft.Launcher` 的 `assets` 命令与打包/自检改用 `Sharpcraft.Assets`，不再需要 `Sharpcraft.Client`。
   （开发/诊断命令的进程内复用仍属 R-025 残余，见 §5。）

## 3. 影响面

- 代码：新增 `src/Sharpcraft.Assets`；`Sharpcraft.Client/Assets` 只剩渲染侧类型；`Sharpcraft.Client`、`Sharpcraft.Launcher`、
  测试与沙箱工具增加对 `Sharpcraft.Assets` 的引用。
- 测试：新增 `IndexedAssetSource` 的 TEMP 夹具单测（逻辑路径解析、缺失对象、叠加优先级、原始路径）；既有资源测试改用新命名空间。
- 文档：本 ADR、`docs/modules/launcher.md`、`docs/modules/client.md`、`docs/analysis/assets/jar-entry-map.md`、
  `docs/src/sharpcraft-assets.md`、`docs/src/sharpcraft-client/assets.md`。
- 兼容性：只读叠加，不改协议/存档；索引缺失时行为与拆分前一致（jar `en_us`）。

## 4. 替代方案与被否原因

| 方案 | 否决原因 |
| --- | --- |
| 继续让启动器引用 `Sharpcraft.Client` | 违反 R-025 与原则 8：启动器应只按可执行路径启动客户端，不应装载游戏库 |
| 把索引对象复制进 pack | 违反原则 3 与"只读、不复制"约束；索引约 96% 是音频，复制代价大且易漂移 |
| 把低层类型留在 `Sharpcraft.Client` | 启动器为了打包/自检不得不引用游戏程序集，边界无法收敛 |

## 5. 复查条件与残余

- **R-025 残余**：`frame` / `window` / `walk` / `play` 仍由启动器进程内调用客户端游戏库（`Sharpcraft.Client.Game`），
  因此启动器暂时仍 `ProjectReference Sharpcraft.Client`。彻底解耦的下一步是把这些开发命令改为 **spawn
  `Sharpcraft.Client.exe`**（客户端 CLI 已支持 `--frame/--window/--walk`），启动器只装配参数并流式转发 stdout。
  该项因以下具体限制未在本轮完成：`play` 无对应客户端 CLI 模式；`frame` 的本地场景开关（`MC_FRAME_REPLAY` 等）
  依赖客户端内部场景构建；`HostDebugOptions` 仍直接设置客户端静态项（`ChatLog.BackgroundOpacity`、`PackWatcher.IsEnabled`）。
- 当上述限制被 CLI 显式选项覆盖后，重新评估移除 `Sharpcraft.Client` 项目引用并启用"启动器不得引用客户端"架构断言。
