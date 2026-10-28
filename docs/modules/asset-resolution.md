<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

状态：[已评审]
基线：Minecraft 26.3
证据：`docs/analysis/assets/resource-resolution.md`（行为规格）、`docs/analysis/assets/jar-entry-map.md`（来源清单）、ADR-0005
最后更新：2026-10-28

# 资产解析层（Asset Resolution）

把多个资产来源变成一个**行为与原版一致**的只读资源视图：给定 `Identifier`，得到唯一一份数据；给定前缀，能列出全部可用资源。

## 1. 职责范围

负责：

- 定义来源抽象 `IAssetSource`，并提供四个实现：pack 容器、官方索引 objects、散装目录（模组/调试）、内置兜底。
- 按原版语义解析：`Identifier` + 包类型目录 + `FileToIdConverter` 规则（`docs/analysis/assets/resource-resolution.md` §2）。
- 优先级：多处命中时返回最高优先级的一份；`ListAll` 按优先级保留首个。
- 元数据兄弟：取纹理时同时可取 `<path>.mcmeta`（§4）。
- 语言合并：按原版 `getResourceStack` + 后写覆盖的顺序合并（§5）。
- 缺失诊断：区分"没装"与"装了但缺条目"，给出可操作提示。

明确不负责：

- 解码 PNG、解析模型 JSON 的语义（属于资源消费者：`render` 模块）。
- 打包（`docs/modules/assets.md`）与索引下载（启动器版本获取）。
- 资源包 `pack_format` 兼容性判定（本阶段只消费自家 pack 与官方索引）。

## 2. 侧别归属

- 归属：**`Sharpcraft.Client`**（`Client/Assets/`），与 ADR-0007 一致。
- 判定依据：用户资源包会替换纹理，图集必须随包栈在客户端重建；原版同样由客户端持有资源管理器与拼图逻辑（证据见 ADR-0007）。
- 启动器复用：`Launcher` 已引用 `Client`，其 `assets` / `launch` 校验命令调用同一份解析代码，不存在第二套实现。
- 反方向仍然禁止：`Client` MUST NOT 引用 `Launcher`（架构门禁强制）。

## 3. 依赖关系

| 方向 | 对象 | 说明 |
| --- | --- | --- |
| 依赖 | `Common.Identifiers` | 复用既有 `Identifier`，不新建位置类型 |
| 被复用 | `Launcher`（校验与打包命令） | 通过已有的 `Launcher → Client` 引用调用 |
| 依赖 | `System.IO.Compression` / `System.Text.Json` | zip 与索引 JSON |
| 被依赖 | 渲染层（纹理）、语言层 | 只读消费 |
| 不依赖 | 任何来源实现的调用方 | 通过接口注入，便于测试与替换 |

## 4. 对外契约

```
interface IAssetSource {
    string Id { get; }                                   // 诊断用：pack 名 / "official-objects" / 目录名
    int Priority { get; }                                 // 数值越大优先级越高
    bool TryOpen(Identifier location, out Stream content); // 单份读取
    IEnumerable<Identifier> List(string prefix);           // 该前缀下的全部位置
}
```

- `AssetStack`：持有按优先级排序的来源集合，提供 `TryOpen`、`OpenRequired`、`ListAll`、`OpenMeta`。
- `ResourceLocator`：把 `(资源类型, Identifier)` 变成文件位置（`blockstates/x` → `assets/minecraft/blockstates/x.json`），规则集中在这一点，测试只测这里。
- `MissingResourcePolicy`：`Warn`（默认，打印一次并回退兜底）、`Throw`（测试与严格模式）。

## 5. 来源与栈顺序

**原版用有序列表表达优先级（没有数值）**，见 `docs/analysis/assets/resource-resolution.md` §3.1。本层同样用有序列表：索引 0 是兜底，越往后优先级越高，查找从末尾向前。

默认装配顺序（低 → 高）：

| 位置 | 来源 | 实现 | 说明 |
| --- | --- | --- | --- |
| 0 | 内置兜底 | `BuiltInFallbackSource` | **紫黑格子**占位纹理（代码生成，非 Minecraft 资产） |
| 1 | 官方索引 objects | `OfficialObjectsSource` | 只读挂载 `indexes/<id>.json` + `objects/`；缺失只警告 |
| 2 | 基础 pack | `PackAssetSource` | ADR-0005 的单文件 pack；按 manifest 校验条目 |
| 3+ | 自定义资源包 | `PackAssetSource`（zip）/ `DirectoryAssetSource`（目录） | 用户安装的资源包，按选中顺序依次叠放，后选中的在上 |

- **自定义资源包是预留能力**：本阶段提供加载与叠放（zip 与目录两种形态都支持），`pack.mcmeta` 的 `pack_format` 只**解析并记录**，不做版本区间判定；等 M6 做资源包管理界面时再补兼容策略。
- 模组资源走 `DirectoryAssetSource`，可以叠在用户资源包之上；两者用同一实现，区别只在装配顺序。
- 索引 id 必须与 `version.json` 的 `assetIndex.id` 一致，否则 `OfficialObjectsSource` 拒绝挂载并说明原因。
- **语言特例**：语言按原版 `getResourceStack` 语义合并（高→低依次写入，**后写覆盖先写** ⇒ 低优先级胜出），与渲染资源的方向相反，实现处必须注明原因。

## 6. 关键行为

| 场景 | 期望 |
| --- | --- |
| 多来源命中 | 返回优先级最高的一份；`ListAll` 去重后保留最高优先级 |
| `.mcmeta` 兄弟 | `OpenMeta(textureId)` 自动用 `id.path + ".mcmeta"`；缺失返回空而非报错 |
| 资源缺失（非语言） | 按策略警告一次，然后由调用方回退（渲染用占位纹理） |
| 语言缺失 | 警告并回退 `en_us`；若 `en_us` 也没有，抛错（对应原版 `EmptyTranslationsException`） |
| pack 条目哈希不符 | 视为损坏，拒绝该条目并警告，不静默使用 |
| 索引 objects 缺文件 | 警告列出缺失数量与前 3 个 key，继续运行 |
| 前缀列举 | 只返回存在的资源；不构造不存在的 `Identifier` |
| 位置非法 | `Identifier` 解析失败即拒绝并报告原始字符串 |

## 7. 测试策略

全部用合成数据，不依赖真实 jar 或官方安装：

1. 用 `ZipArchive` 造两个 pack（优先级不同），断言高优先级覆盖、低优先级被遮蔽。
2. 造两个散装目录，断言叠加顺序与覆盖。
3. 造假的 `indexes/<id>.json` + `objects/` 目录（内容随机的"对象"），断言读取与哈希校验路径。
4. 断言 `ListAll` 去重与顺序稳定（同一输入两次列举结果相同）。
5. 断言 `.mcmeta` 兄弟查找（命中/缺失两种情况）。
6. 断言语言合并方向与 §5 一致（构造两份 lang，检查哪个键胜出）。
7. 断言索引 id 不匹配时拒绝挂载。
8. 断言缺失策略：`Warn` 不抛、`Throw` 抛，且警告只打印一次/资源。
9. 断言自定义资源包叠放：低层的同名资源被高层的覆盖；`pack.mcmeta` 的 `pack_format` 被解析但暂不据此拒绝。

## 8. 性能约束

- `TryOpen` 在热路径（每帧可能查很多纹理）上 MUST NOT 为每次查找重建 zip 中央目录：`PackAssetSource` 在构造时读取并缓存条目表。
- 缓存键为 `Identifier`；缓存不持有打开的流（流是短生命周期的）。
- 后续若需要"整批预取"，再引入纹理级缓存，不在本层做。

## 9. 验收标准

- `docs/analysis/assets/resource-resolution.md` 的每条规则都有对应测试；
- 渲染层能在真实数据下取到 `textures/block/stone.png` 与对应 `.mcmeta`；
- 缺失索引资产时只有警告、不崩溃；
- 同一个资源在两个来源都存在时，结果由 §5 的栈顺序决定（有测试守护）；
- 紫黑格子占位纹理在纹理缺失时可见，且**不含任何 Minecraft 资产**。

## 10. 实测（真实安装，2026-10-28）

`launcher assets <jar> <version-json>` 在打包之后会跑一次解析自检，输入为本机 26.3 安装（jar 已校验为基线 SHA-1，资产索引 34）：

| 探针 | 结果 | 来源 |
| --- | --- | --- |
| `textures/block/stone.png` | 151 字节 | base-pack |
| `textures/block/debug.png` | 500 字节 | base-pack |
| `models/block/stone.json` | 98 字节 | base-pack |
| `lang/en_us.json` | 546,577 字节 | base-pack |
| `lang/zh_cn.json` | 562,928 字节 | **official-objects:34** |
| `textures/block/water_still.png.mcmeta` | 44 字节 | base-pack（`.mcmeta` 兄弟规则生效） |
| 列举纹理 | **3,964** | 与 `jar-entry-map.md` 的 `.png` 数一致 |
| 列举语言 | **144** | 索引多语言 + pack 内 en_us |

结论：纹理/模型来自 pack，语言按设计从官方索引读取，两者共存于同一有序栈且互不干扰。

