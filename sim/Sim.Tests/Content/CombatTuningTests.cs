using System;
using System.IO;
using Sim.Content;
using Xunit;

namespace Sim.Tests.Content;

public class CombatTuningTests
{
    private const string ValidJson = @"
{
  ""schemaVersion"": 1,
  ""guardDivisor"": 12000.0,
  ""guardReduceCap"": 0.60,
  ""defenceDivisor"": 80000.0,
  ""defenceReduceCap"": 0.90,
  ""falloffStrength"": 0.25,
  ""attackFloor"": 0.6,
  ""attackScale"": 0.00008,
  ""baseDamageBonusDivisor"": 10000.0
}";

    [Fact]
    public void FromJson_Valid_ReadsAllFields()
    {
        CombatTuning t = CombatTuning.FromJson(ValidJson);

        Assert.Equal(12000.0, t.GuardDivisor);
        Assert.Equal(0.60, t.GuardReduceCap);
        Assert.Equal(80000.0, t.DefenceDivisor);
        Assert.Equal(0.90, t.DefenceReduceCap);
        Assert.Equal(0.25, t.FalloffStrength);
        Assert.Equal(0.6, t.AttackFloor);
        Assert.Equal(0.00008, t.AttackScale);
        Assert.Equal(10000.0, t.BaseDamageBonusDivisor);
    }

    [Fact]
    public void FromJson_MalformedJson_Throws()
    {
        Assert.Throws<CombatDataException>(() => CombatTuning.FromJson(@"{ ""schemaVersion"": 1, "));
    }

    [Fact]
    public void FromJson_UnsupportedSchemaVersion_Throws()
    {
        Assert.Throws<CombatDataException>(() =>
            CombatTuning.FromJson(ValidJson.Replace(@"""schemaVersion"": 1", @"""schemaVersion"": 99")));
    }

    [Theory]
    [InlineData("guardDivisor", "0.0")]
    [InlineData("defenceDivisor", "-1.0")]
    [InlineData("baseDamageBonusDivisor", "0.0")]
    public void FromJson_NonPositiveDivisor_ThrowsNamingField(string field, string badValue)
    {
        string json = ValidJson.Replace(FieldPair(field), $@"""{field}"": {badValue}");

        var ex = Assert.Throws<CombatDataException>(() => CombatTuning.FromJson(json));

        Assert.Contains(field, ex.Message);
    }

    [Theory]
    [InlineData("guardReduceCap", "1.5")]
    [InlineData("falloffStrength", "-0.1")]
    public void FromJson_FractionOutOfRange_ThrowsNamingField(string field, string badValue)
    {
        string json = ValidJson.Replace(FieldPair(field), $@"""{field}"": {badValue}");

        var ex = Assert.Throws<CombatDataException>(() => CombatTuning.FromJson(json));

        Assert.Contains(field, ex.Message);
    }

    [Fact]
    public void FromJson_SameTextTwice_ProducesEqualValue()
    {
        Assert.Equal(CombatTuning.FromJson(ValidJson), CombatTuning.FromJson(ValidJson));
    }

    [Fact]
    public void ShippedCombatFile_EqualsEngineDefault()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "content", "combat.json");
        CombatTuning fromFile = CombatTuning.FromJson(File.ReadAllText(path));

        Assert.Equal(CombatTuning.Default, fromFile);
    }

    // The shipped values match ValidJson, so locating "field": <value> is unambiguous.
    private static string FieldPair(string field) => field switch
    {
        "guardDivisor"            => @"""guardDivisor"": 12000.0",
        "defenceDivisor"          => @"""defenceDivisor"": 80000.0",
        "baseDamageBonusDivisor"  => @"""baseDamageBonusDivisor"": 10000.0",
        "guardReduceCap"          => @"""guardReduceCap"": 0.60",
        "falloffStrength"         => @"""falloffStrength"": 0.25",
        _ => throw new ArgumentOutOfRangeException(nameof(field)),
    };
}
