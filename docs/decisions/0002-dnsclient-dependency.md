<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

状态：[已接受]
日期：2026-10-28

# ADR-0002：引入 DnsClient.NET 做 SRV 记录查询

## 背景

Minecraft 服务器地址可以不带端口（`mcf.ddger.top`），此时客户端必须查询
`_minecraft._tcp.<host>` 的 **SRV 记录**，用记录里的目标主机与端口建立连接。没有这一步，
大量使用 SRV 的服务器完全连不上。

本项目当前没有任何 DNS 解析代码，.NET 的 `Dns` 类**只支持 A/AAAA/MX** 等少数记录类型，
**不提供 SRV**。

## 决策

引入 NuGet 包 **DnsClient 1.8.0**（作者 Michael Conrad），仅用于 `Common/Network/ServerAddressResolver`。

## 许可与合规

| 项 | 值 |
| --- | --- |
| 包许可证 | Apache-2.0 |
| 与项目许可证（LGPL-3.0-or-later）兼容 | 是（Apache-2.0 宽松，可被 LGPL 项目依赖） |
| 传递依赖 | `Microsoft.Extensions.Logging.Abstractions`（MIT） |
| 是否修改 | 否，原样引用 |

许可证检查结论：不引入 copyleft 传染、无专有许可、无"无许可证"依赖。符合原则 10。

## 备选方案

| 方案 | 优点 | 缺点 | 结论 |
| --- | --- | --- | --- |
| 自己实现 UDP DNS 查询 | 零依赖、完全可控 | 需处理名称压缩指针、EDNS、截断重试（TCP 回退）、多种 RCODE；出错面大且难测 | 否决：解析器的边角情况不值得自研 |
| 调用系统 `nslookup`/`dig` | 无依赖 | 依赖外部可执行文件、跨平台差异大、输出解析脆弱、无法用在库中 | 否决 |
| **DnsClient.NET** | 成熟、覆盖真实解析细节、API 简洁 | 新增一个第三方依赖 | **采纳** |

## 后果

- `Sharpcraft.Common` 增加一个直接依赖；启动器与客户端共享。
- SRV 查询失败（NXDOMAIN / 超时）**必须**回退到字面主机 + 25565，不得让地址解析变成硬失败。
- 显式给了端口时**不查 SRV**（与 vanilla 行为一致）。
- 握手包中仍然通告**用户输入的地址**，只有 TCP 目标使用 SRV 结果——转发型服务端依赖这一点。

## 复查条件

- 若将来需要 DNS-over-HTTPS、ECS 或自建解析器（例如为了规避 `Dns.GetHostAddresses` 的阻塞行为），重新评估。
- 若上游包停止维护或出现许可证变更，立即重新评估。
