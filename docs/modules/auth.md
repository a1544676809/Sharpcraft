<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 MinecraftCSharp contributors -->

状态：[已评审]
基线：Minecraft 26.3（认证链路不随游戏版本变化，按官方公开接口实现）
最后更新：2026-10-28
授权：用户于 2026-10-28 明确要求"优先走设备码流程，可以继续做"，作为本模块评审通过依据。

# 认证模块（MinecraftCSharp.Auth）

负责"从微软账号到 Minecraft 会话令牌"的完整链路，供启动器与客户端共用。

## 1. 职责范围

负责：

- 设备码流程（Device Code）发起与轮询。
- Microsoft OAuth2 令牌获取与刷新令牌管理。
- Xbox Live 用户认证 → XSTS 授权。
- Minecraft 服务登录 → 权益/档案校验。
- 令牌缓存、过期判断、静默刷新、错误分类。

不负责：

- 启动编排、版本/资产获取、进程启动（启动器职责，原则 8）。
- 游戏协议与网络会话（`Common`）。
- UI 呈现（启动器）。

## 2. 侧别归属

- 归属：**外围宿主层**。`Common` / `Client` / `Server` MUST NOT 引用本程序集（架构测试已强制）。
- 被引用于：启动器、客户端（仅为接收启动上下文，不自行登录）。

## 3. 链路

| 步骤 | 端点 | 关键点 |
| --- | --- | --- |
| 1 设备码 | `POST login.microsoftonline.com/consumers/oauth2/v2.0/devicecode` | 返回 `user_code` / `verification_uri` / `interval` / `expires_in` |
| 2 轮询 | `POST .../oauth2/v2.0/token`（`grant_type=urn:ietf:params:oauth:grant-type:device_code`） | `authorization_pending` 继续等；`slow_down` 增加间隔 |
| 3 XBL | `POST user.auth.xboxlive.com/user/authenticate` | 请求体 `RpsTicket: "d=<msa_token>"` |
| 4 XSTS | `POST xsts.auth.xboxlive.com/xsts/authorize` | 依赖 XBL token |
| 5 MC 登录 | `POST api.minecraftservices.com/authentication/login_with_xbox` | `identityToken = "XBL3.0 x=<userHash>;<xsts>"` |
| 6 档案 | `GET api.minecraftservices.com/minecraft/profile` | 404 = 无游戏权益 / 无档案 |

作用域：`XboxLive.signin offline_access`（`offline_access` 是拿到刷新令牌的前提）。

## 4. 错误分类（必须可区分）

| 情形 | 识别方式 | 用户可见含义 |
| --- | --- | --- |
| 授权等待中 | `authorization_pending` | 继续等待 |
| 轮询过快 | `slow_down` | 内部退避，不打扰用户 |
| 设备码过期 | `expired_token` | 重新发起 |
| 用户拒绝 | `authorization_declined` | 用户取消 |
| 账号无 Xbox 档案 | XSTS `2148916233` | 需先创建 Xbox 档案 |
| 未成年受限 | XSTS `2148916238` | 需家长同意 |
| 无游戏权益/无档案 | profile 404 | 账号未拥有游戏 |
| 刷新令牌失效 | `invalid_grant` | 需重新交互登录 |

## 5. 令牌与安全

- 令牌、刷新令牌、会话令牌**一律不得**出现在日志、异常消息、遥测中；日志只允许出现账号标识的哈希或长度。
- 刷新令牌**只能**写入平台安全存储（Windows 凭据管理器 / Keychain / Secret Service）。文件存储仅作为后续补充手段，且必须同时满足：仓库外路径、受限权限、加密。
- 客户端进程只接受通过命令行/受限文件传入的**会话令牌**，不保存刷新令牌。
- 内存中的令牌在使用后尽量清零；不足以保证时必须在文档中承认（.NET 字符串不可清零）。

## 6. 客户端 ID

- 设备码流程需要一个 OAuth 客户端 ID。本项目**不**申请专有 Azure 应用（需要项目所有者操作），默认使用社区通用客户端 ID，并允许通过配置覆盖。
- 该选择记入 `docs/risks.md`（随时可能被微软收紧）；一旦被拒，改为自建应用或转向授权码流程。

## 7. 测试要求

- 所有 HTTP 交互通过可注入的 `HttpMessageHandler` 验证，测试不访问网络。
- 轮询状态机必须覆盖：首轮成功、若干次 pending、slow_down 退避、expired、declined、网络失败重试。
- 请求体形状（RpsTicket 前缀、identityToken 拼装）必须有断言，不能只测"能解析响应"。
- 真实登录不进入自动化测试：需要人工输入设备码，属于手工验收项。

## 8. 依赖

- 仅使用 BCL（`System.Net.Http`、`System.Text.Json`）。无新增第三方依赖。

## 9. 未定项

- 令牌安全存储的平台实现细节（原则 7 待决项）。
- 账号列表与离线解锁策略的持久化格式（原则 7.1），本模块只提供"是否已登录正版"的事实，不实现解锁策略。
