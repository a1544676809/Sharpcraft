<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

状态：[已评审]
基线：Minecraft 26.3
证据：`docs/analysis/common/item-registry.md`
最后更新：2026-11-03

# 物品注册表

按基线 jar 的注册顺序提供只读物品身份表，供网络 `ItemStack`、装备映射与后续物品逻辑使用。

## 1. 职责范围

负责：

- 从仓库内的顺序快照加载 `minecraft:item` 注册表；
- 提供 id ↔ 标识符的双向查询，枚举顺序严格等于注册顺序；
- 在启动时校验快照基线、首项 `minecraft:air`、行数与哈希。

明确不负责：

- 物品模型、图标、方块物品渲染；
- `DataComponentPatch` / 组件值与物品属性；
- 模组自定义物品的注册与同步；
- 物品栏与容器交互。

## 2. 侧别归属

- 归属：`Common`（纯数据，无客户端渲染依赖）。
- 判定依据：`docs/analysis/common/item-registry.md` 2；item id 同时被协议与以后服务端逻辑使用。

## 3. 依赖关系

| 方向 | 对象 | 说明 |
| --- | --- | --- |
| 依赖 | `Sharpcraft.Common.Registries.Registry<T>` | 复用有序注册表与 `IdMapper` |
| 依赖 | `Sharpcraft.Common.Identifiers.Identifier` | 标识符解析 |
| 被依赖 | `Client.World` / `Client.Render` | 装备映射、物品身份 |
| 被依赖 | 后续 Server 逻辑 | 物品注册查询 |

## 4. 对外契约

- `ItemRegistry.LoadEmbedded()`：从程序集内嵌的基线快照加载并冻结；`Load(path)` 保留给工具/测试。
- `ItemRegistry.ById(int id)` → `ItemInfo?`
- `ItemRegistry.TryGet(Identifier id, out ItemInfo?)`
- `ItemRegistry.Entries`：只读、有序。
- 不暴露快照文件路径、原始行、jar 或资产内容。

## 5. 关键数据结构

| 结构 | 用途 | 生命周期 | 所属线程 |
| --- | --- | --- | --- |
| `ItemInfo` | `Id`（Identifier）+ 可选后续字段 | 进程级，冻结后只读 | 任意只读 |
| `ItemOrderSnapshot` | 快照行、基线、哈希 | 启动时短暂持有，随后释放或缓存校验结果 | 主线程 |

## 6. 行为对齐与偏离

对齐点：

- 枚举/id 顺序 = 原版 `BuiltInRegistries.ITEM.listElements()` 注册顺序；
- `id 0 = minecraft:air`；
- 线上 `ItemStack` 的 `itemHolderId` 按该顺序解释（`Item.STREAM_CODEC`）。

偏离/限制：

- 只覆盖 vanilla 26.3 内置物品；自定义/模组物品降级为 unknown。

## 7. 验收

1. 快照行数 == `Entries.Count`，首项 `minecraft:air`，哈希匹配。
2. 关键样本 id 与 jar 导出一致（如 `saddle`、`iron_horse_armor`、`wolf_armor`；具体值由快照生成后填入测试）。
3. `RegistryOrderTests` 类似的“顺序快照”测试钉住顺序，不能靠字典枚举。
4. 缺失/损坏快照启动失败并给出基线不匹配提示。

## 8. 未解决问题与风险

- 快照尚未生成（需分析环境导出）。
- 快照与运行期加载是否共用同一文件，需在实现前确认（避免 test 与 runtime 双真相源）。
- 基线升级时必须重新生成快照并更新哈希。

## 9. 参考资料

- `docs/analysis/common/item-registry.md`
- `docs/analysis/common/registry-order.md`



