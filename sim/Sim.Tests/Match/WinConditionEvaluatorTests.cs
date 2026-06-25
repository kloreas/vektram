using Sim.Content;
using Sim.Match;
using Xunit;

namespace Sim.Tests.Match;

/// <summary>
/// Pure-evaluator tests for the data-driven win conditions. No projectile or RNG machinery — the
/// evaluator is a pure function of (condition, context), so each case constructs a context directly.
/// </summary>
public class WinConditionEvaluatorTests
{
    private static WinEvaluationContext Ctx(int turn, int maxTurns, params TeamStanding[] standings)
        => new(turn, maxTurns, standings);

    private static TeamStanding Team(int id, int alive, double hp = 0.0, double dmg = 0.0)
        => new(id, alive, hp, dmg);

    private static readonly WinConditionDefinition LastTeamStanding =
        WinConditionDefinition.LastTeamStanding;

    private static WinConditionDefinition Tiebreak(TiebreakMetric metric)
        => new(WinConditionKind.TurnLimitTiebreak, metric);

    // ── LastTeamStanding ──────────────────────────────────────────────────────

    [Fact]
    public void LastTeamStanding_AllTeamsEliminated_IsDraw()
    {
        WinEvaluationContext ctx = Ctx(5, 200, Team(0, alive: 0), Team(1, alive: 0));

        WinEvaluation eval = WinConditionEvaluator.Evaluate(LastTeamStanding, ctx);

        Assert.True(eval.IsFinished);
        Assert.Equal(MatchOutcome.Draw, eval.Outcome);
        Assert.Null(eval.WinningTeamId);
    }

    [Fact]
    public void LastTeamStanding_OneTeamAlive_ThatTeamWins_WithCorrectOutcomeMapping()
    {
        WinEvaluation team0 = WinConditionEvaluator.Evaluate(
            LastTeamStanding, Ctx(5, 200, Team(0, alive: 1), Team(1, alive: 0)));
        WinEvaluation team1 = WinConditionEvaluator.Evaluate(
            LastTeamStanding, Ctx(5, 200, Team(0, alive: 0), Team(1, alive: 2)));

        Assert.Equal(MatchOutcome.Team0Wins, team0.Outcome);
        Assert.Equal(0, team0.WinningTeamId);
        Assert.Equal(MatchOutcome.Team1Wins, team1.Outcome);
        Assert.Equal(1, team1.WinningTeamId);
    }

    [Fact]
    public void LastTeamStanding_TwoTeamsAliveUnderCap_Continues()
    {
        WinEvaluation eval = WinConditionEvaluator.Evaluate(
            LastTeamStanding, Ctx(turn: 10, maxTurns: 200, Team(0, alive: 1), Team(1, alive: 1)));

        Assert.False(eval.IsFinished);
    }

    [Fact]
    public void LastTeamStanding_TwoTeamsAliveAtCap_IsMaxTurnsReached()
    {
        WinEvaluation eval = WinConditionEvaluator.Evaluate(
            LastTeamStanding, Ctx(turn: 200, maxTurns: 200, Team(0, alive: 1), Team(1, alive: 1)));

        Assert.True(eval.IsFinished);
        Assert.Equal(MatchOutcome.MaxTurnsReached, eval.Outcome);
        Assert.Null(eval.WinningTeamId);
    }

    [Fact]
    public void LastTeamStanding_BossAsOwnTeam_DefeatingBossWins_NoNewCode()
    {
        // "Defeat-a-boss-target" is already expressible today: the boss is simply its own team id.
        // Players are team 0 (alive); the boss is the sole member of team 1. Once the boss is
        // defeated (team 1 alive count 0) the UNCHANGED LastTeamStanding evaluator declares the
        // players the winner — proving the claim without a new win-condition kind.
        WinEvaluationContext bossAlive    = Ctx(8, 200, Team(0, alive: 2), Team(1, alive: 1));
        WinEvaluationContext bossDefeated = Ctx(9, 200, Team(0, alive: 2), Team(1, alive: 0));

        WinEvaluation duringFight = WinConditionEvaluator.Evaluate(LastTeamStanding, bossAlive);
        WinEvaluation afterKill   = WinConditionEvaluator.Evaluate(LastTeamStanding, bossDefeated);

        Assert.False(duringFight.IsFinished);
        Assert.True(afterKill.IsFinished);
        Assert.Equal(MatchOutcome.Team0Wins, afterKill.Outcome);
        Assert.Equal(0, afterKill.WinningTeamId);
    }

    // ── TurnLimitTiebreak ─────────────────────────────────────────────────────

    [Fact]
    public void TurnLimitTiebreak_OneTeamAliveBeforeCap_EarlyEliminationStillWins()
    {
        WinEvaluation eval = WinConditionEvaluator.Evaluate(
            Tiebreak(TiebreakMetric.TotalHpRemaining),
            Ctx(turn: 3, maxTurns: 60, Team(0, alive: 1, hp: 5.0), Team(1, alive: 0)));

        Assert.True(eval.IsFinished);
        Assert.Equal(MatchOutcome.Team0Wins, eval.Outcome);
        Assert.Equal(0, eval.WinningTeamId);
    }

    [Fact]
    public void TurnLimitTiebreak_AllEliminated_IsDraw()
    {
        WinEvaluation eval = WinConditionEvaluator.Evaluate(
            Tiebreak(TiebreakMetric.TotalHpRemaining),
            Ctx(turn: 60, maxTurns: 60, Team(0, alive: 0), Team(1, alive: 0)));

        Assert.Equal(MatchOutcome.Draw, eval.Outcome);
        Assert.Null(eval.WinningTeamId);
    }

    [Fact]
    public void TurnLimitTiebreak_AtCap_HpMetric_HigherHpWins()
    {
        WinEvaluation eval = WinConditionEvaluator.Evaluate(
            Tiebreak(TiebreakMetric.TotalHpRemaining),
            Ctx(turn: 60, maxTurns: 60,
                Team(0, alive: 2, hp: 140.0, dmg: 10.0),
                Team(1, alive: 1, hp: 90.0,  dmg: 80.0)));

        Assert.Equal(MatchOutcome.Team0Wins, eval.Outcome);
        Assert.Equal(0, eval.WinningTeamId);
    }

    [Fact]
    public void TurnLimitTiebreak_AtCap_DamageMetric_HigherDamageWins()
    {
        // Same standings as the HP case but the metric is damage dealt — the lower-HP team that
        // dealt more damage now wins, proving the metric (not just the standings) drives the result.
        WinEvaluation eval = WinConditionEvaluator.Evaluate(
            Tiebreak(TiebreakMetric.TotalDamageDealt),
            Ctx(turn: 60, maxTurns: 60,
                Team(0, alive: 2, hp: 140.0, dmg: 10.0),
                Team(1, alive: 1, hp: 90.0,  dmg: 80.0)));

        Assert.Equal(MatchOutcome.Team1Wins, eval.Outcome);
        Assert.Equal(1, eval.WinningTeamId);
    }

    [Fact]
    public void TurnLimitTiebreak_AtCap_MetricTie_IsDraw()
    {
        WinEvaluation eval = WinConditionEvaluator.Evaluate(
            Tiebreak(TiebreakMetric.TotalHpRemaining),
            Ctx(turn: 60, maxTurns: 60,
                Team(0, alive: 1, hp: 100.0),
                Team(1, alive: 1, hp: 100.0)));

        Assert.Equal(MatchOutcome.Draw, eval.Outcome);
        Assert.Null(eval.WinningTeamId);
    }

    [Fact]
    public void TurnLimitTiebreak_TwoTeamsAliveUnderCap_Continues()
    {
        WinEvaluation eval = WinConditionEvaluator.Evaluate(
            Tiebreak(TiebreakMetric.TotalHpRemaining),
            Ctx(turn: 30, maxTurns: 60,
                Team(0, alive: 1, hp: 50.0),
                Team(1, alive: 1, hp: 100.0)));

        Assert.False(eval.IsFinished);
    }
}
