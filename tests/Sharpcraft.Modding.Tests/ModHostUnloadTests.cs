// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Reflection;
using Sharpcraft.Modding;
using Xunit;

namespace Sharpcraft.Modding.Tests;

/// <summary>
/// The unload guarantees from AGENTS.md principle 2 that can be asserted inside a test host: reference
/// discipline (no strong references to a load context) and deterministic failure handling. Actual GC
/// reclamation is measured by <c>Sharpcraft.Modding.UnloadProbe</c>, which runs as its own process — the
/// test host keeps contexts alive on its own, so an in-process assertion would describe the host rather
/// than the code (docs/modules/modding.md 5, R-019).
/// </summary>
public sealed class ModHostUnloadTests
{
    [Fact]
    public void TheHostKeepsOnlyWeakReferencesToContexts()
    {
        var contextFields = typeof(ModHost)
            .GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
            .Where(field => field.FieldType.ToString().Contains(nameof(ModLoadContext), StringComparison.Ordinal))
            .ToArray();

        Assert.NotEmpty(contextFields);
        Assert.All(contextFields, field =>
        {
            var elementType = field.FieldType.IsGenericType ? field.FieldType.GetGenericArguments()[0] : field.FieldType;
            Assert.True(
                elementType.IsGenericType && elementType.GetGenericTypeDefinition() == typeof(WeakReference<>),
                $"{field.Name} must hold weak references, found {field.FieldType}");
        });
    }

    [Fact]
    public void TheHandleHoldsOnlyWeakReferences()
    {
        foreach (var name in new[] { "Context", "Instance" })
        {
            var property = typeof(ModHandle).GetProperty(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(property);
            Assert.True(
                property!.PropertyType.IsGenericType &&
                property.PropertyType.GetGenericTypeDefinition() == typeof(WeakReference<>),
                $"ModHandle.{name} must be a WeakReference, found {property.PropertyType}");
        }
    }

    [Fact]
    public void AnAssemblyWithoutAnEntryPointIsRejectedAndItsContextIsUnloaded()
    {
        var directory = CreatePayloadDirectory();
        try
        {
            var host = new ModHost("26.3", _ => { });
            var exception = Assert.Throws<InvalidOperationException>(
                () => host.Load(Path.Combine(directory, "Payload.dll")));

            Assert.Contains(nameof(ISharpcraftMod), exception.Message, StringComparison.Ordinal);
            Assert.Equal(0, host.LoadedCount);
        }
        finally
        {
            DeleteQuietly(directory);
        }
    }

    [Fact]
    public void AMissingAssemblyIsReportedClearly()
    {
        var host = new ModHost("26.3", _ => { });
        var exception = Assert.Throws<InvalidOperationException>(
            () => host.Load(Path.Combine(Path.GetTempPath(), "definitely-missing.dll")));

        Assert.Contains("mod assembly not found", exception.Message, StringComparison.Ordinal);
    }

    private static string CreatePayloadDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "sharpcraft-mod-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        // Any assembly will do: these tests exercise loading and rejection, not the contract surface.
        File.Copy(typeof(ModHost).Assembly.Location, Path.Combine(directory, "Payload.dll"));
        return directory;
    }

    private static void DeleteQuietly(string directory)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                Directory.Delete(directory, recursive: true);
                return;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // A just-unloaded assembly can stay mapped for a moment; cleanup must not fail a test.
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
        }
    }
}
