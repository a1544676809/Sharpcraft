<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

# 帧间流水线（规格，待评审）

> 状态：**规格已就绪，未实施**。实施前需项目所有者评审。
> 关联：`frame-baseline.md`（实测数字）、`pass-reduction.md`（pass 成本模型）。

## 1. 要解决的问题

实测一帧 **1.919 ms**，`frame-gap = 0.002 ms`——整帧没有任何空闲等待，每个阶段首尾相接。因此：

- 没有"等 GPU"的时间可回收；
- 唯一的大幅收益来源是**把 CPU 工作分到两个线程并行**。

当前分解（1400×754，视距 12，`MC_PRESENT_MODE=immediate`，Release）：

| 类别 | 阶段 | 合计 |
| --- | --- | --- |
| CPU 准备 / 录制 | `prep-pump` `prep-ui` `prep-entities` `camera` `cel-build` `cel-disc` `scene-terrain` `scene-entities` `oit` `text` `clouds` `scene-open` 等 | ≈0.86 ms（45%） |
| 提交 | `sub-finish` 0.426 + `sub-queue` 0.158 | 0.59 ms（31%） |
| 呈现 / 获取 | `swap` 0.353 + `acquire` 0.110 | 0.46 ms（24%） |

## 2. 目标与判据

| 项 | 值 |
| --- | --- |
| 目标帧时间 | ≤1.45 ms（自 1.92 ms，**−25%**） |
| 理论最优 | max(录制+`Finish()` 1.28, 提交+呈现 0.51) + `acquire` 0.11 ≈ **1.39 ms（−27%）** |
| 判据 | 同机位、同进程 A/B（单线程 vs 双线程），**不得跨运行对比** |
| 画面判据 | 离线 `--frame` 改动前后**哈希相等**（不得有视觉差异） |

**不得把本项当作"已提速"引用**，直到同进程 A/B 给出数字。

## 3. 线程划分

```
主线程（提交线程）              录制线程
  acquire 下一帧图像
  ── 交棒 ──▶                   录制第 N+1 帧
  submit 第 N 帧                  · 准备（pump/UI/实体/相机）
  present 第 N 帧                 · 构建几何
  ◀── 等待录制完成 ──             · 全部 Pass 录制
                                 · Finish()
```

**同步点**：swapchain `acquire` 留在提交线程，因为录制需要目标纹理。因此关键路径为
`acquire + max(录制, 提交+呈现)`。

**约束（原则 14 单轨）**：不得因此产生第二套帧管线。离屏与窗口路径 MUST 继续共用同一套 Pass 执行器；本项只是改变执行它的线程。

## 4. 必须双缓冲的数据

GPU 侧的 uniform 无需处理：`Queue.WriteBuffer` 内部有暂存环，不会踩坏在飞的写入。

**竞态在 C# 侧数组**——录制第 N+1 帧会覆盖第 N 帧仍在读取的内容：

| 数据 | 位置 | 份数 |
| --- | --- | --- |
| 间接绘制参数记录 | `ChunkGeometryStore` / 各 arena 的参数页 | 2 |
| UI 顶点与索引 | `WindowedRenderer` 的 UI 缓冲 | 2 |
| 动态实体几何 | 实体绘制路径 | 2 |
| 文本几何 | 文本绘制路径 | 2 |
| 每帧 uniform 字节数组 | `WindowedRenderer` 的 `cameraBytes` / `frameLightmapBytes` / `fogValues` | 2 |

**份数取 2**（不是 3）：同步点保证同一时刻最多两帧在飞。

## 5. GPU 缓冲的延迟释放

录制线程不再持有"这一帧用完就能删"的假设。规则：

- 缓冲/纹理的释放 MUST 挂到"使用它的帧已完成提交"之后；
- 采用**帧编号队列**：释放请求入队时带上当前帧号，提交线程每帧出队所有 `帧号 <= 已提交帧 - 2` 的请求；
- 不得依赖 GC 或 `using` 作用域的隐式时机。

## 6. 计时

现有 `FrameCpuTimings` 是单线程累加，跨线程会互相污染。要求：

- 每个阶段 MUST 记录**所属线程**，分开累加；
- 新增 `pipeline-wait` 阶段（提交线程等待录制完成的时间），它与 `frame-gap` 一起用于判断流水线是否真的重叠；
- 若 `pipeline-wait ≈ 录制时长`，说明并行没有生效，必须视为失败。

## 7. 回退开关

MUST 提供单线程路径并作为默认，双线程由宿主层显式开启（原则 13：开关只在宿主层解析）。

- `MC_FRAME_PIPELINE=0|1`，默认 `0`；
- 两条路径 MUST 共用同一套 Pass 执行器，只有"谁来调用它"不同；
- 回退路径 MUST 在每次改动后仍被测试覆盖。

## 8. 风险

| 风险 | 影响 | 缓解 |
| --- | --- | --- |
| 双缓冲遗漏某处数组 | 画面撕裂/闪烁，且难复现 | 逐项列出（§4），每项加注释指明份数 |
| 缓冲释放时机错误 | 使用已释放资源，崩溃或黑屏 | 帧号队列（§5），不靠作用域 |
| 跨线程异常传播 | 崩溃信息丢失 | 录制线程的异常 MUST 回传提交线程并原样抛出 |
| 并行未生效却以为生效 | 假收益 | `pipeline-wait` 必须显著小于录制时长（§6） |
| 复杂度上升导致后续维护困难 | 长期成本 | 默认单线程，双线程是可选路径 |

## 9. 实施切片（每片可独立验收）

1. 抽出"录制一帧"为独立可调用单元（不改线程，单线程跑通，离线帧哈希不变）
2. 加 `pipeline-wait` 计时与线程归属（仍单线程，仅计时正确）
3. 引入录制线程 + 双缓冲（先只覆盖间接参数与 UI 顶点）
4. 补齐其余双缓冲项与帧号释放队列
5. 同进程 A/B 测收益；若 `pipeline-wait` 不显著小于录制时长，回滚

**每片都必须保持离线 `--frame` 哈希不变。**

## 10. 切片 1 的状态清点（实测，2026-11-13）

录制区是 `WindowedRenderer.cs` 的 **L1223–L1829**（camera → present-rec，约 600 行）。用脚本对"区外声明、区内引用"的局部量做清点，得到 **105 个**，分三类：

| 类别 | 数量 | 处置 |
| --- | --- | --- |
| 诊断类（`skip*`、`cameraStarted`、`cameraTotalMs`、`worstCameraMs`） | 13 | 按原则 13 可整体删除，不进状态对象 |
| **每帧重写的 CPU 数组** | **10** | **必须双缓冲（§4 的准确清单）** |
| GPU 资源 / 管线 / 标量 | 82 | 状态对象持有即可，无并发问题 |

### 10.1 更正：不是 10 个"数组"，而是三类不同问题

初版把脚本找出的 10 个名字一律当成"每帧重写的 CPU 数组"，**这是错的**。逐个核对声明后分三类：

| 类别 | 名字 | 需要的机制 |
| --- | --- | --- |
| **CPU 暂存缓冲**（每帧完整重写） | `cameraBytes`、`frameLightmapBytes`、`frameFogValues`、`frameFogBytes` | **真正的双缓冲** |
| **CPU 列表**（每帧重填） | `uiRanges`（原 `ui.Ranges.ToArray()`） | 复用容器，无需双缓冲 |
| **GPU 缓冲池**（持久、按需扩容） | `entityLayerVertex`、`entityLayerIndex`、`dynamicVertex`、`dynamicIndex`、`uiVertex`、`uiIndex` | **在飞保护**，不是 CPU 双缓冲 |
| 容量/计数标量 | `entityLayerVertexCapacity`、`entityLayerIndexCount`、`dynamicVertexCapacity`、`dynamicIndexCapacity`、`uiVertexCapacity`、`uiIndexCount` | 无（单线程写入） |

GPU 缓冲池按 L443 的循环持久增长，每帧只重置计数（`entityLayerIndexCount[i] = 0`），句柄本身跨帧存活。它们**不能**靠 CPU 双缓冲解决——需要的是"提交后延迟销毁/延迟复用"，与 §5 的帧号释放队列是同一套机制。

### 10.1.1 已顺带修掉的两处每帧分配

| 位置 | 问题 | 处理 |
| --- | --- | --- |
| 帧循环内 | `frameLightmapBytes`、`frameFogValues`、`frameFogBytes` 每帧新建 | 抽成 `FrameUniformStaging`，一次分配 |
| `uiRanges` | `ui.Ranges.ToArray()` 每帧分配新数组 | 改为 `List` + `Clear()`/`AddRange()` |

600 FPS 下这两处合计每秒约 2400 次分配，全部是完整覆写前的无谓分配。
### 10.2 结论

状态对象需承载 82 个稳定引用 + 10 组双缓冲数组。这是约 300 行的重构，**超出微切片范围**，需作为独立任务实施；实施顺序仍按 §9 的切片 1（先单线程抽出、离线帧哈希不变）。