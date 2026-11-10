// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Architecture.Tests;

/// <summary>
/// Chunk meshing has one home: the render layer (T-793).
/// </summary>
/// <remarks>
/// The windowed startup used to mesh every loaded chunk a second time from the frame request builder, without the per pass
/// cull and visual tables the cache builds, which cost forty five seconds per launch
/// (docs/specs/performance-baseline.md 11). The code now reads the cache's output instead, but nothing in the compiler
/// stops someone from calling the mesher from the request builder again, so this test does: the mesher entry points may
/// only be named from <c>src/Sharpcraft.Client/Render/</c>, where the single cache and worker pool live.
/// </remarks>
public sealed class SingleMeshingHomeTests
{
    [Fact]
    public void OnlyTheTwoMeshersCallTheMesher()
    {
        // The cache meshes synchronously (Render) and the worker pool meshes in the background (World); those two are the
        // only places allowed to name the mesher entry points, so no third caller can start meshing on its own.
        var allowed = new HashSet<string>(StringComparer.Ordinal)
        {
            "src/Sharpcraft.Client/Render/ChunkMeshCache.cs",
            "src/Sharpcraft.Client/World/ChunkWorkerPool.cs"
        };

        var client = Path.Combine(RepositoryRoot(), "src", "Sharpcraft.Client");
        var offenders = new List<string>();

        foreach (var file in Directory.EnumerateFiles(client, "*.cs", SearchOption.AllDirectories))
        {
            if (Path.GetFileName(file) is "ChunkMesher.cs")
            {
                continue;
            }

            // XML doc <see cref> tags name types without calling them, so only real code lines are scanned.
            var code = string.Join(
                '\n',
                File.ReadAllLines(file).Where(line => !line.TrimStart().StartsWith("///", StringComparison.Ordinal)));

            if (!code.Contains("MeshChunk(", StringComparison.Ordinal) &&
                !code.Contains("ChunkMesher.Mesh(", StringComparison.Ordinal))
            {
                continue;
            }

            var relative = Path.GetRelativePath(RepositoryRoot(), file).Replace('\\', '/');
            if (!allowed.Contains(relative))
            {
                offenders.Add(relative);
            }
        }

        Assert.True(
            offenders.Count == 0,
            "chunk meshing gained a third caller: " + string.Join(", ", offenders) +
            ". The frame request must consume the cache's output instead of meshing (docs/specs/performance-baseline.md 11)");
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Sharpcraft.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("the repository root was not found");
    }
}
