<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

# benchmarks

性能基线套件的代码与结果。口径、噪声源与阈值建议见 `docs/specs/performance-baseline.md`。

```
dotnet run -c Release --project benchmarks/Sharpcraft.Benchmarks -- --out benchmarks/results/<date>-baseline.md
```

- `results/` 存每次运行的原始读数（Markdown 表格），文件名用日期，历史结果不删除，便于同机前后对比。
- 结果文件只写运行时间、运行时版本与核数，不写机器名。
- 本目录不得出现任何 Minecraft 资产或捕获的原始数据包；真实包只可通过 `--real <path>` 从仓库外临时读取。
