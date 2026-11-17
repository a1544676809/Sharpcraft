<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

# 风险登记册

> AGENTS.md 原则 12：未解决的不确定性 MUST 登记进本文件（概率 / 影响 / 缓解 / 复查条件）；关闭后保留记录而非删除。
> 原则 15：可能改变可观测行为或视觉效果的简化 MUST 在此建立或更新条目。

| 编号 | 风险 | 概率 | 影响 | 缓解 | 复查条件 | 状态 |
| --- | --- | --- | --- | --- | --- | --- |
| R-001 | 方块实体几何每帧遍历所有已加载区块，原版只遍历可见 section | 中 | 方块实体数量增长后帧时间上升 | 已复用 mesher 与帧状态对象；`BlockEntityVersion` 曾实现但已移除，待重新设计 | 方块实体超过 200 个时测帧时间 | 开放 |
| R-002 | `docs/risks.md` 长期缺失，已知偏差没有集中登记 | 高 | 偏差被遗忘，等效性无法审计 | 本文件建立并录入首批条目 | 每次模块收口时检查 | 处理中 |
| R-003 | element 路径没有原版像素对拍，只有"重构前后不变"的沙盒对比 | 高 | 无法证明与原版一致 | 待建立原版像素采集（Issue #15） | 有 element 路径的像素对拍结果时 | 开放 |
| R-004 | `ItemAtlas` 的验证是合成的，不是原版图集 | 中 | 图集布局差异可能掩盖问题 | `SIM-ITEM-ATLAS-VALIDATION` 已登记；待补原版图集对比 | 拿到原版图集 dump 时 | 开放 |
| R-005 | 铜箱 8 个变体共用 `Chest` 方块实体类型，若原版注册表变化则映射失效 | 低 | 铜箱不可见 | `Repro_022` 覆盖 11 条路径断言 | 基线版本升级时 | 开放 |
| R-006 | 方块实体配对只校验同方块路径，未校验方块状态其余属性 | 低 | 罕见组合下可能误配 | `Repro_028` 覆盖方向与类型矩阵 | 发现新配对异常时 | 开放 |
| R-007 | 钟的钟摆/摇晃动画未接入（`SIM-BE-BELL-NO-SHAKE`），被敲击后钟保持静止 | 高 | 被敲击场景的视觉与原版不同 | 静态几何已按原版 `BellModel` 真值实现；动画待钟方块实体的 `ticks`/`clickDirection` 数据通道 | 补上动画数据源并完成单方块像素对比时 | 开放 |
| R-008 | 饰纹陶罐的陶片图案未解析（`SIM-BE-POT-NO-PATTERNS`），有图案的罐子显示空白侧面 | 高 | 有图案陶罐的视觉与原版不同 | 静态基底与空白侧面已按原版真值实现；图案待 sherds 数据与图案资源映射接入 | 补上陶片图案解析并完成单方块像素对比时 | 开放 |
| R-009 | 饰纹陶罐的 wobble 动画未接入（`SIM-BE-POT-NO-WOBBLE`），被敲击后陶罐保持静止 | 高 | 被敲击场景的视觉与原版不同 | 静态几何已按原版真值实现；动画待 `wobbleProgress` / `wobbleStyle` 数据通道 | 补上 wobble 数据源并完成单方块像素对比时 | 开放 |
| R-010 | 铜傀儡雕像的 SITTING/RUNNING/STAR 姿态未实现（`SIM-BE-COPPER-GOLEM-POSES`），非站立姿态显示站立模型 | 高 | 非站立雕像的轮廓与原版不同 | 四种姿态模型层、四档氧化与 facing 已实现，`SIM-BE-COPPER-GOLEM-POSES` 已移除 | 已解决（Issue #66） | 已解决 |
| R-011 | 讲台书页状态固定（`SIM-BE-LECTERN-STATE`），不播放翻页/打开进度动画 | 高 | 翻页或打开过程的视觉与原版不同 | 静态书本与讲台本体已按原版模型和 `BOOK_STATE` 实现；动画待页面数据与讲台逐刻状态通道 | 接入讲台页面数据并完成单方块像素对比时 | 开放 |
| R-012 | 附魔台书本状态固定（`SIM-BE-ENCHANT-BOOK-STATE`），不播放漂浮/旋转/翻页动画 | 高 | 附魔台书本动态视觉与原版不同 | 静态书本与源 pose 已按 `BookModelDefinition` 和源默认状态实现；动画待时间/flip/open 状态通道 | 接入附魔台动态状态并完成单方块像素对比时 | 开放 |
| R-013 | Shelf 物品槽使用 NBT 静态快照（`SIM-BE-SHELF-ITEMS`），不接实时容器更新与移动动画 | 中 | 运行时物品变化可能延迟，移动动画缺失 | 首切片已实现固定槽位、物品模型和源槽位 pose；后续补容器同步 | 接入 Shelf 容器增量同步并完成动态验证时 | 开放 |
| R-014 | Campfire 火焰/火花/烹饪状态未接入（`SIM-BE-CAMPFIRE-FLAME`） | 高 | 火焰与火花视觉缺失，烹饪状态不可见 | 物品四槽与源 FIXED pose 已实现；火焰待独立状态通道 | 接入 Campfire 火焰/火花状态并完成单方块像素对比时 | 开放 |
| R-015 | Skull 特殊头型未实现（`SIM-BE-SKULL-VARIANTS`），无剩余变体 | 中 | 旧切片中曾缺失的头型已全部补入 | 七种静态头型已实现 | 持续回归 | 已解决 |
| R-016 | Skull 未应用 entityCutoutZOffset（`SIM-BE-SKULL-ZOFFSET`） | 中 | 头像深度顺序/深度偏差与原版不同 | 几何与 UV 已按静态模型实现；偏移依赖 Issue #45 | Issue #45 提供 Z-offset 基础并接入后 | 开放 |
| R-017 | Skull 动画未接入（`SIM-BE-SKULL-ANIM`） | 中 | 被动画驱动的头部转动缺失 | 静态朝向与几何已实现；动画待状态通道 | 接入 Skull 动画状态并补时间序列真值时 | 开放 |
| R-018 | Player 头固定默认 slim Steve 皮肤（`SIM-BE-SKULL-PLAYER-SKIN`），不读取 profile/自定义皮肤 | 高 | 自定义皮肤玩家头显示默认皮肤 | 默认 head+hat 模型与纹理已实现；皮肤缓存待后续通道 | 接入玩家皮肤缓存并完成 profile 真值/像素对比时 | 开放 |
| R-019 | Dragon 颌部动画未接入（`SIM-BE-SKULL-DRAGON-ANIM`） | 中 | Dragon 头的 jaw 不随动画摆动 | 静态 7-box head/jaw 几何已实现；动画待 animationPos 状态通道 | 接入 Dragon 动画状态并补时间序列真值时 | 开放 |
| R-020 | Brushable 刷子粒子/音效/进度动画未接入（`SIM-BE-BRUSHABLE-FX`） | 中 | 刷子动态视觉与音效缺失 | 静态 `FIXED` item pose、dusted/hit_direction 已实现；动态特效待状态通道 | 接入 Brushable 粒子/音效/进度状态并完成单方块像素对比时 | 开放 |
| R-021 | Vault 展示物品不旋转且多 count cluster 未实现（`SIM-BE-VAULT-SPIN` / `SIM-BE-VAULT-COUNT`） | 中 | 动态旋转与 count>1 堆叠视觉与原版不同 | `shared_data.display_item` 的 GROUND 单物品静态显示已实现；spin/count 待状态通道 | 接入 Vault spin/ItemCluster 布局并完成单方块像素对比时 | 开放 |
| R-022 | Banner 图案层未接入（`SIM-BE-BANNER-PATTERNS`） | 中 | 带图案 banner 缺少图案 | 静态 body/flag、ground/wall transform、phase 动画与底色 tint 已实现；patterns 待数据通道 | 接入 banner patterns 并完成单方块像素对比时 | 开放 |
| R-023 | Conduit active 分支未接入（`SIM-BE-CONDUIT-ACTIVE`） | 中 | 激活的 conduit 只显示静态外壳，缺少 cage/wind/eye 与旋转/浮动动画；`ConduitRotation` 固定 0 | active/inactive 都绘制静态 shell；active 数据通道未接 | 接入 conduit active 状态、tickCount 与动画后完成单方块像素对比 | 开放 |
| R-024 | Book/Lectern/Enchant raw 迁移的 UV 口径分歧（Issue #119）——已解决 | 中 | 旧内联 UV（6/64、vertex1 12/64）是 legacy face 校准伪影，非源字面值 | 已切 raw（源字面 setupAnim pose、无 legacy 补偿）；jar 单 cube 探针 BookSeamProbe 确认源 UV=14/64=0.218828；BookVectorProbe 按 #128 归一化（顶点 v/16 + part 位移块单位）消除混合单位。证据：`book-raw-vectors.golden.txt`（5 组×168，part/cube/polygon/vertex keyed 严格位置+UV）、`BookRawReferenceTests`、jar 探针 BookSeamProbe/BookVectorProbe | refactor/raw-book d0c8ad6a，全量 3,431 / 三门禁全绿 | 已解决（Issue #119） |
| R-025 | 旧方块实体内联样本含 legacy face 校准伪影（如 Skull vertex1 UV 0.375，jar 真值 0.125） | 中 | 若按旧内联值验收会误判 raw 正确性 | Skull 已按 jar 探针样本修正；Banner #111 同类已修；用严格原版基准样本替代内联值 | 各 raw 族以 jar 探针严格样本验收时 | 处理中 |
| R-026 | 持有物/手持路径 raw 帧口径（held-item / `PartWorldMatrix` raw 参数）——已验证的设计选择 | 中 | 高 | `ItemInHandMesher` 的 legacy hand frame + jar 拟合 `HeldItemAnchor` 对 raw 实体已源正确：jar 探针 `HeldItemHandFrameProbe.java` origin (-0.25,0.8125,0)×0.9=(-0.225,0.73125,0) 与我们的 legacy `PartWorldMatrix(right_arm)` 逐值一致；14/14 ItemInHand 测试通过。机械切 raw 会使 `anchor·hand·entity` 合成偏差 ~1.07 块，故 `PartWorldMatrix` raw 分支对此用途非必需 | 证据：`ref://mc-26.3-analysis/vectors/HeldItemHandFrameProbe.java`、`docs/modules/render.md` SIM-ENTITY-RAW-HELD-ITEM、现有 ItemInHand 测试 | 已解决（设计选择，Issue #120 关闭） |
| R-027 | raw `CubeListBuilder.mirror` 顶点/面语义不完整（`SIM-ENTITY-RAW-MIRROR`）：源 mirror 交换 cube 的 min/max X 并反转 polygon 顶点顺序，当前 raw 路径仅对 UV 做 U 翻转 | 高 | 中 | 带 mirror 部件的模型（spider/wolf/pig 左半等）在源基准比对中按 part 显式排除；非 mirror 部件位置+UV 严格通过 | 补源 mirror 顶点顺序/面朝向样本并让 raw 路径完整复刻时 | 开放（Issue #121） |
| R-028 | raw 不支持 non-unit `PartPose` scale（`SIM-ENTITY-RAW-SCALE`）：`EntityModelDefinition.FromTree` 对 raw + 非单位 scale 抛 `NotSupportedException`（EnderDragon 等） | 中 | 中 | 相关模型继续走 legacy 路径，行为不退化；raw 范围无非单位 scale 模型 | raw scale 语义实现并有源样本验证时 | 开放（Issue #122） |
| R-029 | raw 零面积实体部件未按源探针完整发射退化面（`SIM-ENTITY-RAW-DEGENERATE`）：零 depth/height 的面（如 chicken-baby 腿/脚/翼，以及 task-62 鱼鳍、蝙蝠翼、蜂脚、 Turtle egg_belly、axolotl body_fin/head_layer 等）在 mesher 中剔除退化面或整块隐藏，只发 8 或 0 顶点；jar 探针按源 6 面发 24 顶点 | 低（面面积 0、视觉无差异） | 中 | 受影响的退化顶点被排除在严格 source UV 比较（显式、可审计）；这些顶点的位置**未被独立 oracle 覆盖**（任务已明确登记为缺口，非静默通过）；非退化顶点严格比较通过 | 让 raw 按源 6 面发射退化面并纳入严格比对，或为受影响族补 full-model world 位置测试 | 开放（Issue #126）；task-62  aquatic/flying 族沿用同一登记 |
| R-030 | raw NodeFrame 的 pose Position+Rotation 在模型空间 probe 中单位混用（像素顶点 + 1/16 块位移）；若把 probe 样本整段比较，会误判为生产 T/R 缺陷 | 中 | 中 | `modelScale=1` 生产输出与源 `T*R` 等价；Book 的 `.364` 是 `modelScale=16` probe 整段比较造成的单位假差，不是生产缺陷 | 后续 raw model-space probe 先归一化旋转顶点项（只除以 16，位移保持块单位），不得把混合样本整体除以 16；新增 pose 样本逐点验证 | 已判定（Issue #128；`RawNodeFramePoseTests` 回归） |
| R-031 | humanoid hat/overlay 在 DSL 中用单一 Grow 近似，源是独立 cube（不同尺寸/pivot/deformation）；`MeshTransformer.scaling` 的 root yOffset 未建模 | 中 | 中 | task-79 整批未迁移 raw、保持 legacy（行为不退化）；28 个 jar 样本已生成；7 个 humanoid mob 补 root 偏移后源正确但 held-item/attack-arm 回归无法单独提交 | DSL 支持独立 overlay cube 维度和 root 偏移、同步 #120 held-item frame 后逐族 1e-5 通过再迁移 | 阻塞于评审（Issue #134） |
| R-032 | 剩余 quadruped 族 DSL 几何（pivot/cube/旋转/多 cube 拆 flat）与 jar 源真值在 1e-5 下不符；horse/cat/polar-bear 另有 layer-scale root 偏移未建模（#134 同类） | 中 | 中 | task-89 整批未迁移 raw、保持 legacy（行为不退化）；27 个 jar 样本已生成 | 逐族按源 probe 修正 DSL 几何 + 补 layer-scale root 偏移后严格 1e-5 通过再迁移 | 阻塞于评审（Issue #137） |
