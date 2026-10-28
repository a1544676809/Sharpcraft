// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Common.Random;

/// <summary>
/// Marsaglia polar method for normal deviates, equivalent to Minecraft's
/// <c>MarsagliaPolarGaussian</c>: every second call returns the cached sample, and
/// reseeding must clear the cache.
/// </summary>
public sealed class MarsagliaPolarGaussian
{
    private readonly IRandomSource _source;
    private double _nextNextGaussian;
    private bool _hasNextNextGaussian;

    /// <summary>Creates the helper over a random source.</summary>
    public MarsagliaPolarGaussian(IRandomSource source) =>
        _source = source ?? throw new ArgumentNullException(nameof(source));

    /// <summary>Drops the cached sample (vanilla does this on reseed).</summary>
    public void Reset() => _hasNextNextGaussian = false;

    /// <summary>Next standard normal sample.</summary>
    public double Next()
    {
        if (_hasNextNextGaussian)
        {
            _hasNextNextGaussian = false;
            return _nextNextGaussian;
        }

        double x;
        double y;
        double radiusSquared;
        do
        {
            x = (2.0 * _source.NextDouble()) - 1.0;
            y = (2.0 * _source.NextDouble()) - 1.0;
            radiusSquared = (x * x) + (y * y);
        }
        while (radiusSquared >= 1.0 || radiusSquared == 0.0);

        var multiplier = Math.Sqrt(-2.0 * Math.Log(radiusSquared) / radiusSquared);
        _nextNextGaussian = y * multiplier;
        _hasNextNextGaussian = true;
        return x * multiplier;
    }
}
