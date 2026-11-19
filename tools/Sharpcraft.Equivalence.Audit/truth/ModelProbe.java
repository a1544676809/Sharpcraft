// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

import com.mojang.blaze3d.vertex.PoseStack;
import com.mojang.math.Axis;
import java.io.BufferedWriter;
import java.lang.reflect.Field;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.ArrayList;
import java.util.List;
import java.util.Locale;
import net.minecraft.SharedConstants;
import net.minecraft.client.model.geom.EntityModelSet;
import net.minecraft.client.model.geom.ModelLayerLocation;
import net.minecraft.client.model.geom.ModelLayers;
import net.minecraft.client.model.geom.ModelPart;
import net.minecraft.server.Bootstrap;
import org.joml.Matrix4f;
import org.joml.Vector3f;

/**
 * Samples vertices from a baked vanilla model layer without going through the game's renderer.
 *
 * <p>This is deliberately a <em>truth generator</em>, not an implementation of rendering. It takes
 * the layer definition from the running jar, applies an optional source part pose, then applies an
 * optional PoseStack transform-chain, and writes every emitted cube vertex. The output is consumed
 * by tests or by a human comparing Sharpcraft's final world-space vertices; it never copies game
 * source or assets into the repository.</p>
 *
 * <p>Output columns: layer, partPath, cubeIndex, polygonIndex, vertexIndex, x, y, z, u, v.</p>
 *
 * <p>Usage:</p>
 * <pre>
 * ModelProbe &lt;output.tsv&gt; &lt;LAYER[,LAYER...]&gt;
 *     [--pose partPath=rx,ry,rz]
 *     [--model-scale factor]
 *     [--transform translate:x,y,z]
 *     [--transform rotX:degrees]
 *     [--transform rotY:degrees]
 *     [--transform rotZ:degrees]
 *     [--transform scale:x,y,z]
 * </pre>
 *
 * <p>Pose angles are radians, matching {@code ModelPart.xRot/yRot/zRot}. Transform angles are
 * degrees, matching the source renderer PoseStack calls. Options are applied in command-line
 * order; with no options the output is the raw y-down model-space layer.</p>
 */
public final class ModelProbe
{
    public static void main(String[] args) throws Exception
    {
        if (args.length < 2)
        {
            System.err.println("usage: ModelProbe <output.tsv> <LAYER[,LAYER...]> "
                + "[--pose partPath=rx,ry,rz] [--transform op:args]");
            System.exit(2);
        }

        Path output = Path.of(args[0]);
        String[] layers = args[1].split(",");
        List<PoseAssignment> poses = new ArrayList<>();
        PoseStack poseStack = new PoseStack();
        float modelScale = 1f;

        for (int index = 2; index < args.length; index++)
        {
            String option = args[index];
            if (index + 1 >= args.length)
            {
                throw new IllegalArgumentException("missing value for " + option);
            }

            String value = args[++index];
            switch (option)
            {
                case "--pose" -> poses.add(parsePose(value));
                case "--model-scale" -> modelScale = parseFloat(value, value);
                case "--transform" -> applyTransform(poseStack, value);
                default -> throw new IllegalArgumentException("unknown option: " + option);
            }
        }

        // ModelPart vertices are in model pixels; the source renderer applies the outer pose
        // first and ModelPart.render applies the 1/16 model scale last.
        poseStack.scale(modelScale, modelScale, modelScale);

        SharedConstants.tryDetectVersion();
        Bootstrap.bootStrap();

        EntityModelSet modelSet = EntityModelSet.vanilla();
        Files.createDirectories(output.toAbsolutePath().getParent());
        try (BufferedWriter writer = Files.newBufferedWriter(output, StandardCharsets.UTF_8))
        {
            writer.write("# layer\tpart\tcube\tpolygon\tvertex\tx\ty\tz\tu\tv\n");
            for (String rawLayer : layers)
            {
                String layerName = rawLayer.trim();
                if (layerName.isEmpty())
                {
                    continue;
                }

                Field field = ModelLayers.class.getField(layerName);
                ModelLayerLocation location = (ModelLayerLocation) field.get(null);
                ModelPart root = modelSet.bakeLayer(location);
                for (PoseAssignment pose : poses)
                {
                    applyPose(root, pose);
                }

                root.visit(poseStack, (pose, partPath, cubeIndex, cube) -> {
                    Matrix4f matrix = pose.pose();
                    String path = partPath.isEmpty() ? "/" : partPath;
                    for (int polygonIndex = 0; polygonIndex < cube.polygons.length; polygonIndex++)
                    {
                        ModelPart.Polygon polygon = cube.polygons[polygonIndex];
                        for (int vertexIndex = 0; vertexIndex < polygon.vertices().length; vertexIndex++)
                        {
                            ModelPart.Vertex vertex = polygon.vertices()[vertexIndex];
                            Vector3f world = matrix.transformPosition(
                                new Vector3f(vertex.x(), vertex.y(), vertex.z()));
                            try
                            {
                                writer.write(String.format(
                                    Locale.ROOT,
                                    "%s\t%s\t%d\t%d\t%d\t%.6f\t%.6f\t%.6f\t%.6f\t%.6f%n",
                                    layerName, path, cubeIndex, polygonIndex, vertexIndex,
                                    world.x, world.y, world.z, vertex.u(), vertex.v()));
                            }
                            catch (Exception exception)
                            {
                                throw new RuntimeException(exception);
                            }
                        }
                    }
                });
            }
        }

        System.out.println("wrote model probe to " + output.toAbsolutePath());
    }

    private static PoseAssignment parsePose(String value)
    {
        int equals = value.indexOf('=');
        if (equals <= 0)
        {
            throw new IllegalArgumentException("pose must be partPath=rx,ry,rz: " + value);
        }

        String path = value.substring(0, equals);
        String[] angles = value.substring(equals + 1).split(",");
        if (angles.length != 3)
        {
            throw new IllegalArgumentException("pose must have three angles: " + value);
        }

        return new PoseAssignment(
            path,
            parseFloat(angles[0], value),
            parseFloat(angles[1], value),
            parseFloat(angles[2], value));
    }

    private static void applyPose(ModelPart root, PoseAssignment pose)
    {
        ModelPart part = root;
        if (!pose.path().isEmpty() && !pose.path().equals("root"))
        {
            for (String segment : pose.path().split("\\."))
            {
                part = part.getChild(segment);
            }
        }

        part.xRot = pose.xRadians();
        part.yRot = pose.yRadians();
        part.zRot = pose.zRadians();
    }

    private static void applyTransform(PoseStack poseStack, String value)
    {
        int colon = value.indexOf(':');
        if (colon <= 0)
        {
            throw new IllegalArgumentException("transform must be op:args: " + value);
        }

        String operation = value.substring(0, colon);
        String[] numbers = value.substring(colon + 1).split(",");
        switch (operation)
        {
            case "translate" -> poseStack.translate(
                parseFloat(numbers, 0, value), parseFloat(numbers, 1, value), parseFloat(numbers, 2, value));
            case "scale" -> poseStack.scale(
                parseFloat(numbers, 0, value), parseFloat(numbers, 1, value), parseFloat(numbers, 2, value));
            case "rotX" -> poseStack.rotateDegrees(Axis.XP, parseFloat(numbers, 0, value));
            case "rotY" -> poseStack.rotateDegrees(Axis.YP, parseFloat(numbers, 0, value));
            case "rotZ" -> poseStack.rotateDegrees(Axis.ZP, parseFloat(numbers, 0, value));
            default -> throw new IllegalArgumentException("unknown transform op: " + operation);
        }
    }

    private static float parseFloat(String[] values, int index, String source)
    {
        if (index >= values.length)
        {
            throw new IllegalArgumentException("missing number in " + source);
        }

        return parseFloat(values[index], source);
    }

    private static float parseFloat(String value, String source)
    {
        try
        {
            return Float.parseFloat(value);
        }
        catch (NumberFormatException exception)
        {
            throw new IllegalArgumentException("bad number in " + source + ": " + value, exception);
        }
    }

    private record PoseAssignment(String path, float xRadians, float yRadians, float zRadians)
    {
    }
}
