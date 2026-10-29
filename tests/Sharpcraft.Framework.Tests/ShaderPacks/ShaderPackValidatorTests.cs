// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using MinecraftCSharp = Sharpcraft;
using Sharpcraft.Framework.Rendering.ShaderPacks;
using Xunit;

namespace Sharpcraft.Framework.Tests.ShaderPacks;

/// <summary>
/// The shader pack contract (docs/specs/render/shader-packs.md): a pack is data, validation is a pure
/// function, an invalid pack never executes, and the built-in pipeline is expressible as the same data so
/// falling back is a substitution rather than a second code path.
/// </summary>
public sealed class ShaderPackValidatorTests
{
    [Fact]
    public void TheBuiltInPipelineIsExpressibleAsAPack()
    {
        var pack = BuiltIn();

        var validation = ShaderPackValidator.Validate(pack);

        Assert.True(validation.IsValid, string.Join("; ", validation.Diagnostics));
        Assert.Equal(["geometry"], validation.ExecutionOrder);
    }

    [Fact]
    public void PhasesRunInDependencyOrderNotDeclarationOrder()
    {
        var pack = BuiltIn() with
        {
            Targets = [new RenderTargetDescription("hdr", new TargetSize.Relative(1.0), "rgba16float")],
            Phases =
            [
                new PhaseDescription("tonemap", "tonemap.wgsl", ["framebuffer"], ["hdr"], ["geometry"], Final: true),
                new PhaseDescription("geometry", "geometry.wgsl", ["hdr"], [], [], Final: false)
            ]
        };

        var validation = ShaderPackValidator.Validate(pack);

        Assert.True(validation.IsValid, string.Join("; ", validation.Diagnostics));
        Assert.Equal(["geometry", "tonemap"], validation.ExecutionOrder);
    }

    [Fact]
    public void APackWithoutAFinalPhaseIsRejected()
    {
        var pack = BuiltIn() with
        {
            Phases = [new PhaseDescription("geometry", "geometry.wgsl", [], [], [], Final: false)]
        };

        var validation = ShaderPackValidator.Validate(pack);

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Diagnostics, diagnostic => diagnostic.Kind == PackDiagnosticKind.MissingFinalPhase);
        Assert.Empty(validation.ExecutionOrder);
    }

    [Fact]
    public void AnUndeclaredTargetIsRejectedWithItsName()
    {
        var pack = BuiltIn() with
        {
            Phases = [new PhaseDescription("geometry", "geometry.wgsl", ["shadow"], [], [], Final: true)]
        };

        var validation = ShaderPackValidator.Validate(pack);

        Assert.False(validation.IsValid);
        var diagnostic = Assert.Single(validation.Diagnostics, d => d.Kind == PackDiagnosticKind.UnknownTarget);
        Assert.Contains("shadow", diagnostic.Detail, StringComparison.Ordinal);
        Assert.Equal("geometry", diagnostic.Where);
    }

    [Fact]
    public void ADependencyCycleIsRejected()
    {
        var pack = BuiltIn() with
        {
            Targets = [new RenderTargetDescription("a", new TargetSize.Absolute(4, 4), "rgba8unorm")],
            Phases =
            [
                new PhaseDescription("first", "a.wgsl", ["a"], [], ["second"], Final: false),
                new PhaseDescription("second", "b.wgsl", [], [], ["first"], Final: true)
            ]
        };

        var validation = ShaderPackValidator.Validate(pack);

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Diagnostics, d => d.Kind == PackDiagnosticKind.CyclicDependency);
    }

    [Fact]
    public void AnUnknownUniformIsRejectedRatherThanIgnored()
    {
        var pack = BuiltIn() with { Uniforms = ["viewProjection", "sunDirection"] };

        var validation = ShaderPackValidator.Validate(pack);

        Assert.False(validation.IsValid);
        var diagnostic = Assert.Single(validation.Diagnostics, d => d.Kind == PackDiagnosticKind.UnknownUniform);
        Assert.Contains("sunDirection", diagnostic.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void DuplicateTargetNamesAreRejected()
    {
        var pack = BuiltIn() with
        {
            Targets =
            [
                new RenderTargetDescription("hdr", new TargetSize.Relative(1.0), "rgba16float"),
                new RenderTargetDescription("hdr", new TargetSize.Relative(0.5), "rgba16float")
            ]
        };

        var validation = ShaderPackValidator.Validate(pack);

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Diagnostics, d => d.Kind == PackDiagnosticKind.DuplicateName);
    }

    [Fact]
    public void RelativeTargetsScaleWithTheFramebufferAndNeverCollapse()
    {
        var half = new TargetSize.Relative(0.5);
        var tiny = new TargetSize.Relative(0.0001);

        Assert.Equal((960, 540), half.Resolve(1920, 1080));
        Assert.Equal((1, 1), tiny.Resolve(1920, 1080));
        Assert.Equal((2048, 2048), new TargetSize.Absolute(2048, 2048).Resolve(1920, 1080));
    }

    /// <summary>The engine's own pipeline expressed as pack data; fallback substitutes this one.</summary>
    internal static ShaderPackDescription BuiltIn() =>
        new(
            "builtin",
            Targets: [],
            Phases: [new PhaseDescription("geometry", "geometry.wgsl", ["framebuffer"], [], [], Final: true)],
            Uniforms: ["viewProjection"]);
}
