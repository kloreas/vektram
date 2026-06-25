using System;
using System.Collections.Generic;
using Sim.Content;
using Sim.Core;
using Sim.Match;
using Sim.Stats;
using Xunit;

namespace Sim.Tests.Stats;

public class LoadoutResolverTests
{
    private const string EquipmentJson = @"
{
  ""schemaVersion"": 1,
  ""equipment"": [
    { ""id"": ""weapon"", ""displayName"": ""Weapon"", ""slot"": ""Weapon"",
      ""modifiers"": [ { ""stat"": ""Attack"", ""op"": ""Flat"", ""value"": 2500.0 } ] },
    { ""id"": ""armor"", ""displayName"": ""Armor"", ""slot"": ""Armor"",
      ""modifiers"": [ { ""stat"": ""Defense"", ""op"": ""Flat"", ""value"": 4000.0 },
                       { ""stat"": ""MaxHp"", ""op"": ""Flat"", ""value"": 50.0 } ] }
  ]
}";

    private static EquipmentCatalog Catalog() => EquipmentCatalog.FromJson(EquipmentJson);

    private static CombatantStats Base() => new(100.0, 1.0, 0.0);

    [Fact]
    public void Resolve_BareLoadout_ReturnsBaseStats()
    {
        CombatantStats baseStats = Base();
        Loadout loadout = Loadout.Bare(baseStats);

        CombatantStats result = LoadoutResolver.Resolve(loadout, Catalog());

        Assert.Equal(baseStats, result);
    }

    [Fact]
    public void Resolve_SinglePiece_AppliesItsModifiers()
    {
        var loadout = new Loadout(Base(), new[] { "weapon" }, null, null);

        CombatantStats result = LoadoutResolver.Resolve(loadout, Catalog());

        Assert.Equal(2500.0, result.Attack);
    }

    [Fact]
    public void Resolve_MultipleSlots_StackAllModifiers()
    {
        var loadout = new Loadout(Base(), new[] { "weapon", "armor" }, null, null);

        CombatantStats result = LoadoutResolver.Resolve(loadout, Catalog());

        Assert.Equal(2500.0, result.Attack);
        Assert.Equal(4000.0, result.Defense);
        Assert.Equal(150.0, result.MaxHp);
    }

    [Fact]
    public void Resolve_UnknownPieceId_ThrowsEquipmentDataException()
    {
        var loadout = new Loadout(Base(), new[] { "missing" }, null, null);

        var ex = Assert.Throws<EquipmentDataException>(() => LoadoutResolver.Resolve(loadout, Catalog()));

        Assert.Contains("missing", ex.Message);
    }

    [Fact]
    public void Resolve_SameLoadoutTwice_IsDeterministic()
    {
        var loadout = new Loadout(Base(), new[] { "weapon", "armor" }, null, null);
        EquipmentCatalog catalog = Catalog();

        CombatantStats first = LoadoutResolver.Resolve(loadout, catalog);
        CombatantStats second = LoadoutResolver.Resolve(loadout, catalog);

        Assert.Equal(first, second);
    }

    [Fact]
    public void Resolve_RuneModifiers_StackWithEquipment()
    {
        var runes = new[] { new StatModifier(StatKind.Attack, ModifierOp.Flat, 500.0, new ModifierSource(ModifierSourceType.Rune, "r1")) };
        var loadout = new Loadout(Base(), new[] { "weapon" }, runes, null);

        CombatantStats result = LoadoutResolver.Resolve(loadout, Catalog());

        Assert.Equal(3000.0, result.Attack);
    }

    [Fact]
    public void Resolve_ChangingCosmetic_NeverChangesStats()
    {
        EquipmentCatalog catalog = Catalog();
        var plain = new Loadout(Base(), new[] { "weapon", "armor" }, null, null);
        var costumed = new Loadout(Base(), new[] { "weapon", "armor" }, null, "flaming_skin");

        CombatantStats plainStats = LoadoutResolver.Resolve(plain, catalog);
        CombatantStats costumedStats = LoadoutResolver.Resolve(costumed, catalog);

        Assert.Equal(plainStats, costumedStats);
    }

    [Fact]
    public void Resolve_NullCatalog_ThrowsEquipmentDataException()
    {
        var loadout = Loadout.Bare(Base());

        Assert.Throws<EquipmentDataException>(() => LoadoutResolver.Resolve(loadout, null!));
    }

    [Fact]
    public void Resolve_ResultFeedsDamageCalculatorUnchanged()
    {
        EquipmentCatalog catalog = Catalog();
        CombatantStats unarmed = LoadoutResolver.Resolve(Loadout.Bare(Base()), catalog);
        CombatantStats armed = LoadoutResolver.Resolve(new Loadout(Base(), new[] { "weapon" }, null, null), catalog);

        double unarmedDamage = ComputeCenterHit(unarmed).FinalDamage;
        double armedDamage = ComputeCenterHit(armed).FinalDamage;

        Assert.True(armedDamage > unarmedDamage);
    }

    private static DamageResult ComputeCenterHit(CombatantStats attacker)
    {
        const double baseDamage = 100.0;
        const double blastRadius = 10.0;
        var origin = new Vec2D(0.0, 0.0);

        var inputs = new DamageInputs(
            origin, origin, baseDamage, blastRadius,
            attacker, CombatantStats.Default, CombatTuning.Default,
            ModeMultiplier: 1.0, ElementAdvantage: 1.0, IsCrit: false, IsMiss: false);

        return DamageCalculator.Compute(inputs);
    }
}
