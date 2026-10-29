// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Framework.Rendering.ShaderPacks;

/// <summary>
/// Validates a pack before anything is executed and produces the phase order. Pure functions only, so the
/// rules are testable without a GPU and a rejected pack can never half-run
/// (docs/specs/render/shader-packs.md 3, 5).
/// </summary>
public static class ShaderPackValidator
{
    /// <summary>Name of the implicit target that is the swapchain image.</summary>
    public const string FramebufferTarget = "framebuffer";

    /// <summary>Validates a pack and computes its execution order.</summary>
    /// <param name="pack">Pack to validate.</param>
    public static PackValidation Validate(ShaderPackDescription pack)
    {
        ArgumentNullException.ThrowIfNull(pack);

        var diagnostics = new List<PackDiagnostic>();

        if (string.IsNullOrWhiteSpace(pack.Name))
        {
            diagnostics.Add(new PackDiagnostic(PackDiagnosticKind.MissingName, null, "pack has no name"));
        }

        // Targets: names must be unique and sizes usable.
        var targets = new HashSet<string>(StringComparer.Ordinal);
        foreach (var target in pack.Targets)
        {
            if (!targets.Add(target.Name))
            {
                diagnostics.Add(new PackDiagnostic(
                    PackDiagnosticKind.DuplicateName,
                    target.Name,
                    "target declared more than once"));
            }

            if (target.Size is TargetSize.Relative { Scale: <= 0 })
            {
                diagnostics.Add(new PackDiagnostic(
                    PackDiagnosticKind.InvalidSize,
                    target.Name,
                    "relative target scale must be positive"));
            }
        }

        // Phases: names unique, targets known, no duplicate writes.
        var phases = new Dictionary<string, PhaseDescription>(StringComparer.Ordinal);
        foreach (var phase in pack.Phases)
        {
            if (!phases.TryAdd(phase.Name, phase))
            {
                diagnostics.Add(new PackDiagnostic(
                    PackDiagnosticKind.DuplicateName,
                    phase.Name,
                    "phase declared more than once"));
            }
        }

        foreach (var phase in pack.Phases)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var written in phase.Writes)
            {
                if (!seen.Add(written))
                {
                    diagnostics.Add(new PackDiagnostic(
                        PackDiagnosticKind.DuplicateWrite,
                        phase.Name,
                        $"writes '{written}' twice"));
                }

                // The framebuffer is implicit; every other name must be declared.
                if (!targets.Contains(written) && written != FramebufferTarget)
                {
                    diagnostics.Add(new PackDiagnostic(
                        PackDiagnosticKind.UnknownTarget,
                        phase.Name,
                        $"writes undeclared target '{written}'"));
                }
            }

            foreach (var read in phase.Reads)
            {
                if (!targets.Contains(read))
                {
                    diagnostics.Add(new PackDiagnostic(
                        PackDiagnosticKind.UnknownTarget,
                        phase.Name,
                        $"reads undeclared target '{read}'"));
                }
            }

            foreach (var dependency in phase.After)
            {
                if (!phases.ContainsKey(dependency))
                {
                    diagnostics.Add(new PackDiagnostic(
                        PackDiagnosticKind.UnknownDependency,
                        phase.Name,
                        $"depends on unknown phase '{dependency}'"));
                }
            }
        }

        var finals = pack.Phases.Where(phase => phase.Final).ToArray();
        if (finals.Length == 0)
        {
            diagnostics.Add(new PackDiagnostic(
                PackDiagnosticKind.MissingFinalPhase,
                null,
                "no phase writes the framebuffer"));
        }
        else if (finals.Length > 1)
        {
            diagnostics.Add(new PackDiagnostic(
                PackDiagnosticKind.MultipleFinalPhases,
                finals[1].Name,
                $"{finals.Length} phases claim the framebuffer"));
        }

        foreach (var uniform in pack.Uniforms)
        {
            if (!KnownUniforms.All.Contains(uniform, StringComparer.Ordinal))
            {
                diagnostics.Add(new PackDiagnostic(
                    PackDiagnosticKind.UnknownUniform,
                    null,
                    $"uniform '{uniform}' is not provided by the engine"));
            }
        }

        if (diagnostics.Count > 0)
        {
            return new PackValidation(diagnostics, []);
        }

        var order = TopologicalOrder(pack.Phases, out var cyclic);
        if (cyclic)
        {
            diagnostics.Add(new PackDiagnostic(
                PackDiagnosticKind.CyclicDependency,
                null,
                "phase dependencies form a cycle"));
            return new PackValidation(diagnostics, []);
        }

        return new PackValidation([], order);
    }

    /// <summary>
    /// Stable topological sort: phases without unmet dependencies keep their written order, which makes the
    /// resulting pipeline reproducible across runs.
    /// </summary>
    private static List<string> TopologicalOrder(IReadOnlyList<PhaseDescription> phases, out bool cyclic)
    {
        var remaining = phases.ToList();
        var emitted = new List<string>(phases.Count);
        var done = new HashSet<string>(StringComparer.Ordinal);
        cyclic = false;

        while (remaining.Count > 0)
        {
            var progressed = false;

            foreach (var phase in remaining.ToArray())
            {
                if (phase.After.All(done.Contains))
                {
                    emitted.Add(phase.Name);
                    done.Add(phase.Name);
                    remaining.Remove(phase);
                    progressed = true;
                }
            }

            if (!progressed)
            {
                cyclic = true;
                return [];
            }
        }

        return emitted;
    }
}
