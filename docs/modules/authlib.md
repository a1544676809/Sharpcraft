<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

状态：[已评审]
基线：Minecraft 26.3 / authlib 10.0.77
证据：`ref://mc-26.3-analysis/src`；本机 `authlib-10.0.77.jar`（仅行为核对，不入库）
最后更新：2026-11-07

# AuthLib 中性共享库

承接原版 `com.mojang.authlib` 中客户端、服务端和启动器共用的档案属性、公钥发现与签名校验能力；不承载游戏逻辑或启动器编排。

## 1. 职责范围

负责：

- `GameProfile` / `Property` 等档案属性数据表示（本仓用 `ProfileProperty`）；
- `textures` 属性的 base64 JSON 解析；
- 从 Minecraft services 发现文档加载 `profilePropertyKeys`；
- 使用 `SHA1withRSA` / PKCS#1 验证属性签名；
- 纹理 URL 允许前缀校验。

明确不负责：

- 皮肤下载、缓存、atlas 注册与渲染（`Sharpcraft.Client`）；
- 微软 / Xbox / XSTS 登录（`Sharpcraft.Auth`）；
- 任何具体游戏实体或网络会话。

## 2. 侧别归属

- 归属：中性共享库（原版 authlib 的等价物），不是外围宿主层。
- 依赖：`Sharpcraft.Common`、BCL。
- 被依赖：`Sharpcraft.Client`、`Sharpcraft.Server`、`Sharpcraft.Auth`。
- `Sharpcraft.Auth` 仍是宿主层；`Client` 不直接引用 `Sharpcraft.Auth`。

## 3. 原版源码映射表

| 本模块概念 / 方法 | 原版类与方法路径（`ref://...`） | 精确常量 / 公式 / 结构 | 位序 / 正负号 / 色彩空间约定 | 缺省 / 越界回退值 |
| --- | --- | --- | --- | --- |
| 属性数据 | `com.mojang.authlib.properties.Property`（authlib 10.0.77） | `name` / `value` / 可选 `signature`；`hasSignature` = signature 非空 | base64 字符串按 US-ASCII 字节参与验签 | 无签名 → `UNSIGNED` |
| 签名状态 | `com.mojang.authlib.SignatureState` | `UNSIGNED` / `INVALID` / `SIGNED` | — | 签名串无法解析 → `INVALID` |
| 公钥信息 | `com.mojang.authlib.services.MinecraftServicesKeyInfo` | `KEY_ALGORITHM=RSA`、`SIGNATURE_ALGORITHM=SHA1withRSA`、`KEY_SIZE_BITS=4096`；X.509 `SubjectPublicKeyInfo` DER | Java `KeyFactory("RSA").generatePublic(X509EncodedKeySpec)`；.NET `RSA.ImportSubjectPublicKeyInfo` | 解析失败跳过该 key |
| 公钥获取 | `MinecraftServicesDiscoveryService.getServicesKeySet` / `MinecraftServicesKeyInfo.get` | 发现文档 `authentication.endpoints.getPublicKeys.uri` → `https://api.minecraftservices.com/publickeys`；读取 `profilePropertyKeys[].publicKey` | 延迟加载；失败后 `ServicesKeySet.EMPTY` | `offlineDeveloperMode` 时禁用 key set |
| 属性验签 | `MinecraftServicesSessionService.getPropertySignatureState` / `ServicesKeyInfo.validateProperty` | 有签名时遍历 `keys(PROFILE_PROPERTY)`；任一通过 → `SIGNED`，全部失败 → `INVALID` | `SHA1withRSA`，PKCS#1 v1.5；输入为 `value` 的 ASCII 字节 | 无 `PROFILE_PROPERTY` key → `INVALID` |
| 纹理载荷 | `MinecraftTexturesPayload` / `MinecraftProfileTexture` | `value` base64 → UTF-8 JSON；`textures.SKIN/CAPE/ELYTRA.url`；`SKIN.metadata.model` 为 `slim` | JSON 键区分大小写；`model` 精确匹配 `slim` | 无 `SKIN` / `CAPE` / `ELYTRA` 时对应路径为 null |
| 纹理 URL 策略 | `MinecraftServicesDiscoveryService.isAllowedTextureDomain` | 发现文档 `profiles.endpoints.getTexture.validUris`；把 `{textureId}` 替换为空串后做 `startsWith` | 案例：`https://textures.minecraft.net/texture/` 与 `http://...` | 不在允许前缀 → payload 视为 EMPTY |
| 本地/远程差异 | `PlayerInfo.createSkinLookup` / `SkinManager.createLookup` | `requireSecure = !isLocalPlayer(profile.id)`；远程只接受 `SIGNED`，本地允许 `UNSIGNED`/`INVALID` | — | 远程非签名 → 默认皮肤 |

## 4. 真值向量（Golden Vectors）

采样来源：authlib 10.0.77 类文件与 26.3 源码的常量/控制流；真实签名样本从 sessionserver 获取后仅用于仓库外核对，不提交其原始用户数据。

| # | 输入 | 期望输出 |
| --- | --- | --- |
| 1 | `Property(value, signature=null)` | `UNSIGNED` |
| 2 | `Property(value, signature=valid)`，用测试 RSA 私钥按 SHA1/PKCS#1 签名 | `SIGNED` |
| 3 | 同 #2 但 `value + "x"` 或签名字节被篡改 | `INVALID` |
| 4 | `Property(value, signature="not-base64")` | `INVALID` |
| 5 | 空 key set + 任意签名 | `INVALID` |
| 6 | 发现文档 `validUris=["https://textures.minecraft.net/texture/{textureId}"]` | `https://textures.minecraft.net/texture/abc` 允许；`https://evil.invalid/...` 拒绝 |
| 7 | `{"textures":{"SKIN":{"url":"https://textures.minecraft.net/texture/aaaa","metadata":{"model":"slim"}}}}` | `Slim=true`，`Body` 路径为 `skins/ab1d...`；CAPE/ELYTRA 为 null |
| 8 | `SKIN` URL 不在允许前缀 | 整个 payload 拒绝（EMPTY） |

测试落点：`tests/Sharpcraft.AuthLib.Tests/ProfileSignatureTests.cs`；`tests/Sharpcraft.Client.Tests/World/PlayerSkinTexturesTests.cs`。

## 5. 实现与已知限制

- 公钥加载失败或离线模式 → `ProfileSignatureKeySet.Empty`；签名属性 `INVALID`，远程玩家按原版回退默认皮肤。
- `PlayerSkinCache` 负责 `requireSecure`（本地 UUID 判定）、下载、缓存、atlas 注册；AuthLib 不处理纹理字节。
- `MinecraftServicesKeyClient` 只在客户端进程启动时加载一次；key 轮换需重启会话（原版 lazy key set 同样不主动刷新）。
- 本模块不实现 `PlayerModelType.SLIM` 的几何或皮肤 atlas；这些属于 `Sharpcraft.Client` 的渲染层。
