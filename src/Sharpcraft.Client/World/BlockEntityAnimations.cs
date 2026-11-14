// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.World;

/// <summary>
/// Block entity animation state driven by block events: which chests are open, and how far their
/// lids have moved. The reference keeps this on the block entity and steps it once per tick
/// (docs/specs/world/block-events.md).
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.world.level.block.entity.ChestLidController", "ref://mc-26.3-analysis/src/net/minecraft/world/level/block/entity/ChestLidController.java")]
public sealed class BlockEntityAnimations
{
    /// <summary>Event id a chest uses to say whether its lid should be open.</summary>
    public const int ChestOpenEvent = 1;

    /// <summary>How far the lid moves each tick; the reference uses a tenth, so ten ticks to travel.</summary>
    public const float StepPerTick = 0.1f;

    private readonly Dictionary<long, ChestLid> _chests = [];

    /// <summary>Chests currently tracked.</summary>
    public int Count => _chests.Count;

    /// <summary>Applies one block event.</summary>
    /// <param name="x">Block X.</param>
    /// <param name="y">Block Y.</param>
    /// <param name="z">Block Z.</param>
    /// <param name="eventId">Event id; only the chest open event is handled.</param>
    /// <param name="parameter">Event parameter; for a chest this is the number of players with it open.</param>
    public void Apply(int x, int y, int z, int eventId, int parameter)
    {
        if (eventId != ChestOpenEvent)
        {
            return;
        }

        var key = Key(x, y, z);
        if (!_chests.TryGetValue(key, out var lid))
        {
            lid = default;
            _chests[key] = lid;
        }

        lid.ShouldBeOpen = parameter > 0;
        _chests[key] = lid;
    }

    /// <summary>Steps every tracked lid one tick.</summary>
    public void Tick()
    {
        foreach (var key in _chests.Keys.ToArray())
        {
            var lid = _chests[key];
            lid.Tick();
            _chests[key] = lid;
        }
    }

    /// <summary>How far the lid at a position is open, interpolated for the partial tick.</summary>
    public float GetOpenness(int x, int y, int z) =>
        _chests.TryGetValue(Key(x, y, z), out var lid) ? lid.Current : 0f;

    /// <summary>
    /// How far the lid is open, interpolated between the previous and current step. The reference
    /// reads this with the partial tick, so a partial of zero still reports the previous step.
    /// </summary>
    /// <param name="x">Block X.</param>
    /// <param name="y">Block Y.</param>
    /// <param name="z">Block Z.</param>
    /// <param name="partialTicks">Fraction of a tick since the last step.</param>
    /// <returns>Zero when the position is not tracked.</returns>
    public float GetOpenness(int x, int y, int z, float partialTicks) =>
        _chests.TryGetValue(Key(x, y, z), out var lid) ? lid.GetOpenness(partialTicks) : 0f;
    /// <summary>Packs a block position the way the reference does.</summary>
    private static long Key(int x, int y, int z) =>
        ((long)x & 0x3FFFFFF) << 38 | ((long)z & 0x3FFFFFF) << 12 | ((long)y & 0xFFF);

    private struct ChestLid
    {
        public bool ShouldBeOpen;

        private float _openness;
        private float _previous;

        public void Tick()
        {
            _previous = _openness;

            if (!ShouldBeOpen && _openness > 0f)
            {
                _openness = MathF.Max(_openness - StepPerTick, 0f);
            }
            else if (ShouldBeOpen && _openness < 1f)
            {
                _openness = MathF.Min(_openness + StepPerTick, 1f);
            }
        }

        public readonly float Current => _openness;

        public readonly float GetOpenness(float partialTicks) =>
            _previous + ((_openness - _previous) * partialTicks);
    }
}
