# Entity model sandbox

独立 WGPU 离屏测试项目：用 `EntityModelMesher` + 默认玩家 skin 直接渲染 PNG，不启动游戏窗口，用于先验证模型/UV，再写回游戏路径。

运行：

```powershell
dotnet run --project tools/Sharpcraft.EntityModelSandbox -c Release
```

输出：`%TEMP%\entity-model-sandbox.png`

相机当前在 `-Z` 看玩家正面；资源包路径默认 `C:\Users\14947\AppData\Local\MinecraftCSharp\packs\minecraft-26.3-*.pack.zip`，可用 `MC_ASSETS_PACK` 覆盖。该工具只读资源、不写仓库资产。


多视角模式（当前）会输出 `front/back/left/right/shoulders` 五张 PNG，用于定位面朝向问题。
