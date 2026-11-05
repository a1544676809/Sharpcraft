<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

<!-- BEGIN GENERATED: class-map (run: dotnet run --project tools/Sharpcraft.ClassMap -- --write) -->

| Project | Sharpcraft type | Kind | Status | Reference class | Reference path | Note |
| --- | --- | --- | --- | --- | --- | --- |
| Sharpcraft.Client | Sharpcraft.Client.Game.TickLoop | Mirror | Full | net.minecraft.client.Timer | ref://mc-26.3-analysis/src/net/minecraft/client/Timer.java | Fixed-timestep accumulator + partialTick (alpha) |
| Sharpcraft.Client | Sharpcraft.Client.World.EntityTracker | Merged | Partial | net.minecraft.client.multiplayer.ClientLevel | ref://mc-26.3-analysis/src/net/minecraft/client/multiplayer/ClientLevel.java | Merges ClientLevel/Entity/SteppedInterpolationHandler responsibilities; position sync per T-639/T-640 |

<!-- END GENERATED: class-map -->
