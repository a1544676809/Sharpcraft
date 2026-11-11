<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

# ADR-0020：每区块几何缓冲 + CPU 视锥剔除（替代单一大 arena）

状态：**已被 ADR-0029 取代**（2026-11-11）——每区块缓冲改为分页共享 arena；本 ADR 的动机（内存随驻留网格增长、不受单一缓冲上限约束）仍成立，见 0029
相关：ADR-0011（bulk upload spike）、`docs/specs/network/play-movement.md 7.27`、`docs/modules/clouds.md`

## 背景

窗口渲染目前把每层（solid/cutout/translucent）的区块几何放进**一个大 arena 缓冲**，再用 GPU 间接绘制/剔除。该设计有两个根本问题：

1. **单缓冲受设备上限约束**：WebGPU/`wgpu` 单个 buffer 有 `maxBufferSize`（默认 256 MiB 量级）。视距 12 已让三层 arena 的预留/扩容峰值触及上限；视距 24–32 更不可能放进一个缓冲。
2. **为不崩溃只能"跳过远处区块"**：达到上限后丢弃远处 chunk，与"视距 12–32 全部可见"的目标冲突；同时扩容时旧+新缓冲并存，瞬时峰值高，仍可能 `wgpuDeviceCreateBuffer: Not enough memory`。

原版客户端为每个 `RenderChunk`/层使用**独立的顶点缓冲**，并用 CPU 视锥剔除决定绘制哪些区块，因此没有全局单缓冲上限，显存随实际网格增长（不再需要的区块会被释放）。

## 决策

**方案 A（采用）**：为每个区块、每一层维护**独立的 vertex/index GPU 缓冲**；上传只写该区块自己的缓冲；区块离开视距时释放其缓冲。绘制时用 **CPU 视锥剔除**（按区块 AABB 与相机视锥平面）决定可见列表，逐区块 `SetVertexBuffer/SetIndexBuffer/DrawIndexed`。

- 内存：与实际驻留网格成正比，无固定上限；不再"达上限丢弃区块"。
- 剔除：CPU 视锥剔除替代 GPU 间接剔除；translucent 仍按相机距离从远到近排序。
- 线程：`Apply/Remove` 在渲染线程调用（与现 meshCache drain 一致）；GPU 缓冲的创建/销毁由所有者（渲染器）统一管理，离开的区块缓冲在下一帧边界释放，避免在途使用。

## 备选与被否决

- **分页 arena（多固定大小 page + GPU 间接剔除）**：可保留 GPU 剔除以降低 draw call，但需要 per-page 绑定/每页间接参数/每页剔除 dispatch，改动面大于本阶段收益；留作后续性能优化（若 CPU 剔除成为瓶颈）。
- **继续增大单缓冲**：被设备 `maxBufferSize` 硬限阻止，不可行。
- **维持跳过远区块**：与需求冲突，否决。

## 后果

- Draw call 数量随可见区块数增长（视距 32 约数千）。可接受为第一步；后续若 profiling 显示瓶颈，再走分页 + GPU 剔除（保留本 ADR 的每区块内存所有权模型）。
- 需要新的 `ChunkGeometryStore` 管理每区块缓冲与内存统计，并改造 `WindowedRenderer` 的三层绘制/排序/剔除路径；离屏 `frame` 的单帧整网格路径不受影响。
- 验收：视距 12 / 24 / 32 长时间运行无 OOM、无 validation error、远处区块全部可见；全量测试与 docs 门禁通过。
