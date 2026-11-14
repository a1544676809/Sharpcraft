// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

import java.io.BufferedWriter;
import java.io.FileWriter;
import java.util.ArrayList;
import java.util.Comparator;
import java.util.List;

import net.minecraft.SharedConstants;
import net.minecraft.core.registries.BuiltInRegistries;
import net.minecraft.resources.Identifier;
import net.minecraft.server.Bootstrap;
import net.minecraft.world.level.block.Block;
import net.minecraft.world.level.block.state.BlockState;
import net.minecraft.world.level.block.state.properties.Property;

/**
 * Dumps every block state of the running version as behaviour metadata: the global state id, the
 * block it belongs to, and each property with the value that state holds.
 *
 * <p>Only names and values are written - no assets and no code - so the output can be compared
 * against this project's decoder without carrying anything from the game into the repository.
 *
 * <p>Run against a version jar; see run.ps1 in the same directory for the classpath assembly.
 */
public final class BlockStateDump
{
    public static void main(String[] args) throws Exception
    {
        if (args.length != 1)
        {
            System.err.println("usage: BlockStateDump <output.tsv>");
            System.exit(2);
        }

        SharedConstants.tryDetectVersion();
        Bootstrap.bootStrap();

        List<String> lines = new ArrayList<>();
        for (Block block : BuiltInRegistries.BLOCK)
        {
            Identifier blockId = BuiltInRegistries.BLOCK.getKey(block);
            for (BlockState state : block.getStateDefinition().getPossibleStates())
            {
                StringBuilder line = new StringBuilder(96);
                line.append(Block.getId(state)).append('\t').append(blockId);

                for (Property<?> property : block.getStateDefinition().getProperties())
                {
                    line.append('\t').append(property.getName()).append('=').append(valueName(property, state));
                }

                lines.add(line.toString());
            }
        }

        lines.sort(Comparator.comparingInt(line -> Integer.parseInt(line.substring(0, line.indexOf('\t')))));

        try (BufferedWriter writer = new BufferedWriter(new FileWriter(args[0])))
        {
            writer.write("# SPDX-License-Identifier: LGPL-3.0-or-later\n");
            writer.write("# Block state properties of the running Minecraft version, state id order.\n");
            writer.write("# stateId\tblockId\tproperty=value...\n");
            for (String line : lines)
            {
                writer.write(line);
                writer.write('\n');
            }
        }

        System.out.println("wrote " + lines.size() + " states");
    }

    @SuppressWarnings({"unchecked", "rawtypes"})
    private static String valueName(Property property, BlockState state)
    {
        return property.getName((Comparable) state.getValue(property));
    }
}
