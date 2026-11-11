<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

<!-- doc
title: 区块几何用分页共享 arena
summary: 每区块独立缓冲让每帧产生数百次绑定与数百条命令；改为每层少量固定大小页并按下标/顶点偏移绘制
status: 已采纳
-->

# ADR-0029：区块几何改用分页共享 arena（取代 ADR-0020 的每区块缓冲）

- 状态：已采纳
- 日期：2026-11-11
- 决策者：项目所有者（用户要求"把区块几何合并到共享缓冲"）
- 关联：`docs/traceability.md` T-798；`docs/analysis/architecture/render-backend-seam.md` 9.27 / 9.28；取代 ADR-0020

## 背景

ADR-0020 为了绕开单个大 arena 的 `maxBufferSize` 上限，把每层几何改成**每区块一对独立缓冲**，代价是绘制时逐区块
`SetVertexBuffer`/`SetIndexBuffer`。

T-798 的 VS 探查（确定机位、858×527、视距 12、RTX 3060）给出自 CPU 排名：`CommandEncoder.Finish()` **34.37%**、
`SetVertexBuffer` **7.45%**、`SetIndexBuffer` **5.59%**。即：绑定开销 13% 是直接成本，`Finish()` 的 34% 是它的下游
后果（命令条目越多，最终化序列化越贵）。

ADR-0020 自己已经把"分页 arena"列为后续选项，本 ADR 就是执行该选项——它同时保留 ADR-0020 的核心目的
（内存随驻留网格增长、不受单一缓冲上限约束）。

## 决策

`ChunkGeometryStore` 改为**每层若干固定大小页**（默认 4 MiB/页，页内含一个顶点缓冲与一个索引缓冲）：

- 页内用空闲区间表分配（顶点区间按 `bytesPerVertex` 对齐、索引区间按索引对齐），释放即合并相邻空区。
- 绘制：绑定整页（每页每 pass 一次），再用 `firstIndex`（索引区间起点）与 `baseVertex`（顶点槽位偏移）逐区块
  `DrawIndexed`。索引数据**逐字节原样上传**，无需重写。
- 增长：页内 first-fit 失败则新建一页；不重建、不重传已有区块。超大单区块单独占一页。
- 卸载：只释放区间，页空则销毁该页；**不做整块重建**，因此卸载不是 O(n) 重传。

规格细节（布局、推导、增长/卸载、绑定次数、诊断口径）见
`docs/analysis/architecture/render-backend-seam.md` 9.28。

## 影响面

- 代码：`src/Sharpcraft.Framework/Rendering/ChunkGeometryStore.cs`（重写缓冲组织，公开 API 不变）、
  `src/Sharpcraft.Framework/Rendering/WindowedRenderer.cs`（新增"每帧绑定次数"诊断行）。
- 接缝：**不新增接缝方法**——`SetVertexBuffer`/`SetIndexBuffer`/`DrawIndexed`/`WriteBuffer` 的既有重载已足够。
- 测试：新增针对分配/释放/合并/就地更新的单元测试（不依赖 GPU：页分配逻辑与缓冲无关的部分可测）。
- 文档：ADR-0020 标注被取代；`docs/analysis/architecture/render-backend-seam.md` 9.28；`docs/traceability.md` T-798。
- 兼容性：不影响存档、协议、模组 API、顶点格式、着色器与 pass 结构。

## 替代方案与被否原因

| 方案 | 否决原因 |
| --- | --- |
| 维持每区块缓冲 | 每帧数百次绑定实测 13% 自 CPU，且 `Finish()` 34% 随之放大 |
| 单一大 arena（ADR-0020 之前的做法） | 触及 `maxBufferSize`，视距 12+ 就要丢远区块 |
| 分页 arena + 每页间接绘制（GPU 剔除） | 需要新增 `DrawIndexedIndirect` 与一致性门禁；T-791 已实测剔除收益比代价低两个量级。分页 arena 本身正是它的前提，留给后续任务 |
| 每次增删整块重建 | 把卸载变成 O(n) 重传，装卸频繁时更差 |

## 复查条件

- 若单页容量（4 MiB）导致可见页数偏多（例如视距 32 时每层 > 16 页），重新评估页大小与"多缓冲绑定"成本。
- 若 `BindCallsTotal/帧` 未降到常数级（预期 ≤ 页数 × pass 数），说明分配策略让区块跨页交错，需改分配为按区块聚合。
- `ChunkCuller`（T-791 停放）若要接线，本 ADR 的页结构是它的前提条件，届时需重新评估。
