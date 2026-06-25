using System;
using System.Collections.Generic;
using Sim.Match;

namespace Sim.Stats;

/// <summary>
/// Pure, deterministic assembly of effective <see cref="CombatantStats"/> from a base stat
/// block plus a list of source-tagged <see cref="StatModifier"/>s. This is the layer that
/// PRODUCES the stats the damage formula consumes; the formula never knows their origin.
/// </summary>
/// <remarks>
/// <para>
/// Per-channel calculation order is fixed: <c>flat → additive% → multiplicative%</c>:
/// <code>
///   final = (base + ΣFlat) × (1 + ΣAdditivePercent) × Π(1 + MultiplicativePercent_i)
/// </code>
/// This subsumes DDTank's flat equip adds (the <see cref="ModifierOp.Flat"/> op) while adding
/// modern percent levers (ADR-0005). The <see cref="CombatantStats.Element"/> enum is carried
/// through unchanged — it is assigned, not stacked.
/// </para>
/// <para>
/// DETERMINISM: modifiers are accumulated in the order supplied by the caller, which fixes the
/// gather order (see <see cref="LoadoutResolver"/>). The per-channel Σ and Π are mathematically
/// order-independent; for exactly-representable operands a re-ordered modifier set yields a
/// bit-identical result. No RNG, no I/O, no Unity.
/// </para>
/// <para>
/// CLAMPS (structural invariants, not balance tuning): <see cref="CombatantStats.CritChance"/>
/// and <see cref="CombatantStats.Dodge"/> are clamped to [0, 1]; <see cref="CombatantStats.MaxHp"/>
/// is floored above zero; every other channel is floored at zero.
/// </para>
/// </remarks>
public static class StatAssembler
{
    /// <summary>Number of stat channels — must equal the count of <see cref="StatKind"/> values.</summary>
    public const int ChannelCount = 11;

    // A combatant always has positive HP capacity; this is an edge guard against pathological
    // modifier data zeroing MaxHp, not a balance value.
    private const double MinMaxHp = 1.0;

    /// <summary>
    /// Assembles effective stats from <paramref name="baseStats"/> and <paramref name="modifiers"/>.
    /// Pure: identical inputs (including modifier order) always yield identical output.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="modifiers"/> is null.</exception>
    public static CombatantStats Assemble(CombatantStats baseStats, IReadOnlyList<StatModifier> modifiers)
    {
        if (modifiers is null)
            throw new ArgumentNullException(nameof(modifiers));

        var flat = new double[ChannelCount];
        var addPercent = new double[ChannelCount];
        var multFactor = new double[ChannelCount];
        for (int i = 0; i < ChannelCount; i++)
            multFactor[i] = 1.0;

        for (int i = 0; i < modifiers.Count; i++)
        {
            StatModifier m = modifiers[i];
            int channel = (int)m.Stat;
            switch (m.Op)
            {
                case ModifierOp.Flat:
                    flat[channel] += m.Value;
                    break;
                case ModifierOp.AdditivePercent:
                    addPercent[channel] += m.Value;
                    break;
                case ModifierOp.MultiplicativePercent:
                    multFactor[channel] *= 1.0 + m.Value;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(modifiers), $"Unknown modifier op '{m.Op}'.");
            }
        }

        double Resolve(StatKind kind, double baseValue)
        {
            int channel = (int)kind;
            return (baseValue + flat[channel]) * (1.0 + addPercent[channel]) * multFactor[channel];
        }

        return baseStats with
        {
            MaxHp          = Math.Max(MinMaxHp, Resolve(StatKind.MaxHp, baseStats.MaxHp)),
            DamageModifier = FloorZero(Resolve(StatKind.DamageModifier, baseStats.DamageModifier)),
            Defense        = FloorZero(Resolve(StatKind.Defense, baseStats.Defense)),
            Attack         = FloorZero(Resolve(StatKind.Attack, baseStats.Attack)),
            SunderArmor    = FloorZero(Resolve(StatKind.SunderArmor, baseStats.SunderArmor)),
            BaseGuard      = FloorZero(Resolve(StatKind.BaseGuard, baseStats.BaseGuard)),
            CritChance     = Clamp01(Resolve(StatKind.CritChance, baseStats.CritChance)),
            CritMultiplier = FloorZero(Resolve(StatKind.CritMultiplier, baseStats.CritMultiplier)),
            Dodge          = Clamp01(Resolve(StatKind.Dodge, baseStats.Dodge)),
            ElementPower   = FloorZero(Resolve(StatKind.ElementPower, baseStats.ElementPower)),
            ElementResist  = FloorZero(Resolve(StatKind.ElementResist, baseStats.ElementResist)),
            // Element is carried through unchanged by `with`.
        };
    }

    private static double FloorZero(double value) => value < 0.0 ? 0.0 : value;

    private static double Clamp01(double value) => value < 0.0 ? 0.0 : value > 1.0 ? 1.0 : value;
}
