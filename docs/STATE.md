<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

# 状态

> 跨会话的唯一进度真相源。只记录易变状态：当前里程碑、进行中的模块、阻塞项、下一步具体动作、未提交改动与后台任务。
> 稳定设计属于 `docs/modules/`，索引属于 `docs/README.md`，决策属于 `docs/decisions/`。本文件不超过 150 行（原则 14）。

## 当前里程碑

**方块实体与方块模型几何**（T-759 / T-041）。渲染性能的历史结论已归档进 `docs/traceability.md` 的 T-83x 行，本文不再复述。

## 已完成

| 任务 | 内容 | 证据 |
| --- | --- | --- |
| T-041 | **箱子渲染全维度**：独立绘制段 + 专属图集（22 贴图）+ 三盒几何 + 朝向 + 双箱左右半 + 材质变体（含蜡封映射） | 16 条几何真值 + 10 条材质路径；默认沙盒帧逐字节不变 |
| T-759 | **方块事件通道**：`block_event` 协议早已解析，补的是状态层——`BlockEntityAnimations` 按位置索引，每 tick 走 0.1，10 tick 行程 | 8 条控制器测试 |
| T-759 | **NBT 类型化 schema**：`BlockEntityData.TryReadSign` 读 `front_text`/`back_text`/`is_waxed`；富文本复用已有 `ComponentRuns.Flatten` | 5 条读取测试 |
| T-759 | **方块模型几何**：`elements` 解析 → 四边形生成 → 接入 `ChunkMesher`（element 方块绕过贪婪合并，单独发射） | 真实资产对拍 torch 6 / stairs 11 / flower_pot 22 面，逐值 MATCH；三角形增量精确对应 |

### 关键教训（本会话真实踩过）

- **`block/cube` 本身就是满立方体 `elements`**。不能用"有 elements"作为走 element 路径的判据，否则全部 2883 个模型绕过合并、世界变空。判据是 `IsPlainCube`（单元素 + 0..16 + 六面全 `cullface`）。
- **像素"有变化"不等于目标物体渲染了**。火把场景 120 像素变化其实是新方块遮挡了平台顶面；火把与花盆像素完全相同才是真相——两个模型都不可见。**必须用三角形增量这种结构量交叉验证**。
- **`BlockSpriteMap.Lookup` 会自己拼 `block/` 前缀**。`ForTexture` 传 `block/torch` 导致 `block/block/torch`，从切片 3c 起 element 路径一直什么都没画，直到 `f4c7fe5` 修复。

## 进行中

无。上一项（定位 element 路径不可见）已闭环。

## 阻塞项

| 任务 | 卡点 | 需要什么 |
| --- | --- | --- |
| T-107 | 文本断言原验证文本缺失，5 次尝试未复现 | 原始验证文本 |
| — | element 路径的**原版像素对拍** | 可靠的 vanilla 参照画面获取手段（目前没有） |

## 下一步具体动作

**分支**：`special-block-rendering` @ `f4c7fe5`，**本地领先 `main` 47 个提交、未推送**（只在明确下令时推送）。工作区干净，2812 测试全绿，五项门禁全过。

按价值排序：

1. **有/无方向着色的定量对比**。着色代码已生效但**从未单独测量**——`ddd64d2` 时的"零变化"是因为几何体根本没画，不是着色无效。用 `MC_FRAME_MODELS=1/2` 场景，临时把 `Shade` 全改 1 对比。
2. **`cullface` 剔除**（性能 + 正确性）。目前 element 面不做剔除，多画不可见三角形。
3. **`multipart` 与属性驱动变体**。解锁台阶上下半、楼梯朝向、栅栏/墙连接。这是"模型按方块缓存而非按状态"这条简化的解法。
4. **元素旋转的 `rescale`**（303 个旋转元素中声明 `rescale` 的部分）。

**不要做**：在没有 vanilla 参照画面的前提下声称"像素等效"。

### 沙盒验证法（所有画面改动都用它）

```
MC_FRAME_LOCAL=1 + --frame  →  RenderLocal → BuildDemoWorld
```

默认场景基线：**`9CA9E03410A2E745` / 34897 B**（2045 方块 / 2392 三角形）。任何画面改动前后必须对拍。

单方块覆盖项：`MC_FRAME_BLOCK_ENTITIES=1`（箱子，state 5562 朝东）、`MC_FRAME_MODELS=1`（火把）、`MC_FRAME_MODELS=2`（花盆）。

**连服务器的离线帧不能用来验证画面细节**：几乎全是天空，地形着色器覆盖像素极少。

## 常用命令

```powershell
dotnet build Sharpcraft.slnx
dotnet test Sharpcraft.slnx --no-build
dotnet run --project tools/Sharpcraft.ClassMap -- --write   # 或 --check
dotnet run --project tools/Sharpcraft.Docs.Indexer -- --write  # 或 --check --check-coverage
pwsh -NoProfile -ExecutionPolicy Bypass -File tools/verify/check-simplifications.ps1
pwsh -NoProfile -ExecutionPolicy Bypass -File tools/verify/check-diagnostics-separation.ps1
pwsh -NoProfile -File tools/verify/check-block-model-geometry.ps1   # 需 MC_ANALYSIS_ROOT
```

窗口启动（**性能测量必须 `-c Release`**，Debug 关闭优化会让结果虚高 1.4–3.3 倍）：

```powershell
Get-Process -Name 'Sharpcraft.Client' -ErrorAction SilentlyContinue | Stop-Process -Force
$pack=(Get-ChildItem "$env:LOCALAPPDATA\Sharpcraft\packs" -Filter 'minecraft-26.3-*.pack.zip' | Sort-Object LastWriteTime -Descending | Select-Object -First 1).FullName
$dbg="$env:TEMP\dbg.json"
'{"MC_CULL_STATS":"1","MC_VIEW_DISTANCE":"12","MC_PRESENT_MODE":"immediate"}' | Set-Content $dbg -Encoding ascii
src/Sharpcraft.Client/bin/Release/net10.0/Sharpcraft.Client.exe --version 26.3 --gameDir "$env:APPDATA\.sharpcraft" --pack $pack --offlineDeveloperMode --username CloudTest --debugOptionsFile $dbg
```

**配置传递规则（原则 13）**：游戏代码只读宿主传入的 `--debugOptionsFile` 快照，**不读环境变量**。用 env 设置 `MC_*` 会静默无效。

**测量纪律**：跨运行比较不可靠（同一场景不同运行差 10%+ 是常态），必须用同进程 A/B。

## 其它已登记待办

| 任务 | 内容 | 状态 |
| --- | --- | --- |
| T-827 | 云改程序化生成 S4/S5 | S1–S3 完成；S4/S5 待做 |
| T-781 | Ctrl+F2 全景截图 | 未开始 |
| — | `spriteRect` 16 B 移除（44 → 28 B） | 需先做图集 UV 烘焙与动画纹理 |
| — | `ChunkGeometryStore` 页只增不减，24 秒内 1296 → 1354 MB | 正常驻留，非泄漏 |
| T-107 | 文本断言 | 阻塞（见上） |

最后更新：2026-11-14
