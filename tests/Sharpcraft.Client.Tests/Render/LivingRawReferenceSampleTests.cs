// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Globalization;
using System.Numerics;
using Sharpcraft.Client.Assets;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Sharpcraft.Framework.Rendering;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Source-equivalence evidence for living-entity raw source-frame models. Three independent checks:
///
/// 1) <see cref="RawBakeMatchesSourceVertices"/>: raw bake + mesher model-space output is compared
///    STRICTLY, keyed by the sample part/cube/polygon/vertex columns, against the ModelProbe source
///    positions AND UVs (jar e877b6a0). No sorting/rounding/set-containment. Per-part isolation works
///    around vanilla's hash-map child order. This is the UV/position source oracle.
/// 2) <see cref="RawModelFinalWorldMatchesLegacyTwin"/>: the real Creeper/Spider/Wither models are
///    meshed through the actual living-entity geometry path (EntityModelMesher.BuildInto with a model
///    override and no block factory) in BOTH raw and legacy modes from the same DSL, and their FULL
///    final world-vertex position multisets are compared strictly. This pins the frame flip+baseline
///    on real geometry, not hand-written points. UV equivalence is NOT claimed here: raw uses source
///    face UVs (verified by check 1), legacy uses the legacy face calibration (a documented deviation).
/// 3) CaveSpider four-facing regression: real raw geometry at Scale=0.7 vs a source-sample-derived
///    live world frame (spider source sample × 0.7 + 24.016*(1-0.7) px root offset + living flip).
///
/// Baby: creeper/spider/wither have no distinct baby model - registered in docs/risks and render.md.
/// Blocker R-027: mirrored parts' min/max-X swap + polygon reversal is not reproduced by raw; those
/// part groups are explicitly excluded from check 1's source comparison, not silently.
/// </summary>
public sealed class LivingRawReferenceSampleTests
{
    private sealed record Sample(string Part, int Cube, int Polygon, int Vertex, float X, float Y, float Z, float U, float V);

    // Hard-coded expected counts from the jar probe samples (independent of the sample-loaded values):
    // total sample rows, rows belonging to genuinely-mirrored parts (Mirror=true), and rows that must be
    // strictly checked. Creeper/Wither have no mirrored parts; Spider has four mirrored left legs (4*24=96).
    public static TheoryData<string, EntityModelDefinition, int, int, string, int, int, int, int> Families => new()
    {
        { "creeper-raw-vectors.reference.txt", CreeperModelDefinition.Definition, 64, 32, "entity/creeper/creeper", 144, 0, 144, 0 },
        { "spider-raw-vectors.reference.txt", SpiderModelDefinition.Definition, 64, 32, "entity/spider/spider", 264, 96, 168, 0 },
        { "wither-raw-vectors.reference.txt", WitherBossModelDefinition.Definition, 64, 64, "entity/wither/wither", 216, 0, 216, 0 },
        { "pig-raw-vectors.reference.txt", PigModelDefinition.Definition, 64, 64, "entity/pig/pig_temperate", 168, 48, 120, 0 },
        { "cow-raw-vectors.reference.txt", CowModelDefinition.Definition, 64, 64, "entity/cow/cow_temperate", 240, 48, 192, 0 },
        { "chicken-raw-vectors.reference.txt", ChickenModelDefinition.Definition, 64, 32, "entity/chicken/chicken_temperate", 192, 24, 168, 0 },
        { "pig-baby-raw-vectors.reference.txt", BabyModelDefinitions.Pig, 32, 32, "entity/pig/pig_temperate_baby", 168, 0, 168, 0 },
        { "cow-baby-raw-vectors.reference.txt", BabyModelDefinitions.Cow, 64, 64, "entity/cow/cow_temperate_baby", 216, 24, 192, 0 },
        // Chicken-baby left/right legs + feet + wings are zero-area cubes (depth=0 or height=0).
        // Task-145 makes the raw mesher emit the source ModelPart.Cube six-face/24-vertex set for
        // this family, so all 192 source rows are strictly checked. Other families' emissive/fully
        // hidden groups remain explicitly excluded (R-029/#126), not silently passed.
        { "chicken-baby-raw-vectors.reference.txt", BabyModelDefinitions.Chicken, 16, 16, "entity/chicken/chicken_temperate_baby", 192, 0, 192, 0 },
        // task-62 aquatic/flying batch. Counts = (total, mirror, checked, degenerate) from jar probe.
        { "cod-raw-vectors.reference.txt", CodModelDefinition.Definition, 32, 32, "entity/fish/cod", 168, 0, 168, 0 },
        { "salmon-raw-vectors.reference.txt", SalmonModelDefinition.Definition, 32, 32, "entity/fish/salmon", 192, 0, 72, 120 },
        { "tropical-fish-small-raw-vectors.reference.txt", TropicalFishModelDefinition.Definition, 32, 32, "entity/fish/tropical_a", 120, 0, 120, 0 },
        { "tropical-fish-large-raw-vectors.reference.txt", TropicalFishModelDefinition.Variants["flopper"], 32, 32, "entity/fish/tropical_b", 144, 0, 144, 0 },
        { "pufferfish-small-raw-vectors.reference.txt", PufferfishModelDefinition.Definition, 32, 32, "entity/fish/pufferfish", 144, 0, 144, 0 },
        { "pufferfish-medium-raw-vectors.reference.txt", PufferfishModelDefinition.Variants["medium"], 32, 32, "entity/fish/pufferfish", 264, 0, 264, 0 },
        { "pufferfish-large-raw-vectors.reference.txt", PufferfishModelDefinition.Variants["large"], 32, 32, "entity/fish/pufferfish", 312, 0, 312, 0 },
        { "tadpole-raw-vectors.reference.txt", TadpoleModelDefinition.Definition, 16, 16, "entity/tadpole/tadpole", 48, 0, 48, 0 },
        { "squid-raw-vectors.reference.txt", SquidModelDefinition.Definition, 64, 32, "entity/squid/squid", 216, 0, 216, 0 },
        { "glow-squid-raw-vectors.reference.txt", SquidModelDefinition.GlowSquid, 64, 32, "entity/squid/glow_squid", 216, 0, 216, 0 },
        { "dolphin-raw-vectors.reference.txt", DolphinModelDefinition.Definition, 64, 64, "entity/dolphin/dolphin", 192, 24, 168, 0 },
        { "axolotl-raw-vectors.reference.txt", AxolotlModelDefinition.Definition, 64, 64, "entity/axolotl/axolotl", 264, 0, 48, 216 },
        { "axolotl-baby-raw-vectors.reference.txt", BabyModelDefinitionsAquatic.Axolotl, 32, 32, "entity/axolotl/axolotl_baby", 264, 0, 48, 216 },
        { "turtle-raw-vectors.reference.txt", TurtleModelDefinition.Definition, 128, 64, "entity/turtle/turtle", 192, 0, 168, 24 },
        { "turtle-baby-raw-vectors.reference.txt", BabyModelDefinitionsMisc.Turtle, 16, 16, "entity/turtle/turtle_baby", 144, 0, 48, 96 },
        { "frog-raw-vectors.reference.txt", FrogModelDefinition.Definition, 48, 48, "entity/frog/frog_temperate", 384, 0, 216, 168 },
        { "bat-raw-vectors.reference.txt", BatModelDefinition.Definition, 32, 32, "entity/bat/bat", 216, 0, 48, 168 },
        { "bee-raw-vectors.reference.txt", BeeModelDefinition.Definition, 64, 64, "entity/bee/bee", 216, 24, 192, 0 },
        { "bee-baby-raw-vectors.reference.txt", BabyModelDefinitionsAquatic.Bee, 32, 32, "entity/bee/bee_baby", 216, 24, 192, 0 },
        { "parrot-raw-vectors.reference.txt", ParrotModelDefinition.Definition, 32, 32, "entity/parrot/parrot", 264, 0, 264, 0 },
        { "allay-raw-vectors.reference.txt", AllayModelDefinition.Definition, 32, 32, "entity/allay/allay", 168, 0, 168, 0 },
        // task-81: CopperGolem entity raw, nested part paths resolved via full parent chain.
        // Counts = (total, mirror, checked, degenerate) from jar probe: 216 rows, no mirrors/degenerate.
        { "copper-golem-raw-vectors.reference.txt", CopperGolemModelDefinition.Definition, 64, 64, "entity/copper_golem/copper_golem", 216, 0, 216, 0 },
        // task-94 special/large first group (total, mirror, checked, degenerate).
        { "shulker-raw-vectors.reference.txt", ShulkerModelDefinition.Definition, 64, 64, "entity/shulker/shulker", 72, 0, 72, 0 },
        { "blaze-raw-vectors.reference.txt", BlazeModelDefinition.Definition, 64, 32, "entity/blaze/blaze", 312, 0, 312, 0 },
        // task-109: Breeze raw (multi-cube head/eyes; sample covers base cutout parts).
        { "breeze-raw-vectors.reference.txt", BreezeModelDefinition.Definition, 32, 32, "entity/breeze/breeze", 168, 0, 120, 48 },
        // task-109: Creaking raw (multi-cube head/body/legs; 384 rows incl. zero-depth plates).
        { "creaking-raw-vectors.reference.txt", CreakingModelDefinition.Definition, 64, 64, "entity/creaking/creaking", 384, 0, 288, 96 },
        { "endermite-raw-vectors.reference.txt", EndermiteModelDefinition.Definition, 64, 32, "entity/endermite/endermite", 96, 0, 96, 0 },
        // task-109: Ghast raw (Scale=4.5 via #141 layer-scale offset).
        { "ghast-raw-vectors.reference.txt", GhastModelDefinition.Definition, 64, 32, "entity/ghast/ghast", 240, 0, 240, 0 },
        // task-104: HappyGhast (Scale=4) raw, enabled by the layer-scale root offset (#141).
        { "happy-ghast-raw-vectors.reference.txt", HappyGhastModelDefinition.Definition, 64, 64, "entity/ghast/happy_ghast", 240, 0, 240, 0 },
        // task-138 Giant raw: reuses raw Zombie geometry + Scale=6 root offset (#141).
        { "giant-raw-vectors.reference.txt", GiantModelDefinition.Definition, 64, 64, "entity/zombie/zombie", 168, 48, 120, 0 },
        // task-109: Guardian raw (multi-cube head; Mirror excluded via real flag).
        { "guardian-raw-vectors.reference.txt", GuardianModelDefinition.Definition, 64, 64, "entity/guardian/guardian", 528, 24, 504, 0 },
        // task-109: IronGolem raw (multi-cube head/body; Mirror excluded via real flag).
        { "iron-golem-raw-vectors.reference.txt", IronGolemModelDefinition.Definition, 128, 128, "entity/iron_golem/iron_golem", 192, 48, 144, 0 },
        // task-95: illager hat as independent cube + layer scale 0.9375 (4 families share one mesh).
        { "evoker-raw-vectors.reference.txt", IllagerModelDefinition.Evoker, 64, 64, "entity/illager/evoker", 288, 72, 216, 0 },
        { "vindicator-raw-vectors.reference.txt", IllagerModelDefinition.Vindicator, 64, 64, "entity/illager/vindicator", 288, 72, 216, 0 },
        { "pillager-raw-vectors.reference.txt", IllagerModelDefinition.Pillager, 64, 64, "entity/illager/pillager", 288, 72, 216, 0 },
        { "illusioner-raw-vectors.reference.txt", IllagerModelDefinition.Illusioner, 64, 64, "entity/illager/illusioner", 288, 72, 216, 0 },
        // task-95: villager + wandering trader (hat/hat_rim already independent; raw + scale 0.9375).
        { "villager-raw-vectors.reference.txt", VillagerModelDefinition.Definition, 64, 64, "entity/villager/villager", 264, 48, 216, 0 },
        { "wandering-trader-raw-vectors.reference.txt", VillagerModelDefinition.WanderingTrader, 64, 64, "entity/wandering_trader/wandering_trader", 264, 48, 216, 0 },
        { "player-raw-vectors.reference.txt", PlayerModelDefinition.Definition, 64, 64, "entity/player/steve", 288, 0, 288, 0 },
        { "player-slim-raw-vectors.reference.txt", PlayerModelDefinition.SlimDefinition, 64, 64, "entity/player/alex", 288, 0, 288, 0 },
        // task-151 Warden/Phantom (total, mirror, checked, degenerate).
        { "warden-raw-vectors.reference.txt", WardenModelDefinition.Definition, 128, 128, "entity/warden/warden", 240, 0, 144, 96 },
        { "phantom-raw-vectors.reference.txt", PhantomModelDefinition.Definition, 64, 64, "entity/phantom/phantom", 192, 48, 144, 0 },
        // task-159 small special (total, mirror, checked, degenerate).
        { "silverfish-raw-vectors.reference.txt", SilverfishModelDefinition.Definition, 64, 32, "entity/silverfish/silverfish", 240, 0, 240, 0 },
        { "slime-raw-vectors.reference.txt", SlimeModelDefinition.Definition, 64, 32, "entity/slime/slime", 96, 0, 96, 0 },
        { "magma-cube-raw-vectors.reference.txt", MagmaCubeModelDefinition.Definition, 64, 64, "entity/slime/magmacube", 216, 0, 216, 0 },
        // task-166 Vex/Nautilus (total, mirror, checked, degenerate).
        { "vex-raw-vectors.reference.txt", VexModelDefinition.Definition, 32, 32, "entity/illager/vex", 168, 0, 120, 48 },
        // shell_back/body_back are zero-depth flat parts (source), mesher culls their faces => degenerate, explicitly counted.
        { "nautilus-raw-vectors.reference.txt", NautilusModelDefinition.Definition, 128, 128, "entity/nautilus/nautilus", 192, 0, 144, 48 },
        { "zombie-nautilus-raw-vectors.reference.txt", ZombieNautilusModelDefinition.Definition, 128, 128, "entity/nautilus/zombie_nautilus", 192, 0, 144, 48 },
        // task-121 humanoid subset (total, mirror, checked, degenerate).
        { "skeleton-raw-vectors.reference.txt", HumanoidMobModelDefinition.Skeleton, 64, 32, "entity/skeleton/skeleton", 168, 48, 120, 0 },
        { "stray-raw-vectors.reference.txt", HumanoidMobModelDefinition.Stray, 64, 32, "entity/skeleton/stray", 168, 48, 120, 0 },
        { "wither-skeleton-raw-vectors.reference.txt", HumanoidMobModelDefinition.WitherSkeleton, 64, 32, "entity/skeleton/wither_skeleton", 168, 48, 120, 0 },
        // task-178 Bogged raw: head mushrooms are six 6x4x0 zero-depth cubes; source emits 24 each.
        { "bogged-raw-vectors.reference.txt", HumanoidMobModelDefinition.Bogged, 64, 32, "entity/skeleton/bogged", 312, 48, 264, 0 },
        { "husk-raw-vectors.reference.txt", HumanoidMobModelDefinition.Husk, 64, 64, "entity/zombie/husk", 168, 48, 120, 0 },
        { "drowned-raw-vectors.reference.txt", HumanoidMobModelDefinition.Drowned, 64, 64, "entity/zombie/drowned", 168, 0, 168, 0 },
        { "enderman-raw-vectors.reference.txt", HumanoidMobModelDefinition.Enderman, 64, 32, "entity/enderman/enderman", 168, 48, 120, 0 },
        { "zombie-raw-vectors.reference.txt", HumanoidMobModelDefinition.Zombie, 64, 64, "entity/zombie/zombie", 168, 48, 120, 0 },
        // task-108 humanoid batch (total, mirror, checked, degenerate).
        // task-173: ZombieVillager raw (multi-cube head/nose, hat_rim, body outer).
        { "zombie-villager-raw-vectors.reference.txt", ZombieVillagerModelDefinition.Definition, 64, 64, "entity/zombie_villager/zombie_villager", 240, 48, 192, 0 },
        // task-165: Strider raw (6 zero-height bristles degenerate, 3 right-bristle Mirror).
        { "strider-raw-vectors.reference.txt", StriderModelDefinition.Definition, 64, 128, "entity/strider/strider", 216, 0, 72, 144 },
        // task-165: SnowGolem raw (source right-arm zRot -1, grow -0.5).
        { "snow-golem-raw-vectors.reference.txt", SnowGolemModelDefinition.Definition, 64, 64, "entity/snow_golem/snow_golem", 120, 0, 120, 0 },
        // task-165: Sniffer raw (multi-cube body/head, zero-height flat layers degenerate).
        { "sniffer-raw-vectors.reference.txt", SnifferModelDefinition.Definition, 192, 192, "entity/sniffer/sniffer", 360, 0, 312, 48 },
        // task-158: baby_armadillo raw (distinct BabyArmadilloModel geometry, mirror legs, zero-depth ears).
        { "baby-armadillo-raw-vectors.reference.txt", BabyModelDefinitionsOdd.Armadillo, 64, 64, "entity/armadillo/armadillo_baby", 264, 48, 168, 48 },
        // task-158: baby_hoglin raw (distinct BabyHoglinModel geometry, head xRot +0.8727, left-ear Mirror).
        { "baby-hoglin-raw-vectors.reference.txt", BabyModelDefinitionsNether.Hoglin, 64, 64, "entity/hoglin/hoglin_baby", 264, 24, 216, 24 },
        // task-158: baby_llama raw (distinct BabyLlamaModel geometry, chest yRot +PI/2).
        { "baby-llama-raw-vectors.reference.txt", BabyModelDefinitionsEquine.Llama, 64, 64, "entity/llama/llama_brown_baby", 264, 0, 216, 48 },
        // task-150: Armadillo raw (multi-cube body/head, rolled cube).
        { "armadillo-raw-vectors.reference.txt", ArmadilloModelDefinition.Definition, 64, 64, "entity/armadillo/armadillo", 264, 0, 216, 48 },
        // task-150: Ravager raw (multi-cube head, source Mirror on right_horn + left legs, body +PI/2).
        { "ravager-raw-vectors.reference.txt", RavagerModelDefinition.Definition, 128, 128, "entity/illager/ravager", 288, 72, 216, 0 },
        // task-150: Hoglin raw (source head xRot +0.8727, zero-width mane degenerate).
        { "hoglin-raw-vectors.reference.txt", HoglinModelDefinition.Definition, 128, 64, "entity/hoglin/hoglin", 264, 0, 240, 24 },
        // task-150: Llama + TraderLlama raw (shared geometry, multi-cube head, source rotations).
        { "llama-raw-vectors.reference.txt", LlamaModelDefinition.Definition, 128, 64, "entity/llama/llama_brown", 264, 0, 216, 48 },
        { "trader-llama-raw-vectors.reference.txt", TraderLlamaModelDefinition.Definition, 128, 64, "entity/llama/llama_brown", 264, 0, 216, 48 },
        // task-143: Camel raw (multi-cube head, zero-size group marker degenerate).
        { "camel-raw-vectors.reference.txt", CamelModelDefinition.Definition, 128, 128, "entity/camel/camel", 288, 0, 264, 24 },
        // task-143: Goat raw (multi-cube head/body, source nose xRot +0.9599, left-ear Mirror).
        { "goat-raw-vectors.reference.txt", GoatModelDefinition.Definition, 64, 64, "entity/goat/goat", 288, 24, 240, 24 },
        // task-143: Rabbit raw (nested pivots, source body xRot -0.3927).
        { "rabbit-raw-vectors.reference.txt", RabbitModelDefinition.Definition, 64, 64, "entity/rabbit/rabbit_brown", 216, 0, 216, 0 },
        // task-143: Panda raw (multi-cube head, source body +PI/2).
        { "panda-raw-vectors.reference.txt", PandaModelDefinition.Definition, 64, 64, "entity/panda/panda", 216, 0, 216, 0 },
        // task-137: Horse raw (scale 1.1 #141, source rotations, mirror legs).
        { "horse-raw-vectors.reference.txt", HorseModelDefinition.Definition, 64, 64, "entity/horse/horse_brown", 288, 48, 240, 0 },
        // task-137: Cat raw (scale 0.8 #141, multi-cube head, source rotations).
        { "cat-raw-vectors.reference.txt", CatModelDefinition.Definition, 64, 32, "entity/cat/cat_tabby", 264, 0, 264, 0 },
        // task-137: Wolf raw (multi-cube head, source rotations, right-leg Mirror).
        { "wolf-raw-vectors.reference.txt", WolfModelDefinition.Definition, 64, 32, "entity/wolf/wolf", 264, 48, 216, 0 },
        // task-137: Fox raw (source body xRot +PI/2; tail child of body).
        { "fox-raw-vectors.reference.txt", FoxModelDefinition.Definition, 48, 32, "entity/fox/fox", 240, 0, 240, 0 },
        // task-135: Sheep raw (source Mirror per createLegs; #156 face order).
        { "sheep-raw-vectors.reference.txt", SheepModelDefinition.Definition, 64, 32, "entity/sheep/sheep", 144, 48, 96, 0 },
        // task-135: PolarBear raw (scale 1.2, multi-cube head/body, #141 offset).
        { "polar-bear-raw-vectors.reference.txt", PolarBearModelDefinition.Definition, 128, 64, "entity/bear/polarbear", 240, 24, 216, 0 },
        // player BLOCKED (Player legacy to avoid Piglin parts-reuse coupling, task-108)
        // player-slim BLOCKED (Player legacy to avoid Piglin parts-reuse coupling, task-108)
        // task-108 blockers (multi-cube head / zero-depth mushroom / overlay parts DSL gaps).
        // bogged BLOCKED (head mushrooms) / witch BLOCKED / zombie-villager BLOCKED /
        // piglin, piglin-brute, zombified-piglin BLOCKED (ears + clothing overlays).
    };

    /// <summary>
    /// Guards against isolated-definition false greens: the production definitions migrated in the
    /// task-94 special batch must themselves carry the raw source-frame flags. The sample test below
    /// rebuilds an isolated definition for one part at a time, so a production definition missing these
    /// flags would still pass the model-space samples while the real living entity renders the legacy way.
    /// </summary>
    [Fact]
    public void Task94MigratedProductionDefinitionsCarryRawSourceFrameFlags()
    {
        // Guards isolated-definition false greens: the model-space sample test rebuilds a one-part
        // definition with raw flags, so a production definition missing them would still pass samples
        // while the real living entity renders the legacy way (the Blaze regression this fixes).
        // HappyGhast is Scale != 1 and stays legacy pending the layer-scale root-offset spec (#141).
        var migrated = new (string Name, EntityModelDefinition Def)[]
        {
            ("shulker", ShulkerModelDefinition.Definition),
            ("blaze", BlazeModelDefinition.Definition),
            ("endermite", EndermiteModelDefinition.Definition)
        };
        foreach (var (name, def) in migrated)
        {
            Assert.True(def.RawSourceFrame, $"{name}: production definition must be rawSourceFrame");
            Assert.Equal(0f, def.BaselinePx);
            Assert.Equal(24f, def.SourceFrameBaselinePx);
            Assert.Null(def.FaceTransforms is { Count: > 0 } ? def.FaceTransforms : null);
        }
    }

    [Theory]
    [MemberData(nameof(Families))]
    public void RawBakeMatchesSourceVertices(string sampleFile, EntityModelDefinition definition, int width, int height,
        string texturePath, int expectedTotal, int expectedMirror, int expectedChecked, int expectedDegenerate)
    {
        var rows = LoadSample(sampleFile);
        var atlas = MakeAtlas(texturePath, width, height);

        // Verify the raw sample file itself against hard-coded expectations, before any comparison.
        Assert.Equal(expectedTotal, rows.Count);

        var checkedVertices = 0;
        var mirrorVertices = 0;
        var degenerateVertices = 0;
        var expectedNonMirrorVertices = 0;
        foreach (var group in rows.GroupBy(r => (r.Part, r.Cube)))
        {
            var (part, cube) = group.Key;
            // Source ModelParts can own several cubes, while our DSL flattens each cube to its own part
            // (e.g. source /head has head+snout+horns). Map the sample (part,cube) to our flat part name.
            // Mirror exclusion is determined by the REAL baked part's Mirror flag (R-027), never by name.
            EntityModelPart[] sourceParts = FlatPartsForSampleCube(definition, part, cube);
            Assert.NotEmpty(sourceParts);
            // task-174: zero-area fish/bee groups are source-emitted (ModelPart.Cube always emits 6 faces);
            // include them in strict comparison instead of culling by dimensions.
            // Salmon is excluded: its source right_fin has texOffs -4, whose out-of-range UVs are emitted
            // differently by the mesh/probe path; kept as an explicit R-029/#126 blocker rather than fitted.
            var includeRawDegenerate = sampleFile.StartsWith("chicken-baby-", StringComparison.Ordinal)
                || sampleFile.StartsWith("cod-", StringComparison.Ordinal)
                || sampleFile.StartsWith("tropical-fish-", StringComparison.Ordinal)
                || sampleFile.StartsWith("pufferfish-", StringComparison.Ordinal)
                || sampleFile.StartsWith("tadpole-", StringComparison.Ordinal)
                || sampleFile.StartsWith("bee-", StringComparison.Ordinal)
                || sampleFile.StartsWith("bogged-", StringComparison.Ordinal)
                || sampleFile.StartsWith("parrot-", StringComparison.Ordinal)
                || sampleFile.StartsWith("allay-", StringComparison.Ordinal);
            if ((!includeRawDegenerate && (sourceParts[0].Width == 0f || sourceParts[0].Height == 0f || sourceParts[0].Depth == 0f))
                || EmittedVertCount(sourceParts[0], width, height, atlas, texturePath) == 0)
            {
                // Degenerate/hidden cube: mesher culls the faces the source probe emits. Pre-existing
                // behavior, registered as blocker (R-029/#126). Explicitly excluded, not silently passed.
                // Chicken-baby is the task-145 acceptance family: its flat legs/feet/wings now emit the
                // source 24 vertices and are checked strictly below.
                degenerateVertices += group.Count();
                continue;
            }
            if (sourceParts[0].Mirror)
            {
                mirrorVertices += group.Count();
                continue; // R-027 mirror blocker (actual Mirror flag, not name heuristic)
            }
            expectedNonMirrorVertices += group.Count();

            Assert.NotEmpty(sourceParts);
            // Include ancestor parts so child pivots (e.g. baby pig snout is a child of head) compose.
            // The target part is emitted first in definition.Parts order; ancestors follow.
            var targetName = sourceParts[0].Name;
            var sourceNames = sourceParts.Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
            var partOrder = definition.Parts.Where(p => sourceNames.Contains(p.Name) || IsAncestorOf(p, targetName, definition)).ToArray();
            // The jar sample is bind-pose geometry with ALL parts emitted; strip runtime visibility
            // predicates (e.g. illager crossed-vs-open arms are mutually exclusive at runtime) so the
            // geometry test compares the stored cubes, not a particular animation state.
            partOrder = partOrder.Select(p => p with { VisibleWhen = null }).ToArray();
            var single = new EntityModelDefinition(definition.Name, width, height, partOrder,
                texturePath: texturePath, scale: definition.Scale, baselinePx: 0f, rawSourceFrame: true,
                sourceFrameBaselinePx: definition.SourceFrameBaselinePx);
            var geometry = BuildModelSpace(single, MakeLivingState(180f), width, height, atlas);
            var source = group.OrderBy(r => r.Polygon).ThenBy(r => r.Vertex).ToArray();
            // The target's 24 vertices are emitted after all preceding parts in partOrder; each part
            // emits a variable count (0 for fully-zero/hidden parts, 8 for single-plane parts, 24 for
            // full cubes), so sum actual emitted counts rather than assuming 24 each.
            var vertexOffset = 0;
            foreach (var before in partOrder)
            {
                if (before.Name == targetName) break;
                vertexOffset += EmittedVertCount(before, width, height, atlas, texturePath);
            }
            // For the flattened Wither ribcage, all four cubes were returned; map cube index to its part.
            if (part == "/ribcage") vertexOffset = cube * 24;
            Assert.Equal(24, source.Length);
            Assert.True(geometry.VertexFloats / ChunkMesher.FloatsPerVertex >= vertexOffset + 24,
                $"part={part} cube={cube} target={targetName} vertexOffset={vertexOffset} partOrder=[{string.Join(",", partOrder.Select(p => p.Name))}] geoVerts={geometry.VertexFloats / ChunkMesher.FloatsPerVertex}");
            for (var i = 0; i < source.Length; i++)
            {
                var s = source[i];
                var p = VertexFieldReader.Position(geometry.Vertices, vertexOffset + i);
                var uv = VertexFieldReader.Uv(geometry.Vertices, vertexOffset + i);
                // #128: samples are in normalized render units; production built with modelScale:1.
                // Tight 1e-5 tolerance (Book standard) for both position and UV. The sample UV is
                // quantized to half-float first, matching what the geometry buffer actually stores.
                Assert.True(MathF.Abs(s.X - p.X) < 1e-5f && MathF.Abs(s.Y - p.Y) < 1e-5f && MathF.Abs(s.Z - p.Z) < 1e-5f,
                    $"part={part} cube={cube} poly={s.Polygon} v={s.Vertex}: src=({s.X},{s.Y},{s.Z}) our=({p.X},{p.Y},{p.Z})");
                var su = QuantizeHalf(s.U);
                var sv = QuantizeHalf(s.V);
                Assert.True(MathF.Abs(su - uv.U) < 1e-5f && MathF.Abs(sv - uv.V) < 1e-5f,
                    $"part={part} cube={cube} poly={s.Polygon} v={s.Vertex} UV: src=({su},{sv}) our=({uv.U},{uv.V})");
                checkedVertices++;
            }
        }
        // Counts pinned against hard-coded jar-probe expectations (no sample self-justification):
        //   total = mirror + degenerate + checked, each equals its explicit constant.
        Assert.Equal(expectedMirror, mirrorVertices);
        Assert.Equal(expectedDegenerate, degenerateVertices);
        Assert.Equal(expectedChecked, checkedVertices);
        Assert.Equal(expectedNonMirrorVertices, checkedVertices);
        Assert.Equal(expectedTotal, mirrorVertices + degenerateVertices + checkedVertices);
    }

    /// <summary>
    /// Real-model final-world equivalence. Both models come from the SAME declarative DSL mesh (read via
    /// reflection), one baked raw (current production definition), one baked legacy
    /// (rawSourceFrame:false, baselinePx:24). Both are meshed through the real living path and their
    /// complete world-vertex POSITION multisets compared strictly. UV is excluded here by design
    /// (legacy applies a face-UV calibration; raw uses source UVs verified in check 1).
    /// </summary>
    public static TheoryData<EntityModelDefinition, EntityModelDefinition, int, int, string, EntityRenderState> WorldCases => new()
    {
        { CreeperModelDefinition.Definition, LegacyEntityTwinFactory.Creeper(), 64, 32, "entity/creeper/creeper", new EntityRenderState(1, 32, 10f, 2f, -4f, 45f, 0f) },
        { SpiderModelDefinition.Definition, LegacyEntityTwinFactory.Spider(), 64, 32, "entity/spider/spider", new EntityRenderState(1, 127, -3f, 1f, 6f, 250f, 0f) },
        { WitherBossModelDefinition.Definition, LegacyEntityTwinFactory.Wither(), 64, 64, "entity/wither/wither", new EntityRenderState(1, 149, 0f, 5f, 0f, 90f, 0f) }
    };

    [Theory]
    [MemberData(nameof(WorldCases))]
    public void RawModelFinalWorldMatchesLegacyTwin(EntityModelDefinition rawDefinition, EntityModelDefinition legacyTwin, int width, int height, string texturePath, EntityRenderState state)
    {
        var atlas = MakeAtlas(texturePath, width, height);
        // Full-model final world comparison through the real living path. Raw and legacy emit faces in
        // different order (raw = source face order), so per-index equality is not expected; the strict
        // relation is a multiplicity-preserving bijection of world POSITION tuples. UVs are not compared
        // here because legacy carries a documented face-UV calibration while raw uses source UVs (which
        // are verified strictly against the jar probe in RawBakeMatchesSourceVertices).
        AssertFullModelWorldPositions(rawDefinition, legacyTwin, state, width, height, atlas);
    }

    private static void AssertFullModelWorldPositions(EntityModelDefinition raw, EntityModelDefinition legacy, EntityRenderState state,
        int width, int height, ItemAtlas atlas)
    {
        // Full-model strict world POSITION comparison through the real living path. Raw and legacy
        // emit faces/parts in different order, and rotated/mirrored parts can land in each other's
        // slots (R-027 mirror convention), so the strict relation is a multiplicity-preserving
        // bijection of ALL world position tuples - a sorted LIST at 1e-4, not a set and not bounds.
        // This detects any dropped/duplicated/moved vertex while absorbing only 1-ULP float noise.
        // Per-vertex UV source equivalence is proven separately, strictly per part/cube/polygon/vertex,
        // in RawBakeMatchesSourceVertices against the jar probe (legacy UV is a documented deviation).
        var rawGeo = BuildLivingWorld(raw, state, width, height, atlas);
        var legacyGeo = BuildLivingWorld(legacy, state, width, height, atlas);
        Assert.Equal(legacyGeo.VertexFloats / ChunkMesher.FloatsPerVertex,
                     rawGeo.VertexFloats / ChunkMesher.FloatsPerVertex);
        Assert.Equal(WorldPositions(legacyGeo), WorldPositions(rawGeo));
    }

    /// <summary>
    /// World positions for every emitted vertex as a sorted list keyed at 1e-4 (4 decimals). Sorting a
    /// LIST (not a set) preserves multiplicity, so a duplicated/dropped/reordered vertex still fails;
    /// the 1e-4 key only absorbs the 1-ULP float difference between the two mathematically-equal bakes.
    /// </summary>
    private static (int, int, int)[] WorldPositions(DynamicGeometry geometry)
    {
        var count = geometry.VertexFloats / ChunkMesher.FloatsPerVertex;
        var result = new (int, int, int)[count];
        for (var i = 0; i < count; i++)
        {
            var p = VertexFieldReader.Position(geometry.Vertices, i);
            result[i] = ((int)MathF.Round(p.X * 10000f), (int)MathF.Round(p.Y * 10000f), (int)MathF.Round(p.Z * 10000f));
        }
        Array.Sort(result);
        return result;
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(90f)]
    [InlineData(180f)]
    [InlineData(270f)]
    public void CaveSpiderLayerScaleRootFrameMatchesSpiderSource(float yaw)
    {
        // CaveSpider reuses the raw spider geometry at Scale=0.7. Its world frame is source-driven:
        // the spider model-space sample is scaled by 0.7, translated by 24.016*(1-0.7) px, then the
        // living flip/baseline is applied. This replaces the old legacy-twin oracle, which kept the
        // missing layer-scale root offset (R-034) and was therefore wrong for scaled raw models.
        var raw = CaveSpiderModelDefinition.Definition;
        Assert.True(raw.RawSourceFrame);
        Assert.Equal(0f, raw.BaselinePx);
        Assert.Null(raw.FaceTransforms is { Count: > 0 } ? raw.FaceTransforms : null);

        var source = LoadSample("spider-raw-vectors.reference.txt").First();
        var scaled = new Vector3(
            -source.X * 0.7f,
            24f / 16f - (24.016f * (1f - 0.7f) / 16f + source.Y * 0.7f),
            source.Z * 0.7f);
        var expected = Vector3.Transform(
            scaled,
            Matrix4x4.CreateRotationY((180f - yaw) * (MathF.PI / 180f)));

        var state = new EntityRenderState(1, CaveSpiderModelDefinition.CaveSpiderEntityTypeId, 0f, 0f, 0f, yaw, 0f);
        var atlas = MakeAtlas("entity/spider/cave_spider", 64, 32);
        var geometry = BuildLivingWorld(raw, state, 64, 32, atlas, modelScale: 1f);
        var actual = VertexFieldReader.Position(geometry.Vertices, 0);
        Assert.True(
            MathF.Abs(expected.X - actual.X) < 1e-5f
                && MathF.Abs(expected.Y - actual.Y) < 1e-5f
                && MathF.Abs(expected.Z - actual.Z) < 1e-5f,
            $"yaw={yaw}: expected=({expected.X:F6},{expected.Y:F6},{expected.Z:F6})"
                + $" actual=({actual.X:F6},{actual.Y:F6},{actual.Z:F6})");
    }

    /// <summary>
    /// Creeper/spider/wither have no distinct baby model definitions (baby mammals only). This is an
    /// explicit N/A registration; the mammal families' baby variants are covered by their own migrations.
    /// </summary>
    [Fact]
    public void BabyVariantsAreNotApplicableToTheseFamilies()
    {
        foreach (var typeId in new[] { CreeperModelDefinition.CreeperEntityTypeId, SpiderModelDefinition.SpiderEntityTypeId, WitherBossModelDefinition.WitherEntityTypeId })
        {
            var baby = EntityModelResolver.TryResolve(typeId, baby: true);
            Assert.True(baby is null || !baby.Name.StartsWith("baby_", StringComparison.Ordinal),
                $"type {typeId} unexpectedly has a distinct baby model");
        }
    }

    /// <summary>
    /// Maps a source sample (part path, cube index) to our DSL's flattened part records. Several source
    /// ModelParts own multiple cubes that our DSL declares as separate parts; the mapping reflects the
    /// CubeListBuilder.addBox order in the 26.3 source (PigModel/CowModel/ChickenModel/WitherBossModel).
    /// </summary>
    private static EntityModelPart[] FlatPartsForSampleCube(EntityModelDefinition definition, string part, int cube)
    {
        string flatName;
        switch (part)
        {
            // Baby variants: cube order follows the reference Baby*Model CubeListBuilder.addBox order.
            case "/head" when definition.Name.StartsWith("baby_pig", StringComparison.Ordinal):
                flatName = cube == 0 ? "head" : "snout"; break;
            case "/head" when definition.Name.StartsWith("baby_cow", StringComparison.Ordinal):
                flatName = cube switch { 0 => "head", 1 => "ear_right", 2 => "ear_left", _ => "snout" }; break;
            case "/body" when definition.Name.StartsWith("baby_chicken", StringComparison.Ordinal):
                flatName = cube == 0 ? "body" : "beak"; break;
            case "/left_leg" when definition.Name.StartsWith("baby_chicken", StringComparison.Ordinal):
                flatName = cube == 0 ? "left_leg" : "left_foot"; break;
            case "/right_leg" when definition.Name.StartsWith("baby_chicken", StringComparison.Ordinal):
                flatName = cube == 0 ? "right_leg" : "right_foot"; break;
            case "/head" when definition.Name.StartsWith("pig", StringComparison.Ordinal):
                flatName = cube == 0 ? "head" : "snout"; break;
            case "/head" when definition.Name.StartsWith("cow", StringComparison.Ordinal) || definition.Name.StartsWith("mooshroom", StringComparison.Ordinal):
                flatName = cube switch { 0 => "head", 1 => "snout", 2 => "right_horn", _ => "left_horn" }; break;
            case "/body" when definition.Name.StartsWith("cow", StringComparison.Ordinal) || definition.Name.StartsWith("mooshroom", StringComparison.Ordinal):
                flatName = cube == 0 ? "body" : "udder"; break;
            // task-62 multi-cube source parts flattened into separate DSL parts (source addBox order).
            case "/body" when definition.Name.StartsWith("turtle", StringComparison.Ordinal):
                flatName = cube == 0 ? "body" : "belly"; break;
            case "/body" when definition.Name.StartsWith("axolotl", StringComparison.Ordinal):
                flatName = cube == 0 ? "body" : "body_layer"; break;
            case "/root/body" when definition.Name.StartsWith("baby_axolotl", StringComparison.Ordinal):
                flatName = cube == 0 ? "body" : "body_fin"; break;
            case "/root/body" when definition.Name.StartsWith("axolotl", StringComparison.Ordinal):
                flatName = cube == 0 ? "body" : "body_layer"; break;
            case "/root/body" when definition.Name.StartsWith("frog", StringComparison.Ordinal):
                flatName = cube == 0 ? "body" : "body_layer"; break;
            case "/root/body/head" when definition.Name.StartsWith("frog", StringComparison.Ordinal):
                // Source CubeListBuilder order: head_layer (height 0) first, then head (height 3).
                flatName = cube == 0 ? "head_layer" : "head"; break;
            case "/root/body" when definition.Name.StartsWith("allay", StringComparison.Ordinal):
                flatName = cube == 0 ? "body" : "body_inner"; break;
            // task-166: nautilus shell (3 cubes) and body (2 cubes), Vex body (2 cubes) split into flat parts.
            case "/root/shell" when definition.Name is "nautilus" or "zombie_nautilus":
                flatName = cube switch { 0 => "shell", 1 => "shell_mid", _ => "shell_back" }; break;
            case "/root/body" when definition.Name is "nautilus" or "zombie_nautilus":
                flatName = cube == 0 ? "body" : "body_back"; break;
            case "/root/body" when definition.Name == "vex":
                flatName = cube == 0 ? "body" : "body_inner"; break;
            case "/bone" when definition.Name.StartsWith("baby_bee", StringComparison.Ordinal):
                flatName = cube == 0 ? "bone" : "antenna_left"; break;
            // task-81: CopperGolem source head is one ModelPart with 4 cubes (head/nose/antenna/
            // antenna_tip), flattened into separate DSL parts in addBox order.
            case "/body/head" when definition.Name.StartsWith("copper_golem", StringComparison.Ordinal):
                flatName = cube switch { 0 => "head", 1 => "nose", 2 => "antenna", _ => "antenna_tip" }; break;
            // task-95: illager multi-cube source parts. Source addBox order: /body -> body, body_outer;
            // /arms -> arms_right, arms_cross. Hat is now an independent child part named "hat".
            case "/body" when definition.Name is "evoker" or "vindicator" or "pillager" or "illusioner":
                flatName = cube == 0 ? "body" : "body_outer"; break;
            case "/arms" when definition.Name is "evoker" or "vindicator" or "pillager" or "illusioner":
                flatName = cube == 0 ? "arms_right" : "arms_cross"; break;
            case "/arms/left_shoulder" when definition.Name is "evoker" or "vindicator" or "pillager" or "illusioner":
                flatName = "arms_left"; break;
            // task-95: villager source 'arms' part has 3 cubes in addBox order: left, right(mirror), middle.
            case "/arms" when definition.Name is "villager" or "wandering_trader":
                flatName = cube switch { 0 => "arms_left", 1 => "arms_right", _ => "arms_middle" }; break;
            // task-109: guardian source /head has 5 cubes -> DSL flattened head/head_left/head_right/head_top/head_bottom.
            case "/head" when definition.Name == "guardian":
                flatName = cube switch { 0 => "head", 1 => "head_left", 2 => "head_right", 3 => "head_top", _ => "head_bottom" }; break;
            case "/head/tail0/tail1/tail2" when definition.Name == "guardian":
                flatName = cube == 0 ? "tail2_body" : "tail2_fin"; break;
            // task-109: iron-golem source /head has 2 cubes (head,nose), /body has 2 (body,lower_body).
            case "/head" when definition.Name == "iron_golem":
                flatName = cube == 0 ? "head" : "nose"; break;
            case "/body" when definition.Name == "iron_golem":
                flatName = cube == 0 ? "body" : "lower_body"; break;
            // task-109: creaking multi-cube parts (source addBox order).
            case "/root/upper_body/head" when definition.Name == "creaking":
                flatName = cube switch { 0 => "head", 1 => "head_top", 2 => "head_plate_right", _ => "head_plate_left" }; break;
            case "/root/upper_body/body" when definition.Name == "creaking":
                flatName = cube == 0 ? "body" : "body_front"; break;
            case "/root/upper_body/right_arm" when definition.Name == "creaking":
                flatName = cube == 0 ? "right_arm" : "right_hand"; break;
            case "/root/upper_body/left_arm" when definition.Name == "creaking":
                flatName = cube switch { 0 => "left_arm", 1 => "left_upper", _ => "left_hand" }; break;
            case "/root/left_leg" when definition.Name == "creaking":
                flatName = cube == 0 ? "left_leg" : "left_foot"; break;
            case "/root/right_leg" when definition.Name == "creaking":
                flatName = cube switch { 0 => "right_leg", 1 => "right_foot", _ => "right_knee" }; break;
            // task-109: breeze source head/eyes have 2 cubes each (head,head_cube / eyes,eyes_cube).
            case "/body/head" when definition.Name == "breeze":
                flatName = cube == 0 ? "head" : "head_cube"; break;
            case "/body/head/eyes" when definition.Name == "breeze":
                flatName = cube == 0 ? "eyes" : "eyes_cube"; break;
            // task-173: zombie_villager source /head has 2 cubes (head,nose); /body has 2 (body,body_outer).
            case "/head" when definition.Name == "zombie_villager":
                flatName = cube == 0 ? "head" : "nose"; break;
            case "/body" when definition.Name == "zombie_villager":
                flatName = cube == 0 ? "body" : "body_outer"; break;
            // task-165: sniffer source /bone/body has 3 cubes (body,body_outer,body_bottom); /head 2 (head,head_flat).
            case "/bone/body" when definition.Name == "sniffer":
                flatName = cube switch { 0 => "body", 1 => "body_outer", _ => "body_bottom" }; break;
            case "/bone/body/head" when definition.Name == "sniffer":
                flatName = cube == 0 ? "head" : "head_flat"; break;
            // task-158: baby_armadillo source /body has 2 cubes (body grow0.3, body_inner); tail cube named right_ear_cube.
            case "/body" when definition.Name == "baby_armadillo":
                flatName = cube == 0 ? "body" : "body_inner"; break;
            case "/body/tail/right_ear_cube" when definition.Name == "baby_armadillo":
                flatName = "tail_cube"; break;
            // task-158: baby_hoglin source /head has 3 cubes (head,head_right,head_left), /body has 2 (body,body_mane).
            case "/head" when definition.Name == "baby_hoglin":
                flatName = cube switch { 0 => "head", 1 => "head_right", _ => "head_left" }; break;
            case "/body" when definition.Name == "baby_hoglin":
                flatName = cube == 0 ? "body" : "body_mane"; break;
            // task-158: baby_llama source /head has 4 cubes (head,snout,ear_right,ear_left).
            case "/head" when definition.Name == "baby_llama":
                flatName = cube switch { 0 => "head", 1 => "snout", 2 => "ear_right", _ => "ear_left" }; break;
            // task-150: armadillo source /body has 2 cubes (body_outer grow0.3, body grow0).
            case "/body" when definition.Name == "armadillo":
                flatName = cube == 0 ? "body_outer" : "body"; break;
            // task-150: ravager source /neck/head has 2 cubes (head,head_snout); /body has 2 (body,body_lower).
            case "/neck/head" when definition.Name == "ravager":
                flatName = cube == 0 ? "head" : "head_snout"; break;
            case "/body" when definition.Name == "ravager":
                flatName = cube == 0 ? "body" : "body_lower"; break;
            // task-150: llama source /head has 4 cubes (head,neck,left_ear,right_ear).
            case "/head" when definition.Name == "llama" || definition.Name.StartsWith("trader_llama", StringComparison.Ordinal):
                flatName = cube switch { 0 => "head", 1 => "neck", 2 => "left_ear", _ => "right_ear" }; break;
            // task-143: camel source /body/head has 3 cubes (head_lower,head_upper,head_snout).
            case "/body/head" when definition.Name == "camel":
                flatName = cube switch { 0 => "head_lower", 1 => "head_upper", _ => "head_snout" }; break;
            // task-143: goat source /head has 3 cubes (right_ear,left_ear,goatee), body 2 (body_lower,body_upper).
            case "/head" when definition.Name == "goat":
                flatName = cube switch { 0 => "right_ear", 1 => "left_ear", _ => "goatee" }; break;
            case "/body" when definition.Name == "goat":
                flatName = cube == 0 ? "body_lower" : "body_upper"; break;
            // task-143: panda source /head has 4 cubes (head,nose,left_ear,right_ear).
            case "/head" when definition.Name == "panda":
                flatName = cube switch { 0 => "head", 1 => "nose", 2 => "left_ear", _ => "right_ear" }; break;
            // task-137: cat source /head has 4 cubes (main,nose,ear1,ear2).
            case "/head" when definition.Name == "cat":
                flatName = cube switch { 0 => "head_main", 1 => "nose", 2 => "ear1", _ => "ear2" }; break;
            // task-137: wolf source /head/real_head has 4 cubes (real_head,right_ear,left_ear,snout).
            case "/head/real_head" when definition.Name == "wolf":
                flatName = cube switch { 0 => "real_head", 1 => "right_ear", 2 => "left_ear", _ => "snout" }; break;
            // task-135: polar_bear source head has 4 cubes (head,mouth,right_ear,left_ear), body 2 (body,body_upper).
            case "/head" when definition.Name == "polar_bear":
                flatName = cube switch { 0 => "head", 1 => "mouth", 2 => "right_ear", _ => "left_ear" }; break;
            case "/body" when definition.Name == "polar_bear":
                flatName = cube == 0 ? "body" : "body_upper"; break;
            case "/head/beak": flatName = "beak"; break;
            case "/head/red_thing": flatName = "red_thing"; break;
            case "/ribcage" when definition.Name.StartsWith("wither", StringComparison.Ordinal):
                // Wither ribcage is one source part with 4 cubes; meshed together below via the
                // multi-cube group handling in the caller (all 4 parts, offset by cube).
                return new[] { "ribcage", "rib_1", "rib_2", "rib_3" }.Select(n => definition.Parts.Single(p => p.Name == n)).ToArray();
            default:
                flatName = part.TrimStart('/').Split('/').Last(); break;
        }
        return [definition.Parts.Single(p => p.Name == flatName)];
    }

    /// <summary>Vertex count a single part emits when meshed (depends only on its cube dimensions/visibility).</summary>
    private static int EmittedVertCount(EntityModelPart part, int w, int h, ItemAtlas atlas, string texturePath)
    {
        var single = new EntityModelDefinition("count", w, h, [part with { VisibleWhen = null }], texturePath: texturePath, baselinePx: 0f, rawSourceFrame: true);
        var geo = BuildModelSpace(single, MakeLivingState(180f), w, h, atlas);
        return geo.VertexFloats / ChunkMesher.FloatsPerVertex;
    }

    /// <summary>True if part p is an ancestor (transitively) of targetName.</summary>
    private static bool IsAncestorOf(EntityModelPart p, string targetName, EntityModelDefinition def)
    {
        if (p.Name is null) return false;
        var cursor = def.Parts.FirstOrDefault(x => x.Name == targetName);
        var guard = 0;
        while (cursor.Name is not null && cursor.Parent is { } parent && guard++ < 32)
        {
            if (parent == p.Name) return true;
            cursor = def.Parts.FirstOrDefault(x => x.Name == parent);
        }
        return false;
    }

    private static ItemAtlas MakeAtlas(string tex, int w, int h) =>
        ItemAtlas.FromTextures(new Dictionary<string, TextureData>(StringComparer.Ordinal) { [tex] = new(w, h, new byte[w * h * 4]) });

    private static EntityRenderState MakeLivingState(float yaw) =>
        new(1, 0, 0f, 0f, 0f, yaw, 0f);

    /// <summary>Quantize a full-float sample UV to the half-float value the geometry buffer stores.</summary>
    private static float QuantizeHalf(float value) =>
        (float)BitConverter.UInt16BitsToHalf((ushort)BitConverter.HalfToUInt16Bits((Half)value));

    private static DynamicGeometry BuildModelSpace(EntityModelDefinition def, EntityRenderState state, int w, int h, ItemAtlas atlas) =>
        // #128 render units: modelScale:1 gives px=1/16, so output matches normalized samples.
        new EntityModelMesher().BuildModel(def, state, atlas.Get(def.TexturePath)!, w, h, modelScale: 1f);

    private static DynamicGeometry BuildLivingWorld(EntityModelDefinition def, EntityRenderState state, int w, int h, ItemAtlas atlas, float modelScale = EntityPlaceholderMesher.ModelScale)
    {
        // The internal BuildInto seam exposes the exact living-entity geometry path with a model override
        // and no block transform factory, so EntityModelTransform.ForRawSourceFrame is exercised on real parts.
        return new EntityModelMesher().BuildInto(new DynamicGeometry(), null, [state], new SpriteRect(0, 0, w, h, 1), w, h,
            skinAtlas: atlas, modelOverride: def, modelScale: modelScale);
    }

    private static List<Sample> LoadSample(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Repro", "Data", fileName);
        var result = new List<Sample>();
        foreach (var raw in File.ReadLines(path))
        {
            if (raw.StartsWith('#') || string.IsNullOrWhiteSpace(raw)) continue;
            var f = raw.Split('\t');
            result.Add(new Sample(f[1], int.Parse(f[2], CultureInfo.InvariantCulture), int.Parse(f[3], CultureInfo.InvariantCulture),
                int.Parse(f[4], CultureInfo.InvariantCulture), float.Parse(f[5], CultureInfo.InvariantCulture),
                float.Parse(f[6], CultureInfo.InvariantCulture), float.Parse(f[7], CultureInfo.InvariantCulture),
                float.Parse(f[8], CultureInfo.InvariantCulture), float.Parse(f[9], CultureInfo.InvariantCulture)));
        }
        return result;
    }
}
