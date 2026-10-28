<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

状态：[已接受]
日期：2026-10-28

# ADR-0007：资源加载与图集归客户端所有，启动器只做校验与打包

## 背景

T-034 把资源解析层放在启动器（外围宿主层），并计划由启动器预生成 `atlas.png` + `atlas.json` 交给客户端采样。项目所有者指出这个方向与"等效"冲突：**我们未来要支持加载用户安装的 Minecraft 资源包**，资源包会替换纹理，图集必须随包栈重建；如果图集是启动器烘焙的静态产物，那么一旦叠上资源包就会立刻失效，届时又得把逻辑搬回客户端。

原版的做法是明确的（证据见 §证据）：客户端自己持有 `ResourceManager`，`SpriteLoader` 从资源管理器发现精灵、`TextureAtlas` 完成拼图，`ModelManager` 在同一套资源管理器上烘焙模型。资源包只是在客户端包栈里多几层。

## 决策

1. **资源加载归 `Sharpcraft.Client`**：`IAssetSource`、`AssetStack`、`ResourcePath`/`ResourceLookup`、zip/目录/官方索引/内置兜底四个来源、以及包容器契约（`AssetPackManifest`）全部落在 `Client/Assets/`。
2. **图集在客户端构建**：从包栈发现精灵 → 拼图 → 上传纹理。启动器**不再**预生成图集。
3. **启动器保留校验与打包职责**：`launch` / `assets` 仍由启动器驱动（jar 校验、pack 打包、索引 id 比对），它通过**已有的** `Launcher → Client` 引用复用同一份解析代码，不存在两套实现。
4. **边界不变**：`Client` 不得引用 `Launcher`（架构门禁 B1-B3 继续强制）；资源层只用 `Common`（`Identifier`）与 `System.*`，不引入宿主类型。
5. **资源包叠放在客户端生效**：用户资源包 / 模组目录按包栈顺序叠放，改纹理只需重建图集，不需要重启启动器流程。`pack_format` 兼容性判定仍留在 M6（R-016）。

## 备选方案

| 方案 | 优点 | 缺点 | 结论 |
| --- | --- | --- | --- |
| **客户端持有资源层与图集**（采纳） | 与原版一致；资源包天然生效；一处实现 | 客户端要认识 pack 容器格式 | 采纳 |
| 启动器预生成图集 | 客户端最简单 | 资源包一变就失效，正是所有者指出的问题 | 否决 |
| 新建独立 `Sharpcraft.Resources` 项目 | 归属更中性 | 现有 `Launcher → Client` 引用已存在，多一个项目只增加门禁与文档成本，收益不明确 | 暂不采纳，若将来换启动器再考虑 |
| 客户端与启动器各写一份解析 | 互不牵连 | 两套路径规则必然漂移 | 否决 |

## 后果

- **正面**：资源包与模组覆盖在架构上就位；解析规则只有一份（`Client/Assets`）；启动器的验证命令与客户端渲染使用同一份代码，不会出现"启动器说能读到、客户端读不到"。
- **负面**：`Client` 认识 pack 容器格式（`manifest.json` 校验），因此打包器与读取器共享契约类型，改格式要同时改两侧（由测试守护）。
- **影响其它文档**：`docs/modules/asset-resolution.md` 的侧别归属改为 Client；`docs/modules/render.md` §6.1 的图集来源改为客户端；`docs/architecture/boundaries.md` 增加一行归属说明；矩阵新增 T-038。

## 证据（原版，`ref://mc-26.3-analysis/`）

| 事实 | 位置 |
| --- | --- |
| 图集由客户端 `SpriteLoader` 从 `ResourceManager` 发现精灵并拼图 | `src/net/minecraft/client/renderer/texture/SpriteLoader.java`、`TextureAtlas.java` |
| 图集必须包含 missing sprite（缺失纹理占位） | `TextureAtlas.upload` 对 `MissingTextureAtlasSprite` 的强制检查 |
| 模型烘焙同样基于客户端资源管理器 | `src/net/minecraft/client/resources/model/ModelBakery.java`、`ModelManager.java` |

## 复查条件

- 若将来把资源加载抽成独立程序集（例如服务端也要读数据包），按本 ADR 的第四条重新划分，不改变"图集在客户端构建"这一条。
