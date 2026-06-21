using System;
using Sim.Ai;
using Sim.Core;
using Sim.Match;
using Sim.Projectile;
using Sim.Terrain;
using Sim.Tests.Match;
using Xunit;

namespace Sim.Tests.Ai;

public class BotAgentTests
{
    // ── Infrastructure ────────────────────────────────────────────────────────

    private static readonly IProjectileSimulator ProjectileSim = new ProjectileSimulator();
    private static readonly IMatchSimulator      MatchSim      = new MatchSimulator(ProjectileSim);
    private static readonly ITerrainQuery        FlatGround    = FlatTerrain.Ground;

    private const uint   DefaultSeed   = 42;
    private const double StartHp       = 100.0;
    private const double ShotSpeed     = 50.0;

    // Standard weapon: tight blast for precision tests
    private static readonly Weapon StandardWeapon    = new(ShotSpeed, 100.0,  3.0);
    // Large blast for competence test — ensures a near-miss still deals damage
    private static readonly Weapon CompetenceWeapon  = new(ShotSpeed, 200.0, 10.0);

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static Combatant MakeCombatant(double x, double hp = StartHp)
        => new(new Vec2D(x, 0.0), hp, CombatantStats.Default);

    private static MatchState MakeState(
        Vec2D          selfPos,
        Combatant[]    allies,
        Combatant[]    enemies,
        WorldEnvironment env,
        int            turn = 0)
        => new(
            new Combatant(selfPos, StartHp, CombatantStats.Default),
            allies,
            enemies,
            FlatGround,
            env,
            turn);

    private static FireCommand MakeCmd(Vec2D origin, FireAction action)
        => new(origin, action.AngleDegrees, action.Speed, DefaultSeed);

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Determinism_SameSeed_SameState_IdenticalFireAction()
    {
        var env     = WorldEnvironment.Default;
        var enemies = new[] { MakeCombatant(100.0) };
        var state   = MakeState(Vec2D.Zero, Array.Empty<Combatant>(), enemies, env);

        var botA = new BotAgent(ProjectileSim, StandardWeapon, BotDifficulty.Medium, DefaultSeed);
        var botB = new BotAgent(ProjectileSim, StandardWeapon, BotDifficulty.Medium, DefaultSeed);

        var actionA = botA.ChooseAction(state);
        var actionB = botB.ChooseAction(state);

        Assert.Equal(actionA.AngleDegrees, actionB.AngleDegrees);
        Assert.Equal(actionA.Speed,        actionB.Speed);
        Assert.Equal(actionA.Weapon,       actionB.Weapon);
    }

    [Fact]
    public void Competence_HardBot_DefeatsPassiveOpponent()
    {
        const double EnemyX   = 100.0;
        const int    MaxTurns = 20;

        var passiveWeapon = new Weapon(ShotSpeed, 0.0, 0.0);
        var passive       = new ScriptedAgent(new FireAction(90.0, ShotSpeed, passiveWeapon));
        var hardBot       = new BotAgent(ProjectileSim, CompetenceWeapon, BotDifficulty.Hard, DefaultSeed);

        var entries = new[]
        {
            new CombatantEntry(MakeCombatant(0.0),     0, hardBot),
            new CombatantEntry(MakeCombatant(EnemyX),  1, passive),
        };
        var opts = new MatchOptions(FriendlyFire: false, SelfDamage: false);

        var result = MatchSim.Run(entries, opts, FlatGround, WorldEnvironment.Default, DefaultSeed);

        Assert.Equal(MatchOutcome.Team0Wins, result.Outcome);
        Assert.True(result.TurnCount <= MaxTurns,
            $"Hard bot needed {result.TurnCount} turns; expected ≤ {MaxTurns}");
    }

    [Fact]
    public void TargetSelection_ShotLandsNearNearestEnemy()
    {
        const double NearX = 50.0;
        const double FarX  = 300.0;

        var env     = WorldEnvironment.Default;
        var enemies = new[] { MakeCombatant(NearX), MakeCombatant(FarX) };
        var state   = MakeState(Vec2D.Zero, Array.Empty<Combatant>(), enemies, env);

        var bot    = new BotAgent(ProjectileSim, StandardWeapon, BotDifficulty.Hard, DefaultSeed);
        var action = bot.ChooseAction(state);
        var shot   = ProjectileSim.Simulate(MakeCmd(Vec2D.Zero, action), env, FlatGround);

        double distNear = Math.Abs(shot.ImpactPoint.X - NearX);
        double distFar  = Math.Abs(shot.ImpactPoint.X - FarX);

        Assert.True(distNear < distFar,
            $"Impact at {shot.ImpactPoint.X:F2} m — dist-to-near={distNear:F2} dist-to-far={distFar:F2}");
    }

    [Fact]
    public void DifficultyOrdering_HardMostAccurate_EasyLeastAccurate()
    {
        const double TargetX   = 150.0;
        const int    SeedCount = 5;

        var env     = WorldEnvironment.Default;
        var enemies = new[] { MakeCombatant(TargetX) };
        var state   = MakeState(Vec2D.Zero, Array.Empty<Combatant>(), enemies, env);

        double sumEasy = 0, sumMedium = 0, sumHard = 0;

        for (uint seed = 0; seed < SeedCount; seed++)
        {
            var botEasy   = new BotAgent(ProjectileSim, StandardWeapon, BotDifficulty.Easy,   seed);
            var botMedium = new BotAgent(ProjectileSim, StandardWeapon, BotDifficulty.Medium, seed);
            var botHard   = new BotAgent(ProjectileSim, StandardWeapon, BotDifficulty.Hard,   seed);

            var shotEasy   = ProjectileSim.Simulate(MakeCmd(Vec2D.Zero, botEasy.ChooseAction(state)),   env, FlatGround);
            var shotMedium = ProjectileSim.Simulate(MakeCmd(Vec2D.Zero, botMedium.ChooseAction(state)), env, FlatGround);
            var shotHard   = ProjectileSim.Simulate(MakeCmd(Vec2D.Zero, botHard.ChooseAction(state)),   env, FlatGround);

            sumEasy   += Math.Abs(shotEasy.ImpactPoint.X   - TargetX);
            sumMedium += Math.Abs(shotMedium.ImpactPoint.X - TargetX);
            sumHard   += Math.Abs(shotHard.ImpactPoint.X   - TargetX);
        }

        double avgHard   = sumHard   / SeedCount;
        double avgMedium = sumMedium / SeedCount;
        double avgEasy   = sumEasy   / SeedCount;

        Assert.True(avgHard < avgMedium,
            $"Hard avg miss {avgHard:F2} m must be < Medium avg miss {avgMedium:F2} m");
        Assert.True(avgMedium < avgEasy,
            $"Medium avg miss {avgMedium:F2} m must be < Easy avg miss {avgEasy:F2} m");
    }

    [Fact]
    public void WindCompensation_FullComp_NearTarget_ZeroComp_MissesMore()
    {
        const double TargetX    = 100.0;
        const double WindX      = 5.0;
        const double NearThresh = 8.0; // full-comp expected within 8 m (grid quantization ~1.5°)

        var selfPos = Vec2D.Zero;
        var windEnv = new WorldEnvironment(SimConstants.DefaultGravity, WindX);
        var enemies = new[] { MakeCombatant(TargetX) };
        var state   = MakeState(selfPos, Array.Empty<Combatant>(), enemies, windEnv);

        var botFull = new BotAgent(ProjectileSim, StandardWeapon, new BotDifficulty(60, 0.0, 1.0), DefaultSeed);
        var botNone = new BotAgent(ProjectileSim, StandardWeapon, new BotDifficulty(60, 0.0, 0.0), DefaultSeed);

        var actionFull = botFull.ChooseAction(state);
        var actionNone = botNone.ChooseAction(state);

        var cmdFull = new FireCommand(selfPos, actionFull.AngleDegrees, actionFull.Speed, DefaultSeed);
        var cmdNone = new FireCommand(selfPos, actionNone.AngleDegrees, actionNone.Speed, DefaultSeed);

        // Both evaluated in the actual (wind) environment
        var shotFull       = ProjectileSim.Simulate(cmdFull, windEnv,               FlatGround);
        var shotNone       = ProjectileSim.Simulate(cmdNone, windEnv,               FlatGround);
        // No-comp drift: difference between with-wind and no-wind landings for the same angle
        var shotNoneNoWind = ProjectileSim.Simulate(cmdNone, WorldEnvironment.Default, FlatGround);

        double missFull   = Math.Abs(shotFull.ImpactPoint.X - TargetX);
        double missNone   = Math.Abs(shotNone.ImpactPoint.X - TargetX);
        double windDrift  = Math.Abs(shotNone.ImpactPoint.X - shotNoneNoWind.ImpactPoint.X);

        Assert.True(missFull < NearThresh,
            $"Full-comp shot landed {missFull:F2} m from target; expected < {NearThresh} m");
        Assert.True(missNone >= windDrift * 0.5,
            $"No-comp miss {missNone:F2} m should be ≥ half wind drift {windDrift:F2} m");
    }

    [Fact]
    public void SearchBudget_LargerBudget_CloserImpact()
    {
        const double TargetX = 150.0;

        var env     = WorldEnvironment.Default;
        var enemies = new[] { MakeCombatant(TargetX) };
        var state   = MakeState(Vec2D.Zero, Array.Empty<Combatant>(), enemies, env);

        // No noise, no wind — isolates pure budget effect
        var botSmall = new BotAgent(ProjectileSim, StandardWeapon, new BotDifficulty(3,  0.0, 1.0), DefaultSeed);
        var botLarge = new BotAgent(ProjectileSim, StandardWeapon, new BotDifficulty(60, 0.0, 1.0), DefaultSeed);

        var shotSmall = ProjectileSim.Simulate(MakeCmd(Vec2D.Zero, botSmall.ChooseAction(state)), env, FlatGround);
        var shotLarge = ProjectileSim.Simulate(MakeCmd(Vec2D.Zero, botLarge.ChooseAction(state)), env, FlatGround);

        double missSmall = Math.Abs(shotSmall.ImpactPoint.X - TargetX);
        double missLarge = Math.Abs(shotLarge.ImpactPoint.X - TargetX);

        Assert.True(missLarge < missSmall,
            $"Budget-60 miss {missLarge:F2} m must be < budget-3 miss {missSmall:F2} m");
    }

    [Fact]
    public void ValidFireAction_NoEnemies_ReturnsFallback()
    {
        var state = MakeState(Vec2D.Zero, Array.Empty<Combatant>(), Array.Empty<Combatant>(), WorldEnvironment.Default);
        var bot   = new BotAgent(ProjectileSim, StandardWeapon, BotDifficulty.Hard, DefaultSeed);

        var action = bot.ChooseAction(state);

        Assert.Equal(90.0,                          action.AngleDegrees);
        Assert.Equal(StandardWeapon.ProjectileSpeed, action.Speed);
        Assert.Equal(StandardWeapon,                 action.Weapon);
    }

    [Fact]
    public void BotVsBot_2v2_RunsToCompletion_TerminalOutcome()
    {
        var weapon = new Weapon(ShotSpeed, 50.0, 5.0);

        // Hard bots for team 0, Medium for team 1 — mix of difficulties
        var entries = new[]
        {
            new CombatantEntry(MakeCombatant(  0.0), 0, new BotAgent(ProjectileSim, weapon, BotDifficulty.Hard,   DefaultSeed)),
            new CombatantEntry(MakeCombatant( 20.0), 0, new BotAgent(ProjectileSim, weapon, BotDifficulty.Hard,   DefaultSeed + 1)),
            new CombatantEntry(MakeCombatant(100.0), 1, new BotAgent(ProjectileSim, weapon, BotDifficulty.Medium, DefaultSeed + 2)),
            new CombatantEntry(MakeCombatant(120.0), 1, new BotAgent(ProjectileSim, weapon, BotDifficulty.Medium, DefaultSeed + 3)),
        };
        var opts = new MatchOptions(FriendlyFire: false, SelfDamage: false);

        var result = MatchSim.Run(entries, opts, FlatGround, WorldEnvironment.Default, DefaultSeed);

        Assert.True(result.TurnCount > 0);
        Assert.True(
            result.Outcome == MatchOutcome.Team0Wins     ||
            result.Outcome == MatchOutcome.Team1Wins     ||
            result.Outcome == MatchOutcome.Draw          ||
            result.Outcome == MatchOutcome.MaxTurnsReached,
            $"Unexpected outcome: {result.Outcome}");
    }
}
