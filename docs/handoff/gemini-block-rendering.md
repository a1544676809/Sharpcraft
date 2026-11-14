<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

# 交接：方块实体与方块模型几何（给 Gemini）

> 更新：2026-11-14
> 分支：`special-block-rendering` @ `0655137`（`main` = `d48bbc1`，领先 53 个提交，**未推送**）
> 测试：2812 全绿；五项门禁全过；工作区干净

## 0. 先读这些

1. `AGENTS.md`（项目宪章，尤其原则 3 / 13 / 14 / 15）
2. `docs/STATE.md`（当前状态）
3. 本文档
4. 三个规格：`docs/specs/render/block-model-geometry.md`、`docs/specs/world/block-events.md`、`docs/specs/world/block-entity-data.md`

## 1. 一句话结论

**这条分支不能合并。** 方块状态 schema 是唯一已证等效的部分；箱子有 3 个 blocker（UV、绕序、服务器路径缺贴图）；element 模型路径有判据缺陷；原则 15 的简化登记几乎全缺。

## 2. 已证等效的部分（可单独摘出合并）

| 模块 | 证据 |
| --- | --- |
| 方块状态属性解码 | `tools/Sharpcraft.BlockState.Audit/compare.ps1` 对真实 26.3 运行时导出 **35723/35723 条逐一比对，零差异** |

这是整个分支最硬的证据，且与渲染无关。建议**优先单独 review 并合并这一条线**。

## 3. 三个 blocker（先修这些，别做新功能）

| # | 问题 | 位置 | 说明 |
| --- | --- | --- | --- |
| T-848 | 箱子盒面 UV 全为 `(0,0)-(1,1)` | `ChestMesher.cs:130-199`、`EntityPlaceholderMesher.cs:181-184` | 每个面采样整张 64×64 图集。`golden-vectors/chest.md` 已记录 body `(0,19)` / lid `(0,0)` / lock `(0,0)` 分区 |
| T-849 | `AppendBox` 六面绕序朝内 | `EntityPlaceholderMesher.cs:155-160` | +X 面叉积为 −X；地形管线 `CullMode.Back`。**pre-existing**，箱子沿用 |
| T-850 | 服务器路径箱子仍是缺失贴图立方体 | `FrameCommand.cs:929-951` 先网格化、`954+` 才建方块实体 | `block/chest` 无元素几何也无方块贴图 |

## 4. Major（登记在 T-851..T-859）

| 任务 | 摘要 |
| --- | --- |
| T-851 | `IsPlainCube` 误判 11/14 真实模型（observer / piston / glazed_terracotta / crafter / cube_mirrored…） |
| T-852 | 面级 `rotation` 未解析，252 面 / 62 模型 UV 旋转丢失 |
| T-853 | `SectionHasElementModel` **无调用点**——`9500a0c` 的性能声明是假的 |
| T-854 | element 模型集未到 `ChunkWorkerPool` 与 `WindowCommand.BuildRequest` |
| T-855 | `ShulkerBox` 被 `IsChestLike` 命中，用箱子几何渲染 |
| T-856 | `BlockEntityRenderState.Sky/Block` 恒 15/0，洞里也全亮 |
| T-857 | 动画无 partial tick、每帧 `Keys.ToArray()`、条目永不清理 |
| T-858 | `ForTexture` 丢弃命名空间 |
| T-859 | Euler `{x,y,z}` 旋转形式未解析 |

## 5. 证据质量问题（重要）

| 事实 | 含义 |
| --- | --- |
| 12 个测试文件里只有 2 个是真 golden vector（`ChestMesherGoldenTests` / `ChestMesherGeometryTests`） | 其余是结构化自检 |
| `tools/verify/check-block-model-geometry.ps1` 的 Python 是 C# 的**逐行转写** | "独立对拍"不成立，同一 bug 两边同现 |
| 该脚本 **`default_uv()` 定义了但从未调用** | UV 完全没比过 |
| `IsPlainCube` 的两个"纯立方体"测试都因父模型缺失而通过 | 删掉 `IsPlainCube` 也照样过 |

## 6. 工具与验证方法（可复用）

```powershell
# 沙盒基线：9CA9E03410A2E745 / 34897 B（2045 方块 / 2392 三角形）
MC_FRAME_LOCAL=1 + --frame 640x360
# 单方块场景
{"MC_FRAME_LOCAL":"1","MC_FRAME_BLOCK_ENTITIES":"1"}   # 箱子 state 5562
{"MC_FRAME_LOCAL":"1","MC_FRAME_MODELS":"1"}           # 火把
{"MC_FRAME_LOCAL":"1","MC_FRAME_MODELS":"2"}           # 花盆
```

**教训（血泪）**：
- **像素有变化 ≠ 目标物体渲染了**。火把场景 120 像素变化其实是新方块遮挡了平台顶面；火把与花盆像素完全相同才是真相（都不可见）。
- **必须用三角形增量交叉验证**：火把 +12（6 面×2）、花盆 +44（22 面×2）才对得上。
- **定量对比必须核对二进制是否真的重建**。一次陈旧的 Release 二进制让我得出"着色生效"的错误结论，来回翻了两轮。

## 7. 建议顺序

1. **摘出方块状态 schema 单独合并**（唯一有真证据的）
2. **修三个 blocker**（T-848/849/850）——在这之前，箱子外观本身就是错的，任何像素对拍都没有意义
3. **收紧 `IsPlainCube`**（T-851）并补真实模型用例
4. **补原则 15 登记**（T-860）——合并硬门槛，不可跳过
5. **补面级 rotation**（T-852）后再谈"模型几何完成"
6. **方向着色**（T-759 遗留）：实测无渲染效果，根因未定位；在修好之前不要声称有

## 8. 不要做的事

- 不要在没有 vanilla 参照画面的前提下声称"像素等效"
- 不要在 `FrameCommand` 里继续加 `MC_FRAME_*` 调试分支（原则 13）
- 不要相信"测试全绿"等于等效——本分支 2812 测试全绿，但三个 blocker 都在
- 不要重复已证伪的尝试：把 cull pass 折叠进消费者（更慢）、多绘制合批（无收益）、celestial buffer 复用（500→5 fps）

## 9. 已知的遗留与免责

- **方向着色**：`Shade` 表实现正确且有 6 条单测，但渲染实测**零效果**，根因未定位。规格 §18 已记录。
- **未与原版像素对拍**：本分支从未取得可靠的 vanilla 参照画面。
- **`MC_BUFFER_CHURN`**：`RenderBackend.BufferCreatedObserver` 是全局可变静态属性，违反原则 13（T-861）。
- 本分支由 AI 代理（deepseek-v4.1-flash）在长会话中产出，期间发生过 4 次已记录的错误判断（`0 triangles` 回归、着色两次翻转、S14 假完成、假性能优化）。**请以怀疑态度复核全部结论**。
