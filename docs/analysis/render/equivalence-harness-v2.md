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

### 2.1 状态覆盖基线（`--suite statematrix`）

```
dotnet run --project tools/Sharpcraft.Equivalence.Audit -- --suite statematrix --out <output.tsv>
```

输出 `type_kind	type_id	type_name	state_class	layer_class	definition_or_layer	sample_file	sample_key	status	strict`，并在文件尾与 stdout 输出各态 counts 和每层 counts。

* `SOURCE_SAMPLE`：`tests/Sharpcraft.Client.Tests/Repro/Data/*.reference.txt` 有对应源样本；`sample_file` 是真实相对路径，`sample_key` 是该文件真实存在的数据 key（rig 层名/状态名）。
* `SOURCE_CONSTRAINT`：公式/常量样本（`creeper-energy-swirl-uv.reference.txt`、`glint-texture-transform.reference.txt`）；key 形如 `millis|scale`。
* `NO_SOURCE`：源有该 `type×state×layer` 行为但无样本，`strict=no`，**不得计入覆盖通过**，附优先级。
* `UNKNOWN`：方块实体有生产 renderer 但源行为（静态/动态、是否需要 keyed 样本）无法从现有元数据确认；`strict=no`，不得默认当作 NO_SOURCE（Issue #219）。
* **元数据驱动，不再交叉积**（Issue #219）：layer 由 `EntityModelDefinition.RenderLayers` 中 `Model != null` 的真实层决定（共享 `DefaultRenderLayers` 占位层 `Model==null` 不计）；state 由 `EntityModelResolver.TryResolve(type, baby:true)`（baby）、`CreeperModelDefinition.CreeperEntityTypeId`/`WitherBossModelDefinition.WitherEntityTypeId`（powered/energy）与 `StateLayerAffinity`（state→可影响 layer）决定。a `non-humanoid × elytra/armor/trim/glint`、`non-creeper/wither × powered/energy`、`no-baby × baby` 不再产生伪行。
* 行来自 `EntityModelResolver.Definitions` / `VanillaBlockEntityTypes` 的机器枚举，样本 key 来自文件解析；没有手写假路径。
* 校验：`StateMatrixBaseline.Build` 对每条 strict 行检查文件与 key 存在，缺失即抛错；`StateMatrixBaselineTests` 复验无伪行、UNKNOWN 不算 NO_SOURCE、strict 行文件/key 真实。NO_SOURCE/UNKNOWN 行不得标 strict。
* 修正后规模（Issue #219）：约 460 行，`SOURCE_SAMPLE≈136`/`SOURCE_CONSTRAINT=55`/`NO_SOURCE≈224`/`UNKNOWN=45`；真实缺口以 `pose/base` 为主。禁止用 golden hash 自证——`SOURCE_SAMPLE/CONSTRAINT` 必须指向真实源样本/公式文件。

**语义边界**：这是覆盖基线，不是全状态等效证明。它回答“哪些生产类型/状态/layer 有可核对源样本或公式约束，哪些没有”，不替代逐像素/逐顶点 strict 对拍；`NO_SOURCE` 明确表示未覆盖。
