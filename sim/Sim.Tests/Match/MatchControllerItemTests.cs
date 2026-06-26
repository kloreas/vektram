using System;
using System.Collections.Generic;
using Sim.Content;
using Sim.Core;
using Sim.Items;
using Sim.Match;
using Sim.Projectile;
using Sim.Terrain;
using Xunit;

namespace Sim.Tests.Match;

/// <summary>
/// Exercises optional item use wired into the live turn (system #3): a RestoreHp heal and a
/// GrantBall shell swap applied before the shot, clean rejection of unavailable items, and
/// determinism. The bot/agent path is untouched — item use enters only through the
/// controller's <see cref="TurnAction"/> overload.
/// </summary>
public class MatchControllerItemTests
{
    private static readonly IProjectileSimulator ProjectileSim = new ProjectileSimulator();
    private static readonly ITerrainQuery        Ground        = FlatTerrain.Ground;
    private static readonly WorldEnvironment     NoWind        = WorldEnvironment.Default;

    private const uint   Seed      = 7u;
    private const double SurviveHp = 1000.0;   // high enough that no one dies on turn 0

    private const string PotionId     = "potion";
    private const string HeavyShellId = "heavy_shell";
    private const double HealAmount   = 30.0;

    // c0 fires straight up at near-zero speed → impact at its own feet (0,0); a target at
    // x=1 is within the blast and takes splash we can read from the turn event.
    private static readonly Weapon     SplashWeapon = new(50.0, 100.0, 10.0);
    private static readonly FireAction FeetShot     = new(90.0, 0.5, SplashWeapon);

    private static readonly ItemCatalog Items = ItemCatalog.FromJson(@"
{ ""schemaVersion"": 1, ""items"": [
    { ""id"": ""potion"",      ""displayName"": ""Potion"",      ""category"": ""Consumable"", ""maxStack"": 9, ""effect"": { ""kind"": ""RestoreHp"", ""amount"": 30.0 } },
    { ""id"": ""heavy_shell"", ""displayName"": ""Heavy Round"", ""category"": ""BallGrant"",  ""maxStack"": 9, ""effect"": { ""kind"": ""GrantBall"", ""ballId"": ""heavy"" } }
] }");

    private static readonly BallCatalog Balls = BallCatalog.FromJson(@"
{ ""schemaVersion"": 1, ""balls"": [
    { ""id"": ""standard"", ""displayName"": ""Standard"", ""type"": ""Standard"", ""gravityScale"": 1.0, ""windSensitivity"": 1.0, ""blastRadius"": 2.5, ""baseDamage"": 100.0, ""projectileSpeed"": 45.0 },
    { ""id"": ""heavy"",    ""displayName"": ""Heavy"",    ""type"": ""Heavy"",    ""gravityScale"": 1.4, ""windSensitivity"": 0.5, ""blastRadius"": 3.2, ""baseDamage"": 140.0, ""projectileSpeed"": 40.0 }
] }");

    private static MatchController NewController(
        Combatant[] combatants,
        int[] teamIds,
        Inventory[]? inventories,
        MatchOptions options,
        WorldEnvironment environment)
        => new(
            ProjectileSim, combatants, teamIds, options, Ground, environment, Seed,
            rules: null, inventories: inventories, itemCatalog: Items, ballCatalog: Balls);

    // ── RestoreHp ───────────────────────────────────────────────────────────────

    [Fact]
    public void RestoreHp_HealsActor_AndIsLogged()
    {
        var combatants = new[]
        {
            new Combatant(new Vec2D(0.0, 0.0), 50.0, CombatantStats.Default),       // MaxHp 100
            new Combatant(new Vec2D(100.0, 0.0), SurviveHp, CombatantStats.Default),
        };
        var inventories = new[] { Inventory.Empty.Add(PotionId, 1), Inventory.Empty };
        var ctrl = NewController(combatants, new[] { 0, 1 }, inventories,
            new MatchOptions(FriendlyFire: false, SelfDamage: false), NoWind);

        TurnEvent turn = ctrl.ResolveTurn(new TurnAction(FeetShot, PotionId));

        Assert.NotNull(turn.ItemUse);
        Assert.True(turn.ItemUse!.Value.Applied);
        Assert.Equal(ItemEffectKind.RestoreHp, turn.ItemUse.Value.Kind);
        Assert.Equal(HealAmount, turn.ItemUse.Value.HpRestored, 6);
        Assert.Equal(80.0, turn.CombatantResults[0].HpBefore, 6);   // 50 + 30, before the (disabled) self-blast
    }

    [Fact]
    public void RestoreHp_DoesNotOverheal_ClampsToMaxHp()
    {
        var combatants = new[]
        {
            new Combatant(new Vec2D(0.0, 0.0), 90.0, CombatantStats.Default),       // MaxHp 100
            new Combatant(new Vec2D(100.0, 0.0), SurviveHp, CombatantStats.Default),
        };
        var inventories = new[] { Inventory.Empty.Add(PotionId, 1), Inventory.Empty };
        var ctrl = NewController(combatants, new[] { 0, 1 }, inventories,
            new MatchOptions(FriendlyFire: false, SelfDamage: false), NoWind);

        TurnEvent turn = ctrl.ResolveTurn(new TurnAction(FeetShot, PotionId));

        Assert.Equal(10.0, turn.ItemUse!.Value.HpRestored, 6);      // 90 → 100, only 10 restored
        Assert.Equal(100.0, turn.CombatantResults[0].HpBefore, 6);
    }

    [Fact]
    public void RestoreHp_AppliesBeforeShot_SelfBlastSubtractsFromHealedHp()
    {
        // Clarification #1: heal first, then the same turn's self-blast hits the post-heal HP.
        var inventories = new[] { Inventory.Empty.Add(PotionId, 1), Inventory.Empty };
        var selfDamage  = new MatchOptions(FriendlyFire: false, SelfDamage: true);

        var healed = NewController(
            new[]
            {
                new Combatant(new Vec2D(0.0, 0.0), 50.0, CombatantStats.Default),
                new Combatant(new Vec2D(100.0, 0.0), SurviveHp, CombatantStats.Default),
            },
            new[] { 0, 1 }, inventories, selfDamage, NoWind);
        var noHeal = NewController(
            new[]
            {
                new Combatant(new Vec2D(0.0, 0.0), 50.0, CombatantStats.Default),
                new Combatant(new Vec2D(100.0, 0.0), SurviveHp, CombatantStats.Default),
            },
            new[] { 0, 1 }, null, selfDamage, NoWind);

        CombatantTurnResult healedActor = healed.ResolveTurn(new TurnAction(FeetShot, PotionId)).CombatantResults[0];
        CombatantTurnResult plainActor  = noHeal.ResolveTurn(FeetShot).CombatantResults[0];

        Assert.Equal(80.0, healedActor.HpBefore, 6);                       // healed 50 → 80 before the blast
        Assert.Equal(50.0, plainActor.HpBefore, 6);
        Assert.True(plainActor.DamageReceived > 0.0);                      // the self-blast actually lands
        Assert.Equal(plainActor.DamageReceived, healedActor.DamageReceived, 6);  // same shot, same self-damage
        Assert.Equal(plainActor.HpAfter + HealAmount, healedActor.HpAfter, 6);   // net: heal raised the floor by 30
    }

    // ── GrantBall ─────────────────────────────────────────────────────────────────

    [Fact]
    public void GrantBall_UsesGrantedShellDamage_NotWeaponDamage()
    {
        BallDefinition heavy = Balls.Get("heavy");

        double granted = GrantBallTurnTargetDamage(out double fireOnly);

        var cmd = new FireCommand(new Vec2D(0.0, 0.0), FeetShot.AngleDegrees, FeetShot.Speed, Seed);
        Vec2D impact = ProjectileSim.Simulate(cmd, NoWind, Ground, heavy.Physics).ImpactPoint;
        double expected = ExpectedDamage(
            heavy.BaseDamage, heavy.BlastRadius, new Vec2D(1.0, 0.0),
            CombatantStats.Default, CombatantStats.Default, impact);

        Assert.Equal(expected, granted, 6);          // damage came from the heavy ball
        Assert.NotEqual(fireOnly, granted);          // not the default weapon's damage
    }

    [Fact]
    public void GrantBall_UsesGrantedShellPhysics_ChangesTrajectory()
    {
        BallDefinition heavy = Balls.Get("heavy");
        var wind = new WorldEnvironment(SimConstants.DefaultGravity, 8.0);
        var arc  = new FireAction(45.0, 30.0, SplashWeapon);

        Vec2D neutralImpact = SingleShotImpact(arc, itemId: null, wind);
        Vec2D heavyImpact   = SingleShotImpact(arc, itemId: HeavyShellId, wind);

        var cmd = new FireCommand(new Vec2D(0.0, 0.0), arc.AngleDegrees, arc.Speed, Seed);
        Vec2D expectedHeavyImpact = ProjectileSim.Simulate(cmd, wind, Ground, heavy.Physics).ImpactPoint;

        Assert.Equal(expectedHeavyImpact, heavyImpact);                       // trajectory used the heavy physics
        Assert.True(Math.Abs(heavyImpact.X - neutralImpact.X) > 0.5);         // and it differs from the neutral path
    }

    [Fact]
    public void GrantBall_DrivesBothTrajectoryAndDamage_FromSameBall()
    {
        // Clarification #2: within one GrantBall turn, the simulated shot AND the applied damage
        // both come from the granted ball — no physics/ball-vs-weapon mismatch.
        BallDefinition heavy = Balls.Get("heavy");
        var combatants = new[]
        {
            new Combatant(new Vec2D(0.0, 0.0), SurviveHp, CombatantStats.Default),
            new Combatant(new Vec2D(1.0, 0.0), SurviveHp, CombatantStats.Default),
        };
        var inventories = new[] { Inventory.Empty.Add(HeavyShellId, 1), Inventory.Empty };
        var ctrl = NewController(combatants, new[] { 0, 1 }, inventories, MatchOptions.Default, NoWind);

        TurnEvent turn = ctrl.ResolveTurn(new TurnAction(FeetShot, HeavyShellId));

        var cmd = new FireCommand(new Vec2D(0.0, 0.0), FeetShot.AngleDegrees, FeetShot.Speed, Seed);
        ShotResult heavyShot = ProjectileSim.Simulate(cmd, NoWind, Ground, heavy.Physics);
        double expectedDamage = ExpectedDamage(
            heavy.BaseDamage, heavy.BlastRadius, combatants[1].Position,
            CombatantStats.Default, CombatantStats.Default, heavyShot.ImpactPoint);

        Assert.Equal(heavyShot.ImpactPoint, turn.ImpactPoint);                        // physics ← heavy
        Assert.Equal(expectedDamage, turn.CombatantResults[1].DamageReceived, 6);     // damage ← heavy
        Assert.Equal("heavy", turn.ItemUse!.Value.GrantedBallId);
    }

    // ── Inventory consumption ─────────────────────────────────────────────────────

    [Fact]
    public void SuccessfulUse_DecrementsInventory()
    {
        var combatants = new[]
        {
            new Combatant(new Vec2D(0.0, 0.0), SurviveHp, CombatantStats.Default),
            new Combatant(new Vec2D(1.0, 0.0), SurviveHp, CombatantStats.Default),
        };
        var inventories = new[] { Inventory.Empty.Add(PotionId, 2), Inventory.Empty };
        var ctrl = NewController(combatants, new[] { 0, 1 }, inventories, MatchOptions.Default, NoWind);

        ctrl.ResolveTurn(new TurnAction(FeetShot, PotionId));

        Assert.Equal(1, ctrl.InventoryOf(0).CountOf(PotionId));
    }

    // ── Clean rejection ───────────────────────────────────────────────────────────

    [Fact]
    public void UsingItemNotHeld_IsRejectedCleanly_TurnFiresFireOnly()
    {
        var combatants = new[]
        {
            new Combatant(new Vec2D(0.0, 0.0), SurviveHp, CombatantStats.Default),
            new Combatant(new Vec2D(1.0, 0.0), SurviveHp, CombatantStats.Default),
        };
        var withItem = NewController(combatants, new[] { 0, 1 },
            new[] { Inventory.Empty, Inventory.Empty }, MatchOptions.Default, NoWind);
        var plain = NewController(CloneRoster(combatants), new[] { 0, 1 }, null, MatchOptions.Default, NoWind);

        TurnEvent rejected = withItem.ResolveTurn(new TurnAction(FeetShot, PotionId));
        TurnEvent fireOnly = plain.ResolveTurn(FeetShot);

        Assert.False(rejected.ItemUse!.Value.Applied);
        Assert.Equal(ItemEffectKind.RestoreHp, rejected.ItemUse.Value.Kind);     // known item, just not held
        Assert.Equal(0, withItem.InventoryOf(0).StackCount);                     // inventory untouched
        Assert.Equal(fireOnly.CombatantResults[1].DamageReceived, rejected.CombatantResults[1].DamageReceived, 6);
        Assert.False(withItem.IsOver);                                           // the turn still proceeded
    }

    [Fact]
    public void UsingUnknownItemId_IsRejectedCleanly_WithNullKind()
    {
        var combatants = new[]
        {
            new Combatant(new Vec2D(0.0, 0.0), SurviveHp, CombatantStats.Default),
            new Combatant(new Vec2D(1.0, 0.0), SurviveHp, CombatantStats.Default),
        };
        var ctrl = NewController(combatants, new[] { 0, 1 },
            new[] { Inventory.Empty.Add(PotionId, 1), Inventory.Empty }, MatchOptions.Default, NoWind);

        TurnEvent turn = ctrl.ResolveTurn(new TurnAction(FeetShot, "does_not_exist"));

        Assert.False(turn.ItemUse!.Value.Applied);
        Assert.Null(turn.ItemUse.Value.Kind);
        Assert.Equal(1, ctrl.InventoryOf(0).CountOf(PotionId));                  // unrelated stack untouched
    }

    [Fact]
    public void ItemUse_WithoutCatalogs_IsRejectedCleanly()
    {
        var combatants = new[]
        {
            new Combatant(new Vec2D(0.0, 0.0), SurviveHp, CombatantStats.Default),
            new Combatant(new Vec2D(1.0, 0.0), SurviveHp, CombatantStats.Default),
        };
        // No item/ball catalog supplied → item use cannot be resolved.
        var ctrl = new MatchController(
            ProjectileSim, combatants, new[] { 0, 1 }, MatchOptions.Default, Ground, NoWind, Seed);

        TurnEvent turn = ctrl.ResolveTurn(new TurnAction(FeetShot, PotionId));

        Assert.False(turn.ItemUse!.Value.Applied);
        Assert.Null(turn.ItemUse.Value.Kind);
    }

    // ── Determinism & no-item equivalence ─────────────────────────────────────────

    [Fact]
    public void SameSeedAndActions_WithItemUse_ProduceIdenticalLogs()
    {
        List<TurnEvent> first  = RunScriptedItemMatch();
        List<TurnEvent> second = RunScriptedItemMatch();

        Assert.Equal(first.Count, second.Count);
        for (int i = 0; i < first.Count; i++)
            Assert.Equal(first[i], second[i]);
    }

    [Fact]
    public void FireOnlyTurnAction_EqualsFireActionOverload()
    {
        var combatants = new[]
        {
            new Combatant(new Vec2D(0.0, 0.0), SurviveHp, CombatantStats.Default),
            new Combatant(new Vec2D(1.0, 0.0), SurviveHp, CombatantStats.Default),
        };
        var viaAction = NewController(combatants, new[] { 0, 1 },
            new[] { Inventory.Empty.Add(PotionId, 1), Inventory.Empty }, MatchOptions.Default, NoWind);
        var viaFire = new MatchController(
            ProjectileSim, CloneRoster(combatants), new[] { 0, 1 }, MatchOptions.Default, Ground, NoWind, Seed);

        TurnEvent withItemsConfigured = viaAction.ResolveTurn(new TurnAction(FeetShot, null));
        TurnEvent plain               = viaFire.ResolveTurn(FeetShot);

        Assert.Equal(plain, withItemsConfigured);   // fire-only is bit-identical regardless of item config
        Assert.Null(withItemsConfigured.ItemUse);
    }

    // ── Item use flips the authoritative OUTCOME ──────────────────────────────────

    [Fact]
    public void ItemUse_FlipsAuthoritativeOutcome_VsFireOnlyControl()
    {
        // A 1-turn cap + a zero-damage weapon: fire-only cannot end the match (the lone enemy
        // survives → MaxTurnsReached). Using the heavy shell first swaps in a lethal ball that
        // one-shots the enemy → Team0Wins. The single item use flips the authoritative result.
        var inertShot = new FireAction(90.0, 0.5, new Weapon(50.0, 0.0, 0.0));   // feet impact, deals nothing fire-only
        var capOne    = new MatchModeRules(WinConditionDefinition.LastTeamStanding, 1, TurnOrderPolicyKind.RoundRobin);

        MatchController Build(Inventory[]? inventories) => new(
            ProjectileSim,
            new[]
            {
                new Combatant(new Vec2D(0.0, 0.0), SurviveHp, CombatantStats.Default),
                new Combatant(new Vec2D(1.0, 0.0), 50.0, CombatantStats.Default),    // dies to the heavy ball, survives a 0-damage shot
            },
            new[] { 0, 1 }, new MatchOptions(FriendlyFire: false, SelfDamage: false),
            Ground, NoWind, Seed, rules: null, inventories: inventories,
            itemCatalog: Items, ballCatalog: Balls, modeRules: capOne);

        var withItem = Build(new[] { Inventory.Empty.Add(HeavyShellId, 1), Inventory.Empty });
        var fireOnly = Build(null);

        TurnEvent itemTurn = withItem.ResolveTurn(new TurnAction(inertShot, HeavyShellId));
        fireOnly.ResolveTurn(new TurnAction(inertShot, null));

        Assert.True(itemTurn.ItemUse!.Value.Applied);                            // the heavy shell resolved
        Assert.Equal("heavy", itemTurn.ItemUse.Value.GrantedBallId);
        Assert.Equal(MatchOutcome.Team0Wins, withItem.Result.Outcome);          // lethal granted ball ended it
        Assert.Equal(MatchOutcome.MaxTurnsReached, fireOnly.Result.Outcome);    // a 0-damage shot could not
        Assert.NotEqual(withItem.Result.Outcome, fireOnly.Result.Outcome);      // one item use flipped the outcome
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────

    private static List<TurnEvent> RunScriptedItemMatch()
    {
        var combatants = new[]
        {
            new Combatant(new Vec2D(0.0, 0.0), SurviveHp, CombatantStats.Default),
            new Combatant(new Vec2D(1.0, 0.0), SurviveHp, CombatantStats.Default),
        };
        var inventories = new[]
        {
            Inventory.Empty.Add(PotionId, 1),
            Inventory.Empty.Add(HeavyShellId, 1),
        };
        var ctrl = NewController(combatants, new[] { 0, 1 }, inventories, MatchOptions.Default, NoWind);

        var log = new List<TurnEvent>
        {
            ctrl.ResolveTurn(new TurnAction(FeetShot, PotionId)),        // c0 heals then fires
            ctrl.ResolveTurn(new TurnAction(FeetShot, HeavyShellId)),    // c1 swaps shell then fires
        };
        return log;
    }

    private static double GrantBallTurnTargetDamage(out double fireOnlyTargetDamage)
    {
        var combatants = new[]
        {
            new Combatant(new Vec2D(0.0, 0.0), SurviveHp, CombatantStats.Default),
            new Combatant(new Vec2D(1.0, 0.0), SurviveHp, CombatantStats.Default),
        };
        var inventories = new[] { Inventory.Empty.Add(HeavyShellId, 1), Inventory.Empty };

        var granted = NewController(combatants, new[] { 0, 1 }, inventories, MatchOptions.Default, NoWind);
        var plain   = NewController(CloneRoster(combatants), new[] { 0, 1 }, null, MatchOptions.Default, NoWind);

        double grantedDamage = granted.ResolveTurn(new TurnAction(FeetShot, HeavyShellId)).CombatantResults[1].DamageReceived;
        fireOnlyTargetDamage = plain.ResolveTurn(FeetShot).CombatantResults[1].DamageReceived;
        return grantedDamage;
    }

    private static Vec2D SingleShotImpact(FireAction shot, string? itemId, WorldEnvironment environment)
    {
        var combatants = new[]
        {
            new Combatant(new Vec2D(0.0, 0.0), SurviveHp, CombatantStats.Default),
            new Combatant(new Vec2D(200.0, 0.0), SurviveHp, CombatantStats.Default),
        };
        var inventories = new[] { Inventory.Empty.Add(HeavyShellId, 1), Inventory.Empty };
        var ctrl = NewController(combatants, new[] { 0, 1 }, inventories, MatchOptions.Default, environment);

        return ctrl.ResolveTurn(new TurnAction(shot, itemId)).ImpactPoint;
    }

    private static double ExpectedDamage(
        double baseDamage, double blastRadius, Vec2D target,
        CombatantStats attacker, CombatantStats defender, Vec2D impact)
    {
        var inputs = new DamageInputs(
            impact, target, baseDamage, blastRadius,
            attacker, defender, CombatTuning.Default, 1.0, 1.0, false, false);
        return DamageCalculator.Compute(inputs).FinalDamage;
    }

    private static Combatant[] CloneRoster(Combatant[] roster)
    {
        var copy = new Combatant[roster.Length];
        Array.Copy(roster, copy, roster.Length);
        return copy;
    }
}
