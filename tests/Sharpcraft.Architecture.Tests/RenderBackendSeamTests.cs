// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Xunit;

namespace Sharpcraft.Architecture.Tests;

/// <summary>
/// Render backend seam gate (docs/analysis/architecture/render-backend-seam.md S4): no source file outside the
/// rendering abstraction layer may name the WGPU binding. In particular Sharpcraft.Client consumes the neutral
/// PresentMode and lets the Rendering layer convert it at the WindowedRenderer.Run boundary.
/// </summary>
public sealed class RenderBackendSeamTests
{
    // Whitelist: exactly one directory, src/Sharpcraft.Framework/Rendering. It owns the WGPU binding and
    // BackendConvert.ToWgpu, which maps the neutral enums onto WGPU native values. No Sharpcraft.Client file is
    // whitelisted: the three MC_PRESENT_MODE leaks in WindowCommand.cs were replaced by neutral PresentMode values
    // in this change. If an unavoidable residue is ever found, add that single file here with a documented reason;
    // never exempt a whole project (that would silently undo the seam). Only .cs sources are scanned: a
    // PackageReference to Brew.Wgpu in a .csproj is a dependency declaration, not a leaked handle.
    private static readonly string RenderingDirectory =
        Path.Combine("src", "Sharpcraft.Framework", "Rendering") + Path.DirectorySeparatorChar;

    [Fact]
    public void WgpuTypesStayInsideTheRenderingAbstraction()
    {
        var root = RepositoryLocator.Root;
        var srcRoot = Path.Combine(root, "src");
        Assert.True(Directory.Exists(Path.Combine(srcRoot, "Sharpcraft.Client")),
            "src/Sharpcraft.Client is missing; the gate cannot cover the client");

        var offenders = new List<string>();

        foreach (var file in Directory.EnumerateFiles(srcRoot, "*.cs", SearchOption.AllDirectories))
        {
            if (IsBuildOutput(file))
            {
                continue;
            }

            var relative = Path.GetRelativePath(root, file);
            if (relative.StartsWith(RenderingDirectory, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var lines = File.ReadAllLines(file);
            for (var line = 0; line < lines.Length; line++)
            {
                if (lines[line].Contains("Brew.Wgpu", StringComparison.Ordinal))
                {
                    offenders.Add($"{relative.Replace('\\', '/')}:{line + 1}: {lines[line].Trim()}");
                }
            }
        }

        Assert.True(offenders.Count == 0,
            "Brew.Wgpu must stay in src/Sharpcraft.Framework/Rendering; src/Sharpcraft.Client must use the neutral enums\n"
            + string.Join("\n", offenders));
    }

    private static bool IsBuildOutput(string file) =>
        file.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar, StringComparison.Ordinal) ||
        file.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.Ordinal);
}
