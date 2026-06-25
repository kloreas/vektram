using System;
using System.Collections.Generic;
using Sim.Content;
using Sim.Match;
using Sim.Stats;
using Xunit;

namespace Sim.Tests.Stats;

public class StatAssemblerTests
{
    private static readonly ModifierSource Gear = new(ModifierSourceType.Equipment, "gear");
    private static readonly ModifierSource RuneSource = new(ModifierSourceType.Rune, "rune");

    private static CombatantStats Base(double attack = 0.0) =>
        new(100.0, 1.0, 0.0) { Attack = attack };

    [Fact]
    public void ChannelCount_MatchesStatKindValueCount()
    {
        int kindValues = Enum.GetValues(typeof(StatKind)).Length;

        Assert.Equal(StatAssembler.ChannelCount, kindValues);
    }

    [Fact]
    public void Assemble_NoModifiers_ReturnsBaseUnchanged()
    {
        CombatantStats baseStats = Base(attack: 42.0);

        CombatantStats result = StatAssembler.Assemble(baseStats, Array.Empty<StatModifier>());

        Assert.Equal(baseStats, result);
    }

    [Fact]
    public void Assemble_SingleFlat_AddsToChannel()
    {
        CombatantStats baseStats = Base(attack: 100.0);
        var modifiers = new[] { new StatModifier(StatKind.Attack, ModifierOp.Flat, 20.0, Gear) };

        CombatantStats result = StatAssembler.Assemble(baseStats, modifiers);

        Assert.Equal(120.0, result.Attack);
    }

    [Fact]
    public void Assemble_FlatThenAdditiveThenMultiplicative_AppliesInFixedOrder()
    {
        CombatantStats baseStats = Base(attack: 100.0);
        var modifiers = new[]
        {
            new StatModifier(StatKind.Attack, ModifierOp.Flat, 20.0, Gear),
            new StatModifier(StatKind.Attack, ModifierOp.AdditivePercent, 0.10, RuneSource),
            new StatModifier(StatKind.Attack, ModifierOp.MultiplicativePercent, 0.05, RuneSource),
        };

        CombatantStats result = StatAssembler.Assemble(baseStats, modifiers);

        // (100 + 20) * (1 + 0.10) * (1 + 0.05) = 138.6
        Assert.Equal(138.6, result.Attack, 6);
    }

    [Fact]
    public void Assemble_MultipleSameOp_SumsFlatsAndAdditives_MultipliesMultiplicatives()
    {
        CombatantStats baseStats = Base(attack: 0.0);
        var modifiers = new[]
        {
            new StatModifier(StatKind.Attack, ModifierOp.Flat, 10.0, Gear),
            new StatModifier(StatKind.Attack, ModifierOp.Flat, 30.0, Gear),
            new StatModifier(StatKind.Attack, ModifierOp.AdditivePercent, 0.25, RuneSource),
            new StatModifier(StatKind.Attack, ModifierOp.AdditivePercent, 0.25, RuneSource),
            new StatModifier(StatKind.Attack, ModifierOp.MultiplicativePercent, 0.5, RuneSource),
            new StatModifier(StatKind.Attack, ModifierOp.MultiplicativePercent, 0.25, RuneSource),
        };

        CombatantStats result = StatAssembler.Assemble(baseStats, modifiers);

        // (0 + 40) * (1 + 0.5) * (1.5 * 1.25) = 40 * 1.5 * 1.875 = 112.5
        Assert.Equal(112.5, result.Attack, 9);
    }

    [Fact]
    public void Assemble_SameInputTwice_ProducesIdenticalResult()
    {
        CombatantStats baseStats = Base(attack: 100.0);
        var modifiers = new[]
        {
            new StatModifier(StatKind.Attack, ModifierOp.Flat, 13.0, Gear),
            new StatModifier(StatKind.Attack, ModifierOp.MultiplicativePercent, 0.5, RuneSource),
        };

        CombatantStats first = StatAssembler.Assemble(baseStats, modifiers);
        CombatantStats second = StatAssembler.Assemble(baseStats, modifiers);

        Assert.Equal(first, second);
    }

    [Fact]
    public void Assemble_ReorderedSameSet_ProducesBitIdenticalResult()
    {
        CombatantStats baseStats = Base(attack: 0.0);
        // Exactly-representable operands so Sigma/Pi are bit-stable under reordering.
        var ordered = new[]
        {
            new StatModifier(StatKind.Attack, ModifierOp.Flat, 10.0, Gear),
            new StatModifier(StatKind.Attack, ModifierOp.Flat, 20.0, Gear),
            new StatModifier(StatKind.Attack, ModifierOp.MultiplicativePercent, 0.5, RuneSource),
            new StatModifier(StatKind.Attack, ModifierOp.MultiplicativePercent, 0.25, RuneSource),
        };
        var shuffled = new[] { ordered[2], ordered[0], ordered[3], ordered[1] };

        CombatantStats a = StatAssembler.Assemble(baseStats, ordered);
        CombatantStats b = StatAssembler.Assemble(baseStats, shuffled);

        Assert.Equal(a.Attack, b.Attack);   // exact equality, not approximate
        Assert.Equal(a, b);
    }

    [Fact]
    public void Assemble_DroppingOneSource_ReproducesWithoutThatSource()
    {
        CombatantStats baseStats = Base(attack: 50.0);
        var gearMod = new StatModifier(StatKind.Attack, ModifierOp.Flat, 10.0, Gear);
        var runeMod = new StatModifier(StatKind.Attack, ModifierOp.Flat, 20.0, RuneSource);

        CombatantStats withoutRune = StatAssembler.Assemble(
            baseStats, FilterOutSource(new[] { gearMod, runeMod }, RuneSource.SourceId));
        CombatantStats gearOnly = StatAssembler.Assemble(baseStats, new[] { gearMod });

        Assert.Equal(gearOnly, withoutRune);
    }

    [Fact]
    public void Assemble_RuneAndEquipmentModifiers_StackTogether()
    {
        CombatantStats baseStats = Base(attack: 0.0);
        var modifiers = new[]
        {
            new StatModifier(StatKind.Attack, ModifierOp.Flat, 10.0, Gear),
            new StatModifier(StatKind.Attack, ModifierOp.Flat, 20.0, RuneSource),
        };

        CombatantStats result = StatAssembler.Assemble(baseStats, modifiers);

        Assert.Equal(30.0, result.Attack);
    }

    [Fact]
    public void Assemble_CritChanceAboveOne_ClampsToOne()
    {
        CombatantStats baseStats = Base();
        var modifiers = new[] { new StatModifier(StatKind.CritChance, ModifierOp.Flat, 2.0, Gear) };

        CombatantStats result = StatAssembler.Assemble(baseStats, modifiers);

        Assert.Equal(1.0, result.CritChance);
    }

    [Fact]
    public void Assemble_DodgeBelowZero_ClampsToZero()
    {
        CombatantStats baseStats = Base();
        var modifiers = new[] { new StatModifier(StatKind.Dodge, ModifierOp.Flat, -1.0, Gear) };

        CombatantStats result = StatAssembler.Assemble(baseStats, modifiers);

        Assert.Equal(0.0, result.Dodge);
    }

    [Fact]
    public void Assemble_MaxHpDrivenNegative_FloorsAboveZero()
    {
        CombatantStats baseStats = Base();
        var modifiers = new[] { new StatModifier(StatKind.MaxHp, ModifierOp.Flat, -500.0, Gear) };

        CombatantStats result = StatAssembler.Assemble(baseStats, modifiers);

        Assert.True(result.MaxHp > 0.0);
    }

    [Fact]
    public void Assemble_AttackDrivenNegative_FloorsAtZero()
    {
        CombatantStats baseStats = Base(attack: 10.0);
        var modifiers = new[] { new StatModifier(StatKind.Attack, ModifierOp.Flat, -50.0, Gear) };

        CombatantStats result = StatAssembler.Assemble(baseStats, modifiers);

        Assert.Equal(0.0, result.Attack);
    }

    [Fact]
    public void Assemble_NeverTouchesElement()
    {
        CombatantStats baseStats = Base() with { Element = Element.Fire };
        var modifiers = new[] { new StatModifier(StatKind.ElementPower, ModifierOp.Flat, 100.0, Gear) };

        CombatantStats result = StatAssembler.Assemble(baseStats, modifiers);

        Assert.Equal(Element.Fire, result.Element);
    }

    [Fact]
    public void Assemble_NullModifiers_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => StatAssembler.Assemble(Base(), null!));
    }

    private static IReadOnlyList<StatModifier> FilterOutSource(IEnumerable<StatModifier> modifiers, string sourceId)
    {
        var kept = new List<StatModifier>();
        foreach (StatModifier m in modifiers)
            if (!string.Equals(m.Source.SourceId, sourceId, StringComparison.Ordinal))
                kept.Add(m);
        return kept;
    }
}
