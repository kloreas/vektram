using Sim.Core;

namespace Sim.Match;

/// <summary>
/// Complete record of one turn, sufficient to replay it from stored state.
/// Combatant indices (0 and 1) are stable across the whole match.
/// </summary>
/// <param name="TurnNumber">Zero-based turn index.</param>
/// <param name="ActingCombatantIndex">0 or 1 — which combatant fired this turn.</param>
/// <param name="Action">The action the acting combatant submitted.</param>
/// <param name="ImpactPoint">World position where the projectile hit the terrain surface.</param>
/// <param name="Combatant0Result">HP change for combatant 0 this turn (may be self-damage if 0 was the actor).</param>
/// <param name="Combatant1Result">HP change for combatant 1 this turn (may be self-damage if 1 was the actor).</param>
public readonly record struct TurnEvent(
    int TurnNumber,
    int ActingCombatantIndex,
    FireAction Action,
    Vec2D ImpactPoint,
    CombatantTurnResult Combatant0Result,
    CombatantTurnResult Combatant1Result);
