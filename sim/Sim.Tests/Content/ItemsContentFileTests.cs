using System;
using System.IO;
using Sim.Content;
using Xunit;

namespace Sim.Tests.Content;

/// <summary>
/// Validates the shipped <c>items.json</c> and that its ball references resolve against the
/// shipped <c>balls.json</c>. The test project performs the file reads; <c>/sim</c> only parses.
/// </summary>
public class ItemsContentFileTests
{
    private static string ReadContent(string fileName) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "content", fileName));

    [Fact]
    public void ShippedItemsFile_ParsesSuccessfully()
    {
        ItemCatalog catalog = ItemCatalog.FromJson(ReadContent("items.json"));

        Assert.True(catalog.Count >= 4);
        Assert.Equal(ItemEffectKind.GrantBall, catalog.Get("shell_standard").Effect.Kind);
        Assert.Equal(ItemEffectKind.RestoreHp, catalog.Get("heal_small").Effect.Kind);
    }

    [Fact]
    public void ShippedItemsFile_BallReferencesResolveAgainstShippedBalls()
    {
        ItemCatalog items = ItemCatalog.FromJson(ReadContent("items.json"));
        BallCatalog balls = BallCatalog.FromJson(ReadContent("balls.json"));

        items.ValidateBallReferences(balls);   // should not throw
    }
}
