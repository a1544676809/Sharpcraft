// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Reflection;
using System.Xml.Linq;
using Xunit;

namespace Sharpcraft.Architecture.Tests;

/// <summary>
/// Enforces the side-boundary rules from docs/architecture/boundaries.md (B1-B3) at build time.
///
/// Two sources of truth are combined on purpose:
///   1. IL-level references (<see cref="Assembly.GetReferencedAssemblies"/>) prove actual coupling.
///   2. Declared ProjectReferences in the .csproj prove intent, and catch a violation before any
///      code consumes it (a project reference alone emits no IL reference, so it would silently pass).
/// </summary>
public sealed class AssemblyBoundaryTests
{
    private const string Client = "Sharpcraft.Client";
    private const string Server = "Sharpcraft.Server";
    private const string Common = "Sharpcraft.Common";
    private const string Framework = "Sharpcraft.Framework";
    private const string Auth = "Sharpcraft.Auth";
    private const string AuthLib = "Sharpcraft.AuthLib";
    private const string Launcher = "Sharpcraft.Launcher";
    private const string DocsIndexer = "Sharpcraft.Docs.Indexer";
    private const string Assets = "Sharpcraft.Assets";

    [Theory]
    [InlineData(Client, Server)]
    [InlineData(Server, Client)]
    public void SidesMustNotReferenceEachOther(string assemblyName, string forbidden)
    {
        Assert.DoesNotContain(forbidden, DependenciesOf(assemblyName));
    }

    /// <summary>
    /// ADR-0026: the client is a standalone game executable. It must not depend on the launcher (host) or the
    /// authentication library (host plumbing); it receives its identity through the command line/session file.
    /// </summary>
    [Theory]
    [InlineData(Client, Launcher)]
    [InlineData(Client, Auth)]
    public void ClientMustNotReferenceHostOrAuth(string assemblyName, string forbidden)
    {
        Assert.DoesNotContain(forbidden, DependenciesOf(assemblyName));
    }

    /// <summary>
    /// The neutral auth/profile library is shared by the game sides and the host; it must stay below all of them.
    /// </summary>
    [Theory]
    [InlineData(AuthLib, Client)]
    [InlineData(AuthLib, Server)]
    [InlineData(AuthLib, Framework)]
    [InlineData(AuthLib, Launcher)]
    [InlineData(AuthLib, Auth)]
    public void NeutralAuthLibMustNotReferenceGameOrHostAssemblies(string assemblyName, string forbidden)
    {
        Assert.DoesNotContain(forbidden, DependenciesOf(assemblyName));
    }

    [Fact]
    public void LauncherMustNotReferenceClient()
    {
        // Product launch spawns the client executable; the launcher must not depend on the client assembly.
        Assert.DoesNotContain(Client, DependenciesOf(Launcher));
    }

    [Theory]
    [InlineData(Common, Client)]
    [InlineData(Common, Server)]
    [InlineData(Common, Launcher)]
    [InlineData(Common, Auth)]
    [InlineData(Common, AuthLib)]
    [InlineData(Common, Framework)]
    [InlineData(Common, Assets)]
    public void CommonMustNotReferenceUpperLayers(string assemblyName, string forbidden)
    {
        Assert.DoesNotContain(forbidden, DependenciesOf(assemblyName));
    }

    /// <summary>
    /// ADR-0027: the low-level asset library depends only on Common. It must never pull the engine, the game, or the host
    /// back in, otherwise the launcher could not reuse it without also loading a game assembly.
    /// </summary>
    [Theory]
    [InlineData(Assets, Framework)]
    [InlineData(Assets, Client)]
    [InlineData(Assets, Launcher)]
    [InlineData(Assets, Auth)]
    [InlineData(Assets, AuthLib)]
    [InlineData(Assets, Server)]
    public void AssetsMustNotReferenceUpperLayers(string assemblyName, string forbidden)
    {
        Assert.DoesNotContain(forbidden, DependenciesOf(assemblyName));
    }

    /// <summary>
    /// Engineering tooling is not part of the product: game code must never depend on it, and the
    /// tool must stay free of product assemblies so it keeps building on its own (ADR-0008).
    /// </summary>
    [Theory]
    [InlineData(Client, DocsIndexer)]
    [InlineData(Server, DocsIndexer)]
    [InlineData(Common, DocsIndexer)]
    [InlineData(Framework, DocsIndexer)]
    [InlineData(Auth, DocsIndexer)]
    [InlineData(AuthLib, DocsIndexer)]
    [InlineData(Launcher, DocsIndexer)]
    public void ProductAssembliesMustNotReferenceEngineeringTooling(string assemblyName, string forbidden)
    {
        Assert.DoesNotContain(forbidden, DependenciesOf(assemblyName));
    }

    [Fact]
    public void EngineeringToolingMustNotReferenceProductAssemblies()
    {
        var dependencies = DependenciesOf(DocsIndexer);
        Assert.DoesNotContain(dependencies, name => name.StartsWith("Sharpcraft.", StringComparison.Ordinal));
    }

    [Fact]
    public void FrameworkMustNotReferenceGameOrHostAssemblies()
    {
        var dependencies = DependenciesOf(Framework);
        Assert.DoesNotContain(dependencies, name => name.StartsWith("Sharpcraft.", StringComparison.Ordinal));
    }

    /// <summary>
    /// The authentication library is host plumbing: it must not pull the engine (WGPU, windowing)
    /// into every consumer. Only host components that actually render (the launcher's frame and
    /// window commands) may reference the engine (boundaries.md R7).
    /// </summary>
    [Fact]
    public void HostLibrariesMustNotReferenceTheEngine()
    {
        Assert.DoesNotContain(Framework, DependenciesOf(Auth));
    }

    [Theory]
    [InlineData(Server, Framework)]
    [InlineData(Server, Auth)]
    [InlineData(Server, Launcher)]
    public void ServerMustNotReferenceHostOrEngineAssemblies(string assemblyName, string forbidden)
    {
        Assert.DoesNotContain(forbidden, DependenciesOf(assemblyName));
    }

    /// <summary>IL references plus declared project references, deduplicated.</summary>
    private static string[] DependenciesOf(string assemblyName)
    {
        var ilReferences = IlReferencesOf(assemblyName);
        var declared = DeclaredProjectReferencesOf(assemblyName);
        return ilReferences.Concat(declared).Distinct(StringComparer.Ordinal).ToArray();
    }

    private static string[] IlReferencesOf(string assemblyName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, assemblyName + ".dll");
        Assert.True(File.Exists(path), $"assembly '{assemblyName}' was not copied to the test output: {path}");

        return Assembly.LoadFrom(path)
            .GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .ToArray();
    }

    private static string[] DeclaredProjectReferencesOf(string assemblyName)
    {
        // Product assemblies live under src/, engineering tooling under tools/.
        var root = RepositoryRoot();
        var projectFile = Path.Combine(root, "src", assemblyName, assemblyName + ".csproj");
        if (!File.Exists(projectFile))
        {
            projectFile = Path.Combine(root, "tools", assemblyName, assemblyName + ".csproj");
        }

        Assert.True(File.Exists(projectFile), $"project file not found: {projectFile}");

        return XDocument.Load(projectFile)
            .Descendants()
            .Where(element => element.Name.LocalName == "ProjectReference")
            .Select(element => element.Attribute("Include")?.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => Path.GetFileNameWithoutExtension(value!.Replace('\\', Path.DirectorySeparatorChar)))
            .ToArray();
    }

    /// <summary>Walks up from the test output directory to the repository root.</summary>
    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Sharpcraft.slnx")) &&
                File.Exists(Path.Combine(directory.FullName, "Directory.Build.props")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException($"repository root not found above {AppContext.BaseDirectory}");
    }
}
