<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

状态：[已评审]
基线：Minecraft 26.3
证据：ADR-0007；ADR-0027；`docs/analysis/assets/resource-resolution.md`；`docs/analysis/assets/block-models.md`
最后更新：2026-11-06
授权：用户 2026-10-28 决定"资源与图集归客户端"，启动器只负责校验与打包（ADR-0007）。

# Client/Assets —— 图集与方块贴图解析（渲染侧）

把"一个方块该显示哪张纹理"从路径猜测做成数据驱动的解析链：资源栈（`Sharpcraft.Assets`）→ 方块状态与模型 → 图集 → 精灵矩形。

> 2026-11-06 起，底层 pack/格式/IO 类型（资源栈、包、路径规则、索引来源、`ResourceManager`）已下沉到
> 独立库 `Sharpcraft.Assets`（ADR-0027）。本目录只保留**渲染侧**类型。

## 1. 职责范围

负责：

- 图集（`SpriteAtlas` / `SpriteAtlasBuilder`）：把纹理按货架算法拼进一张方形纹理，记录精灵的像素矩形与半纹素内缩后的 UV 矩形。
- 方块贴图解析（`BlockSpriteMap` 与 `BlockModelResolver`）：`blockstates` → 首个变体 → 模型 → `parent` 链合并 `textures` → 递归解析 `#变量` → 每面纹理；失败时回落到名字约定、再回落到 `<名字>_top/_side/_bottom`，最后才是 missing 精灵。
- 字体现（`BitmapFontDefinition`）、颜色表（`Colormap`）、云与天体精灵、动画帧尺寸与 mip 链等渲染附属物。

明确不负责（自 2026-11-06 起由 `Sharpcraft.Assets` 负责）：

- 有序资源栈（`AssetStack`）、资源路径规则（`ResourcePath`）、资源包发现/格式/overlay、索引只读叠加（`IndexedAssetSource`）、
  会话级 `ResourceManager`。见 [`docs/src/sharpcraft-assets.md`](../sharpcraft-assets.md)。

其余不负责：

- 不读 jar、不校验 jar：那是启动器资产管线的职责（原则 3、8）。
- 不做窗口与 GPU 绑定：这里只产出像素与矩形，上传纹理与采样在 `Framework/Rendering`。
- 不解析 element 几何、不消费 `uv`、不做 `multipart` 条件求值、不做生物群系染色与叠加层（见 §7）。

## 2. 侧别归属

- 归属：`Client`（渲染侧）。
- 判定依据：用户资源包会替换纹理与模型，图集必须按“当前生效的包栈”在客户端侧重算；底层栈与来源在 `Sharpcraft.Assets`，
  本目录只消费它（ADR-0007、ADR-0027）。

## 3. 依赖关系

| 方向 | 对象 | 说明 |
| --- | --- | --- |
| 依赖 | `Sharpcraft.Common` | `Identifier`、`Registries.BlockStateLayout`、`Imaging`（PNG 解码） |
| 依赖 | `Sharpcraft.Assets` | `AssetStack`、`ResourceKind`、`ResourcePath`、`ResourceManager`、来源实现 |
| 被依赖 | `Sharpcraft.Client/Render` | 网格构建按面取精灵矩形 |
| 被依赖 | `Sharpcraft.Launcher` | 出图与验收命令复用同一套解析（仅开发工具；见 ADR-0027 残余） |
| 不依赖 | `Sharpcraft.Framework`、`Sharpcraft.Launcher` | 不得反向依赖，由架构测试守护 |

## 4. 对外契约

| 类型 | 契约 |
| --- | --- |
| `SpriteAtlas` | `Sprites`（id → `SpriteRect`）、`Pixels`、`Get`、`Contains`；`MissingSprite` 恒存在 |
| `SpriteRect` | 像素矩形与 `U0/V0/U1/V1`（已按半纹素内缩） |
| `BlockSpriteMap` | `ForState(int, BlockFace)` 与 `ForBlock(Identifier, BlockFace)`；有布局时是数组下标查表 |
| `BlockModelResolver` | `Resolve(Identifier)` 返回每面纹理或 null，内部带缓存 |

> 资源栈与来源的对外契约见 [`docs/src/sharpcraft-assets.md`](../sharpcraft-assets.md)。

## 5. 关键结构与生命周期

- 图集在启动后构建一次，资源包变更时重建；构建同步执行，实测 52–95 ms（1301 个精灵、1024²）。
- `BlockSpriteMap` 建图集时把 35,723 个方块状态一次性映射为方块下标数组，之后按面查表，稳态零分配。
- 纹理像素上传 GPU 后即可释放，当前仍保留在图集对象里以便诊断采样（见 §7）。

## 6. 与原版的对齐点与偏离点

对齐：

- 包优先级"后压入者胜"、资源查找逆序扫描、缺失回落 missing 精灵、动画条带取首帧。
- 模型继承：子 `textures` 覆盖父、`#变量` 递归解析、面标签 `up/down/north…` 与顶/底/侧三桶的对应关系。

有意偏离：

- 只取首个变体，忽略权重随机与 `x`/`y` 旋转。
- 贪心合并后的四边形在自己的 UV 空间内平铺整张纹理，而不按 element `uv` 取子区域。
- 半纹素 UV 内缩替代原版的图集内边距（padding）：等价且省内存。

## 7. 未解决问题与风险

- **特殊渲染方块**：267 个方块（流体、火把、火、箱子、活塞）没有立方体模型，当前落到 missing 精灵（T-041）。
- **动态注册表**：`registry_data` 驱动的布局尚未接（T-042），服务器增删或重排方块时状态 id 会错位。
- 图集未做 mipmap 与 alpha 排序，远处纹理有闪烁（见 `docs/modules/render.md`）。
- 图集缓冲随包重建而未复用；多包频繁切换时需要池化。

<!-- BEGIN GENERATED: class-map -->

| Project | Sharpcraft type | Kind | Status | Reference class | Reference path | Note |
| --- | --- | --- | --- | --- | --- | --- |
| Sharpcraft.Client | Sharpcraft.Client.Assets.BlockModelResolver | Mirror | Partial | net.minecraft.client.renderer.block.BlockModelResolver | ref://mc-26.3-analysis/src/net/minecraft/client/renderer/block/BlockModelResolver.java | auto-resolved by name; verify |
| Sharpcraft.Client | Sharpcraft.Client.Assets.Transparency | Mirror | Partial | com.mojang.blaze3d.platform.Transparency | ref://mc-26.3-analysis/src/com/mojang/blaze3d/platform/Transparency.java | auto-resolved by name; verify |

<!-- END GENERATED: class-map -->
