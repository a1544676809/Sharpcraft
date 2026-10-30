<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

# 启动器 / 基准支持（benchmarking）

只服务于**开发期测量**的代码，默认不生效。

## 组成

| 类型 | 作用 |
| --- | --- |
| `ChunkBufferHarness` | `MC_DEV_BENCH=1` 时启用；`MC_BENCH_BUFFER=<秒>` 指定窗口。缓存服务端发来的区块，**达到期望区块数或超时（先到者）**后一次性释放，使计时从"传输结束"开始 |

## 约束

- **不得进入发布形态**：未设置 `MC_DEV_BENCH` 时 `FromEnvironment` 返回 null，任何调用点都走原路径；开启时启动打印 `benchmark mode: not for normal play`。
- 不做任何渲染或网络语义改变；只是"先收后装"。
- 指标与判据见 `docs/specs/quality-attributes.md`「区块网格构建基准」；流程见 `benchmarks/chunk-mesh/README.md`。
## 进程卫生（2026-10-30 实测）

跑基准会在机器上留下两类**看起来像"游戏没关干净"**的残留，实测已分清原因：

| 残留 | 数量特征 | 原因 | 处置 |
| --- | --- | --- | --- |
| `dotnet.exe` + `MSBuild.dll /noautoresponse` | 十几个，空闲 0% CPU | **MSBuild 节点复用**（构建服务器），不是游戏进程 | 设 `MSBUILDDISABLENODEREUSE=1`；结束时 `dotnet build-server shutdown` |
| `Sharpcraft.Launcher.exe` | 一个 | 极少数实例卡在**原生侧退出**（0 线程 / 0 CPU / 无窗口，`taskkill /F` 也无效）| 只能重启回收；脚本改用 `taskkill /T` 杀进程树，减少发生 |

客户端本身的退出路径是干净的：`MC_WINDOW_SECONDS=8` 实测 `launcher 2 → 1`（自己在窗口结束后退出，`dotnet run` 包装进程一并结束，`dotnet` 计数回到 0）。
另有静态证据：`ChunkWorkerPool` 的工作线程 `IsBackground = true`，`LivePlaySession.Dispose` 会 `Cancel` 接收泵并等待 1 秒。
