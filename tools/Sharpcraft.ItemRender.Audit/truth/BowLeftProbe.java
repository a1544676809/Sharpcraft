// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors
//
// Reference probe: replays the bow's thirdperson_lefthand display entry through JOML with the reference left-hand fix
// (ItemTransform.apply(true, pose) negates translation.x and rotation.y/z).
//
//   java -cp "<...>/libraries/org/joml/joml/1.10.9/joml-1.10.9.jar" BowLeftProbe.java
//
// Output: [[0.133022, 0.631326, 0.764024], [0.111619, 0.756427, -0.644483], [-0.984808, 0.171010, 0.030154]]
//         translation (0.432583, -0.904382, 0.081403)
import org.joml.*;

public final class BowLeftProbe {
  public static void main(String[] args) {
    // bow thirdperson_lefthand: rotation [-80, -280, 40], translation [-1, -2, 2.5] px, scale 1, with the left-hand fix.
    float rx = (float) java.lang.Math.toRadians(-80.0);
    float ry = (float) java.lang.Math.toRadians(280.0);
    float rz = (float) java.lang.Math.toRadians(-40.0);
    Matrix4f m = new Matrix4f()
        .translate(1f/16f, -2f/16f, 2.5f/16f)
        .rotate(new Quaternionf().rotationXYZ(rx, ry, rz))
        .scale(1f, 1f, 1f)
        .translate(-0.5f, -0.5f, -0.5f);
    StringBuilder sb = new StringBuilder();
    for (int r = 0; r < 4; r++) { sb.append('['); for (int c = 0; c < 4; c++) { sb.append(String.format(java.util.Locale.ROOT,"%.6f",m.get(r,c))); if (c<3) sb.append(", "); } sb.append(']'); if (r<3) sb.append(' '); }
    System.out.println(sb);
  }
}
