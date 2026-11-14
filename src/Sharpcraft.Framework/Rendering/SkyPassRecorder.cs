// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;

namespace Sharpcraft.Framework.Rendering;

/// <summary>
/// Records the sky part of the scene pass: the gradient backdrop, then the sun, moon, stars, sunrise band and
/// the sky disc — all before terrain, because terrain must draw over them.
/// </summary>
/// <remarks>
/// The order here is not arbitrary. The camera delta is applied <em>after</em> the per-frame draw rebuild,
/// because a freshly built draw uses the initial camera model and translating first would be overwritten, which
/// left the bodies stuck at the world origin. The vanilla sky disc updates its own uniform before drawing for
/// the same reason.
/// <para>
/// The stage timings are injected as one delegate bound at construction, so recording the sky costs no
/// allocation per frame and the diagnostic stays where the loop can remove it wholesale (principle 13).
/// </para>
/// </remarks>
internal sealed class SkyPassRecorder
{
    private readonly IRenderBackend _backend;
    private readonly PipelineHandle _skyPipeline;
    private readonly OffscreenRenderer.CelestialResources? _celestial;
    private readonly OffscreenRenderer.CelestialColorResources? _celestialColour;
    private readonly OffscreenRenderer.CelestialColorResources? _skyDiscColour;
    private readonly SkyDiscResources? _skyDiscVanilla;
    private readonly Func<(CelestialQuadDraw[] Draws, CelestialColorDraw[] Colors, CelestialColorDraw[] SkyDiscs)?>? _celestialPerFrame;
    private readonly Func<(Vector3 Sky, Vector3 Fog, float SkyFogEnd, Vector3 Eye)>? _skyDiscPerFrame;
    private readonly FrameCpuTimings _cpu;

    /// <summary>Captures the pipelines, bodies and callbacks the sky draws need.</summary>
    /// <param name="backend">Backend to record through.</param>
    /// <param name="skyPipeline">Gradient backdrop pipeline.</param>
    /// <param name="celestial">Textured sun and moon, or null when the request has none.</param>
    /// <param name="celestialColour">Sunrise band and stars, or null when the request has none.</param>
    /// <param name="skyDiscColour">Colour-only sky disc used when the vanilla disc is off.</param>
    /// <param name="skyDiscVanilla">Per-pixel sky disc used when the vanilla disc is on.</param>
    /// <param name="celestialPerFrame">Rebuilds the celestial draws each frame, or null to keep them.</param>
    /// <param name="skyDiscPerFrame">Supplies the sky disc state each frame, or null to skip the disc.</param>
    /// <param name="cpu">Stage timing sink, bound once so recording allocates nothing.</param>
    public SkyPassRecorder(
        IRenderBackend backend,
        PipelineHandle skyPipeline,
        OffscreenRenderer.CelestialResources? celestial,
        OffscreenRenderer.CelestialColorResources? celestialColour,
        OffscreenRenderer.CelestialColorResources? skyDiscColour,
        SkyDiscResources? skyDiscVanilla,
        Func<(CelestialQuadDraw[] Draws, CelestialColorDraw[] Colors, CelestialColorDraw[] SkyDiscs)?>? celestialPerFrame,
        Func<(Vector3 Sky, Vector3 Fog, float SkyFogEnd, Vector3 Eye)>? skyDiscPerFrame,
        FrameCpuTimings cpu)
    {
        _backend = backend ?? throw new ArgumentNullException(nameof(backend));
        _skyPipeline = skyPipeline;
        _celestial = celestial;
        _celestialColour = celestialColour;
        _skyDiscColour = skyDiscColour;
        _skyDiscVanilla = skyDiscVanilla;
        _celestialPerFrame = celestialPerFrame;
        _skyDiscPerFrame = skyDiscPerFrame;
        _cpu = cpu ?? throw new ArgumentNullException(nameof(cpu));
    }

    /// <summary>Records the sky and everything that lives on it.</summary>
    /// <param name="pass">Scene pass being recorded.</param>
    /// <param name="skipSky">Host asked to leave the sky out.</param>
    /// <param name="skipCelestial">Host asked to leave the bodies out.</param>
    /// <param name="skipGradient">The host supplies a flat sky colour through the clear colour.</param>
    /// <param name="cameraDelta">How far the eye moved since the request was built.</param>
    public void Record(
        RenderPassHandle pass,
        bool skipSky,
        bool skipCelestial,
        bool skipGradient,
        Vector3 cameraDelta)
    {
        if (skipSky)
        {
            return;
        }

        // Sky first, over the cleared target; world geometry then draws on top of it.
        if (!skipGradient)
        {
            _backend.SetPipeline(pass, _skyPipeline);
            _backend.Draw(pass, 3, 1, 0, 0);
        }

        _cpu.Mark(FrameCpuTimings.SceneSkyStage);

        if (skipCelestial)
        {
            _cpu.Mark(FrameCpuTimings.SceneCelestialStage);
            return;
        }

        if (_celestialPerFrame?.Invoke() is { } update)
        {
            _celestial?.SetDraws(update.Draws);
            _celestialColour?.SetDraws(update.Colors);
            _skyDiscColour?.SetDraws(update.SkyDiscs);
        }

        _cpu.Mark(FrameCpuTimings.CelestialBuildStage);

        // Applied after the rebuild above: a freshly built draw carries the initial camera model, so
        // translating first would be overwritten and the bodies would sit at the origin.
        _celestial?.UpdateCamera(cameraDelta);
        _celestialColour?.UpdateCamera(cameraDelta);
        _skyDiscColour?.UpdateCamera(cameraDelta);

        _cpu.Mark(FrameCpuTimings.CelestialCameraStage);

        if (_skyDiscVanilla is not null && _skyDiscPerFrame?.Invoke() is { } disc)
        {
            _skyDiscVanilla.Update(disc.Sky, disc.Fog, disc.SkyFogEnd, disc.Eye);

            _cpu.Mark(FrameCpuTimings.CelestialDiscStage);
            _skyDiscVanilla.Draw(_backend, pass);
        }

        _skyDiscColour?.Draw(_backend, pass);
        _celestial?.Draw(_backend, pass);
        _celestialColour?.Draw(_backend, pass);

        _cpu.Mark(FrameCpuTimings.SceneCelestialStage);
    }
}
