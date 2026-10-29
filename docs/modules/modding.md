<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

状态：[已评审]
基线：Minecraft 26.3
证据：AGENTS.md 原则 2；`docs/architecture/boundaries.md`
最后更新：2026-10-29
授权：用户 2026-10-29 裁决"模组热加载确实没落地，可以修正"，本轮补最小可用宿主。

# Modding —— 模组契约与可卸载宿主

把"模组"做成宿主结构的一部分：模组程序集加载进**可卸载**的 `AssemblyLoadContext`，卸载后必须能真正被 GC 回收。

## 1. 职责范围

负责：

- 公开契约：`ISharpcraftMod`（模组入口）与 `IModContext`（宿主提供给模组的能力）。
- 加载：按程序集路径创建 collectible ALC，解析依赖时**共享**契约程序集，其余从模组目录加载。
- 卸载：丢弃所有强引用后 `Unload()`，并提供可断言的"已被回收"检查手段。
- 诊断：加载失败、契约不匹配、卸载未回收都必须可区分。

明确不负责：

- 不做模组清单解析、版本约束、依赖排序（`[待定]`，见 `AGENTS.md` 原则 2）。
- 不做内容注册表的热重建（`SHOULD 保留世界状态，只重建模组注册项`，尚未实现）。
- 不托管模组的资源注入与渲染扩展点（渲染扩展点在原则 11 的着色器包契约里另行定义）。

## 2. 侧别归属

- 归属：**外围宿主层**（模组宿主不是游戏逻辑的任何一侧）。
- 判定依据：它只负责程序集生命周期与契约分发；游戏侧通过契约类型消费模组，不反向依赖宿主。

## 3. 依赖关系

| 方向 | 对象 | 说明 |
| --- | --- | --- |
| 依赖 | .NET BCL | 仅 `System.Reflection`、`System.Runtime.Loader` |
| 被依赖 | 启动器 / 客户端宿主 | 由宿主决定何时加载、何时卸载 |
| 不依赖 | `Common` / `Client` / `Server` | 契约层不得引用游戏程序集，避免把模组绑死在实现上 |

## 4. 对外契约

| 类型 | 契约 |
| --- | --- |
| `ISharpcraftMod` | `Id`（模组标识）、`OnLoad(IModContext)`、`OnUnload()` |
| `IModContext` | 宿主信息（游戏版本、契约版本）与日志出口；**不暴露**游戏内部类型 |
| `ModLoadContext` | collectible ALC；共享契约程序集，其余从模组目录解析 |
| `ModHost` | `Load(path)` → `ModHandle`；`Unload(ModHandle)`；内部只持有 `WeakReference` |
| `ModHandle` | 模组标识与入口类型名；不持有 ALC 的强引用 |

## 5. 卸载验证（原则 2 的硬要求）

"能卸载"必须有自动化证据，不能靠"我调用了 `Unload()`"。现已落地的是**可确定验证**的那部分：

1. **引用纪律**：`ModHost` 对每个 ALC 只保留 `WeakReference`，`ModHandle` 也只持弱引用。
   由 `ModHostUnloadTests` 反射断言（字段/属性类型必须是 `WeakReference<>`），任何一处改成强引用即失败。
2. **收集器语义**：`ModLoadContext` 必须是 `isCollectible == true`，且 `Unload()` 恰好触发一次 `Unloading`。
3. **失败路径**：加载失败（缺文件、无入口类型、入口类型多于一个）不得残留上下文。

**尚未能自动验证**：卸载后 ALC 被 GC 真正回收。为此写了独立进程探针
`tests/Sharpcraft.Modding.UnloadProbe`（进程内 xUnit 会自己持有上下文，测不到），但它在本机实测**连空的可卸载上下文都没有被回收**：

```
measurement: an empty collectible context was NOT reclaimed (R-019)
check 1-2 ok: collectible context raises Unloading once
measurement: a context that loaded one of our assemblies is NOT reclaimed (R-019)
```

探针因此只断言可确定的三项，回收情况作为**测量值输出**，缺口登记为 R-019。这不是"验证通过"，是"验证到此为止"——
在 ALC 回收的观测手段定下来之前，不得声称热卸载已验证。

## 6. 与原版的关系与偏离

- 原版模组（Java）依赖类加载器与 Mixin 注入；本项目的模组是 **.NET 程序集 + 契约**，不追求与原版模组二进制兼容。
- 偏离点：无 Mixin、无字节码改写；扩展点通过契约与事件暴露。

## 7. 未解决问题

- 模组清单格式、API 版本策略、依赖解析规则仍未定稿（`AGENTS.md` 原则 2 `[待定]`）。
- 热重载时保留世界状态、只重建注册项：未实现。
- 测试目前验证"可回收"与"宿主不持强引用"，尚未构建示例模组程序集来自动验证契约分发路径（需要编译期产物，留待清单格式定稿）。
