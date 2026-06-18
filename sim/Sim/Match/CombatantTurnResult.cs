namespace Sim.Match;

/// <summary>Per-combatant outcome recorded in the match log for one turn.</summary>
/// <param name="DamageReceived">
/// Final damage applied to this combatant this turn (after defense, never negative).
/// Zero if the blast did not reach this combatant.
/// </param>
/// <param name="HpBefore">HP at the start of the turn, before damage was applied.</param>
/// <param name="HpAfter">HP after damage was applied. May be negative (combatant is defeated when ≤ 0).</param>
public readonly record struct CombatantTurnResult(
    double DamageReceived,
    double HpBefore,
    double HpAfter);
