using System;
using Sim.Content;

namespace Sim.Match;

/// <summary>
/// Pure, stateless blast-damage computation. The formula shape is adopted from DDTank's
/// <c>MakeDamage</c> (ADR-0005): attack scaling, multiplicative guard/defence damage
/// reduction, distance falloff, an element layer, and a crit multiplier — with all divisors
/// and curves supplied as tuning data rather than hardcoded, and all RNG resolved by the
/// caller.
/// </summary>
public static class DamageCalculator
{
    /// <summary>
    /// Computes the final damage dealt to one combatant by a blast.
    /// </summary>
    /// <remarks>
    /// Formula (see <see cref="DamageInputs"/> for inputs):
    /// <code>
    ///   distance ≥ radius        → 0   (out of range)
    ///   IsMiss                   → 0   (dodged)
    ///   falloff       = 1 − Tuning.FalloffStrength × (distance / radius)
    ///   attackFactor  = Tuning.AttackFloor + Attacker.Attack × Tuning.AttackScale
    ///   guardReduce   = clamp((Defender.BaseGuard − Attacker.SunderArmor) / Tuning.GuardDivisor,   0, GuardReduceCap)
    ///   defenceReduce = clamp(Defender.Defense / Tuning.DefenceDivisor,                            0, DefenceReduceCap)
    ///   core          = BaseDamage × attackFactor × (1 + BaseDamage / Tuning.BaseDamageBonusDivisor)
    ///                   × falloff × (1 − guardReduce) × (1 − defenceReduce)
    ///                   × Attacker.DamageModifier × ModeMultiplier × (IsCrit ? Attacker.CritMultiplier : 1)
    ///   elementBonus  = max(0, Attacker.ElementPower − Defender.ElementResist) × ElementAdvantage
    ///   final         = max(0, core + elementBonus)
    /// </code>
    /// Pure and deterministic: identical inputs always produce an identical result.
    /// </remarks>
    public static DamageResult Compute(in DamageInputs inputs)
    {
        double distance = (inputs.TargetPosition - inputs.ImpactPoint).Length;

        if (distance >= inputs.BlastRadius)
            return Zero(inputs, isMiss: false);

        if (inputs.IsMiss)
            return Zero(inputs, isMiss: true);

        CombatantStats attacker = inputs.Attacker;
        CombatantStats defender = inputs.Defender;
        CombatTuning   t        = inputs.Tuning;

        double falloff      = 1.0 - t.FalloffStrength * (distance / inputs.BlastRadius);
        double attackFactor = t.AttackFloor + attacker.Attack * t.AttackScale;
        double baseScale    = 1.0 + inputs.BaseDamage / t.BaseDamageBonusDivisor;

        double guardReduce   = Clamp((defender.BaseGuard - attacker.SunderArmor) / t.GuardDivisor, 0.0, t.GuardReduceCap);
        double defenceReduce = Clamp(defender.Defense / t.DefenceDivisor, 0.0, t.DefenceReduceCap);

        double critFactor = inputs.IsCrit ? attacker.CritMultiplier : 1.0;

        double core = inputs.BaseDamage * attackFactor * baseScale * falloff
                      * (1.0 - guardReduce) * (1.0 - defenceReduce)
                      * attacker.DamageModifier * inputs.ModeMultiplier * critFactor;

        double elementBonus = Math.Max(0.0, attacker.ElementPower - defender.ElementResist) * inputs.ElementAdvantage;

        double finalDamage = Math.Max(0.0, core + elementBonus);

        return new DamageResult(
            finalDamage,
            IsMiss: false,
            IsCrit: inputs.IsCrit,
            Falloff: falloff,
            GuardReduce: guardReduce,
            DefenceReduce: defenceReduce,
            AttackFactor: attackFactor,
            ElementBonus: elementBonus,
            ModeMultiplier: inputs.ModeMultiplier);
    }

    private static DamageResult Zero(in DamageInputs inputs, bool isMiss) =>
        new(0.0, isMiss, IsCrit: false, Falloff: 0.0, GuardReduce: 0.0,
            DefenceReduce: 0.0, AttackFactor: 0.0, ElementBonus: 0.0, ModeMultiplier: inputs.ModeMultiplier);

    private static double Clamp(double value, double min, double max) =>
        value < min ? min : value > max ? max : value;
}
