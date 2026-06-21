using System.Collections.Generic;
using Sim.Ai;
using Sim.Core;
using Sim.Match;
using Sim.Projectile;
using Sim.Terrain;
using Xunit;

namespace Sim.Tests.Match;

public class MatchControllerTests
{
    // ── Infrastructure ────────────────────────────────────────────────────────

    private static readonly IProjectileSimulator ProjectileSim = new ProjectileSimulator();
    private static readonly IMatchSimulator      MatchSim      = new MatchSimulator(ProjectileSim);
    private static readonly ITerrainQuery        FlatGround    = FlatTerrain.Ground;
    private static readonly WorldEnvironment     NoWind        = WorldEnvironment.Default;

    private const uint   TestSeed  = 0u;
    private const double StartHp   = 100.0;
    private const double LowHp     = 1.0;
    private const double ShotSpeed = 50.0;

    // ── Weapons ───────────────────────────────────────────────────────────────

    private static readonly Weapon BigBlastWeapon   = new(ShotSpeed, 200.0, 500.0);
    private static readonly Weapon NoDamageWeapon   = new(ShotSpeed,   0.0,   0.0);
    private static readonly Weapon SmallBlastWeapon = new(ShotSpeed,  50.0,   2.0);

    // ── Standard actions ─────────────────────────────────────────────────────

    // 45°, 500 m radius — hits anything within 500 m of the ~255 m landing point
    private static readonly FireAction KillAction      = new(45.0, ShotSpeed, BigBlastWeapon);
    // Straight up at near-zero speed — impacts at the shooter's feet; 2 m radius
    private static readonly FireAction StraightUpSmall = new(90.0, 0.5, SmallBlastWeapon);
    // No damage, no meaningful trajectory
    private static readonly FireAction NoopAction      = new(90.0, 0.5, NoDamageWeapon);

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static Combatant C(double x, double hp = StartHp)
        => new(new Vec2D(x, 0.0), hp, CombatantStats.Default);

    private static CombatantEntry E(double x, int teamId, IAgent agent, double hp = StartHp)
        => new(C(x, hp), teamId, agent);

    private static MatchController MakeController(
        Combatant[]  combatants,
        int[]        teamIds,
        MatchOptions opts)
        => new(ProjectileSim, combatants, teamIds, opts, FlatGround, NoWind, TestSeed);

    private static MatchController MakeController(Combatant[] combatants, int[] teamIds)
        => MakeController(combatants, teamIds, MatchOptions.Default);

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public void StepByStep_EqualsRunToCompletion_OutcomeAndLog()
    {
        // Arrange: C0(T0) kills C1(T1,1HP) with KillAction on turn 0.
        // C1 never gets to act, so only one TurnEvent is logged.
        var combatants = new[] { C(0.0), C(50.0, LowHp) };
        var teamIds    = new[] { 0, 1 };
        var opts       = new MatchOptions(FriendlyFire: false, SelfDamage: false);

        // Step-by-step through MatchController
        var ctrl    = MakeController(combatants, teamIds, opts);
        var ctrlLog = new List<TurnEvent>();
        while (!ctrl.IsOver)
            ctrlLog.Add(ctrl.ResolveTurn(ctrl.CurrentActorIndex == 0 ? KillAction : NoopAction));

        // Run-to-completion via MatchSimulator using equivalent scripted agents
        var entries = new[]
        {
            E(0.0,  0, new ScriptedAgent(KillAction)),
            E(50.0, 1, new ScriptedAgent(NoopAction), LowHp),
        };
        var runResult = MatchSim.Run(entries, opts, FlatGround, NoWind, TestSeed);

        Assert.Equal(runResult.Outcome,       ctrl.Result.Outcome);
        Assert.Equal(runResult.WinningTeamId, ctrl.Result.WinningTeamId);
        Assert.Equal(runResult.TurnCount,     ctrl.Result.TurnCount);
        Assert.Equal(runResult.Log.Count,     ctrlLog.Count);
        for (int i = 0; i < runResult.Log.Count; i++)
            Assert.Equal(runResult.Log[i], ctrlLog[i]);
    }

    [Fact]
    public void CurrentActorIndex_FollowsTurnOrder_SkipsDefeated()
    {
        // 3 combatants: C0(T0) at x=0, C1(T1,1HP) at x=0.5, C2(T1) at x=200.
        // Turn 0: C0 fires StraightUpSmall → impact at (0,0), C1 (distance 0.5 m < 2 m radius) dies.
        // After C1 dies the turn order must skip index 1 entirely.
        var combatants = new[] { C(0.0), C(0.5, LowHp), C(200.0) };
        var teamIds    = new[] { 0, 1, 1 };
        var opts       = new MatchOptions(FriendlyFire: true, SelfDamage: false);

        var ctrl = MakeController(combatants, teamIds, opts);

        // Turn 0: team 0 goes first
        Assert.Equal(0, ctrl.CurrentActorIndex);
        ctrl.ResolveTurn(StraightUpSmall);   // C1 dies

        // C2 (index 2) is now the only living T1 member
        Assert.Equal(2, ctrl.CurrentActorIndex);
        Assert.False(ctrl.IsOver);
        ctrl.ResolveTurn(NoopAction);        // C2 noops

        // Back to C0
        Assert.Equal(0, ctrl.CurrentActorIndex);
        ctrl.ResolveTurn(KillAction);        // C0 kills C2 (205 m < 500 m blast radius, 178 HP dealt)

        Assert.True(ctrl.IsOver);
        Assert.Equal(MatchOutcome.Team0Wins, ctrl.Result.Outcome);
        // Index 1 must never have appeared as the acting combatant
        Assert.DoesNotContain(1, new[] { ctrl.Result.Log[0].ActingCombatantIndex,
                                          ctrl.Result.Log[1].ActingCombatantIndex,
                                          ctrl.Result.Log[2].ActingCombatantIndex });
    }

    [Fact]
    public void IsOver_SetOnWin_ResultCorrect()
    {
        var combatants = new[] { C(0.0), C(50.0, LowHp) };
        var teamIds    = new[] { 0, 1 };
        var opts       = new MatchOptions(FriendlyFire: false, SelfDamage: false);

        var ctrl = MakeController(combatants, teamIds, opts);

        Assert.False(ctrl.IsOver);

        ctrl.ResolveTurn(KillAction);   // C0 kills C1 → Team0Wins

        Assert.True(ctrl.IsOver);
        Assert.Equal(MatchOutcome.Team0Wins, ctrl.Result.Outcome);
        Assert.Equal(0,                      ctrl.Result.WinningTeamId);
        Assert.Equal(1,                      ctrl.Result.TurnCount);
    }

    [Fact]
    public void IsOver_SetOnDraw_ResultCorrect()
    {
        // All four combatants at 1 HP within StraightUpSmall's 2 m blast.
        // C0 (actor, T0) fires: SelfDamage=on + FriendlyFire=on → everyone eliminated in one turn.
        var combatants = new[] { C(0.0, LowHp), C(0.5, LowHp), C(1.0, LowHp), C(1.5, LowHp) };
        var teamIds    = new[] { 0, 0, 1, 1 };

        var ctrl = MakeController(combatants, teamIds);  // MatchOptions.Default (FF=on, SD=on)

        Assert.False(ctrl.IsOver);

        ctrl.ResolveTurn(StraightUpSmall);

        Assert.True(ctrl.IsOver);
        Assert.Equal(MatchOutcome.Draw, ctrl.Result.Outcome);
        Assert.Null(ctrl.Result.WinningTeamId);
    }

    [Fact]
    public void Determinism_SameSeedAndActions_IdenticalLog()
    {
        var combatants = new[] { C(0.0), C(50.0, LowHp) };
        var teamIds    = new[] { 0, 1 };
        var opts       = new MatchOptions(FriendlyFire: false, SelfDamage: false);

        var ctrl1 = MakeController(combatants, teamIds, opts);
        var ctrl2 = MakeController(combatants, teamIds, opts);

        var log1 = new List<TurnEvent>();
        var log2 = new List<TurnEvent>();

        while (!ctrl1.IsOver)
            log1.Add(ctrl1.ResolveTurn(ctrl1.CurrentActorIndex == 0 ? KillAction : NoopAction));

        while (!ctrl2.IsOver)
            log2.Add(ctrl2.ResolveTurn(ctrl2.CurrentActorIndex == 0 ? KillAction : NoopAction));

        Assert.Equal(ctrl1.Result.Outcome,       ctrl2.Result.Outcome);
        Assert.Equal(ctrl1.Result.WinningTeamId, ctrl2.Result.WinningTeamId);
        Assert.Equal(ctrl1.Result.TurnCount,     ctrl2.Result.TurnCount);
        Assert.Equal(log1.Count,                 log2.Count);
        for (int i = 0; i < log1.Count; i++)
            Assert.Equal(log1[i], log2[i]);
    }

    [Fact]
    public void MixedFlow_CallerDrivesAllActions_RunsToCompletion()
    {
        // Simulates interactive play: the caller drives every combatant's action manually
        // (consulting CurrentState, then deciding). In real play a human would do this for
        // their own combatants and the server would validate; here a BotAgent stands in.
        var combatants = new[] { C(0.0), C(20.0), C(60.0), C(80.0) };
        var teamIds    = new[] { 0, 0, 1, 1 };
        var opts       = new MatchOptions(FriendlyFire: false, SelfDamage: false);
        var weapon     = new Weapon(ShotSpeed, 80.0, 8.0);

        var botT0 = new BotAgent(ProjectileSim, weapon, BotDifficulty.Hard,   TestSeed);
        var botT1 = new BotAgent(ProjectileSim, weapon, BotDifficulty.Medium, TestSeed + 1u);

        var ctrl = new MatchController(
            ProjectileSim, combatants, teamIds, opts, FlatGround, NoWind, TestSeed);

        while (!ctrl.IsOver)
        {
            var state     = ctrl.CurrentState;
            int actorTeam = teamIds[ctrl.CurrentActorIndex];
            var action    = actorTeam == 0
                ? botT0.ChooseAction(state)
                : botT1.ChooseAction(state);
            ctrl.ResolveTurn(action);
        }

        Assert.True(ctrl.IsOver);
        Assert.True(ctrl.Result.TurnCount > 0);
        Assert.True(
            ctrl.Result.Outcome == MatchOutcome.Team0Wins      ||
            ctrl.Result.Outcome == MatchOutcome.Team1Wins      ||
            ctrl.Result.Outcome == MatchOutcome.Draw           ||
            ctrl.Result.Outcome == MatchOutcome.MaxTurnsReached);
    }
}
