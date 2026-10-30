<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

# 0012 聊天类型注册表的归属与解码

状态：**已按原版核对**（实现前仍按流程提交评审）
日期：2026-10-30
相关：T-096、T-142、原则 5（三侧边界）、原则 12（规格即矩阵）

## 背景

玩家消息的显示形态（`<名字> 内容`、`名字 » 内容`、`[名字 -> 目标] 内容` 等）**不是客户端硬编码的**。
原版把这条规则放进 `chat_type` 注册表：每个条目带一个 `chat` 装饰（翻译键 + 参数列表，参数取
`sender` / `content` / `target`）。收包时 `player_chat` / `disguised_chat` 只带**聊天类型 id** 与名字组件，
客户端用 `ChatType.Bound.decorate(content)` 展开成最终组件。

因此没有注册表就只能显示 `名字: 内容` 这种近似——这正是当前与官方不一致的地方。

## 需要的数据

配置阶段服务端会同步注册表（`registry_data`）。`minecraft:chat_type` 每个条目含：

| 字段 | 用途 |
| --- | --- |
| `chat.translation_key` | 装饰的翻译键（如 `chat.type.text`）|
| `chat.parameters` | 参数顺序，取值 `sender` / `content` / `target` |
| `narration.translation_key` + 参数 | 朗读文本，暂不使用 |

聊天类型 id 在同一会话内稳定，且**由服务端在配置阶段给出**，所以必须按会话缓存，不能写死序号。

## 决策

1. **注册表数据的归属**：`Common/Network` 下的只读快照类型 `ChatTypeRegistry`，只保存
   `id → (translationKey, parameters[])`。它属于**协议数据**，不含任何渲染或本地化逻辑，因此放在 Common。
2. **解码位置**：`PlayerChatPackets` / `DisguisedChatPackets` 只负责把 id 与各组件读出来，
   **不做**装饰；装饰由调用方（会话层）用 `ChatTypeRegistry` + `LanguageTable` 完成。
   这样 Common 不依赖本地化实现，边界保持单向。
3. **装饰算法**：按 `parameters` 顺序把 `sender` / `content` / `target` 的**样式段**依次代入翻译键的
   `%s` 占位（语言表已支持 `%s` / `%1$s` / `%%`）。缺失的参数（如非 `msg_command` 没有 `target`）
   按空段处理并记录诊断，不静默吞掉。
4. **降级**：注册表尚未收到时，退回当前的 `名字: 内容` 形态并在日志说明一次，不阻塞进服。

## 影响面

| 模块 | 变更 |
| --- | --- |
| `Common/Network` | 新增 `ChatTypeRegistry`（解析 `registry_data` 中 `minecraft:chat_type`）；`PlayerChatPackets` 输出聊天类型 id 与名字/目标组件 |
| `Launcher`（会话层） | 收到注册表后填充；`player_chat` / `disguised_chat` 走装饰 |
| `Client/Ui` | 无变更（仍接收样式段）|

## 被否决的方案

- **把 `chat.type.text` 等常见键写死在客户端**：注册表可由服务端/数据包改变，写死会在自定义服务端上显示错误，
  且违反原则 4「不臆造协议语义」。
- **在 `Common/Network` 里做装饰**：会让 Common 依赖本地化实现，破坏依赖方向（原则 5）。

## 依据原版核对（取代原先的"待确认"）

| 问题 | 原版事实 | 证据 |
| --- | --- | --- |
| 注册表归属 | 聊天类型**按 id 经注册表解析**（`ByteBufCodecs.holder(Registries.CHAT_TYPE, …)`），是**每连接**状态 | `ChatType.STREAM_CODEC` |
| 装饰在哪一层 | 编解码只产出 id 与 name/target 组件；装饰由客户端 `ChatListener.showMessageToPlayer` 调 `ChatType.Bound.decorate` 完成 | `ChatListener` / `ChatType.Bound.decorate` |
| 注册表缺失时 | `registry.getOrThrow(chatType)` **直接抛异常**——原版**没有**"退回 `名字: 内容`"这种降级 | 同上 |
| 参数代入 | `SENDER → chatType.name()`、`TARGET → targetName 或 EMPTY`、`CONTENT → content`，再 `translatable(key, params).withStyle(decoration.style)` | `ChatTypeDecoration.resolveParameters` |

**据此修正第 4 条决策**：不再"退回 `名字: 内容`"。注册表缺少该 id 时，**丢弃该条消息并记录一次诊断**
（原版会抛异常，我们选择不崩客户端，但绝不猜一个形态显示）。

另外确认：`ChatType.DEFAULT_CHAT_DECORATION = withSender("chat.type.text")`，参数为 `[SENDER, CONTENT]`，
与语言表里的 `<%s> %s` 对应。
