<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

# 状态

> 跨会话的唯一进度真相源。只记录易变状态：当前里程碑、进行中的模块、阻塞项、下一步具体动作、未提交改动与后台任务。
> 稳定设计属于 `docs/modules/`，索引属于 `docs/README.md`，决策属于 `docs/decisions/`。本文件不超过 150 行（原则 14）。

## 当前里程碑

M5.3 渲染管线。当前焦点：**渲染性能**。

## 已完成

| 任务 | 内容 | 结果 |
| --- | --- | --- |
| T-829 | 参考 OIT 颜色目标改半精度 | `transmittance`×2 与 `accumulate` 由 RGBA32F 改为 **RGBA16F**，与原版一致。水域视角 **6.5–6.7 → 2.12–2.21 ms**（OIT 部分），整帧 **≈268 fps vs 原版 245 fps** |
| T-829 | 雾必须降低透射率 | 原实现只把雾混进颜色，片段透射率仍取 `1-alpha`，浓雾中可透过水看到背后地形。两个 pass 改用同一 `coverage = mix(alpha, 1, fogAmount)` |
| T-831 | 网格缓存保留已卸载区块的网格 | 传送后缓存 **1281 条 vs 世界 637 个区块**，VS 快照显示该字典保留 **613 MB**、一次传送窗口内涨 **225 MB**。两处修复：`DrainUpdates` 插入前校验区块仍在世界、`PruneTo` 与世界对账。修复后 `meshes=637` 与世界一致，8 次传送工作集 **1179 → 1296 MB（+117 MB）**，修复前同操作 +500～900 MB |
| T-830 | 区块多绘制间接合批 | 逐页收集 20 字节参数记录、一次上传、每页一次 `MultiDrawIndexedIndirect`。**实测收益为零**（637 区块/约 460 次可见绘制压成约百批次，`sub-queue` 仍 0.17–0.20 ms）。保留为结构对齐原版，文档已注明不得当作优化引用 |
| T-798 | 区块几何分页共享 arena | 每层若干固定大小页；绘制绑定整页一次、按 `firstIndex`/`baseVertex` 逐区块绘制。稳态 **5.13–5.70 → 2.54–2.76 ms（−51%）**，`binds/frame` 2668 → 278。ADR-0029 |
| T-797 | 默认选高性能 GPU + 可指定 | 原生垫片 + `[UnsafeAccessor]`，默认 `HighPerformance`。`MC_GPU` 支持 `high`/`low`/`list`/名称子串。ADR-0028 |
| T-793 | 首帧请求 44.6 s | 根因：`BuildRequest` 二次网格化。删除第二遍改为消费 `ChunkMeshCache`。**44.6 s → 26 ms**，分配 1.09 GB → 0 |
| T-792 | 窗口只渲染 7–9 个区块 | 根因：预载后 29 秒无帧，服务器 30 秒判死。修复后区块 7 → 496 |

## 进行中

**渲染帧时间归因（续）**：装置已就绪（`MC_FRAME_CPU` 分段计时、`MC_SKIP_OIT` / `MC_SKIP_CLOUDS` / `MC_SKIP_TRANSLUCENT` 三个 A/B 开关、`MC_AB_TOGGLE` 同进程交替、`MC_OIT_STAGES` 逐阶段截断、`tools/verify/measure-frame.ps1`）。

**已确认的帧结构事实**（RTX 3060，非核显，旧核显基线不可比）：
- 水域视角 2534×1334、视距 12、`MC_PRESENT_MODE=immediate`：整帧 **3.72 ms**，其中 OIT **2.12–2.21 ms**（同进程 A/B 实测）
- 该视角**已反超原版**：我们 ≈268 fps vs 原版 245 fps
- **无大面积水域时，渲染性能已全面超越原版**（用户实测确认）。此前记录的「非水域 435–528 vs 原版 545」是误判：该机位画面中仍有水，不能当作无水基准
- 水域视角是最后一块落后原版的地方，现已反超
- 逐阶段成本：depth bounds 1.86–1.93 ms，+cull 2.34–2.40，+transmittance 2.83–2.86（相对整帧）

**已试并证伪的方向（勿重复）**：
- 把 cull pass 折叠进消费者：3.72 → **3.82 ms**，两个消费者各多一次全屏采样
- 多绘制合批降低 CPU 提交：见 T-830，无收益
- 零透射率剔除：我们水域每像素约 1 层水面，无过度绘制可省

## 阻塞项

- **T-807（已解决，无需再做）**：原记录的 0.169 ms 与"每天体每帧一次 `WriteBuffer`"已不成立——T-810/T-812 已把 `UpdateCamera` 改成纯空操作（相机经共享 sky 矩阵生效）、tint 上传加缓存跳过。实测天体各阶段合计 **0.06–0.07 ms**：`cel-camera` **0.000**、`cel-build` 0.037–0.046、`cel-disc` 0.020–0.023、`scene-celestial` 0.002–0.003。剩余成本是**绘制列表构建**，原版每帧同样要算星体旋转，不属可回退的上传开销。**等效优化**（原版即如此）
- **T-802**：全部跳过后的**空帧 1.56 ms 且与分辨率无关**（858×527 与 429×263 差 0.001 ms）。原版整帧 1.6 ms，我们空帧就 1.56 ms。需先解耦 `updates` 与渲染帧、查清 `present` 阻塞
- **T-804**：实测为 14 个 pass，已合并至 12（`submit` −0.043 ms）。由此测得**每个 pass 对象约 0.02 ms**，推翻"多 pass 即大头"的旧推断——**不值得继续砍 pass**
- **T-791**：GPU 区块剔除**实测否决**（总收益 0.4–2.7 ms 中 CPU 循环 <0.05 ms）。`ChunkCuller` 保持停放
- 本地 Fabric 服务器可能自行退出（`Can't keep up` 后进程消失）。测量前先确认存活，重启约 14 s

## 下一步具体动作

1. ~~补 FP16 的等效性证据~~ **已完成**：FP16 与 FP32 同机位逐像素比对，水域最大差 5/255、平均 0.088、>1 的像素 0.015%，无可见色带（见 `specs/render/cloud-geometry.md` §32）。同批复现性能对照 450 vs 385 fps
2. **FP16 验证的天空部分**（可选）：水域已证明无色带，天空与云的差异被云漂移污染，若要严格结论需关云重测一次。

**不要再走的方向**：继续压 OIT（水域已反超，剩余只有降透明层分辨率/降小波阶数这类**会改变画面**的偏差，须按原则 9 由项目所有者裁决）；继续砍 pass 数量（T-804 已证伪）。

## 常用命令

```powershell
dotnet build Sharpcraft.slnx
dotnet test Sharpcraft.slnx --no-build
dotnet run --project tools/Sharpcraft.ClassMap -- --check
dotnet run --project tools/Sharpcraft.Docs.Indexer -- --check --check-coverage
pwsh -NoProfile -ExecutionPolicy Bypass -File tools/verify/check-simplifications.ps1
pwsh -NoProfile -ExecutionPolicy Bypass -File tools/verify/check-diagnostics-separation.ps1
```

窗口启动（先杀进程，避免 DLL 锁导致构建静默失败；**性能测量必须用 `-c Release`**，Debug 关闭优化会让结果虚高 1.4–3.3 倍）：

```powershell
Get-Process -Name 'Sharpcraft.Client' -ErrorAction SilentlyContinue | Stop-Process -Force
$pack=(Get-ChildItem "$env:LOCALAPPDATA\Sharpcraft\packs" -Filter 'minecraft-26.3-*.pack.zip' | Select-Object -First 1).FullName
$dbg="$env:TEMP\dbg.json"
'{"MC_CULL_STATS":"1","MC_VIEW_DISTANCE":"12","MC_PRESENT_MODE":"immediate","MC_FRAME_CPU":"1"}' | Set-Content $dbg -Encoding UTF8
src/Sharpcraft.Client/bin/Release/net10.0/Sharpcraft.Client.exe --version 26.3 --gameDir "$env:APPDATA\.sharpcraft" --pack $pack --offlineDeveloperMode --username Test --quickPlayMultiplayer 127.0.0.1:25565 --width 2534 --height 1334 --debugOptionsFile $dbg
```

**配置传递规则（原则 13）**：游戏代码只读宿主传入的 `--debugOptionsFile` 快照，**不读环境变量**。用 env 设置 `MC_*` 会静默无效。

**测量纪律**：跨运行比较不可靠（同一场景不同运行差 10%+ 是常态），必须用 `MC_AB_TOGGLE` 同进程 A/B 或 `MC_OIT_STAGES` 逐阶段截断；任何性能数字都要标明构建配置、分辨率、视距与机位。

## 已完成：T-833 遮挡剔除（默认开启）

**结果**：三角形 1,157,844 → **257,366（−78%）**，帧 1.83 → **1.76 ms**。修复四个缺陷后才成立：重算只跟相机走（第一帧残缺集合用到结束）、`visited` 单字节锁死入口面、单调规则丢了防 U 型弯、最高 section 越界一层。关键优化是按区块列缓存，把每帧 35,000 次字典查找压到 ≤625 次。

**剩余**：网格流入触发的重算尚未节流；2×2×2 宏块方案经评估否掉（浅层洞穴会漏网）。帧时间基线见 `docs/specs/render/frame-baseline.md`，其中记录了三条已证伪的归因，**勿重复调查**。

目标：地下场景实测每帧绘制 **1,512,472 个三角形**（`drawn=282`），其中大片被岩石挡住。视距 4→12 时帧时间 1.68→3.44 ms，证明是几何/片元瓶颈，遮挡剔除有真实空间。

**已完成并有测试**（分支 `vertex-24b`）：面对矩阵计算（`SectionVisibilityCompute`）、逐帧遍历（`SectionOcclusionTraversal`）、网格化时产出矩阵、状态映射双基准、绘制路径接线。开关 `MC_OCCLUSION=1`，默认关闭。

**顺带修掉的两个真 bug**：
- `ChunkWorkerPool` 未给 `ToMeshData` 传 section 起点，异步路径全部网格声称从 section 0 开始（世界是 −4），查找偏移 4 层
- 遍历的单调规则累积路径上所有方向并禁其反向，把 600 个区块列压到 17 个；已改为只禁止从刚进入的面直接退回

**排查过程留档**（一开始的失败版本，四个缺陷修复前的证据链）：

| 检查 | 实测值 | 结论 |
| --- | --- | --- |
| 网格 `SectionMinY` | `-4`，矩阵数 24 | 正确 |
| 相机高度矩阵 | `0xFFFFFFFFF`（全连通） | 正确 |
| `isLoaded` | 周边全 `True` | 正确 |
| 半径 | `12`，与世界视距一致 | 正确 |
| 查询侧命中 | `kept=417,523` 与遍历 `kept=419` 一致 | 映射正确 |

**当时的症状**：遍历只保住 419 个 section，纵向走满、横向仅 ±2。根因是 `Refresh` 只在相机跨段时重算，第一次算时世界只有几十个区块。
## 其它已登记待办

| 任务 | 内容 | 状态 |
| --- | --- | --- |
| T-827 | 云改程序化生成 S4/S5（OIT 三阶段接新顶点、删旧顶点路径） | S1–S3 完成；S4/S5 待做，见 `specs/render/cloud-geometry.md` §10 |
| T-781 | Ctrl+F2 全景截图 | 未开始 |
| T-764 | 剩余 special 类型 | 未开始 |
| — | `spriteRect` 16 B 移除（44 → 28 B 对齐原版） | 需先做图集 UV 烘焙与动画纹理 |
| — | `ChunkGeometryStore` 页只增不减，工作集 24 秒内 1296 → 1354 MB | 属正常驻留，非泄漏，见下条 |
| T-831 | 移动时的内存增长 | **已定位并修复**（见「已完成」）。方法留档：`dotnet-gcdump`/`dotnet-dump` 两份对比 + VS 探查器差异视图。**教训**：`dotnet-dump` 的 `-min` 过滤只能看大对象，会漏掉整片中小对象；字典类容器的「浅表大小」极小（46 KB）但保留大小可达 613 MB，判断必须看**保留大小**而非浅表 |

最后更新：2026-11-12
