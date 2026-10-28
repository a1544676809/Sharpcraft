<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

状态：[已评审]
基线：Minecraft 26.3
证据：`docs/analysis/assets/jar-entry-map.md`（来自 `ref://mc-26.3-analysis/entries.csv`）
最后更新：2026-10-29

# 资产管线（Assets）

把用户本地的原版 jar 变成客户端可消费的只读资源目录，且**仓库内不出现任何资产**（原则 3）。

## 1. 职责范围

负责：

- 版本与来源校验：确认 jar 属于 26.3 基线（SHA-1 比对），不匹配时给出可操作报错。
- **打包**：把白名单类别（模型、纹理含 `.mcmeta`、blockstates、items、字体、shaders）打进**单个 pack 容器**，跳过 `net/`、`data/`、`com/`、`META-INF/`（ADR-0005）。
- **索引挂载**：音频等走官方资产索引的类别，直接只读挂载官方 `objects/`，**不复制、不转存**。
- 资源解析：以 `IAssetSource` 统一两个来源，定义覆盖顺序（模组包 > 基础 pack > 官方 objects > 内置兜底）。
- 产物清单：pack 内含 `manifest.json`（条目 → 源条目、长度、SHA-256、提取时间），pack 文件名带自身哈希。

明确不负责：

- 下载 jar（属于启动器的版本获取职责）。
- 解析模型/纹理语义（属于资源加载层与渲染层）。
- 模组资产的合并策略实现（只提供覆盖接口点）。
- 索引资产的**补全下载**：是否允许由启动器从官方端点补齐，属待决项（ADR-0005 待决 1）。

## 2. 侧别归属

- 归属：**外围宿主层**（与启动器、认证同层），不是 `Client`/`Server`/`Common` 的任何一侧。
- 判定依据：原则 5 把"资产管线"明确列在外围宿主层，且游戏逻辑不得读取 jar。

## 3. 依赖关系

| 方向 | 对象 | 说明 |
| --- | --- | --- |
| 依赖 | `System.IO.Compression` | 读取 zip 条目，无需第三方库 |
| 依赖 | `System.Security.Cryptography` | SHA-1 校验（与官方清单同算法） |
| 被依赖 | 启动器（`launcher assets` 子命令） | 唯一的调用入口 |
| 被依赖 | 资源加载层（只读消费输出目录） | 见 `docs/modules/render.md` §7 |
| 不依赖 | `Client` / `Server` / `Framework` | 宿主层不得被游戏逻辑引用（原则 5） |

## 4. 对外契约

- `VersionManifest.ReadFile(path)` / `Parse(json)`：版本 `id`、`assetIndex.id`、`downloads.client.sha1`；缺字段或 JSON 无效时报可诊断错误。
- `JarVerifier.Verify(path, expectedSha1, expectedSize)`：流式 SHA-1，返回实际哈希或抛 `JarVerificationException`（含期望/实际值，不提供绕过开关）。
- `AssetPackBuilder.Build(jarPath, outputDirectory, versionId, expectedJarSha1)` → `AssetPackResult`（条目数、字节数、pack 路径、pack SHA-256、耗时）；输出目录在仓库内时拒绝执行。
- `AssetPackBuilder.ReadManifest(packPath)` → `AssetPackManifest`：读回 pack 内的清单。
- `AssetSelection.IsIncluded` / `ExcludedCategory` / `IsSafeEntryName`：白名单、跳过分类与 zip-slip 防护。
- `HostPaths.DefaultPacksDirectory()` / `IsInsideRepository(path)`：仓库外输出目录规则。

## 5. 输出布局（全部在仓库外）

```
<userData>/Sharpcraft/
  packs/
    minecraft-26.3-<packSha256 前 12 位>.pack.zip     # 单个包，内含 manifest.json 与资产条目
```

条目索引缓存（原设计里的 `cache/pack-index/`）尚未实现：当前每次构建直接读 jar 并重算哈希。

官方索引资产不进上述目录：运行时直接读用户机器上的 `assets/indexes/<id>.json` 与 `assets/objects/<前两位>/<哈希>`，只读挂载。

**pack 容器格式**（zip）：

| 组成部分 | 内容 |
| --- | --- |
| `manifest.json` | 基线版本、jar 的 SHA-1、生成时间、条目列表（`name`、`sourceEntry`、`size`、`sha256`） |
| payload 条目 | **保留原始层级**：`assets/minecraft/textures/block/stone.png`，与 jar 条目名一一对应（ADR-0005 裁决 2） |
| 容器级校验 | 文件名内嵌 pack 自身 SHA-256 前 12 位；另在 manifest 记录完整哈希 |

- 打包 MUST 原子：先写 `.tmp`，校验通过后重命名；中断不产生可用半成品。
- 已存在同名 pack 时直接复用，不重建（同名即同内容）。
- 模组包与调试用的散装目录可作为**额外来源**挂载在基础 pack 之上（优先级更高），但基础 pack 本身不散装。
- **语言文件来源顺序**：官方索引优先（多语言齐全），pack 内的 `en_us` 作兜底。

## 6. 关键行为

| 场景 | 期望行为 |
| --- | --- |
| jar 缺失 | 明确报错，指出期望路径与获取方式（原则 3：由用户自备或官方分发） |
| SHA-1 不符 | 拒绝提取，报出期望/实际哈希与"版本不符"的结论，不提供 `--force` 绕过 |
| 条目缺失（例如该版本无某目录） | 记入报告并继续，不视为错误 |
| 重复运行 | 已存在同名 pack（哈希一致）则直接复用；jar 变化则重建整包 |
| 提取中断 | 下次运行可续；不产生半写文件（先写临时文件再原子替换） |
| 输出目录在仓库内 | 拒绝执行 |
| `.mcmeta` 与 `.png` | 必须成对进入 pack，不得只取 `.png` |
| 索引资产缺失 | **只警告不阻断**（ADR-0005 裁决 1）：打印缺失项、期望路径与索引 id 后继续运行 |
| `net/`、`data/`、`com/`、`META-INF/` | 一律跳过，并在报告中记录跳过数量 |

## 7. 测试策略（不依赖真实 jar）

1. **合成 jar**：测试内用 `ZipArchive` 现场构造含若干条目的 zip，写盘到临时目录，再用已知 SHA-1 走完整链路。
2. **篡改场景**：改一个字节后校验必须失败；报错信息必须包含期望/实际哈希。
3. **幂等性**：连续两次提取，第二次报告"新增 0 / 跳过 N"，且产物修改时间不变。
4. **白名单**：构造含 `net/`、`data/` 条目的合成 jar，断言这些条目**从未**出现在输出目录。
5. **成对提取**：断言 `.png` 与 `.mcmeta` 同进同出。
6. **路径安全**：条目名含 `..` 或绝对路径时拒绝（zip-slip 防护）。
7. **pack 读取**：写入后按 manifest 逐条读回，字节与源条目一致；缺失条目与哈希不符必须报错。
8. 真实验收（手工）：用用户提供的 26.3 jar 跑一次，核对 `jar-entry-map.md` 的数量级（如 `textures/block` 约 1,406）。

## 8. 未决问题

1. **（已关闭）基线版本的索引 id**：26.3 为 `34`，与官方 `version_manifest_v2.json` 中该版本的 `assetIndex.id` 一致（2026-10-29 复验）。
2. **索引下载补齐**：已裁决不做（只读 + 警告），若后续需要再立 ADR。
3. **签名验证**：jar 带 `MOJANGCS.SF`；当前结论是不必要，SHA-1 已足够判定版本。
4. **着色器是否进 pack**：当前取"进 pack 但不参与运行时加载"，仅作行为参考。

## 9. 验收标准

- `launcher assets --jar <path>` 能完成校验与提取并打印报告；
- 上述 7 项测试全绿（真实验收除外）；
- `git status` 中不出现任何资产（提取产物目录在仓库外，且被 `.gitignore` 覆盖）；
- 输出目录可被 `Client/Assets` 的资源栈直接读取（T-034 / T-038）。

## 10. 实测（真实 26.3 jar）

`launcher assets <jar> <version-json>`，输入为本机 26.3 客户端（41,483,720 字节，SHA-1 `e877b6a0…e7b87`，与基线一致）、26.3 版本清单（assetIndex 34）。

| 项 | 值 |
| --- | --- |
| 打入 pack 的条目 | **11,533** |
| pack 大小 | 5.6 MB（5,854,134 字节） |
| pack 自身 SHA-256 | `a51bffa6a64c…` |
| 构建耗时（冷） | 768 ms |
| 复用耗时（热） | 164–181 ms（只校验清单与哈希） |
| 跳过 | `net` 11,267、`data` 10,449、`com` 690、`META-INF` 5 |
| 内容核对 | 3,964 个 `.png`、218 个 `.mcmeta`，条目路径全部保留 `assets/minecraft/…` |
| 仓库保护 | 输出指向仓库内时拒绝执行，且**不留任何目录** |

2026-10-29 复验：从官方 `piston-meta` 重新取 26.3 清单与 jar（同一 SHA-1），重建 pack 耗时 681 ms，条目数与 pack 字节数、SHA-256 与上表逐字节一致；解析探针：`texture block/stone` 151 字节、`model block/stone` 98 字节、图集 1024×1024 / 1301 精灵 / 74 ms。官方索引 `34` 在本机未安装，故语言/音频走索引的探针跳过（只警告）。

