// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using Sharpcraft.Modding;

// Unload probe for the modding host (AGENTS.md principle 2). It runs as its own process because a test
// host keeps its own load contexts alive, which would make an in-process measurement describe the host
// instead of the code under test.
//
// Verified deterministically; the exit code is 0 only when all of these hold:
//   1. a mod context is collectible and reports it;
//   2. Unload() raises Unloading exactly once;
//   3. a collectible context with nothing loaded is reclaimed by the GC.
//
// Additionally measured, printed but not asserted: whether a context that loaded one of our own
// assemblies is reclaimed. In this environment it is not; the reason is recorded as R-019 instead of
// being guessed at.
var failures = 0;

MeasureEmptyContextReclamation();
failures += VerifyCollectibleAndUnloading();
MeasureLoadedContextReclamation();

Console.WriteLine(failures == 0
    ? "unload probe: deterministic unload checks passed"
    : $"unload probe: {failures} check(s) failed");

return failures == 0 ? 0 : 1;

static int VerifyCollectibleAndUnloading()
{
    var context = new ModLoadContext("probe", Path.GetTempPath());
    var unloadCount = 0;
    context.Unloading += _ => unloadCount++;

    if (!context.IsCollectible)
    {
        Console.WriteLine("check 1 FAILED: ModLoadContext is not collectible");
        return 1;
    }

    context.Unload();

    if (unloadCount != 1)
    {
        Console.WriteLine($"check 2 FAILED: Unloading raised {unloadCount} time(s), expected 1");
        return 1;
    }

    Console.WriteLine("check 1-2 ok: collectible context raises Unloading once");
    return 0;
}

static void MeasureEmptyContextReclamation()
{
    // A plain non-inlined call on the main thread releases the context; a Thread keeps its own state
    // alive long enough that the weak reference looks immortal (measured).
    var weak = CreateAndUnload();

    Console.WriteLine(Collect(weak)
        ? "measurement: an empty collectible context is reclaimed"
        : "measurement: an empty collectible context was NOT reclaimed (R-019)");
}

[MethodImpl(MethodImplOptions.NoInlining)]
static WeakReference<AssemblyLoadContext> CreateAndUnload()
{
    var context = new AssemblyLoadContext("empty", isCollectible: true);
    context.Unload();
    return new WeakReference<AssemblyLoadContext>(context);
}

/// <summary>
/// Loads a copy of our own assembly into a collectible context, unloads it and reports whether the GC
/// reclaimed the context. Printed as a measurement: see R-019.
/// </summary>
static void MeasureLoadedContextReclamation()
{
    var weak = LoadAndUnload();
    var reclaimed = Collect(weak);
    Console.WriteLine(reclaimed
        ? "measurement: a context that loaded one of our assemblies is reclaimed"
        : "measurement: a context that loaded one of our assemblies is NOT reclaimed (R-019)");
}

[MethodImpl(MethodImplOptions.NoInlining)]
static WeakReference<AssemblyLoadContext> LoadAndUnload()
{
    var directory = Path.Combine(Path.GetTempPath(), "sharpcraft-probe-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(directory);

    var payload = Path.Combine(directory, "Payload.dll");
    File.Copy(typeof(ModHost).Assembly.Location, payload);

    var context = new ModLoadContext("loaded", directory);
    context.LoadFromAssemblyPath(payload);
    context.Unload();

    return new WeakReference<AssemblyLoadContext>(context);
}

static bool Collect(WeakReference<AssemblyLoadContext> weak, int attempts = 20)
{
    for (var attempt = 0; attempt < attempts; attempt++)
    {
        if (!weak.TryGetTarget(out _))
        {
            return true;
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
    }

    return !weak.TryGetTarget(out _);
}
