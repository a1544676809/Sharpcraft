// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

import net.minecraft.SharedConstants;
import net.minecraft.core.registries.BuiltInRegistries;
import net.minecraft.server.Bootstrap;
import net.minecraft.world.entity.EntityTypes;

/** Prints the numeric BuiltInRegistries.ENTITY_TYPE id used by the thrown-trident production wiring. */
public final class EntityTypeRegistryProbe
{
    public static void main(String[] args)
    {
        SharedConstants.tryDetectVersion();
        Bootstrap.bootStrap();
        System.out.println("minecraft:trident\t" + BuiltInRegistries.ENTITY_TYPE.getId(EntityTypes.TRIDENT));
    }
}
