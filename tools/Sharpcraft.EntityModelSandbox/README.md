# Entity model sandbox

独立 WGPU 测试项目：用 `EntityModelMesher` + 默认玩家 skin 渲染玩家模型，用于先验证模型/UV，再写回游戏路径。

窗口模式（推荐）：

```powershell
dotnet run --project tools/Sharpcraft.EntityModelSandbox -c Release -- --window
```

窗口内按住鼠标左键拖动可旋转视角。

离线多视角模式会输出 `front/back/left/right/shoulders` 五张 PNG 到 `%TEMP%`：

```powershell
dotnet run --project tools/Sharpcraft.EntityModelSandbox -c Release
```

资源包路径通过 `MC_ASSETS_PACK` 覆盖；自定义皮肤通过 `MC_SANDBOX_SKIN` 指定。两者都不设置时使用本地启动器默认路径。该工具只读资源、不写仓库资产。
