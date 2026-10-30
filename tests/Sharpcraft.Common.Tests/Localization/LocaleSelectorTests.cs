// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Common.Localization;
using Xunit;

namespace Sharpcraft.Common.Tests.Localization;

/// <summary>
/// Choosing the language file from the machine's locale (docs/modules/ui.md 8.4). The reference shows the interface in
/// the player's language, so on a Chinese machine the same message must not appear in English.
/// </summary>
public sealed class LocaleSelectorTests
{
    [Theory]
    [InlineData("zh-CN", "zh_cn")]
    [InlineData("en-US", "en_us")]
    [InlineData("pt-BR", "pt_br")]
    [InlineData("ja-JP", "ja_jp")]
    public void ASimpleLocaleMapsToItsLanguageFile(string culture, string expected)
    {
        Assert.Equal(expected, LocaleSelector.Candidates(culture)[0]);
    }

    [Fact]
    public void AScriptedLocaleFallsBackToLanguageAndRegion()
    {
        // zh-Hans-CN has no file of its own; zh_cn is the one the game ships.
        var candidates = LocaleSelector.Candidates("zh-Hans-CN");

        Assert.Equal("zh_hans_cn", candidates[0]);
        Assert.Contains("zh_cn", candidates);
        Assert.Equal(LocaleSelector.Fallback, candidates[^1]);
    }

    [Fact]
    public void TheLanguageAloneIsTriedBeforeEnglish()
    {
        var candidates = LocaleSelector.Candidates("de-DE");

        Assert.Equal(["de_de", "de", LocaleSelector.Fallback], candidates);
    }

    [Fact]
    public void AnUnknownOrMissingLocaleStillEndsAtEnglish()
    {
        Assert.Equal([LocaleSelector.Fallback], LocaleSelector.Candidates(null));
        Assert.Equal([LocaleSelector.Fallback], LocaleSelector.Candidates("   "));
        Assert.Equal(LocaleSelector.Fallback, LocaleSelector.Candidates("xx-YY")[^1]);
    }

    [Fact]
    public void CandidatesHaveNoDuplicates()
    {
        var candidates = LocaleSelector.Candidates("zh-Hans-CN");

        Assert.Equal(candidates.Length, candidates.Distinct(StringComparer.Ordinal).Count());
    }
}
