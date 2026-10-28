<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

# 性能基线结果

运行时间：2026-10-28 19:08:36 +08:00　运行时：10.0.12　CPU 核数：20

fixtures：packet=50046 B，world=9 区块，mesh=287253 方块 / 71760 三角形，nbt=891 B；iterations=24，warmup=3

| 项 | 采样 | P50 (ms) | P95 (ms) | min (ms) | max (ms) | 分配 (B/次) |
| --- | --- | --- | --- | --- | --- | --- |
| B-1 chunk packet parse | 24 | 0.166 | 0.262 | 0.145 | 0.277 | 110,816 |
| B-2 chunk meshing | 12 | 24.444 | 153.224 | 23.466 | 153.224 | 4,305,736 |
| B-2b chunk meshing (pooled) | 12 | 23.455 | 24.475 | 22.361 | 24.475 | 0 |
| B-3 clip-space transform | 24 | 1.913 | 2.116 | 1.570 | 2.178 | 4,018,584 |
| B-3b clip-space (span dest) | 24 | 1.529 | 1.833 | 1.394 | 1.965 | 0 |
| B-4 png encode 960x540 | 24 | 13.083 | 13.816 | 12.063 | 14.297 | 5,392,608 |
| B-5 nbt parse | 24 | 0.004 | 0.009 | 0.004 | 0.009 | 3,808 |

> 读数说明见 `docs/specs/performance-baseline.md` §4（网络、JIT/GC、驱动缓存、降频）。同机前后对比才有意义。
