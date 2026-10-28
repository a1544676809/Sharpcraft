<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

# 性能基线结果

运行时间：2026-10-28 18:44:49 +08:00　运行时：10.0.12　CPU 核数：20

fixtures：packet=50046 B，world=9 区块，mesh=287253 方块 / 71760 三角形，nbt=891 B；iterations=24，warmup=3

| 项 | 采样 | P50 (ms) | P95 (ms) | min (ms) | max (ms) | 分配 (B/次) |
| --- | --- | --- | --- | --- | --- | --- |
| B-1 chunk packet parse | 24 | 0.159 | 0.178 | 0.134 | 0.185 | 110,304 |
| B-2 chunk meshing | 12 | 21.831 | 132.875 | 20.964 | 132.875 | 4,305,736 |
| B-2b chunk meshing (pooled) | 12 | 20.768 | 22.227 | 19.580 | 22.227 | 0 |
| B-3 clip-space transform | 24 | 1.890 | 2.207 | 1.130 | 2.962 | 4,018,584 |
| B-3b clip-space (span dest) | 24 | 1.467 | 1.776 | 1.407 | 1.873 | 0 |
| B-4 png encode 960x540 | 24 | 12.585 | 13.269 | 11.665 | 14.000 | 5,392,608 |
| B-5 nbt parse | 24 | 0.004 | 0.008 | 0.004 | 0.010 | 3,808 |

> 读数说明见 `docs/specs/performance-baseline.md` §4（网络、JIT/GC、驱动缓存、降频）。同机前后对比才有意义。
