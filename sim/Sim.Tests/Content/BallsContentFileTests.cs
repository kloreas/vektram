using System;
using System.IO;
using Sim.Content;
using Xunit;

namespace Sim.Tests.Content;

/// <summary>
/// Validates the shipped <c>/content/data/balls.json</c>. The test project performs the
/// file read (copied to output via the csproj); <c>/sim</c> only parses the text.
/// </summary>
public class BallsContentFileTests
{
    private static BallCatalog LoadShippedCatalog()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "content", "balls.json");
        string json = File.ReadAllText(path);
        return BallCatalog.FromJson(json);
    }

    [Fact]
    public void ShippedBallsFile_ParsesSuccessfully()
    {
        BallCatalog catalog = LoadShippedCatalog();

        Assert.True(catalog.Count >= 3);
    }

    [Theory]
    [InlineData("standard", ShellType.Standard)]
    [InlineData("heavy", ShellType.Heavy)]
    [InlineData("light", ShellType.Light)]
    public void ShippedBallsFile_ContainsStarterShell(string id, ShellType expectedType)
    {
        BallCatalog catalog = LoadShippedCatalog();

        BallDefinition def = catalog.Get(id);

        Assert.Equal(expectedType, def.Type);
        Assert.True(def.Physics.GravityScale > 0.0);
        Assert.True(def.BlastRadius > 0.0);
    }
}
