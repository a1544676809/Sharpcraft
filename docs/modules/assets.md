<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 MinecraftCSharp contributors -->

状态：[待评审]
基线：Minecraft 26.3
证据：`docs/analysis/assets/jar-entry-map.md`（来自 `ref://mc-26.3-analysis/entries.csv`）
最后更新：2026-10-28

# 资产管线（Assets）

把用户本地的原版 jar 变成客户端可消费的只读资源目录，且**仓库内不出现任何资产**（原则 3）。

## 1. 职责范围

负责：

- 版本与来源校验：确认 jar 属于 26.3 基线（SHA-1 比对），不匹配时给出可操作报错。
- 按清单提取：只解出白名单类别（方块/物品模型与纹理、blockstates、语言、字体、shaders），跳过 `net/`、`data/`、`com/`、`META-INF/`。
- 输出目录布局：稳定、可文档化、可被模组覆盖。
- 增量与幂等：重复运行只补齐缺失或过期项，不重写已是最新的产物。
- 产物清单（manifest）：记录每个产物的来源条目、大小、哈希，供资源加载层与诊断使用。

明确不负责：

- 下载 jar 或官方资产索引（那是启动器的版本/依赖获取职责，按原则 8 归属启动器）。
- 解析模型/纹理语义（属于资源加载层与渲染层）。
- 模组资产的合并策略实现（只提供覆盖接口点）。

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

- `AssetPaths`：输出目录布局常量（见 §5），资源加载层与测试共用同一份定义。
- `JarVerifier.VerifyAsync(path, expectedSha1)`：返回校验结果或带修复建议的错误。
- `AssetExtractor.ExtractAsync(jarPath, outputRoot, options)`：返回 `ExtractionReport`（新增/跳过/过期项计数与耗时）。
- `AssetManifest`：产物清单的读写；每项含 `path`、`sourceEntry`、`size`、`sha256`、`extractedAt`。

## 5. 输出目录布局（仓库外）

```
<userData>/MinecraftCSharp/assets/<基线版本>/
  blockstates/…
  models/block/…  models/item/…
  textures/block/…  textures/item/…  textures/entity/…  textures/gui/…  （含同名 .mcmeta）
  items/…
  lang/…
  font/…
  shaders/…            # 仅作行为参考，不参与运行时加载
  manifest.json        # 产物清单
```

- 布局与 jar 内 `assets/minecraft/` 的相对路径一一对应，减少映射歧义。
- 命名空间目录保留（当前只有 `minecraft`，为模组命名空间预留同名结构）。
- 该目录 MUST 位于仓库之外，且启动器启动时检查"输出目录不得在仓库内"（防止误配污染仓库）。

## 6. 关键行为

| 场景 | 期望行为 |
| --- | --- |
| jar 缺失 | 明确报错，指出期望路径与获取方式（原则 3：由用户自备或官方分发） |
| SHA-1 不符 | 拒绝提取，报出期望/实际哈希与"版本不符"的结论，不提供 `--force` 绕过 |
| 条目缺失（例如该版本无某目录） | 记入报告并继续，不视为错误 |
| 重复运行 | 已存在且大小/哈希一致的产物跳过；仅补齐缺失与变化项 |
| 提取中断 | 下次运行可续；不产生半写文件（先写临时文件再原子替换） |
| 输出目录在仓库内 | 拒绝执行 |
| `.mcmeta` 与 `.png` | 必须成对提取，不得只取 `.png` |
| `net/`、`data/`、`com/`、`META-INF/` | 一律跳过，并在报告中记录跳过数量 |

## 7. 测试策略（不依赖真实 jar）

1. **合成 jar**：测试内用 `ZipArchive` 现场构造含若干条目的 zip，写盘到临时目录，再用已知 SHA-1 走完整链路。
2. **篡改场景**：改一个字节后校验必须失败；报错信息必须包含期望/实际哈希。
3. **幂等性**：连续两次提取，第二次报告"新增 0 / 跳过 N"，且产物修改时间不变。
4. **白名单**：构造含 `net/`、`data/` 条目的合成 jar，断言这些条目**从未**出现在输出目录。
5. **成对提取**：断言 `.png` 与 `.mcmeta` 同进同出。
6. **路径安全**：条目名含 `..` 或绝对路径时拒绝（zip-slip 防护）。
7. 真实验收（手工）：用用户提供的 26.3 jar 跑一次，核对 `jar-entry-map.md` 的数量级（如 `textures/block` 约 1,406）。

## 8. 未决问题

1. **音效与语言的来源**：条目清单里没有 `sounds/`、`sounds.json`，`lang/` 只有 3 条；推测走官方资产索引（`indexes/*.json` + `objects/<hash>`）。这属于**启动器的版本获取职责**，资产管线只负责"拿到本地文件后的提取"。需实测确认后再决定是否扩展本文档。
2. **签名验证**：jar 带 `MOJANGCS.SF`，是否需要额外校验签名（当前结论：不必要，SHA-1 已足够判定版本）。
3. **着色器是否提取**：当前取"不参与运行时加载"，仅作为行为参考；若将来做原版管线对照再议。

## 9. 验收标准

- `launcher assets --jar <path>` 能完成校验与提取并打印报告；
- 上述 7 项测试全绿（真实验收除外）；
- `git status` 中不出现任何资产（提取产物目录在仓库外，且被 `.gitignore` 覆盖）；
- 输出目录可被资源加载层直接读取（下一里程碑 T-036 消费）。
