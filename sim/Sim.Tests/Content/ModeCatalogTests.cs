using System.Collections.Generic;
using Sim.Content;
using Xunit;

namespace Sim.Tests.Content;

public class ModeCatalogTests
{
    private const string ValidJson = @"
{
  ""schemaVersion"": 1,
  ""modes"": [
    { ""id"": ""elimination"", ""displayName"": ""Elimination"", ""teamSizes"": [],
      ""modeMultiplier"": 1.0, ""friendlyFire"": true, ""selfDamage"": true,
      ""turnOrder"": ""RoundRobin"", ""maxTurns"": 200,
      ""winCondition"": { ""kind"": ""LastTeamStanding"" } },
    { ""id"": ""attrition_timed"", ""displayName"": ""Timed Attrition"", ""teamSizes"": [2, 2],
      ""modeMultiplier"": 0.5, ""friendlyFire"": false, ""selfDamage"": true,
      ""turnOrder"": ""RoundRobin"", ""maxTurns"": 60,
      ""winCondition"": { ""kind"": ""TurnLimitTiebreak"", ""tiebreak"": ""TotalHpRemaining"" } }
  ]
}";

    private static string OneMode(string body) =>
        @"{ ""schemaVersion"": 1, ""modes"": [ " + body + " ] }";

    // ── Happy path ────────────────────────────────────────────────────────────

    [Fact]
    public void FromJson_Valid_LoadsAllDefinitions()
    {
        ModeCatalog catalog = ModeCatalog.FromJson(ValidJson);

        Assert.Equal(2, catalog.Count);
        Assert.Contains("elimination", catalog.Ids);
        Assert.Contains("attrition_timed", catalog.Ids);
    }

    [Fact]
    public void Get_LastTeamStandingMode_ReturnsAllFields()
    {
        ModeCatalog catalog = ModeCatalog.FromJson(ValidJson);

        ModeDefinition mode = catalog.Get("elimination");

        Assert.Equal("Elimination", mode.DisplayName);
        Assert.Empty(mode.TeamSizes);
        Assert.Equal(1.0, mode.ModeMultiplier);
        Assert.True(mode.FriendlyFire);
        Assert.True(mode.SelfDamage);
        Assert.Equal(TurnOrderPolicyKind.RoundRobin, mode.TurnOrder);
        Assert.Equal(200, mode.MaxTurns);
        Assert.Equal(WinConditionKind.LastTeamStanding, mode.WinCondition.Kind);
        Assert.Null(mode.WinCondition.Tiebreak);
    }

    [Fact]
    public void Get_TurnLimitTiebreakMode_ReturnsTiebreakAndTeamSizes()
    {
        ModeCatalog catalog = ModeCatalog.FromJson(ValidJson);

        ModeDefinition mode = catalog.Get("attrition_timed");

        Assert.Equal(new[] { 2, 2 }, mode.TeamSizes);
        Assert.False(mode.FriendlyFire);
        Assert.Equal(WinConditionKind.TurnLimitTiebreak, mode.WinCondition.Kind);
        Assert.Equal(TiebreakMetric.TotalHpRemaining, mode.WinCondition.Tiebreak);
    }

    [Fact]
    public void AcceptsRoster_ConstrainedMode_MatchesOnlyTheDeclaredStructure()
    {
        ModeDefinition mode = ModeCatalog.FromJson(ValidJson).Get("attrition_timed");

        Assert.True(mode.AcceptsRoster(new[] { 0, 0, 1, 1 }));
        Assert.False(mode.AcceptsRoster(new[] { 0, 1 }));        // wrong sizes
        Assert.False(mode.AcceptsRoster(new[] { 0, 0, 0, 1 }));  // wrong split
    }

    [Fact]
    public void AcceptsRoster_UnconstrainedMode_AcceptsAnyRoster()
    {
        ModeDefinition mode = ModeCatalog.FromJson(ValidJson).Get("elimination");

        Assert.True(mode.AcceptsRoster(new[] { 0, 1 }));
        Assert.True(mode.AcceptsRoster(new[] { 0, 0, 1, 1, 2, 2 }));
    }

    // ── Validation ────────────────────────────────────────────────────────────

    [Fact]
    public void FromJson_MalformedJson_Throws()
    {
        Assert.Throws<ModeDataException>(() => ModeCatalog.FromJson(@"{ ""schemaVersion"": 1, ""modes"": ["));
    }

    [Fact]
    public void FromJson_WrongSchemaVersion_Throws()
    {
        var ex = Assert.Throws<ModeDataException>(() =>
            ModeCatalog.FromJson(@"{ ""schemaVersion"": 2, ""modes"": [] }"));

        Assert.Contains("schemaVersion", ex.Message);
    }

    [Fact]
    public void FromJson_MissingModesArray_Throws()
    {
        Assert.Throws<ModeDataException>(() => ModeCatalog.FromJson(@"{ ""schemaVersion"": 1 }"));
    }

    [Fact]
    public void FromJson_DuplicateId_Throws()
    {
        string json = @"{ ""schemaVersion"": 1, ""modes"": [
            { ""id"": ""m"", ""displayName"": ""A"", ""teamSizes"": [], ""modeMultiplier"": 1.0,
              ""friendlyFire"": true, ""selfDamage"": true, ""turnOrder"": ""RoundRobin"",
              ""maxTurns"": 10, ""winCondition"": { ""kind"": ""LastTeamStanding"" } },
            { ""id"": ""m"", ""displayName"": ""B"", ""teamSizes"": [], ""modeMultiplier"": 1.0,
              ""friendlyFire"": true, ""selfDamage"": true, ""turnOrder"": ""RoundRobin"",
              ""maxTurns"": 10, ""winCondition"": { ""kind"": ""LastTeamStanding"" } } ] }";

        var ex = Assert.Throws<ModeDataException>(() => ModeCatalog.FromJson(json));

        Assert.Contains("m", ex.Message);
    }

    [Fact]
    public void FromJson_EmptyId_Throws()
    {
        string json = OneMode(
            @"{ ""id"": """", ""displayName"": ""A"", ""teamSizes"": [], ""modeMultiplier"": 1.0,
                ""friendlyFire"": true, ""selfDamage"": true, ""turnOrder"": ""RoundRobin"",
                ""maxTurns"": 10, ""winCondition"": { ""kind"": ""LastTeamStanding"" } }");

        Assert.Throws<ModeDataException>(() => ModeCatalog.FromJson(json));
    }

    [Fact]
    public void FromJson_UnknownTurnOrder_Throws()
    {
        string json = OneMode(
            @"{ ""id"": ""m"", ""displayName"": ""A"", ""teamSizes"": [], ""modeMultiplier"": 1.0,
                ""friendlyFire"": true, ""selfDamage"": true, ""turnOrder"": ""Agility"",
                ""maxTurns"": 10, ""winCondition"": { ""kind"": ""LastTeamStanding"" } }");

        var ex = Assert.Throws<ModeDataException>(() => ModeCatalog.FromJson(json));

        Assert.Contains("turnOrder", ex.Message);
    }

    [Fact]
    public void FromJson_UnknownWinConditionKind_Throws()
    {
        string json = OneMode(
            @"{ ""id"": ""m"", ""displayName"": ""A"", ""teamSizes"": [], ""modeMultiplier"": 1.0,
                ""friendlyFire"": true, ""selfDamage"": true, ""turnOrder"": ""RoundRobin"",
                ""maxTurns"": 10, ""winCondition"": { ""kind"": ""HoldTheZone"" } }");

        Assert.Throws<ModeDataException>(() => ModeCatalog.FromJson(json));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(201)]
    public void FromJson_MaxTurnsOutOfRange_Throws(int maxTurns)
    {
        string json = OneMode(
            @"{ ""id"": ""m"", ""displayName"": ""A"", ""teamSizes"": [], ""modeMultiplier"": 1.0,
                ""friendlyFire"": true, ""selfDamage"": true, ""turnOrder"": ""RoundRobin"",
                ""maxTurns"": " + maxTurns.ToString(System.Globalization.CultureInfo.InvariantCulture) +
            @", ""winCondition"": { ""kind"": ""LastTeamStanding"" } }");

        var ex = Assert.Throws<ModeDataException>(() => ModeCatalog.FromJson(json));

        Assert.Contains("maxTurns", ex.Message);
    }

    [Fact]
    public void FromJson_NonPositiveModeMultiplier_Throws()
    {
        string json = OneMode(
            @"{ ""id"": ""m"", ""displayName"": ""A"", ""teamSizes"": [], ""modeMultiplier"": 0.0,
                ""friendlyFire"": true, ""selfDamage"": true, ""turnOrder"": ""RoundRobin"",
                ""maxTurns"": 10, ""winCondition"": { ""kind"": ""LastTeamStanding"" } }");

        Assert.Throws<ModeDataException>(() => ModeCatalog.FromJson(json));
    }

    [Fact]
    public void FromJson_LastTeamStandingWithTiebreak_Throws()
    {
        string json = OneMode(
            @"{ ""id"": ""m"", ""displayName"": ""A"", ""teamSizes"": [], ""modeMultiplier"": 1.0,
                ""friendlyFire"": true, ""selfDamage"": true, ""turnOrder"": ""RoundRobin"",
                ""maxTurns"": 10,
                ""winCondition"": { ""kind"": ""LastTeamStanding"", ""tiebreak"": ""TotalHpRemaining"" } }");

        var ex = Assert.Throws<ModeDataException>(() => ModeCatalog.FromJson(json));

        Assert.Contains("tiebreak", ex.Message);
    }

    [Fact]
    public void FromJson_TurnLimitTiebreakWithoutTiebreak_Throws()
    {
        string json = OneMode(
            @"{ ""id"": ""m"", ""displayName"": ""A"", ""teamSizes"": [], ""modeMultiplier"": 1.0,
                ""friendlyFire"": true, ""selfDamage"": true, ""turnOrder"": ""RoundRobin"",
                ""maxTurns"": 10, ""winCondition"": { ""kind"": ""TurnLimitTiebreak"" } }");

        var ex = Assert.Throws<ModeDataException>(() => ModeCatalog.FromJson(json));

        Assert.Contains("tiebreak", ex.Message);
    }

    [Fact]
    public void FromJson_NonPositiveTeamSize_Throws()
    {
        string json = OneMode(
            @"{ ""id"": ""m"", ""displayName"": ""A"", ""teamSizes"": [2, 0], ""modeMultiplier"": 1.0,
                ""friendlyFire"": true, ""selfDamage"": true, ""turnOrder"": ""RoundRobin"",
                ""maxTurns"": 10, ""winCondition"": { ""kind"": ""LastTeamStanding"" } }");

        Assert.Throws<ModeDataException>(() => ModeCatalog.FromJson(json));
    }

    [Fact]
    public void FromJson_SameText_ProducesValueEqualDefinitions()
    {
        ModeDefinition a = ModeCatalog.FromJson(ValidJson).Get("attrition_timed");
        ModeDefinition b = ModeCatalog.FromJson(ValidJson).Get("attrition_timed");

        Assert.Equal(a, b);   // element-wise equality over TeamSizes, not reference equality
    }
}
