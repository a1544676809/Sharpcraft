<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
# Sharpcraft 全项目原版一致性对照体系（v2）

## 1. 为什么视觉对照必须分“同引擎回归”与“跨引擎语义”两轨
原版 26.3（OpenGL）与 Sharpcraft（wgpu / Reversed-Z / 小波 OIT）在光栅化亚像素、雾效 LUT 与 Gamma 路径上存在合法的底层差异，**整屏逐像素相等在跨引擎对拍中必然产生大量误报**。因此本体系把验证拆成两条互补轨道：
1. **数值与结构全量精确对拍（`--suite state|protocol|physics|blockmodel|entity|vis`）**：把所有确定性逻辑（状态解码、包常量、运动步进、盒模型 UV/AABB、遮挡连通矩阵）全部压到无 GPU 的 Canonical TSV 对拍，实现 `1e-4` 级逐字段零误差门禁。
2. **视觉双模式 ROI 对拍（`image_roi_diff.py`）**：
   * `--mode regression`（同引擎 `OffscreenRenderer` 回归锁）：严格校验 `bg_leak=0`、`IoU >= 0.99`、`MAE <= 1.0`。
   * `--mode cross-engine`（跨引擎原版对照）：忽略跨 API 亚像素与全局曝光微差，仅校验各 ROI **剪影重合度（`IoU >= 0.90`）**、**主色调一致（RGB 通道均值差 `<= 28`）** 与 **前景区域非空**。

## 2. 统一多套件命令行
* 原版端：`ModelGeometryProbe.java` 支持 `--suite all|state|entity|vis`
* 自研端：`Sharpcraft.Equivalence.Audit` 支持 `--suite all|state|protocol|physics|blockmodel|entity|vis`
* 统一汇总：`run_all.ps1 -Jar <26.3.jar>` 自动跑批并输出 `PASS/FAIL` 总表与 `report.json`。
