<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

状态：[已评审]
基线：Minecraft 26.3
证据：`docs/modules/ui.md`（设计）；`docs/specs/quality-attributes.md` QA-1.2
最后更新：2026-11-20

# Client/Ui —— 声明式界面的内核实现

把声明式文档变成一个可绘制的 2D 批次。这一层不认识世界，也不发绘制调用，只产出几何与区间。

## 1. 职责范围

负责：解析声明式子集、元素树、单趟布局、把控件树变成 `UiGeometry` 的四边形与 scissor 区间、
读入并缓存每个元素的数值、锚点、方向、阴影与绑定键。

不负责：渲染管线的状态切换（`Framework/Rendering`）、输入路由的采集（`Client/Input`）、
富文本的排版策略（随字体与聊天一起做）。

## 2. 关键类型

| 类型 | 职责 |
| --- | --- |
| `UiNode` | 解析后的元素（名字、属性、子节点），不可变 |
| `UiMarkup` | 解析器；**拒绝**未知元素与未知属性，并解码标准 XML 实体 |
| `UiStyles` | 文档加载时一次性读好数值、颜色、锚点、方向、阴影与 `{Binding}` 键，供每帧使用 |
| `UiStyle` | 单个元素的已解析值（颜色、宽高、内边距、间距、外边距、锚点、方向、阴影、绑定键） |
| `UiScreenLayout` | 单趟布局：`Screen` → `StackPanel` / `TextBlock` / `Spacer`（`Build` 标有 `[HotPath]`） |
| `UiColours` | `#RRGGBB` / `#AARRGGBB` → 打包颜色字 |

## 3. 不变量

- **未知即拒绝**：解析阶段遇到不认识的元素/属性直接抛 `FormatException`，指出名字与偏移；
  `Text` 含 `{` 或 `}` 但非合法整串 `{Binding Key}` 时在 `UiStyles.Compile` 期抛 `FormatException`。
  静默忽略一个属性或把原始花括号画到屏幕上，事后排查的代价远大于当场拒绝。
- **每帧零分配**：数值、颜色与绑定键在 `UiStyles` 里读一次；`UiScreenLayout.Build` 标有 `[HotPath]`，
  布局只做算术，几何缓冲复用。两条均由测试守护（64 帧重建分配为 0）。
- **控件只追加数据**：单个函数把整棵树写成几何，期间不切换任何渲染状态。
- **单趟布局**：尺寸来自文档或元素种类，锚点与横向面板在单趟测量中就地计算，避免通用约束求解。

## 4. 已实现的行为（P0 内核进行中）

| 项 | 状态 |
| --- | --- |
| 声明式子集：`Screen` / `StackPanel` / `TextBlock` / `Spacer`、XML 实体解码（`&lt;` `&gt;` `&quot;` `&apos;` `&amp;`）、`Screen` 的 `xmlns` / `xmlns:*` / `x:Class` / `Id` | 已实现 |
| 布局：背景填充、垂直/水平堆叠（`Orientation`）、兄弟间距、9 点锚定（`Anchor` + `Margin`）、显式 `Width`/`Height`、`Spacer`、行内居中 | 已实现 |
| 文本：`TextBlock` 支持 `Shadow` 开关、整串 `{Binding Key}` 与 `resolveBinding` 回调（`null` 回退空串） | 已实现 |
| 文本占位 | 无字体时按字符画 5×7 占位格，步进 7；有字体时按 `BitmapFont` 字形绘制 |
| 热加载 | `UiDocumentLoader` 已接入（后台解析 + 原子交换） |

### 4.1 声明式属性表

| 元素 | 允许属性 | 说明 |
| --- | --- | --- |
| `Screen` | `Background`、`Padding`、`Spacing`、`Id`、`x:Class`、`xmlns` / `xmlns:*` | 根元素；`xmlns*` 仅校验接受不写入属性表 |
| `StackPanel` | `Background`、`Padding`、`Spacing`、`Orientation`（`Vertical`/`Horizontal`）、`Anchor`、`Margin`、`Width`、`Height`、`Id` | 容器；默认垂直堆叠 |
| `TextBlock` | `Text`、`Colour`、`Height`、`Anchor`、`Margin`、`Shadow`（`true`/`false`）、`Id` | 文本；`Shadow` 默认 `true` |
| `Spacer` | `Width`、`Height`、`Background`、`Anchor`、`Margin`、`Id` | 占位/分割矩形；`Width` 与 `Height` 均 `> 0` 且有 `Background` 时绘制矩形 |

### 4.2 九点锚点对照表

| 常量（`UiScreenLayout`） | 值 | XAML `Anchor` 值 | 定位基准（相对于 `Screen` 内边距区域，扣除 `Margin`） |
| --- | --- | --- | --- |
| `AnchorNone` | `0` | （省略） | 沿流式垂直游标依次排列 |
| `AnchorTopLeft` | `1` | `TopLeft` | 左上角 `(areaX + margin, areaY + margin)` |
| `AnchorTopCenter` | `2` | `TopCenter` | 顶边水平居中 `(areaX + (areaW - elemW) * 0.5, areaY + margin)` |
| `AnchorTopRight` | `3` | `TopRight` | 右上角 `(areaX + areaW - elemW - margin, areaY + margin)` |
| `AnchorCenterLeft` | `4` | `CenterLeft` | 左边垂直居中 |
| `AnchorCenter` | `5` | `Center` | 屏幕正中 |
| `AnchorCenterRight` | `6` | `CenterRight` | 右边垂直居中 |
| `AnchorBottomLeft` | `7` | `BottomLeft` | 左下角 |
| `AnchorBottomCenter` | `8` | `BottomCenter` | 底边水平居中（快捷栏/状态栏位置） |
| `AnchorBottomRight` | `9` | `BottomRight` | 右下角 |

### 4.3 `{Binding}` 与 `resolveBinding` 契约

- **仅支持整串绑定**：`Text="{Binding Health}"` 在 `UiStyles.Compile` 时预提取键名 `"Health"`；不支持混排（如 `Text="HP: {Binding Health}"`）。若 `Text` 含 `{` 或 `}` 但不满足整串 `{Binding <非空键>}`，编译期直接抛出 `FormatException`。
- **回退语义**：`UiScreenLayout.Build(..., Func<string, string?>? resolveBinding = null)` 在每帧渲染时通过委托读取绑定值；若未传入 `resolveBinding` 或委托返回 `null`，则回退为 `string.Empty`，不会把原始 `{Binding ...}` 文本画到屏幕上。
- **零分配要求**：传入的 `resolveBinding` 应使用静态委托或预分配缓存字符串，避免逐帧构造新字符串。

## 4.4 位图字体度量

| 类型 | 职责 |
| --- | --- |
| `BitmapFont` | 从一张字形表（RGBA）推导每个字符的槽位、**墨迹宽度**与步进、基线高度 |

规则（与原版一致）：

- 字形表是等大方格网格，槽位顺序即码位（16 列，逐行）
- 字形宽度 = **最右的非透明列 +1**，不是整格宽：`.` 的步进必须远小于 `m`
- 步进 = `round(宽度 × scale) + 1`，最后的 `+1` 是字间空隙；`scale = 行高 / 格高`

本类只吃像素，不碰 GPU、不读资产：真实字形表由启动器运行时从 jar 提取后解码传入，
因此单元测试用**合成**字形表即可覆盖规则，仓库内不出现任何资产。

## 4.5 文字渲染（已接通）

`TextBlock` 在有字体时按 **每个可见字符一个带纹理四边形** 输出，顶点颜色同时作为墨色（字形表的覆盖率放在 alpha）。
无字体时退回占位竖条，因此内核在没有资产的环境里依然可测。

批次按**材质**分段：纯色（面板）与文字（字形）各成区间，渲染侧只在材质变化时切换管线与绑定组，
所以"一个面板 + 一行字"是两次绑定，整屏仍远低于预算的 3 次提交。

## 5. 与渲染的边界

本层产出 `UiGeometry`（20 字节顶点、scissor 区间）。渲染侧只为每个区间绑定一次 scissor 并下发一次绘制，
因此**绘制次数由面板数决定，与控件/字符数量无关**。

## 6. 测试

- `UiMarkupTests`（9 项）：默认文档的树形、注释与自闭合、三类拒绝（未知元素/未知属性/根元素不是 `Screen`）、
  颜色两种写法、背景 + 每可见字符一格、堆叠间距与内边距落位、以及 64 帧零分配。
- `UiLayoutExpansionTests`：XML 实体与命名空间头、`BottomCenter` + 横向 `StackPanel` + `Spacer`、显式 `Width`/`Height`/`Margin`、`Shadow` 开关、畸形/混排绑定拒绝、`null` 解析回退与 `{Binding}` 64 帧零分配。
- `UiGeometryTests`（7 项）守护几何与批处理本身。

<!-- BEGIN GENERATED: class-map -->

| Project | Sharpcraft type | Kind | Status | Reference class | Reference path | Note |
| --- | --- | --- | --- | --- | --- | --- |
| Sharpcraft.Client | Sharpcraft.Client.Ui.ChatLog | Mirror | Partial | net.minecraft.client.multiplayer.chat.ChatLog | ref://mc-26.3-analysis/src/net/minecraft/client/multiplayer/chat/ChatLog.java | auto-resolved by name; verify |
| Sharpcraft.Client | Sharpcraft.Client.Ui.GuiSpriteScaling | Mirror | Partial | net.minecraft.client.resources.metadata.gui.GuiSpriteScaling | ref://mc-26.3-analysis/src/net/minecraft/client/resources/metadata/gui/GuiSpriteScaling.java | auto-resolved by name; verify |
| Sharpcraft.Client | Sharpcraft.Client.Ui.Tooltip | Mirror | Partial | net.minecraft.client.gui.components.Tooltip | ref://mc-26.3-analysis/src/net/minecraft/client/gui/components/Tooltip.java | auto-resolved by name; verify |

<!-- END GENERATED: class-map -->
