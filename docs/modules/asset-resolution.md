<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 MinecraftCSharp contributors -->

状态：[待评审]
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

- 归属：**外围宿主层**（与资产打包、启动器同层）。
- 判定依据：原则 5 把资产管线列在外围宿主层；游戏逻辑不得读 jar，也不应直接持有来源实现。
- 消费方向：`Client`/`Framework` 通过一个**只读接口**取资源，接口定义在宿主层程序集，避免 Client 反向依赖宿主。

## 3. 依赖关系

| 方向 | 对象 | 说明 |
| --- | --- | --- |
| 依赖 | `Common.Identifiers` | 复用既有 `Identifier`，不新建位置类型 |
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

## 5. 来源与优先级

| 优先级 | 来源 | 实现 | 说明 |
| --- | --- | --- | --- |
| 300 | 模组/调试散装目录 | `DirectoryAssetSource` | 目录结构 `<dir>/assets/<namespace>/…`；支持多目录叠加 |
| 200 | 基础 pack | `PackAssetSource` | 读 ADR-0005 产出的 zip；按 manifest 校验条目哈希 |
| 100 | 官方索引 objects | `OfficialObjectsSource` | 只读：`indexes/<id>.json` + `objects/<ab>/<hash>`；缺失只警告 |
| 0 | 内置兜底 | `BuiltInFallbackSource` | 编译进程序集的占位纹理/空语言；**不是 Minecraft 资产** |

- 优先级数值固定，不依赖注册顺序，避免"接错顺序就静默改变画面"。
- 索引 id 必须与 `version.json` 的 `assetIndex.id` 一致，否则 `OfficialObjectsSource` 拒绝挂载并说明原因。
- **语言特例**：按原版语义（§5 高→低、后写覆盖），合并时**低优先级覆盖高优先级**；我们对 en_us 之外的 locale 采用"索引优先、pack 兜底"的取用顺序，并在实现处注明这与渲染资源的方向相反的原因。

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

## 8. 性能约束

- `TryOpen` 在热路径（每帧可能查很多纹理）上 MUST NOT 为每次查找重建 zip 中央目录：`PackAssetSource` 在构造时读取并缓存条目表。
- 缓存键为 `Identifier`；缓存不持有打开的流（流是短生命周期的）。
- 后续若需要"整批预取"，再引入纹理级缓存，不在本层做。

## 9. 验收标准

- `docs/analysis/assets/resource-resolution.md` 的每条规则都有对应测试；
- 渲染层能在真实数据下取到 `textures/block/stone.png` 与对应 `.mcmeta`；
- 缺失索引资产时只有警告、不崩溃；
- 同一个资源在两个来源都存在时，结果由 §5 的优先级表决定（有测试守护）。
