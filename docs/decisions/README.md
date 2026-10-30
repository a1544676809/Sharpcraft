<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

# 决策记录（ADR）

AGENTS.md 原则 12：不可逆或跨模块的决策必须在此留档，**只追加、不修改历史**。推翻旧决策时要写新 ADR 并在旧条目上标注 superseded。

## 索引

<!-- BEGIN GENERATED: docs/decisions -->

| 文档 | 摘要 | 状态 | 最后更新 |
| --- | --- | --- | --- |
| [`0000-template.md`](0000-template.md) | 背景 / 决策 / 影响面 / 替代方案 / 复查条件 | 模板 | - |
| [`0001-architecture-rules-as-tests.md`](0001-architecture-rules-as-tests.md) | 架构规则写在文档里只能靠人工评审执行。 | 已采纳 | - |
| [`0002-dnsclient-dependency.md`](0002-dnsclient-dependency.md) | Minecraft 服务器地址可以不带端口（mcf.ddger.top），此时客户端必须查询 _minecraft._tcp.<host> 的 SRV 记录，用记录里的目标主机与端口建立连接。 | 已接受 | - |
| [`0003-wgpu-binding.md`](0003-wgpu-binding.md) | 原则 6 要求图形后端为 WGPU，且所有 WGPU 调用收敛在渲染抽象层内。 | 已接受 | - |
| [`0004-window-layer.md`](0004-window-layer.md) | 原则 6 要求窗口创建、输入等平台能力走抽象接口并给出各平台实现；AGENTS §5 把"WGPU 绑定与窗口层选型"列为待决项。 | 已接受 | - |
| [`0005-asset-pack-and-official-index.md`](0005-asset-pack-and-official-index.md) | docs/modules/assets.md 初稿把 jar 内资产按目录散装解到用户数据目录（assets/<版本>/textures/block/...，共 11,813 个文件）。 | 已接受 | - |
| [`0006-offline-login-gating.md`](0006-offline-login-gating.md) | 原则 7.1 早已规定：启动器必须原生支持多账号；只有存在至少一个已通过官方认证的正版账号时才允许离线登录；正版账号全部删除后离线能力必须立即失效；不得留任何后门。 | 已接受 | - |
| [`0007-resources-live-in-the-client.md`](0007-resources-live-in-the-client.md) | T-034 把资源解析层放在启动器（外围宿主层），并计划由启动器预生成 atlas.png + atlas.json 交给客户端采样。 | 已接受 | - |
| [`0008-generated-documentation-index.md`](0008-generated-documentation-index.md) | 目录索引改为工具生成，CI 检查索引与源码模块覆盖 | 已接受 | - |
| [`0009-access-control-applies-to-the-launcher.md`](0009-access-control-applies-to-the-launcher.md) | 澄清宪章 7.1 的适用范围，明确游戏侧不加严不判定 | 已接受 | - |
| [`0010-ui-hot-reload-scope.md`](0010-ui-hot-reload-scope.md) | 热加载的适用构建、路径边界与不可退化的约束 | 已接受 | - |
| [`0011-accepted-frame-spike-on-bulk-upload.md`](0011-accepted-frame-spike-on-bulk-upload.md) | 以一个受控偏差换取更早出现的地形，并给出复查条件 | 已接受 | - |
| [`0012-chat-type-registry.md`](0012-chat-type-registry.md) | 日期：2026-10-30 相关：T-096、T-142、原则 5（三侧边界）、原则 12（规格即矩阵） | **已按原版核对**（实现前仍按流程提交评审） | - |
| [`0013-gamma-space-ui-blending.md`](0013-gamma-space-ui-blending.md) | 我们的交换链使用 BGRA8UnormSrgb，因此 GPU 在线性空间做混合；原版 Minecraft 没有 sRGB 帧缓冲，混合发生在 gamma 空间。 | 已采纳（用户 2026-10-30 明确选择方案 A） | - |
| [`0014-gui-sprite-atlas-and-nine-slice.md`](0014-gui-sprite-atlas-and-nine-slice.md) | 提示框（tooltip）在原版由两张 GUI 贴图叠成：tooltip/background 与 tooltip/frame，后者为 nine_slice（border = 10、stretch_inner = true）。 | **提议（待评审）** | - |
| [`0015-resource-pack-stack-and-reload.md`](0015-resource-pack-stack-and-reload.md) | 提示框贴图目前只从"基础包 zip"读取。 | **提议（待评审）** | - |
| [`0016-resource-pack-file-watch.md`](0016-resource-pack-file-watch.md) | 包作者改一个贴图就要重启客户端。 | **提议（待评审）** | - |
| [`0017-reload-invalidation-by-layout.md`](0017-reload-invalidation-by-layout.md) | 原版重载成功后直接 levelExtractor.allChanged()（全部区块几何重建，Minecraft.reloadResourcePacks 成功分支）。 | **已接受**（所有者 2026-10-30 确认：允许略微卡顿，采用分层失效） | - |
| [`0018-native-vulkan-backend.md`](0018-native-vulkan-backend.md) | 为获得几何/细分/Mesh Shader/硬件光追/VRS 等 Vulkan 独有能力而新增原生后端的成本、收益与门槛 | 提议 | - |

<!-- END GENERATED: docs/decisions -->

## 规则

- 文件名：`NNNN-<短横线标题>.md`，四位递增编号，不复用。
- 模板：[`0000-template.md`](0000-template.md)。
- 每份 ADR 必须写清"背景 / 决策 / 影响面 / 替代方案 / 复查条件"。
- 决策涉及许可证或产品策略（原则 7.1、10）时，必须在 ADR 中引用项目所有者的确认。
