# 区块网格构建基准（chunk-mesh benchmark）

目标：在**排除服务端传输变量**后，测量"区块到达 → 网格构建 → 上传"的耗时。

## 前置（代码侧，待实现）

| 开关 | 含义 |
| --- | --- |
| `MC_MESH_FIFO=1` | 网格构建退回先进先出顺序，作为「距离优先」的对照 |

> 基准相关开关默认关闭；任何会改变正常加载/渲染行为的临时挂钩都不得留在树里（历史上的一条已于 2026-10-30 移除，见下）。

## 控制变量

- 所有 bot **同一位置、同一朝向**：用原版客户端的 mcpfabric RPC 逐个传送，例如 `/tp <bot> -83 120 46`。
- 传送完成后再让 bot 触发区块加载（传送本身即会引起新区块下发）。
- 每个 bot 日志里会打印 `bench: player at (x,y,z), chunks N`，用它核对位置与区块数一致。

## 运行

```powershell
pwsh -File benchmarks/chunk-mesh/run.ps1 -Bots 4 -BufferSeconds 5 -Teleport "-83 120 46"
```

脚本会：
1. 以 `MC_MESH_FIFO=1`（对照）或不设置（距离优先）启动 N 个客户端实例（不同 `MC_LOGIN_NAME`）；
2. 通过 mcpfabric RPC 依次 `/tp` 到同一坐标（token 从本地配置读取，**不打印、不提交**）；
3. 收集各自日志中的 `bench:` 行，汇总成表格。

## 当前状态

**区块缓存挂钩已移除**（2026-10-30）：早期实现过「先缓存服务端区块、收到 arm 文件指令再一次性装入」的 dev 挂钩
（`ChunkBufferHarness`，`MC_DEV_BENCH` / `MC_BENCH_BUFFER` / `MC_BENCH_DELAY` / `MC_BENCH_ARM_FILE`），
它会让客户端在收到指令前不安装区块。现在计时直接以传送时刻为起点，不再有这条会改变加载行为的路径。
本目录先固化流程与判据，避免用"含服务端传输"的数字当结论。
