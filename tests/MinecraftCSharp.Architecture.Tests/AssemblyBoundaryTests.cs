// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 MinecraftCSharp contributors

using System.Reflection;
using System.Xml.Linq;
using Xunit;

namespace MinecraftCSharp.Architecture.Tests;

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
    private const string Client = "MinecraftCSharp.Client";
    private const string Server = "MinecraftCSharp.Server";
    private const string Common = "MinecraftCSharp.Common";
    private const string Framework = "MinecraftCSharp.Framework";
    private const string Auth = "MinecraftCSharp.Auth";
    private const string Launcher = "MinecraftCSharp.Launcher";

    [Theory]
    [InlineData(Client, Server)]
    [InlineData(Server, Client)]
    public void SidesMustNotReferenceEachOther(string assemblyName, string forbidden)
    {
        Assert.DoesNotContain(forbidden, DependenciesOf(assemblyName));
    }

    [Theory]
    [InlineData(Common, Client)]
    [InlineData(Common, Server)]
    [InlineData(Common, Launcher)]
    [InlineData(Common, Auth)]
    [InlineData(Common, Framework)]
    public void CommonMustNotReferenceUpperLayers(string assemblyName, string forbidden)
    {
        Assert.DoesNotContain(forbidden, DependenciesOf(assemblyName));
    }

    [Fact]
    public void FrameworkMustNotReferenceGameOrHostAssemblies()
    {
        var dependencies = DependenciesOf(Framework);
        Assert.DoesNotContain(dependencies, name => name.StartsWith("MinecraftCSharp.", StringComparison.Ordinal));
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
        var projectFile = Path.Combine(RepositoryRoot(), "src", assemblyName, assemblyName + ".csproj");
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
            if (File.Exists(Path.Combine(directory.FullName, "MinecraftCSharp.slnx")) &&
                File.Exists(Path.Combine(directory.FullName, "Directory.Build.props")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException($"repository root not found above {AppContext.BaseDirectory}");
    }
}
