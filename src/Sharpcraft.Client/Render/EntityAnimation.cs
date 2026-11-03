// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;

namespace Sharpcraft.Client.Render;

/// <summary>Which part transform an animation channel writes.</summary>
public enum AnimationTarget
{
    Position,
    Rotation,
    Scale
}

/// <summary>Interpolation between two adjacent keyframes.</summary>
public enum AnimationInterpolation
{
    Linear,
    CatmullRom
}

/// <summary>One keyframe. <see cref="PreTarget"/> is used by the next segment and <see cref="PostTarget"/> by the previous.</summary>
public readonly record struct AnimationKeyframe(
    float Timestamp,
    Vector3 PreTarget,
    Vector3 PostTarget,
    AnimationInterpolation Interpolation)
{
    public AnimationKeyframe(float timestamp, Vector3 target, AnimationInterpolation interpolation)
        : this(timestamp, target, target, interpolation)
    {
    }
}

/// <summary>A single target's keyframe track for one part.</summary>
public sealed record AnimationChannel(AnimationTarget Target, AnimationKeyframe[] Keyframes);

/// <summary>Named pose offsets for one entity part (all values are additive, as in the reference offset* methods).</summary>
public readonly record struct PoseOffset(Vector3 Position, Vector3 Rotation, Vector3 Scale)
{
    public static readonly PoseOffset Zero = new(Vector3.Zero, Vector3.Zero, Vector3.Zero);
}

/// <summary>Per-frame pose overrides keyed by part name. The caller reuses one instance per entity/frame.</summary>
public sealed class EntityPose
{
    private readonly Dictionary<string, PoseOffset> _offsets = new(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, PoseOffset> Offsets => _offsets;

    public void Clear() => _offsets.Clear();

    public PoseOffset Get(string partName) =>
        _offsets.TryGetValue(partName, out var offset) ? offset : PoseOffset.Zero;

    public void Add(string partName, in PoseOffset delta)
    {
        var current = Get(partName);
        _offsets[partName] = new PoseOffset(
            current.Position + delta.Position,
            current.Rotation + delta.Rotation,
            current.Scale + delta.Scale);
    }
}

/// <summary>Static animation data: length, looping and bone → channels.</summary>
public sealed class AnimationDefinition
{
    public AnimationDefinition(float lengthInSeconds, bool looping, IReadOnlyDictionary<string, IReadOnlyList<AnimationChannel>> boneAnimations)
    {
        LengthInSeconds = lengthInSeconds;
        Looping = looping;
        BoneAnimations = boneAnimations;
    }

    public float LengthInSeconds { get; }
    public bool Looping { get; }
    public IReadOnlyDictionary<string, IReadOnlyList<AnimationChannel>> BoneAnimations { get; }

    public KeyframeAnimation Bake(IReadOnlySet<string> partNames)
    {
        var entries = new List<KeyframeAnimation.Entry>();
        foreach (var (partName, channels) in BoneAnimations)
        {
            if (!partNames.Contains(partName))
            {
                throw new InvalidOperationException($"animation references missing part '{partName}'");
            }

            foreach (var channel in channels)
            {
                entries.Add(new KeyframeAnimation.Entry(partName, channel.Target, channel.Keyframes));
            }
        }

        return new KeyframeAnimation(LengthInSeconds, Looping, entries.ToArray());
    }
}

/// <summary>Baked animation evaluator. Immutable after construction and safe to cache per model.</summary>
public sealed class KeyframeAnimation
{
    public readonly record struct Entry(string PartName, AnimationTarget Target, AnimationKeyframe[] Keyframes);

    private readonly float _lengthInSeconds;
    private readonly bool _looping;
    private readonly Entry[] _entries;

    public KeyframeAnimation(float lengthInSeconds, bool looping, Entry[] entries)
    {
        _lengthInSeconds = lengthInSeconds;
        _looping = looping;
        _entries = entries;
    }

    /// <summary>Applies the animation at elapsed seconds into <paramref name="pose"/>.</summary>
    public void Apply(float secondsSinceStart, float targetScale, EntityPose pose)
    {
        var seconds = _looping && _lengthInSeconds > 0f
            ? secondsSinceStart % _lengthInSeconds
            : secondsSinceStart;

        foreach (var entry in _entries)
        {
            var value = Sample(entry.Keyframes, seconds);
            value *= targetScale;
            pose.Add(entry.PartName, new PoseOffset(
                entry.Target == AnimationTarget.Position ? value : Vector3.Zero,
                entry.Target == AnimationTarget.Rotation ? value : Vector3.Zero,
                entry.Target == AnimationTarget.Scale ? value : Vector3.Zero));
        }
    }

    /// <summary>Applies the 0-second pose (the reference's applyStatic).</summary>
    public void ApplyStatic(EntityPose pose) => Apply(0f, 1f, pose);

    private static Vector3 Sample(AnimationKeyframe[] keyframes, float seconds)
    {
        if (keyframes.Length == 0)
        {
            return Vector3.Zero;
        }

        if (keyframes.Length == 1 || seconds <= keyframes[0].Timestamp)
        {
            return keyframes[0].PostTarget;
        }

        var prev = 0;
        while (prev + 1 < keyframes.Length && keyframes[prev + 1].Timestamp <= seconds)
        {
            prev++;
        }

        var next = Math.Min(keyframes.Length - 1, prev + 1);
        var previous = keyframes[prev];
        var nextFrame = keyframes[next];
        if (next == prev)
        {
            return previous.PostTarget;
        }

        var alpha = Math.Clamp((seconds - previous.Timestamp) / (nextFrame.Timestamp - previous.Timestamp), 0f, 1f);
        return nextFrame.Interpolation switch
        {
            AnimationInterpolation.CatmullRom => CatmullRom(keyframes, prev, next, alpha),
            _ => Vector3.Lerp(previous.PostTarget, nextFrame.PreTarget, alpha)
        };
    }

    private static Vector3 CatmullRom(AnimationKeyframe[] keyframes, int prev, int next, float alpha)
    {
        var point0 = keyframes[Math.Max(0, prev - 1)].PostTarget;
        var point1 = keyframes[prev].PostTarget;
        var point2 = keyframes[next].PostTarget;
        var point3 = keyframes[Math.Min(keyframes.Length - 1, next + 1)].PostTarget;
        return new Vector3(
            CatmullRom(alpha, point0.X, point1.X, point2.X, point3.X),
            CatmullRom(alpha, point0.Y, point1.Y, point2.Y, point3.Y),
            CatmullRom(alpha, point0.Z, point1.Z, point2.Z, point3.Z));
    }

    private static float CatmullRom(float t, float p0, float p1, float p2, float p3) =>
        0.5f * ((2f * p1) + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t * t
                + (-p0 + 3f * p1 - 3f * p2 + p3) * t * t * t);
}

/// <summary>Reference-compatible animation clock: start tick and elapsed milliseconds.</summary>
public sealed class AnimationState
{
    private const int Stopped = int.MinValue;
    private int _startTick = Stopped;

    public bool IsStarted => _startTick != Stopped;

    public void Start(int tickCount) => _startTick = tickCount;

    public void StartIfStopped(int tickCount)
    {
        if (!IsStarted)
        {
            Start(tickCount);
        }
    }

    public void AnimateWhen(bool condition, int tickCount)
    {
        if (condition)
        {
            StartIfStopped(tickCount);
        }
        else
        {
            Stop();
        }
    }

    public void Stop() => _startTick = Stopped;

    public long GetTimeInMillis(float ageInTicks) => (long)((ageInTicks - _startTick) * 50f);
}

/// <summary>Reference vector constructors for animation data (POSITION Y is inverted; SCALE is additive).</summary>
public static class KeyframeAnimations
{
    public static Vector3 PosVec(float x, float y, float z) => new(x, -y, z);

    public static Vector3 DegreeVec(float xDegrees, float yDegrees, float zDegrees) =>
        new(xDegrees * (MathF.PI / 180f), yDegrees * (MathF.PI / 180f), zDegrees * (MathF.PI / 180f));

    public static Vector3 ScaleVec(float x, float y, float z) => new(x - 1f, y - 1f, z - 1f);
}

