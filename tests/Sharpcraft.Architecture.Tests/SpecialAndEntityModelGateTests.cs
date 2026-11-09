// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Text.RegularExpressions;
using Xunit;

namespace Sharpcraft.Architecture.Tests;

/// <summary>
/// Hard architectural gates for Special Models, Block Entities, and Entity Models.
/// Prevents Agents from re-inventing broken UV strip formulas, adding UV wrap/seam-split hacks,
/// or emitting double-winding band-aids when porting Vanilla models.
/// </summary>
public sealed partial class SpecialAndEntityModelGateTests
{
    /// <summary>
    /// Rule 1: The Minecraft <c>ModelPart.Cube</c> UV strip formula (<c>u0..u4, v0..v2</c>) must never chain
    /// <c>u3</c> off <c>u22</c> (<c>u22 + ...Size.Z</c> or <c>u22 + d</c>). Any such expression in <c>src/</c>
    /// shifts the EAST and SOUTH faces by +width pixels into transparent texture margins.
    /// </summary>
    [Fact]
    public void NoSourceFileChainsU3OffU22()
    {
        var srcRoot = Path.Combine(RepositoryLocator.Root, "src");
        var offenders = new List<string>();

        foreach (var file in Directory.EnumerateFiles(srcRoot, "*.cs", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(file);
            if (BrokenU3ChainRegex().IsMatch(text))
            {
                offenders.Add(Path.GetRelativePath(RepositoryLocator.Root, file));
            }
        }

        Assert.True(
            offenders.Count == 0,
            "FATAL UV BUG: u3 (f8) must be computed as u2 + depth (tx + d + w + d), NEVER u22 + depth! Found in:\n"
            + string.Join("\n", offenders));
    }

    /// <summary>
    /// Rule 2: Special model bakers must never split faces across texture seams (uRight > textureWidth) or
    /// emit double windings for a single cube face. In Vanilla ModelPart.Cube, every box fits within [0, texWidth]
    /// and uses single outward-facing winding.
    /// </summary>
    [Fact]
    public void SpecialModelBakeDoesNotContainSeamSplittingOrDoubleWindingHacks()
    {
        var path = Path.Combine(RepositoryLocator.Root, "src", "Sharpcraft.Client", "Render", "SpecialModelBake.cs");
        if (!File.Exists(path))
        {
            return;
        }

        var text = File.ReadAllText(path);

        Assert.DoesNotContain("seamTop", text, StringComparison.Ordinal);
        Assert.DoesNotContain("seamBottom", text, StringComparison.Ordinal);
        Assert.DoesNotContain("uRight <= textureWidth", text, StringComparison.Ordinal);
    }

    [GeneratedRegex(@"u3\s*=\s*u22\s*\+", RegexOptions.CultureInvariant)]
    private static partial Regex BrokenU3ChainRegex();
}
