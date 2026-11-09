// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors
//
// Reference probe: reproduces ItemInHandLayer.submitArmWithItem with the shipped JOML build and prints the complete
// world matrix of the held item, so Sharpcraft's held-item anchor can be solved against it instead of guessed.
//
// Run from this directory (paths are machine specific; the JOML jar ships inside the vanilla Minecraft libraries):
//     java -cp "<...>/libraries/org/joml/joml/1.10.9/joml-1.10.9.jar" MatrixDump.java
//
// Scenario: adult right hand, identity arm pose, generic generated item display transform (translation (0, 3, 1) px,
// no rotation, scale 0.55). The printed rows are the reference world matrix; HeldItemPlacementComparisonTests carries
// the same values and pins the C# side against them.

import org.joml.*;

public final class MatrixDump {
    private MatrixDump() {
    }

    public static void main(String[] args) {
        Matrix4f m = new Matrix4f()
            // translateToHand: root part (identity) then the arm pivot at (-5, 2, 0) model pixels
            .translate(-5f / 16f, 2f / 16f, 0f)
            // ItemInHandLayer.submitArmWithItem
            .rotateX((float) java.lang.Math.toRadians(-90.0))
            .rotateY((float) java.lang.Math.toRadians(180.0))
            .translate(1f / 16f, 2f / 16f, -10f / 16f)
            // item display transform (third person right hand)
            .translate(0f, 3f / 16f, 1f / 16f)
            .scale(0.55f, 0.55f, 0.55f)
            .translate(-0.5f, -0.5f, -0.5f);

        StringBuilder sb = new StringBuilder();
        for (int r = 0; r < 4; r++) {
            for (int c = 0; c < 4; c++) {
                sb.append(String.format(java.util.Locale.ROOT, "%.6ff, ", m.get(r, c)));
            }
            if (r < 3) {
                sb.append("| ");
            }
        }
        System.out.println(sb);
    }
}
