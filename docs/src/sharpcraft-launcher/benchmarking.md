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
