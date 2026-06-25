namespace Sim.Match;

/// <summary>
/// Result of one win-condition evaluation: either keep playing, or finish with this outcome.
/// </summary>
/// <param name="IsFinished">When <see langword="false"/>, the match continues and the other fields are unused.</param>
/// <param name="Outcome">How the match ends (meaningful only when <see cref="IsFinished"/>).</param>
/// <param name="WinningTeamId">
/// Winning team id, or <see langword="null"/> for <see cref="MatchOutcome.Draw"/> and
/// <see cref="MatchOutcome.MaxTurnsReached"/>.
/// </param>
public readonly record struct WinEvaluation(
    bool         IsFinished,
    MatchOutcome Outcome,
    int?         WinningTeamId)
{
    /// <summary>The "keep playing" result.</summary>
    public static WinEvaluation Continue { get; } = new(false, default, null);

    /// <summary>A finished result with the given outcome and winner.</summary>
    public static WinEvaluation Finished(MatchOutcome outcome, int? winningTeamId) =>
        new(true, outcome, winningTeamId);
}
