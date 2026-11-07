<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

<!-- doc
title: Sharpcraft.AuthLib —— 中性档案/验签共享库
summary: 原版 com.mojang.authlib 的共享部分：档案属性、公钥发现、SHA1withRSA 验签与皮肤纹理载荷
status: 已实现
side: Common
updated: 2026-11-07
-->

# Sharpcraft.AuthLib —— 中性档案/验签共享库

承接原版 `com.mojang.authlib` 中可跨客户端、服务端与启动器复用的纯数据与密码学部分；不包含任何游戏渲染、网络会话或启动器编排。

## 1. 职责范围

负责：

- `ProfileProperty`：`textures` 等档案属性的 name/value/signature 表示。
- `ProfileSignatureState`：`Unsigned` / `Invalid` / `Signed`。
- `ProfileSignatureKey` / `ProfileSignatureKeySet`：RSA 公钥包装、`SHA1withRSA` PKCS#1 验签、纹理 URL 前缀策略。
- `MinecraftServicesKeyClient`：从 Minecraft services 发现文档和 `publickeys` 端点加载 `profilePropertyKeys`，失败时返回空 key set。
- `PlayerSkinTextures` / `PlayerSkinTexturesParser`：解析 `textures` profile property 的 base64 JSON（SKIN/CAPE/ELYTRA、slim 元数据、纹理 URL 校验）。

明确不负责：

- 皮肤下载、缓存与渲染 atlas（`Sharpcraft.Client` 的 `PlayerSkinCache` / `EntitySkinLibrary`）。
- 微软 / Xbox / XSTS 登录链路（`Sharpcraft.Auth`）。
- 任何游戏世界、渲染、窗口或 GPU 类型。

## 2. 侧别归属

- 归属：中性共享库；不是 Client/Server 游戏逻辑，也不是外围宿主层。
- 依赖方向：只依赖 `Sharpcraft.Common` 与 BCL；`Client` / `Server` / `Auth` 可引用它。
- 禁止反向引用 `Client` / `Server` / `Framework` / `Launcher` / `Auth`（`AssemblyBoundaryTests` 守护）。

## 3. 关键类型

| 类型 | 作用 |
| --- | --- |
| `ProfileProperty` | 线上属性值 + 可选签名 |
| `ProfileSignatureKey` | RSA 公钥与 `SHA1withRSA` 验证 |
| `ProfileSignatureKeySet` | 信任公钥集合、`Validate`、纹理 URL 前缀判定 |
| `MinecraftServicesKeyClient` | 发现文档 + 公钥 HTTP 加载器 |
| `PlayerSkinTexturesParser` | `textures` 属性载荷解析与 URL 策略 |

## 4. 与原版对应

| 本仓实现 | 26.3 / authlib 对应 |
| --- | --- |
| `ProfileProperty` | `com.mojang.authlib.properties.Property` |
| `ProfileSignatureState` | `com.mojang.authlib.SignatureState` |
| `ProfileSignatureKey` / `ProfileSignatureKeySet` | `MinecraftServicesKeyInfo` / `ServicesKeySet` |
| `MinecraftServicesKeyClient` | `MinecraftServicesSessionService.unpackTextures` 的公钥部分 + `MinecraftServicesDiscoveryService` |
| `PlayerSkinTexturesParser` | `MinecraftProfileTextures` / `MinecraftProfileTexture` 解析 |

<!-- BEGIN GENERATED: class-map -->

| Project | Sharpcraft type | Kind | Status | Reference class | Reference path | Note |
| --- | --- | --- | --- | --- | --- | --- |
| Sharpcraft.AuthLib | Sharpcraft.AuthLib.MinecraftServicesKeyClient | Adapter | Partial | com.mojang.authlib.services.MinecraftServicesKeyInfo | ref://mc-26.3-analysis/src | Discovery + publickeys loader; no client/server game types |
| Sharpcraft.AuthLib | Sharpcraft.AuthLib.PlayerSkinTexture | Mirror | Partial | net.minecraft.client.resources.SkinManager.TextureCache | ref://mc-26.3-analysis/src/net/minecraft/client/resources/SkinManager.java | One cached profile texture; download is handled by the client skin cache |
| Sharpcraft.AuthLib | Sharpcraft.AuthLib.PlayerSkinTextures | Mirror | Partial | net.minecraft.world.entity.player.PlayerSkin | ref://mc-26.3-analysis/src/net/minecraft/world/entity/player/PlayerSkin.java | Body/cape/elytra paths, slim model flag and signature state |
| Sharpcraft.AuthLib | Sharpcraft.AuthLib.PlayerSkinTexturesParser | Adapter | Partial | net.minecraft.client.resources.SkinManager | ref://mc-26.3-analysis/src/net/minecraft/client/resources/SkinManager.java | Decodes the textures profile property and applies the discovery URL policy |
| Sharpcraft.AuthLib | Sharpcraft.AuthLib.ProfileProperty | Mirror | Partial | com.mojang.authlib.properties.Property | ref://mc-26.3-analysis/src | Wire property value plus optional signature; authlib is an external library, not part of the 26.3 source tree |
| Sharpcraft.AuthLib | Sharpcraft.AuthLib.ProfileSignatureKey | Adapter | Partial | com.mojang.authlib.services.MinecraftServicesKeyInfo | ref://mc-26.3-analysis/src | RSA public key wrapper; discovery/download lives in MinecraftServicesKeyClient |
| Sharpcraft.AuthLib | Sharpcraft.AuthLib.ProfileSignatureKeySet | Adapter | Partial | com.mojang.authlib.services.ServicesKeySet | ref://mc-26.3-analysis/src | Immutable key set plus allowed texture URL prefixes |
| Sharpcraft.AuthLib | Sharpcraft.AuthLib.ProfileSignatureState | Mirror | Full | com.mojang.authlib.SignatureState | ref://mc-26.3-analysis/src | UNSIGNED / INVALID / SIGNED |

<!-- END GENERATED: class-map -->
