// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.World;

/// <summary>
/// 64-tick ring of Ender Dragon <c>(y, yRot)</c> samples. The reference records one sample per server tick and the
/// model samples delays 0..23 for the neck, head and tail chains.
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.world.entity.boss.enderdragon.DragonFlightHistory", "ref://mc-26.3-analysis/src/net/minecraft/world/entity/boss/enderdragon/DragonFlightHistory.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Full)]
public sealed class DragonFlightHistory
{
    /// <summary>Number of retained samples; the bit mask wraps at 63.</summary>
    public const int Length = 64;

    private const int Mask = Length - 1;

    private readonly Sample[] _samples = new Sample[Length];
    private int _head = -1;

    /// <summary>One recorded flight sample.</summary>
    [Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.world.entity.boss.enderdragon.DragonFlightHistory.Sample", "ref://mc-26.3-analysis/src/net/minecraft/world/entity/boss/enderdragon/DragonFlightHistory.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Full)]
    public readonly record struct Sample(double Y, float YRot);

    /// <summary>Records the current tick's y and yaw.</summary>
    public void Record(double y, float yRot)
    {
        var sample = new Sample(y, yRot);
        if (_head < 0)
        {
            Array.Fill(_samples, sample);
        }

        _head = (_head + 1) & Mask;
        _samples[_head] = sample;
    }

    /// <summary>Sample at a delay in ticks; delays wrap through the ring.</summary>
    public Sample Get(int delay) => _samples[(_head - delay) & Mask];

    /// <summary>Linearly interpolated sample between <paramref name="delay"/> and <paramref name="delay"/> + 1.</summary>
    public Sample Get(int delay, float partialTicks)
    {
        var sample = Get(delay);
        var previous = Get(delay + 1);
        return new Sample(
            previous.Y + ((sample.Y - previous.Y) * partialTicks),
            RotLerp(partialTicks, previous.YRot, sample.YRot));
    }

    private static float RotLerp(float partialTicks, float from, float to) =>
        from + (WrapDegrees(to - from) * partialTicks);

    private static float WrapDegrees(float degrees)
    {
        var wrapped = degrees % 360f;
        if (wrapped >= 180f)
        {
            wrapped -= 360f;
        }
        else if (wrapped < -180f)
        {
            wrapped += 360f;
        }

        return wrapped;
    }
}
