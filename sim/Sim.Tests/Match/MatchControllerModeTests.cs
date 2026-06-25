using System.Collections.Generic;
using Sim.Content;
using Sim.Core;
using Sim.Match;
using Sim.Projectile;
using Sim.Terrain;
using Xunit;

namespace Sim.Tests.Match;

/// <summary>
/// Verifies the data-driven mode wiring at the controller level: the default mode reproduces
/// pre-#5 behavior bit-for-bit, the turn-limit tiebreak resolves a reached cap, a mode's MaxTurns
/// drives the cap, and a boss-as-its-own-team match ends through the unchanged last-team-standing
/// evaluator.
/// </summary>
public class MatchControllerModeTests
{
    private static readonly IProjectileSimulator ProjectileSim = new ProjectileSimulator();
    private static readonly IMatchSimulator      MatchSim      = new MatchSimulator(ProjectileSim);
    private static readonly ITerrainQuery        FlatGround    = FlatTerrain.Ground;
    private static readonly WorldEnvironment     NoWind        = WorldEnvironment.Default;

    private const uint   TestSeed  = 0u;
    private const double StartHp   = 100.0;
    private const double ShotSpeed = 50.0;
    private const double CleanWinTargetX = 255.0;   // ≈ 45° / 50 m/s landing point

    // CleanWinWeapon (radius 50) reaches the target at the impact point without catching a
    // shooter standing back at x=0. NoDamageWeapon is an inert "pass" shot.
    private static readonly Weapon CleanWinWeapon = new(ShotSpeed, 200.0, 50.0);
    private static readonly Weapon NoDamageWeapon = new(ShotSpeed,   0.0,  0.0);

    private static readonly FireAction CleanWinShot = new(45.0, ShotSpeed, CleanWinWeapon);
    private static readonly FireAction NoopShot      = new(90.0, 0.5, NoDamageWeapon);

    private static Combatant C(double x, double hp = StartHp)
        => new(new Vec2D(x, 0.0), hp, CombatantStats.Default);

    private static MatchController Controller(
        Combatant[] combatants, int[] teamIds, MatchModeRules? modeRules)
        => new(ProjectileSim, combatants, teamIds, MatchOptions.Default,
               FlatGround, NoWind, TestSeed, modeRules: modeRules);

    private static (MatchResult Result, List<TurnEvent> Log) RunCleanWin(MatchModeRules? modeRules)
    {
        var ctrl = Controller(new[] { C(0.0), C(CleanWinTargetX) }, new[] { 0, 1 }, modeRules);
        var log  = new List<TurnEvent>();
        while (!ctrl.IsOver)
            log.Add(ctrl.ResolveTurn(ctrl.CurrentActorIndex == 0 ? CleanWinShot : NoopShot));
        return (ctrl.Result, log);
    }

    // ── Default mode reproduces pre-#5 behavior bit-for-bit ───────────────────

    [Fact]
    public void NullModeRules_EqualsExplicitEliminationMode_BitForBit_AndIsTeam0Win()
    {
        var (nullResult, nullLog) = RunCleanWin(null);
        var (modeResult, modeLog) = RunCleanWin(ModeSetup.ToModeRules(ModeDefinition.Default));

        // Today's behavior: the clean-win shot ends the match Team0Wins on turn 1.
        Assert.Equal(MatchOutcome.Team0Wins, nullResult.Outcome);
        Assert.Equal(0, nullResult.WinningTeamId);
        Assert.Equal(1, nullResult.TurnCount);

        // The explicit default mode reproduces it identically (outcome, winner, count, full log).
        Assert.Equal(nullResult.Outcome,       modeResult.Outcome);
        Assert.Equal(nullResult.WinningTeamId, modeResult.WinningTeamId);
        Assert.Equal(nullResult.TurnCount,     modeResult.TurnCount);
        Assert.Equal(nullLog.Count,            modeLog.Count);
        for (int i = 0; i < nullLog.Count; i++)
            Assert.Equal(nullLog[i], modeLog[i]);
    }

    [Fact]
    public void MatchSimulator_ForwardsModeRules_ToController()
    {
        var entries = new[]
        {
            new CombatantEntry(C(0.0),             0, new ScriptedAgent(CleanWinShot)),
            new CombatantEntry(C(CleanWinTargetX), 1, new ScriptedAgent(NoopShot)),
        };

        MatchResult result = MatchSim.Run(
            entries, MatchOptions.Default, FlatGround, NoWind, TestSeed,
            rules: null, modeRules: ModeSetup.ToModeRules(ModeDefinition.Default));

        Assert.Equal(MatchOutcome.Team0Wins, result.Outcome);
        Assert.Equal(0, result.WinningTeamId);
    }

    // ── TurnLimitTiebreak resolves a reached cap ──────────────────────────────

    private static MatchModeRules TiebreakRules(TiebreakMetric metric, int maxTurns) =>
        new(new WinConditionDefinition(WinConditionKind.TurnLimitTiebreak, metric),
            maxTurns, TurnOrderPolicyKind.RoundRobin);

    [Fact]
    public void TurnLimitTiebreak_CapReached_HigherHpTeamWins()
    {
        // 2v2, everyone passes (no damage), so neither team is eliminated. team0 keeps 200 HP;
        // team1 keeps 101. At the 4-turn cap the HP tiebreak hands team 0 the win.
        var combatants = new[] { C(0.0, 100.0), C(500.0, 100.0), C(20.0, 100.0), C(520.0, 1.0) };
        var teamIds    = new[] { 0, 1, 0, 1 };

        var ctrl = Controller(combatants, teamIds, TiebreakRules(TiebreakMetric.TotalHpRemaining, 4));
        while (!ctrl.IsOver)
            ctrl.ResolveTurn(NoopShot);

        Assert.Equal(MatchOutcome.Team0Wins, ctrl.Result.Outcome);
        Assert.Equal(0, ctrl.Result.WinningTeamId);
        Assert.Equal(4, ctrl.Result.TurnCount);
    }

    [Fact]
    public void TurnLimitTiebreak_CapReached_EqualHp_IsDraw()
    {
        var combatants = new[] { C(0.0, 100.0), C(500.0, 100.0), C(20.0, 100.0), C(520.0, 100.0) };
        var teamIds    = new[] { 0, 1, 0, 1 };

        var ctrl = Controller(combatants, teamIds, TiebreakRules(TiebreakMetric.TotalHpRemaining, 4));
        while (!ctrl.IsOver)
            ctrl.ResolveTurn(NoopShot);

        Assert.Equal(MatchOutcome.Draw, ctrl.Result.Outcome);
        Assert.Null(ctrl.Result.WinningTeamId);
    }

    [Fact]
    public void TurnLimitTiebreak_Deterministic_SameSeedAndMode_IdenticalResult()
    {
        var combatants = new[] { C(0.0, 100.0), C(500.0, 100.0), C(20.0, 100.0), C(520.0, 1.0) };
        var teamIds    = new[] { 0, 1, 0, 1 };

        var ctrl1 = Controller(combatants, teamIds, TiebreakRules(TiebreakMetric.TotalHpRemaining, 4));
        var ctrl2 = Controller(combatants, teamIds, TiebreakRules(TiebreakMetric.TotalHpRemaining, 4));
        while (!ctrl1.IsOver) ctrl1.ResolveTurn(NoopShot);
        while (!ctrl2.IsOver) ctrl2.ResolveTurn(NoopShot);

        Assert.Equal(ctrl1.Result.Outcome,       ctrl2.Result.Outcome);
        Assert.Equal(ctrl1.Result.WinningTeamId, ctrl2.Result.WinningTeamId);
        Assert.Equal(ctrl1.Result.TurnCount,     ctrl2.Result.TurnCount);
    }

    // ── A mode's MaxTurns drives the cap ──────────────────────────────────────

    [Fact]
    public void ModeMaxTurns_DrivesTheCap_NotTheSafetyCeiling()
    {
        // Both pass forever; a low per-mode cap of 2 ends the match well before the 200 ceiling.
        var ctrl = Controller(
            new[] { C(0.0), C(500.0) }, new[] { 0, 1 },
            new MatchModeRules(WinConditionDefinition.LastTeamStanding, 2, TurnOrderPolicyKind.RoundRobin));

        while (!ctrl.IsOver)
            ctrl.ResolveTurn(NoopShot);

        Assert.Equal(MatchOutcome.MaxTurnsReached, ctrl.Result.Outcome);
        Assert.Equal(2, ctrl.Result.TurnCount);
    }

    // ── Boss-as-its-own-team needs no new code ────────────────────────────────

    [Fact]
    public void BossAsOwnTeam_DefeatingBoss_EndsViaLastTeamStanding_NoNewCode()
    {
        // Player is team 0; the boss is the sole member of team 1 with enough HP to survive one
        // clean-win shot (~122 < 150). The default (last-team-standing) mode ends the match the
        // moment the boss team is wiped — defeat-a-boss is expressible with zero new code.
        var ctrl = Controller(new[] { C(0.0, 100.0), C(CleanWinTargetX, 150.0) }, new[] { 0, 1 }, modeRules: null);

        ctrl.ResolveTurn(CleanWinShot);      // boss 150 → ~28, survives
        Assert.False(ctrl.IsOver);

        while (!ctrl.IsOver)
            ctrl.ResolveTurn(ctrl.CurrentActorIndex == 0 ? CleanWinShot : NoopShot);

        Assert.Equal(MatchOutcome.Team0Wins, ctrl.Result.Outcome);
        Assert.Equal(0, ctrl.Result.WinningTeamId);
        Assert.True(ctrl.Result.TurnCount >= 3);   // boss was a real multi-turn target, not one-shot
    }
}
