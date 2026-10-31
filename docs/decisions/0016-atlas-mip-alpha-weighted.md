<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

# ADR-0016：图集 mip 链改为整幅 alpha 加权盒式滤波

- 状态：已接受（2026-10-31）
- 相关：ADR-0015、`AtlasMipChain`、`Repro-005`

## 背景

方块边缘出现过 1~2 像素的纯黑线，远处连成黑块。逐项排除后（CPU 网格无缝隙、深度与邻居一致、
关闭纹理/顶点色无效、按面分层无效），用 `MC_NO_MIPS=1` 关闭 mip 链时**黑线完全消失**，锁定在 mip 链。

## 原因

上一版 mip 是"逐 sprite + 覆盖率补偿"：
1. 每个 sprite 的层级像素写在 `x >> (level+1)` 位置，**不同 sprite 的层级区域在高 mip 上会重叠**，后写覆盖先写，内容变成邻居/空白；
2. `scaleAlphaToCoverage` 会把 alpha 乘到最大 4 倍，使原本半透明的 texel 通过 0.5 的 alpha 测试，
   但它的颜色仍是与透明黑平均出来的暗色 ⇒ **alpha 通过、颜色近黑**的细线，距离越远越宽。

## 决定

mip 链改为对**整幅图集**做 4 级盒式滤波，颜色按 alpha 加权平均
（`rgb = Σ(rgb·a) / Σa`，`a = Σa / 4`），全透明邻域保持全透明。

- padding（ADR-0015）已保证整幅滤波不会取到邻居 sprite，所以不需要逐 sprite 处理；
- alpha 加权保证透明黑不会把边缘染黑；
- 关闭 `MC_NO_MIPS` 后实测：**纯黑像素 0、细线 0**，与关闭 mip 的表现一致。

## 影响

- 远处纹理仍然平滑（mip 生效），且不再出现黑边。
- `MC_NO_MIPS=1` 保留为诊断开关。
- 遗留：平坦地面上仍有**矩形淡蓝区域**（疑似水体或未网格化区块），与本 ADR 无关，另行定位。
