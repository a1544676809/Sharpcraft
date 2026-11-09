<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

# 截图（F2）行为规格

> 状态：`[已评审]`（依据 26.3 分析工作区实测读取）
> 关联任务：T-780
> 上游文档：`docs/specs/render/pipeline.md`、`docs/decisions/0003-wgpu-binding.md`（截图走离屏回读）

## 1. 原版源码映射表

| 行为 | 原版位置 | 关键事实 |
| --- | --- | --- |
| 键位绑定 | `net.minecraft.client.Options#keyScreenshot` | `new KeyMapping("key.screenshot", 59, Category.MISC)`；`com.mojang.blaze3d.platform.InputConstants.KEY_F2 = 59` ⇒ 默认 **F2** |
| 触发点 | `net.minecraft.client.Minecraft#handleKeybinds` | `options.keyScreenshot.matches(key)` → `Screenshot.grab(minecraft, controlDown)`；Ctrl 按下时走全景图变体 |
| 入口 | `net.minecraft.client.Screenshot#grab(Minecraft, boolean)` | 取 `gameRenderer.mainRenderTarget()`，回调里 `showDebugChat` 输出结果消息 |
| 抓取 | `Screenshot#takeScreenshot(RenderTarget, int downscaleFactor, Consumer<NativeImage>)` | 主目标颜色纹理 `copyTextureToBuffer` → map；逐像素读取 |
| 像素约定 | 同上 | 行序**垂直翻转**（目标第 y 行写入图像第 `height - y - 1` 行）；alpha 强制 `0xFF` |
| 降采样 | 同上 | `downscaleFactor > 1` 时按块平均 RGB；宽高不能被整除则抛 `IllegalArgumentException` |
| 目录 | `Screenshot#SCREENSHOT_DIR` | `<gameDir>/screenshots`，不存在则创建。**`gameDir` 来自客户端 `--gameDir`**（原版同参数），未提供时用 `<roaming appdata>/.sharpcraft`（对应原版 `%APPDATA%\.minecraft` 的位置），**不是** `%LOCALAPPDATA%` |
| 文件名 | `Screenshot#getFile(File)` | `Util.getFilenameFormattedDateTime()`；已存在则追加 `_2`、`_3`…；扩展名 `.png` |
| 写盘 | `Screenshot#grab(...)` | 在 IO 线程池执行；`NativeImage.writeToFile` |
| 成功提示 | 同上 | 聊天消息 **`screenshot.success`（走语言表 `LanguageTable.Format`）**，参数为文件名，样式带下划线 + 点击打开文件事件 |
| 失败提示 | 同上 | **`screenshot.failure`（同样走语言表）**，参数为异常消息；同时 `LOGGER.warn("Couldn't save screenshot")` |
| 全景图 | `Screenshot#grab(File, "panorama_N.png", target, 4, …)` | 由 `grabPanoramixScreenshot` 走 6 次旋转；**本仓暂不实现**（登记为简化） |

## 2. 本仓设计

### 2.1 为什么必须"先渲染到中间纹理"

交换链纹理不保证可 `COPY_SRC`（见 ADR-0003），直接拷贝会失败。因此窗口路径改为：

```
所有世界/界面 pass → 中间颜色纹理（RenderAttachment | TextureBinding | CopySrc）
                  → 单三角形 blit → 交换链纹理
```

这同时满足原则 14 的"窗口呈现必须先绘制到与离屏同源的目标纹理再上屏"，并让截图与上屏**逐像素一致**（含 HUD）。

### 2.2 抓取流程（对应原版 takeScreenshot）

1. F2 按下 → 客户端设置"本帧结束后抓图"请求，携带目标路径
2. 渲染器在本帧记录完成后：`copyTextureToBuffer(中间纹理 → 回读缓冲)`
3. 提交 → map → 按原版约定**垂直翻转**、alpha 置 `0xFF`
4. `PngEncoder.EncodeRgba` 写盘（在后台线程，避免阻塞帧）
5. 成功后向聊天栏投递 `screenshot.success`（文件名 + 点击打开）；失败投递 `screenshot.failure`

### 2.3 已知刻意的简化

| 编号 | 简化内容 | 影响 | 状态 |
| --- | --- | --- | --- |
| SIM-SCREENSHOT-PANORAMA | Ctrl+F2 全景图（6 视角 + 4× 降采样）未实现 | 仅影响全景图功能，普通 F2 不受影响 | 待实现（T-780 后续切片） |
| SIM-SCREENSHOT-THREAD | 写盘在渲染线程内完成，不做原版的 IO 池排队 | 单次写盘约数十毫秒，仅在按 F2 时发生 | 保持（复查条件：出现连拍卡顿报告） |
| SIM-SCREENSHOT-NOFLIP | **不做原版的垂直翻转** | 无（图像方向正确）：原版翻转是因为 GL 回读是自底向上，而 wgpu 回读本身自顶向下 | 解决（实机截图验证正向、含 HUD） |
| — | **红蓝通道交换**（非简化，属格式适配） | 交换链为 `BGRA8Unorm`，回读字节序是 B,G,R,A，写出 PNG 前需交换 R/B；原版目标为 RGBA，无此步骤 | 解决（实机截图蓝天/绿草颜色正确） |

## 3. 验收

| 项 | 证据 |
| --- | --- |
| 文件落在 `screenshots/`，命名 `yyyy-MM-dd_HH.mm.ss.png`，重名追加 `_2` | `ScreenshotPathTests` |
| 垂直翻转正确（画面不倒置） | 实机 F2 截图与窗口画面逐像素对比 |
| 含 HUD | 同上（截图内可见聊天/物品栏） |
| 聊天提示出现 | 实机截图（聊天栏显示 `Saved screenshot as <name>`） |
