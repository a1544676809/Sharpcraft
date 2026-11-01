// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;

namespace Sharpcraft.Common.World;

/// <summary>A cubic-bezier easing identical to the reference EasingType.CubicBezier (docs/modules/time.md 5).</summary>
public readonly struct CubicBezierEasing
{
    private readonly float _x1;
    private readonly float _y1;
    private readonly float _x2;
    private readonly float _y2;

    public CubicBezierEasing(float x1, float y1, float x2, float y2)
    {
        _x1 = x1;
        _y1 = y1;
        _x2 = x2;
        _y2 = y2;
    }

    /// <summary>symmetricCubicBezier(x1,y1) = cubicBezier(x1,y1,1-x1,1-y1).</summary>
    public static CubicBezierEasing Symmetric(float x1, float y1) => new(x1, y1, 1f - x1, 1f - y1);

    public float Apply(float x)
    {
        var xCurve = Curve(_x1, _x2);
        var yCurve = Curve(_y1, _y2);
        return Sample(yCurve, SolveT(xCurve, x));
    }

    private static CubicCurve Curve(float v1, float v2) =>
        new((3f * v1) - (3f * v2) + 1f, (-6f * v1) + (3f * v2), 3f * v1);

    private static float Sample(CubicCurve curve, float t) => (((curve.A * t) + curve.B) * t + curve.C) * t;

    private static float Gradient(CubicCurve curve, float t) => ((3f * curve.A * t) + (2f * curve.B)) * t + curve.C;

    private static float SolveT(CubicCurve xCurve, float x)
    {
        var t = x;

        for (var i = 0; i < 4; i++)
        {
            var error = Sample(xCurve, t) - x;
            if (MathF.Abs(error) < 1e-5f)
            {
                return t;
            }

            var gradient = Gradient(xCurve, t);
            if (gradient < 1e-5f)
            {
                break;
            }

            t -= Math.Clamp(error / gradient, -0.25f, 0.25f);
        }

        var low = 0f;
        var high = 1f;
        var current = t;
        while (low < high)
        {
            var error = Sample(xCurve, current) - x;
            if (MathF.Abs(error) < 1e-5f)
            {
                return current;
            }

            if (error < 0f)
            {
                low = current;
            }
            else
            {
                high = current;
            }

            current = (high + low) / 2f;
        }

        return current;
    }

    private readonly record struct CubicCurve(float A, float B, float C);
}

/// <summary>One float keyframe: tick within the period and the value there.</summary>
public readonly record struct FloatKeyframe(int Ticks, float Value);

/// <summary>
/// A float keyframe track sampled exactly like the reference KeyframeTrackSampler: period wrap, wrap segments across the
/// period boundary, and <c>lerp(from, to, ease(alpha))</c> inside each segment (docs/modules/time.md 5).
/// </summary>
public sealed class FloatKeyframeTrack
{
    private readonly Segment[] _segments;
    private readonly int _periodTicks;

    private FloatKeyframeTrack(Segment[] segments, int periodTicks)
    {
        _segments = segments;
        _periodTicks = periodTicks;
    }

    public static FloatKeyframeTrack Build(IReadOnlyList<FloatKeyframe> keyframes, CubicBezierEasing easing, int periodTicks) =>
        Build(keyframes, easing.Apply, periodTicks);

    public static FloatKeyframeTrack Build(IReadOnlyList<FloatKeyframe> keyframes, Func<float, float> easing, int periodTicks)
    {
        ArgumentNullException.ThrowIfNull(keyframes);
        if (keyframes.Count == 0)
        {
            throw new ArgumentException("track needs at least one keyframe", nameof(keyframes));
        }

        if (keyframes.Count == 1)
        {
            var only = keyframes[0].Value;
            return new FloatKeyframeTrack([new Segment(easing, only, 0, only, 0)], periodTicks);
        }

        var first = keyframes[0];
        var last = keyframes[^1];
        var segments = new List<Segment>(keyframes.Count + 1)
        {
            new(easing, last.Value, last.Ticks - periodTicks, first.Value, first.Ticks)
        };

        for (var i = 0; i < keyframes.Count - 1; i++)
        {
            segments.Add(new Segment(easing, keyframes[i].Value, keyframes[i].Ticks, keyframes[i + 1].Value, keyframes[i + 1].Ticks));
        }

        segments.Add(new Segment(easing, last.Value, last.Ticks, first.Value, first.Ticks + periodTicks));
        return new FloatKeyframeTrack(segments.ToArray(), periodTicks);
    }

    public float Sample(long ticks)
    {
        var sampleTicks = (int)(((ticks % _periodTicks) + _periodTicks) % _periodTicks);
        var segment = GetSegmentAt(sampleTicks);

        if (sampleTicks <= segment.FromTicks)
        {
            return segment.FromValue;
        }

        if (sampleTicks >= segment.ToTicks)
        {
            return segment.ToValue;
        }

        var alpha = (float)(sampleTicks - segment.FromTicks) / (segment.ToTicks - segment.FromTicks);
        return segment.FromValue + (segment.Ease(alpha) * (segment.ToValue - segment.FromValue));
    }

    private Segment GetSegmentAt(int ticks)
    {
        foreach (var segment in _segments)
        {
            if (ticks < segment.ToTicks)
            {
                return segment;
            }
        }

        return _segments[^1];
    }

    private readonly record struct Segment(Func<float, float> Ease, float FromValue, int FromTicks, float ToValue, int ToTicks);
}

/// <summary>
/// Other overworld-day environment attributes from Timelines.java that are plain (linear) keyframe tracks. Values are the
/// effective attribute values (modifier tracks on the default bases), documented per member.
/// </summary>
public static class SkyAttributeTracks
{
    private const int Period = SkyTimelines.PeriodTicks;

    // STAR_BRIGHTNESS: base 0.0 with FloatModifier.MAXIMUM track, so the sampled track is the value.
    private static readonly FloatKeyframeTrack StarBrightness = FloatKeyframeTrack.Build(
    [
        new FloatKeyframe(92, 0.037f), new FloatKeyframe(627, 0f), new FloatKeyframe(11373, 0f),
        new FloatKeyframe(11732, 0.016f), new FloatKeyframe(11959, 0.044f), new FloatKeyframe(12399, 0.143f),
        new FloatKeyframe(12729, 0.258f), new FloatKeyframe(13228, 0.5f), new FloatKeyframe(22772, 0.5f),
        new FloatKeyframe(23032, 0.364f), new FloatKeyframe(23356, 0.225f), new FloatKeyframe(23758, 0.101f)
    ], static x => x, Period);

    // SKY_LIGHT_FACTOR: default base 1.0 with FloatModifier.MULTIPLY track (730/11270 = 1.0, 13140/22860 = 0.24).
    private static readonly FloatKeyframeTrack SkyLightFactorTrack = FloatKeyframeTrack.Build(
    [
        new FloatKeyframe(730, 1f), new FloatKeyframe(11270, 1f), new FloatKeyframe(13140, 0.24f), new FloatKeyframe(22860, 0.24f)
    ], static x => x, Period);

    public static float StarBrightnessAt(int dayTimeTicks) => StarBrightness.Sample(dayTimeTicks);

    /// <summary>Star brightness at a sub-tick fraction, linear between the previous and current tick (probe semantics).</summary>
    public static float StarBrightnessAt(int dayTimeTicks, float partialTick)
    {
        var t = Math.Clamp(partialTick, 0f, 1f);
        var previous = StarBrightness.Sample(dayTimeTicks - 1);
        var current = StarBrightness.Sample(dayTimeTicks);
        return previous + ((current - previous) * t);
    }

    public static float SkyLightFactorAt(int dayTimeTicks) => SkyLightFactorTrack.Sample(dayTimeTicks);

    /// <summary>Moon phase index 0..7 from the moon timeline (period 24000*8, one phase per 24000 ticks).</summary>
    public static int MoonPhaseIndex(long totalTicks)
    {
        const int moonPeriod = Period * 8;
        var ticks = ((totalTicks % moonPeriod) + moonPeriod) % moonPeriod;
        return (int)(ticks / Period);
    }

    private static float SrgbToLinear(int channel)
    {
        var x = channel / 255f;
        return x >= 0.04045f ? MathF.Pow((x + 0.055f) / 1.055f, 2.4f) : x / 12.92f;
    }

    // SUNRISE_SUNSET_COLOR keyframes (tick, ARGB32) from Timelines.java; decoded per channel to linear RGB + linear alpha.
    private static readonly (int Tick, int Argb)[] SunriseRaw =
    [
        (71, 1609540403), (310, 703969843), (565, 117167155), (730, 16770355), (11270, 16770355),
        (11397, 83679283), (11522, 268028723), (11690, 703969843), (11929, 1609540403), (12243, -1310226637),
        (12358, -857440717), (12512, -371166669), (12613, -153261261), (12732, -19242189), (12841, -19440589),
        (13035, -321760973), (13252, -1043577037), (13775, 918435635), (13888, 532362547), (14039, 163001139),
        (14192, 11744051), (21807, 11678515), (21961, 163001139), (22112, 532362547), (22225, 918435635),
        (22748, -1043577037), (22965, -321760973), (23159, -19440589), (23272, -19242189), (23488, -371166669),
        (23642, -857440717), (23757, -1310226637)
    ];

    private static FloatKeyframeTrack SunriseChannel(Func<int, float> select) =>
        FloatKeyframeTrack.Build(
            SunriseRaw.Select(k => new FloatKeyframe(k.Tick, select(k.Argb))).ToArray(),
            static x => x,
            Period);

    private static readonly FloatKeyframeTrack SunriseR = SunriseChannel(a => SrgbToLinear((a >> 16) & 0xFF));
    private static readonly FloatKeyframeTrack SunriseG = SunriseChannel(a => SrgbToLinear((a >> 8) & 0xFF));
    private static readonly FloatKeyframeTrack SunriseB = SunriseChannel(a => SrgbToLinear(a & 0xFF));
    private static readonly FloatKeyframeTrack SunriseA = SunriseChannel(a => ((a >> 24) & 0xFF) / 255f);

    public static Vector4 SunriseSunsetColorAt(int dayTimeTicks) =>
        new(SunriseR.Sample(dayTimeTicks), SunriseG.Sample(dayTimeTicks), SunriseB.Sample(dayTimeTicks), SunriseA.Sample(dayTimeTicks));

    /// <summary>Sunrise/sunset colour at a sub-tick fraction, linear between the previous and current tick.</summary>
    public static Vector4 SunriseSunsetColorAt(int dayTimeTicks, float partialTick)
    {
        var t = Math.Clamp(partialTick, 0f, 1f);
        return Vector4.Lerp(SunriseSunsetColorAt(dayTimeTicks - 1), SunriseSunsetColorAt(dayTimeTicks), t);
    }

    // SKY_LIGHT_COLOR: white * ColorModifier.MULTIPLY_RGB track (730/11270 white, 13140/22860 = NIGHT_SKY_LIGHT_COLOR).
    private const int NightSkyChannel = 122; // as8BitChannel(0.48)
    private static readonly float NightSky = SrgbToLinear(NightSkyChannel);

    private static readonly FloatKeyframeTrack SkyLightColorR = FloatKeyframeTrack.Build(
        [new FloatKeyframe(730, 1f), new FloatKeyframe(11270, 1f), new FloatKeyframe(13140, NightSky), new FloatKeyframe(22860, NightSky)], static x => x, Period);

    private static readonly FloatKeyframeTrack SkyLightColorB = FloatKeyframeTrack.Build(
        [new FloatKeyframe(730, 1f), new FloatKeyframe(11270, 1f), new FloatKeyframe(13140, 1f), new FloatKeyframe(22860, 1f)], static x => x, Period);

    public static Vector3 SkyLightColorAt(int dayTimeTicks) =>
        new(SkyLightColorR.Sample(dayTimeTicks), SkyLightColorR.Sample(dayTimeTicks), SkyLightColorB.Sample(dayTimeTicks));
}

/// <summary>Overworld day environment tracks from Timelines.java: sun/moon/star angles (degrees).</summary>
public static class SkyTimelines
{
    public const int PeriodTicks = 24000;

    private static readonly CubicBezierEasing SkyAngleEase = CubicBezierEasing.Symmetric(0.362f, 0.241f);

    private static readonly FloatKeyframeTrack SunAngleTrack = FloatKeyframeTrack.Build(
        [new FloatKeyframe(6000, 360f), new FloatKeyframe(6000, 0f)], SkyAngleEase, PeriodTicks);

    private static readonly FloatKeyframeTrack MoonAngleTrack = FloatKeyframeTrack.Build(
        [new FloatKeyframe(6000, 540f), new FloatKeyframe(6000, 180f)], SkyAngleEase, PeriodTicks);

    public static float SunAngleDegrees(int dayTimeTicks) => SunAngleTrack.Sample(dayTimeTicks);

    public static float MoonAngleDegrees(int dayTimeTicks) => MoonAngleTrack.Sample(dayTimeTicks);

    public static float StarAngleDegrees(int dayTimeTicks) => SunAngleTrack.Sample(dayTimeTicks);

    /// <summary>
    /// Sun angle at a sub-tick fraction: linear between the previous and current tick value, matching the reference
    /// EnvironmentAttributeProbe.partialTickLerp (docs/modules/time.md 5). At partial 0 it returns the previous tick's
    /// value and at partial 1 the current tick's value, so consecutive frames are continuous without per-tick stepping.
    /// </summary>
    public static float SunAngleDegrees(int dayTimeTicks, float partialTick) => LerpBetweenTicks(SunAngleTrack, dayTimeTicks, partialTick);

    public static float MoonAngleDegrees(int dayTimeTicks, float partialTick) => LerpBetweenTicks(MoonAngleTrack, dayTimeTicks, partialTick);

    public static float StarAngleDegrees(int dayTimeTicks, float partialTick) => LerpBetweenTicks(SunAngleTrack, dayTimeTicks, partialTick);

    /// <summary>Linear interpolation between the track value one tick ago and at <paramref name="ticks"/>.</summary>
    public static float LerpBetweenTicks(FloatKeyframeTrack track, int ticks, float partialTick)
    {
        ArgumentNullException.ThrowIfNull(track);
        var previous = track.Sample(ticks - 1L);
        var current = track.Sample(ticks);
        var t = Math.Clamp(partialTick, 0f, 1f);
        return previous + ((current - previous) * t);
    }
}
