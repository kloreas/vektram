using System;
using System.Collections.Generic;
using Sim.Core;

namespace Sim.Match;

/// <summary>
/// Complete record of one turn, sufficient to replay it from stored state.
/// Combatant indices are stable across the whole match and parallel the
/// <see cref="CombatantEntry"/> roster passed to <see cref="IMatchSimulator.Run"/>.
/// </summary>
/// <param name="TurnNumber">Zero-based turn index.</param>
/// <param name="ActingCombatantIndex">Roster index of the combatant that fired this turn.</param>
/// <param name="Action">The action the acting combatant submitted.</param>
/// <param name="ImpactPoint">World position where the projectile hit the terrain surface.</param>
/// <param name="CombatantResults">
/// Per-combatant HP change for this turn, indexed by roster position.
/// A combatant shielded by friendly-fire rules, or outside blast radius, records
/// <see cref="CombatantTurnResult.DamageReceived"/> of 0.
/// </param>
public readonly record struct TurnEvent(
    int TurnNumber,
    int ActingCombatantIndex,
    FireAction Action,
    Vec2D ImpactPoint,
    IReadOnlyList<CombatantTurnResult> CombatantResults)
{
    /// <summary>
    /// The item use resolved this turn (before the shot), or <see langword="null"/> for a
    /// fire-only turn. A rejected use is still logged here with
    /// <see cref="TurnItemUse.Applied"/> = <see langword="false"/>.
    /// </summary>
    public TurnItemUse? ItemUse { get; init; } = null;

    // CombatantResults is IReadOnlyList<T> (a reference type), so the compiler-generated
    // Equals would use reference equality — wrong for determinism tests. Override with
    // element-wise comparison instead.

    /// <inheritdoc/>
    public bool Equals(TurnEvent other)
    {
        if (TurnNumber           != other.TurnNumber           ||
            ActingCombatantIndex != other.ActingCombatantIndex ||
            !Action.Equals(other.Action)                       ||
            !ImpactPoint.Equals(other.ImpactPoint))
            return false;

        if (!Nullable.Equals(ItemUse, other.ItemUse))
            return false;

        if (CombatantResults.Count != other.CombatantResults.Count)
            return false;

        for (int i = 0; i < CombatantResults.Count; i++)
            if (!CombatantResults[i].Equals(other.CombatantResults[i]))
                return false;

        return true;
    }

    /// <inheritdoc/>
    public override int GetHashCode()
        => HashCode.Combine(TurnNumber, ActingCombatantIndex, Action, ImpactPoint, ItemUse);
}
