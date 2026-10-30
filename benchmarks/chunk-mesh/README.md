# 区块网格构建基准（chunk-mesh benchmark）

目标：在**排除服务端传输变量**后，测量"区块到达 → 网格构建 → 上传"的耗时。

## 前置（代码侧，待实现）

| 开关 | 含义 |
| --- | --- |
| `MC_DEV_BENCH=1` | 基准模式总闸；未设置时下列开关**无效**，并在启动时打印 `benchmark mode: not for normal play` |
| `MC_BENCH_BUFFER=<秒>` | 连接后只收不建：缓存服务端区块，**达到期望区块数或超时（先到者）**后一次性装入并开始计时 |

> 该路径**不得进入发布形态**：由 `MC_DEV_BENCH` 把关，默认关闭；相关说明登记在 `docs/specs/quality-attributes.md`。

## 控制变量

- 所有 bot **同一位置、同一朝向**：用原版客户端的 mcpfabric RPC 逐个传送，例如 `/tp <bot> -83 120 46`。
- 传送完成后再让 bot 触发区块加载（传送本身即会引起新区块下发）。
- 每个 bot 日志里会打印 `bench: player at (x,y,z), chunks N`，用它核对位置与区块数一致。

## 运行

```powershell
pwsh -File benchmarks/chunk-mesh/run.ps1 -Bots 4 -BufferSeconds 5 -Teleport "-83 120 46"
```

脚本会：
1. 以 `MC_DEV_BENCH=1 MC_BENCH_BUFFER=<秒>` 启动 N 个客户端实例（不同 `MC_LOGIN_NAME`）；
2. 通过 mcpfabric RPC 依次 `/tp` 到同一坐标（token 从本地配置读取，**不打印、不提交**）；
3. 收集各自日志中的 `bench:` 行，汇总成表格。

## 当前状态

**尚未实现** `MC_DEV_BENCH` / `MC_BENCH_BUFFER`（需要改造窗口路径的区块接收：先缓存、后装入、再计时）。
本目录先固化流程与判据，避免用"含服务端传输"的数字当结论。
