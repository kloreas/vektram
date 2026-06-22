using Sim.Content;
using Xunit;

namespace Sim.Tests.Content;

public class ItemCatalogTests
{
    private const string ValidJson = @"
{
  ""schemaVersion"": 1,
  ""items"": [
    { ""id"": ""shell_standard"", ""displayName"": ""Standard Shell"", ""category"": ""BallGrant"",
      ""maxStack"": 99, ""effect"": { ""kind"": ""GrantBall"", ""ballId"": ""standard"" } },
    { ""id"": ""heal_small"", ""displayName"": ""Small Heal Stone"", ""category"": ""Consumable"",
      ""maxStack"": 20, ""effect"": { ""kind"": ""RestoreHp"", ""amount"": 150.0 } }
  ]
}";

    [Fact]
    public void FromJson_Valid_LoadsAllDefinitions()
    {
        ItemCatalog catalog = ItemCatalog.FromJson(ValidJson);

        Assert.Equal(2, catalog.Count);
        Assert.Contains("shell_standard", catalog.Ids);
        Assert.Contains("heal_small", catalog.Ids);
    }

    [Fact]
    public void Get_GrantBallItem_ReturnsAllFields()
    {
        ItemCatalog catalog = ItemCatalog.FromJson(ValidJson);

        ItemDefinition shell = catalog.Get("shell_standard");

        Assert.Equal("Standard Shell", shell.DisplayName);
        Assert.Equal(ItemCategory.BallGrant, shell.Category);
        Assert.Equal(99, shell.MaxStack);
        Assert.Equal(ItemEffectKind.GrantBall, shell.Effect.Kind);
        Assert.Equal("standard", shell.Effect.BallId);
    }

    [Fact]
    public void Get_RestoreHpItem_ReturnsAmount()
    {
        ItemCatalog catalog = ItemCatalog.FromJson(ValidJson);

        ItemDefinition heal = catalog.Get("heal_small");

        Assert.Equal(ItemEffectKind.RestoreHp, heal.Effect.Kind);
        Assert.Equal(150.0, heal.Effect.Amount);
        Assert.Null(heal.Effect.BallId);
    }

    [Fact]
    public void Get_UnknownId_ThrowsWithIdInMessage()
    {
        ItemCatalog catalog = ItemCatalog.FromJson(ValidJson);

        var ex = Assert.Throws<ItemDataException>(() => catalog.Get("missing"));

        Assert.Contains("missing", ex.Message);
    }

    [Fact]
    public void FromJson_MalformedJson_Throws()
    {
        Assert.Throws<ItemDataException>(() => ItemCatalog.FromJson(@"{ ""schemaVersion"": 1, ""items"": ["));
    }

    [Fact]
    public void FromJson_UnsupportedSchemaVersion_Throws()
    {
        Assert.Throws<ItemDataException>(() =>
            ItemCatalog.FromJson(ValidJson.Replace(@"""schemaVersion"": 1", @"""schemaVersion"": 7")));
    }

    [Fact]
    public void FromJson_DuplicateId_ThrowsWithIdInMessage()
    {
        const string dup = @"
{
  ""schemaVersion"": 1,
  ""items"": [
    { ""id"": ""dup"", ""displayName"": ""A"", ""category"": ""Consumable"", ""maxStack"": 1,
      ""effect"": { ""kind"": ""RestoreHp"", ""amount"": 1.0 } },
    { ""id"": ""dup"", ""displayName"": ""B"", ""category"": ""Consumable"", ""maxStack"": 1,
      ""effect"": { ""kind"": ""RestoreHp"", ""amount"": 1.0 } }
  ]
}";

        var ex = Assert.Throws<ItemDataException>(() => ItemCatalog.FromJson(dup));

        Assert.Contains("dup", ex.Message);
    }

    [Fact]
    public void FromJson_UnknownCategory_Throws()
    {
        string json = ValidJson.Replace(@"""category"": ""Consumable""", @"""category"": ""Mount""");

        Assert.Throws<ItemDataException>(() => ItemCatalog.FromJson(json));
    }

    [Fact]
    public void FromJson_UnknownEffectKind_Throws()
    {
        string json = ValidJson.Replace(@"""kind"": ""RestoreHp"", ""amount"": 150.0", @"""kind"": ""Teleport""");

        Assert.Throws<ItemDataException>(() => ItemCatalog.FromJson(json));
    }

    [Fact]
    public void FromJson_GrantBallMissingBallId_Throws()
    {
        const string json = @"
{
  ""schemaVersion"": 1,
  ""items"": [
    { ""id"": ""x"", ""displayName"": ""X"", ""category"": ""BallGrant"", ""maxStack"": 1,
      ""effect"": { ""kind"": ""GrantBall"" } }
  ]
}";

        Assert.Throws<ItemDataException>(() => ItemCatalog.FromJson(json));
    }

    [Fact]
    public void FromJson_RestoreHpNegativeAmount_Throws()
    {
        string json = ValidJson.Replace(@"""amount"": 150.0", @"""amount"": -10.0");

        Assert.Throws<ItemDataException>(() => ItemCatalog.FromJson(json));
    }

    [Fact]
    public void FromJson_MaxStackBelowOne_Throws()
    {
        string json = ValidJson.Replace(@"""maxStack"": 20", @"""maxStack"": 0");

        Assert.Throws<ItemDataException>(() => ItemCatalog.FromJson(json));
    }

    [Fact]
    public void FromJson_SameTextTwice_ProducesValueEqualDefinitions()
    {
        ItemCatalog a = ItemCatalog.FromJson(ValidJson);
        ItemCatalog b = ItemCatalog.FromJson(ValidJson);

        Assert.Equal(a.Get("shell_standard"), b.Get("shell_standard"));
        Assert.Equal(a.Get("heal_small"), b.Get("heal_small"));
    }

    [Fact]
    public void ValidateBallReferences_AllResolve_DoesNotThrow()
    {
        ItemCatalog items = ItemCatalog.FromJson(ValidJson);
        BallCatalog balls = BallCatalog.FromJson(BallJsonWith("standard"));

        items.ValidateBallReferences(balls);   // should not throw
    }

    [Fact]
    public void ValidateBallReferences_DanglingBallId_Throws()
    {
        ItemCatalog items = ItemCatalog.FromJson(ValidJson);
        BallCatalog balls = BallCatalog.FromJson(BallJsonWith("other"));   // no "standard"

        var ex = Assert.Throws<ItemDataException>(() => items.ValidateBallReferences(balls));

        Assert.Contains("standard", ex.Message);
    }

    private static string BallJsonWith(string ballId) => $@"
{{
  ""schemaVersion"": 1,
  ""balls"": [
    {{ ""id"": ""{ballId}"", ""displayName"": ""X"", ""type"": ""Standard"",
       ""gravityScale"": 1.0, ""windSensitivity"": 1.0, ""blastRadius"": 1.0,
       ""baseDamage"": 1.0, ""projectileSpeed"": 1.0 }}
  ]
}}";
}
