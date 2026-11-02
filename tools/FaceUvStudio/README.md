# Face UV Studio

浏览器端玩家 skin 面 UV 诊断页，用于确定 wide 玩家每个面的正确朝向。

打开方式：直接用浏览器打开 `tools/FaceUvStudio/index.html`，选择 `my_skin.png` 或任意 64×64 玩家皮肤。

功能：

- 按部件（head/body/left_arm/right_arm/left_leg/right_leg）查看六个面；
- 每个面单独设置 `0/90/180/270` 旋转、水平翻转、垂直翻转；
- 导出所有部件的每面朝向 JSON，作为 `EntityModelMesher` 的 UV 角点映射输入；
- 页面只读取用户本地图片，不包含任何 Minecraft 资产。

判定规则：看向 Steve 正面时，所有侧面箭头向上，红色在左、蓝色在右；顶/底视角同样。


界面包含：逐面清晰裁切 + 可拖动旋转的 3D 立方体预览；每面可旋转/翻 H/翻 V，底部导出 JSON。
