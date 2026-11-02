# Entity model sandbox

独立 WGPU 测试项目：用 `EntityModelMesher` + 默认玩家 skin 渲染玩家模型，用于先验证模型/UV，再写回游戏路径。

窗口模式（推荐，单进程，无需网页）：

```powershell
dotnet run --project tools/Sharpcraft.EntityModelSandbox -c Release -- --window
```

窗口内：`PageUp/PageDown` 切换部件，`Left/Right` 切换面，`Space` 旋转 90°，`LeftShift` 翻 H，`LeftCtrl` 翻 V；按住鼠标左键拖动旋转视角。当前状态显示在窗口标题。

离线多视角模式会输出 `front/back/left/right/shoulders` 五张 PNG 到 `%TEMP%`：

```powershell
dotnet run --project tools/Sharpcraft.EntityModelSandbox -c Release
```

资源包路径通过 `MC_ASSETS_PACK` 覆盖；自定义皮肤通过 `MC_SANDBOX_SKIN` 指定。两者都不设置时使用本地启动器默认路径。该工具只读资源、不写仓库资产。


`template.png` 这类 1600×1600（25× 放大）的 UV 模板会被识别为逻辑 64×64，可用 `MC_SANDBOX_SKIN` 指向它启动。


## 模型与纹理

沙盒使用游戏同一条资源路径：`MC_ASSETS_PACK` → `ResourceManager` → `EntitySkinLibrary`。

- `MC_SANDBOX_MODEL=player|zombie|skeleton` 选择注册模型；
- 纹理由 `EntityModelDefinition.TexturePath` 自动解析；
- 只有需要临时覆盖玩家皮肤时才设置 `MC_SANDBOX_SKIN`。

```powershell
$env:MC_ASSETS_PACK="C:\path\to\minecraft-26.3.pack.zip"
$env:MC_SANDBOX_MODEL="skeleton"
dotnet run --project tools/Sharpcraft.EntityModelSandbox -c Release -- --window
```
