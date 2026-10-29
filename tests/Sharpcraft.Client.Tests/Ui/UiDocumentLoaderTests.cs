// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Ui;
using Xunit;

namespace Sharpcraft.Client.Tests.Ui;

/// <summary>
/// Reloading documents while the game runs (docs/modules/ui.md 8, docs/decisions/0010). The interesting cases are
/// the failures: a broken edit must not blank the screen, and a slow parse must not overwrite a newer document.
/// </summary>
public sealed class UiDocumentLoaderTests
{
    private const string Valid = """
<Screen Padding="4">
  <TextBlock Text="hello" Colour="#FFFFFFFF" />
</Screen>
""";

    private const string AlsoValid = """
<Screen Padding="6">
  <TextBlock Text="changed" Colour="#FFFF0000" />
</Screen>
""";

    [Fact]
    public void AValidDocumentIsApplied()
    {
        var loader = new UiDocumentLoader("unused.xaml");

        Assert.True(loader.TryApply(Valid, generation: 1));
        Assert.NotNull(loader.Current);
        Assert.Equal("hello", loader.Current!.Root.Children[0].Get("Text"));
        Assert.Equal(string.Empty, loader.LastError);
        Assert.Equal(1, loader.AppliedLoads);
    }

    [Fact]
    public void ABrokenDocumentKeepsThePreviousScreen()
    {
        var loader = new UiDocumentLoader("unused.xaml");
        loader.TryApply(Valid, generation: 1);
        var first = loader.Current;

        // An element the kernel does not know is a document error, not a reason to go blank.
        Assert.False(loader.TryApply("<Screen><Slider/></Screen>", generation: 2));
        Assert.Same(first, loader.Current);
        Assert.Contains("unknown element Slider", loader.LastError);
        Assert.Equal(1, loader.RejectedLoads);
    }

    [Fact]
    public void ABrokenColourIsRejectedToo()
    {
        var loader = new UiDocumentLoader("unused.xaml");
        loader.TryApply(Valid, generation: 1);
        var first = loader.Current;

        Assert.False(loader.TryApply("<Screen><TextBlock Text=\"x\" Colour=\"blue\"/></Screen>", generation: 2));
        Assert.Same(first, loader.Current);
    }

    [Fact]
    public void AStaleGenerationDoesNotOverwriteANewerOne()
    {
        var loader = new UiDocumentLoader("unused.xaml");
        loader.TryApply(Valid, generation: 5);
        var newest = loader.Current;

        // A parse that started earlier finishes later: it must not put the older document back on screen.
        Assert.False(loader.TryApply(AlsoValid, generation: 3));
        Assert.Same(newest, loader.Current);
    }

    [Fact]
    public void RecoveryWorksAfterARejection()
    {
        var loader = new UiDocumentLoader("unused.xaml");
        loader.TryApply(Valid, generation: 1);
        loader.TryApply("<Screen><Slider/></Screen>", generation: 2);

        Assert.True(loader.TryApply(AlsoValid, generation: 3));
        Assert.Equal("changed", loader.Current!.Root.Children[0].Get("Text"));
        Assert.Equal(string.Empty, loader.LastError);
    }

    [Fact]
    public void OnlyTheManagedDirectoryIsAccepted()
    {
        var root = Path.Combine(Path.GetTempPath(), "sharpcraft-ui-test");

        Assert.True(UiDocumentFile.IsManaged(root, UiDocumentFile.PathFor(root)));

        // A sibling that only looks like it is inside must be refused, as must anything elsewhere on disk.
        Assert.False(UiDocumentFile.IsManaged(root, Path.Combine(root, "ui", "..", "outside.xaml")));
        Assert.False(UiDocumentFile.IsManaged(root, Path.Combine(root, "elsewhere.xaml")));
    }

    [Fact]
    public void EnsureExistsCreatesTheDirectoryAndTheStarterDocument()
    {
        var root = Path.Combine(Path.GetTempPath(), "sharpcraft-ui-ensure-" + Guid.NewGuid().ToString("N"));

        try
        {
            var path = UiDocumentFile.EnsureExists(root, Valid);

            Assert.True(File.Exists(path));
            Assert.Equal(Valid, File.ReadAllText(path));

            // Second call must not overwrite what the player has since edited.
            File.WriteAllText(path, AlsoValid);
            Assert.Equal(path, UiDocumentFile.EnsureExists(root, Valid));
            Assert.Equal(AlsoValid, File.ReadAllText(path));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
