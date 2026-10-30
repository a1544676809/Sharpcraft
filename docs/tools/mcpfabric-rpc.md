# mcpfabric 远程接口（用于对照验证）

`mcpfabric` 是装在**原版客户端**上的 Fabric 模组，在 `127.0.0.1:25599` 上暴露一个 JSON-RPC 接口。
它让"把原版和我们的客户端放在同一条件下比对"变成可自动化的事：可以直接让原版发消息、跑命令、取原版
**帧缓冲截图**（不含窗口边框、不受遮挡影响），比抓窗口可靠得多。

## 连接

| 项 | 值 |
| --- | --- |
| 地址 | `POST http://127.0.0.1:25599/rpc` |
| 认证 | `Authorization: Bearer <token>`（`requireAuth` 为 true 时必需）|
| 协议 | **自定义 JSON-RPC**，不是标准 MCP；方法名是点号风格 |
| 配置 | `<游戏目录>/config/mcpfabric.config.json`（含 token；**不得提交**）|

请求体：`{"jsonrpc":"2.0","id":1,"method":"<方法>","params":{...}}`。
响应：成功 `{"ok":true,"result":{...}}`；失败 `{"ok":false,"error":{"code":"...","message":"..."}}`。
未知方法返回 `unknown_method`，参数缺失返回 `bad_request`（据此可以试探方法名）。

## 实测可用的方法

客户端侧（原版客户端在跑时）：

| 方法 | 用途 |
| --- | --- |
| `chat.send` | 以原版玩家身份发一条消息（`message` 参数）|
| `command.run` | 执行命令 |
| `screen.get` | 当前界面（`inWorld`、`screen`、`paused` 等）|
| `vision.screenshot` | **原版帧缓冲 PNG（base64）**，实测 854×480 |
| `ui.widgets` / `ui.click` / `ui.set` | 界面控件操作（需 `enableUiControl=true`，默认关闭）|
| `world.get` / `world.set` / `world.fill` / `world.find` / `world.raycast` | 世界读写（需 `enableWorldWrite`）|
| `inventory.select` / `inventory.swap` / `inventory.drop` | 背包操作 |
| `player.get`、`chat.get`、`vision.describe` | **实测不存在**（服务端侧才有）|

## 对照验证的用法

1. `chat.send` 让原版发出**指定字符串**；
2. `vision.screenshot` 取原版帧缓冲；
3. 截我们自己的窗口（`PrintWindow`，避免遮挡）或走离屏截图路径；
4. 按 **canvas px**（各自 GUI 缩放换算）比较位置、尺寸、颜色。

已用它验证过：两边同一串文字的行高都是 21 设备px（= 10.5 canvas）、笔画都是 2 px，
即字形尺寸一致；观感差异来自对比度而非字形。

## 注意

- 该接口能改世界与背包，**只在需要对照时开启**，用完关掉客户端；
- token 只从配置文件读取，**不得写进仓库、日志或提交信息**。
