<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

<!-- doc
title: 着色器组织规范（规格）
summary: 文件布局、命名、include、绑定单一来源、启动期自检、与测试门禁；WGSL 语境下的严谨约定
status: 草稿
-->

# 着色器组织规范

定位：本项目**不照搬**原版的目录与 GLSL 组织（语言不同），但**严谨性对齐**。原版实测 93 条目
（`core/` 64 一管线一对、`include/` 18 共享片段、`post/` 10 效果链），我们的对应关系见 §8。
对比与差距见 `docs/analysis/render/shader-organisation.md`。

## 1. 目录与文件（MUST）

```
src/Sharpcraft.Framework/Rendering/Shaders/
  pipelines/   terrain.wgsl  sky.wgsl  ui.wgsl  ui_text.wgsl  ...   # 一条管线一个文件
  include/     camera.wgsl  fog.wgsl  bindings.wgsl  ...             # 只放共享声明，不含入口
```

- **一条管线一个文件**（MUST）：文件名即管线名，与 vanlila `core/` 的"文件即管线"同义。
- `include/` 里的文件 **MUST NOT** 声明任何入口点（`@vertex/@fragment/@compute`）。
- 着色器 **MUST NOT** 再以内联字符串出现在 C# 里（由 §7 的门禁测试保证）。

## 2. 命名（MUST）

| 项 | 规则 |
| --- | --- |
| 入口点 | `vs_<pipeline>` / `fs_<pipeline>` / `cs_<pipeline>` |
| 共享结构体 | 全大写驼峰前缀 `Sc`（如 `ScCamera`、`ScFog`），避免与未来包内符号冲突 |
| uniform 变量 | `<结构体名小写>`（如 `camera`、`fog`）|
| 资源变量 | 语义名（`atlasTexture`、`atlasSampler`），不出现 `tex0` 这类位置名 |

## 3. include（MUST）

- 语法：`//#include "include/camera.wgsl"`，每行一条，放在文件头。
- 组合器负责拼接；**展开深度上限 4**；重复 include 只展开一次；缺失文件立即失败并指明"哪个管线、哪一行、找的是什么"。
- WGSL 原生无预处理器 ⇒ 组合器是我们自己的实现，**MUST** 有单元测试（嵌套、重复、缺失、循环）。

## 4. 绑定契约单一来源（MUST）

`ShaderBindings` 是唯一事实来源，`group` 恒为 0：

| binding | 语义 | 类型 | 可见阶段 |
| --- | --- | --- | --- |
| 0 | 图集纹理 | texture_2d<f32> | Fragment（compute 管线另定）|
| 1 | 图集采样器 | sampler | Fragment |
| 2 | 相机 uniform（64B）| uniform | Vertex |
| 3 | 雾 uniform（32B）| uniform | Vertex + Fragment |
| 4+ | 管线专属，必须在 §5 的条目表里显式声明 | — | — |

渲染管线的 `bindGroupLayout` **MUST** 由该表生成，禁止手写第二份。

## 5. 管线条目表（MUST）

每条管线在 C# 里有一个描述：`{ Name, ShaderFile, VertexEntry, FragmentEntry?, ComputeEntry?, DepthTest, DepthWrite, Blend, Bindings }`。
新增管线 MUST 先加描述，再加文件；两者缺一即视为未完成。

## 6. 启动期自检（MUST）

在创建管线之前：

1. 解析 WGSL 里所有 `@group(0) @binding(n)` 与入口点；
2. 与条目表比对：多一个、少一个、类型不符、入口缺失 ⇒ **立即抛出**，消息包含**管线名、文件名、差异明细**；
3. 失败 MUST NOT 静默回退（这是内置管线；只有未来外部着色器包才允许回退，见原则 11）。

## 7. 测试门禁（MUST）

| 测试 | 断言 |
| --- | --- |
| 无反内联 WGSL | 扫描 `src/Sharpcraft.Framework/Rendering/*.cs`，不得出现 `@vertex/@fragment/@compute` 之外的 WGSL 字符串常量 |
| include 组合器 | 嵌套/重复/缺失/循环四种情形 |
| 绑定一致性 | 每个管线：文件里的 binding 集合 == 条目表声明集合 |
| 入口存在 | 条目表声明的入口在文件里存在 |
| uniform 尺寸 | C# 侧常量字节数与 WGSL 结构体尺寸注释一致（64B 相机、32B 雾）|

## 8. 与原版的对应关系

| 原版 | 我们 |
| --- | --- |
| `core/<name>.vsh/.fsh` 一对 | `pipelines/<name>.wgsl` 单文件双入口（WGSL 允许同文件多入口）|
| `include/*.glsl` | `include/*.wgsl` |
| `post/` 效果链 + JSON | 后续 P3 再定义（ADR 与 `shader-packs.md` 已留接入点）|

差异原因：WGSL 无预处理、无独立 `.vsh/.fsh` 概念、入口点由字符串指定，因此"一文件双入口 + 自研 include"是等价且更少文件的组织。

## 9. 迁移顺序（本次改造）

G1 拆文件 → G2 include 组合器 → G3 绑定表 → G4 自检 → G5 条目表；每步跑全量测试，最后删除所有内联 WGSL 并用 §7 门禁防止回退。

## 10. 实施进度

| 步 | 状态 | 证据 |
| --- | --- | --- |
| 规范成文 | 完成 | 本文件 |
| G2 include 组合器 | **完成** | `ShaderIncludeComposer`：只展开一次、缺失报"管线+行+目标"、深度上限 4（4 项测试）|
| G3 绑定表 | **完成** | `ShaderBindings`（0/1/2/3 + 64B/32B 尺寸常量）|
| G4 启动自检 | **完成（核心）** | `ShaderDescriptor.Validate`：多/少 binding 双向报错、入口缺失（2 项测试）|
| G1 拆文件 | **完成**：terrain、sky、ui_panel、ui_sprite、ui_text 全部移入 `Shaders/pipelines/*.wgsl`（嵌入式资源 + `ShaderLibrary`）| 两次重构后帧比对均 `identical (max channel delta 0)` |
| G5 条目表 | 待做 | 待 G1 完成后与门禁测试一并加 |
| §7 门禁测试 | **完成（禁内联 + 管线文件存在）** | `ShaderSourceTests`(2)：`Rendering/*.cs` 不得含 `@vertex/@fragment/@compute`，且每个管线文件含入口 |
