<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

# 调试控制通道与 MCP 暴露（设计草案）

> 状态：`[待评审]`
> 关联任务：T-782
> 约束：原则 8（客户端可被任意启动器启动，调试通道不得成为启动前提）、原则 13（调试代码可整体删除、游戏程序集不得承载调试开关）

## 1. 目标

让**外部代理/工具**能驱动一个正在运行的客户端做验收：截图、查询状态、触发场景，而不必注入键盘事件或抓窗口。当前痛点：F2 截图功能已具备，但自动化验收仍需按键；`PrintWindow` 抓窗口既不可靠也不跨平台。

## 2. 形态（推荐方案 A）

```
┌────────────────┐   stdio (MCP)   ┌──────────────────┐   localhost TCP   ┌─────────────────────┐
│ 代理 / MCP 客户端 │ ─────────────► │ Sharpcraft.DevMcp │ ────────────────► │ 客户端（--debugControl）│
└────────────────┘                 │ （宿主层工具）      │                   │ Diagnostics 程序集     │
                                   └──────────────────┘                   └─────────────────────┘
```

- **客户端侧**：新增独立程序集 `Sharpcraft.Client.Diagnostics`，内含一个仅回环监听的 JSON-over-TCP 服务。`Sharpcraft.Client` 主程序集**不引用**它；只有传入 `--debugControl` 时由入口按名加载（反射或 `Assembly.Load`），未传入时零存在感。
- **桥接侧**：新增工具 `tools/Sharpcraft.DevMcp`，实现 MCP server（stdio），把 MCP 工具调用转成上图的 TCP 请求。它属于宿主层，游戏程序集不引用。
- **默认关闭**：不传 `--debugControl` 时，客户端既不开端口也不加载诊断程序集。

## 3. 首批暴露的能力（建议）

| MCP 工具 | 作用 | 依赖 |
| --- | --- | --- |
| `screenshot` | 抓一帧到指定路径（复用 `FrameCapture` + 现有命名规则） | 已有 |
| `state` | 返回玩家坐标/朝向、视距、FPS、帧号、当前维度 | 已有 |
| `entities` | 列出附近实体（类型/坐标/数量） | 已有 |
| `items` | 列出附近掉落物（物品 id/数量/坐标） | 已有 |
| `setBlock` / `summon` | 摆场景（等价于 RCON，但无需服务端命令权限） | 需新增 |
| `camera` | 强制相机位姿，便于固定机位对比 | 需新增 |

**注意**：`setBlock`/`summon` 走客户端侧会绕过服务端权威（原则 4/5），因此**不建议**由客户端提供；这类场景仍走 RCON。客户端侧只暴露"只读 + 截图 + 相机"这类**纯客户端表现**能力。

## 4. 为什么不用 RCON

RCON 是**服务端**通道：它能摆场景，但拿不到客户端的帧、相机与渲染状态。二者互补，不互相替代。

## 5. 安全与合规

- 仅监听 `127.0.0.1`，不接受外部连接；随机端口写入启动日志，供桥接工具读取。
- 只在 `--debugControl` 下启用；不改变默认启动路径与性能。
- 诊断程序集与 `tools/Sharpcraft.DevMcp` 必须可整体删除，删除后构建与行为不变（原则 13 的可删除性验收）。
- 不引入任何 Minecraft 资产；不改变客户端对第三方启动器的兼容性（原则 8）。

## 6. 决策

**采纳方案 A（MCP 桥 + 可移除的诊断程序集）**，方案 B 仅作为 A 未落地前的临时手段，不单独排期。

理由：A 提供"查询状态 + 固定机位 + 截图"的组合能力，复杂缺陷（如本会话的掉落物占位盒、箱子 UV）靠截图猜要反复多轮，靠状态查询可一轮定位。

## 7. 易分离性验收（原则 13，硬性）

方案 A 必须满足以下全部条件，否则视为架构缺陷：

| 条件 | 验收方式 |
| --- | --- |
| `Sharpcraft.Client` 主程序集**不引用** `Sharpcraft.Client.Diagnostics` | 架构测试：程序集引用图检查（无该引用边） |
| 不传 `--debugControl` 时不加载诊断程序集、不开端口 | 启动日志无诊断行；`netstat` 无新增监听 |
| 游戏程序集（`Common`/`Client`/`Framework`/`Server`）中环境变量读取数 = 0 | 现有 `tools/verify/check-diagnostics-separation.ps1` 与架构测试 |
| 删除 `Sharpcraft.Client.Diagnostics` 与 `tools/Sharpcraft.DevMcp` 后：构建通过、全量测试通过、同机位截图逐像素一致 | 删除后跑一遍门禁 + 对比截图 |
| 诊断通道不得成为启动前提（原则 8） | 任意第三方启动器按原版参数启动仍正常 |

## 8. 实现切片

| 切片 | 内容 |
| --- | --- |
| A1 | `Sharpcraft.Client.Diagnostics` 程序集骨架：回环 TCP 监听、JSON 行协议、`ping`/`state` 两个只读方法；入口按 `--debugControl` 反射加载 |
| A2 | `screenshot` 方法（复用 `FrameCapture` 与现有命名规则）+ `entities`/`items` 只读查询 |
| A3 | `tools/Sharpcraft.DevMcp`：MCP server（stdio），把上述方法映射成 MCP 工具 |
| A4 | 易分离性验收：删除试验 + 架构测试（程序集引用图 + 环境变量计数） |
