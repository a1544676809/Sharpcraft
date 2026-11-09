// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors
//
// Reference probe: dumps the held-item world matrix with the STAB offset applied, for swingAnimation = 0.5, a generated
// item display transform and an adult right hand. SpearAnimations.thirdPersonAttackItem rotates the item about -X by
// 70 deg * (attack - retract) around the pivot (0, -0.125, 0.125).
//
//   java -cp "<...>/libraries/org/joml/joml/1.10.9/joml-1.10.9.jar" StabProbe.java
//
// Output (swingAnimation = 0.5): attack = 1.0, retract = 0.004922, deg = 69.655495,
//   [[-0.55, 0, 0], [0, 0.51569, -0.19122], [0, -0.19122, -0.51569]]  translation (-0.1, 0.77750, 0.30341)
import org.joml.*;

public final class StabProbe {
  public static void main(String[] args) {
    float anim = 0.5f;
    float attack = easeInQuad(progress(anim, 0.05f, 0.2f));
    float retract = easeInOutExpo(progress(anim, 0.4f, 1.0f));
    float deg = 70f * (attack - retract);
    Matrix4f m = new Matrix4f()
        .translate(-5f/16f, 2f/16f, 0f)
        .rotateX((float) java.lang.Math.toRadians(-90.0))
        .rotateY((float) java.lang.Math.toRadians(180.0))
        .translate(1f/16f, 2f/16f, -10f/16f)
        .translate(0f, -0.125f, 0.125f)
        .rotateX((float) java.lang.Math.toRadians(-deg))
        .translate(0f, 0.125f, -0.125f)
        .translate(0f, 3f/16f, 1f/16f).scale(0.55f).translate(-0.5f,-0.5f,-0.5f);
    StringBuilder sb = new StringBuilder();
    sb.append(String.format(java.util.Locale.ROOT, "attack=%.6f retract=%.6f deg=%.6f :: ", attack, retract, deg));
    for (int r = 0; r < 4; r++) { sb.append('['); for (int c = 0; c < 4; c++) { sb.append(String.format(java.util.Locale.ROOT,"%.5f",m.get(r,c))); if (c<3) sb.append(", "); } sb.append(']'); if (r<3) sb.append(' '); }
    System.out.println(sb);
  }
  static float progress(float t, float s, float e) { return java.lang.Math.max(0f, java.lang.Math.min(1f, (t - s) / (e - s))); }
  static float easeInQuad(float x) { return x * x; }
  static float easeInOutExpo(float x) {
    if (x < 0.5f) return x == 0f ? 0f : (float)(java.lang.Math.pow(2, 20*x - 10) / 2);
    return x == 1f ? 1f : (float)((2 - java.lang.Math.pow(2, -20*x + 10)) / 2);
  }
}
