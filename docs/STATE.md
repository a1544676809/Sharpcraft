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
| T-786 | 渲染后端接缝收口 | 逃生口 **225 → 1**（−99.6%），`new WgpuRenderBackend` **9 → 2**，原生 pass **0**；唯一保留项 `SIM-SURFACE-NOT-IN-SEAM`（T-788）已裁决保持。`Brew.Wgpu` 在 `Framework/Rendering` 之外 **0 文件** |
| T-792 | 窗口只渲染 7–9 个区块 | 根因：预载后 29 秒无帧运行，无人应答 keep-alive，服务器 30 秒判死连接。修复后区块 **7 → 496**，服务器不再 `Timed out` |
| T-793 | 首帧请求 44.6 s | 根因：`BuildRequest` 二次网格化且未传 `StateCullTable`/`StateVisualCache`。根治：删除第二遍，改为消费 `ChunkMeshCache` 已有产出。**44.6 s → 26 ms**，几何逐项等价，分配 1.09 GB → 0 |
| T-794 | 首帧 6.8 s 空白 | 未复现——是 T-793 那 1.09 GB 分配风暴引发的一次性 GC，随修复消失。生产代码零改动 |
| T-791 | GPU 区块剔除 | **实测否决**：剔除总收益 0.4–2.7 ms，CPU 循环 <0.05 ms（<1% 帧时间）；搬 GPU 省 <0.01 ms 却要新增 `DrawIndexedIndirect` + 一致性门禁。`ChunkCuller` 保留停放 |

## 进行中

**T-796 渲染帧时间归因的测量装置**：分段 CPU 计时（`MC_FRAME_CPU`）、三个 A/B pass 开关（`MC_SKIP_OIT` / `MC_SKIP_CLOUDS` / `MC_SKIP_TRANSLUCENT`）、脚本 `tools/verify/measure-frame.ps1`。装置已落地并跑出对照表，结论见 `specs/performance-baseline.md` §12。

## 阻塞项

- 本会话期间本地 Fabric 服务器曾自行退出（`Can't keep up` 后进程消失，仓库外目录），已按 `run.bat` 的同款命令行重启；重启耗时约 14 s。后续测量需确认服务器存活。
- `docs/STATE.md` 在 `2e60e42` 之后与 `DocumentationDriftTests` 不一致（缺 SPDX 头、缺 `## 已完成`/`## 进行中`/`## 阻塞项`/`## 下一步具体动作` 与 `最后更新：`、含绝对路径），已在 T-796 中按测试要求补齐。

## 下一步具体动作
**渲染帧时间归因**：把 6.0 ms 拆成 CPU 与 GPU 两部分，找出大头。

已知对照（固定机位 `-74.098 / 121 / 37.303` 朝北、放大化世界、视距 32、`MC_PRESENT_MODE=immediate`）：

| | 帧率 | 帧时间 |
| --- | --- | --- |
| 原版 26.3 | 650 fps（1280×720）/ 630 fps（854×480） | ≈ 1.5 ms |
| 本实现 | ≈ 165 fps（1280×720） | ≈ 6.0 ms |

即**慢约 4 倍**，这是性能工作的正确入口。

**首要怀疑**：参考 OIT 序列每帧全跑 9 个 pass（bounds → cull → cloud bounds → transmittance → cloud transmittance → accumulate → cloud accumulate → composite），很可能是大头。先测其占比，不要凭猜测优化（原则 9）。

**注意**：用户提示机器即将断电，断电后**性能基准可能变化**，届时需重新采样基线。

## 已测量并记录的性能事实

- 剔除 ON：`chunks=646 culled=444` / `≈6.0 ms`；剔除 OFF：`chunks=674 culled=0` / `6.4–8.7 ms`
- 启动分段（进程年龄）：资源 0.38 s → 登录+预载 6.3–7.2 s → 客户端准备 0.26 s → 窗口 0.55 s → wgpu 1.01 s → 管线+上传+首帧 1.0 s。**渲染侧已无可去除项**
- compute 运行时真值：`pyramid: 9 levels, mismatches 0`、`motion: mismatches 0`（对 CPU 参考；原版无 compute shader，故无原版真值可提）

## 未提交改动

T-796 未提交：`src/Sharpcraft.Framework/Rendering/WindowedRenderer.cs`（分段计时 + 三个 pass 跳过）、`src/Sharpcraft.Framework/Rendering/FrameCpuTimings.cs`（新增）、`src/Sharpcraft.Framework/Rendering/FrameDiagnostics.cs`（4 个显式选项）、`src/Sharpcraft.Client/Game/GameOptions.cs`（宿主开关映射）、`tools/verify/measure-frame.ps1`（新增）、`docs/specs/performance-baseline.md` §12、`docs/traceability.md`、本文件。

最后更新：2026-11-10 23:20

## 待办（已登记，按优先级）

| 任务 | 内容 | 状态 |
| --- | --- | --- |
| — | 渲染帧时间归因（下一步动作） | 未开始 |
| T-795 | 草/植被类方块尚未实现 | `规格不足`——需先定原版植被渲染类别、随机偏移、双面、光照语义 |
| T-781 | Ctrl+F2 全景截图 | 未开始 |
| T-782 | 剩余 MCP 工具 | 未开始 |
| T-764 | 剩余 special 类型 | 未开始 |
| — | `ChunkCuller` 接线 | 停放，复查条件见 T-791 |

## 常用命令

```powershell
dotnet build Sharpcraft.slnx
dotnet test Sharpcraft.slnx --no-build
dotnet run --project tools/Sharpcraft.ClassMap -- --check
dotnet run --project tools/Sharpcraft.Docs.Indexer -- --check --check-coverage
pwsh -NoProfile -ExecutionPolicy Bypass -File tools/verify/check-simplifications.ps1
```

窗口启动（先杀进程，避免 DLL 锁导致构建静默失败）：

```powershell
Get-Process -Name 'Sharpcraft.Client' -ErrorAction SilentlyContinue | Stop-Process -Force
$pack=(Get-ChildItem "$env:LOCALAPPDATA\Sharpcraft\packs" -Filter 'minecraft-26.3-*.pack.zip' | Select-Object -First 1).FullName
$dbg="$env:TEMP\dbg.json"
'{"MC_CULL_STATS":"1","MC_VIEW_DISTANCE":"32","MC_PRESENT_MODE":"immediate","MC_PERF_SUMMARY":"1"}' | Set-Content $dbg -Encoding UTF8
src/Sharpcraft.Client/bin/Debug/net10.0/Sharpcraft.Client.exe --version 26.3 --gameDir "$env:APPDATA\.sharpcraft" --pack $pack --offlineDeveloperMode --username Test --quickPlayMultiplayer 127.0.0.1:25565 --width 1280 --height 720 --debugControl --debugOptionsFile $dbg
```

**配置传递规则（原则 13）**：游戏代码只读宿主传入的 `--debugOptionsFile` 快照，**不读环境变量**。用 env 设置 `MC_*` 会静默无效。
