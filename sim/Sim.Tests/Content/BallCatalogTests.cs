using System.Collections.Generic;
using Sim.Content;
using Xunit;

namespace Sim.Tests.Content;

public class BallCatalogTests
{
    private const string ValidJson = @"
{
  ""schemaVersion"": 1,
  ""balls"": [
    { ""id"": ""standard"", ""displayName"": ""Standard Shell"", ""type"": ""Standard"",
      ""gravityScale"": 1.0, ""windSensitivity"": 1.0, ""blastRadius"": 2.5, ""baseDamage"": 100.0, ""projectileSpeed"": 45.0 },
    { ""id"": ""heavy"", ""displayName"": ""Heavy Shell"", ""type"": ""Heavy"",
      ""gravityScale"": 1.4, ""windSensitivity"": 0.5, ""blastRadius"": 3.2, ""baseDamage"": 140.0, ""projectileSpeed"": 40.0 }
  ]
}";

    [Fact]
    public void FromJson_ValidDocument_LoadsAllDefinitions()
    {
        BallCatalog catalog = BallCatalog.FromJson(ValidJson);

        Assert.Equal(2, catalog.Count);
        Assert.Contains("standard", catalog.Ids);
        Assert.Contains("heavy", catalog.Ids);
    }

    [Fact]
    public void Get_KnownId_ReturnsAllFields()
    {
        BallCatalog catalog = BallCatalog.FromJson(ValidJson);

        BallDefinition heavy = catalog.Get("heavy");

        Assert.Equal("heavy", heavy.Id);
        Assert.Equal("Heavy Shell", heavy.DisplayName);
        Assert.Equal(ShellType.Heavy, heavy.Type);
        Assert.Equal(1.4, heavy.Physics.GravityScale);
        Assert.Equal(0.5, heavy.Physics.WindSensitivity);
        Assert.Equal(3.2, heavy.BlastRadius);
        Assert.Equal(140.0, heavy.BaseDamage);
        Assert.Equal(40.0, heavy.ProjectileSpeed);
    }

    [Fact]
    public void Get_UnknownId_ThrowsWithIdInMessage()
    {
        BallCatalog catalog = BallCatalog.FromJson(ValidJson);

        var ex = Assert.Throws<BallDataException>(() => catalog.Get("missing"));

        Assert.Contains("missing", ex.Message);
    }

    [Fact]
    public void TryGet_UnknownId_ReturnsFalse()
    {
        BallCatalog catalog = BallCatalog.FromJson(ValidJson);

        bool found = catalog.TryGet("missing", out BallDefinition _);

        Assert.False(found);
    }

    [Fact]
    public void FromJson_MalformedJson_ThrowsBallDataException()
    {
        const string malformed = @"{ ""schemaVersion"": 1, ""balls"": [ ";

        Assert.Throws<BallDataException>(() => BallCatalog.FromJson(malformed));
    }

    [Fact]
    public void FromJson_DuplicateIds_ThrowsWithIdInMessage()
    {
        const string duplicate = @"
{
  ""schemaVersion"": 1,
  ""balls"": [
    { ""id"": ""dup"", ""displayName"": ""A"", ""type"": ""Standard"",
      ""gravityScale"": 1.0, ""windSensitivity"": 1.0, ""blastRadius"": 1.0, ""baseDamage"": 1.0, ""projectileSpeed"": 1.0 },
    { ""id"": ""dup"", ""displayName"": ""B"", ""type"": ""Standard"",
      ""gravityScale"": 1.0, ""windSensitivity"": 1.0, ""blastRadius"": 1.0, ""baseDamage"": 1.0, ""projectileSpeed"": 1.0 }
  ]
}";

        var ex = Assert.Throws<BallDataException>(() => BallCatalog.FromJson(duplicate));

        Assert.Contains("dup", ex.Message);
    }

    [Fact]
    public void FromJson_UnsupportedSchemaVersion_Throws()
    {
        const string future = @"{ ""schemaVersion"": 99, ""balls"": [] }";

        Assert.Throws<BallDataException>(() => BallCatalog.FromJson(future));
    }

    [Theory]
    [InlineData("gravityScale", "0.0")]
    [InlineData("gravityScale", "-1.0")]
    [InlineData("windSensitivity", "-0.1")]
    [InlineData("blastRadius", "0.0")]
    [InlineData("baseDamage", "-5.0")]
    [InlineData("projectileSpeed", "-1.0")]
    public void FromJson_InvalidNumericValue_ThrowsNamingField(string field, string badValue)
    {
        string json = BuildOneBall(("type", "\"Standard\""), (field, badValue));

        var ex = Assert.Throws<BallDataException>(() => BallCatalog.FromJson(json));

        Assert.Contains(field, ex.Message);
    }

    [Fact]
    public void FromJson_UnknownShellType_Throws()
    {
        string json = BuildOneBall(("type", "\"Plasma\""));

        Assert.Throws<BallDataException>(() => BallCatalog.FromJson(json));
    }

    [Fact]
    public void FromJson_SameTextTwice_ProducesValueEqualDefinitions()
    {
        BallCatalog first  = BallCatalog.FromJson(ValidJson);
        BallCatalog second = BallCatalog.FromJson(ValidJson);

        Assert.Equal(first.Get("standard"), second.Get("standard"));
        Assert.Equal(first.Get("heavy"), second.Get("heavy"));
    }

    private static string BuildOneBall(params (string Field, string RawValue)[] overrides)
    {
        var fields = new Dictionary<string, string>
        {
            ["id"]              = "\"x\"",
            ["displayName"]     = "\"X\"",
            ["type"]            = "\"Standard\"",
            ["gravityScale"]    = "1.0",
            ["windSensitivity"] = "1.0",
            ["blastRadius"]     = "1.0",
            ["baseDamage"]      = "1.0",
            ["projectileSpeed"] = "1.0",
        };
        foreach ((string field, string rawValue) in overrides)
            fields[field] = rawValue;

        var pairs = new List<string>();
        foreach (KeyValuePair<string, string> entry in fields)
            pairs.Add($"\"{entry.Key}\": {entry.Value}");
        string body = string.Join(", ", pairs);

        return $@"{{ ""schemaVersion"": 1, ""balls"": [ {{ {body} }} ] }}";
    }
}
