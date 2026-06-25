namespace Sim.Content;

/// <summary>
/// The metric that decides a <see cref="WinConditionKind.TurnLimitTiebreak"/> when the turn cap
/// is reached with two or more teams still alive. Authored as data in the mode's win condition.
/// </summary>
public enum TiebreakMetric
{
    /// <summary>The team with the greatest summed living-combatant HP wins.</summary>
    TotalHpRemaining,

    /// <summary>The team that has dealt the most cumulative damage to enemies wins.</summary>
    TotalDamageDealt
}
