<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

状态：[已评审]
基线：Minecraft 26.3（认证链路不随游戏版本变化，按官方公开接口实现）
最后更新：2026-10-28
授权：用户于 2026-10-28 明确要求"优先走设备码流程，可以继续做"，作为本模块评审通过依据。

# 认证模块（Sharpcraft.Auth）

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
| 1 设备码 | `POST login.live.com/oauth20_connect.srf` | 表单编码；`response_type=device_code`；返回 `user_code` / `verification_uri` / `interval` / `expires_in` |
| 2 轮询 | `POST login.live.com/oauth20_token.srf`（`grant_type=urn:ietf:params:oauth:grant-type:device_code`） | `authorization_pending` 继续等；`slow_down` 增加间隔 |
| 3 XBL | `POST user.auth.xboxlive.com/user/authenticate` | JSON（PascalCase）；`RpsTicket: "d=<msa_token>"` |
| 4 XSTS | `POST xsts.auth.xboxlive.com/xsts/authorize` | JSON；依赖 XBL token |
| 5 MC 登录 | `POST api.minecraftservices.com/authentication/login_with_xbox` | JSON；`identityToken = "XBL3.0 x=<userHash>;<xsts>"` |
| 6 档案 | `GET api.minecraftservices.com/minecraft/profile` | 404 = 无游戏权益 / 无档案 |

### 3.1 两套身份端点不可混用（实测踩过）

| 客户端 ID 形态 | 端点族 | 设备码请求体 |
| --- | --- | --- |
| MSA 应用（如 `00000000402b5328`，非 GUID） | `login.live.com`（`oauth20_connect.srf` / `oauth20_token.srf`） | 需加 `response_type=device_code` |
| Azure AD 应用注册（GUID） | `login.microsoftonline.com/{tenant}/oauth2/v2.0/{devicecode,token}` | 不要 `response_type` |

实测错误链（两次都真实发生）：

1. 把 MSA 应用 ID 发到 AAD v2 端点 → `AADSTS700016: Application with identifier ... was not found in the directory ...`；
2. 用 **JSON** 而不是表单编码 → `AADSTS900144: The request body must contain the following parameter: 'client_id'`。

两项都已由 `IdentityEndpoints` 的预设（`MicrosoftAccount` / `AzureAd`）与表单编码固定住，并有测试守住。

作用域：`XboxLive.signin offline_access`（`offline_access` 是拿到刷新令牌的前提）。

### 3.2 真实登录验收记录

| 日期 | 结果 |
| --- | --- |
| 2026-10-28 | **成功**：设备码 → MSA 令牌 → XBL → XSTS → Minecraft 会话 → 档案，返回玩家档案名与 UUID，会话有效期约 24 小时 |

档案名与 UUID 按隐私要求不写入文档；验收方式见下文"手工验收"。

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
- **开发阶段允许持久化令牌**（用户 2026-10-28 明确授权），但必须同时满足：
  - 位置在仓库之外：默认 `%LOCALAPPDATA%/Sharpcraft/tokens.json`；
  - `TokenStore.EnsureOutsideRepository` 会向上查找 `.git`，一旦路径落在工作树内直接抛错；
  - `.gitignore` 额外屏蔽 `tokens.json` / `*tokens.json` 作为第二道防线；
  - 非 Windows 平台把文件权限收窄到当前用户（best effort）。
- 正式发布前必须改为平台安全存储（Windows 凭据管理器 / Keychain / Secret Service），见 §9。
- 客户端进程只接受通过命令行/受限文件传入的**会话令牌**，不保存刷新令牌。
- 内存中的令牌在使用后尽量清零；不足以保证时必须在文档中承认（.NET 字符串不可清零）。
- `logout` 命令删除缓存；刷新令牌失效时缓存会被自动丢弃并回落到交互登录。

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

- 令牌安全存储的平台实现细节（原则 7 待决项）。当前仅满足"仓库外 + 权限收紧"，尚未接入 OS 凭据库。
- 账号列表与离线解锁策略的持久化格式（原则 7.1），本模块只提供"是否已登录正版"的事实，不实现解锁策略。

## 10. 手工验收

```
dotnet run --project src/Sharpcraft.Launcher -- login
```

1. 打开输出提示的 `https://www.microsoft.com/link`，输入显示的用户码；
2. 用微软账号登录并同意；
3. 期望输出 `Signed in as <名字> (<UUID>)`，退出码 0。

失败时输出会带分类原因（`[NoXboxProfile]` / `[AccountRestricted]` / `[NoGameOwnership]` / `[RefreshTokenRejected]`）与微软的服务端描述，便于定位。

## 传输层（重试、诊断、连接）

认证链路对网络抖动敏感，以下三条是实测逼出来的，不是预设：

| 事项 | 具体做法 | 依据 |
| --- | --- | --- |
| 有界重试 | 每次请求最多 3 次，间隔 400 ms / 1200 ms；只对传输类失败重试（`HttpRequestException` / `IOException` / 超时），调用方取消不重试 | 端点重复调用是安全的：再登录一次只是签发新会话；该端点实测会间歇性 RST |
| 请求可诊断 | `MC_TRANSPORT_DEBUG=1` 时打印每次请求的方法与 URI、重试次数、异常链 | 失败按主机分类，定位到具体调用才能修 |
| 连接策略 | 地址并发竞速（IPv4 优先但不排除 IPv6）、显式 HTTP/1.1、带 User-Agent | 当某个地址族不可达时，逐地址串行连接会一路等到超时；竞速让可达地址立即胜出，省掉整段等待 |

失败分类：重试耗尽后抛 `AuthError.Transport`，消息含尝试次数；令牌与刷新令牌一律不进消息与日志。

