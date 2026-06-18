using Sim.Core;
using Sim.Match;
using Sim.Projectile;
using Sim.Terrain;
using Xunit;

namespace Sim.Tests.Match;

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

    // Huge blast: always hits both combatants wherever the shot lands.
    private static readonly Weapon BigBlastWeapon = new(ShotSpeed, 200.0, 500.0);

    // Zero damage: baseDamage=0 ensures no HP change regardless of distance/radius.
    private static readonly Weapon NoDamageWeapon = new(ShotSpeed, 0.0, 0.0);

    // Small weapon that damages only combatants within 2 m — used for targeted tests.
    private static readonly Weapon SmallBlastWeapon = new(ShotSpeed, 50.0, 2.0);

    // ── Standard actions ─────────────────────────────────────────────────────

    // Fires 45° right at full speed — lands ~255 m away.
    // With BigBlastWeapon the 500 m radius covers any combatant on the map.
    private static readonly FireAction KillAction = new(45.0, ShotSpeed, BigBlastWeapon);

    // NoDamageWeapon: shot deals zero damage regardless of placement.
    private static readonly FireAction NoopAction = new(90.0, 0.5, NoDamageWeapon);

    // 90°, low speed — projectile goes straight up and lands back at the shooter's feet.
    // Used for self-damage and area-blast tests together with SmallBlastWeapon.
    private static readonly FireAction StraightUpSmall = new(90.0, 0.5, SmallBlastWeapon);

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static Combatant MakeCombatant(double x, double hp = StartHp)
        => new(new Vec2D(x, 0.0), hp, CombatantStats.Default);

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public void FullMatch_TwoScriptedAgents_RunsToCompletion()
    {
        // c0 fires KillAction (huge blast, ~255 m landing, 500 m radius).
        // At 50 m apart, c1 is well within the 500 m radius → lethal.
        // c0 is ~255 m from its own impact → also takes damage but survives (< StartHp).
        var c0    = MakeCombatant(0.0);
        var c1    = MakeCombatant(50.0);
        var kill  = new ScriptedAgent(KillAction);
        var noop  = new ScriptedAgent(NoopAction);

        var result = Sim.Run(c0, c1, kill, noop, FlatGround, NoWind, TestSeed);

        Assert.Equal(MatchOutcome.Player0Wins, result.Outcome);
        Assert.Equal(0, result.WinnerIndex);
        Assert.True(result.TurnCount > 0);
        Assert.NotEmpty(result.Log);
    }

    [Fact]
    public void DefeatedCombatant_EndsMatch_BeforeMaxTurns()
    {
        var c0   = MakeCombatant(0.0);
        var c1   = MakeCombatant(50.0);
        var kill = new ScriptedAgent(KillAction);
        var noop = new ScriptedAgent(NoopAction);

        var result = Sim.Run(c0, c1, kill, noop, FlatGround, NoWind, TestSeed);

        Assert.NotEqual(MatchOutcome.MaxTurnsReached, result.Outcome);
        Assert.True(result.TurnCount < SimConstants.MaxTurnsPerMatch);
    }

    [Fact]
    public void CorrectWinnerIndex_Named()
    {
        var c0   = MakeCombatant(0.0);
        var c1   = MakeCombatant(50.0);
        var kill = new ScriptedAgent(KillAction);
        var noop = new ScriptedAgent(NoopAction);

        var result = Sim.Run(c0, c1, kill, noop, FlatGround, NoWind, TestSeed);

        Assert.Equal(MatchOutcome.Player0Wins, result.Outcome);
        Assert.Equal(0, result.WinnerIndex);
    }

    [Fact]
    public void TurnsAlternate_EvenTurnsCombatant0_OddTurnsCombatant1()
    {
        var c0   = MakeCombatant(0.0);
        var c1   = MakeCombatant(50.0);
        var noop = new ScriptedAgent(NoopAction);

        var result = Sim.Run(c0, c1, noop, noop, FlatGround, NoWind, TestSeed);

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

        var result = Sim.Run(c0, c1, noop, noop, FlatGround, NoWind, TestSeed);

        Assert.Equal(MatchOutcome.MaxTurnsReached, result.Outcome);
        Assert.Null(result.WinnerIndex);
        Assert.Equal(SimConstants.MaxTurnsPerMatch, result.TurnCount);
    }

    [Fact]
    public void TurnCount_EqualsLogCount()
    {
        var c0   = MakeCombatant(0.0);
        var c1   = MakeCombatant(50.0);
        var noop = new ScriptedAgent(NoopAction);

        // Verify for both early-exit and cap cases.
        var stalemate = Sim.Run(c0, c1, noop, noop, FlatGround, NoWind, TestSeed);
        Assert.Equal(stalemate.TurnCount, stalemate.Log.Count);

        var kill   = new ScriptedAgent(KillAction);
        var earlyExit = Sim.Run(c0, c1, kill, noop, FlatGround, NoWind, TestSeed);
        Assert.Equal(earlyExit.TurnCount, earlyExit.Log.Count);
    }

    [Fact]
    public void Determinism_SameInputs_IdenticalOutcome()
    {
        var c0   = MakeCombatant(0.0);
        var c1   = MakeCombatant(50.0);

        var r1 = Sim.Run(c0, c1, new ScriptedAgent(KillAction), new ScriptedAgent(NoopAction), FlatGround, NoWind, TestSeed);
        var r2 = Sim.Run(c0, c1, new ScriptedAgent(KillAction), new ScriptedAgent(NoopAction), FlatGround, NoWind, TestSeed);

        Assert.Equal(r1.Outcome,     r2.Outcome);
        Assert.Equal(r1.WinnerIndex, r2.WinnerIndex);
        Assert.Equal(r1.TurnCount,   r2.TurnCount);
        Assert.Equal(r1.Log.Count,   r2.Log.Count);
    }

    [Fact]
    public void Determinism_SameInputs_IdenticalTurnLog()
    {
        var c0 = MakeCombatant(0.0);
        var c1 = MakeCombatant(50.0);

        var r1 = Sim.Run(c0, c1, new ScriptedAgent(KillAction), new ScriptedAgent(NoopAction), FlatGround, NoWind, TestSeed);
        var r2 = Sim.Run(c0, c1, new ScriptedAgent(KillAction), new ScriptedAgent(NoopAction), FlatGround, NoWind, TestSeed);

        for (int i = 0; i < r1.Log.Count; i++)
            Assert.Equal(r1.Log[i], r2.Log[i]);
    }

    [Fact]
    public void SelfDamage_ShotWithinOwnBlastRadius_DamagesShooter()
    {
        // c0 fires straight up (90°, low speed) — lands back at c0's feet.
        // SmallBlastWeapon radius = 2 m; c0 at (0,0) takes near-full damage.
        // c1 at (200, 0) is 200 m from impact — far outside 2 m radius → no damage.
        var c0     = MakeCombatant(0.0);
        var c1     = MakeCombatant(200.0);
        var selfHit = new ScriptedAgent(StraightUpSmall);
        var noop    = new ScriptedAgent(NoopAction);

        var result = Sim.Run(c0, c1, selfHit, noop, FlatGround, NoWind, TestSeed);

        var firstTurn = result.Log[0];
        Assert.True(firstTurn.Combatant0Result.DamageReceived > 0.0,
            "Shooter (combatant 0) must take self-damage when within own blast radius.");
        Assert.Equal(0.0, firstTurn.Combatant1Result.DamageReceived);
    }

    [Fact]
    public void BothInBlastRadius_DamagesBothCombatants()
    {
        // c0 at (0,0) fires straight up — lands at ≈(0,0).
        // c1 at (1,0) is within SmallBlastWeapon's 2 m radius → both take damage.
        var c0     = MakeCombatant(0.0);
        var c1     = MakeCombatant(1.0);
        var splash  = new ScriptedAgent(StraightUpSmall);
        var noop    = new ScriptedAgent(NoopAction);

        var result = Sim.Run(c0, c1, splash, noop, FlatGround, NoWind, TestSeed);

        var firstTurn = result.Log[0];
        Assert.True(firstTurn.Combatant0Result.DamageReceived > 0.0, "Shooter takes self-damage.");
        Assert.True(firstTurn.Combatant1Result.DamageReceived > 0.0, "Nearby opponent takes splash damage.");
    }

    [Fact]
    public void DoubleKO_ProducesDraw()
    {
        // Both combatants start with 1 HP and sit within SmallBlastWeapon's 2 m radius.
        // c0 fires straight up → impact ≈(0,0).
        // c0 at distance ~0: takes 50 damage → HP = 1 − 50 = −49 → defeated.
        // c1 at distance 1: distanceFactor = 0.5, damage = 25 → HP = 1 − 25 = −24 → defeated.
        var c0      = MakeCombatant(0.0, hp: 1.0);
        var c1      = MakeCombatant(1.0, hp: 1.0);
        var suicide = new ScriptedAgent(StraightUpSmall);
        var noop    = new ScriptedAgent(NoopAction);

        var result = Sim.Run(c0, c1, suicide, noop, FlatGround, NoWind, TestSeed);

        Assert.Equal(MatchOutcome.Draw, result.Outcome);
        Assert.Null(result.WinnerIndex);
    }
}
