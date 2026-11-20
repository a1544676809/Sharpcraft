// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Diagnostics;

namespace Sharpcraft.Common.Network;

/// <summary>
/// Tracks per-chunk receive/processing duration and computes the desired chunks per tick
/// for <c>chunk_batch_received</c> flow control, matching vanilla <c>ChunkBatchSizeCalculator</c>.
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.multiplayer.ChunkBatchSizeCalculator", "ref://mc-26.3-analysis/src/net/minecraft/client/multiplayer/ChunkBatchSizeCalculator.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Full)]
public sealed class ChunkBatchSizeCalculator
{
    private const int MaxOldSamplesWeight = 49;
    private const double ClampCoefficient = 3.0;
    private const double TargetBatchBudgetNanos = 7_000_000.0;

    private double _aggregatedNanosPerChunk = 2_000_000.0;
    private int _oldSamplesWeight = 1;
    private long _batchStartTimestamp = Stopwatch.GetTimestamp();

    /// <summary>Marks the start of an incoming chunk batch (<c>chunk_batch_start</c>).</summary>
    public void OnBatchStart() => _batchStartTimestamp = Stopwatch.GetTimestamp();

    /// <summary>Records the completion of a chunk batch (<c>chunk_batch_finished</c>) with <paramref name="batchSize"/> chunks.</summary>
    public void OnBatchFinished(int batchSize)
    {
        if (batchSize <= 0)
        {
            return;
        }

        var elapsedTicks = Stopwatch.GetTimestamp() - _batchStartTimestamp;
        var batchDurationNanos = elapsedTicks * (1_000_000_000.0 / Stopwatch.Frequency);
        RecordBatchDurationNanos(batchDurationNanos, batchSize);
    }

    /// <summary>Updates the EMA with an explicit batch duration in nanoseconds (exposed for deterministic tests).</summary>
    internal void RecordBatchDurationNanos(double batchDurationNanos, int batchSize)
    {
        if (batchSize <= 0)
        {
            return;
        }

        var nanosPerChunk = Math.Max(1.0, batchDurationNanos / batchSize);
        var clampedNanosPerChunk = Math.Clamp(
            nanosPerChunk,
            _aggregatedNanosPerChunk / ClampCoefficient,
            _aggregatedNanosPerChunk * ClampCoefficient);

        _aggregatedNanosPerChunk =
            ((_aggregatedNanosPerChunk * _oldSamplesWeight) + clampedNanosPerChunk) / (_oldSamplesWeight + 1);
        _oldSamplesWeight = Math.Min(MaxOldSamplesWeight, _oldSamplesWeight + 1);
    }

    /// <summary>Desired chunks per tick to report in <c>chunk_batch_received</c>.</summary>
    public float DesiredChunksPerTick => (float)(TargetBatchBudgetNanos / _aggregatedNanosPerChunk);
}
