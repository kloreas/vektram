using System.Collections.Generic;
using Sim.Core;
using Sim.Match;
using Sim.Projectile;
using Sim.Terrain;
using Xunit;

namespace Sim.Tests.Match;

/// <summary>
/// 1v1 regression suite — one combatant per team, both default MatchOptions.
/// Verifies that the generalized team engine is a clean superset of the original duel behaviour.
/// </summary>
public class MatchSimulatorTests
{
    // ── Infrastructure ────────────────────────────────────────────────────────

    private static readonly IProjectileSimulator ProjectileSim = new ProjectileSimulator();
    private static readonly IMatchSimulator      Sim           = new MatchSimulator(ProjectileSim);
    private static readonly ITerrainQuery        FlatGround    = FlatTerrain.Ground;
    private static readonly WorldEnvironment     NoWind        = WorldEnvironment.Default;

    private const uint   TestSeed  = 0;
    private const double StartHp   = 100.0;
    private const double ShotSpeed = 50.0;

    // ── Weapons ───────────────────────────────────────────────────────────────

    private static readonly Weapon BigBlastWeapon  = new(ShotSpeed, 200.0, 500.0);
    private static readonly Weapon NoDamageWeapon  = new(ShotSpeed, 0.0, 0.0);
    private static readonly Weapon SmallBlastWeapon = new(ShotSpeed, 50.0, 2.0);

    // ── Standard actions ─────────────────────────────────────────────────────

    private static readonly FireAction KillAction      = new(45.0, ShotSpeed, BigBlastWeapon);
    private static readonly FireAction NoopAction      = new(90.0, 0.5, NoDamageWeapon);
    private static readonly FireAction StraightUpSmall = new(90.0, 0.5, SmallBlastWeapon);

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static Combatant MakeCombatant(double x, double hp = StartHp)
        => new(new Vec2D(x, 0.0), hp, CombatantStats.Default);

    private static IReadOnlyList<CombatantEntry> Duel(
        Combatant c0, IAgent a0, Combatant c1, IAgent a1)
        => new[] { new CombatantEntry(c0, 0, a0), new CombatantEntry(c1, 1, a1) };

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public void FullMatch_OnePerTeam_RunsToCompletion()
    {
        var c0 = MakeCombatant(0.0);
        var c1 = MakeCombatant(50.0);

        var result = Sim.Run(Duel(c0, new ScriptedAgent(KillAction), c1, new ScriptedAgent(NoopAction)),
            MatchOptions.Default, FlatGround, NoWind, TestSeed);

        Assert.Equal(MatchOutcome.Team0Wins, result.Outcome);
        Assert.Equal(0, result.WinningTeamId);
        Assert.True(result.TurnCount > 0);
        Assert.NotEmpty(result.Log);
    }

    [Fact]
    public void DefeatedTeam_EndsMatch_BeforeMaxTurns()
    {
        var c0 = MakeCombatant(0.0);
        var c1 = MakeCombatant(50.0);

        var result = Sim.Run(Duel(c0, new ScriptedAgent(KillAction), c1, new ScriptedAgent(NoopAction)),
            MatchOptions.Default, FlatGround, NoWind, TestSeed);

        Assert.NotEqual(MatchOutcome.MaxTurnsReached, result.Outcome);
        Assert.True(result.TurnCount < SimConstants.MaxTurnsPerMatch);
    }

    [Fact]
    public void CorrectWinningTeam_Named()
    {
        var c0 = MakeCombatant(0.0);
        var c1 = MakeCombatant(50.0);

        var result = Sim.Run(Duel(c0, new ScriptedAgent(KillAction), c1, new ScriptedAgent(NoopAction)),
            MatchOptions.Default, FlatGround, NoWind, TestSeed);

        Assert.Equal(MatchOutcome.Team0Wins, result.Outcome);
        Assert.Equal(0, result.WinningTeamId);
    }

    [Fact]
    public void TurnsAlternate_EvenTurnsTeam0_OddTurnsTeam1()
    {
        var c0   = MakeCombatant(0.0);
        var c1   = MakeCombatant(50.0);
        var noop = new ScriptedAgent(NoopAction);

        var result = Sim.Run(Duel(c0, noop, c1, noop),
            MatchOptions.Default, FlatGround, NoWind, TestSeed);

        Assert.Equal(SimConstants.MaxTurnsPerMatch, result.Log.Count);
        for (int i = 0; i < result.Log.Count; i++)
            Assert.Equal(i % 2, result.Log[i].ActingCombatantIndex);
    }

    [Fact]
    public void MaxTurnCap_EndsStalemate()
    {
        var c0   = MakeCombatant(0.0);
        var c1   = MakeCombatant(50.0);
        var noop = new ScriptedAgent(NoopAction);

        var result = Sim.Run(Duel(c0, noop, c1, noop),
            MatchOptions.Default, FlatGround, NoWind, TestSeed);

        Assert.Equal(MatchOutcome.MaxTurnsReached, result.Outcome);
        Assert.Null(result.WinningTeamId);
        Assert.Equal(SimConstants.MaxTurnsPerMatch, result.TurnCount);
    }

    [Fact]
    public void TurnCount_EqualsLogCount()
    {
        var c0   = MakeCombatant(0.0);
        var c1   = MakeCombatant(50.0);
        var noop = new ScriptedAgent(NoopAction);
        var kill = new ScriptedAgent(KillAction);

        var stalemate = Sim.Run(Duel(c0, noop, c1, noop),
            MatchOptions.Default, FlatGround, NoWind, TestSeed);
        Assert.Equal(stalemate.TurnCount, stalemate.Log.Count);

        var earlyExit = Sim.Run(Duel(c0, kill, c1, noop),
            MatchOptions.Default, FlatGround, NoWind, TestSeed);
        Assert.Equal(earlyExit.TurnCount, earlyExit.Log.Count);
    }

    [Fact]
    public void Determinism_SameInputs_IdenticalOutcome()
    {
        var c0 = MakeCombatant(0.0);
        var c1 = MakeCombatant(50.0);

        var r1 = Sim.Run(Duel(c0, new ScriptedAgent(KillAction), c1, new ScriptedAgent(NoopAction)),
            MatchOptions.Default, FlatGround, NoWind, TestSeed);
        var r2 = Sim.Run(Duel(c0, new ScriptedAgent(KillAction), c1, new ScriptedAgent(NoopAction)),
            MatchOptions.Default, FlatGround, NoWind, TestSeed);

        Assert.Equal(r1.Outcome,       r2.Outcome);
        Assert.Equal(r1.WinningTeamId, r2.WinningTeamId);
        Assert.Equal(r1.TurnCount,     r2.TurnCount);
        Assert.Equal(r1.Log.Count,     r2.Log.Count);
    }

    [Fact]
    public void Determinism_SameInputs_IdenticalTurnLog()
    {
        var c0 = MakeCombatant(0.0);
        var c1 = MakeCombatant(50.0);

        var r1 = Sim.Run(Duel(c0, new ScriptedAgent(KillAction), c1, new ScriptedAgent(NoopAction)),
            MatchOptions.Default, FlatGround, NoWind, TestSeed);
        var r2 = Sim.Run(Duel(c0, new ScriptedAgent(KillAction), c1, new ScriptedAgent(NoopAction)),
            MatchOptions.Default, FlatGround, NoWind, TestSeed);

        for (int i = 0; i < r1.Log.Count; i++)
            Assert.Equal(r1.Log[i], r2.Log[i]);
    }

    [Fact]
    public void SelfDamage_FriendlyFireOn_ShotWithinOwnRadius_DamagesShooter()
    {
        // c0 fires straight up, low speed → lands at c0's feet; 2 m radius covers c0 only.
        // c1 at 200 m is well outside radius → no damage.
        var c0      = MakeCombatant(0.0);
        var c1      = MakeCombatant(200.0);
        var selfHit = new ScriptedAgent(StraightUpSmall);
        var noop    = new ScriptedAgent(NoopAction);

        var result    = Sim.Run(Duel(c0, selfHit, c1, noop), MatchOptions.Default, FlatGround, NoWind, TestSeed);
        var firstTurn = result.Log[0];

        Assert.True(firstTurn.CombatantResults[0].DamageReceived > 0.0,
            "Shooter (combatant 0) must take self-damage when within own blast radius.");
        Assert.Equal(0.0, firstTurn.CombatantResults[1].DamageReceived);
    }

    [Fact]
    public void BothInBlastRadius_FriendlyFireOn_DamagesBothCombatants()
    {
        // c0 at (0,0) fires straight up → impact ≈ (0,0); c1 at (1,0) is within 2 m radius.
        var c0     = MakeCombatant(0.0);
        var c1     = MakeCombatant(1.0);
        var splash = new ScriptedAgent(StraightUpSmall);
        var noop   = new ScriptedAgent(NoopAction);

        var result    = Sim.Run(Duel(c0, splash, c1, noop), MatchOptions.Default, FlatGround, NoWind, TestSeed);
        var firstTurn = result.Log[0];

        Assert.True(firstTurn.CombatantResults[0].DamageReceived > 0.0, "Shooter takes self-damage.");
        Assert.True(firstTurn.CombatantResults[1].DamageReceived > 0.0, "Nearby opponent takes splash damage.");
    }

    [Fact]
    public void DoubleKO_ProducesDraw()
    {
        // Both combatants start with 1 HP and sit within SmallBlastWeapon's 2 m radius.
        // c0 fires straight up → both defeated in the same turn.
        var c0      = MakeCombatant(0.0, hp: 1.0);
        var c1      = MakeCombatant(1.0, hp: 1.0);
        var suicide = new ScriptedAgent(StraightUpSmall);
        var noop    = new ScriptedAgent(NoopAction);

        var result = Sim.Run(Duel(c0, suicide, c1, noop), MatchOptions.Default, FlatGround, NoWind, TestSeed);

        Assert.Equal(MatchOutcome.Draw, result.Outcome);
        Assert.Null(result.WinningTeamId);
    }
}
