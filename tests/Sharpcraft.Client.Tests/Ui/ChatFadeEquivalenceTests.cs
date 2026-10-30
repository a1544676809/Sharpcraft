// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Ui;
using Xunit;

namespace Sharpcraft.Client.Tests.Ui;

/// <summary>
/// Equivalence check for the chat fade (docs/modules/ui.md 8.4).
/// </summary>
/// <remarks>
/// The reference computes the opacity of a chat line from its age with a short expression; that expression is
/// transcribed here and compared across every age a line can have. The fade is one of those things that is easy to
/// get approximately right and impossible to notice being slightly wrong, which is exactly why it is compared
/// rather than eyeballed.
/// </remarks>
public sealed class ChatFadeEquivalenceTests
{
    /// <summary>The reference's fade, transcribed: one minus the fraction of life, times ten, clamped, squared.</summary>
    private static float Reference(long ageTicks)
    {
        var t = ageTicks / 200.0;
        t = 1.0 - t;
        t *= 10.0;
        t = Math.Clamp(t, 0.0, 1.0);
        t *= t;
        return (float)t;
    }

    [Fact]
    public void TheFadeAgreesWithTheTranscribedReferenceForEveryAge()
    {
        for (var age = 0L; age <= 300L; age++)
        {
            var expected = Reference(age);
            var actual = ChatLog.AlphaFor(age);

            Assert.True(
                Math.Abs(expected - actual) < 1e-6f,
                $"age {age}: expected {expected}, got {actual}");
        }
    }

    [Fact]
    public void TheLineIsUnchangedUntilTheLastTenthOfItsLife()
    {
        // Ninety percent of two hundred ticks is one hundred and eighty, so everything before that is full opacity.
        Assert.Equal(1f, ChatLog.AlphaFor(0), 6);
        Assert.Equal(1f, ChatLog.AlphaFor(179), 6);
        Assert.True(ChatLog.AlphaFor(181) < 1f);
        Assert.Equal(0f, ChatLog.AlphaFor(200), 6);
    }
}
