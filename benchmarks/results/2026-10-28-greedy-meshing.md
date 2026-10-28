<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

# 性能基线结果

运行时间：2026-10-28 19:26:05 +08:00　运行时：10.0.12　CPU 核数：20

fixtures：packet=50046 B，world=9 区块，mesh=287253 方块 / 15716 三角形，nbt=891 B；iterations=24，warmup=3

| 项 | 采样 | P50 (ms) | P95 (ms) | min (ms) | max (ms) | 分配 (B/次) |
| --- | --- | --- | --- | --- | --- | --- |
| B-1 chunk packet parse | 24 | 0.184 | 0.206 | 0.154 | 0.218 | 110,816 |
| B-2 chunk meshing | 12 | 34.700 | 42.252 | 33.525 | 42.252 | 943,096 |
| B-2b chunk meshing (pooled) | 12 | 35.128 | 61.707 | 33.393 | 61.707 | 0 |
| B-3 clip-space transform | 24 | 1.362 | 1.536 | 0.608 | 1.698 | 880,120 |
| B-3b clip-space (span dest) | 24 | 1.204 | 1.280 | 1.191 | 1.304 | 0 |
| B-4 png encode 960x540 | 24 | 13.375 | 15.184 | 12.959 | 15.492 | 5,392,608 |
| B-5 nbt parse | 24 | 0.005 | 0.009 | 0.004 | 0.009 | 3,808 |

> 读数说明见 `docs/specs/performance-baseline.md` §4（网络、JIT/GC、驱动缓存、降频）。同机前后对比才有意义。
