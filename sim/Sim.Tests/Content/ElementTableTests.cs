using System;
using System.IO;
using Sim.Content;
using Xunit;

namespace Sim.Tests.Content;

public class ElementTableTests
{
    private const string ValidJson = @"
{
  ""schemaVersion"": 1,
  ""elements"": [""None"", ""Fire"", ""Water"", ""Wind"", ""Land"", ""Light"", ""Dark""],
  ""advantages"": [
    { ""attacker"": ""Fire"",  ""defender"": ""Wind"",  ""multiplier"": 1.25 },
    { ""attacker"": ""Water"", ""defender"": ""Fire"",  ""multiplier"": 1.25 }
  ]
}";

    [Fact]
    public void FromJson_Valid_ReadsOverrides()
    {
        ElementTable table = ElementTable.FromJson(ValidJson);

        Assert.Equal(2, table.OverrideCount);
        Assert.Equal(1.25, table.Advantage(Element.Fire, Element.Wind));
        Assert.Equal(1.25, table.Advantage(Element.Water, Element.Fire));
    }

    [Fact]
    public void Advantage_UnspecifiedPair_ReturnsNeutralOne()
    {
        ElementTable table = ElementTable.FromJson(ValidJson);

        Assert.Equal(1.0, table.Advantage(Element.Wind, Element.Fire));   // reverse pair not listed
        Assert.Equal(1.0, table.Advantage(Element.Light, Element.Dark));
        Assert.Equal(1.0, table.Advantage(Element.None, Element.Fire));
    }

    [Fact]
    public void Neutral_ReturnsOneForEveryPair()
    {
        Assert.Equal(0, ElementTable.Neutral.OverrideCount);
        Assert.Equal(1.0, ElementTable.Neutral.Advantage(Element.Fire, Element.Water));
    }

    [Fact]
    public void FromJson_MalformedJson_Throws()
    {
        Assert.Throws<CombatDataException>(() => ElementTable.FromJson(@"{ ""schemaVersion"": 1, ""advantages"": ["));
    }

    [Fact]
    public void FromJson_UnsupportedSchemaVersion_Throws()
    {
        Assert.Throws<CombatDataException>(() =>
            ElementTable.FromJson(ValidJson.Replace(@"""schemaVersion"": 1", @"""schemaVersion"": 2")));
    }

    [Fact]
    public void FromJson_UnknownElement_Throws()
    {
        const string json = @"
{
  ""schemaVersion"": 1,
  ""elements"": [""None"", ""Fire""],
  ""advantages"": [ { ""attacker"": ""Plasma"", ""defender"": ""Fire"", ""multiplier"": 1.25 } ]
}";

        Assert.Throws<CombatDataException>(() => ElementTable.FromJson(json));
    }

    [Fact]
    public void FromJson_DuplicatePair_Throws()
    {
        const string json = @"
{
  ""schemaVersion"": 1,
  ""elements"": [""None"", ""Fire"", ""Water""],
  ""advantages"": [
    { ""attacker"": ""Fire"", ""defender"": ""Water"", ""multiplier"": 1.25 },
    { ""attacker"": ""Fire"", ""defender"": ""Water"", ""multiplier"": 0.8 }
  ]
}";

        var ex = Assert.Throws<CombatDataException>(() => ElementTable.FromJson(json));

        Assert.Contains("Fire", ex.Message);
    }

    [Fact]
    public void FromJson_NegativeMultiplier_Throws()
    {
        string json = ValidJson.Replace(@"""multiplier"": 1.25", @"""multiplier"": -1.0");

        Assert.Throws<CombatDataException>(() => ElementTable.FromJson(json));
    }

    [Fact]
    public void FromJson_SameTextTwice_ProducesEqualLookups()
    {
        ElementTable a = ElementTable.FromJson(ValidJson);
        ElementTable b = ElementTable.FromJson(ValidJson);

        Assert.Equal(a.Advantage(Element.Fire, Element.Wind), b.Advantage(Element.Fire, Element.Wind));
        Assert.Equal(a.OverrideCount, b.OverrideCount);
    }

    [Fact]
    public void ShippedElementsFile_ParsesWithExpectedAdvantage()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "content", "elements.json");
        ElementTable table = ElementTable.FromJson(File.ReadAllText(path));

        Assert.Equal(1.25, table.Advantage(Element.Fire, Element.Wind));
        Assert.Equal(1.25, table.Advantage(Element.Light, Element.Dark));
        Assert.Equal(1.0, table.Advantage(Element.Fire, Element.Fire));
    }
}
