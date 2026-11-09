// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors
//
// Reference probe: dumps the held-item world matrix for two different item display transforms, so a single anchor can
// be checked to be independent of them. Case A is a generated item (translation (0, 3, 1) px, scale 0.55, no rotation);
// case B is a shield (rotation [0, 90, 0], translation (10, 6, -4) px, scale 1).
//
// Run from this directory (paths are machine specific; the JOML jar ships inside the vanilla Minecraft libraries):
//     java -cp "<...>/libraries/org/joml/joml/1.10.9/joml-1.10.9.jar" DisplayVariantsProbe.java

import org.joml.*;

public final class DisplayVariantsProbe {
    private DisplayVariantsProbe() {
    }

    public static void main(String[] args) {
        dump("generated", new Matrix4f()
            .translate(0f, 3f / 16f, 1f / 16f)
            .scale(0.55f, 0.55f, 0.55f)
            .translate(-0.5f, -0.5f, -0.5f));
        dump("shield", new Matrix4f()
            .translate(10f / 16f, 6f / 16f, -4f / 16f)
            .rotate(new Quaternionf().rotationXYZ(0f, (float) java.lang.Math.toRadians(90.0), 0f))
            .scale(1f, 1f, 1f)
            .translate(-0.5f, -0.5f, -0.5f));
    }

    private static void dump(String name, Matrix4f display) {
        Matrix4f world = new Matrix4f()
            .translate(-5f / 16f, 2f / 16f, 0f)
            .rotateX((float) java.lang.Math.toRadians(-90.0))
            .rotateY((float) java.lang.Math.toRadians(180.0))
            .translate(1f / 16f, 2f / 16f, -10f / 16f)
            .mul(display);

        StringBuilder sb = new StringBuilder(name).append(": ");
        for (int r = 0; r < 4; r++) {
            sb.append('[');
            for (int c = 0; c < 4; c++) {
                sb.append(String.format(java.util.Locale.ROOT, "%.6f", world.get(r, c)));
                if (c < 3) {
                    sb.append(", ");
                }
            }
            sb.append(']');
            if (r < 3) {
                sb.append(' ');
            }
        }
        System.out.println(sb);
    }
}
