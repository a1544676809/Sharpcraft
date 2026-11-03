<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

<!-- doc
title: 物品注册表（Item registry）调查
summary: 26.3 内置 item 注册表的 id 来源、网络语义、快照生成方案与风险
status: 草稿
updated: 2026-11-03
-->

# 物品注册表（Item registry）调查

## 1. 结论速查

- `minecraft:item` 是 **内置（built-in）注册表**，不是随数据包加载的动态注册表；客户端必须使用与基线 jar 一致的注册顺序。
- `ItemStack` 的网络字段是 `Item.STREAM_CODEC = ByteBufCodecs.holderRegistry(Registries.ITEM)`，线上写的是 **registry id（VarInt）**，不是资源路径。
- 因此 `set_equipment` 里的 `itemHolderId` 要正确解释，必须知道 26.3 的 **完整 item 注册顺序**。
- 仓库当前没有 item 注册表、没有 item 顺序快照；必须先补一份**只含标识符的注册顺序快照**（不含任何游戏资产）。

## 2. 证据

### 2.1 内置注册表

- `BuiltInRegistries.ITEM = registerDefaultedWithIntrusiveHolders(Registries.ITEM, "air", Items.AIR)`：item 是静态注册表，默认项 `minecraft:air`。
- `BuiltInRegistries` 的字段初始化顺序 + `Items` 的 `registerItem` / `registerBlock` / `ColorCollection` 调用顺序共同决定 id；`MappedRegistry.listElements()` / 迭代按注册顺序，`byId` 下标即 id（`docs/analysis/common/registry-order.md` §1、§3）。

### 2.2 网络语义

- `Item.STREAM_CODEC` 使用 `ByteBufCodecs.holderRegistry(Registries.ITEM)`，读取 registry id。
- `RegistrySynchronization.SYNCHRONIZED_REGISTRIES` 来自 `RegistryDataLoader.SYNCHRONIZED_REGISTRIES`；该列表是**动态注册表**集合（dimension type、biome 等），item 不在其中。因此 vanilla 客户端不能从配置阶段的 RegistryData 推断 item id，必须使用内置顺序。
- `ItemStack.OPTIONAL_STREAM_CODEC` 在非空栈时依次写 `count`、`itemHolderId`、`DataComponentPatch`；第一阶段的装备解码只消费前两项及空 patch（见 `docs/analysis/render/entity-equipment.md` 2）。

### 2.3 为什么不能从 `Items.java` 文本顺序自动得到 id

`Items` 的注册入口不止一种：

- `registerItem(...)`；
- `registerBlock(...)`（方块物品）；
- `ColorCollection.registerItems(...)` / `registerBlockItems(...)`（按颜色批量展开，每个颜色生成一个 item）；
- 还有 spawn egg 等辅助注册方法。

静态源码的书写顺序不等于运行期注册顺序；批量注册还会按内部颜色/枚举顺序展开。因此必须从**实际运行的 `BuiltInRegistries.ITEM.listElements()`** 导出顺序，而不是解析源码文本。

## 3. 建议产物

### 3.1 快照文件（仓库内）

新增 `tests/Sharpcraft.Common.Tests/TestData/vanilla-item-order.txt`：

- 每行一个 `minecraft:<path>`，行号 = registry id（第一行 `minecraft:air` = 0）；
- 文件头记录基线版本、jar SHA-1、导出日期与工具版本；
- 只含标识符，不含名称翻译、图标、模型、NBT 或任何资产，符合原则 3。

现有 `vanilla-block-order.txt` 是同类先例：它已作为顺序契约入库，并由 `RegistryOrderTests` 钉住。

### 3.2 生成方式（分析工作区）

在 `../mc-26.3-analysis/` 的 Java 分析工具中：

1. 启动 26.3 客户端/数据引导，取 `BuiltInRegistries.ITEM`；
2. 遍历 `listElements()`，输出每个 holder 的 identifier；
3. 记录 `size` 与首尾元素，导出为 JSON/文本；
4. 把结果复制为上面的快照文件，并在文档中记基线 SHA-1。

不得把 jar、class、反编译源码或生成脚本输出写入仓库；仓库只存快照与生成说明。

## 4. C# 侧设计建议（待模块文档定稿）

- 复用现有 `Sharpcraft.Common.Registries.Registry<T>`：按快照顺序 `Register(Identifier, ItemInfo)`，`Freeze()`；`ById(id)` 与 `TryGet(id)` 均可。
- `ItemInfo` 第一阶段只需 `Identifier Id`（以及可选的观察到的最大堆叠/装备类别，后续再加）；不加载 item 模型或组件。
- 启动时校验：
  - 快照行数 = 注册表元素数；
  - 第 0 行是 `minecraft:air`；
  - 文件哈希与基线元数据匹配；不匹配则启动失败并给出可操作提示。
- 不把 `Dictionary` 枚举顺序当作 id；id 只来自快照行号。
- 对外只暴露有序 `Entries`、`ById`、`TryGet`；渲染/装备层不得直接读文件。

## 5. 与装备 E2/E3 的关系

- E2 的“受控物品 → 装备纹理映射”应以 `ItemInfo.Id`（注册名）为 key，而不是手写整数 id；整数 id 由快照解析。
- E3 的装备层通过 `ItemInfo` 查表得到材质/纹理；遇到快照中不存在的物品（自定义/mod）走“未知装备”降级，不崩溃。
- 这样可以避免在 `EntityModelDefinition` 或渲染器中散落魔法数字。

## 6. 未解决问题与风险

1. **快照尚未生成**：需要分析环境执行上述导出；当前仓库没有该数据。
2. **基线升级**：item id 顺序会随版本变化，快照必须随基线升级重新生成；版本不匹配应硬失败，而不是回退到猜测。
3. **自定义/模组物品**：vanilla 内置快照不含自定义 item；对自定义 id 只能降级为“未知”，不能正确映射装备纹理。若未来需要模组 item，需另设计注册表同步/扩展协议。
4. **服务端差异**：若连接非 vanilla 服务端且其 item 注册表不同，id 解释会错位；本项目只承诺 vanilla 26.3 基线。
5. **快照文件体积/维护**：完整 item 清单行数较大但纯文本可审计；需要确认是否拆分为生成资源与测试快照两份（同一文件可被测试与运行期共用，避免双真相源）。

## 7. 参考资料

- `docs/analysis/common/registry-order.md`
- `docs/analysis/render/entity-equipment.md`
- `docs/specs/network/entity-sync.md`
- `docs/modules/entity-equipment.md`
