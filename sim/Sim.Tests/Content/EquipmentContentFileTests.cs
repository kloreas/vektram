using System;
using System.IO;
using Sim.Content;
using Sim.Stats;
using Xunit;

namespace Sim.Tests.Content;

/// <summary>
/// Validates the shipped <c>equipment.json</c>: it parses, covers each first-pass slot, and
/// every modifier carries a valid (defined) stat/op. The test project performs the file read;
/// <c>/sim</c> only parses.
/// </summary>
public class EquipmentContentFileTests
{
    private static string ReadContent(string fileName) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "content", fileName));

    [Fact]
    public void ShippedEquipmentFile_ParsesSuccessfully()
    {
        EquipmentCatalog catalog = EquipmentCatalog.FromJson(ReadContent("equipment.json"));

        Assert.True(catalog.Count >= 3);
        Assert.Equal(EquipmentSlot.Weapon, catalog.Get("weapon_recruit_cannon").Slot);
        Assert.Equal(EquipmentSlot.Armor, catalog.Get("armor_recruit_plate").Slot);
        Assert.Equal(EquipmentSlot.Accessory, catalog.Get("accessory_keen_charm").Slot);
    }

    [Fact]
    public void ShippedEquipmentFile_EveryModifierHasDefinedStatAndOp()
    {
        EquipmentCatalog catalog = EquipmentCatalog.FromJson(ReadContent("equipment.json"));

        foreach (string id in catalog.Ids)
            foreach (StatModifier m in catalog.Get(id).Modifiers)
            {
                Assert.True(Enum.IsDefined(typeof(StatKind), m.Stat));
                Assert.True(Enum.IsDefined(typeof(ModifierOp), m.Op));
                Assert.Equal(ModifierSourceType.Equipment, m.Source.Type);
                Assert.Equal(id, m.Source.SourceId);
            }
    }
}
