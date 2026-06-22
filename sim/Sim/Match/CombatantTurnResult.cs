namespace Sim.Match;

/// <summary>Per-combatant outcome recorded in the match log for one turn.</summary>
/// <remarks>
/// <see cref="IsCrit"/> and <see cref="IsMiss"/> are recorded for replay/UI; they are not
/// otherwise recoverable from the numeric damage. Both default to <see langword="false"/>,
/// so existing three-argument constructions remain valid.
/// </remarks>
/// <param name="DamageReceived">
/// Final damage applied to this combatant this turn (after reductions, never negative).
/// Zero if the blast did not reach this combatant or the hit was dodged.
/// </param>
/// <param name="HpBefore">HP at the start of the turn, before damage was applied.</param>
/// <param name="HpAfter">HP after damage was applied. May be negative (combatant is defeated when ≤ 0).</param>
public readonly record struct CombatantTurnResult(
    double DamageReceived,
    double HpBefore,
    double HpAfter)
{
    /// <summary>The hit on this combatant was a critical.</summary>
    public bool IsCrit { get; init; } = false;

    /// <summary>The hit on this combatant was dodged (zero damage from the dodge).</summary>
    public bool IsMiss { get; init; } = false;
}
