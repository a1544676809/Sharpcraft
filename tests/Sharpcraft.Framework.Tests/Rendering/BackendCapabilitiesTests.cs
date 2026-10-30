// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Framework.Rendering;
using Xunit;

namespace Sharpcraft.Framework.Tests.Rendering;

/// <summary>
/// Backend capability negotiation (docs/specs/render/backend-interface.md): a pack that requires a stage this backend does
/// not have must be refused with the missing names, not compiled halfway.
/// </summary>
public sealed class BackendCapabilitiesTests
{
    [Fact]
    public void TheDefaultBackendHasComputeButNoGeometryOrTessellation()
    {
        var capabilities = BackendCapabilities.Default;

        Assert.True(capabilities.Supports(GraphicsCapability.Compute));
        Assert.False(capabilities.Supports(GraphicsCapability.GeometryShader));
        Assert.False(capabilities.Supports(GraphicsCapability.TessellationShader));
        Assert.False(capabilities.Supports(GraphicsCapability.RayTracingPipeline));
    }

    [Fact]
    public void MissingNamesEveryUnsupportedRequirement()
    {
        var missing = BackendCapabilities.Default.Missing(
            GraphicsCapability.Compute | GraphicsCapability.TessellationShader | GraphicsCapability.MeshShader);

        Assert.Equal(2, missing.Count);
        Assert.Contains("TessellationShader", missing);
        Assert.Contains("MeshShader", missing);
    }

    [Fact]
    public void ASupportedRequirementHasNothingMissing()
    {
        Assert.Empty(BackendCapabilities.Default.Missing(GraphicsCapability.Compute));
    }
}
