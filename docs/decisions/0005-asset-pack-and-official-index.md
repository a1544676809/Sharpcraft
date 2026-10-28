<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 MinecraftCSharp contributors -->

状态：[已接受]
日期：2026-10-28

# ADR-0005：jar 内置资产打包为单文件容器，音频沿用官方资产索引

## 背景

`docs/modules/assets.md` 初稿把 jar 内资产**按目录散装**解到用户数据目录（`assets/<版本>/textures/block/...`，共 11,813 个文件）。权衡后发现三个问题：

1. **小文件过多**：绝大多数是几百字节到几 KB 的 PNG/JSON，散装会让文件系统、杀软与云同步在每次启动扫描时付出明显代价；
2. **原子性与完整性**：散装目录难以整体校验与原子替换，升级基线或提取中断会留下"半新半旧"的目录；
3. **音频本来就不在 jar 里**：条目清单无 `sounds/` 与 `sounds.json`，`lang/` 只有 3 条（`docs/analysis/assets/jar-entry-map.md` §4）。若连音频也要提取，就得另起下载链路，而那批文件在用户机器上通常**已经存在**（官方启动器下载的 `assets/objects/…`）。

## 决策

1. **jar 内置资产 → 单文件包（pack）**，不再散装。
   - 容器：**zip（deflate）**，内含 `manifest.json`（条目 → 源条目、长度、SHA-256、提取时间）与 payload 条目。
   - 命名与位置：`<userData>/MinecraftCSharp/packs/minecraft-<基线版本>-<packSha256 前 12 位>.pack.zip`，仓库外。
   - 原子发布：先写 `*.tmp`，校验通过后重命名；pack 名含自身哈希，因此"同名即同内容"。
2. **音频（以及经实测确认走索引的其它类别）沿用官方资产索引**，不复制、不转存。
   - 读取官方 `indexes/<id>.json` + `assets/objects/<前两位>/<哈希>`，只读挂载。
   - 索引 id 与 jar 的 `version.json` 中 `assetIndex.id` 比对，不匹配即拒绝（防版本错配）。
   - 缺失时给出可操作报错；**是否允许启动器从官方端点补全见"待决"**。
3. 资源解析统一走 `IAssetSource` 抽象，两个来源并列：`PackAssetSource`（第 1 条）与 `OfficialObjectsSource`（第 2 条）。解析顺序：模组包 > 基础 pack > 官方 objects > 内置兜底（占位纹理）。
4. **仓库内仍然不得出现任何资产**（原则 3 不变）：pack 与 objects 都在仓库外，`.gitignore` 兜底，提取器拒绝仓库内输出路径。

## 备选方案

| 方案 | 优点 | 缺点 | 结论 |
| --- | --- | --- | --- |
| **单文件 zip pack + 官方索引只读挂载** | 一文件搞定、可整体校验与原子替换、可用现成工具查看、音频零复制 | zip 无逐段校验（需自带 manifest）；单条目变更要重建整包 | **采纳** |
| 散装目录（初稿） | 可直接浏览、增量最细 | 上万小文件、难校验、难原子替换 | 否决 |
| 自研二进制容器（索引 + 分段压缩） | 随机读最优、可逐段哈希 | 需自做工具与调试路径、无现成查看器 | 暂不采纳（实测成瓶颈再换） |
| 连音频一起打包进 pack | 单一来源 | 需从官方端点下载并复制用户已有数据，体积翻倍且无收益 | 否决 |

## 后果

- **正面**：启动只打开 1 个文件；pack 内容用一个哈希即可判定；音频与官方启动器共享同一份磁盘数据（省空间、天然复用）。
- **负面**：单资源变更要重建整包（当前只在基线变更时发生）；调试需 `launcher assets extract <条目>` 一类工具；随机读多一次"打开容器 + 定位条目"的成本（zip 中央目录可缓存）。
- **影响其它文档**：`docs/modules/assets.md` §5 输出布局重写；`docs/analysis/assets/jar-entry-map.md` §3 资源来源分两类；`docs/milestones/roadmap.md` 的 M3 任务拆分调整（原 T-034 资源加载层要加 pack 读取器）。

## 已裁决（2026-10-28，所有者确认）

1. **索引资产只读，缺失只提示不阻断**：不做下载器；缺失时打印警告（期望路径 + 索引 id）后继续运行，表现为无音效/仅英文。
2. **pack 内路径保持原始层级**：条目名保留 `assets/minecraft/…`，与 jar 一一对应，便于对照与手工检查，不做扁平化。

## 实测证据（2026-10-28）

本机 `E:/games/minecraft/.minecraft/assets`：

| 项 | 值 |
| --- | --- |
| 26.2 的 `assetIndex.id` | `32` |
| 索引 SHA-1 | `d6540f46a8fec49e45fb7312cdcf401a8e4e15ff` |
| 索引条目 / 总量 | 5,057 条 / 约 458 MB |
| 索引内含 | `sounds/…`、`sounds.json`、`lang/<locale>.json`（多语言齐全） |
| 索引不含 | `textures/block/…`（0 条，纹理只在 jar 内） |

这直接支撑"音频与语言走索引、纹理与模型打包"的划分。

## 复查条件

- 实测显示"打开 pack + 随机读"成为启动或纹理加载瓶颈时，重新评估容器格式（备选三）。
- 若官方资产的目录布局在后续版本改变，复查 `OfficialObjectsSource`。
