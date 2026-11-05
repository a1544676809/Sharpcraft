<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

状态：[已评审]
基线：Minecraft 26.3
最后更新：2026-11-05

# Common/Reference —— 实现与原版类的对应元数据

`ReferenceClassAttribute`：标注 Sharpcraft 的每个类型对应原版（26.3）哪个类，是 "实现 ↔ 原版类" 映射的**唯一真相源**（AGENTS.md §3.5）。由 `tools/Sharpcraft.ClassMap` 生成/校验；只存类名与 `ref://mc-26.3-analysis/...` 指针，**不存放任何原版源码**（原则 3）。

## 实现与原版类对应

| Sharpcraft 类型 | Kind | 原版类 | 说明 |
| --- | --- | --- | --- |
| `Sharpcraft.Common.Reference.ReferenceClassAttribute` | New | 无（工具元数据） | 我方引入的文档/治理元数据，无原版对应 |
| `Sharpcraft.Common.Reference.ReferenceKind` | New | 无 | 同上 |
| `Sharpcraft.Common.Reference.ReferenceStatus` | New | 无 | 同上 |

- 用法：`[ReferenceClass("net.minecraft.client.multiplayer.ClientPacketListener", "ref://mc-26.3-analysis/src/net/minecraft/client/multiplayer/ClientPacketListener.java", Kind = ReferenceKind.Mirror, Status = ReferenceStatus.Full, Note = "...")]`。
- `Kind`：`Mirror` / `Merged` / `Adapter` / `New` / `Stub`；`Status`：`Full` / `Partial` / `NotStarted`。
- 流程：加注解 → `dotnet run --project tools/Sharpcraft.ClassMap -- --write` 生成 `docs/architecture/class-map.generated.md`；`--check` 由架构测试与 CI 门禁执行；存量未注解类型在 `docs/src/.classmap-baseline.txt`（只减不增）。
