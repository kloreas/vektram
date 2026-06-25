using System;
using System.Collections.Generic;
using Sim.Content;

namespace Sim.Match;

/// <summary>
/// Pure evaluator for data-driven win conditions. The ONLY switch in the engine keyed by
/// win-condition KIND (never by mode id), so a thousand modes reuse this handful of cases with
/// different parameters (ADR-0006, Decision 1). No RNG, no I/O: the controller builds the
/// <see cref="WinEvaluationContext"/>; this decides whether — and how — the match ends.
/// </summary>
/// <remarks>
/// EXTENSION BOUNDARY: a condition that only READS existing context is a new
/// <see cref="WinConditionKind"/> value plus one case here — for example survive-N-turns, or
/// defeat-a-boss-target (already expressible today as <see cref="WinConditionKind.LastTeamStanding"/>
/// with the boss as its own team id; see <c>WinConditionEvaluatorTests</c> for the proof). A
/// condition that needs new match STATE (hold-a-zone) additionally adds a field to
/// <see cref="WinEvaluationContext"/>. Both are named, localized extensions — never an engine rewrite.
/// </remarks>
public static class WinConditionEvaluator
{
    /// <summary>
    /// Evaluates <paramref name="condition"/> against the post-turn <paramref name="context"/>.
    /// </summary>
    public static WinEvaluation Evaluate(in WinConditionDefinition condition, in WinEvaluationContext context)
    {
        return condition.Kind switch
        {
            WinConditionKind.LastTeamStanding  => EvaluateLastTeamStanding(context),
            WinConditionKind.TurnLimitTiebreak => EvaluateTurnLimitTiebreak(condition, context),
            _ => throw new ArgumentOutOfRangeException(
                     nameof(condition), condition.Kind, "Unsupported win-condition kind."),
        };
    }

    private static WinEvaluation EvaluateLastTeamStanding(in WinEvaluationContext ctx)
    {
        int aliveTeams = CountAliveTeams(ctx.Standings, out int lastAliveTeam);

        if (aliveTeams == 0)
            return WinEvaluation.Finished(MatchOutcome.Draw, null);
        if (aliveTeams == 1)
            return WinEvaluation.Finished(OutcomeForWinner(lastAliveTeam), lastAliveTeam);
        if (ctx.TurnNumber >= ctx.MaxTurns)
            return WinEvaluation.Finished(MatchOutcome.MaxTurnsReached, null);
        return WinEvaluation.Continue;
    }

    private static WinEvaluation EvaluateTurnLimitTiebreak(in WinConditionDefinition condition, in WinEvaluationContext ctx)
    {
        int aliveTeams = CountAliveTeams(ctx.Standings, out int lastAliveTeam);

        // Early elimination still wins outright — the tiebreak only resolves a reached turn cap.
        if (aliveTeams == 0)
            return WinEvaluation.Finished(MatchOutcome.Draw, null);
        if (aliveTeams == 1)
            return WinEvaluation.Finished(OutcomeForWinner(lastAliveTeam), lastAliveTeam);
        if (ctx.TurnNumber >= ctx.MaxTurns)
            return ResolveTiebreak(condition.Tiebreak ?? TiebreakMetric.TotalHpRemaining, ctx.Standings);
        return WinEvaluation.Continue;
    }

    private static WinEvaluation ResolveTiebreak(TiebreakMetric metric, IReadOnlyList<TeamStanding> standings)
    {
        // argmax of the metric over still-alive teams; first team (ascending id) wins on strict max.
        double best     = double.NegativeInfinity;
        int    bestTeam = -1;
        bool   tied     = false;

        foreach (TeamStanding s in standings)
        {
            if (s.AliveCount == 0)
                continue;

            double value = metric == TiebreakMetric.TotalHpRemaining
                ? s.TotalHpRemaining
                : s.TotalDamageDealt;

            if (value > best)
            {
                best     = value;
                bestTeam = s.TeamId;
                tied     = false;
            }
            else if (value == best)
            {
                tied = true;
            }
        }

        if (tied || bestTeam < 0)
            return WinEvaluation.Finished(MatchOutcome.Draw, null);
        return WinEvaluation.Finished(OutcomeForWinner(bestTeam), bestTeam);
    }

    private static int CountAliveTeams(IReadOnlyList<TeamStanding> standings, out int lastAliveTeam)
    {
        int count = 0;
        lastAliveTeam = -1;
        foreach (TeamStanding s in standings)
            if (s.AliveCount > 0)
            {
                count++;
                lastAliveTeam = s.TeamId;
            }
        return count;
    }

    // CARRY-FORWARD (deliberate, not a regression): the pre-#5 team-wipe check mapped winner 0 ->
    // Team0Wins and every other winner -> Team1Wins. Preserved verbatim so the existing suite stays
    // bit-for-bit; team id >= 2 maps as it does today (no test exercises it). The generic
    // MatchOutcome rename lands with the first FFA mode (ADR-0006) — WinningTeamId already carries
    // the real winning team id, so no information is lost in the meantime.
    private static MatchOutcome OutcomeForWinner(int teamId) =>
        teamId == 0 ? MatchOutcome.Team0Wins : MatchOutcome.Team1Wins;
}
