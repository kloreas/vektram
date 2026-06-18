using System;
using Sim.Core;

namespace Sim.Match;

/// <summary>
/// Pure, stateless damage computation for a single blast.
/// </summary>
public static class DamageCalculator
{
    /// <summary>
    /// Computes the final damage dealt to one combatant by a projectile blast.
    /// </summary>
    /// <remarks>
    /// Formula:
    /// <code>
    ///   distanceFactor = 1 − (distance / blastRadius)   [linear falloff; 1.0 at centre, 0.0 at edge]
    ///   raw            = weapon.BaseDamage × distanceFactor × attacker.DamageModifier
    ///   finalDamage    = max(0, raw − defender.Defense)
    /// </code>
    /// Returns 0 when the combatant is at or beyond the blast radius.
    /// Defense can reduce damage to zero but never below (no healing).
    /// </remarks>
    /// <param name="impactPoint">World position where the projectile hit the terrain.</param>
    /// <param name="combatantPosition">World position of the combatant receiving the blast.</param>
    /// <param name="weapon">Fired weapon — provides base damage and blast radius.</param>
    /// <param name="attackerStats">Stats of the combatant who fired — provides <see cref="CombatantStats.DamageModifier"/>.</param>
    /// <param name="defenderStats">Stats of the combatant being hit — provides <see cref="CombatantStats.Defense"/>.</param>
    /// <returns>Final damage in HP. Always ≥ 0.</returns>
    public static double Compute(
        Vec2D impactPoint,
        Vec2D combatantPosition,
        Weapon weapon,
        CombatantStats attackerStats,
        CombatantStats defenderStats)
    {
        double distance = (combatantPosition - impactPoint).Length;

        if (distance >= weapon.BlastRadius)
            return 0.0;

        double distanceFactor = 1.0 - distance / weapon.BlastRadius;
        double raw = weapon.BaseDamage * distanceFactor * attackerStats.DamageModifier;
        return Math.Max(0.0, raw - defenderStats.Defense);
    }
}
