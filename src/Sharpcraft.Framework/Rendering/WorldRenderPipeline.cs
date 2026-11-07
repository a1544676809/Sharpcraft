// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Framework.Rendering;

/// <summary>Stable names for the world render passes shared by windowed and offscreen rendering.</summary>
public enum WorldRenderPassKind
{
    Sky,
    TerrainOpaque,
    TerrainCutout,
    Entities,
    Translucent,
    WorldText,
    Clouds,
    Ui
}

/// <summary>One named stage of the world render pipeline.</summary>
public sealed record WorldRenderPass(string Name, WorldRenderPassKind Kind);

/// <summary>
/// Single source of truth for the canonical world pass order. This is a contract only; the actual pass extraction is
/// staged separately so the windowed and offscreen shells cannot silently diverge while the refactor is in progress.
/// </summary>
public static class WorldRenderPipeline
{
    public static readonly IReadOnlyList<WorldRenderPass> DefaultPasses =
    [
        new("sky", WorldRenderPassKind.Sky),
        new("terrain-opaque", WorldRenderPassKind.TerrainOpaque),
        new("terrain-cutout", WorldRenderPassKind.TerrainCutout),
        new("entities", WorldRenderPassKind.Entities),
        new("translucent", WorldRenderPassKind.Translucent),
        new("world-text", WorldRenderPassKind.WorldText),
        new("clouds", WorldRenderPassKind.Clouds),
        new("ui", WorldRenderPassKind.Ui)
    ];

    /// <summary>Validates a pass plan: unique names, unique kinds and the canonical order.</summary>
    public static void Validate(IReadOnlyList<WorldRenderPass> passes)
    {
        ArgumentNullException.ThrowIfNull(passes);
        if (passes.Count != DefaultPasses.Count)
        {
            throw new InvalidOperationException($"world pass count {passes.Count} != {DefaultPasses.Count}");
        }

        var names = new HashSet<string>(StringComparer.Ordinal);
        var kinds = new HashSet<WorldRenderPassKind>();
        for (var i = 0; i < passes.Count; i++)
        {
            var pass = passes[i];
            if (!names.Add(pass.Name) || !kinds.Add(pass.Kind) || pass.Kind != DefaultPasses[i].Kind)
            {
                throw new InvalidOperationException($"invalid world pass at index {i}: {pass.Name}/{pass.Kind}");
            }
        }
    }
}

/// <summary>Small recorder used by diagnostics/tests to assert actual pass order without touching GPU state.</summary>
public sealed class FramePassRecorder
{
    private readonly List<string> _passes = [];

    public IReadOnlyList<string> Passes => _passes;

    public void Record(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        _passes.Add(name);
    }

    public void Clear() => _passes.Clear();
}
