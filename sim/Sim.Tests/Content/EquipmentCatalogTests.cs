using Sim.Content;
using Sim.Stats;
using Xunit;

namespace Sim.Tests.Content;

public class EquipmentCatalogTests
{
    private const string ValidJson = @"
{
  ""schemaVersion"": 1,
  ""equipment"": [
    { ""id"": ""weapon_recruit_cannon"", ""displayName"": ""Recruit Cannon"", ""slot"": ""Weapon"",
      ""modifiers"": [ { ""stat"": ""Attack"", ""op"": ""Flat"", ""value"": 2500.0 } ] },
    { ""id"": ""armor_recruit_plate"", ""displayName"": ""Recruit Plate"", ""slot"": ""Armor"",
      ""modifiers"": [ { ""stat"": ""Defense"", ""op"": ""Flat"", ""value"": 4000.0 },
                       { ""stat"": ""BaseGuard"", ""op"": ""Flat"", ""value"": 600.0 } ] }
  ]
}";

    [Fact]
    public void FromJson_Valid_LoadsAllDefinitions()
    {
        EquipmentCatalog catalog = EquipmentCatalog.FromJson(ValidJson);

        Assert.Equal(2, catalog.Count);
        Assert.Contains("weapon_recruit_cannon", catalog.Ids);
        Assert.Contains("armor_recruit_plate", catalog.Ids);
    }

    [Fact]
    public void Get_Piece_ReturnsSlotAndModifiers()
    {
        EquipmentCatalog catalog = EquipmentCatalog.FromJson(ValidJson);

        EquipmentDefinition armor = catalog.Get("armor_recruit_plate");

        Assert.Equal("Recruit Plate", armor.DisplayName);
        Assert.Equal(EquipmentSlot.Armor, armor.Slot);
        Assert.Equal(2, armor.Modifiers.Count);
        Assert.Equal(StatKind.Defense, armor.Modifiers[0].Stat);
        Assert.Equal(ModifierOp.Flat, armor.Modifiers[0].Op);
        Assert.Equal(4000.0, armor.Modifiers[0].Value);
    }

    [Fact]
    public void FromJson_StampsEquipmentSourceWithPieceId()
    {
        EquipmentCatalog catalog = EquipmentCatalog.FromJson(ValidJson);

        StatModifier modifier = catalog.Get("weapon_recruit_cannon").Modifiers[0];

        Assert.Equal(ModifierSourceType.Equipment, modifier.Source.Type);
        Assert.Equal("weapon_recruit_cannon", modifier.Source.SourceId);
    }

    [Fact]
    public void Get_UnknownId_ThrowsWithIdInMessage()
    {
        EquipmentCatalog catalog = EquipmentCatalog.FromJson(ValidJson);

        var ex = Assert.Throws<EquipmentDataException>(() => catalog.Get("missing"));

        Assert.Contains("missing", ex.Message);
    }

    [Fact]
    public void FromJson_MalformedJson_Throws()
    {
        Assert.Throws<EquipmentDataException>(() =>
            EquipmentCatalog.FromJson(@"{ ""schemaVersion"": 1, ""equipment"": ["));
    }

    [Fact]
    public void FromJson_UnsupportedSchemaVersion_Throws()
    {
        Assert.Throws<EquipmentDataException>(() =>
            EquipmentCatalog.FromJson(ValidJson.Replace(@"""schemaVersion"": 1", @"""schemaVersion"": 9")));
    }

    [Fact]
    public void FromJson_DuplicateId_ThrowsWithIdInMessage()
    {
        const string dup = @"
{
  ""schemaVersion"": 1,
  ""equipment"": [
    { ""id"": ""dup"", ""displayName"": ""A"", ""slot"": ""Weapon"", ""modifiers"": [] },
    { ""id"": ""dup"", ""displayName"": ""B"", ""slot"": ""Armor"", ""modifiers"": [] }
  ]
}";

        var ex = Assert.Throws<EquipmentDataException>(() => EquipmentCatalog.FromJson(dup));

        Assert.Contains("dup", ex.Message);
    }

    [Fact]
    public void FromJson_UnknownSlot_Throws()
    {
        string json = ValidJson.Replace(@"""slot"": ""Weapon""", @"""slot"": ""Mount""");

        Assert.Throws<EquipmentDataException>(() => EquipmentCatalog.FromJson(json));
    }

    [Fact]
    public void FromJson_UnknownStatKind_Throws()
    {
        string json = ValidJson.Replace(@"""stat"": ""Attack""", @"""stat"": ""Luck""");

        Assert.Throws<EquipmentDataException>(() => EquipmentCatalog.FromJson(json));
    }

    [Fact]
    public void FromJson_UnknownOp_Throws()
    {
        string json = ValidJson.Replace(@"""op"": ""Flat"", ""value"": 2500.0", @"""op"": ""Exponential"", ""value"": 2500.0");

        Assert.Throws<EquipmentDataException>(() => EquipmentCatalog.FromJson(json));
    }

    [Fact]
    public void FromJson_ModifierMissingValue_Throws()
    {
        string json = ValidJson.Replace(@"{ ""stat"": ""Attack"", ""op"": ""Flat"", ""value"": 2500.0 }",
                                        @"{ ""stat"": ""Attack"", ""op"": ""Flat"" }");

        Assert.Throws<EquipmentDataException>(() => EquipmentCatalog.FromJson(json));
    }

    [Fact]
    public void FromJson_EmptyId_Throws()
    {
        string json = ValidJson.Replace(@"""id"": ""weapon_recruit_cannon""", @"""id"": """"");

        Assert.Throws<EquipmentDataException>(() => EquipmentCatalog.FromJson(json));
    }

    [Fact]
    public void FromJson_PieceWithEmptyModifiers_IsAllowed()
    {
        const string json = @"
{
  ""schemaVersion"": 1,
  ""equipment"": [
    { ""id"": ""bare"", ""displayName"": ""Bare"", ""slot"": ""Accessory"", ""modifiers"": [] }
  ]
}";

        EquipmentCatalog catalog = EquipmentCatalog.FromJson(json);

        Assert.Empty(catalog.Get("bare").Modifiers);
    }

    [Fact]
    public void FromJson_SameTextTwice_ProducesValueEqualDefinitions()
    {
        EquipmentCatalog a = EquipmentCatalog.FromJson(ValidJson);
        EquipmentCatalog b = EquipmentCatalog.FromJson(ValidJson);

        Assert.Equal(a.Get("armor_recruit_plate"), b.Get("armor_recruit_plate"));
    }
}
