using System.Collections.Generic;
using Sim.Core;
using Sim.Match;
using Sim.Projectile;
using Sim.Terrain;
using Xunit;

namespace Sim.Tests.Match;

/// <summary>
/// Team-match test suite covering N-vs-N scenarios, turn-order policy, friendly-fire options,
/// team win/draw conditions, scale, and determinism.
/// </summary>
public class TeamMatchSimulatorTests
{
    // ── Infrastructure ────────────────────────────────────────────────────────

    private static readonly IProjectileSimulator ProjectileSim = new ProjectileSimulator();
    private static readonly IMatchSimulator      Sim           = new MatchSimulator(ProjectileSim);
    private static readonly ITerrainQuery        FlatGround    = FlatTerrain.Ground;
    private static readonly WorldEnvironment     NoWind        = WorldEnvironment.Default;

    private const uint   TestSeed  = 0;
    private const double StartHp   = 100.0;
    private const double LowHp     = 1.0;
    private const double ShotSpeed = 50.0;

    // ── Weapons ───────────────────────────────────────────────────────────────

    // 500 m radius — covers any combatant within range of C0's 45° shot landing ~255 m away.
    private static readonly Weapon BigBlastWeapon   = new(ShotSpeed, 200.0, 500.0);
    private static readonly Weapon NoDamageWeapon   = new(ShotSpeed, 0.0,   0.0);
    private static readonly Weapon SmallBlastWeapon = new(ShotSpeed, 50.0,  2.0);

    // ── Standard actions ─────────────────────────────────────────────────────

    // Lands ~255 m from origin; 500 m radius → anyone within ~755 m of origin is hit.
    private static readonly FireAction KillAction      = new(45.0, ShotSpeed, BigBlastWeapon);
    private static readonly FireAction NoopAction      = new(90.0, 0.5, NoDamageWeapon);
    // Fires straight up at 0.5 m/s → impact ≈ at shooter's feet; 2 m radius.
    private static readonly FireAction StraightUpSmall = new(90.0, 0.5, SmallBlastWeapon);

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static Combatant C(double x, double hp = StartHp)
        => new(new Vec2D(x, 0.0), hp, CombatantStats.Default);

    private static CombatantEntry E(double x, int teamId, IAgent agent, double hp = StartHp)
        => new(C(x, hp), teamId, agent);

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public void TwoVsTwo_ScriptedAgents_RunsToCompletion_Team0Wins()
    {
        // C0 (T0) fires KillAction every turn; impact at ~255 m, 500 m radius.
        // C4–C7 (T1) sit at 50–125 m: all within radius, all 1 HP → all die on turn 0.
        // C1 (T0) at 3000 m: outside 500 m radius → unharmed.
        // FriendlyFire=false so C1 is safe even if in range.
        var entries = new[]
        {
            E(    0.0, 0, new ScriptedAgent(KillAction)),   // C0: T0, fires kill
            E( 3000.0, 0, new ScriptedAgent(NoopAction)),   // C1: T0, safe at distance
            E(   50.0, 1, new ScriptedAgent(NoopAction), LowHp),  // C2: T1
            E(   75.0, 1, new ScriptedAgent(NoopAction), LowHp),  // C3: T1
        };
        var opts = new MatchOptions(FriendlyFire: false, SelfDamage: false);

        var result = Sim.Run(entries, opts, FlatGround, NoWind, TestSeed);

        Assert.Equal(MatchOutcome.Team0Wins, result.Outcome);
        Assert.Equal(0, result.WinningTeamId);
        Assert.True(result.TurnCount > 0);
        Assert.True(result.TurnCount < SimConstants.MaxTurnsPerMatch);
    }

    [Fact]
    public void TeamNotDefeated_WhileAnyMemberSurvives()
    {
        // C0 (T0) fires StraightUpSmall → impact ≈ (0,0), 2 m radius.
        // C1 (T1, 1 HP) at (0.5,0): in range → defeated on turn 0.
        // C2 (T1, 100 HP) at (200,0): outside 2 m radius → survives.
        // Team 1 still has C2 → match does NOT end on turn 0.
        var entries = new[]
        {
            E(  0.0, 0, new ScriptedAgent(StraightUpSmall)),
            E(  0.5, 1, new ScriptedAgent(NoopAction), LowHp),
            E(200.0, 1, new ScriptedAgent(NoopAction)),
        };

        var result = Sim.Run(entries, MatchOptions.Default, FlatGround, NoWind, TestSeed);

        Assert.True(result.Log[0].CombatantResults[1].HpAfter <= 0.0,
            "C1 (T1) must be defeated on turn 0.");
        Assert.True(result.TurnCount > 1,
            "Match must continue past turn 0 because C2 (same team as C1) is still alive.");
    }

    [Fact]
    public void TurnOrder_TeamAlternation_DefeatedCombatantsSkipped()
    {
        // Roster: C0(T0) C1(T1) C2(T0,1HP) C3(T1)
        // Expected pattern before C2 dies: 0, 1, 2, 3, ...
        // C2 fires StraightUpSmall on turn 2 → self-kills.
        // Expected pattern after: ... 3, 0, 1, 0, 3, 0, 1, ...  (C2 never appears again)
        var entries = new[]
        {
            E(  0.0, 0, new ScriptedAgent(NoopAction)),
            E( 50.0, 1, new ScriptedAgent(NoopAction)),
            E(200.0, 0, new ScriptedAgent(StraightUpSmall), LowHp), // C2: self-kills on its first action
            E(250.0, 1, new ScriptedAgent(NoopAction)),
        };

        var result = Sim.Run(entries, MatchOptions.Default, FlatGround, NoWind, TestSeed);

        // Initial round-robin: T0 (C0), T1 (C1), T0 (C2), T1 (C3)
        Assert.Equal(0, result.Log[0].ActingCombatantIndex);
        Assert.Equal(1, result.Log[1].ActingCombatantIndex);
        Assert.Equal(2, result.Log[2].ActingCombatantIndex);
        Assert.Equal(3, result.Log[3].ActingCombatantIndex);

        // C2 must have defeated itself on turn 2.
        Assert.True(result.Log[2].CombatantResults[2].HpAfter <= 0.0, "C2 must self-destruct.");

        // C2 must never appear as actor after it died.
        for (int i = 3; i < result.Log.Count; i++)
            Assert.NotEqual(2, result.Log[i].ActingCombatantIndex);

        // The turn after C2 dies should go to T1 (C3), not C2.
        Assert.Equal(3, result.Log[3].ActingCombatantIndex);
    }

    [Fact]
    public void FriendlyFire_On_BlastDamagesAllies()
    {
        // C0 (T0) fires StraightUpSmall → impact ≈ (0,0).
        // C1 (T0, ally) at (1,0): within 2 m → takes damage with FriendlyFire=on.
        // C2 (T1) at (200,0): outside radius — irrelevant to this check.
        var entries = new[]
        {
            E(  0.0, 0, new ScriptedAgent(StraightUpSmall)),
            E(  1.0, 0, new ScriptedAgent(NoopAction)),
            E(200.0, 1, new ScriptedAgent(NoopAction)),
        };
        var opts = new MatchOptions(FriendlyFire: true, SelfDamage: true);

        var result = Sim.Run(entries, opts, FlatGround, NoWind, TestSeed);

        Assert.True(result.Log[0].CombatantResults[1].DamageReceived > 0.0,
            "Ally (C1) must take damage when FriendlyFire=on.");
    }

    [Fact]
    public void FriendlyFire_Off_AlliesSpared_EnemiesStillDamaged()
    {
        // C0 (T0) fires StraightUpSmall → impact ≈ (0,0).
        // C1 (T0, ally) at (1,0): within radius but FriendlyFire=off → 0 damage.
        // C2 (T1, enemy) at (1.5,0): within radius, enemy → still takes damage.
        var entries = new[]
        {
            E(0.0, 0, new ScriptedAgent(StraightUpSmall)),
            E(1.0, 0, new ScriptedAgent(NoopAction)),
            E(1.5, 1, new ScriptedAgent(NoopAction)),
        };
        var opts = new MatchOptions(FriendlyFire: false, SelfDamage: true);

        var result = Sim.Run(entries, opts, FlatGround, NoWind, TestSeed);

        var turn0 = result.Log[0];
        Assert.Equal(0.0, turn0.CombatantResults[1].DamageReceived);  // ally spared
        Assert.True(turn0.CombatantResults[2].DamageReceived > 0.0, "Enemy must still take damage.");
    }

    [Fact]
    public void FriendlyFireOff_SelfDamageOn_AllySpared_ShooterHurt()
    {
        // FriendlyFire=off + SelfDamage=on: "protect teammates but you can blow yourself up."
        // C0 (T0) fires straight up → within own blast.
        // C1 (T0, ally) at (1,0): also within radius but shielded by FF=off.
        var entries = new[]
        {
            E(0.0, 0, new ScriptedAgent(StraightUpSmall)),
            E(1.0, 0, new ScriptedAgent(NoopAction)),
            E(200.0, 1, new ScriptedAgent(NoopAction)),
        };
        var opts = new MatchOptions(FriendlyFire: false, SelfDamage: true);

        var result = Sim.Run(entries, opts, FlatGround, NoWind, TestSeed);

        var turn0 = result.Log[0];
        Assert.True(turn0.CombatantResults[0].DamageReceived > 0.0, "Shooter must still take self-damage.");
        Assert.Equal(0.0, turn0.CombatantResults[1].DamageReceived);  // ally safe
    }

    [Fact]
    public void FriendlyFireOff_SelfDamageOff_OnlyEnemiesDamaged()
    {
        // FriendlyFire=off + SelfDamage=off: only enemies take damage.
        // C0 (T0): self, SelfDamage=off → 0 damage.
        // C1 (T0, ally) at (1,0): ally, FF=off → 0 damage.
        // C2 (T1, enemy) at (1.5,0): enemy → takes damage.
        var entries = new[]
        {
            E(0.0, 0, new ScriptedAgent(StraightUpSmall)),
            E(1.0, 0, new ScriptedAgent(NoopAction)),
            E(1.5, 1, new ScriptedAgent(NoopAction)),
        };
        var opts = new MatchOptions(FriendlyFire: false, SelfDamage: false);

        var result = Sim.Run(entries, opts, FlatGround, NoWind, TestSeed);

        var turn0 = result.Log[0];
        Assert.Equal(0.0, turn0.CombatantResults[0].DamageReceived);  // self immune
        Assert.Equal(0.0, turn0.CombatantResults[1].DamageReceived);  // ally immune
        Assert.True(turn0.CombatantResults[2].DamageReceived > 0.0, "Enemy must take damage.");
    }

    [Fact]
    public void DoubleTeamKO_SameTurn_ProducesDraw()
    {
        // All four combatants (2 T0, 2 T1) have 1 HP and sit within 2 m of C0's impact.
        // C0 fires StraightUpSmall with FriendlyFire=on + SelfDamage=on → all four defeated.
        var entries = new[]
        {
            E(0.0, 0, new ScriptedAgent(StraightUpSmall), LowHp),
            E(0.5, 0, new ScriptedAgent(NoopAction),      LowHp),
            E(1.0, 1, new ScriptedAgent(NoopAction),      LowHp),
            E(1.5, 1, new ScriptedAgent(NoopAction),      LowHp),
        };

        var result = Sim.Run(entries, MatchOptions.Default, FlatGround, NoWind, TestSeed);

        Assert.Equal(MatchOutcome.Draw, result.Outcome);
        Assert.Null(result.WinningTeamId);
    }

    [Fact]
    public void FourVsFour_RunsToCompletion()
    {
        // Scale check: 8 combatants (4v4). C0 fires KillAction; 4 T1 enemies at 1 HP die
        // on turn 0. FriendlyFire=off so T0 teammates are unaffected.
        var entries = new[]
        {
            E(   0.0, 0, new ScriptedAgent(KillAction)),           // kills all T1 in one shot
            E(1000.0, 0, new ScriptedAgent(NoopAction)),
            E(1100.0, 0, new ScriptedAgent(NoopAction)),
            E(1200.0, 0, new ScriptedAgent(NoopAction)),
            E(  50.0, 1, new ScriptedAgent(NoopAction), LowHp),
            E(  75.0, 1, new ScriptedAgent(NoopAction), LowHp),
            E( 100.0, 1, new ScriptedAgent(NoopAction), LowHp),
            E( 125.0, 1, new ScriptedAgent(NoopAction), LowHp),
        };
        var opts = new MatchOptions(FriendlyFire: false, SelfDamage: false);

        var result = Sim.Run(entries, opts, FlatGround, NoWind, TestSeed);

        Assert.Equal(MatchOutcome.Team0Wins, result.Outcome);
        Assert.Equal(0, result.WinningTeamId);
        Assert.True(result.TurnCount < SimConstants.MaxTurnsPerMatch);
    }

    [Fact]
    public void MaxTurnCap_MultiCombatant_EndsStalemate()
    {
        // All noop → no damage → stalemate hits the cap.
        var entries = new[]
        {
            E(  0.0, 0, new ScriptedAgent(NoopAction)),
            E( 50.0, 0, new ScriptedAgent(NoopAction)),
            E(100.0, 1, new ScriptedAgent(NoopAction)),
            E(150.0, 1, new ScriptedAgent(NoopAction)),
        };

        var result = Sim.Run(entries, MatchOptions.Default, FlatGround, NoWind, TestSeed);

        Assert.Equal(MatchOutcome.MaxTurnsReached, result.Outcome);
        Assert.Null(result.WinningTeamId);
        Assert.Equal(SimConstants.MaxTurnsPerMatch, result.TurnCount);
    }

    [Fact]
    public void Determinism_SameRosterSeedAgents_IdenticalResultAndLog()
    {
        var entries1 = new[]
        {
            E(  0.0, 0, new ScriptedAgent(KillAction)),
            E( 10.0, 0, new ScriptedAgent(NoopAction)),
            E( 50.0, 1, new ScriptedAgent(NoopAction), LowHp),
            E(100.0, 1, new ScriptedAgent(NoopAction), LowHp),
        };
        var entries2 = new[]
        {
            E(  0.0, 0, new ScriptedAgent(KillAction)),
            E( 10.0, 0, new ScriptedAgent(NoopAction)),
            E( 50.0, 1, new ScriptedAgent(NoopAction), LowHp),
            E(100.0, 1, new ScriptedAgent(NoopAction), LowHp),
        };
        var opts = new MatchOptions(FriendlyFire: false, SelfDamage: false);

        var r1 = Sim.Run(entries1, opts, FlatGround, NoWind, TestSeed);
        var r2 = Sim.Run(entries2, opts, FlatGround, NoWind, TestSeed);

        Assert.Equal(r1.Outcome,       r2.Outcome);
        Assert.Equal(r1.WinningTeamId, r2.WinningTeamId);
        Assert.Equal(r1.TurnCount,     r2.TurnCount);
        Assert.Equal(r1.Log.Count,     r2.Log.Count);

        for (int i = 0; i < r1.Log.Count; i++)
            Assert.Equal(r1.Log[i], r2.Log[i]);
    }
}
