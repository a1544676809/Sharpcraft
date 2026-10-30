// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Reflection;

namespace Sharpcraft.Framework.Rendering.Shaders;

/// <summary>
/// Loads a pipeline's shader from the embedded <c>Shaders/pipelines</c> folder, expanding its includes.
/// </summary>
/// <remarks>
/// Shaders live as files so a directory listing tells you which pipelines exist, and so shared declarations are written once
/// (docs/specs/render/shader-organisation.md 1-3). They are embedded rather than read from disk so the renderer has no
/// runtime file dependency.
/// </remarks>
public static class ShaderLibrary
{
    private const string PipelinePrefix = "Sharpcraft.Framework.Rendering.Shaders.pipelines.";

    private static readonly Assembly Owner = typeof(ShaderLibrary).Assembly;

    /// <summary>Loads and composes one pipeline shader.</summary>
    /// <param name="pipeline">Pipeline name, matching the file name.</param>
    /// <exception cref="InvalidOperationException">The shader or an include is missing.</exception>
    public static string Load(string pipeline)
    {
        ArgumentException.ThrowIfNullOrEmpty(pipeline);

        var source = ReadRaw(PipelinePrefix + pipeline + ".wgsl")
            ?? throw new InvalidOperationException($"shader pipeline '{pipeline}' is not embedded");

        return ShaderIncludeComposer.Compose(pipeline, source, ResolveInclude);
    }

    /// <summary>Reads one embedded text resource by its suffix, for diagnostics.</summary>
    /// <param name="suffix">Resource name suffix.</param>
    public static string? ReadRaw(string suffix)
    {
        foreach (var name in Owner.GetManifestResourceNames())
        {
            if (name.EndsWith(suffix, StringComparison.Ordinal))
            {
                using var stream = Owner.GetManifestResourceStream(name);
                using var reader = new StreamReader(stream!);
                return reader.ReadToEnd();
            }
        }

        return null;
    }

    private static string? ResolveInclude(string path)
    {
        // Include paths are written as "include/x.wgsl"; the embedded name uses dots.
        var flat = path.Replace('/', '.');
        return ReadRaw("Shaders." + flat);
    }
}
