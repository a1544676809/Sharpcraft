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
| T-792 | 窗口只渲染 7–9 个区块 | 根因：预载后 29 秒无帧，服务器 30 秒判死。修复后区块 7 → 496 |
| T-832 | 地形顶点压缩到 24 字节 | 位置 + Unorm8x4 颜色 + Float16x2 UV + 打包（精灵矩形/天空光/方块光），44 → 24 字节（−45%）。离线 `--frame` 同机位逐像素比对**最大差 0**。**不是帧时间优化**（顶点带宽非瓶颈） |
| T-834 | OIT pass 削减 | 8 个 OIT pass **不可合并**（有真实依赖链）；剔除与云边界两个 pass 合并成功（剔除管线 `CompareFunction.Always` 且不写深度，深度附件无语义）。省 1 pass ≈0.023 ms，**低于跨运行噪声**。云分支未被离线帧覆盖，已如实记录 |
| T-835 | 雾的等距面与距离 | 四个缺陷：① 雾距离取 `clip.w`（视空间平面）而非径向 → 低头看水面是**直线**、随视角转；② 雾色向天空收敛的单位错（`skyFogEnd` 应为方块数，`clampedLerp` 输入已除过 32）→ 雾像独立雾幕；③ 三个着色器的 `Camera` 结构体**漏声明 `skyViewProjection`**，`eye` 落到字节 64 读到天空矩阵 → 整片糊白（根因）；④ 起止距离硬编码 64/160 而非按视距推导。现为 `[172.8, 192]` 柱形壳，与原版 `fog_cylindrical_distance` 一致。大气壳未接，见 `known-gaps.md` |

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

**分支**：`frame-pipeline` @ `ec8479e`，领先 `origin/main` **67 个提交未推送**。项目所有者已明确：**暂时不推送。**

### 验证手段的覆盖范围（必须记住）

**离线 `--frame` 与沙盒 `MC_FRAME_LOCAL` 只渲染 `OffscreenRenderer`，完全不经过 `WindowedRenderer`。** 两者是平行实现（原则 14 的单轨尚未落地，见蓝图第四步）。

因此：

离线帧/沙盒对拍对 `OffscreenRenderer` **有效**；对 `WindowedRenderer` 及其组件**无效**（哈希必然不变），只能靠运行时冒烟（帧时间、剔除统计、截图）。本会话给 9 个组件抽取跑的哈希判据因此无效，真正验证它们的是冒烟（1.920 ms、`culled=412 drawn=256`）。


### 沙盒验证法（本轮建立，所有画面改动都用它）

```
MC_FRAME_LOCAL=1 + --frame  →  RenderLocal → BuildDemoWorld
```

不连服务器、**两次哈希完全相同**。改动前后对拍可量化：像素差比例 / 最大通道差 / 差异区域。T-841 就是这样确认的（7.82% 像素、最大差 10/255、区域恰为岛屿几何）。

**连服务器的离线帧不能用来验证画面细节**：该帧几乎全是天空，地形着色器覆盖像素极少，连"把大气雾终点压到 32"这种极端值都测不出变化。本轮因此浪费了两轮。

单独覆盖项：`MC_FORCE_RAIN` / `MC_FORCE_THUNDER` 可强制雨量与雷暴，便于验证天气相关画面。

### 已完成（本轮）

T-837 录制区抽取（9 组件，`WindowedRenderer.cs` 2400 → 2065）；**3A 完成（本片）**：新增 `MC_FRAME_HASHES=N`（宿主解析进 `FrameDiagnostics.FrameHashes`）+ `FrameHashTrace.cs`，每帧回读颜色目标打印 sha256；钉住配置（`MC_FRAME_LOCAL`/`MC_FORCE_DAYTIME=6000`/`MC_FORCE_PARTIAL=0.5`/`MC_AB_TOGGLE=5`/`MC_SKIP_SKY=1`，见 `frame-pipelining.md` §12）两次运行 20 帧序列**逐一相同**、`digest 2eb9f706…`、`distinct=2`，翻转 `MC_SKIP_SKY` 哈希即变，判据可用；连服务器配置 `distinct=20`、两次 digest 不同（世界状态漂移），已记录不作判据。T-838/T-839 两个潜在缺陷修复并补回归测试；T-840 删除死 `LightmapParameters` 管线；T-841 大气雾壳（含量化验证）。测试 2718 全绿。
**T-843 完成（本轮）**：切片 1（`FrameExecutor.Record` + `FrameTarget` 参数化，窗口 1975→1663）→ 2A（4 委托删除，OIT 入 `FrameOitTargets.cs`，窗口→1469）→ 2B-1（管线/layout 集中入 `FrameResources.cs`、10 工厂入 `FramePipelines.cs` 264 行，窗口→1254）→ 2B-2a（`FrameResources` 升格唯一装配点：`Options`/`ExecutorResources`/`CreateFrame`/`Resize`，窗口→**1126**）→ **2B-2b（本片）**：`OffscreenRenderer` 删 L577–L732 录制块与 5 个重复管线工厂，改为构造 `FrameResources`→`FrameExecutor.Record`→回读，`OffscreenRenderer.cs` **1533→1346**（−187）；`FrameExecutor.cs`/`FrameRequest` 契约未改；`Repro_001` 断言随代码位置调整、用例保留。全量静态门全绿（build 0 警告、**2718 测试**、ClassMap/Docs/两 verify）；**沙盒帧（仅 `{"MC_FRAME_LOCAL":"1"}`、640×360）`sha256 9CA9E03410A2E745…`/34897 B，与基线逐字节相同**（`identical=True`），未做任何补偿；`GlintTime==0` 的时间戳 padding 只被 `fs_entity_glint` 消费、实测不改像素。服务器 `--frame`（带半透明）`check-frame-baseline.ps1` 跑通（14892 B、EXIT=0、无 Validation），哈希变为 OIT 合成（原单次 blend）+ DrawRanges 合并 + cutout 换 pass，因服务器帧不确定**无法逐项分离**，已在规格 §5 如实记录。Release 冒烟稳态中位数 **1.63 ms ≤2.2**、`cull: chunks=637 culled=449 drawn=183`、EXIT=0、无异常。**架构发现（规格 §5）**：离屏地形走 `EntityPassRecorder` 的 static-world fallback 而非 `TerrainPassRecorder`，两侧不同构。**剩余小项**：`FrameResources` 仍为离屏多建一份未使用的深度纹理（不影响像素）；后续可清。离线帧哈希对窗口改动不敏感，勿当判据。

### 下一步候选

1. **雨雾**。规格与全部常量已写入 `docs/specs/render/lighting.md` §11.1（`rainFogMultiplier` 逐刻插值 + `environmentalStart/End` 计算）。实现需改 `fogPerFrame` 回调契约（当前只回颜色，需加环境范围），约 5 文件。**验证钩子已就绪**：`MC_FORCE_RAIN=1` + 沙盒对拍。
2. **T-837 切片 3B（真正换线程）**：3A 判据已建（`frame-pipelining.md` §12），3B 待做；上限约 −27%（1.92 → 1.39 ms），双缓冲与延迟释放已就位。必须用 §12 钉住配置验哈希序列逐一相同，再做同进程 A/B 看 `pipeline-wait`；若无实质收益按 §9 回滚。上限注意：`MC_FRAME_LOCAL` 判据不覆盖 arena 参数页双缓冲。
3. 雾基色的 `FOG_COLOR` 混合。**注意**：`skyColorMixFactor` 在视距 12 时为 0，故该项在当前默认配置下**不产生差异**，仅在低视距时才有意义——优先级低。
4. 其余：T-827 云 S4/S5、T-781 Ctrl+F2 全景、T-764 剩余 special renderer、夜间亮度定标。

**不要做**：继续抽 `PrepPump`（42 行）与 resize 块——耦合 `surface`/`depthTexture`/`blitDepthBindGroup` 等大量 by-ref 状态，风险高于收益。

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

最后更新：2026-11-13
