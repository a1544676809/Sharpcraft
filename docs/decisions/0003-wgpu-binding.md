<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 MinecraftCSharp contributors -->

状态：[已接受]
日期：2026-10-28

# ADR-0003：WGPU 绑定选用 Silk.NET.WebGPU，首帧采用离屏渲染

## 背景

原则 6 要求图形后端为 **WGPU**，且所有 WGPU 调用收敛在渲染抽象层内。AGENTS §5 把"WGPU 绑定与窗口层选型"列为待评审决策；本次为避免长期悬空，先定绑定与"首帧"路径。

## 决策

1. **绑定**：`Silk.NET.WebGPU` 2.23.0 + 原生库包 `Silk.NET.WebGPU.Native.WGPU` 2.23.0。
2. **首帧路径**：**离屏渲染**（离屏纹理 → 渲染一个方块面批次 → `copy_texture_to_buffer` → 回读 → 存 PNG）。窗口与输入层留到后续里程碑。
3. 所有 WGPU 调用只出现在 `MinecraftCSharp.Framework`（引擎层），游戏逻辑与 `Client` 不得持有 wgpu 句柄。

## 许可与合规

| 项 | 值 |
| --- | --- |
| Silk.NET.WebGPU | MIT |
| Silk.NET.WebGPU.Native.WGPU（含 wgpu-native 二进制） | MIT / Apache-2.0（wgpu-native 上游双许可） |
| 与项目许可证（LGPL-3.0-or-later）兼容 | 是，均为宽松许可 |
| 是否随仓库分发 | 否（由 NuGet 还原，不进仓库） |

## 备选方案

| 方案 | 优点 | 缺点 | 结论 |
| --- | --- | --- | --- |
| **Silk.NET.WebGPU + 原生包** | 官方绑定、二进制随包、跨平台、版本对齐由包管理 | 依赖面较大；原生库版本需跟随包 | **采纳** |
| wgpu-native 官方二进制 + 手写 P/Invoke | 依赖面最小 | 绑定声明、平台二进制分发、版本对齐全部自维护 | 否决 |
| 其他绑定（Ahjo.Wgpu / WGPU.NET 等） | 也许更轻 | 生态与维护度不及 Silk.NET，跨平台二进制支持不明 | 否决 |
| 自研软件光栅化 | 无依赖 | 违反原则 6（必须走 WGPU） | 否决 |

## 后果

- `MinecraftCSharp.Framework` 新增两个包依赖；`Client` / `Common` 不变。
- 首帧验收为"生成 PNG 并与预期特征比对"（清屏色、方块面占比），不依赖窗口系统，可在无显示环境运行（有 GPU/驱动即可）。
- 无可用 adapter 时必须**明确报错并给出可操作提示**，不得静默降级为软件渲染（原则 9：行为偏差需受控）。
- 窗口、输入、交换链与帧循环属于后续里程碑，届时另立 ADR 或补充本文。

## 复查条件

- 若某平台无法获得 adapter（例如无 GPU 的 CI），改为"仅本地验收"，并在 `docs/specs/quality-attributes.md` 记录测量方法。
- 若需支持 WebGPU 之外的图形后端做对照测试，重新评估抽象层边界。
