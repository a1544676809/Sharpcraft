// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Text;
using Sharpcraft.Common.Registries;

if (args.Length != 1)
{
    Console.Error.WriteLine("usage: Sharpcraft.BlockState.Audit <output.tsv>");
    return 2;
}

var layout = VanillaBlockRegistry.Layout;
var definitions = BlockStateDefinitions.Baseline;

using var writer = new StreamWriter(args[0], append: false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
writer.NewLine = "\n";
writer.WriteLine("# SPDX-License-Identifier: LGPL-3.0-or-later");
writer.WriteLine("# Block state properties as this project decodes them, state id order.");
writer.WriteLine("# stateId\tblockId\tproperty=value...");

var written = 0;
var line = new StringBuilder(96);
for (var stateId = 0; stateId < layout.TotalStates; stateId++)
{
    if (layout.BlockAt(stateId) is not { } block)
    {
        continue;
    }

    line.Clear();
    line.Append(stateId).Append('\t').Append(block);

    if (definitions.TryGet(block, out var definition) && layout.TryGetBaseId(block, out var baseId))
    {
        var values = definition.ValuesAt(stateId - baseId);
        var properties = definition.Properties;
        for (var i = 0; i < properties.Count; i++)
        {
            line.Append('\t').Append(properties[i].Name).Append('=').Append(values[i]);
        }
    }

    writer.WriteLine(line);
    written++;
}

Console.WriteLine($"wrote {written} states");
return 0;
