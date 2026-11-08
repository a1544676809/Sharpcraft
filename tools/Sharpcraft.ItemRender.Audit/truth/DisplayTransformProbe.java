import org.joml.*;
import java.io.*;
import java.util.*;

// Reference oracle for item display transforms (docs/modules/item-rendering.md 3.5.5).
//
// It reproduces the reference ItemTransform.apply pose sequence with the JOML version the game ships:
//   translate(clamped translation * 1/16) -> rotate(new Quaternionf().rotationXYZ(rx, ry, rz)) -> scale -> translate(-0.5)
// and prints where a handful of model points land. The C# side must reproduce these numbers exactly, so the output is
// committed as tests/Sharpcraft.Client.Tests/Data/item-display-truth.json.
//
// Run:  java -cp <joml.jar> truth/DisplayTransformProbe.java > <outfile>
public class DisplayTransformProbe {
  record Case(String name, float rx, float ry, float rz, float tx, float ty, float tz, float sx, float sy, float sz) {}

  static final List<Case> CASES = List.of(
      new Case("chest_gui", 30f, 45f, 0f, 0f, 0f, 0f, 0.625f, 0.625f, 0.625f),
      new Case("chest_ground", 0f, 0f, 0f, 0f, 3f, 0f, 0.25f, 0.25f, 0.25f),
      new Case("chest_fixed", 0f, 180f, 0f, 0f, 0f, 0f, 0.5f, 0.5f, 0.5f),
      new Case("chest_thirdperson_righthand", 75f, 315f, 0f, 0f, 2.5f, 0f, 0.375f, 0.375f, 0.375f),
      new Case("rotation_order_probe", 30f, 45f, 60f, 0f, 0f, 0f, 1f, 1f, 1f),
      new Case("nonuniform_scale_probe", 10f, 20f, 30f, 1f, -2f, 3f, 0.5f, 0.75f, 1.25f));

  static final float[][] POINTS = {
      {0f, 0f, 0f}, {1f, 0f, 0f}, {0f, 1f, 0f}, {0f, 0f, 1f}, {0.5f, 0.5f, 0.5f}, {1f, 1f, 1f}
  };

  public static void main(String[] args) throws IOException {
    StringBuilder sb = new StringBuilder();
    sb.append("{\n  \"source\": \"JOML 1.10.9 quaternion rotationXYZ; reference ItemTransform.apply sequence\",\n  \"cases\": [\n");
    for (int c = 0; c < CASES.size(); c++) {
      Case k = CASES.get(c);
      Matrix4f m = new Matrix4f()
          .translation(k.tx() / 16f, k.ty() / 16f, k.tz() / 16f)
          .rotate(new Quaternionf().rotationXYZ(
              (float) java.lang.Math.toRadians(k.rx()),
              (float) java.lang.Math.toRadians(k.ry()),
              (float) java.lang.Math.toRadians(k.rz())))
          .scale(k.sx(), k.sy(), k.sz())
          .translate(-0.5f, -0.5f, -0.5f);
      sb.append("    {\n      \"name\": \"").append(k.name()).append("\",\n");
      sb.append("      \"points\": [\n");
      for (int p = 0; p < POINTS.length; p++) {
        Vector3f in = new Vector3f(POINTS[p][0], POINTS[p][1], POINTS[p][2]);
        Vector3f out = m.transformPosition(new Vector3f(in));
        sb.append(String.format("        {\"in\": [%.6f, %.6f, %.6f], \"out\": [%.6f, %.6f, %.6f]}%s%n",
            in.x, in.y, in.z, out.x, out.y, out.z, p + 1 < POINTS.length ? "," : ""));
      }
      sb.append("      ]\n    }").append(c + 1 < CASES.size() ? ",\n" : "\n");
    }
    sb.append("  ]\n}\n");
    try (Writer w = new FileWriter(args.length > 0 ? args[0] : "item-display-truth.json")) {
      w.write(sb.toString());
    }
    System.out.println("wrote truth vectors");
  }
}
