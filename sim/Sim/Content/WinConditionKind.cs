namespace Sim.Content;

/// <summary>
/// The family of win-condition evaluators a mode can select. Authored as data in
/// <c>/content/data/modes.json</c>; the match controller dispatches on this kind through the
/// pure <c>Sim.Match.WinConditionEvaluator</c>.
/// </summary>
/// <remarks>
/// This is the ONE switch in the whole engine keyed by condition KIND rather than by mode id,
/// so many modes reuse this handful of evaluators with different parameters (ADR-0006,
/// Decision 1). New conditions extend this enum + one evaluator case — a small, named
/// extension, never an engine rewrite.
/// </remarks>
public enum WinConditionKind
{
    /// <summary>
    /// A team wins when it is the last with a living combatant; all teams eliminated in the
    /// same turn is a Draw; the turn cap with two or more teams alive is MaxTurnsReached.
    /// This is the pre-#5 behavior, now expressed as one data case.
    /// </summary>
    LastTeamStanding,

    /// <summary>
    /// Last-team-standing, but when the turn cap is reached with two or more teams alive a
    /// <see cref="TiebreakMetric"/> decides the winner instead of a no-result MaxTurnsReached.
    /// </summary>
    TurnLimitTiebreak
}
