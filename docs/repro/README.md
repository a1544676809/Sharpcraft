<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 MinecraftCSharp contributors -->

# 最小复现用例库（Repro）

AGENTS.md 原则 12：修复缺陷前必须先落一个**最小复现用例**，修复后永久保留为回归。

## 约定

- 位置：`tests/<受测工程>/Repro/Repro_<编号>Tests.cs`
- 编号：缺陷编号（对应 `docs/traceability.md` ID 或 issue 编号），同一缺陷一个文件。
- 内容：只做一件事——用尽可能少的代码触发该缺陷，断言**期望行为**（修复前失败、修复后通过）。
- 依赖：不得依赖网络、真实 jar、用户目录或时间；需要外部输入时把输入内联为常量或 `TestData/` 数据文件。

## 文件骨架

```csharp
// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 MinecraftCSharp contributors

using Xunit;

namespace <受测工程>.Tests.Repro;

/// <summary>
/// Repro for issue #编号: 一句话描述可观测到的错误行为。
/// Kept as a regression test after the fix (AGENTS.md principle 12).
/// </summary>
public sealed class Repro_<编号>Tests
{
    [Fact]
    public void ReproducesTheReportedBehaviour()
    {
        // Arrange: 最小输入
        // Act: 触发缺陷的那一次调用
        // Assert: 期望行为（修复前此断言失败）
    }
}
```

## 与追踪矩阵的关系

- 缺陷修复完成后，在 `docs/traceability.md` 增加一行，把"规格章节"指向该缺陷对应的行为规格（没有就补一节），测试列指向本文件。
- 回归用例不得因为"表达方式过时"而删除；确需替换时，先写新用例再删旧用例，并在提交信息里说明。
