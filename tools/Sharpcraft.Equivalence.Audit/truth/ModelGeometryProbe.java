// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

import com.mojang.blaze3d.vertex.PoseStack;
import com.mojang.blaze3d.vertex.VertexConsumer;
import com.mojang.math.Axis;
import java.io.BufferedWriter;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.Locale;
import net.minecraft.SharedConstants;
import net.minecraft.client.model.geom.ModelPart;
import net.minecraft.client.model.object.chest.ChestModel;
import net.minecraft.client.renderer.chunk.VisGraph;
import net.minecraft.client.renderer.chunk.VisibilitySet;
import net.minecraft.core.BlockPos;
import net.minecraft.core.Direction;
import net.minecraft.server.Bootstrap;

/**
 * Emits canonical ground-truth geometry and visibility outputs from the running Minecraft jar
 * for automated comparison against Sharpcraft.
 */
public final class ModelGeometryProbe {
    public static void main(String[] args) throws Exception {
        if (args.length != 1) {
            System.err.println("usage: ModelGeometryProbe <output.tsv>");
            System.exit(2);
        }

        SharedConstants.tryDetectVersion();
        Bootstrap.bootStrap();

        try (BufferedWriter out = Files.newBufferedWriter(Path.of(args[0]), StandardCharsets.UTF_8)) {
            out.write("# Canonical Vanilla 26.3 Oracle output.\n");
            emitChestCases(out);
            emitVisibilityCases(out);
        }
    }

    private static void emitChestCases(BufferedWriter out) throws Exception {
        String[] typeNames = {"single", "left", "right"};
        Direction[] facings = {Direction.NORTH, Direction.EAST, Direction.SOUTH, Direction.WEST};
        String[] facingNames = {"north", "east", "south", "west"};
        float[] opens = {0.0f, 0.5f, 1.0f};

        for (int ti = 0; ti < typeNames.length; ti++) {
            ModelPart root = switch (ti) {
                case 1 -> ChestModel.createDoubleBodyLeftLayer().bakeRoot();
                case 2 -> ChestModel.createDoubleBodyRightLayer().bakeRoot();
                default -> ChestModel.createSingleBodyLayer().bakeRoot();
            };
            ChestModel model = new ChestModel(root);

            for (int fi = 0; fi < facings.length; fi++) {
                for (float open : opens) {
                    float eased = 1.0f - open;
                    eased = 1.0f - eased * eased * eased;
                    model.setupAnim(eased);

                    PoseStack poseStack = new PoseStack();
                    poseStack.translate(0.5f, 0.5f, 0.5f);
                    poseStack.mulPose(Axis.YP.rotationDegrees(-facings[fi].toYRot()));
                    poseStack.translate(-0.5f, -0.5f, -0.5f);

                    BoundsCollector collector = new BoundsCollector();
                    model.renderToBuffer(poseStack, collector, 0, 0);

                    String caseId = String.format(Locale.ROOT, "%s_%s_open%.1f", typeNames[ti], facingNames[fi], open);
                    float expectedAngle = eased * ((float) Math.PI / 2.0f);
                    out.write(String.format(
                        Locale.ROOT,
                        "chest_geom\t%s\tverts=%d\ttris=%d\tangle=%.5f\tmin=(%.5f,%.5f,%.5f)\tmax=(%.5f,%.5f,%.5f)\n",
                        caseId,
                        collector.vertexCount,
                        collector.vertexCount / 2,
                        expectedAngle,
                        collector.minX, collector.minY, collector.minZ,
                        collector.maxX, collector.maxY, collector.maxZ));
                }
            }
        }
    }

    private static void emitVisibilityCases(BufferedWriter out) throws Exception {
        emitVisCase(out, "empty", (x, y, z) -> false);
        emitVisCase(out, "full", (x, y, z) -> true);
        emitVisCase(out, "solid_wall_y8", (x, y, z) -> y == 8);
        emitVisCase(out, "wall_y8_with_hole", (x, y, z) -> y == 8 && !(x == 8 && z == 8));
    }

    @FunctionalInterface
    interface BlockPredicate {
        boolean isOpaque(int x, int y, int z);
    }

    private static void emitVisCase(BufferedWriter out, String name, BlockPredicate pred) throws Exception {
        VisGraph graph = new VisGraph();
        BlockPos.MutableBlockPos pos = new BlockPos.MutableBlockPos();
        for (int y = 0; y < 16; y++) {
            for (int z = 0; z < 16; z++) {
                for (int x = 0; x < 16; x++) {
                    if (pred.isOpaque(x, y, z)) {
                        pos.set(x, y, z);
                        graph.setOpaque(pos);
                    }
                }
            }
        }
        VisibilitySet vis = graph.resolve();
        long mask = 0L;
        Direction[] dirs = {Direction.WEST, Direction.EAST, Direction.DOWN, Direction.UP, Direction.NORTH, Direction.SOUTH};
        for (int a = 0; a < 6; a++) {
            for (int b = 0; b < 6; b++) {
                if (vis.visibilityBetween(dirs[a], dirs[b])) {
                    mask |= 1L << (a * 6 + b);
                }
            }
        }
        out.write(String.format(Locale.ROOT, "vis_graph\t%s\tmask=0x%09x\n", name, mask));
    }

    private static final class BoundsCollector implements VertexConsumer {
        int vertexCount;
        float minX = Float.POSITIVE_INFINITY, minY = Float.POSITIVE_INFINITY, minZ = Float.POSITIVE_INFINITY;
        float maxX = Float.NEGATIVE_INFINITY, maxY = Float.NEGATIVE_INFINITY, maxZ = Float.NEGATIVE_INFINITY;

        @Override
        public VertexConsumer addVertex(float x, float y, float z) {
            vertexCount++;
            minX = Math.min(minX, x);
            minY = Math.min(minY, y);
            minZ = Math.min(minZ, z);
            maxX = Math.max(maxX, x);
            maxY = Math.max(maxY, y);
            maxZ = Math.max(maxZ, z);
            return this;
        }

        @Override public VertexConsumer setColor(int r, int g, int b, int a) { return this; }
        @Override public VertexConsumer setColor(int argb) { return this; }
        @Override public VertexConsumer setUv(float u, float v) { return this; }
        @Override public VertexConsumer setUv1(int u, int v) { return this; }
        @Override public VertexConsumer setUv2(int u, int v) { return this; }
        @Override public VertexConsumer setNormal(float x, float y, float z) { return this; }
        @Override public VertexConsumer setLineWidth(float width) { return this; }
    }
}
