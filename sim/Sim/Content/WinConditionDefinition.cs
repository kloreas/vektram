namespace Sim.Content;

/// <summary>
/// Data-driven win condition for a mode: a discriminated union expressed as data — a
/// <see cref="Kind"/> plus the parameters that kind needs. Authored inside a
/// <see cref="ModeDefinition"/> in <c>/content/data/modes.json</c> and evaluated each turn by
/// the pure <c>Sim.Match.WinConditionEvaluator</c>.
/// </summary>
/// <remarks>
/// The nested-object JSON shape (<c>{ "kind": ..., "tiebreak": ... }</c>) lets a future kind add
/// its own parameters without disturbing existing rows. EXTENSION BOUNDARY: a condition that only
/// reads existing match progress (survive-N-turns; defeat-a-boss-target, which is just
/// <see cref="WinConditionKind.LastTeamStanding"/> with the boss as its own team id) is a new
/// <see cref="WinConditionKind"/> value plus one evaluator case. A condition that needs new match
/// state (hold-a-zone) additionally needs a field on <c>Sim.Match.WinEvaluationContext</c>.
/// </remarks>
/// <param name="Kind">Which win-condition evaluator drives this mode.</param>
/// <param name="Tiebreak">
/// The metric that breaks a tie for <see cref="WinConditionKind.TurnLimitTiebreak"/>;
/// <see langword="null"/> for kinds that need no tiebreak.
/// </param>
public readonly record struct WinConditionDefinition(
    WinConditionKind Kind,
    TiebreakMetric?  Tiebreak)
{
    /// <summary>
    /// The pre-#5 win condition: the last team with a living combatant wins. Carries no
    /// tiebreak. This is the value <c>ModeDefinition.Default</c> uses.
    /// </summary>
    public static WinConditionDefinition LastTeamStanding { get; } =
        new(WinConditionKind.LastTeamStanding, null);
}
