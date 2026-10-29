<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

状态：[已评审]
基线：Minecraft 26.3
证据：`docs/specs/render/shader-packs.md`（契约规格）；AGENTS.md 原则 11
最后更新：2026-10-29
授权：用户 2026-10-29 裁决"着色器包一等接入点确实没落地，可以修正"。

# Framework/Rendering/ShaderPacks —— 着色器包契约层

把"外部着色器包"变成引擎的一等输入：包声明渲染目标、阶段、顺序与 uniform，引擎只负责校验与执行。

## 1. 职责范围

负责：

- 声明模型：`TargetSize`（相对/绝对）、`RenderTargetDescription`、`PhaseDescription`、`ShaderPackDescription`。
- 校验：`ShaderPackValidator.Validate` 纯函数，产出可诊断项与执行顺序；非法包**整体拒绝**，不部分执行。
- 引擎能力边界：`KnownUniforms` 列出引擎真正提供的 uniform，包声明之外的必须被拒绝而不是静默忽略。

明确不负责：

- 不解析 Iris 的具体文件格式（`[待定]`，规格 §6）。
- 不执行阶段：WGPU 调用仍在 `OffscreenRenderer` / `WindowedRenderer`；本层只输出"该按什么顺序做什么"。
- 不管理资产：包内容由用户本地提供，仓库不分发（原则 11）。

## 2. 侧别归属

- 归属：引擎层（`Framework`），因为它是渲染管线的形态描述。
- 判定依据：渲染目标的创建与绑定属于 WGPU 抽象层，不能下放到 `Client`；`Client` 只提供世界几何。

## 3. 依赖关系

| 方向 | 对象 | 说明 |
| --- | --- | --- |
| 依赖 | .NET BCL | 纯数据与纯函数，无 GPU 依赖 |
| 被依赖 | 渲染器（待接线） | 按 `PackValidation.ExecutionOrder` 执行阶段 |
| 不依赖 | `Common` / `Client` / `Server` | 契约层不认识游戏对象 |

## 4. 对外契约

| 类型 | 契约 |
| --- | --- |
| `TargetSize.Resolve` | 帧缓冲物理像素 → 目标像素；相对比例至少 1 像素 |
| `ShaderPackValidator.Validate` | 返回 `PackValidation`：合法时含执行顺序，非法时含全部诊断 |
| `PackDiagnostic` | `Kind` + `Where` + `Detail`；不含资产内容与绝对路径 |
| `KnownUniforms.All` | 引擎提供的 uniform 白名单 |

## 5. 校验规则（全部有测试）

名字非空且唯一；阶段的写入/读取目标必须已声明（`framebuffer` 除外，它是隐式目标）；依赖必须存在且无环；
恰好一个阶段写帧缓冲；同一阶段不得重复写同一目标；uniform 必须在白名单内；相对尺寸必须为正。

## 6. 与"回退"的关系

内置管线用**同一份数据**表达（单阶段、写 `framebuffer`、只要 `viewProjection`）。因此加载外部包失败时，
"回退"= 换一个 `ShaderPackDescription` 实例，而不是切换到另一条代码路径——这是原则 11 要求的
"新增 pass 只改包描述，不改引擎代码"的具体形态。

## 7. 未完成

- **尚未接线**：两个渲染器仍在硬编码单阶段，未消费本层的执行顺序；这是下一步（并把接线后的行为规格补进
  `docs/modules/render.md`）。
- Iris 目标版本、特性子集、`.properties`/阶段文件映射仍未定稿。
