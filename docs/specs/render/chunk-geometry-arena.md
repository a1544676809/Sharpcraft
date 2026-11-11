<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

<!-- doc
title: 区块几何的共享缓冲组织（arena）
summary: 为什么要合并、缓冲怎么长、区块卸载怎么回收、每帧怎么画；附实测归因与验收口径
-->

# 区块几何的共享缓冲组织

> **实施说明（2026-11-11）**：本规格 §3 提出的"每层一个大缓冲"在实现时因缓冲大小上限而改为**每层若干固定大小页**（顶点页 4 MiB / 索引页 512 KiB）。其余决策（索引按区块相对编号存储、`firstIndex`/`baseVertex` 放置、空闲区间回收、三层独立）均按本规格落地。实测：帧时间 5.13–5.70 → 2.54–2.76 ms，绑定次数 2668 → 278 次/帧，几何逐项一致。
>
> **权威记录**：实现细节与取舍理由见 `docs/decisions/0029-paged-shared-geometry-arena.md`（该 ADR 取代 ADR-0020）与 `docs/analysis/architecture/render-backend-seam.md` §9.28。
>
> **一处行为变化**：半透明层不再按距离排序绘制（参考 OIT 序列顺序无关）；若将来切回非 OIT 的 alpha 混合路径，此项必须回退——已写入 ADR-0029 的复查条件。

## 1. 问题：每区块独立缓冲导致提交量爆炸

VS「CPU 使用率」探查器实测（固定机位、858×527、视距 12、RTX 3060、`MC_PRESENT_MODE=immediate`）。自 CPU 指函数自身耗时，不含被调者：

| 函数 | 自 CPU |
| --- | --- |
| `Brew.Wgpu.CommandEncoder.Finish()` | **34.37%** |
| `Brew.Wgpu.RenderPassEncoder.SetVertexBuffer(...)` | **7.45%** |
| `Brew.Wgpu.RenderPassEncoder.SetIndexBuffer(...)` | **5.59%** |
| `Brew.Wgpu.Queue.Submit(...)` | 5.43% |
| `Brew.Wgpu.Surface.Present()` | 5.22% |
| `ChunkGeometryStore.DrawFarthestFirst` | 1.76% |
| `ChunkGeometryStore.Draw` | 1.19% |

对应的绘制入口对**每个区块**执行：

```csharp
_backend.SetVertexBuffer(pass, 0, entry.Vertex);          // 每区块
_backend.SetIndexBuffer(pass, entry.Index, ...);          // 每区块
_backend.DrawIndexed(pass, entry.IndexCount, 1, 0, 0, 0); // 永远从偏移 0
```

每个区块持有独立的顶点与索引缓冲。于是每帧产生**数百次缓冲绑定 + 数百次绘制**：

- 13% 的自 CPU 是直接的绑定开销；
- 34.37% 的 `Finish()` 是其下游后果——记录的命令条目越多，最终化时逐条序列化的成本越高。

**这不是泄漏，是组织方式。** 顶点格式、着色器、pass 结构都没有问题。

## 2. 现状结构

`ChunkGeometryStore`（文档与注释中称 arena）当前是「区块 → 一对缓冲」的字典：

- 每个 `Entry` 持有 `Vertex` / `Index` 两个 `BufferHandle`；
- 区块网格变化时整对重建；
- 绘制时逐条目绑定、从偏移 0 发起绘制；
- 三个图层（不透明 / 切边 / 半透明）各有一个 arena 实例。

## 3. 目标结构

每个图层一个 arena，arena 持有**少量（理想为 1 个）共享大缓冲**：

```
Arena (per layer)
  VertexBuffer   : 一个大缓冲，区块几何首尾相接追加
  IndexBuffer    : 对应的大缓冲，索引值以「该区块顶点起始」为基址
  Slot[chunkKey] : { VertexByteOffset, VertexFloatOffset, FirstIndex, IndexCount, Bounds, Version }
```

绘制时：

1. 每个图层**一次** `SetVertexBuffer` + **一次** `SetIndexBuffer`；
2. 之后每个可见区块只发 `DrawIndexed(indexCount, 1, firstIndex, baseVertex, 0)`；
3. `baseVertex` 用 `VertexFloatOffset / FloatsPerVertex`，`firstIndex` 用 `slot.FirstIndex`。

**偏移语义的推导**：

- `firstIndex` 是索引缓冲内的**索引元素下标**（不是字节偏移），等于该区块索引区间起点；
- `baseVertex` 是**顶点下标**，会被加到每条被读索引上；取该区块顶点区间的起始顶点下标；
- 二者同时使用是安全的：`firstIndex` 定位索引区间，`baseVertex` 把该区间内的相对索引平移到全局顶点空间；
- 因此索引数据可以**按区块相对顶点编号**存储，无需在追加时重写索引值——这让追加变成纯拷贝。

## 4. 关键设计决策

### 4.1 增长策略：追加 + 容量翻倍重传

- 新几何追加到尾部；剩余容量不足时，**容量翻倍**并重建整个缓冲（一次性重传该图层全部几何）。
- 重传代价是 O(该图层总字节)，但发生频率按倍增摊还是 O(1)/区块。
- **不采用**「每区块一个小缓冲」之外的另一种简单做法——每次增长都新建缓冲并只拷新数据（会留下碎片，且区块顺序与绘制顺序脱钩）。

### 4.2 区块卸载：空闲区间列表

- 区块离开视野时，其区间进入**按字节偏移排序的空闲列表**，可与相邻空闲区间合并。
- 新区块优先从空闲区间中**能容下且最紧的**一块取；取不下再追加。
- **不采用**「卸载即整块重建」：那会把一次卸载变成 O(几何总量) 的重传，而卸载是常态。

### 4.3 三个图层独立

- 不透明 / 切边 / 半透明各一个 arena，互不影响增长与回收。
- 半透明需要**由远及近**绘制，因此其槽位列表每帧按距离排序；共享缓冲不影响这件事——排序的只是 `firstIndex`/`baseVertex` 对，绘制序列仍然是一串 `DrawIndexed`。

### 4.4 不改变的部分

顶点格式、顶点属性布局、着色器、pass 划分与顺序、OIT 的 pass 数量——**均不变**。本规格只改几何在缓冲中的组织方式。

## 5. 每帧绘制序列（每个图层）

1. 若该层缓冲被重建过，绑定一次新的 `Vertex` / `Index`；
2. 遍历可见槽位（按各自排序规则），逐个 `DrawIndexed`；
3. 若在遍历途中缓冲被重建（不应发生——重建只在两帧之间），必须重新绑定。

**Per-frame 绑定次数目标**：每层 ≤ 2 次（顶点 + 索引），与区块数量无关。当前是「每区块 2 次」。

## 6. 不变量与验收

### 6.1 硬不变量（几何等价）

改变前后必须**逐项相同**：

- `mesh: N blocks, N triangles`
- `frame #0 ... uploaded +N KiB`
- 首帧 `cull: chunks=N cutout=N translucent=N`
- 各图层三角形总数

任何一项变化都要能解释清楚；**不能以"看起来差不多"验收**。

### 6.2 绑定次数的可观测性

必须加诊断计数并打印每帧的 `SetVertexBuffer` / `SetIndexBuffer` 调用次数，证明从「每区块 2 次」降到「每层 ≤ 2 次」。这是本规格要达成的**可测量目标**。

### 6.3 性能

稳态 `meanMs` 应下降（基线约 4–5 ms，视机器的降频状态浮动）。若未下降，说明瓶颈不在绑定——**如实报告，不得宣称完成**。

**测量纪律（本项目已踩过的坑）**：区块加载期的 CPU 与帧时间都偏高，8 个网格化 worker 满载是**正常行为**。任何采样必须先等到区块数稳定（`cull: chunks=` 连续多个采样点不变）再开始。

### 6.4 画面

截图与改动前对比，不得出现缺失面、黑块、z-fighting、错位。

## 7. 风险与未决

| 项 | 说明 |
| --- | --- |
| 缓冲重建的抖动 | 翻倍重建会一次性重传整个图层，可能造成偶发长帧。需在实现后观察 `worst frame` 是否恶化 |
| 顶点属性对齐 | 追加位置必须满足顶点步长的对齐要求，否则绑定偏移非法 |
| 索引格式 | 现有为 `Uint32`；共享缓冲下索引值是区块内相对编号，`baseVertex` 承担平移 |
| 与 GPU 剔除的关系 | 间接绘制以「共享 arena + 每槽位一条记录」为前提。本规格完成后，`ChunkCuller`（T-791，当前停放）才有落地条件——**但不属于本规格范围** |

## 8. 与原版的关系

原版把区块几何放进分代的顶点缓冲池（按大小分桶复用），并非每区块独立缓冲，也不是单一巨石缓冲。本规格选择「单一大缓冲 + 空闲列表回收」，理由是当前实现只需消除绑定次数爆炸，且单一缓冲让偏移语义最简单。**这是实现选择而非行为对齐点**——对齐标准仍然是最终像素与三角形数量。
