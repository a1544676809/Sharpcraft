<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

# Class-map 提案（人工核对，勿直接提交）

由 `tools/Sharpcraft.ClassMap -- --propose` 生成。仅含类名与 ref:// 指针，无原版源码。

| Sharpcraft type | 我们的文件 | 候选原版类 | 匹配路径 | 状态 |
| --- | --- | --- | --- | --- |
| Sharpcraft.Client.Assets.AnimationFrameSize | src/Sharpcraft.Client/Assets/AnimationFrameSize.cs | AnimationFrameSize |  | 未匹配 |
| Sharpcraft.Client.Assets.AssetPackEntry | src/Sharpcraft.Client/Assets/AssetPackManifest.cs | AssetPackEntry |  | 未匹配 |
| Sharpcraft.Client.Assets.AssetPackManifest | src/Sharpcraft.Client/Assets/AssetPackManifest.cs | AssetPackManifest |  | 未匹配 |
| Sharpcraft.Client.Assets.BuiltInFallbackSource | src/Sharpcraft.Client/Assets/AssetSources.cs | BuiltInFallbackSource |  | 未匹配 |
| Sharpcraft.Client.Assets.DirectoryAssetSource | src/Sharpcraft.Client/Assets/AssetSources.cs | DirectoryAssetSource |  | 未匹配 |
| Sharpcraft.Client.Assets.OfficialObjectsSource | src/Sharpcraft.Client/Assets/AssetSources.cs | OfficialObjectsSource |  | 未匹配 |
| Sharpcraft.Client.Assets.ZipAssetSource | src/Sharpcraft.Client/Assets/AssetSources.cs | ZipAssetSource |  | 未匹配 |
| Sharpcraft.Client.Assets.AssetNotFoundException | src/Sharpcraft.Client/Assets/AssetStack.cs | AssetNotFoundException |  | 未匹配 |
| Sharpcraft.Client.Assets.AssetStack | src/Sharpcraft.Client/Assets/AssetStack.cs | AssetStack |  | 未匹配 |
| Sharpcraft.Client.Assets.IAssetSource | src/Sharpcraft.Client/Assets/AssetStack.cs | IAssetSource |  | 未匹配 |
| Sharpcraft.Client.Assets.IMetadataAssetSource | src/Sharpcraft.Client/Assets/AssetStack.cs | IMetadataAssetSource |  | 未匹配 |
| Sharpcraft.Client.Assets.MissingResourcePolicy | src/Sharpcraft.Client/Assets/AssetStack.cs | MissingResourcePolicy |  | 未匹配 |
| Sharpcraft.Client.Assets.AtlasMipChain | src/Sharpcraft.Client/Assets/AtlasMipChain.cs | AtlasMipChain |  | 未匹配 |
| Sharpcraft.Client.Assets.BitmapFontDefinition | src/Sharpcraft.Client/Assets/BitmapFontDefinition.cs | BitmapFontDefinition |  | 未匹配 |
| Sharpcraft.Client.Assets.BitmapFontProvider | src/Sharpcraft.Client/Assets/BitmapFontDefinition.cs | BitmapFont |  | 未匹配 |
| Sharpcraft.Client.Assets.BlockFaceTextures | src/Sharpcraft.Client/Assets/BlockModelResolver.cs | BlockFaceTextures |  | 未匹配 |
| Sharpcraft.Client.Assets.BlockModelResolver | src/Sharpcraft.Client/Assets/BlockModelResolver.cs | BlockModelResolver | ref://mc-26.3-analysis/src/net/minecraft/client/renderer/block/BlockModelResolver.java | resolved |
| Sharpcraft.Client.Assets.BlockFace | src/Sharpcraft.Client/Assets/BlockSpriteMap.cs | BlockFace |  | 未匹配 |
| Sharpcraft.Client.Assets.BlockSpriteMap | src/Sharpcraft.Client/Assets/BlockSpriteMap.cs | BlockSpriteMap |  | 未匹配 |
| Sharpcraft.Client.Assets.CelestialSprites | src/Sharpcraft.Client/Assets/CelestialSprites.cs | CelestialSprites |  | 未匹配 |
| Sharpcraft.Client.Assets.CloudTexture | src/Sharpcraft.Client/Assets/CloudTexture.cs | CloudTexture |  | 未匹配 |
| Sharpcraft.Client.Assets.Colormap | src/Sharpcraft.Client/Assets/Colormap.cs | Colormap |  | 未匹配 |
| Sharpcraft.Client.Assets.PackFormatVersion | src/Sharpcraft.Client/Assets/PackFormatVersion.cs | PackFormatVersion |  | 未匹配 |
| Sharpcraft.Client.Assets.PackWatcher | src/Sharpcraft.Client/Assets/PackWatcher.cs | PackWatcher |  | 未匹配 |
| Sharpcraft.Client.Assets.ResourceFilter | src/Sharpcraft.Client/Assets/ResourceFilter.cs | ResourceFilter |  | 未匹配 |
| Sharpcraft.Client.Assets.ResourceManager | src/Sharpcraft.Client/Assets/ResourceManager.cs | ResourceManager | ref://mc-26.3-analysis/src/net/minecraft/server/packs/resources/ResourceManager.java | resolved |
| Sharpcraft.Client.Assets.ResourcePack | src/Sharpcraft.Client/Assets/ResourcePack.cs | ResourcePack |  | 未匹配 |
| Sharpcraft.Client.Assets.ResourcePackKind | src/Sharpcraft.Client/Assets/ResourcePack.cs | ResourcePackKind |  | 未匹配 |
| Sharpcraft.Client.Assets.ResourcePackDiscovery | src/Sharpcraft.Client/Assets/ResourcePackDiscovery.cs | ResourcePackDiscovery |  | 未匹配 |
| Sharpcraft.Client.Assets.ResourcePackFormats | src/Sharpcraft.Client/Assets/ResourcePackFormats.cs | ResourcePackFormats |  | 未匹配 |
| Sharpcraft.Client.Assets.OverlayEntry | src/Sharpcraft.Client/Assets/ResourcePackOverlays.cs | OverlayEntry |  | 未匹配 |
| Sharpcraft.Client.Assets.ResourcePackOverlays | src/Sharpcraft.Client/Assets/ResourcePackOverlays.cs | ResourcePackOverlays |  | 未匹配 |
| Sharpcraft.Client.Assets.ResourcePackSource | src/Sharpcraft.Client/Assets/ResourcePackSource.cs | ResourcePackSource |  | 未匹配 |
| Sharpcraft.Client.Assets.ResourcePackStack | src/Sharpcraft.Client/Assets/ResourcePackStack.cs | ResourcePackStack |  | 未匹配 |
| Sharpcraft.Client.Assets.PackMetadata | src/Sharpcraft.Client/Assets/ResourcePath.cs | PackMetadata |  | 未匹配 |
| Sharpcraft.Client.Assets.ResourceKind | src/Sharpcraft.Client/Assets/ResourcePath.cs | ResourceKind |  | 未匹配 |
| Sharpcraft.Client.Assets.ResourceLookup | src/Sharpcraft.Client/Assets/ResourcePath.cs | ResourceLookup |  | 未匹配 |
| Sharpcraft.Client.Assets.ResourcePath | src/Sharpcraft.Client/Assets/ResourcePath.cs | ResourcePath |  | 未匹配 |
| Sharpcraft.Client.Assets.SpriteAlpha | src/Sharpcraft.Client/Assets/SpriteAlpha.cs | SpriteAlpha |  | 未匹配 |
| Sharpcraft.Client.Assets.SpriteAtlas | src/Sharpcraft.Client/Assets/SpriteAtlas.cs | SpriteAtlas |  | 未匹配 |
| Sharpcraft.Client.Assets.SpriteAtlasBuilder | src/Sharpcraft.Client/Assets/SpriteAtlas.cs | SpriteAtlasBuilder |  | 未匹配 |
| Sharpcraft.Client.Assets.SpriteRect | src/Sharpcraft.Client/Assets/SpriteAtlas.cs | SpriteRect |  | 未匹配 |
| Sharpcraft.Client.Assets.SpriteImageCache | src/Sharpcraft.Client/Assets/SpriteImageCache.cs | SpriteImageCache |  | 未匹配 |
| Sharpcraft.Client.Assets.Transparency | src/Sharpcraft.Client/Assets/Transparency.cs | Transparency | ref://mc-26.3-analysis/src/com/mojang/blaze3d/platform/Transparency.java | resolved |
| Sharpcraft.Client.Camera.CameraController | src/Sharpcraft.Client/Camera/CameraController.cs | CameraController |  | 未匹配 |
| Sharpcraft.Client.Camera.LookConvention | src/Sharpcraft.Client/Camera/LookConvention.cs | LookConvention |  | 未匹配 |
| Sharpcraft.Client.ClientArguments | src/Sharpcraft.Client/ClientArguments.cs | ClientArguments |  | 未匹配 |
| Sharpcraft.Client.Game.PlayerInterpolator | src/Sharpcraft.Client/Game/PlayerInterpolator.cs | PlayerInterpolator |  | 未匹配 |
| Sharpcraft.Client.Game.PreciseWait | src/Sharpcraft.Client/Game/PreciseWait.cs | PreciseWait |  | 未匹配 |
| Sharpcraft.Client.Input.FrameInput | src/Sharpcraft.Client/Input/InputMapper.cs | FrameInput |  | 未匹配 |
| Sharpcraft.Client.Input.InputAction | src/Sharpcraft.Client/Input/InputMapper.cs | InputAction |  | 未匹配 |
| Sharpcraft.Client.Input.InputMapper | src/Sharpcraft.Client/Input/InputMapper.cs | InputMapper |  | 未匹配 |
| Sharpcraft.Client.Input.KeyBindings | src/Sharpcraft.Client/Input/InputMapper.cs | KeyBindings |  | 未匹配 |
| Sharpcraft.Client.Render.AnimationClipPlayer | src/Sharpcraft.Client/Render/AnimationClipPlayer.cs | AnimationClipPlayer |  | 未匹配 |
| Sharpcraft.Client.Render.AxolotlAnimationProvider | src/Sharpcraft.Client/Render/AxolotlAnimationProvider.cs | AxolotlModel / AxolotlAnimation |  | 未匹配 |
| Sharpcraft.Client.Render.AxolotlAnimations | src/Sharpcraft.Client/Render/AxolotlAnimations.cs | AxolotlAnimation |  | 未匹配 |
| Sharpcraft.Client.Render.AxolotlModelDefinition | src/Sharpcraft.Client/Render/AxolotlModelDefinition.cs | AxolotlModel |  | 未匹配 |
| Sharpcraft.Client.Render.BabyModelDefinitions | src/Sharpcraft.Client/Render/BabyModelDefinitions.cs | BabyModelDefinitions |  | 未匹配 |
| Sharpcraft.Client.Render.BabyModelDefinitionsAquatic | src/Sharpcraft.Client/Render/BabyModelDefinitionsAquatic.cs | BabyModelDefinitionsAquatic |  | 未匹配 |
| Sharpcraft.Client.Render.BabyModelDefinitionsEquine | src/Sharpcraft.Client/Render/BabyModelDefinitionsEquine.cs | BabyModelDefinitionsEquine |  | 未匹配 |
| Sharpcraft.Client.Render.BabyModelDefinitionsExtra | src/Sharpcraft.Client/Render/BabyModelDefinitionsExtra.cs | BabyModelDefinitionsExtra |  | 未匹配 |
| Sharpcraft.Client.Render.BabyModelDefinitionsFinal | src/Sharpcraft.Client/Render/BabyModelDefinitionsFinal.cs | BabyModelDefinitionsFinal |  | 未匹配 |
| Sharpcraft.Client.Render.BabyModelDefinitionsHumanoid | src/Sharpcraft.Client/Render/BabyModelDefinitionsHumanoid.cs | BabyModelDefinitionsHumanoid |  | 未匹配 |
| Sharpcraft.Client.Render.BabyModelDefinitionsMisc | src/Sharpcraft.Client/Render/BabyModelDefinitionsMisc.cs | BabyModelDefinitionsMisc |  | 未匹配 |
| Sharpcraft.Client.Render.BabyModelDefinitionsNether | src/Sharpcraft.Client/Render/BabyModelDefinitionsNether.cs | BabyModelDefinitionsNether |  | 未匹配 |
| Sharpcraft.Client.Render.BabyModelDefinitionsOdd | src/Sharpcraft.Client/Render/BabyModelDefinitionsOdd.cs | BabyModelDefinitionsOdd |  | 未匹配 |
| Sharpcraft.Client.Render.BiomeTint | src/Sharpcraft.Client/Render/BiomeTint.cs | BiomeTint |  | 未匹配 |
| Sharpcraft.Client.Render.BlockTintKind | src/Sharpcraft.Client/Render/BiomeTint.cs | BlockTintKind |  | 未匹配 |
| Sharpcraft.Client.Render.BlockEntityPlaceholderMesher | src/Sharpcraft.Client/Render/BlockEntityPlaceholderMesher.cs | BlockEntityPlaceholderMesher |  | 未匹配 |
| Sharpcraft.Client.Render.BlockEntityRenderState | src/Sharpcraft.Client/Render/BlockEntityPlaceholderMesher.cs | BlockEntityRenderState | ref://mc-26.3-analysis/src/net/minecraft/client/renderer/blockentity/state/BlockEntityRenderState.java | resolved |
| Sharpcraft.Client.Render.BlockLayer | src/Sharpcraft.Client/Render/BlockLayerRules.cs | BlockLayer |  | 未匹配 |
| Sharpcraft.Client.Render.BlockLayerRules | src/Sharpcraft.Client/Render/BlockLayerRules.cs | BlockLayerRules |  | 未匹配 |
| Sharpcraft.Client.Render.BlockLightFlicker | src/Sharpcraft.Client/Render/BlockLightFlicker.cs | BlockLightFlicker |  | 未匹配 |
| Sharpcraft.Client.Render.BlockRenderLayers | src/Sharpcraft.Client/Render/BlockRenderLayers.cs | BlockRenderLayers |  | 未匹配 |
| Sharpcraft.Client.Render.BlockTintTable | src/Sharpcraft.Client/Render/BlockTintTable.cs | BlockTintTable |  | 未匹配 |
| Sharpcraft.Client.Render.CameraJitter | src/Sharpcraft.Client/Render/CameraJitter.cs | CameraJitter |  | 未匹配 |
| Sharpcraft.Client.Render.CatModelDefinition | src/Sharpcraft.Client/Render/CatModelDefinition.cs | CatModel |  | 未匹配 |
| Sharpcraft.Client.Render.CaveSpiderModelDefinition | src/Sharpcraft.Client/Render/CaveSpiderModelDefinition.cs | CaveSpiderModel |  | 未匹配 |
| Sharpcraft.Client.Render.CelestialGeometry | src/Sharpcraft.Client/Render/CelestialGeometry.cs | CelestialGeometry |  | 未匹配 |
| Sharpcraft.Client.Render.CelestialMeshBuilder | src/Sharpcraft.Client/Render/CelestialMeshBuilder.cs | CelestialMeshBuilder |  | 未匹配 |
| Sharpcraft.Client.Render.ChunkMeshCache | src/Sharpcraft.Client/Render/ChunkMeshCache.cs | ChunkMeshCache |  | 未匹配 |
| Sharpcraft.Client.Render.ChunkMesher | src/Sharpcraft.Client/Render/ChunkMesher.cs | ChunkMesher |  | 未匹配 |
| Sharpcraft.Client.Render.MeshData | src/Sharpcraft.Client/Render/ChunkMesher.cs | MeshData | ref://mc-26.3-analysis/src/com/mojang/blaze3d/vertex/MeshData.java | resolved |
| Sharpcraft.Client.Render.CloudDrawCache | src/Sharpcraft.Client/Render/CloudDrawCache.cs | CloudDrawCache |  | 未匹配 |
| Sharpcraft.Client.Render.CloudGeometryBuilder | src/Sharpcraft.Client/Render/CloudGeometryBuilder.cs | CloudGeometryBuilder |  | 未匹配 |
| Sharpcraft.Client.Render.CloudCameraSide | src/Sharpcraft.Client/Render/CloudMeshBuilder.cs | CloudCameraSide |  | 未匹配 |
| Sharpcraft.Client.Render.CloudFaceDirection | src/Sharpcraft.Client/Render/CloudMeshBuilder.cs | CloudFaceDirection |  | 未匹配 |
| Sharpcraft.Client.Render.CloudMeshBuilder | src/Sharpcraft.Client/Render/CloudMeshBuilder.cs | CloudMeshBuilder |  | 未匹配 |
| Sharpcraft.Client.Render.CloudQuad | src/Sharpcraft.Client/Render/CloudMeshBuilder.cs | CloudQuad |  | 未匹配 |
| Sharpcraft.Client.Render.CloudRangePolicy | src/Sharpcraft.Client/Render/CloudRangePolicy.cs | CloudRangePolicy |  | 未匹配 |
| Sharpcraft.Client.Render.State | src/Sharpcraft.Client/Render/EnderDragonAnimationProvider.cs | State |  | 未匹配 |
| Sharpcraft.Client.Render.AnimationInterpolation | src/Sharpcraft.Client/Render/EntityAnimation.cs | AnimationInterpolation |  | 未匹配 |
| Sharpcraft.Client.Render.AnimationKeyframe | src/Sharpcraft.Client/Render/EntityAnimation.cs | AnimationKeyframe |  | 未匹配 |
| Sharpcraft.Client.Render.AnimationState | src/Sharpcraft.Client/Render/EntityAnimation.cs | AnimationState | ref://mc-26.3-analysis/src/net/minecraft/world/entity/AnimationState.java | resolved |
| Sharpcraft.Client.Render.AnimationTarget | src/Sharpcraft.Client/Render/EntityAnimation.cs | AnimationTarget |  | 未匹配 |
| Sharpcraft.Client.Render.EntityPose | src/Sharpcraft.Client/Render/EntityAnimation.cs | EntityPose |  | 未匹配 |
| Sharpcraft.Client.Render.Entry | src/Sharpcraft.Client/Render/EntityAnimation.cs | Entry |  | 未匹配 |
| Sharpcraft.Client.Render.PoseOffset | src/Sharpcraft.Client/Render/EntityAnimation.cs | PoseOffset |  | 未匹配 |
| Sharpcraft.Client.Render.EntityAnimationRegistry | src/Sharpcraft.Client/Render/EntityAnimationProvider.cs | EntityAnimationRegistry |  | 未匹配 |
| Sharpcraft.Client.Render.IEntityAnimationProvider | src/Sharpcraft.Client/Render/EntityAnimationProvider.cs | IEntityModel / IEntityAnimation |  | 未匹配 |
| Sharpcraft.Client.Render.EntityModelLayerHelpers | src/Sharpcraft.Client/Render/EntityModelLayerHelpers.cs | EntityModelLayerHelpers |  | 未匹配 |
| Sharpcraft.Client.Render.EntityModelResolver | src/Sharpcraft.Client/Render/EntityModelResolver.cs | EntityModelResolver |  | 未匹配 |
| Sharpcraft.Client.Render.EntityPlaceholderMesher | src/Sharpcraft.Client/Render/EntityPlaceholderMesher.cs | EntityPlaceholderMesher |  | 未匹配 |
| Sharpcraft.Client.Render.EntitySkinLibrary | src/Sharpcraft.Client/Render/EntitySkinLibrary.cs | EntitySkinLibrary |  | 未匹配 |
| Sharpcraft.Client.Render.EntitySkinTexture | src/Sharpcraft.Client/Render/EntitySkinTexture.cs | EntitySkinTexture |  | 未匹配 |
| Sharpcraft.Client.Render.EntityVariantTable | src/Sharpcraft.Client/Render/EntityVariantTable.cs | EntityVariantTable |  | 未匹配 |
| Sharpcraft.Client.Render.EquineAnimationProvider | src/Sharpcraft.Client/Render/EquineAnimationProvider.cs | EquineModel / EquineAnimation |  | 未匹配 |
| Sharpcraft.Client.Render.EquipmentModelParts | src/Sharpcraft.Client/Render/EquipmentModelParts.cs | EquipmentModelParts |  | 未匹配 |
| Sharpcraft.Client.Render.EquipmentAssetKind | src/Sharpcraft.Client/Render/EquipmentTextureCatalog.cs | EquipmentAssetKind |  | 未匹配 |
| Sharpcraft.Client.Render.EquipmentTexture | src/Sharpcraft.Client/Render/EquipmentTextureCatalog.cs | EquipmentTexture |  | 未匹配 |
| Sharpcraft.Client.Render.EquipmentTextureCatalog | src/Sharpcraft.Client/Render/EquipmentTextureCatalog.cs | EquipmentTextureCatalog |  | 未匹配 |
| Sharpcraft.Client.Render.FaceUvTransform | src/Sharpcraft.Client/Render/FaceUvTransform.cs | FaceUvTransform |  | 未匹配 |
| Sharpcraft.Client.Render.FelineAnimationProvider | src/Sharpcraft.Client/Render/FelineAnimationProvider.cs | FelineModel / FelineAnimation |  | 未匹配 |
| Sharpcraft.Client.Render.FluidSurface | src/Sharpcraft.Client/Render/FluidSurface.cs | FluidSurface |  | 未匹配 |
| Sharpcraft.Client.Render.GiantModelDefinition | src/Sharpcraft.Client/Render/GiantModelDefinition.cs | GiantModel |  | 未匹配 |
| Sharpcraft.Client.Render.State | src/Sharpcraft.Client/Render/GuardianAnimationProvider.cs | State |  | 未匹配 |
| Sharpcraft.Client.Render.HumanoidMobModelDefinition | src/Sharpcraft.Client/Render/HumanoidMobModelDefinition.cs | HumanoidMobModel |  | 未匹配 |
| Sharpcraft.Client.Render.IllagerArmPose | src/Sharpcraft.Client/Render/IllagerArmPose.cs | IllagerArmPose |  | 未匹配 |
| Sharpcraft.Client.Render.LayerSinks | src/Sharpcraft.Client/Render/LayerSinks.cs | LayerSinks |  | 未匹配 |
| Sharpcraft.Client.Render.MeshBuffer | src/Sharpcraft.Client/Render/MeshBuffer.cs | MeshBuffer |  | 未匹配 |
| Sharpcraft.Client.Render.MeshBuildPolicy | src/Sharpcraft.Client/Render/MeshBuildPolicy.cs | MeshBuildPolicy |  | 未匹配 |
| Sharpcraft.Client.Render.MeshedChunk | src/Sharpcraft.Client/Render/MeshedChunk.cs | MeshedChunk |  | 未匹配 |
| Sharpcraft.Client.Render.NameplateProjection | src/Sharpcraft.Client/Render/NameplateProjection.cs | NameplateProjection |  | 未匹配 |
| Sharpcraft.Client.Render.OcclusionCulling | src/Sharpcraft.Client/Render/OcclusionCulling.cs | OcclusionCulling |  | 未匹配 |
| Sharpcraft.Client.Render.PiglinArmPose | src/Sharpcraft.Client/Render/PiglinAnimationProvider.cs | PiglinArmPose | ref://mc-26.3-analysis/src/net/minecraft/world/entity/monster/piglin/PiglinArmPose.java | resolved |
| Sharpcraft.Client.Render.EntityModelLayer | src/Sharpcraft.Client/Render/PlayerModelDefinition.cs | EntityModelLayer |  | 未匹配 |
| Sharpcraft.Client.Render.PufferfishAnimationProvider | src/Sharpcraft.Client/Render/PufferfishAnimationProvider.cs | PufferfishModel / PufferfishAnimation |  | 未匹配 |
| Sharpcraft.Client.Render.PufferfishModelDefinition | src/Sharpcraft.Client/Render/PufferfishModelDefinition.cs | PufferfishModel |  | 未匹配 |
| Sharpcraft.Client.Render.FrameHistory | src/Sharpcraft.Client/Render/Reprojection.cs | FrameHistory |  | 未匹配 |
| Sharpcraft.Client.Render.Reprojection | src/Sharpcraft.Client/Render/Reprojection.cs | Reprojection |  | 未匹配 |
| Sharpcraft.Client.Render.SkyVisibility | src/Sharpcraft.Client/Render/SkyVisibility.cs | SkyVisibility |  | 未匹配 |
| Sharpcraft.Client.Render.SmoothLighting | src/Sharpcraft.Client/Render/SmoothLighting.cs | SmoothLighting |  | 未匹配 |
| Sharpcraft.Client.Render.LegacyRandom | src/Sharpcraft.Client/Render/StarField.cs | LegacyRandom |  | 未匹配 |
| Sharpcraft.Client.Render.StarField | src/Sharpcraft.Client/Render/StarField.cs | StarField |  | 未匹配 |
| Sharpcraft.Client.Render.StateCullTable | src/Sharpcraft.Client/Render/StateCullTable.cs | StateCullTable |  | 未匹配 |
| Sharpcraft.Client.Render.QuadVisual | src/Sharpcraft.Client/Render/StateVisualCache.cs | QuadVisual |  | 未匹配 |
| Sharpcraft.Client.Render.StateVisualCache | src/Sharpcraft.Client/Render/StateVisualCache.cs | StateVisualCache |  | 未匹配 |
| Sharpcraft.Client.Render.TraderLlamaModelDefinition | src/Sharpcraft.Client/Render/TraderLlamaModelDefinition.cs | TraderLlamaModel |  | 未匹配 |
| Sharpcraft.Client.Render.TranslucentOrder | src/Sharpcraft.Client/Render/TranslucentOrder.cs | TranslucentOrder |  | 未匹配 |
| Sharpcraft.Client.Render.TropicalFishAnimationProvider | src/Sharpcraft.Client/Render/TropicalFishAnimationProvider.cs | TropicalFishModel / TropicalFishAnimation |  | 未匹配 |
| Sharpcraft.Client.Render.TropicalFishModelDefinition | src/Sharpcraft.Client/Render/TropicalFishModelDefinition.cs | TropicalFishModel |  | 未匹配 |
| Sharpcraft.Client.Render.LightmapParameters | src/Sharpcraft.Client/Render/VanillaLightmap.cs | LightmapParameters |  | 未匹配 |
| Sharpcraft.Client.Render.VanillaLightmap | src/Sharpcraft.Client/Render/VanillaLightmap.cs | VanillaLightmap |  | 未匹配 |
| Sharpcraft.Client.Render.ViewFrustum | src/Sharpcraft.Client/Render/ViewFrustum.cs | ViewFrustum |  | 未匹配 |
| Sharpcraft.Client.Render.WitherAnimationProvider | src/Sharpcraft.Client/Render/WitherAnimationProvider.cs | WitherModel / WitherAnimation |  | 未匹配 |
| Sharpcraft.Client.Render.WorldTextMeshBuilder | src/Sharpcraft.Client/Render/WorldTextMeshBuilder.cs | WorldTextMeshBuilder |  | 未匹配 |
| Sharpcraft.Client.Render.ZombieNautilusModelDefinition | src/Sharpcraft.Client/Render/ZombieNautilusModelDefinition.cs | ZombieNautilusModel |  | 未匹配 |
| Sharpcraft.Client.Ui.ActionBar | src/Sharpcraft.Client/Ui/ActionBar.cs | ActionBar |  | 未匹配 |
| Sharpcraft.Client.Ui.BitmapFont | src/Sharpcraft.Client/Ui/BitmapFont.cs | BitmapFont |  | 未匹配 |
| Sharpcraft.Client.Ui.Glyph | src/Sharpcraft.Client/Ui/BitmapFont.cs | Glyph |  | 未匹配 |
| Sharpcraft.Client.Ui.ChatHitTest | src/Sharpcraft.Client/Ui/ChatHitTest.cs | ChatHitTest |  | 未匹配 |
| Sharpcraft.Client.Ui.ChatInput | src/Sharpcraft.Client/Ui/ChatInput.cs | ChatInput |  | 未匹配 |
| Sharpcraft.Client.Ui.ChatLog | src/Sharpcraft.Client/Ui/ChatLog.cs | ChatLog | ref://mc-26.3-analysis/src/net/minecraft/client/multiplayer/chat/ChatLog.java | resolved |
| Sharpcraft.Client.Ui.MessageTag | src/Sharpcraft.Client/Ui/ChatLog.cs | MessageTag |  | 未匹配 |
| Sharpcraft.Client.Ui.ChatPrompt | src/Sharpcraft.Client/Ui/ChatPrompt.cs | ChatPrompt |  | 未匹配 |
| Sharpcraft.Client.Ui.FontDefinition | src/Sharpcraft.Client/Ui/FontDefinition.cs | FontDefinition |  | 未匹配 |
| Sharpcraft.Client.Ui.GuiSpriteAtlas | src/Sharpcraft.Client/Ui/GuiSpriteAtlas.cs | GuiSpriteAtlas |  | 未匹配 |
| Sharpcraft.Client.Ui.GuiSpriteBorder | src/Sharpcraft.Client/Ui/GuiSpriteScaling.cs | GuiSpriteBorder |  | 未匹配 |
| Sharpcraft.Client.Ui.GuiSpriteScaling | src/Sharpcraft.Client/Ui/GuiSpriteScaling.cs | GuiSpriteScaling | ref://mc-26.3-analysis/src/net/minecraft/client/resources/metadata/gui/GuiSpriteScaling.java | resolved |
| Sharpcraft.Client.Ui.GuiSpriteScalingType | src/Sharpcraft.Client/Ui/GuiSpriteScaling.cs | GuiSpriteScalingType |  | 未匹配 |
| Sharpcraft.Client.Ui.NineSlice | src/Sharpcraft.Client/Ui/NineSlice.cs | NineSlice |  | 未匹配 |
| Sharpcraft.Client.Ui.NineSliceCell | src/Sharpcraft.Client/Ui/NineSlice.cs | NineSliceCell |  | 未匹配 |
| Sharpcraft.Client.Ui.PlayerListPanel | src/Sharpcraft.Client/Ui/PlayerListPanel.cs | PlayerListPanel |  | 未匹配 |
| Sharpcraft.Client.Ui.PlayerListRow | src/Sharpcraft.Client/Ui/PlayerListPanel.cs | PlayerListRow |  | 未匹配 |
| Sharpcraft.Client.Ui.ScrollBar | src/Sharpcraft.Client/Ui/ScrollBar.cs | ScrollBar |  | 未匹配 |
| Sharpcraft.Client.Ui.ScrollState | src/Sharpcraft.Client/Ui/ScrollState.cs | ScrollState |  | 未匹配 |
| Sharpcraft.Client.Ui.TextEditModel | src/Sharpcraft.Client/Ui/TextEditModel.cs | TextEditModel |  | 未匹配 |
| Sharpcraft.Client.Ui.TextEditRenderer | src/Sharpcraft.Client/Ui/TextEditRenderer.cs | TextEditRenderer |  | 未匹配 |
| Sharpcraft.Client.Ui.Title | src/Sharpcraft.Client/Ui/Title.cs | Title |  | 未匹配 |
| Sharpcraft.Client.Ui.Tooltip | src/Sharpcraft.Client/Ui/Tooltip.cs | Tooltip | ref://mc-26.3-analysis/src/net/minecraft/client/gui/components/Tooltip.java | resolved |
| Sharpcraft.Client.Ui.UiDocumentFile | src/Sharpcraft.Client/Ui/UiDocumentFile.cs | UiDocumentFile |  | 未匹配 |
| Sharpcraft.Client.Ui.CompiledScreen | src/Sharpcraft.Client/Ui/UiDocumentLoader.cs | CompiledScreen |  | 未匹配 |
| Sharpcraft.Client.Ui.UiDocumentLoader | src/Sharpcraft.Client/Ui/UiDocumentLoader.cs | UiDocumentLoader |  | 未匹配 |
| Sharpcraft.Client.Ui.UiMarkup | src/Sharpcraft.Client/Ui/UiMarkup.cs | UiMarkup |  | 未匹配 |
| Sharpcraft.Client.Ui.UiNode | src/Sharpcraft.Client/Ui/UiMarkup.cs | UiNode |  | 未匹配 |
| Sharpcraft.Client.Ui.UiColours | src/Sharpcraft.Client/Ui/UiScreenLayout.cs | UiColours |  | 未匹配 |
| Sharpcraft.Client.Ui.UiScreenLayout | src/Sharpcraft.Client/Ui/UiScreenLayout.cs | UiScreenLayout |  | 未匹配 |
| Sharpcraft.Client.Ui.UiStyle | src/Sharpcraft.Client/Ui/UiScreenLayout.cs | UiStyle |  | 未匹配 |
| Sharpcraft.Client.Ui.UiStyles | src/Sharpcraft.Client/Ui/UiScreenLayout.cs | UiStyles |  | 未匹配 |
| Sharpcraft.Client.World.ChunkWorkerPool | src/Sharpcraft.Client/World/ChunkWorkerPool.cs | ChunkWorkerPool |  | 未匹配 |
| Sharpcraft.Client.World.ClientWorld | src/Sharpcraft.Client/World/ClientWorld.cs | ClientWorld |  | 未匹配 |
| Sharpcraft.Client.World.EntityAnimationTickContext | src/Sharpcraft.Client/World/EntityAnimationState.cs | EntityAnimationTickContext |  | 未匹配 |
| Sharpcraft.Client.World.IEntityAnimationState | src/Sharpcraft.Client/World/EntityAnimationState.cs | IEntityAnimationState |  | 未匹配 |
| Sharpcraft.Client.World.IEntityArmPoseAnimationState | src/Sharpcraft.Client/World/EntityAnimationState.cs | IEntityArmPoseAnimationState |  | 未匹配 |
| Sharpcraft.Client.World.IEntityEventAnimationState | src/Sharpcraft.Client/World/EntityAnimationState.cs | IEntityEventAnimationState |  | 未匹配 |
| Sharpcraft.Client.World.IEntityPoseAnimationState | src/Sharpcraft.Client/World/EntityAnimationState.cs | IEntityPoseAnimationState |  | 未匹配 |
| Sharpcraft.Client.World.IEntitySyncedDataAnimationState | src/Sharpcraft.Client/World/EntityAnimationState.cs | IEntitySyncedDataAnimationState |  | 未匹配 |
| Sharpcraft.Client.World.IEntityTickAnimationState | src/Sharpcraft.Client/World/EntityAnimationState.cs | IEntityTickAnimationState |  | 未匹配 |
| Sharpcraft.Client.World.EntityEquipment | src/Sharpcraft.Client/World/EntityEquipment.cs | EntityEquipment | ref://mc-26.3-analysis/src/net/minecraft/world/entity/EntityEquipment.java | resolved |
| Sharpcraft.Client.World.EntityRenderState | src/Sharpcraft.Client/World/EntityTracker.cs | EntityRenderState | ref://mc-26.3-analysis/src/net/minecraft/client/renderer/entity/state/EntityRenderState.java | resolved |
| Sharpcraft.Client.World.TrackedEntity | src/Sharpcraft.Client/World/EntityTracker.cs | TrackedEntity |  | 未匹配 |
| Sharpcraft.Client.World.EntityVariantSpec | src/Sharpcraft.Client/World/EntityVariantSpecs.cs | EntityVariantSpec |  | 未匹配 |
| Sharpcraft.Client.World.EntityVariantSpecs | src/Sharpcraft.Client/World/EntityVariantSpecs.cs | EntityVariantSpecs |  | 未匹配 |
| Sharpcraft.Client.World.EntityWorldAdapter | src/Sharpcraft.Client/World/EntityWorldAdapter.cs | EntityWorldAdapter |  | 未匹配 |
| Sharpcraft.Client.World.FlapAnimationState | src/Sharpcraft.Client/World/FlapAnimationState.cs | FlapAnimationState |  | 未匹配 |
| Sharpcraft.Client.World.PandaPoseAnimationState | src/Sharpcraft.Client/World/PandaPoseAnimationState.cs | PandaPoseAnimationState |  | 未匹配 |
| Sharpcraft.Client.World.PlayerListEntry | src/Sharpcraft.Client/World/PlayerListRoster.cs | PlayerListEntry |  | 未匹配 |
| Sharpcraft.Client.World.PlayerListRoster | src/Sharpcraft.Client/World/PlayerListRoster.cs | PlayerListRoster |  | 未匹配 |
| Sharpcraft.Client.World.PolarBearStandAnimationState | src/Sharpcraft.Client/World/PolarBearStandAnimationState.cs | PolarBearStandAnimationState |  | 未匹配 |
| Sharpcraft.Client.World.RabbitHopAnimationState | src/Sharpcraft.Client/World/RabbitHopAnimationState.cs | RabbitHopAnimationState |  | 未匹配 |
| Sharpcraft.Client.World.RabbitIdleAnimationState | src/Sharpcraft.Client/World/RabbitIdleAnimationState.cs | RabbitIdleAnimationState |  | 未匹配 |
| Sharpcraft.Client.World.SceneFingerprint | src/Sharpcraft.Client/World/SceneFingerprint.cs | SceneFingerprint |  | 未匹配 |
| Sharpcraft.Client.World.SceneRecorder | src/Sharpcraft.Client/World/SceneRecorder.cs | SceneRecorder |  | 未匹配 |
| Sharpcraft.Client.World.SquidTentacleAnimationState | src/Sharpcraft.Client/World/SquidTentacleAnimationState.cs | SquidTentacleAnimationState |  | 未匹配 |
| Sharpcraft.Client.World.TrackedAttributeMap | src/Sharpcraft.Client/World/TrackedAttributeMap.cs | TrackedAttributeMap |  | 未匹配 |
| Sharpcraft.Client.World.WalkAnimationState | src/Sharpcraft.Client/World/WalkAnimationState.cs | WalkAnimationState | ref://mc-26.3-analysis/src/net/minecraft/world/entity/WalkAnimationState.java | resolved |
| Sharpcraft.Client.World.WitherHeadAim | src/Sharpcraft.Client/World/WitherHeadAnimationState.cs | WitherHeadAim |  | 未匹配 |
| Sharpcraft.Client.World.WitherHeadAnimationState | src/Sharpcraft.Client/World/WitherHeadAnimationState.cs | WitherHeadAnimationState |  | 未匹配 |
| Sharpcraft.Common.Coordinates.BlockCoordinates | src/Sharpcraft.Common/Coordinates/BlockCoordinates.cs | BlockCoordinates |  | 未匹配 |
| Sharpcraft.Common.Diagnostics.HotPathAttribute | src/Sharpcraft.Common/Diagnostics/HotPathAttribute.cs | HotPathAttribute |  | 未匹配 |
| Sharpcraft.Common.Identifiers.Identifier | src/Sharpcraft.Common/Identifiers/Identifier.cs | Identifier | ref://mc-26.3-analysis/src/net/minecraft/resources/Identifier.java | resolved |
| Sharpcraft.Common.Identifiers.IdentifierException | src/Sharpcraft.Common/Identifiers/IdentifierException.cs | IdentifierException | ref://mc-26.3-analysis/src/net/minecraft/IdentifierException.java | resolved |
| Sharpcraft.Common.Imaging.PngDecoder | src/Sharpcraft.Common/Imaging/PngDecoder.cs | PngDecoder |  | 未匹配 |
| Sharpcraft.Common.Imaging.RgbaImage | src/Sharpcraft.Common/Imaging/PngDecoder.cs | RgbaImage |  | 未匹配 |
| Sharpcraft.Common.Imaging.Crc32 | src/Sharpcraft.Common/Imaging/PngEncoder.cs | Crc32 |  | 未匹配 |
| Sharpcraft.Common.Imaging.PngEncoder | src/Sharpcraft.Common/Imaging/PngEncoder.cs | PngEncoder |  | 未匹配 |
| Sharpcraft.Common.Lifecycle.OfflineIdentity | src/Sharpcraft.Common/Lifecycle/OfflineIdentity.cs | OfflineIdentity |  | 未匹配 |
| Sharpcraft.Common.Lifecycle.SessionFile | src/Sharpcraft.Common/Lifecycle/SessionFile.cs | SessionFile |  | 未匹配 |
| Sharpcraft.Common.Localization.LanguageTable | src/Sharpcraft.Common/Localization/LanguageTable.cs | LanguageTable |  | 未匹配 |
| Sharpcraft.Common.Localization.LocaleSelector | src/Sharpcraft.Common/Localization/LocaleSelector.cs | LocaleSelector |  | 未匹配 |
| Sharpcraft.Common.Nbt.NbtReader | src/Sharpcraft.Common/Nbt/NbtReader.cs | NbtReader |  | 未匹配 |
| Sharpcraft.Common.Nbt.NbtTagType | src/Sharpcraft.Common/Nbt/NbtTagType.cs | NbtTagType |  | 未匹配 |
| Sharpcraft.Common.Nbt.NbtByte | src/Sharpcraft.Common/Nbt/NbtTags.cs | NbtByte |  | 未匹配 |
| Sharpcraft.Common.Nbt.NbtByteArray | src/Sharpcraft.Common/Nbt/NbtTags.cs | NbtByteArray |  | 未匹配 |
| Sharpcraft.Common.Nbt.NbtCompound | src/Sharpcraft.Common/Nbt/NbtTags.cs | NbtCompound |  | 未匹配 |
| Sharpcraft.Common.Nbt.NbtDouble | src/Sharpcraft.Common/Nbt/NbtTags.cs | NbtDouble |  | 未匹配 |
| Sharpcraft.Common.Nbt.NbtFloat | src/Sharpcraft.Common/Nbt/NbtTags.cs | NbtFloat |  | 未匹配 |
| Sharpcraft.Common.Nbt.NbtInt | src/Sharpcraft.Common/Nbt/NbtTags.cs | NbtInt |  | 未匹配 |
| Sharpcraft.Common.Nbt.NbtIntArray | src/Sharpcraft.Common/Nbt/NbtTags.cs | NbtIntArray |  | 未匹配 |
| Sharpcraft.Common.Nbt.NbtList | src/Sharpcraft.Common/Nbt/NbtTags.cs | NbtList |  | 未匹配 |
| Sharpcraft.Common.Nbt.NbtLong | src/Sharpcraft.Common/Nbt/NbtTags.cs | NbtLong |  | 未匹配 |
| Sharpcraft.Common.Nbt.NbtLongArray | src/Sharpcraft.Common/Nbt/NbtTags.cs | NbtLongArray |  | 未匹配 |
| Sharpcraft.Common.Nbt.NbtShort | src/Sharpcraft.Common/Nbt/NbtTags.cs | NbtShort |  | 未匹配 |
| Sharpcraft.Common.Nbt.NbtString | src/Sharpcraft.Common/Nbt/NbtTags.cs | NbtString |  | 未匹配 |
| Sharpcraft.Common.Nbt.NbtTag | src/Sharpcraft.Common/Nbt/NbtTags.cs | NbtTag |  | 未匹配 |
| Sharpcraft.Common.Nbt.NbtWriter | src/Sharpcraft.Common/Nbt/NbtWriter.cs | NbtWriter |  | 未匹配 |
| Sharpcraft.Common.Network.AesCfb8 | src/Sharpcraft.Common/Network/AesCfb8.cs | AesCfb8 |  | 未匹配 |
| Sharpcraft.Common.Network.BlockEntityPackets | src/Sharpcraft.Common/Network/BlockEntityPackets.cs | BlockEntityPackets |  | 未匹配 |
| Sharpcraft.Common.Network.BlockEntityUpdate | src/Sharpcraft.Common/Network/BlockEntityPackets.cs | BlockEntityUpdate |  | 未匹配 |
| Sharpcraft.Common.Network.BlockPositionCodec | src/Sharpcraft.Common/Network/BlockPositionCodec.cs | BlockPositionCodec |  | 未匹配 |
| Sharpcraft.Common.Network.BlockChange | src/Sharpcraft.Common/Network/BlockUpdatePackets.cs | BlockChange |  | 未匹配 |
| Sharpcraft.Common.Network.BlockUpdatePackets | src/Sharpcraft.Common/Network/BlockUpdatePackets.cs | BlockUpdatePackets |  | 未匹配 |
| Sharpcraft.Common.Network.ChatClick | src/Sharpcraft.Common/Network/ChatInteractions.cs | ChatClick |  | 未匹配 |
| Sharpcraft.Common.Network.ChatClickAction | src/Sharpcraft.Common/Network/ChatInteractions.cs | ChatClickAction |  | 未匹配 |
| Sharpcraft.Common.Network.ChatInteractions | src/Sharpcraft.Common/Network/ChatInteractions.cs | ChatInteractions |  | 未匹配 |
| Sharpcraft.Common.Network.ChatPackets | src/Sharpcraft.Common/Network/ChatPackets.cs | ChatPackets |  | 未匹配 |
| Sharpcraft.Common.Network.ComponentText | src/Sharpcraft.Common/Network/ChatPackets.cs | ComponentText |  | 未匹配 |
| Sharpcraft.Common.Network.ChatTypeDecoration | src/Sharpcraft.Common/Network/ChatTypeDecorations.cs | ChatTypeDecoration | ref://mc-26.3-analysis/src/net/minecraft/network/chat/ChatTypeDecoration.java | resolved |
| Sharpcraft.Common.Network.ChatTypeTable | src/Sharpcraft.Common/Network/ChatTypeDecorations.cs | ChatTypeTable |  | 未匹配 |
| Sharpcraft.Common.Network.ChunkPacketParser | src/Sharpcraft.Common/Network/ChunkPacketParser.cs | ChunkPacketParser |  | 未匹配 |
| Sharpcraft.Common.Network.ParsedChunk | src/Sharpcraft.Common/Network/ChunkPacketParser.cs | ParsedChunk |  | 未匹配 |
| Sharpcraft.Common.Network.ComponentRuns | src/Sharpcraft.Common/Network/ComponentRuns.cs | ComponentRuns |  | 未匹配 |
| Sharpcraft.Common.Network.StyledRuns | src/Sharpcraft.Common/Network/ComponentRuns.cs | StyledRuns |  | 未匹配 |
| Sharpcraft.Common.Network.TextRun | src/Sharpcraft.Common/Network/ComponentRuns.cs | TextRun |  | 未匹配 |
| Sharpcraft.Common.Network.ChatTypeBinding | src/Sharpcraft.Common/Network/DisguisedChatPackets.cs | ChatTypeBinding |  | 未匹配 |
| Sharpcraft.Common.Network.DeleteChatPackets | src/Sharpcraft.Common/Network/DisguisedChatPackets.cs | DeleteChatPackets |  | 未匹配 |
| Sharpcraft.Common.Network.DisguisedChatPackets | src/Sharpcraft.Common/Network/DisguisedChatPackets.cs | DisguisedChatPackets |  | 未匹配 |
| Sharpcraft.Common.Network.DimensionTypeInfo | src/Sharpcraft.Common/Network/DynamicRegistries.cs | DimensionTypeInfo |  | 未匹配 |
| Sharpcraft.Common.Network.DynamicRegistries | src/Sharpcraft.Common/Network/DynamicRegistries.cs | DynamicRegistries |  | 未匹配 |
| Sharpcraft.Common.Network.Entry | src/Sharpcraft.Common/Network/DynamicRegistries.cs | Entry |  | 未匹配 |
| Sharpcraft.Common.Network.RegistryDataPackets | src/Sharpcraft.Common/Network/DynamicRegistries.cs | RegistryDataPackets |  | 未匹配 |
| Sharpcraft.Common.Network.EntityDataItem | src/Sharpcraft.Common/Network/EntityDataPackets.cs | EntityDataItem |  | 未匹配 |
| Sharpcraft.Common.Network.EntityDataPackets | src/Sharpcraft.Common/Network/EntityDataPackets.cs | EntityDataPackets |  | 未匹配 |
| Sharpcraft.Common.Network.EntityEventPackets | src/Sharpcraft.Common/Network/EntityEventPackets.cs | EntityEventPackets |  | 未匹配 |
| Sharpcraft.Common.Network.EntityLinkPackets | src/Sharpcraft.Common/Network/EntityLinkPackets.cs | EntityLinkPackets |  | 未匹配 |
| Sharpcraft.Common.Network.EntityMotion | src/Sharpcraft.Common/Network/EntityPackets.cs | EntityMotion |  | 未匹配 |
| Sharpcraft.Common.Network.GameEventPacket | src/Sharpcraft.Common/Network/GameEventPacket.cs | GameEventPacket |  | 未匹配 |
| Sharpcraft.Common.Network.GameEventPacketCodec | src/Sharpcraft.Common/Network/GameEventPacket.cs | GameEventPacketCodec |  | 未匹配 |
| Sharpcraft.Common.Network.HandshakePackets | src/Sharpcraft.Common/Network/HandshakePackets.cs | HandshakePackets |  | 未匹配 |
| Sharpcraft.Common.Network.ItemComponentInfo | src/Sharpcraft.Common/Network/ItemComponentPatchReader.cs | ItemComponentInfo |  | 未匹配 |
| Sharpcraft.Common.Network.LevelEventPackets | src/Sharpcraft.Common/Network/LevelEventPackets.cs | LevelEventPackets |  | 未匹配 |
| Sharpcraft.Common.Network.LightData | src/Sharpcraft.Common/Network/LightDataCodec.cs | LightData |  | 未匹配 |
| Sharpcraft.Common.Network.LightDataCodec | src/Sharpcraft.Common/Network/LightDataCodec.cs | LightDataCodec |  | 未匹配 |
| Sharpcraft.Common.Network.EncryptionRequest | src/Sharpcraft.Common/Network/LoginPackets.cs | EncryptionRequest |  | 未匹配 |
| Sharpcraft.Common.Network.LoginPackets | src/Sharpcraft.Common/Network/LoginPackets.cs | LoginPackets |  | 未匹配 |
| Sharpcraft.Common.Network.MinecraftConnection | src/Sharpcraft.Common/Network/MinecraftConnection.cs | MinecraftConnection |  | 未匹配 |
| Sharpcraft.Common.Network.LoginOutcome | src/Sharpcraft.Common/Network/MinecraftLoginClient.cs | LoginOutcome |  | 未匹配 |
| Sharpcraft.Common.Network.MinecraftLoginClient | src/Sharpcraft.Common/Network/MinecraftLoginClient.cs | MinecraftLoginClient |  | 未匹配 |
| Sharpcraft.Common.Network.MinecraftLoginCrypto | src/Sharpcraft.Common/Network/MinecraftLoginCrypto.cs | MinecraftLoginCrypto |  | 未匹配 |
| Sharpcraft.Common.Network.PacketCompression | src/Sharpcraft.Common/Network/PacketCompression.cs | PacketCompression |  | 未匹配 |
| Sharpcraft.Common.Network.PacketFrameBuffer | src/Sharpcraft.Common/Network/PacketFrameBuffer.cs | PacketFrameBuffer |  | 未匹配 |
| Sharpcraft.Common.Network.PacketFramer | src/Sharpcraft.Common/Network/PacketFramer.cs | PacketFramer |  | 未匹配 |
| Sharpcraft.Common.Network.PalettedContainerCodec | src/Sharpcraft.Common/Network/PalettedContainerCodec.cs | PalettedContainerCodec |  | 未匹配 |
| Sharpcraft.Common.Network.PassengerPackets | src/Sharpcraft.Common/Network/PassengerPackets.cs | PassengerPackets |  | 未匹配 |
| Sharpcraft.Common.Network.PayloadWriter | src/Sharpcraft.Common/Network/PayloadWriter.cs | PayloadWriter |  | 未匹配 |
| Sharpcraft.Common.Network.PlayMovementPackets | src/Sharpcraft.Common/Network/PlayMovementPackets.cs | PlayMovementPackets |  | 未匹配 |
| Sharpcraft.Common.Network.JoinGameInfo | src/Sharpcraft.Common/Network/PlayPackets.cs | JoinGameInfo |  | 未匹配 |
| Sharpcraft.Common.Network.PlayPackets | src/Sharpcraft.Common/Network/PlayPackets.cs | PlayPackets |  | 未匹配 |
| Sharpcraft.Common.Network.PlaySession | src/Sharpcraft.Common/Network/PlaySession.cs | PlaySession |  | 未匹配 |
| Sharpcraft.Common.Network.PlaySessionStats | src/Sharpcraft.Common/Network/PlaySession.cs | PlaySessionStats |  | 未匹配 |
| Sharpcraft.Common.Network.PlayerChatPackets | src/Sharpcraft.Common/Network/PlayerChatPackets.cs | PlayerChatPackets |  | 未匹配 |
| Sharpcraft.Common.Network.PlayerInfoUpdate | src/Sharpcraft.Common/Network/PlayerListPackets.cs | PlayerInfoUpdate |  | 未匹配 |
| Sharpcraft.Common.Network.PlayerListActions | src/Sharpcraft.Common/Network/PlayerListPackets.cs | PlayerListActions |  | 未匹配 |
| Sharpcraft.Common.Network.PlayerListPackets | src/Sharpcraft.Common/Network/PlayerListPackets.cs | PlayerListPackets |  | 未匹配 |
| Sharpcraft.Common.Network.PlayerPositionInfo | src/Sharpcraft.Common/Network/PlayerPositionInfo.cs | PlayerPositionInfo |  | 未匹配 |
| Sharpcraft.Common.Network.PlayerRotationInfo | src/Sharpcraft.Common/Network/PlayerPositionInfo.cs | PlayerRotationInfo |  | 未匹配 |
| Sharpcraft.Common.Network.MovePacketKind | src/Sharpcraft.Common/Network/PlayerSync.cs | MovePacketKind |  | 未匹配 |
| Sharpcraft.Common.Network.PlayerSync | src/Sharpcraft.Common/Network/PlayerSync.cs | PlayerSync |  | 未匹配 |
| Sharpcraft.Common.Network.ServerPositionOutcome | src/Sharpcraft.Common/Network/PlayerSync.cs | ServerPositionOutcome |  | 未匹配 |
| Sharpcraft.Common.Network.ConfigurationClientbound | src/Sharpcraft.Common/Network/ProtocolConstants.cs | ConfigurationClientbound |  | 未匹配 |
| Sharpcraft.Common.Network.ConfigurationServerbound | src/Sharpcraft.Common/Network/ProtocolConstants.cs | ConfigurationServerbound |  | 未匹配 |
| Sharpcraft.Common.Network.ConnectionState | src/Sharpcraft.Common/Network/ProtocolConstants.cs | ConnectionState |  | 未匹配 |
| Sharpcraft.Common.Network.HandshakeServerbound | src/Sharpcraft.Common/Network/ProtocolConstants.cs | HandshakeServerbound |  | 未匹配 |
| Sharpcraft.Common.Network.LoginClientbound | src/Sharpcraft.Common/Network/ProtocolConstants.cs | LoginClientbound |  | 未匹配 |
| Sharpcraft.Common.Network.LoginServerbound | src/Sharpcraft.Common/Network/ProtocolConstants.cs | LoginServerbound |  | 未匹配 |
| Sharpcraft.Common.Network.PacketIds | src/Sharpcraft.Common/Network/ProtocolConstants.cs | PacketIds |  | 未匹配 |
| Sharpcraft.Common.Network.PlayClientbound | src/Sharpcraft.Common/Network/ProtocolConstants.cs | PlayClientbound |  | 未匹配 |
| Sharpcraft.Common.Network.PlayServerbound | src/Sharpcraft.Common/Network/ProtocolConstants.cs | PlayServerbound |  | 未匹配 |
| Sharpcraft.Common.Network.ProtocolConstants | src/Sharpcraft.Common/Network/ProtocolConstants.cs | ProtocolConstants |  | 未匹配 |
| Sharpcraft.Common.Network.StatusClientbound | src/Sharpcraft.Common/Network/ProtocolConstants.cs | StatusClientbound |  | 未匹配 |
| Sharpcraft.Common.Network.StatusServerbound | src/Sharpcraft.Common/Network/ProtocolConstants.cs | StatusServerbound |  | 未匹配 |
| Sharpcraft.Common.Network.ResolvedServerAddress | src/Sharpcraft.Common/Network/ServerAddressResolver.cs | ResolvedServerAddress | ref://mc-26.3-analysis/src/net/minecraft/client/multiplayer/resolver/ResolvedServerAddress.java | resolved |
| Sharpcraft.Common.Network.ServerAddressResolver | src/Sharpcraft.Common/Network/ServerAddressResolver.cs | ServerAddressResolver | ref://mc-26.3-analysis/src/net/minecraft/client/multiplayer/resolver/ServerAddressResolver.java | resolved |
| Sharpcraft.Common.Network.EquipmentItem | src/Sharpcraft.Common/Network/SetEquipmentPackets.cs | EquipmentItem |  | 未匹配 |
| Sharpcraft.Common.Network.SetEquipmentPackets | src/Sharpcraft.Common/Network/SetEquipmentPackets.cs | SetEquipmentPackets |  | 未匹配 |
| Sharpcraft.Common.Network.ClockNetworkState | src/Sharpcraft.Common/Network/SetTimePacket.cs | ClockNetworkState | ref://mc-26.3-analysis/src/net/minecraft/world/clock/ClockNetworkState.java | resolved |
| Sharpcraft.Common.Network.SetTimePacket | src/Sharpcraft.Common/Network/SetTimePacket.cs | SetTimePacket |  | 未匹配 |
| Sharpcraft.Common.Network.SetTimePacketCodec | src/Sharpcraft.Common/Network/SetTimePacket.cs | SetTimePacketCodec |  | 未匹配 |
| Sharpcraft.Common.Network.StatusPingClient | src/Sharpcraft.Common/Network/StatusPingClient.cs | StatusPingClient |  | 未匹配 |
| Sharpcraft.Common.Network.StatusPingResult | src/Sharpcraft.Common/Network/StatusPingClient.cs | StatusPingResult |  | 未匹配 |
| Sharpcraft.Common.Network.SwingAnimationPackets | src/Sharpcraft.Common/Network/SwingAnimationPackets.cs | SwingAnimationPackets |  | 未匹配 |
| Sharpcraft.Common.Network.AttributeValue | src/Sharpcraft.Common/Network/UpdateAttributesPackets.cs | AttributeValue |  | 未匹配 |
| Sharpcraft.Common.Network.UpdateAttributesPackets | src/Sharpcraft.Common/Network/UpdateAttributesPackets.cs | UpdateAttributesPackets |  | 未匹配 |
| Sharpcraft.Common.Network.Utf8StringCodec | src/Sharpcraft.Common/Network/Utf8StringCodec.cs | Utf8StringCodec |  | 未匹配 |
| Sharpcraft.Common.Network.DecodeStatus | src/Sharpcraft.Common/Network/VarInt.cs | DecodeStatus |  | 未匹配 |
| Sharpcraft.Common.Network.VarInt | src/Sharpcraft.Common/Network/VarInt.cs | VarInt | ref://mc-26.3-analysis/src/net/minecraft/network/VarInt.java | resolved |
| Sharpcraft.Common.Network.VarLong | src/Sharpcraft.Common/Network/VarLong.cs | VarLong | ref://mc-26.3-analysis/src/net/minecraft/network/VarLong.java | resolved |
| Sharpcraft.Common.Random.IPositionalRandomFactory | src/Sharpcraft.Common/Random/IPositionalRandomFactory.cs | IPositionalRandomFactory |  | 未匹配 |
| Sharpcraft.Common.Random.IRandomSource | src/Sharpcraft.Common/Random/IRandomSource.cs | IRandomSource |  | 未匹配 |
| Sharpcraft.Common.Random.JavaStringHash | src/Sharpcraft.Common/Random/JavaStringHash.cs | JavaStringHash |  | 未匹配 |
| Sharpcraft.Common.Random.LegacyPositionalRandomFactory | src/Sharpcraft.Common/Random/LegacyPositionalRandomFactory.cs | LegacyPositionalRandomFactory |  | 未匹配 |
| Sharpcraft.Common.Random.LegacyRandomSource | src/Sharpcraft.Common/Random/LegacyRandomSource.cs | LegacyRandomSource | ref://mc-26.3-analysis/src/net/minecraft/world/level/levelgen/LegacyRandomSource.java | resolved |
| Sharpcraft.Common.Random.MarsagliaPolarGaussian | src/Sharpcraft.Common/Random/MarsagliaPolarGaussian.cs | MarsagliaPolarGaussian | ref://mc-26.3-analysis/src/net/minecraft/world/level/levelgen/MarsagliaPolarGaussian.java | resolved |
| Sharpcraft.Common.Random.PositionalSeed | src/Sharpcraft.Common/Random/PositionalSeed.cs | PositionalSeed |  | 未匹配 |
| Sharpcraft.Common.Random.RandomSequence | src/Sharpcraft.Common/Random/RandomSequence.cs | RandomSequence | ref://mc-26.3-analysis/src/net/minecraft/world/RandomSequence.java | resolved |
| Sharpcraft.Common.Random.RandomSource | src/Sharpcraft.Common/Random/RandomSource.cs | RandomSource | ref://mc-26.3-analysis/src/net/minecraft/util/RandomSource.java | resolved |
| Sharpcraft.Common.Random.SeedSupport | src/Sharpcraft.Common/Random/SeedSupport.cs | SeedSupport |  | 未匹配 |
| Sharpcraft.Common.Random.Algorithm | src/Sharpcraft.Common/Random/WorldgenRandom.cs | Algorithm |  | 未匹配 |
| Sharpcraft.Common.Random.WorldgenRandom | src/Sharpcraft.Common/Random/WorldgenRandom.cs | WorldgenRandom | ref://mc-26.3-analysis/src/net/minecraft/world/level/levelgen/WorldgenRandom.java | resolved |
| Sharpcraft.Common.Random.Xoroshiro128PlusPlus | src/Sharpcraft.Common/Random/Xoroshiro128PlusPlus.cs | Xoroshiro128PlusPlus | ref://mc-26.3-analysis/src/net/minecraft/world/level/levelgen/Xoroshiro128PlusPlus.java | resolved |
| Sharpcraft.Common.Random.XoroshiroPositionalRandomFactory | src/Sharpcraft.Common/Random/XoroshiroPositionalRandomFactory.cs | XoroshiroPositionalRandomFactory |  | 未匹配 |
| Sharpcraft.Common.Random.XoroshiroRandomSource | src/Sharpcraft.Common/Random/XoroshiroRandomSource.cs | XoroshiroRandomSource | ref://mc-26.3-analysis/src/net/minecraft/world/level/levelgen/XoroshiroRandomSource.java | resolved |
| Sharpcraft.Common.Reference.ReferenceClassAttribute | src/Sharpcraft.Common/Reference/ReferenceClassAttribute.cs | ReferenceClassAttribute |  | 未匹配 |
| Sharpcraft.Common.Reference.ReferenceKind | src/Sharpcraft.Common/Reference/ReferenceClassAttribute.cs | ReferenceKind |  | 未匹配 |
| Sharpcraft.Common.Reference.ReferenceStatus | src/Sharpcraft.Common/Reference/ReferenceClassAttribute.cs | ReferenceStatus |  | 未匹配 |
| Sharpcraft.Common.Registries.AttributeTypes | src/Sharpcraft.Common/Registries/AttributeTypes.cs | AttributeTypes | ref://mc-26.3-analysis/src/net/minecraft/world/attribute/AttributeTypes.java | resolved |
| Sharpcraft.Common.Registries.BlockStateDefinition | src/Sharpcraft.Common/Registries/BlockStateDefinition.cs | BlockStateDefinition |  | 未匹配 |
| Sharpcraft.Common.Registries.BlockStateLayout | src/Sharpcraft.Common/Registries/BlockStateLayout.cs | BlockStateLayout |  | 未匹配 |
| Sharpcraft.Common.Registries.Entry | src/Sharpcraft.Common/Registries/BlockStateLayout.cs | Entry |  | 未匹配 |
| Sharpcraft.Common.Registries.BlockStateProperty | src/Sharpcraft.Common/Registries/BlockStateProperty.cs | BlockStateProperty |  | 未匹配 |
| Sharpcraft.Common.Registries.DataComponentTypes | src/Sharpcraft.Common/Registries/DataComponentTypes.cs | DataComponentTypes |  | 未匹配 |
| Sharpcraft.Common.Registries.EnchantmentTypes | src/Sharpcraft.Common/Registries/EnchantmentTypes.cs | EnchantmentTypes |  | 未匹配 |
| Sharpcraft.Common.Registries.EntityDimensionsInfo | src/Sharpcraft.Common/Registries/EntityDimensionsRegistry.cs | EntityDimensionsInfo |  | 未匹配 |
| Sharpcraft.Common.Registries.EntityDimensionsRegistry | src/Sharpcraft.Common/Registries/EntityDimensionsRegistry.cs | EntityDimensionsRegistry |  | 未匹配 |
| Sharpcraft.Common.Registries.IdMapper | src/Sharpcraft.Common/Registries/IdMapper.cs | IdMapper | ref://mc-26.3-analysis/src/net/minecraft/core/IdMapper.java | resolved |
| Sharpcraft.Common.Registries.ItemInfo | src/Sharpcraft.Common/Registries/ItemRegistry.cs | ItemInfo |  | 未匹配 |
| Sharpcraft.Common.Registries.ItemRegistry | src/Sharpcraft.Common/Registries/ItemRegistry.cs | ItemRegistry |  | 未匹配 |
| Sharpcraft.Common.Registries.ItemTagRegistry | src/Sharpcraft.Common/Registries/ItemTagRegistry.cs | ItemTagRegistry |  | 未匹配 |
| Sharpcraft.Common.Registries.Entry | src/Sharpcraft.Common/Registries/Registry.cs | Entry |  | 未匹配 |
| Sharpcraft.Common.Registries.Registry | src/Sharpcraft.Common/Registries/Registry.cs | Registry | ref://mc-26.3-analysis/src/net/minecraft/core/Registry.java | resolved |
| Sharpcraft.Common.Registries.VanillaBlockRegistry | src/Sharpcraft.Common/Registries/VanillaBlockRegistry.cs | VanillaBlockRegistry |  | 未匹配 |
| Sharpcraft.Common.World.BiomeClimate | src/Sharpcraft.Common/World/BiomeRegistry.cs | BiomeClimate |  | 未匹配 |
| Sharpcraft.Common.World.BiomeEffects | src/Sharpcraft.Common/World/BiomeRegistry.cs | BiomeEffects |  | 未匹配 |
| Sharpcraft.Common.World.BiomeEntry | src/Sharpcraft.Common/World/BiomeRegistry.cs | BiomeEntry |  | 未匹配 |
| Sharpcraft.Common.World.BiomeRegistry | src/Sharpcraft.Common/World/BiomeRegistry.cs | BiomeRegistry |  | 未匹配 |
| Sharpcraft.Common.World.GrassColourModifier | src/Sharpcraft.Common/World/BiomeRegistry.cs | GrassColourModifier |  | 未匹配 |
| Sharpcraft.Common.World.BlockEntityRecord | src/Sharpcraft.Common/World/BlockEntityRecord.cs | BlockEntityRecord |  | 未匹配 |
| Sharpcraft.Common.World.BlockTint | src/Sharpcraft.Common/World/BlockTint.cs | BlockTint |  | 未匹配 |
| Sharpcraft.Common.World.ChunkLight | src/Sharpcraft.Common/World/ChunkLight.cs | ChunkLight |  | 未匹配 |
| Sharpcraft.Common.World.ChunkSection | src/Sharpcraft.Common/World/ChunkSection.cs | ChunkSection |  | 未匹配 |
| Sharpcraft.Common.World.ColorMapColorUtil | src/Sharpcraft.Common/World/ColorMapColorUtil.cs | ColorMapColorUtil | ref://mc-26.3-analysis/src/net/minecraft/world/level/ColorMapColorUtil.java | resolved |
| Sharpcraft.Common.World.ColourBlend | src/Sharpcraft.Common/World/ColourBlend.cs | ColourBlend |  | 未匹配 |
| Sharpcraft.Common.World.FluidKind | src/Sharpcraft.Common/World/FluidState.cs | FluidKind |  | 未匹配 |
| Sharpcraft.Common.World.FluidState | src/Sharpcraft.Common/World/FluidState.cs | FluidState | ref://mc-26.3-analysis/src/net/minecraft/world/level/material/FluidState.java | resolved |
| Sharpcraft.Common.World.FluidStateCodec | src/Sharpcraft.Common/World/FluidState.cs | FluidStateCodec |  | 未匹配 |
| Sharpcraft.Common.World.GaussianWeights | src/Sharpcraft.Common/World/GaussianWeights.cs | GaussianWeights |  | 未匹配 |
| Sharpcraft.Common.World.LevelChunk | src/Sharpcraft.Common/World/LevelChunk.cs | LevelChunk | ref://mc-26.3-analysis/src/net/minecraft/world/level/chunk/LevelChunk.java | resolved |
| Sharpcraft.Common.World.PalettedContainer | src/Sharpcraft.Common/World/PalettedContainer.cs | PalettedContainer | ref://mc-26.3-analysis/src/net/minecraft/world/level/chunk/PalettedContainer.java | resolved |
| Sharpcraft.Common.World.SkyColour | src/Sharpcraft.Common/World/SkyColour.cs | SkyColour |  | 未匹配 |
| Sharpcraft.Common.World.SkyColourSource | src/Sharpcraft.Common/World/SkyColourSource.cs | SkyColourSource |  | 未匹配 |
| Sharpcraft.Common.World.CubicBezierEasing | src/Sharpcraft.Common/World/SkyTimelines.cs | CubicBezierEasing |  | 未匹配 |
| Sharpcraft.Common.World.FloatKeyframe | src/Sharpcraft.Common/World/SkyTimelines.cs | FloatKeyframe |  | 未匹配 |
| Sharpcraft.Common.World.FloatKeyframeTrack | src/Sharpcraft.Common/World/SkyTimelines.cs | FloatKeyframeTrack |  | 未匹配 |
| Sharpcraft.Common.World.SkyAttributeTracks | src/Sharpcraft.Common/World/SkyTimelines.cs | SkyAttributeTracks |  | 未匹配 |
| Sharpcraft.Common.World.SkyTimelines | src/Sharpcraft.Common/World/SkyTimelines.cs | SkyTimelines |  | 未匹配 |
| Sharpcraft.Common.World.WeatherBlend | src/Sharpcraft.Common/World/WeatherBlend.cs | WeatherBlend |  | 未匹配 |
| Sharpcraft.Framework.Platform.GuiCanvas | src/Sharpcraft.Framework/Platform/GuiScale.cs | GuiCanvas |  | 未匹配 |
| Sharpcraft.Framework.Platform.GuiScale | src/Sharpcraft.Framework/Platform/GuiScale.cs | GuiScale |  | 未匹配 |
| Sharpcraft.Framework.Platform.InputKey | src/Sharpcraft.Framework/Platform/InputKey.cs | InputKey |  | 未匹配 |
| Sharpcraft.Framework.Platform.TimerResolution | src/Sharpcraft.Framework/Platform/TimerResolution.cs | TimerResolution |  | 未匹配 |
| Sharpcraft.Framework.Platform.NativeSurfaceKind | src/Sharpcraft.Framework/Platform/WindowHost.cs | NativeSurfaceKind |  | 未匹配 |
| Sharpcraft.Framework.Platform.NativeSurfaceSource | src/Sharpcraft.Framework/Platform/WindowHost.cs | NativeSurfaceSource |  | 未匹配 |
| Sharpcraft.Framework.Platform.WindowHost | src/Sharpcraft.Framework/Platform/WindowHost.cs | WindowHost |  | 未匹配 |
| Sharpcraft.Framework.Platform.WindowInputState | src/Sharpcraft.Framework/Platform/WindowInputState.cs | WindowInputState |  | 未匹配 |
| Sharpcraft.Framework.Rendering.AtlasTextureSource | src/Sharpcraft.Framework/Rendering/AtlasTextureSource.cs | AtlasTextureSource |  | 未匹配 |
| Sharpcraft.Framework.Rendering.BackendCapabilities | src/Sharpcraft.Framework/Rendering/BackendCapabilities.cs | BackendCapabilities |  | 未匹配 |
| Sharpcraft.Framework.Rendering.GraphicsCapability | src/Sharpcraft.Framework/Rendering/BackendCapabilities.cs | GraphicsCapability |  | 未匹配 |
| Sharpcraft.Framework.Rendering.CameraUniform | src/Sharpcraft.Framework/Rendering/CameraUniform.cs | CameraUniform |  | 未匹配 |
| Sharpcraft.Framework.Rendering.ChunkCuller | src/Sharpcraft.Framework/Rendering/ChunkCuller.cs | ChunkCuller |  | 未匹配 |
| Sharpcraft.Framework.Rendering.ChunkGeometryStore | src/Sharpcraft.Framework/Rendering/ChunkGeometryStore.cs | ChunkGeometryStore |  | 未匹配 |
| Sharpcraft.Framework.Rendering.ClipSpaceTransform | src/Sharpcraft.Framework/Rendering/ClipSpaceTransform.cs | ClipSpaceTransform |  | 未匹配 |
| Sharpcraft.Framework.Rendering.DepthLinearisation | src/Sharpcraft.Framework/Rendering/DepthLinearisation.cs | DepthLinearisation |  | 未匹配 |
| Sharpcraft.Framework.Rendering.DepthPyramid | src/Sharpcraft.Framework/Rendering/DepthPyramid.cs | DepthPyramid |  | 未匹配 |
| Sharpcraft.Framework.Rendering.DynamicGeometry | src/Sharpcraft.Framework/Rendering/DynamicGeometry.cs | DynamicGeometry |  | 未匹配 |
| Sharpcraft.Framework.Rendering.EntityGeometryLayer | src/Sharpcraft.Framework/Rendering/EntityGeometryLayer.cs | EntityGeometryLayer |  | 未匹配 |
| Sharpcraft.Framework.Rendering.EntityGeometryLayerKind | src/Sharpcraft.Framework/Rendering/EntityGeometryLayer.cs | EntityGeometryLayerKind |  | 未匹配 |
| Sharpcraft.Framework.Rendering.FrameDiagnostics | src/Sharpcraft.Framework/Rendering/FrameDiagnostics.cs | FrameDiagnostics |  | 未匹配 |
| Sharpcraft.Framework.Rendering.CameraMath | src/Sharpcraft.Framework/Rendering/FrameRequest.cs | CameraMath |  | 未匹配 |
| Sharpcraft.Framework.Rendering.CelestialColorBlend | src/Sharpcraft.Framework/Rendering/FrameRequest.cs | CelestialColorBlend |  | 未匹配 |
| Sharpcraft.Framework.Rendering.CelestialColorDraw | src/Sharpcraft.Framework/Rendering/FrameRequest.cs | CelestialColorDraw |  | 未匹配 |
| Sharpcraft.Framework.Rendering.CelestialQuadDraw | src/Sharpcraft.Framework/Rendering/FrameRequest.cs | CelestialQuadDraw |  | 未匹配 |
| Sharpcraft.Framework.Rendering.FrameRequest | src/Sharpcraft.Framework/Rendering/FrameRequest.cs | FrameRequest |  | 未匹配 |
| Sharpcraft.Framework.Rendering.FrameResult | src/Sharpcraft.Framework/Rendering/FrameRequest.cs | FrameResult |  | 未匹配 |
| Sharpcraft.Framework.Rendering.LightmapUniform | src/Sharpcraft.Framework/Rendering/FrameRequest.cs | LightmapUniform |  | 未匹配 |
| Sharpcraft.Framework.Rendering.TextureData | src/Sharpcraft.Framework/Rendering/FrameRequest.cs | TextureData |  | 未匹配 |
| Sharpcraft.Framework.Rendering.FrustumPlanes | src/Sharpcraft.Framework/Rendering/FrustumPlanes.cs | FrustumPlanes |  | 未匹配 |
| Sharpcraft.Framework.Rendering.GeometryData | src/Sharpcraft.Framework/Rendering/GeometryData.cs | GeometryData |  | 未匹配 |
| Sharpcraft.Framework.Rendering.LightmapUniformCodec | src/Sharpcraft.Framework/Rendering/LightmapUniformCodec.cs | LightmapUniformCodec |  | 未匹配 |
| Sharpcraft.Framework.Rendering.OffscreenRenderer | src/Sharpcraft.Framework/Rendering/OffscreenRenderer.cs | OffscreenRenderer |  | 未匹配 |
| Sharpcraft.Framework.Rendering.RenderingUnavailableException | src/Sharpcraft.Framework/Rendering/OffscreenRenderer.cs | RenderingUnavailableException |  | 未匹配 |
| Sharpcraft.Framework.Rendering.OitWavelet | src/Sharpcraft.Framework/Rendering/OitWavelet.cs | OitWavelet |  | 未匹配 |
| Sharpcraft.Framework.Rendering.PixelComparison | src/Sharpcraft.Framework/Rendering/PixelComparison.cs | PixelComparison |  | 未匹配 |
| Sharpcraft.Framework.Rendering.PixelDifference | src/Sharpcraft.Framework/Rendering/PixelComparison.cs | PixelDifference |  | 未匹配 |
| Sharpcraft.Framework.Rendering.ShaderPacks.PackDiagnostic | src/Sharpcraft.Framework/Rendering/ShaderPacks/PackDiagnostic.cs | PackDiagnostic |  | 未匹配 |
| Sharpcraft.Framework.Rendering.ShaderPacks.PackDiagnosticKind | src/Sharpcraft.Framework/Rendering/ShaderPacks/PackDiagnostic.cs | PackDiagnosticKind |  | 未匹配 |
| Sharpcraft.Framework.Rendering.ShaderPacks.PackValidation | src/Sharpcraft.Framework/Rendering/ShaderPacks/PackDiagnostic.cs | PackValidation |  | 未匹配 |
| Sharpcraft.Framework.Rendering.ShaderPacks.KnownUniforms | src/Sharpcraft.Framework/Rendering/ShaderPacks/ShaderPackDescription.cs | KnownUniforms |  | 未匹配 |
| Sharpcraft.Framework.Rendering.ShaderPacks.PhaseDescription | src/Sharpcraft.Framework/Rendering/ShaderPacks/ShaderPackDescription.cs | PhaseDescription |  | 未匹配 |
| Sharpcraft.Framework.Rendering.ShaderPacks.RenderTargetDescription | src/Sharpcraft.Framework/Rendering/ShaderPacks/ShaderPackDescription.cs | RenderTargetDescription |  | 未匹配 |
| Sharpcraft.Framework.Rendering.ShaderPacks.ShaderPackDescription | src/Sharpcraft.Framework/Rendering/ShaderPacks/ShaderPackDescription.cs | ShaderPackDescription |  | 未匹配 |
| Sharpcraft.Framework.Rendering.ShaderPacks.ShaderPackValidator | src/Sharpcraft.Framework/Rendering/ShaderPacks/ShaderPackValidator.cs | ShaderPackValidator |  | 未匹配 |
| Sharpcraft.Framework.Rendering.ShaderPacks.Absolute | src/Sharpcraft.Framework/Rendering/ShaderPacks/TargetSize.cs | Absolute | ref://mc-26.3-analysis/src/net/minecraft/world/level/storage/loot/providers/number/floats/Absolute.java<br>ref://mc-26.3-analysis/src/net/minecraft/world/level/storage/loot/providers/number/ints/Absolute.java | ambiguous |
| Sharpcraft.Framework.Rendering.ShaderPacks.Relative | src/Sharpcraft.Framework/Rendering/ShaderPacks/TargetSize.cs | Relative | ref://mc-26.3-analysis/src/net/minecraft/world/entity/Relative.java | resolved |
| Sharpcraft.Framework.Rendering.ShaderPacks.TargetSize | src/Sharpcraft.Framework/Rendering/ShaderPacks/TargetSize.cs | TargetSize |  | 未匹配 |
| Sharpcraft.Framework.Rendering.Shaders.ShaderBindings | src/Sharpcraft.Framework/Rendering/Shaders/ShaderBindings.cs | ShaderBindings |  | 未匹配 |
| Sharpcraft.Framework.Rendering.Shaders.ShaderDescriptor | src/Sharpcraft.Framework/Rendering/Shaders/ShaderDescriptor.cs | ShaderDescriptor |  | 未匹配 |
| Sharpcraft.Framework.Rendering.Shaders.ShaderIncludeComposer | src/Sharpcraft.Framework/Rendering/Shaders/ShaderIncludeComposer.cs | ShaderIncludeComposer |  | 未匹配 |
| Sharpcraft.Framework.Rendering.Shaders.ShaderLibrary | src/Sharpcraft.Framework/Rendering/Shaders/ShaderLibrary.cs | ShaderLibrary |  | 未匹配 |
| Sharpcraft.Framework.Rendering.Shaders.ShaderPipelines | src/Sharpcraft.Framework/Rendering/Shaders/ShaderPipelines.cs | ShaderPipelines |  | 未匹配 |
| Sharpcraft.Framework.Rendering.UiFrame | src/Sharpcraft.Framework/Rendering/UiFrame.cs | UiFrame |  | 未匹配 |
| Sharpcraft.Framework.Rendering.Range | src/Sharpcraft.Framework/Rendering/UiGeometry.cs | Range |  | 未匹配 |
| Sharpcraft.Framework.Rendering.UiGeometry | src/Sharpcraft.Framework/Rendering/UiGeometry.cs | UiGeometry |  | 未匹配 |
| Sharpcraft.Framework.Rendering.UiMaterial | src/Sharpcraft.Framework/Rendering/UiGeometry.cs | UiMaterial |  | 未匹配 |
| Sharpcraft.Framework.Rendering.UiProjection | src/Sharpcraft.Framework/Rendering/UiProjection.cs | UiProjection |  | 未匹配 |
| Sharpcraft.Framework.Rendering.UiRenderer | src/Sharpcraft.Framework/Rendering/UiRenderer.cs | UiRenderer |  | 未匹配 |
| Sharpcraft.Framework.Rendering.UiTextureSource | src/Sharpcraft.Framework/Rendering/UiTextureSource.cs | UiTextureSource |  | 未匹配 |
| Sharpcraft.Framework.Rendering.VertexLayout | src/Sharpcraft.Framework/Rendering/VertexLayout.cs | VertexLayout |  | 未匹配 |
| Sharpcraft.Framework.Rendering.WindowedRenderer | src/Sharpcraft.Framework/Rendering/WindowedRenderer.cs | WindowedRenderer |  | 未匹配 |
| Sharpcraft.Framework.Rendering.WorldTextDraw | src/Sharpcraft.Framework/Rendering/WorldTextDraw.cs | WorldTextDraw |  | 未匹配 |
