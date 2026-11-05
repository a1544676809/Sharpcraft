<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

# Class-map 提案（人工核对，勿直接提交）

由 `tools/Sharpcraft.ClassMap -- --propose` 生成。仅含类名与 ref:// 指针，无原版源码。

| Sharpcraft type | 候选原版类 | 匹配路径 | 状态 |
| --- | --- | --- | --- |
| Sharpcraft.Auth.AuthError | AuthError |  | 未匹配 |
| Sharpcraft.Auth.AuthException | AuthException |  | 未匹配 |
| Sharpcraft.Auth.DeviceCodeChallenge | DeviceCodeChallenge |  | 未匹配 |
| Sharpcraft.Auth.MicrosoftToken | MicrosoftToken |  | 未匹配 |
| Sharpcraft.Auth.MinecraftProfile | MinecraftProfile |  | 未匹配 |
| Sharpcraft.Auth.MinecraftSession | MinecraftSession |  | 未匹配 |
| Sharpcraft.Auth.XboxUserToken | XboxUserToken |  | 未匹配 |
| Sharpcraft.Auth.XstsToken | XstsToken |  | 未匹配 |
| Sharpcraft.Auth.DeviceCodeFlow | DeviceCodeFlow |  | 未匹配 |
| Sharpcraft.Auth.IdentityEndpoints | IdentityEndpoints |  | 未匹配 |
| Sharpcraft.Auth.MinecraftServicesClient | MinecraftServicesClient |  | 未匹配 |
| Sharpcraft.Auth.SessionServerClient | SessionServerClient |  | 未匹配 |
| Sharpcraft.Auth.StoredTokens | StoredTokens |  | 未匹配 |
| Sharpcraft.Auth.TokenStore | TokenStore |  | 未匹配 |
| Sharpcraft.Auth.XboxLiveAuthenticator | XboxLiveAuthenticator |  | 未匹配 |
| Sharpcraft.Client.Assets.AnimationFrameSize | AnimationFrameSize |  | 未匹配 |
| Sharpcraft.Client.Assets.AssetPackEntry | AssetPackEntry |  | 未匹配 |
| Sharpcraft.Client.Assets.AssetPackManifest | AssetPackManifest |  | 未匹配 |
| Sharpcraft.Client.Assets.BuiltInFallbackSource | BuiltInFallbackSource |  | 未匹配 |
| Sharpcraft.Client.Assets.DirectoryAssetSource | DirectoryAssetSource |  | 未匹配 |
| Sharpcraft.Client.Assets.OfficialObjectsSource | OfficialObjectsSource |  | 未匹配 |
| Sharpcraft.Client.Assets.ZipAssetSource | ZipAssetSource |  | 未匹配 |
| Sharpcraft.Client.Assets.AssetNotFoundException | AssetNotFoundException |  | 未匹配 |
| Sharpcraft.Client.Assets.AssetStack | AssetStack |  | 未匹配 |
| Sharpcraft.Client.Assets.IAssetSource | IAssetSource |  | 未匹配 |
| Sharpcraft.Client.Assets.IMetadataAssetSource | IMetadataAssetSource |  | 未匹配 |
| Sharpcraft.Client.Assets.MissingResourcePolicy | MissingResourcePolicy |  | 未匹配 |
| Sharpcraft.Client.Assets.AtlasMipChain | AtlasMipChain |  | 未匹配 |
| Sharpcraft.Client.Assets.BitmapFontDefinition | BitmapFontDefinition |  | 未匹配 |
| Sharpcraft.Client.Assets.BitmapFontProvider | BitmapFont |  | 未匹配 |
| Sharpcraft.Client.Assets.BlockFaceTextures | BlockFaceTextures |  | 未匹配 |
| Sharpcraft.Client.Assets.BlockModelResolver | BlockModelResolver | ref://mc-26.3-analysis/src/net/minecraft/client/renderer/block/BlockModelResolver.java | resolved |
| Sharpcraft.Client.Assets.BlockFace | BlockFace |  | 未匹配 |
| Sharpcraft.Client.Assets.BlockSpriteMap | BlockSpriteMap |  | 未匹配 |
| Sharpcraft.Client.Assets.CelestialSprites | CelestialSprites |  | 未匹配 |
| Sharpcraft.Client.Assets.CloudTexture | CloudTexture |  | 未匹配 |
| Sharpcraft.Client.Assets.Colormap | Colormap |  | 未匹配 |
| Sharpcraft.Client.Assets.PackFormatVersion | PackFormatVersion |  | 未匹配 |
| Sharpcraft.Client.Assets.PackWatcher | PackWatcher |  | 未匹配 |
| Sharpcraft.Client.Assets.ResourceFilter | ResourceFilter |  | 未匹配 |
| Sharpcraft.Client.Assets.ResourceManager | ResourceManager | ref://mc-26.3-analysis/src/net/minecraft/server/packs/resources/ResourceManager.java | resolved |
| Sharpcraft.Client.Assets.ResourcePack | ResourcePack |  | 未匹配 |
| Sharpcraft.Client.Assets.ResourcePackKind | ResourcePackKind |  | 未匹配 |
| Sharpcraft.Client.Assets.ResourcePackDiscovery | ResourcePackDiscovery |  | 未匹配 |
| Sharpcraft.Client.Assets.ResourcePackFormats | ResourcePackFormats |  | 未匹配 |
| Sharpcraft.Client.Assets.OverlayEntry | OverlayEntry |  | 未匹配 |
| Sharpcraft.Client.Assets.ResourcePackOverlays | ResourcePackOverlays |  | 未匹配 |
| Sharpcraft.Client.Assets.ResourcePackSource | ResourcePackSource |  | 未匹配 |
| Sharpcraft.Client.Assets.ResourcePackStack | ResourcePackStack |  | 未匹配 |
| Sharpcraft.Client.Assets.PackMetadata | PackMetadata |  | 未匹配 |
| Sharpcraft.Client.Assets.ResourceKind | ResourceKind |  | 未匹配 |
| Sharpcraft.Client.Assets.ResourceLookup | ResourceLookup |  | 未匹配 |
| Sharpcraft.Client.Assets.ResourcePath | ResourcePath |  | 未匹配 |
| Sharpcraft.Client.Assets.SpriteAlpha | SpriteAlpha |  | 未匹配 |
| Sharpcraft.Client.Assets.SpriteAtlas | SpriteAtlas |  | 未匹配 |
| Sharpcraft.Client.Assets.SpriteAtlasBuilder | SpriteAtlasBuilder |  | 未匹配 |
| Sharpcraft.Client.Assets.SpriteRect | SpriteRect |  | 未匹配 |
| Sharpcraft.Client.Assets.SpriteImageCache | SpriteImageCache |  | 未匹配 |
| Sharpcraft.Client.Assets.Transparency | Transparency | ref://mc-26.3-analysis/src/com/mojang/blaze3d/platform/Transparency.java | resolved |
| Sharpcraft.Client.Camera.CameraController | CameraController |  | 未匹配 |
| Sharpcraft.Client.Camera.LookConvention | LookConvention |  | 未匹配 |
| Sharpcraft.Client.ClientArguments | ClientArguments |  | 未匹配 |
| Sharpcraft.Client.Game.PlayerInterpolator | PlayerInterpolator |  | 未匹配 |
| Sharpcraft.Client.Game.PreciseWait | PreciseWait |  | 未匹配 |
| Sharpcraft.Client.Input.FrameInput | FrameInput |  | 未匹配 |
| Sharpcraft.Client.Input.InputAction | InputAction |  | 未匹配 |
| Sharpcraft.Client.Input.InputMapper | InputMapper |  | 未匹配 |
| Sharpcraft.Client.Input.KeyBindings | KeyBindings |  | 未匹配 |
| Sharpcraft.Client.Render.AllayAnimationProvider | AllayModel | ref://mc-26.3-analysis/src/net/minecraft/client/model/animal/allay/AllayModel.java | resolved |
| Sharpcraft.Client.Render.AllayModelDefinition | AllayModel | ref://mc-26.3-analysis/src/net/minecraft/client/model/animal/allay/AllayModel.java | resolved |
| Sharpcraft.Client.Render.AnimationClipPlayer | AnimationClipPlayer |  | 未匹配 |
| Sharpcraft.Client.Render.ArmadilloAnimationProvider | ArmadilloModel | ref://mc-26.3-analysis/src/net/minecraft/client/model/animal/armadillo/ArmadilloModel.java | resolved |
| Sharpcraft.Client.Render.ArmadilloAnimations | ArmadilloAnimation | ref://mc-26.3-analysis/src/net/minecraft/client/animation/definitions/ArmadilloAnimation.java | resolved |
| Sharpcraft.Client.Render.ArmadilloModelDefinition | ArmadilloModel | ref://mc-26.3-analysis/src/net/minecraft/client/model/animal/armadillo/ArmadilloModel.java | resolved |
| Sharpcraft.Client.Render.AxolotlAnimationProvider | AxolotlModel / AxolotlAnimation |  | 未匹配 |
| Sharpcraft.Client.Render.AxolotlAnimations | AxolotlAnimation |  | 未匹配 |
| Sharpcraft.Client.Render.AxolotlModelDefinition | AxolotlModel |  | 未匹配 |
| Sharpcraft.Client.Render.BabyModelDefinitions | BabyModelDefinitions |  | 未匹配 |
| Sharpcraft.Client.Render.BabyModelDefinitionsAquatic | BabyModelDefinitionsAquatic |  | 未匹配 |
| Sharpcraft.Client.Render.BabyModelDefinitionsEquine | BabyModelDefinitionsEquine |  | 未匹配 |
| Sharpcraft.Client.Render.BabyModelDefinitionsExtra | BabyModelDefinitionsExtra |  | 未匹配 |
| Sharpcraft.Client.Render.BabyModelDefinitionsFinal | BabyModelDefinitionsFinal |  | 未匹配 |
| Sharpcraft.Client.Render.BabyModelDefinitionsHumanoid | BabyModelDefinitionsHumanoid |  | 未匹配 |
| Sharpcraft.Client.Render.BabyModelDefinitionsMisc | BabyModelDefinitionsMisc |  | 未匹配 |
| Sharpcraft.Client.Render.BabyModelDefinitionsNether | BabyModelDefinitionsNether |  | 未匹配 |
| Sharpcraft.Client.Render.BabyModelDefinitionsOdd | BabyModelDefinitionsOdd |  | 未匹配 |
| Sharpcraft.Client.Render.BatAnimationProvider | BatModel | ref://mc-26.3-analysis/src/net/minecraft/client/model/ambient/BatModel.java | resolved |
| Sharpcraft.Client.Render.BatAnimations | BatAnimation | ref://mc-26.3-analysis/src/net/minecraft/client/animation/definitions/BatAnimation.java | resolved |
| Sharpcraft.Client.Render.BatModelDefinition | BatModel | ref://mc-26.3-analysis/src/net/minecraft/client/model/ambient/BatModel.java | resolved |
| Sharpcraft.Client.Render.BeeAnimationProvider | BeeModel | ref://mc-26.3-analysis/src/net/minecraft/client/model/animal/bee/BeeModel.java | resolved |
| Sharpcraft.Client.Render.BeeModelDefinition | BeeModel | ref://mc-26.3-analysis/src/net/minecraft/client/model/animal/bee/BeeModel.java | resolved |
| Sharpcraft.Client.Render.BiomeTint | BiomeTint |  | 未匹配 |
| Sharpcraft.Client.Render.BlockTintKind | BlockTintKind |  | 未匹配 |
| Sharpcraft.Client.Render.BlazeAnimationProvider | BlazeModel | ref://mc-26.3-analysis/src/net/minecraft/client/model/monster/blaze/BlazeModel.java | resolved |
| Sharpcraft.Client.Render.BlazeModelDefinition | BlazeModel | ref://mc-26.3-analysis/src/net/minecraft/client/model/monster/blaze/BlazeModel.java | resolved |
| Sharpcraft.Client.Render.BlockEntityPlaceholderMesher | BlockEntityPlaceholderMesher |  | 未匹配 |
| Sharpcraft.Client.Render.BlockEntityRenderState | BlockEntityRenderState | ref://mc-26.3-analysis/src/net/minecraft/client/renderer/blockentity/state/BlockEntityRenderState.java | resolved |
| Sharpcraft.Client.Render.BlockLayer | BlockLayer |  | 未匹配 |
| Sharpcraft.Client.Render.BlockLayerRules | BlockLayerRules |  | 未匹配 |
| Sharpcraft.Client.Render.BlockLightFlicker | BlockLightFlicker |  | 未匹配 |
| Sharpcraft.Client.Render.BlockRenderLayers | BlockRenderLayers |  | 未匹配 |
| Sharpcraft.Client.Render.BlockTintTable | BlockTintTable |  | 未匹配 |
| Sharpcraft.Client.Render.BreezeAnimationProvider | BreezeModel | ref://mc-26.3-analysis/src/net/minecraft/client/model/monster/breeze/BreezeModel.java | resolved |
| Sharpcraft.Client.Render.BreezeAnimations | BreezeAnimation | ref://mc-26.3-analysis/src/net/minecraft/client/animation/definitions/BreezeAnimation.java | resolved |
| Sharpcraft.Client.Render.BreezeModelDefinition | BreezeModel | ref://mc-26.3-analysis/src/net/minecraft/client/model/monster/breeze/BreezeModel.java | resolved |
| Sharpcraft.Client.Render.CamelAnimationProvider | CamelModel | ref://mc-26.3-analysis/src/net/minecraft/client/model/animal/camel/CamelModel.java | resolved |
| Sharpcraft.Client.Render.CamelAnimations | CamelAnimation | ref://mc-26.3-analysis/src/net/minecraft/client/animation/definitions/CamelAnimation.java | resolved |
| Sharpcraft.Client.Render.CamelModelDefinition | CamelModel | ref://mc-26.3-analysis/src/net/minecraft/client/model/animal/camel/CamelModel.java | resolved |
| Sharpcraft.Client.Render.CameraJitter | CameraJitter |  | 未匹配 |
| Sharpcraft.Client.Render.CatModelDefinition | CatModel |  | 未匹配 |
| Sharpcraft.Client.Render.CaveSpiderModelDefinition | CaveSpiderModel |  | 未匹配 |
| Sharpcraft.Client.Render.CelestialGeometry | CelestialGeometry |  | 未匹配 |
| Sharpcraft.Client.Render.CelestialMeshBuilder | CelestialMeshBuilder |  | 未匹配 |
| Sharpcraft.Client.Render.ChickenAnimationProvider | ChickenModel | ref://mc-26.3-analysis/src/net/minecraft/client/model/animal/chicken/ChickenModel.java | resolved |
| Sharpcraft.Client.Render.ChickenModelDefinition | ChickenModel | ref://mc-26.3-analysis/src/net/minecraft/client/model/animal/chicken/ChickenModel.java | resolved |
| Sharpcraft.Client.Render.ChunkMeshCache | ChunkMeshCache |  | 未匹配 |
| Sharpcraft.Client.Render.ChunkMesher | ChunkMesher |  | 未匹配 |
| Sharpcraft.Client.Render.MeshData | MeshData | ref://mc-26.3-analysis/src/com/mojang/blaze3d/vertex/MeshData.java | resolved |
| Sharpcraft.Client.Render.CloudDrawCache | CloudDrawCache |  | 未匹配 |
| Sharpcraft.Client.Render.CloudGeometryBuilder | CloudGeometryBuilder |  | 未匹配 |
| Sharpcraft.Client.Render.CloudCameraSide | CloudCameraSide |  | 未匹配 |
| Sharpcraft.Client.Render.CloudFaceDirection | CloudFaceDirection |  | 未匹配 |
| Sharpcraft.Client.Render.CloudMeshBuilder | CloudMeshBuilder |  | 未匹配 |
| Sharpcraft.Client.Render.CloudQuad | CloudQuad |  | 未匹配 |
| Sharpcraft.Client.Render.CloudRangePolicy | CloudRangePolicy |  | 未匹配 |
| Sharpcraft.Client.Render.CodAnimationProvider | CodModel | ref://mc-26.3-analysis/src/net/minecraft/client/model/animal/fish/CodModel.java | resolved |
| Sharpcraft.Client.Render.CodModelDefinition | CodModel | ref://mc-26.3-analysis/src/net/minecraft/client/model/animal/fish/CodModel.java | resolved |
| Sharpcraft.Client.Render.CopperGolemAnimationProvider | CopperGolemModel | ref://mc-26.3-analysis/src/net/minecraft/client/model/animal/golem/CopperGolemModel.java | resolved |
| Sharpcraft.Client.Render.CopperGolemAnimations | CopperGolemAnimation | ref://mc-26.3-analysis/src/net/minecraft/client/animation/definitions/CopperGolemAnimation.java | resolved |
| Sharpcraft.Client.Render.CopperGolemModelDefinition | CopperGolemModel | ref://mc-26.3-analysis/src/net/minecraft/client/model/animal/golem/CopperGolemModel.java | resolved |
| Sharpcraft.Client.Render.CowModelDefinition | CowModel | ref://mc-26.3-analysis/src/net/minecraft/client/model/animal/cow/CowModel.java | resolved |
| Sharpcraft.Client.Render.CreakingAnimationProvider | CreakingModel | ref://mc-26.3-analysis/src/net/minecraft/client/model/monster/creaking/CreakingModel.java | resolved |
| Sharpcraft.Client.Render.CreakingAnimations | CreakingAnimation | ref://mc-26.3-analysis/src/net/minecraft/client/animation/definitions/CreakingAnimation.java | resolved |
| Sharpcraft.Client.Render.CreakingModelDefinition | CreakingModel | ref://mc-26.3-analysis/src/net/minecraft/client/model/monster/creaking/CreakingModel.java | resolved |
| Sharpcraft.Client.Render.CreeperModelDefinition | CreeperModel | ref://mc-26.3-analysis/src/net/minecraft/client/model/monster/creeper/CreeperModel.java | resolved |
| Sharpcraft.Client.Render.DolphinAnimationProvider | DolphinModel | ref://mc-26.3-analysis/src/net/minecraft/client/model/animal/dolphin/DolphinModel.java | resolved |
| Sharpcraft.Client.Render.DolphinModelDefinition | DolphinModel | ref://mc-26.3-analysis/src/net/minecraft/client/model/animal/dolphin/DolphinModel.java | resolved |
| Sharpcraft.Client.Render.EnderDragonAnimationProvider | EnderDragonModel | ref://mc-26.3-analysis/src/net/minecraft/client/model/monster/dragon/EnderDragonModel.java | resolved |
| Sharpcraft.Client.Render.State | State |  | 未匹配 |
| Sharpcraft.Client.Render.EnderDragonModelDefinition | EnderDragonModel | ref://mc-26.3-analysis/src/net/minecraft/client/model/monster/dragon/EnderDragonModel.java | resolved |
| Sharpcraft.Client.Render.EndermanAnimationProvider | EndermanModel | ref://mc-26.3-analysis/src/net/minecraft/client/model/monster/enderman/EndermanModel.java | resolved |
| Sharpcraft.Client.Render.EndermiteAnimationProvider | EndermiteModel | ref://mc-26.3-analysis/src/net/minecraft/client/model/monster/endermite/EndermiteModel.java | resolved |
| Sharpcraft.Client.Render.EndermiteModelDefinition | EndermiteModel | ref://mc-26.3-analysis/src/net/minecraft/client/model/monster/endermite/EndermiteModel.java | resolved |
| Sharpcraft.Client.Render.AnimationChannel | AnimationChannel | ref://mc-26.3-analysis/src/net/minecraft/client/animation/AnimationChannel.java | resolved |
| Sharpcraft.Client.Render.AnimationDefinition | AnimationDefinition | ref://mc-26.3-analysis/src/net/minecraft/client/animation/AnimationDefinition.java | resolved |
| Sharpcraft.Client.Render.AnimationInterpolation | AnimationInterpolation |  | 未匹配 |
| Sharpcraft.Client.Render.AnimationKeyframe | AnimationKeyframe |  | 未匹配 |
| Sharpcraft.Client.Render.AnimationState | AnimationState | ref://mc-26.3-analysis/src/net/minecraft/world/entity/AnimationState.java | resolved |
| Sharpcraft.Client.Render.AnimationTarget | AnimationTarget |  | 未匹配 |
| Sharpcraft.Client.Render.EntityPose | EntityPose |  | 未匹配 |
| Sharpcraft.Client.Render.Entry | Entry |  | 未匹配 |
| Sharpcraft.Client.Render.KeyframeAnimation | KeyframeAnimation | ref://mc-26.3-analysis/src/net/minecraft/client/animation/KeyframeAnimation.java | resolved |
| Sharpcraft.Client.Render.KeyframeAnimations | KeyframeAnimation | ref://mc-26.3-analysis/src/net/minecraft/client/animation/KeyframeAnimation.java | resolved |
| Sharpcraft.Client.Render.PoseOffset | PoseOffset |  | 未匹配 |
| Sharpcraft.Client.Render.EntityAnimationRegistry | EntityAnimationRegistry |  | 未匹配 |
| Sharpcraft.Client.Render.IEntityAnimationProvider | IEntityModel / IEntityAnimation |  | 未匹配 |
| Sharpcraft.Client.Render.EntityModelDefinition | EntityModel | ref://mc-26.3-analysis/src/net/minecraft/client/model/EntityModel.java | resolved |
| Sharpcraft.Client.Render.EntityModelLayerHelpers | EntityModelLayerHelpers |  | 未匹配 |
| Sharpcraft.Client.Render.EntityModelResolver | EntityModelResolver |  | 未匹配 |
| Sharpcraft.Client.Render.EntityPlaceholderMesher | EntityPlaceholderMesher |  | 未匹配 |
| Sharpcraft.Client.Render.EntitySkinLibrary | EntitySkinLibrary |  | 未匹配 |
| Sharpcraft.Client.Render.EntitySkinTexture | EntitySkinTexture |  | 未匹配 |
| Sharpcraft.Client.Render.EntityVariantTable | EntityVariantTable |  | 未匹配 |
| Sharpcraft.Client.Render.EquineAnimationProvider | EquineModel / EquineAnimation |  | 未匹配 |
| Sharpcraft.Client.Render.EquipmentModelParts | EquipmentModelParts |  | 未匹配 |
| Sharpcraft.Client.Render.EquipmentAssetKind | EquipmentAssetKind |  | 未匹配 |
| Sharpcraft.Client.Render.EquipmentTexture | EquipmentTexture |  | 未匹配 |
| Sharpcraft.Client.Render.EquipmentTextureCatalog | EquipmentTextureCatalog |  | 未匹配 |
| Sharpcraft.Client.Render.FaceUvTransform | FaceUvTransform |  | 未匹配 |
| Sharpcraft.Client.Render.FelineAnimationProvider | FelineModel / FelineAnimation |  | 未匹配 |
| Sharpcraft.Client.Render.FluidSurface | FluidSurface |  | 未匹配 |
| Sharpcraft.Client.Render.FoxAnimationProvider | FoxModel | ref://mc-26.3-analysis/src/net/minecraft/client/model/animal/fox/FoxModel.java | resolved |
| Sharpcraft.Client.Render.FoxModelDefinition | FoxModel | ref://mc-26.3-analysis/src/net/minecraft/client/model/animal/fox/FoxModel.java | resolved |
| Sharpcraft.Client.Render.FrogAnimationProvider | FrogModel | ref://mc-26.3-analysis/src/net/minecraft/client/model/animal/frog/FrogModel.java | resolved |
| Sharpcraft.Client.Render.FrogAnimations | FrogAnimation | ref://mc-26.3-analysis/src/net/minecraft/client/animation/definitions/FrogAnimation.java | resolved |
| Sharpcraft.Client.Render.FrogModelDefinition | FrogModel | ref://mc-26.3-analysis/src/net/minecraft/client/model/animal/frog/FrogModel.java | resolved |
| Sharpcraft.Client.Render.GhastAnimationProvider | GhastModel | ref://mc-26.3-analysis/src/net/minecraft/client/model/monster/ghast/GhastModel.java | resolved |
| Sharpcraft.Client.Render.GhastModelDefinition | GhastModel | ref://mc-26.3-analysis/src/net/minecraft/client/model/monster/ghast/GhastModel.java | resolved |
| Sharpcraft.Client.Render.GiantModelDefinition | GiantModel |  | 未匹配 |
| Sharpcraft.Client.Render.GoatModelDefinition | GoatModel | ref://mc-26.3-analysis/src/net/minecraft/client/model/animal/goat/GoatModel.java | resolved |
| Sharpcraft.Client.Render.GuardianAnimationProvider | GuardianModel | ref://mc-26.3-analysis/src/net/minecraft/client/model/monster/guardian/GuardianModel.java | resolved |
| Sharpcraft.Client.Render.State | State |  | 未匹配 |
| Sharpcraft.Client.Render.GuardianModelDefinition | GuardianModel | ref://mc-26.3-analysis/src/net/minecraft/client/model/monster/guardian/GuardianModel.java | resolved |
| Sharpcraft.Client.Render.HappyGhastAnimationProvider | HappyGhastModel | ref://mc-26.3-analysis/src/net/minecraft/client/model/animal/ghast/HappyGhastModel.java | resolved |
| Sharpcraft.Client.Render.HappyGhastModelDefinition | HappyGhastModel | ref://mc-26.3-analysis/src/net/minecraft/client/model/animal/ghast/HappyGhastModel.java | resolved |
| Sharpcraft.Client.Render.HoglinAnimationProvider | HoglinModel | ref://mc-26.3-analysis/src/net/minecraft/client/model/monster/hoglin/HoglinModel.java | resolved |
| Sharpcraft.Client.Render.HoglinModelDefinition | HoglinModel | ref://mc-26.3-analysis/src/net/minecraft/client/model/monster/hoglin/HoglinModel.java | resolved |
| Sharpcraft.Client.Render.HorseModelDefinition | HorseModel | ref://mc-26.3-analysis/src/net/minecraft/client/model/animal/equine/HorseModel.java | resolved |
| Sharpcraft.Client.Render.HumanoidAnimationProvider | HumanoidModel | ref://mc-26.3-analysis/src/net/minecraft/client/model/HumanoidModel.java | resolved |
| Sharpcraft.Client.Render.HumanoidMobModelDefinition | HumanoidMobModel |  | 未匹配 |
| Sharpcraft.Client.Render.IllagerAnimationProvider | IllagerModel | ref://mc-26.3-analysis/src/net/minecraft/client/model/monster/illager/IllagerModel.java | resolved |
| Sharpcraft.Client.Render.IllagerArmPose | IllagerArmPose |  | 未匹配 |
| Sharpcraft.Client.Render.IllagerModelDefinition | IllagerModel | ref://mc-26.3-analysis/src/net/minecraft/client/model/monster/illager/IllagerModel.java | resolved |
| Sharpcraft.Client.Render.IronGolemAnimationProvider | IronGolemModel | ref://mc-26.3-analysis/src/net/minecraft/client/model/animal/golem/IronGolemModel.java | resolved |
| Sharpcraft.Client.Render.IronGolemModelDefinition | IronGolemModel | ref://mc-26.3-analysis/src/net/minecraft/client/model/animal/golem/IronGolemModel.java | resolved |
| Sharpcraft.Client.Render.LayerSinks | LayerSinks |  | 未匹配 |
| Sharpcraft.Client.Render.LlamaAnimationProvider | LlamaModel | ref://mc-26.3-analysis/src/net/minecraft/client/model/animal/llama/LlamaModel.java | resolved |
| Sharpcraft.Client.Render.LlamaModelDefinition | LlamaModel | ref://mc-26.3-analysis/src/net/minecraft/client/model/animal/llama/LlamaModel.java | resolved |
| Sharpcraft.Client.Render.MagmaCubeAnimationProvider | MagmaCubeModel | ref://mc-26.3-analysis/src/net/minecraft/client/model/monster/slime/MagmaCubeModel.java | resolved |
| Sharpcraft.Client.Render.MagmaCubeModelDefinition | MagmaCubeModel | ref://mc-26.3-analysis/src/net/minecraft/client/model/monster/slime/MagmaCubeModel.java | resolved |
| Sharpcraft.Client.Render.MeshBuffer | MeshBuffer |  | 未匹配 |
| Sharpcraft.Client.Render.MeshBuildPolicy | MeshBuildPolicy |  | 未匹配 |
| Sharpcraft.Client.Render.MeshedChunk | MeshedChunk |  | 未匹配 |
| Sharpcraft.Client.Render.NameplateProjection | NameplateProjection |  | 未匹配 |
| Sharpcraft.Client.Render.NautilusAnimationProvider | NautilusModel | ref://mc-26.3-analysis/src/net/minecraft/client/model/animal/nautilus/NautilusModel.java | resolved |
| Sharpcraft.Client.Render.NautilusAnimations | NautilusAnimation | ref://mc-26.3-analysis/src/net/minecraft/client/animation/definitions/NautilusAnimation.java | resolved |
| Sharpcraft.Client.Render.NautilusModelDefinition | NautilusModel | ref://mc-26.3-analysis/src/net/minecraft/client/model/animal/nautilus/NautilusModel.java | resolved |
| Sharpcraft.Client.Render.OcclusionCulling | OcclusionCulling |  | 未匹配 |
| Sharpcraft.Client.Render.PandaAnimationProvider | PandaModel | ref://mc-26.3-analysis/src/net/minecraft/client/model/animal/panda/PandaModel.java | resolved |
| Sharpcraft.Client.Render.PandaModelDefinition | PandaModel | ref://mc-26.3-analysis/src/net/minecraft/client/model/animal/panda/PandaModel.java | resolved |
| Sharpcraft.Client.Render.ParrotAnimationProvider | ParrotModel | ref://mc-26.3-analysis/src/net/minecraft/client/model/animal/parrot/ParrotModel.java | resolved |
| Sharpcraft.Client.Render.ParrotModelDefinition | ParrotModel | ref://mc-26.3-analysis/src/net/minecraft/client/model/animal/parrot/ParrotModel.java | resolved |
| Sharpcraft.Client.Render.PhantomAnimationProvider | PhantomModel | ref://mc-26.3-analysis/src/net/minecraft/client/model/monster/phantom/PhantomModel.java | resolved |
| Sharpcraft.Client.Render.PhantomModelDefinition | PhantomModel | ref://mc-26.3-analysis/src/net/minecraft/client/model/monster/phantom/PhantomModel.java | resolved |
| Sharpcraft.Client.Render.PigModelDefinition | PigModel | ref://mc-26.3-analysis/src/net/minecraft/client/model/animal/pig/PigModel.java | resolved |
| Sharpcraft.Client.Render.PiglinAnimationProvider | PiglinModel | ref://mc-26.3-analysis/src/net/minecraft/client/model/monster/piglin/PiglinModel.java | resolved |
| Sharpcraft.Client.Render.PiglinArmPose | PiglinArmPose | ref://mc-26.3-analysis/src/net/minecraft/world/entity/monster/piglin/PiglinArmPose.java | resolved |
| Sharpcraft.Client.Render.PiglinModelDefinition | PiglinModel | ref://mc-26.3-analysis/src/net/minecraft/client/model/monster/piglin/PiglinModel.java | resolved |
| Sharpcraft.Client.Render.EntityModelLayer | EntityModelLayer |  | 未匹配 |
| Sharpcraft.Client.Render.PolarBearAnimationProvider | PolarBearModel | ref://mc-26.3-analysis/src/net/minecraft/client/model/animal/polarbear/PolarBearModel.java | resolved |
| Sharpcraft.Client.Render.PolarBearModelDefinition | PolarBearModel | ref://mc-26.3-analysis/src/net/minecraft/client/model/animal/polarbear/PolarBearModel.java | resolved |
| Sharpcraft.Client.Render.PufferfishAnimationProvider | PufferfishModel / PufferfishAnimation |  | 未匹配 |
| Sharpcraft.Client.Render.PufferfishModelDefinition | PufferfishModel |  | 未匹配 |
| Sharpcraft.Client.Render.QuadrupedAnimationProvider | QuadrupedModel | ref://mc-26.3-analysis/src/net/minecraft/client/model/QuadrupedModel.java | resolved |
| Sharpcraft.Client.Render.RabbitAnimationProvider | RabbitModel | ref://mc-26.3-analysis/src/net/minecraft/client/model/animal/rabbit/RabbitModel.java | resolved |
| Sharpcraft.Client.Render.RabbitModelDefinition | RabbitModel | ref://mc-26.3-analysis/src/net/minecraft/client/model/animal/rabbit/RabbitModel.java | resolved |
| Sharpcraft.Client.Render.RavagerAnimationProvider | RavagerModel | ref://mc-26.3-analysis/src/net/minecraft/client/model/monster/ravager/RavagerModel.java | resolved |
| Sharpcraft.Client.Render.RavagerModelDefinition | RavagerModel | ref://mc-26.3-analysis/src/net/minecraft/client/model/monster/ravager/RavagerModel.java | resolved |
| Sharpcraft.Client.Render.FrameHistory | FrameHistory |  | 未匹配 |
| Sharpcraft.Client.Render.Reprojection | Reprojection |  | 未匹配 |
| Sharpcraft.Client.Render.SalmonAnimationProvider | SalmonModel | ref://mc-26.3-analysis/src/net/minecraft/client/model/animal/fish/SalmonModel.java | resolved |
| Sharpcraft.Client.Render.SalmonModelDefinition | SalmonModel | ref://mc-26.3-analysis/src/net/minecraft/client/model/animal/fish/SalmonModel.java | resolved |
| Sharpcraft.Client.Render.SheepModelDefinition | SheepModel | ref://mc-26.3-analysis/src/net/minecraft/client/model/animal/sheep/SheepModel.java | resolved |
| Sharpcraft.Client.Render.ShulkerAnimationProvider | ShulkerModel | ref://mc-26.3-analysis/src/net/minecraft/client/model/monster/shulker/ShulkerModel.java | resolved |
| Sharpcraft.Client.Render.ShulkerModelDefinition | ShulkerModel | ref://mc-26.3-analysis/src/net/minecraft/client/model/monster/shulker/ShulkerModel.java | resolved |
| Sharpcraft.Client.Render.SilverfishAnimationProvider | SilverfishModel | ref://mc-26.3-analysis/src/net/minecraft/client/model/monster/silverfish/SilverfishModel.java | resolved |
| Sharpcraft.Client.Render.SilverfishModelDefinition | SilverfishModel | ref://mc-26.3-analysis/src/net/minecraft/client/model/monster/silverfish/SilverfishModel.java | resolved |
| Sharpcraft.Client.Render.SkeletonAnimationProvider | SkeletonModel | ref://mc-26.3-analysis/src/net/minecraft/client/model/monster/skeleton/SkeletonModel.java | resolved |
| Sharpcraft.Client.Render.SkyVisibility | SkyVisibility |  | 未匹配 |
| Sharpcraft.Client.Render.SlimeModelDefinition | SlimeModel | ref://mc-26.3-analysis/src/net/minecraft/client/model/monster/slime/SlimeModel.java | resolved |
| Sharpcraft.Client.Render.SmoothLighting | SmoothLighting |  | 未匹配 |
| Sharpcraft.Client.Render.SnifferAnimationProvider | SnifferModel | ref://mc-26.3-analysis/src/net/minecraft/client/model/animal/sniffer/SnifferModel.java | resolved |
| Sharpcraft.Client.Render.SnifferAnimations | SnifferAnimation | ref://mc-26.3-analysis/src/net/minecraft/client/animation/definitions/SnifferAnimation.java | resolved |
| Sharpcraft.Client.Render.SnifferModelDefinition | SnifferModel | ref://mc-26.3-analysis/src/net/minecraft/client/model/animal/sniffer/SnifferModel.java | resolved |
| Sharpcraft.Client.Render.SnowGolemAnimationProvider | SnowGolemModel | ref://mc-26.3-analysis/src/net/minecraft/client/model/animal/golem/SnowGolemModel.java | resolved |
| Sharpcraft.Client.Render.SnowGolemModelDefinition | SnowGolemModel | ref://mc-26.3-analysis/src/net/minecraft/client/model/animal/golem/SnowGolemModel.java | resolved |
| Sharpcraft.Client.Render.SpiderAnimationProvider | SpiderModel | ref://mc-26.3-analysis/src/net/minecraft/client/model/monster/spider/SpiderModel.java | resolved |
| Sharpcraft.Client.Render.SpiderModelDefinition | SpiderModel | ref://mc-26.3-analysis/src/net/minecraft/client/model/monster/spider/SpiderModel.java | resolved |
| Sharpcraft.Client.Render.SquidAnimationProvider | SquidModel | ref://mc-26.3-analysis/src/net/minecraft/client/model/animal/squid/SquidModel.java | resolved |
| Sharpcraft.Client.Render.SquidModelDefinition | SquidModel | ref://mc-26.3-analysis/src/net/minecraft/client/model/animal/squid/SquidModel.java | resolved |
| Sharpcraft.Client.Render.LegacyRandom | LegacyRandom |  | 未匹配 |
| Sharpcraft.Client.Render.StarField | StarField |  | 未匹配 |
| Sharpcraft.Client.Render.StateCullTable | StateCullTable |  | 未匹配 |
| Sharpcraft.Client.Render.QuadVisual | QuadVisual |  | 未匹配 |
| Sharpcraft.Client.Render.StateVisualCache | StateVisualCache |  | 未匹配 |
| Sharpcraft.Client.Render.StriderAnimationProvider | StriderModel | ref://mc-26.3-analysis/src/net/minecraft/client/model/monster/strider/StriderModel.java | resolved |
| Sharpcraft.Client.Render.StriderModelDefinition | StriderModel | ref://mc-26.3-analysis/src/net/minecraft/client/model/monster/strider/StriderModel.java | resolved |
| Sharpcraft.Client.Render.TadpoleAnimationProvider | TadpoleModel | ref://mc-26.3-analysis/src/net/minecraft/client/model/animal/frog/TadpoleModel.java | resolved |
| Sharpcraft.Client.Render.TadpoleModelDefinition | TadpoleModel | ref://mc-26.3-analysis/src/net/minecraft/client/model/animal/frog/TadpoleModel.java | resolved |
| Sharpcraft.Client.Render.TraderLlamaModelDefinition | TraderLlamaModel |  | 未匹配 |
| Sharpcraft.Client.Render.TranslucentOrder | TranslucentOrder |  | 未匹配 |
| Sharpcraft.Client.Render.TropicalFishAnimationProvider | TropicalFishModel / TropicalFishAnimation |  | 未匹配 |
| Sharpcraft.Client.Render.TropicalFishModelDefinition | TropicalFishModel |  | 未匹配 |
| Sharpcraft.Client.Render.TurtleAnimationProvider | TurtleModel | ref://mc-26.3-analysis/src/net/minecraft/client/model/animal/turtle/TurtleModel.java | resolved |
| Sharpcraft.Client.Render.TurtleModelDefinition | TurtleModel | ref://mc-26.3-analysis/src/net/minecraft/client/model/animal/turtle/TurtleModel.java | resolved |
| Sharpcraft.Client.Render.LightmapParameters | LightmapParameters |  | 未匹配 |
| Sharpcraft.Client.Render.VanillaLightmap | VanillaLightmap |  | 未匹配 |
| Sharpcraft.Client.Render.VexAnimationProvider | VexModel | ref://mc-26.3-analysis/src/net/minecraft/client/model/monster/vex/VexModel.java | resolved |
| Sharpcraft.Client.Render.VexModelDefinition | VexModel | ref://mc-26.3-analysis/src/net/minecraft/client/model/monster/vex/VexModel.java | resolved |
| Sharpcraft.Client.Render.ViewFrustum | ViewFrustum |  | 未匹配 |
| Sharpcraft.Client.Render.VillagerAnimationProvider | VillagerModel | ref://mc-26.3-analysis/src/net/minecraft/client/model/npc/VillagerModel.java | resolved |
| Sharpcraft.Client.Render.VillagerModelDefinition | VillagerModel | ref://mc-26.3-analysis/src/net/minecraft/client/model/npc/VillagerModel.java | resolved |
| Sharpcraft.Client.Render.WardenAnimationProvider | WardenModel | ref://mc-26.3-analysis/src/net/minecraft/client/model/monster/warden/WardenModel.java | resolved |
| Sharpcraft.Client.Render.WardenAnimations | WardenAnimation | ref://mc-26.3-analysis/src/net/minecraft/client/animation/definitions/WardenAnimation.java | resolved |
| Sharpcraft.Client.Render.WardenModelDefinition | WardenModel | ref://mc-26.3-analysis/src/net/minecraft/client/model/monster/warden/WardenModel.java | resolved |
| Sharpcraft.Client.Render.WitchAnimationProvider | WitchModel | ref://mc-26.3-analysis/src/net/minecraft/client/model/monster/witch/WitchModel.java | resolved |
| Sharpcraft.Client.Render.WitchModelDefinition | WitchModel | ref://mc-26.3-analysis/src/net/minecraft/client/model/monster/witch/WitchModel.java | resolved |
| Sharpcraft.Client.Render.WitherAnimationProvider | WitherModel / WitherAnimation |  | 未匹配 |
| Sharpcraft.Client.Render.WitherBossModelDefinition | WitherBossModel | ref://mc-26.3-analysis/src/net/minecraft/client/model/monster/wither/WitherBossModel.java | resolved |
| Sharpcraft.Client.Render.WolfAnimationProvider | WolfModel | ref://mc-26.3-analysis/src/net/minecraft/client/model/animal/wolf/WolfModel.java | resolved |
| Sharpcraft.Client.Render.WolfModelDefinition | WolfModel | ref://mc-26.3-analysis/src/net/minecraft/client/model/animal/wolf/WolfModel.java | resolved |
| Sharpcraft.Client.Render.WorldTextMeshBuilder | WorldTextMeshBuilder |  | 未匹配 |
| Sharpcraft.Client.Render.ZombieNautilusModelDefinition | ZombieNautilusModel |  | 未匹配 |
| Sharpcraft.Client.Render.ZombieVillagerModelDefinition | ZombieVillagerModel | ref://mc-26.3-analysis/src/net/minecraft/client/model/monster/zombie/ZombieVillagerModel.java | resolved |
| Sharpcraft.Client.Ui.ActionBar | ActionBar |  | 未匹配 |
| Sharpcraft.Client.Ui.BitmapFont | BitmapFont |  | 未匹配 |
| Sharpcraft.Client.Ui.Glyph | Glyph |  | 未匹配 |
| Sharpcraft.Client.Ui.ChatHitTest | ChatHitTest |  | 未匹配 |
| Sharpcraft.Client.Ui.ChatInput | ChatInput |  | 未匹配 |
| Sharpcraft.Client.Ui.ChatLog | ChatLog | ref://mc-26.3-analysis/src/net/minecraft/client/multiplayer/chat/ChatLog.java | resolved |
| Sharpcraft.Client.Ui.MessageTag | MessageTag |  | 未匹配 |
| Sharpcraft.Client.Ui.ChatPrompt | ChatPrompt |  | 未匹配 |
| Sharpcraft.Client.Ui.FontDefinition | FontDefinition |  | 未匹配 |
| Sharpcraft.Client.Ui.GuiSpriteAtlas | GuiSpriteAtlas |  | 未匹配 |
| Sharpcraft.Client.Ui.GuiSpriteBorder | GuiSpriteBorder |  | 未匹配 |
| Sharpcraft.Client.Ui.GuiSpriteScaling | GuiSpriteScaling | ref://mc-26.3-analysis/src/net/minecraft/client/resources/metadata/gui/GuiSpriteScaling.java | resolved |
| Sharpcraft.Client.Ui.GuiSpriteScalingType | GuiSpriteScalingType |  | 未匹配 |
| Sharpcraft.Client.Ui.NineSlice | NineSlice |  | 未匹配 |
| Sharpcraft.Client.Ui.NineSliceCell | NineSliceCell |  | 未匹配 |
| Sharpcraft.Client.Ui.PlayerListPanel | PlayerListPanel |  | 未匹配 |
| Sharpcraft.Client.Ui.PlayerListRow | PlayerListRow |  | 未匹配 |
| Sharpcraft.Client.Ui.ScrollBar | ScrollBar |  | 未匹配 |
| Sharpcraft.Client.Ui.ScrollState | ScrollState |  | 未匹配 |
| Sharpcraft.Client.Ui.TextEditModel | TextEditModel |  | 未匹配 |
| Sharpcraft.Client.Ui.TextEditRenderer | TextEditRenderer |  | 未匹配 |
| Sharpcraft.Client.Ui.Title | Title |  | 未匹配 |
| Sharpcraft.Client.Ui.Tooltip | Tooltip | ref://mc-26.3-analysis/src/net/minecraft/client/gui/components/Tooltip.java | resolved |
| Sharpcraft.Client.Ui.UiDocumentFile | UiDocumentFile |  | 未匹配 |
| Sharpcraft.Client.Ui.CompiledScreen | CompiledScreen |  | 未匹配 |
| Sharpcraft.Client.Ui.UiDocumentLoader | UiDocumentLoader |  | 未匹配 |
| Sharpcraft.Client.Ui.UiMarkup | UiMarkup |  | 未匹配 |
| Sharpcraft.Client.Ui.UiNode | UiNode |  | 未匹配 |
| Sharpcraft.Client.Ui.UiColours | UiColours |  | 未匹配 |
| Sharpcraft.Client.Ui.UiScreenLayout | UiScreenLayout |  | 未匹配 |
| Sharpcraft.Client.Ui.UiStyle | UiStyle |  | 未匹配 |
| Sharpcraft.Client.Ui.UiStyles | UiStyles |  | 未匹配 |
| Sharpcraft.Client.World.ChunkWorkerPool | ChunkWorkerPool |  | 未匹配 |
| Sharpcraft.Client.World.ClientWorld | ClientWorld |  | 未匹配 |
| Sharpcraft.Client.World.EntityAnimationTickContext | EntityAnimationTickContext |  | 未匹配 |
| Sharpcraft.Client.World.IEntityAnimationState | IEntityAnimationState |  | 未匹配 |
| Sharpcraft.Client.World.IEntityArmPoseAnimationState | IEntityArmPoseAnimationState |  | 未匹配 |
| Sharpcraft.Client.World.IEntityEventAnimationState | IEntityEventAnimationState |  | 未匹配 |
| Sharpcraft.Client.World.IEntityPoseAnimationState | IEntityPoseAnimationState |  | 未匹配 |
| Sharpcraft.Client.World.IEntitySyncedDataAnimationState | IEntitySyncedDataAnimationState |  | 未匹配 |
| Sharpcraft.Client.World.IEntityTickAnimationState | IEntityTickAnimationState |  | 未匹配 |
| Sharpcraft.Client.World.EntityEquipment | EntityEquipment | ref://mc-26.3-analysis/src/net/minecraft/world/entity/EntityEquipment.java | resolved |
| Sharpcraft.Client.World.EntityRenderState | EntityRenderState | ref://mc-26.3-analysis/src/net/minecraft/client/renderer/entity/state/EntityRenderState.java | resolved |
| Sharpcraft.Client.World.TrackedEntity | TrackedEntity |  | 未匹配 |
| Sharpcraft.Client.World.EntityVariantSpec | EntityVariantSpec |  | 未匹配 |
| Sharpcraft.Client.World.EntityVariantSpecs | EntityVariantSpecs |  | 未匹配 |
| Sharpcraft.Client.World.EntityWorldAdapter | EntityWorldAdapter |  | 未匹配 |
| Sharpcraft.Client.World.FlapAnimationState | FlapAnimationState |  | 未匹配 |
| Sharpcraft.Client.World.PandaPoseAnimationState | PandaPoseAnimationState |  | 未匹配 |
| Sharpcraft.Client.World.PlayerListEntry | PlayerListEntry |  | 未匹配 |
| Sharpcraft.Client.World.PlayerListRoster | PlayerListRoster |  | 未匹配 |
| Sharpcraft.Client.World.PolarBearStandAnimationState | PolarBearStandAnimationState |  | 未匹配 |
| Sharpcraft.Client.World.RabbitHopAnimationState | RabbitHopAnimationState |  | 未匹配 |
| Sharpcraft.Client.World.RabbitIdleAnimationState | RabbitIdleAnimationState |  | 未匹配 |
| Sharpcraft.Client.World.SceneFingerprint | SceneFingerprint |  | 未匹配 |
| Sharpcraft.Client.World.SceneRecorder | SceneRecorder |  | 未匹配 |
| Sharpcraft.Client.World.SquidTentacleAnimationState | SquidTentacleAnimationState |  | 未匹配 |
| Sharpcraft.Client.World.TrackedAttributeMap | TrackedAttributeMap |  | 未匹配 |
| Sharpcraft.Client.World.WalkAnimationState | WalkAnimationState | ref://mc-26.3-analysis/src/net/minecraft/world/entity/WalkAnimationState.java | resolved |
| Sharpcraft.Client.World.WitherHeadAim | WitherHeadAim |  | 未匹配 |
| Sharpcraft.Client.World.WitherHeadAnimationState | WitherHeadAnimationState |  | 未匹配 |
| Sharpcraft.Common.Coordinates.BlockCoordinates | BlockCoordinates |  | 未匹配 |
| Sharpcraft.Common.Diagnostics.HotPathAttribute | HotPathAttribute |  | 未匹配 |
| Sharpcraft.Common.Identifiers.Identifier | Identifier | ref://mc-26.3-analysis/src/net/minecraft/resources/Identifier.java | resolved |
| Sharpcraft.Common.Identifiers.IdentifierException | IdentifierException | ref://mc-26.3-analysis/src/net/minecraft/IdentifierException.java | resolved |
| Sharpcraft.Common.Imaging.PngDecoder | PngDecoder |  | 未匹配 |
| Sharpcraft.Common.Imaging.RgbaImage | RgbaImage |  | 未匹配 |
| Sharpcraft.Common.Imaging.Crc32 | Crc32 |  | 未匹配 |
| Sharpcraft.Common.Imaging.PngEncoder | PngEncoder |  | 未匹配 |
| Sharpcraft.Common.Lifecycle.OfflineIdentity | OfflineIdentity |  | 未匹配 |
| Sharpcraft.Common.Lifecycle.SessionFile | SessionFile |  | 未匹配 |
| Sharpcraft.Common.Localization.LanguageTable | LanguageTable |  | 未匹配 |
| Sharpcraft.Common.Localization.LocaleSelector | LocaleSelector |  | 未匹配 |
| Sharpcraft.Common.Nbt.NbtReader | NbtReader |  | 未匹配 |
| Sharpcraft.Common.Nbt.NbtTagType | NbtTagType |  | 未匹配 |
| Sharpcraft.Common.Nbt.NbtByte | NbtByte |  | 未匹配 |
| Sharpcraft.Common.Nbt.NbtByteArray | NbtByteArray |  | 未匹配 |
| Sharpcraft.Common.Nbt.NbtCompound | NbtCompound |  | 未匹配 |
| Sharpcraft.Common.Nbt.NbtDouble | NbtDouble |  | 未匹配 |
| Sharpcraft.Common.Nbt.NbtFloat | NbtFloat |  | 未匹配 |
| Sharpcraft.Common.Nbt.NbtInt | NbtInt |  | 未匹配 |
| Sharpcraft.Common.Nbt.NbtIntArray | NbtIntArray |  | 未匹配 |
| Sharpcraft.Common.Nbt.NbtList | NbtList |  | 未匹配 |
| Sharpcraft.Common.Nbt.NbtLong | NbtLong |  | 未匹配 |
| Sharpcraft.Common.Nbt.NbtLongArray | NbtLongArray |  | 未匹配 |
| Sharpcraft.Common.Nbt.NbtShort | NbtShort |  | 未匹配 |
| Sharpcraft.Common.Nbt.NbtString | NbtString |  | 未匹配 |
| Sharpcraft.Common.Nbt.NbtTag | NbtTag |  | 未匹配 |
| Sharpcraft.Common.Nbt.NbtWriter | NbtWriter |  | 未匹配 |
| Sharpcraft.Common.Network.AesCfb8 | AesCfb8 |  | 未匹配 |
| Sharpcraft.Common.Network.BlockEntityPackets | BlockEntityPackets |  | 未匹配 |
| Sharpcraft.Common.Network.BlockEntityUpdate | BlockEntityUpdate |  | 未匹配 |
| Sharpcraft.Common.Network.BlockPositionCodec | BlockPositionCodec |  | 未匹配 |
| Sharpcraft.Common.Network.BlockChange | BlockChange |  | 未匹配 |
| Sharpcraft.Common.Network.BlockUpdatePackets | BlockUpdatePackets |  | 未匹配 |
| Sharpcraft.Common.Network.ChatClick | ChatClick |  | 未匹配 |
| Sharpcraft.Common.Network.ChatClickAction | ChatClickAction |  | 未匹配 |
| Sharpcraft.Common.Network.ChatInteractions | ChatInteractions |  | 未匹配 |
| Sharpcraft.Common.Network.ChatPackets | ChatPackets |  | 未匹配 |
| Sharpcraft.Common.Network.ComponentText | ComponentText |  | 未匹配 |
| Sharpcraft.Common.Network.ChatTypeDecoration | ChatTypeDecoration | ref://mc-26.3-analysis/src/net/minecraft/network/chat/ChatTypeDecoration.java | resolved |
| Sharpcraft.Common.Network.ChatTypeTable | ChatTypeTable |  | 未匹配 |
| Sharpcraft.Common.Network.ChunkPacketParser | ChunkPacketParser |  | 未匹配 |
| Sharpcraft.Common.Network.ParsedChunk | ParsedChunk |  | 未匹配 |
| Sharpcraft.Common.Network.ComponentRuns | ComponentRuns |  | 未匹配 |
| Sharpcraft.Common.Network.StyledRuns | StyledRuns |  | 未匹配 |
| Sharpcraft.Common.Network.TextRun | TextRun |  | 未匹配 |
| Sharpcraft.Common.Network.ChatTypeBinding | ChatTypeBinding |  | 未匹配 |
| Sharpcraft.Common.Network.DeleteChatPackets | DeleteChatPackets |  | 未匹配 |
| Sharpcraft.Common.Network.DisguisedChatPackets | DisguisedChatPackets |  | 未匹配 |
| Sharpcraft.Common.Network.DimensionTypeInfo | DimensionTypeInfo |  | 未匹配 |
| Sharpcraft.Common.Network.DynamicRegistries | DynamicRegistries |  | 未匹配 |
| Sharpcraft.Common.Network.Entry | Entry |  | 未匹配 |
| Sharpcraft.Common.Network.RegistryDataPackets | RegistryDataPackets |  | 未匹配 |
| Sharpcraft.Common.Network.EntityDataItem | EntityDataItem |  | 未匹配 |
| Sharpcraft.Common.Network.EntityDataPackets | EntityDataPackets |  | 未匹配 |
| Sharpcraft.Common.Network.EntityEventPackets | EntityEventPackets |  | 未匹配 |
| Sharpcraft.Common.Network.EntityLinkPackets | EntityLinkPackets |  | 未匹配 |
| Sharpcraft.Common.Network.EntityMotion | EntityMotion |  | 未匹配 |
| Sharpcraft.Common.Network.GameEventPacket | GameEventPacket |  | 未匹配 |
| Sharpcraft.Common.Network.GameEventPacketCodec | GameEventPacketCodec |  | 未匹配 |
| Sharpcraft.Common.Network.HandshakePackets | HandshakePackets |  | 未匹配 |
| Sharpcraft.Common.Network.ItemComponentInfo | ItemComponentInfo |  | 未匹配 |
| Sharpcraft.Common.Network.LevelEventPackets | LevelEventPackets |  | 未匹配 |
| Sharpcraft.Common.Network.LightData | LightData |  | 未匹配 |
| Sharpcraft.Common.Network.LightDataCodec | LightDataCodec |  | 未匹配 |
| Sharpcraft.Common.Network.EncryptionRequest | EncryptionRequest |  | 未匹配 |
| Sharpcraft.Common.Network.LoginPackets | LoginPackets |  | 未匹配 |
| Sharpcraft.Common.Network.MinecraftConnection | MinecraftConnection |  | 未匹配 |
| Sharpcraft.Common.Network.LoginOutcome | LoginOutcome |  | 未匹配 |
| Sharpcraft.Common.Network.MinecraftLoginClient | MinecraftLoginClient |  | 未匹配 |
| Sharpcraft.Common.Network.MinecraftLoginCrypto | MinecraftLoginCrypto |  | 未匹配 |
| Sharpcraft.Common.Network.PacketCompression | PacketCompression |  | 未匹配 |
| Sharpcraft.Common.Network.PacketFrameBuffer | PacketFrameBuffer |  | 未匹配 |
| Sharpcraft.Common.Network.PacketFramer | PacketFramer |  | 未匹配 |
| Sharpcraft.Common.Network.PalettedContainerCodec | PalettedContainerCodec |  | 未匹配 |
| Sharpcraft.Common.Network.PassengerPackets | PassengerPackets |  | 未匹配 |
| Sharpcraft.Common.Network.PayloadWriter | PayloadWriter |  | 未匹配 |
| Sharpcraft.Common.Network.PlayMovementPackets | PlayMovementPackets |  | 未匹配 |
| Sharpcraft.Common.Network.JoinGameInfo | JoinGameInfo |  | 未匹配 |
| Sharpcraft.Common.Network.PlayPackets | PlayPackets |  | 未匹配 |
| Sharpcraft.Common.Network.PlaySession | PlaySession |  | 未匹配 |
| Sharpcraft.Common.Network.PlaySessionStats | PlaySessionStats |  | 未匹配 |
| Sharpcraft.Common.Network.PlayerChatPackets | PlayerChatPackets |  | 未匹配 |
| Sharpcraft.Common.Network.PlayerInfoUpdate | PlayerInfoUpdate |  | 未匹配 |
| Sharpcraft.Common.Network.PlayerListActions | PlayerListActions |  | 未匹配 |
| Sharpcraft.Common.Network.PlayerListPackets | PlayerListPackets |  | 未匹配 |
| Sharpcraft.Common.Network.PlayerPositionInfo | PlayerPositionInfo |  | 未匹配 |
| Sharpcraft.Common.Network.PlayerRotationInfo | PlayerRotationInfo |  | 未匹配 |
| Sharpcraft.Common.Network.MovePacketKind | MovePacketKind |  | 未匹配 |
| Sharpcraft.Common.Network.PlayerSync | PlayerSync |  | 未匹配 |
| Sharpcraft.Common.Network.ServerPositionOutcome | ServerPositionOutcome |  | 未匹配 |
| Sharpcraft.Common.Network.ConfigurationClientbound | ConfigurationClientbound |  | 未匹配 |
| Sharpcraft.Common.Network.ConfigurationServerbound | ConfigurationServerbound |  | 未匹配 |
| Sharpcraft.Common.Network.ConnectionState | ConnectionState |  | 未匹配 |
| Sharpcraft.Common.Network.HandshakeServerbound | HandshakeServerbound |  | 未匹配 |
| Sharpcraft.Common.Network.LoginClientbound | LoginClientbound |  | 未匹配 |
| Sharpcraft.Common.Network.LoginServerbound | LoginServerbound |  | 未匹配 |
| Sharpcraft.Common.Network.PacketIds | PacketIds |  | 未匹配 |
| Sharpcraft.Common.Network.PlayClientbound | PlayClientbound |  | 未匹配 |
| Sharpcraft.Common.Network.PlayServerbound | PlayServerbound |  | 未匹配 |
| Sharpcraft.Common.Network.ProtocolConstants | ProtocolConstants |  | 未匹配 |
| Sharpcraft.Common.Network.StatusClientbound | StatusClientbound |  | 未匹配 |
| Sharpcraft.Common.Network.StatusServerbound | StatusServerbound |  | 未匹配 |
| Sharpcraft.Common.Network.ResolvedServerAddress | ResolvedServerAddress | ref://mc-26.3-analysis/src/net/minecraft/client/multiplayer/resolver/ResolvedServerAddress.java | resolved |
| Sharpcraft.Common.Network.ServerAddressResolver | ServerAddressResolver | ref://mc-26.3-analysis/src/net/minecraft/client/multiplayer/resolver/ServerAddressResolver.java | resolved |
| Sharpcraft.Common.Network.EquipmentItem | EquipmentItem |  | 未匹配 |
| Sharpcraft.Common.Network.SetEquipmentPackets | SetEquipmentPackets |  | 未匹配 |
| Sharpcraft.Common.Network.ClockNetworkState | ClockNetworkState | ref://mc-26.3-analysis/src/net/minecraft/world/clock/ClockNetworkState.java | resolved |
| Sharpcraft.Common.Network.SetTimePacket | SetTimePacket |  | 未匹配 |
| Sharpcraft.Common.Network.SetTimePacketCodec | SetTimePacketCodec |  | 未匹配 |
| Sharpcraft.Common.Network.StatusPingClient | StatusPingClient |  | 未匹配 |
| Sharpcraft.Common.Network.StatusPingResult | StatusPingResult |  | 未匹配 |
| Sharpcraft.Common.Network.SwingAnimationPackets | SwingAnimationPackets |  | 未匹配 |
| Sharpcraft.Common.Network.AttributeValue | AttributeValue |  | 未匹配 |
| Sharpcraft.Common.Network.UpdateAttributesPackets | UpdateAttributesPackets |  | 未匹配 |
| Sharpcraft.Common.Network.Utf8StringCodec | Utf8StringCodec |  | 未匹配 |
| Sharpcraft.Common.Network.DecodeStatus | DecodeStatus |  | 未匹配 |
| Sharpcraft.Common.Network.VarInt | VarInt | ref://mc-26.3-analysis/src/net/minecraft/network/VarInt.java | resolved |
| Sharpcraft.Common.Network.VarLong | VarLong | ref://mc-26.3-analysis/src/net/minecraft/network/VarLong.java | resolved |
| Sharpcraft.Common.Random.IPositionalRandomFactory | IPositionalRandomFactory |  | 未匹配 |
| Sharpcraft.Common.Random.IRandomSource | IRandomSource |  | 未匹配 |
| Sharpcraft.Common.Random.JavaStringHash | JavaStringHash |  | 未匹配 |
| Sharpcraft.Common.Random.LegacyPositionalRandomFactory | LegacyPositionalRandomFactory |  | 未匹配 |
| Sharpcraft.Common.Random.LegacyRandomSource | LegacyRandomSource | ref://mc-26.3-analysis/src/net/minecraft/world/level/levelgen/LegacyRandomSource.java | resolved |
| Sharpcraft.Common.Random.MarsagliaPolarGaussian | MarsagliaPolarGaussian | ref://mc-26.3-analysis/src/net/minecraft/world/level/levelgen/MarsagliaPolarGaussian.java | resolved |
| Sharpcraft.Common.Random.PositionalSeed | PositionalSeed |  | 未匹配 |
| Sharpcraft.Common.Random.RandomSequence | RandomSequence | ref://mc-26.3-analysis/src/net/minecraft/world/RandomSequence.java | resolved |
| Sharpcraft.Common.Random.RandomSource | RandomSource | ref://mc-26.3-analysis/src/net/minecraft/util/RandomSource.java | resolved |
| Sharpcraft.Common.Random.SeedSupport | SeedSupport |  | 未匹配 |
| Sharpcraft.Common.Random.Algorithm | Algorithm |  | 未匹配 |
| Sharpcraft.Common.Random.WorldgenRandom | WorldgenRandom | ref://mc-26.3-analysis/src/net/minecraft/world/level/levelgen/WorldgenRandom.java | resolved |
| Sharpcraft.Common.Random.Xoroshiro128PlusPlus | Xoroshiro128PlusPlus | ref://mc-26.3-analysis/src/net/minecraft/world/level/levelgen/Xoroshiro128PlusPlus.java | resolved |
| Sharpcraft.Common.Random.XoroshiroPositionalRandomFactory | XoroshiroPositionalRandomFactory |  | 未匹配 |
| Sharpcraft.Common.Random.XoroshiroRandomSource | XoroshiroRandomSource | ref://mc-26.3-analysis/src/net/minecraft/world/level/levelgen/XoroshiroRandomSource.java | resolved |
| Sharpcraft.Common.Reference.ReferenceClassAttribute | ReferenceClassAttribute |  | 未匹配 |
| Sharpcraft.Common.Reference.ReferenceKind | ReferenceKind |  | 未匹配 |
| Sharpcraft.Common.Reference.ReferenceStatus | ReferenceStatus |  | 未匹配 |
| Sharpcraft.Common.Registries.AttributeTypes | AttributeTypes | ref://mc-26.3-analysis/src/net/minecraft/world/attribute/AttributeTypes.java | resolved |
| Sharpcraft.Common.Registries.BlockStateDefinition | BlockStateDefinition |  | 未匹配 |
| Sharpcraft.Common.Registries.BlockStateLayout | BlockStateLayout |  | 未匹配 |
| Sharpcraft.Common.Registries.Entry | Entry |  | 未匹配 |
| Sharpcraft.Common.Registries.BlockStateProperty | BlockStateProperty |  | 未匹配 |
| Sharpcraft.Common.Registries.DataComponentTypes | DataComponentTypes |  | 未匹配 |
| Sharpcraft.Common.Registries.EnchantmentTypes | EnchantmentTypes |  | 未匹配 |
| Sharpcraft.Common.Registries.EntityDimensionsInfo | EntityDimensionsInfo |  | 未匹配 |
| Sharpcraft.Common.Registries.EntityDimensionsRegistry | EntityDimensionsRegistry |  | 未匹配 |
| Sharpcraft.Common.Registries.IdMapper | IdMapper | ref://mc-26.3-analysis/src/net/minecraft/core/IdMapper.java | resolved |
| Sharpcraft.Common.Registries.ItemInfo | ItemInfo |  | 未匹配 |
| Sharpcraft.Common.Registries.ItemRegistry | ItemRegistry |  | 未匹配 |
| Sharpcraft.Common.Registries.ItemTagRegistry | ItemTagRegistry |  | 未匹配 |
| Sharpcraft.Common.Registries.Entry | Entry |  | 未匹配 |
| Sharpcraft.Common.Registries.Registry | Registry | ref://mc-26.3-analysis/src/net/minecraft/core/Registry.java | resolved |
| Sharpcraft.Common.Registries.VanillaBlockRegistry | VanillaBlockRegistry |  | 未匹配 |
| Sharpcraft.Common.World.BiomeClimate | BiomeClimate |  | 未匹配 |
| Sharpcraft.Common.World.BiomeEffects | BiomeEffects |  | 未匹配 |
| Sharpcraft.Common.World.BiomeEntry | BiomeEntry |  | 未匹配 |
| Sharpcraft.Common.World.BiomeRegistry | BiomeRegistry |  | 未匹配 |
| Sharpcraft.Common.World.GrassColourModifier | GrassColourModifier |  | 未匹配 |
| Sharpcraft.Common.World.BlockEntityRecord | BlockEntityRecord |  | 未匹配 |
| Sharpcraft.Common.World.BlockTint | BlockTint |  | 未匹配 |
| Sharpcraft.Common.World.ChunkLight | ChunkLight |  | 未匹配 |
| Sharpcraft.Common.World.ChunkSection | ChunkSection |  | 未匹配 |
| Sharpcraft.Common.World.ColorMapColorUtil | ColorMapColorUtil | ref://mc-26.3-analysis/src/net/minecraft/world/level/ColorMapColorUtil.java | resolved |
| Sharpcraft.Common.World.ColourBlend | ColourBlend |  | 未匹配 |
| Sharpcraft.Common.World.FluidKind | FluidKind |  | 未匹配 |
| Sharpcraft.Common.World.FluidState | FluidState | ref://mc-26.3-analysis/src/net/minecraft/world/level/material/FluidState.java | resolved |
| Sharpcraft.Common.World.FluidStateCodec | FluidStateCodec |  | 未匹配 |
| Sharpcraft.Common.World.GaussianWeights | GaussianWeights |  | 未匹配 |
| Sharpcraft.Common.World.LevelChunk | LevelChunk | ref://mc-26.3-analysis/src/net/minecraft/world/level/chunk/LevelChunk.java | resolved |
| Sharpcraft.Common.World.PalettedContainer | PalettedContainer | ref://mc-26.3-analysis/src/net/minecraft/world/level/chunk/PalettedContainer.java | resolved |
| Sharpcraft.Common.World.SkyColour | SkyColour |  | 未匹配 |
| Sharpcraft.Common.World.SkyColourSource | SkyColourSource |  | 未匹配 |
| Sharpcraft.Common.World.CubicBezierEasing | CubicBezierEasing |  | 未匹配 |
| Sharpcraft.Common.World.FloatKeyframe | FloatKeyframe |  | 未匹配 |
| Sharpcraft.Common.World.FloatKeyframeTrack | FloatKeyframeTrack |  | 未匹配 |
| Sharpcraft.Common.World.SkyAttributeTracks | SkyAttributeTracks |  | 未匹配 |
| Sharpcraft.Common.World.SkyTimelines | SkyTimelines |  | 未匹配 |
| Sharpcraft.Common.World.WeatherBlend | WeatherBlend |  | 未匹配 |
| Sharpcraft.Framework.Platform.GuiCanvas | GuiCanvas |  | 未匹配 |
| Sharpcraft.Framework.Platform.GuiScale | GuiScale |  | 未匹配 |
| Sharpcraft.Framework.Platform.InputKey | InputKey |  | 未匹配 |
| Sharpcraft.Framework.Platform.TimerResolution | TimerResolution |  | 未匹配 |
| Sharpcraft.Framework.Platform.NativeSurfaceKind | NativeSurfaceKind |  | 未匹配 |
| Sharpcraft.Framework.Platform.NativeSurfaceSource | NativeSurfaceSource |  | 未匹配 |
| Sharpcraft.Framework.Platform.WindowHost | WindowHost |  | 未匹配 |
| Sharpcraft.Framework.Platform.WindowInputState | WindowInputState |  | 未匹配 |
| Sharpcraft.Framework.Rendering.AtlasTextureSource | AtlasTextureSource |  | 未匹配 |
| Sharpcraft.Framework.Rendering.BackendCapabilities | BackendCapabilities |  | 未匹配 |
| Sharpcraft.Framework.Rendering.GraphicsCapability | GraphicsCapability |  | 未匹配 |
| Sharpcraft.Framework.Rendering.CameraUniform | CameraUniform |  | 未匹配 |
| Sharpcraft.Framework.Rendering.ChunkCuller | ChunkCuller |  | 未匹配 |
| Sharpcraft.Framework.Rendering.ChunkGeometryStore | ChunkGeometryStore |  | 未匹配 |
| Sharpcraft.Framework.Rendering.ClipSpaceTransform | ClipSpaceTransform |  | 未匹配 |
| Sharpcraft.Framework.Rendering.DepthLinearisation | DepthLinearisation |  | 未匹配 |
| Sharpcraft.Framework.Rendering.DepthPyramid | DepthPyramid |  | 未匹配 |
| Sharpcraft.Framework.Rendering.DynamicGeometry | DynamicGeometry |  | 未匹配 |
| Sharpcraft.Framework.Rendering.EntityGeometryLayer | EntityGeometryLayer |  | 未匹配 |
| Sharpcraft.Framework.Rendering.EntityGeometryLayerKind | EntityGeometryLayerKind |  | 未匹配 |
| Sharpcraft.Framework.Rendering.FrameDiagnostics | FrameDiagnostics |  | 未匹配 |
| Sharpcraft.Framework.Rendering.CameraMath | CameraMath |  | 未匹配 |
| Sharpcraft.Framework.Rendering.CelestialColorBlend | CelestialColorBlend |  | 未匹配 |
| Sharpcraft.Framework.Rendering.CelestialColorDraw | CelestialColorDraw |  | 未匹配 |
| Sharpcraft.Framework.Rendering.CelestialQuadDraw | CelestialQuadDraw |  | 未匹配 |
| Sharpcraft.Framework.Rendering.FrameRequest | FrameRequest |  | 未匹配 |
| Sharpcraft.Framework.Rendering.FrameResult | FrameResult |  | 未匹配 |
| Sharpcraft.Framework.Rendering.LightmapUniform | LightmapUniform |  | 未匹配 |
| Sharpcraft.Framework.Rendering.TextureData | TextureData |  | 未匹配 |
| Sharpcraft.Framework.Rendering.FrustumPlanes | FrustumPlanes |  | 未匹配 |
| Sharpcraft.Framework.Rendering.GeometryData | GeometryData |  | 未匹配 |
| Sharpcraft.Framework.Rendering.LightmapUniformCodec | LightmapUniformCodec |  | 未匹配 |
| Sharpcraft.Framework.Rendering.OffscreenRenderer | OffscreenRenderer |  | 未匹配 |
| Sharpcraft.Framework.Rendering.RenderingUnavailableException | RenderingUnavailableException |  | 未匹配 |
| Sharpcraft.Framework.Rendering.OitWavelet | OitWavelet |  | 未匹配 |
| Sharpcraft.Framework.Rendering.PixelComparison | PixelComparison |  | 未匹配 |
| Sharpcraft.Framework.Rendering.PixelDifference | PixelDifference |  | 未匹配 |
| Sharpcraft.Framework.Rendering.ShaderPacks.PackDiagnostic | PackDiagnostic |  | 未匹配 |
| Sharpcraft.Framework.Rendering.ShaderPacks.PackDiagnosticKind | PackDiagnosticKind |  | 未匹配 |
| Sharpcraft.Framework.Rendering.ShaderPacks.PackValidation | PackValidation |  | 未匹配 |
| Sharpcraft.Framework.Rendering.ShaderPacks.KnownUniforms | KnownUniforms |  | 未匹配 |
| Sharpcraft.Framework.Rendering.ShaderPacks.PhaseDescription | PhaseDescription |  | 未匹配 |
| Sharpcraft.Framework.Rendering.ShaderPacks.RenderTargetDescription | RenderTargetDescription |  | 未匹配 |
| Sharpcraft.Framework.Rendering.ShaderPacks.ShaderPackDescription | ShaderPackDescription |  | 未匹配 |
| Sharpcraft.Framework.Rendering.ShaderPacks.ShaderPackValidator | ShaderPackValidator |  | 未匹配 |
| Sharpcraft.Framework.Rendering.ShaderPacks.Absolute | Absolute | ref://mc-26.3-analysis/src/net/minecraft/world/level/storage/loot/providers/number/floats/Absolute.java<br>ref://mc-26.3-analysis/src/net/minecraft/world/level/storage/loot/providers/number/ints/Absolute.java | ambiguous |
| Sharpcraft.Framework.Rendering.ShaderPacks.Relative | Relative | ref://mc-26.3-analysis/src/net/minecraft/world/entity/Relative.java | resolved |
| Sharpcraft.Framework.Rendering.ShaderPacks.TargetSize | TargetSize |  | 未匹配 |
| Sharpcraft.Framework.Rendering.Shaders.ShaderBindings | ShaderBindings |  | 未匹配 |
| Sharpcraft.Framework.Rendering.Shaders.ShaderDescriptor | ShaderDescriptor |  | 未匹配 |
| Sharpcraft.Framework.Rendering.Shaders.ShaderIncludeComposer | ShaderIncludeComposer |  | 未匹配 |
| Sharpcraft.Framework.Rendering.Shaders.ShaderLibrary | ShaderLibrary |  | 未匹配 |
| Sharpcraft.Framework.Rendering.Shaders.ShaderPipelines | ShaderPipelines |  | 未匹配 |
| Sharpcraft.Framework.Rendering.UiFrame | UiFrame |  | 未匹配 |
| Sharpcraft.Framework.Rendering.Range | Range |  | 未匹配 |
| Sharpcraft.Framework.Rendering.UiGeometry | UiGeometry |  | 未匹配 |
| Sharpcraft.Framework.Rendering.UiMaterial | UiMaterial |  | 未匹配 |
| Sharpcraft.Framework.Rendering.UiProjection | UiProjection |  | 未匹配 |
| Sharpcraft.Framework.Rendering.UiRenderer | UiRenderer |  | 未匹配 |
| Sharpcraft.Framework.Rendering.UiTextureSource | UiTextureSource |  | 未匹配 |
| Sharpcraft.Framework.Rendering.VertexLayout | VertexLayout |  | 未匹配 |
| Sharpcraft.Framework.Rendering.WindowedRenderer | WindowedRenderer |  | 未匹配 |
| Sharpcraft.Framework.Rendering.WorldTextDraw | WorldTextDraw |  | 未匹配 |
| Sharpcraft.Launcher.Accounts.AccountKind | AccountKind |  | 未匹配 |
| Sharpcraft.Launcher.Accounts.AccountRecord | AccountRecord |  | 未匹配 |
| Sharpcraft.Launcher.Accounts.GateResult | GateResult |  | 未匹配 |
| Sharpcraft.Launcher.Accounts.OfflineLoginGate | OfflineLoginGate |  | 未匹配 |
| Sharpcraft.Launcher.Accounts.AccountStore | AccountStore |  | 未匹配 |
| Sharpcraft.Launcher.Accounts.AccountsCommand | AccountsCommand |  | 未匹配 |
| Sharpcraft.Launcher.Assets.AssetPackBuilder | AssetPackBuilder |  | 未匹配 |
| Sharpcraft.Launcher.Assets.AssetPackResult | AssetPackResult |  | 未匹配 |
| Sharpcraft.Launcher.Assets.AssetSelection | AssetSelection |  | 未匹配 |
| Sharpcraft.Launcher.Assets.HostPaths | HostPaths |  | 未匹配 |
| Sharpcraft.Launcher.Assets.JarVerificationException | JarVerificationException |  | 未匹配 |
| Sharpcraft.Launcher.Assets.JarVerifier | JarVerifier |  | 未匹配 |
| Sharpcraft.Launcher.Assets.VersionManifest | VersionManifest |  | 未匹配 |
| Sharpcraft.Launcher.Diagnostics.HostDebugOptions | HostDebugOptions |  | 未匹配 |
| Sharpcraft.Launcher.Diagnostics.SpeedOverlay | SpeedOverlay |  | 未匹配 |
| Sharpcraft.Launcher.Launch.LaunchCommand | LaunchCommand |  | 未匹配 |
| Sharpcraft.Launcher.Launch.LaunchContext | LaunchContext |  | 未匹配 |
| Sharpcraft.Launcher.Launch.LaunchPlan | LaunchPlan |  | 未匹配 |
| Sharpcraft.Launcher.Launch.LaunchPlanBuilder | LaunchPlanBuilder |  | 未匹配 |
| Sharpcraft.Launcher.LauncherCommand | LauncherCommand |  | 未匹配 |
| Sharpcraft.Launcher.LauncherCommandLine | LauncherCommandLine |  | 未匹配 |
| Sharpcraft.Launcher.Transport.HostTransport | HostTransport |  | 未匹配 |
| Sharpcraft.Modding.IModContext | IModContext |  | 未匹配 |
| Sharpcraft.Modding.ISharpcraftMod | ISharpcraftMod |  | 未匹配 |
| Sharpcraft.Modding.ModHandle | ModHandle |  | 未匹配 |
| Sharpcraft.Modding.ModHost | ModHost |  | 未匹配 |
| Sharpcraft.Modding.ModLoadContext | ModLoadContext |  | 未匹配 |
