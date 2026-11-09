// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors
//
// Reference probe: replays SpearAnimations.thirdPersonUseItem for a wooden spear (delay 0.75s -> 15 ticks, dismount 100,
// knockback 200, damage 300, forward_movement 0.38) at ticksUsingItem = 30 with no swing, and prints the whole held-item
// world matrix. SpearUseItemTests carries these values.
//
//   java -cp "<...>/libraries/org/joml/joml/1.10.9/joml-1.10.9.jar" SpearUseProbe.java
import org.joml.*;

public final class SpearUseProbe {
  static float progress(float t, float s, float e) { return java.lang.Math.max(0f, java.lang.Math.min(1f, (t - s) / (e - s))); }
  static float inQuad(float x) { return x * x; }
  static float inOutExpo(float x) {
    if (x < 0.5f) return x == 0f ? 0f : (float)(java.lang.Math.pow(2, 20*x - 10) / 2);
    return x == 1f ? 1f : (float)((2 - java.lang.Math.pow(2, -20*x + 10)) / 2);
  }
  static float outBack(float x) { return 1f + 2.70158f * cube(x - 1f) + 1.70158f * (x - 1f) * (x - 1f); }
  static float cube(float x) { return x * x * x; }
  static float inOutSine(float x) { return (float)(-(java.lang.Math.cos((float)java.lang.Math.PI * x) - 1f) / 2.0); }
  static float outCirc(float x) { return (float) java.lang.Math.sqrt(java.lang.Math.max(0f, 1f - (x - 1f) * (x - 1f))); }
  static float inCirc(float x) { return (float)(-java.lang.Math.sqrt(java.lang.Math.max(0f,1f - x * x))) + 1f; }
  static float outCubic(float x) { return 1f - cube(1f - x); }
  static float inOutElastic(float x) {
    if (x == 0f) return 0f;
    if (x == 1f) return 1f;
    double sin = java.lang.Math.sin((20.0 * x - 11.125) * (float) java.lang.Math.PI * 4.0f / 9.0f);
    return x < 0.5f ? (float)(-(java.lang.Math.pow(2.0, 20.0*x - 10.0) * sin) / 2.0) : (float)(java.lang.Math.pow(2.0, -20.0*x + 10.0) * sin / 2.0 + 1.0);
  }

  public static void main(String[] args) {
    // wooden spear: delay 15, dismount 100, knockback 200, damage 300, forward 0.38
    int delay = 15, dismount = 100, knockback = 200, damage = 300;
    float forward = 0.38f;
    float time = 30f;
    float swing = 0f;
    int invert = 1;

    float finishRaising = delay;
    float finishSwaying = dismount + finishRaising;
    float startSwaying = finishSwaying - 20;
    float finishLowering = knockback + finishRaising;
    float startLowering = finishLowering - 40;
    float finishRaisingBack = damage + finishRaising;

    float raise = progress(time, 0f, finishRaising);
    float raiseStart = progress(raise, 0f, 0.5f);
    float raiseMiddle = progress(raise, 0.5f, 0.8f);
    float raiseEnd = progress(raise, 0.8f, 1f);
    float sway = progress(time, startSwaying, finishSwaying);
    float lower = outCubic(inOutElastic(progress(time - 20f, startLowering, finishLowering)));
    float raiseBack = progress(time, finishRaisingBack - 5, finishRaisingBack);
    float swayIntensity = 2f * outCirc(sway) - 2f * inCirc(raiseBack);
    float swaySlow = (float) java.lang.Math.sin(time * 19f * (float)(java.lang.Math.PI / 180.0)) * swayIntensity;
    float swayFast = (float) java.lang.Math.sin(time * 30f * (float)(java.lang.Math.PI / 180.0)) * swayIntensity;

    float attack = inQuad(progress(swing, 0.05f, 0.2f));
    float retract = inOutExpo(progress(swing, 0.4f, 1f));
    float raiseModified = 1f - outBack(1f - raise);
    float hitFeedback = 0f;

    Matrix4f m = new Matrix4f()
        .translate(-5f/16f, 2f/16f, 0f)
        .rotateX((float) java.lang.Math.toRadians(-90.0))
        .rotateY((float) java.lang.Math.toRadians(180.0))
        .translate(1f/16f, 2f/16f, -10f/16f)
        .translate(0f, -hitFeedback * 0.4f, -forward * (raiseModified - raiseBack) + hitFeedback)
        .translate(0f, -0.03125f, 0.125f).rotate(new Quaternionf().rotationX((float) java.lang.Math.toRadians(-(70f * (raise - raiseBack) - 40f * (attack - retract))))).translate(0f, 0.03125f, -0.125f)
        .translate(0f, 0f, 0.125f).rotate(new Quaternionf().rotationY((float) java.lang.Math.toRadians(invert * 90f * (raise - sway + 3f * retract + attack)))).translate(0f, 0f, -0.125f)
        .translate(0f, 3f/16f, 1f/16f).scale(0.55f).translate(-0.5f, -0.5f, -0.5f);

    StringBuilder sb = new StringBuilder();
    sb.append(String.format(java.util.Locale.ROOT, "raise=%.6f sway=%.6f lower=%.6f back=%.6f intensity=%.6f :: ", raise, sway, lower, raiseBack, swayIntensity));
    for (int r = 0; r < 4; r++) { sb.append('['); for (int c = 0; c < 4; c++) { sb.append(String.format(java.util.Locale.ROOT,"%.5f",m.get(r,c))); if (c<3) sb.append(", "); } sb.append(']'); if (r<3) sb.append(' '); }
    System.out.println(sb);
  }
}
