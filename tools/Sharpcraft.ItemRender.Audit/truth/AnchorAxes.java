// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors
//
// Reference probe: prints how A_ref = Rx(-90°) · Ry(180°) · T_off maps the item axes, i.e. the rotation half of
// ItemInHandLayer.submitArmWithItem on its own.
//
// Run from this directory (paths are machine specific; the JOML jar ships inside the vanilla Minecraft libraries):
//     java -cp "<...>/libraries/org/joml/joml/1.10.9/joml-1.10.9.jar" AnchorAxes.java
//
// Output in the reference model frame (y down):
//     ex -> (-1.0000, 0.0000, 0.0000)   ey -> (0.0000, 0.0000, -1.0000)   ez -> (0.0000, -1.0000, 0.0000)
//     origin -> (-0.0625, 0.6250, -0.1250)

import org.joml.*;

public final class AnchorAxes {
    private AnchorAxes() {
    }

    public static void main(String[] args) {
        Matrix4f a = new Matrix4f()
            .rotateX((float) java.lang.Math.toRadians(-90.0))
            .rotateY((float) java.lang.Math.toRadians(180.0))
            .translate(1f / 16f, 2f / 16f, -10f / 16f);

        print("ex", a.transformDirection(new Vector3f(1, 0, 0)));
        print("ey", a.transformDirection(new Vector3f(0, 1, 0)));
        print("ez", a.transformDirection(new Vector3f(0, 0, 1)));
        print("origin", a.transformPosition(new Vector3f(0f, 0f, 0f)));
    }

    private static void print(String label, Vector3f v) {
        System.out.printf(java.util.Locale.ROOT, "%-6s -> (%.4f, %.4f, %.4f)%n", label, v.x, v.y, v.z);
    }
}
