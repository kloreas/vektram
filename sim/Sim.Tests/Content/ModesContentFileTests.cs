using System;
using System.IO;
using Sim.Content;
using Xunit;

namespace Sim.Tests.Content;

/// <summary>
/// Validates the shipped <c>modes.json</c>: it parses, ships the two #5 modes, and its
/// <c>elimination</c> row equals <see cref="ModeDefinition.Default"/> — the drift-lock that keeps
/// the C# engine default and the authored content in sync (mirrors <c>combat.json</c> ↔
/// <c>CombatTuning.Default</c>). The test project performs the file read; <c>/sim</c> only parses.
/// </summary>
public class ModesContentFileTests
{
    private static string ReadContent(string fileName) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "content", fileName));

    [Fact]
    public void ShippedModesFile_ParsesSuccessfully()
    {
        ModeCatalog catalog = ModeCatalog.FromJson(ReadContent("modes.json"));

        Assert.True(catalog.Count >= 2);
        Assert.Equal(WinConditionKind.LastTeamStanding, catalog.Get("elimination").WinCondition.Kind);
        Assert.Equal(WinConditionKind.TurnLimitTiebreak, catalog.Get("attrition_timed").WinCondition.Kind);
    }

    [Fact]
    public void ShippedEliminationRow_EqualsModeDefinitionDefault()
    {
        ModeCatalog catalog = ModeCatalog.FromJson(ReadContent("modes.json"));

        ModeDefinition elimination = catalog.Get("elimination");

        Assert.Equal(ModeDefinition.Default, elimination);
    }
}
