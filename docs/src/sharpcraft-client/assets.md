<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

状态：[已评审]
基线：Minecraft 26.3
证据：ADR-0007；`docs/analysis/assets/resource-resolution.md`；`docs/analysis/assets/block-models.md`
最后更新：2026-10-29
授权：用户 2026-10-28 决定"资源与图集归客户端"，启动器只负责校验与打包（ADR-0007）。

# Client/Assets —— 资源栈、图集与方块贴图解析

把"一个方块该显示哪张纹理"从路径猜测做成数据驱动的解析链：资源栈 → 方块状态与模型 → 图集 → 精灵矩形。

## 1. 职责范围

负责：

- 有序资源栈（`AssetStack`）：按原版语义叠加多个来源，后压入的包优先；缺失资源按策略处理（告警或严格）。
- 资源路径规则（`ResourcePath`）：`assets/<namespace>/<kind>/<path>.<ext>` 的拼装与解析，`kind` 覆盖 `textures`、`blockstates`、`models` 等。
- 图集（`SpriteAtlas`）：把纹理按货架算法拼进一张方形纹理，记录精灵的像素矩形与半纹素内缩后的 UV 矩形。
- 方块贴图解析（`BlockSpriteMap` 与 `BlockModelResolver`）：`blockstates` → 首个变体 → 模型 → `parent` 链合并 `textures` → 递归解析 `#变量` → 每面纹理；失败时回落到名字约定、再回落到 `<名字>_top/_side/_bottom`，最后才是 missing 精灵。

明确不负责：

- 不读 jar、不校验 jar：那是启动器资产管线的职责（原则 3、8）。
- 不做窗口与 GPU 绑定：这里只产出像素与矩形，上传纹理与采样在 `Framework/Rendering`。
- 不解析 element 几何、不消费 `uv`、不做 `multipart` 条件求值、不做生物群系染色与叠加层（见 §7）。

## 2. 侧别归属

- 归属：`Client`。
- 判定依据：用户资源包会替换纹理与模型，图集必须按"当前生效的包栈"在客户端侧重��；把资源与图集放在客户端后，启动器不必知道任何渲染细节，只能通过既有的 `Launcher → Client` 引用复用同一套路径规则（ADR-0007）。

## 3. 依赖关系

| 方向 | 对象 | 说明 |
| --- | --- | --- |
| 依赖 | `Sharpcraft.Common` | `Identifier`、`Registries.BlockStateLayout`、`Imaging`（PNG 解码） |
| 被依赖 | `Sharpcraft.Client/Render` | 网格构建按面取精灵矩形 |
| 被依赖 | `Sharpcraft.Launcher` | 出图与验收命令复用同一套解析 |
| 不依赖 | `Sharpcraft.Framework`、`Sharpcraft.Launcher` | 不得反向依赖，由架构测试守护 |

## 4. 对外契约

| 类型 | 契约 |
| --- | --- |
| `IAssetSource` | `TryRead(ResourcePath)` 返回字节或 null；实现包括 zip 包、目录、官方索引对象、内置兜底 |
| `AssetStack` | `Read(kind, id)`；`Policy` 决定缺失时告警还是抛错；包按压入顺序后进先出 |
| `SpriteAtlas` | `Sprites`（id → `SpriteRect`）、`Pixels`、`Get`、`Contains`；`MissingSprite` 恒存在 |
| `SpriteRect` | 像素矩形与 `U0/V0/U1/V1`（已按半纹素内缩） |
| `BlockSpriteMap` | `ForState(int, BlockFace)` 与 `ForBlock(Identifier, BlockFace)`；有布局时是数组下标查表 |
| `BlockModelResolver` | `Resolve(Identifier)` 返回每面纹理或 null，内部带缓存 |

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
