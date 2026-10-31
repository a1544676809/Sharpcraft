<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

# Diagnostics（宿主层调试选项）

## 职责

把宿主层（启动器 / 工具）的调试开关解析为**显式值**，交给游戏程序集使用。

这是原则 13 的落地位置：**游戏程序集不读环境变量**；删掉本目录后，游戏照常构建、测试通过、行为不变（调试能力消失）。

## 内容

- `HostDebugOptions`：集中读取 `MC_LOGIN_NAME`、`MC_DUMP_CHUNK_DIR`、`MC_RESOURCE_PACK_WATCH`、
  `MC_TEXT_BACKGROUND_OPACITY`、`MC_MESH_FIFO` 等开关，并写入对应的宿主可设置属性。
- 游戏侧对应属性（`MinecraftLoginClient.LoginNameOverride`、`PlaySession.ChunkDumpDirectory`、
  `PackWatcher.IsEnabled`、`ChatLog.BackgroundOpacity`、`ChunkWorkerPool(..., fifoMeshOrder:)`）默认值一律等于"调试关闭"。

- `SpeedOverlay`：开关 `MC_SPEED_OVERLAY=1` 时在画面左下角实时显示水平速度（格/秒），用于与原版 F3 速度直接对照。

## 约束

- 新增调试开关 MUST 只在本目录解析；MUST NOT 下沉到 `Common` / `Client` / `Framework`。
- 自动化检查：`tools/verify/check-diagnostics-separation.ps1`（游戏程序集环境变量读取数 MUST 为 0）。
