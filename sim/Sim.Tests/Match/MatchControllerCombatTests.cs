using Sim.Content;
using Sim.Core;
using Sim.Match;
using Sim.Projectile;
using Sim.Terrain;
using Xunit;

namespace Sim.Tests.Match;

/// <summary>
/// Exercises the damage formula through the match controller: the controller does the seeded
/// crit/miss rolls and the element-advantage lookup, then feeds resolved inputs to the pure
/// <see cref="DamageCalculator"/>.
/// </summary>
public class MatchControllerCombatTests
{
    private static readonly IProjectileSimulator ProjectileSim = new ProjectileSimulator();
    private static readonly ITerrainQuery        Ground        = FlatTerrain.Ground;
    private static readonly WorldEnvironment     NoWind         = WorldEnvironment.Default;

    private const uint   Seed       = 0u;
    private const double SurviveHp  = 1000.0;   // high enough that no one dies on turn 0

    // c0 fires straight up at near-zero speed → impact at its own feet (0,0); c1 at x=1 is
    // within the 10 m blast and takes splash damage we can read from the turn event.
    private static readonly Weapon     SplashWeapon = new(50.0, 100.0, 10.0);
    private static readonly FireAction SplashShot   = new(90.0, 0.5, SplashWeapon);

    private static CombatantTurnResult ResolveTargetResult(
        CombatantStats attacker, CombatantStats defender, CombatRules? rules)
    {
        var combatants = new[]
        {
            new Combatant(new Vec2D(0.0, 0.0), SurviveHp, attacker),
            new Combatant(new Vec2D(1.0, 0.0), SurviveHp, defender),
        };
        var teamIds = new[] { 0, 1 };

        var ctrl = new MatchController(
            ProjectileSim, combatants, teamIds, MatchOptions.Default, Ground, NoWind, Seed, rules);

        TurnEvent turn = ctrl.ResolveTurn(SplashShot);
        return turn.CombatantResults[1];
    }

    // ── Crit ───────────────────────────────────────────────────────────────────

    [Fact]
    public void GuaranteedCrit_AppliesMultiplier_AndFlagsResult()
    {
        var critAttacker = CombatantStats.Default with { CritChance = 1.0, CritMultiplier = 2.0 };

        CombatantTurnResult baseline = ResolveTargetResult(CombatantStats.Default, CombatantStats.Default, CombatRules.Default);
        CombatantTurnResult crit     = ResolveTargetResult(critAttacker, CombatantStats.Default, CombatRules.Default);

        Assert.True(crit.IsCrit);
        Assert.False(baseline.IsCrit);
        Assert.Equal(baseline.DamageReceived * 2.0, crit.DamageReceived, 6);
    }

    [Fact]
    public void GuaranteedCrit_IsReproducibleAcrossSameSeedRuns()
    {
        var critAttacker = CombatantStats.Default with { CritChance = 1.0, CritMultiplier = 2.0 };

        CombatantTurnResult first  = ResolveTargetResult(critAttacker, CombatantStats.Default, CombatRules.Default);
        CombatantTurnResult second = ResolveTargetResult(critAttacker, CombatantStats.Default, CombatRules.Default);

        Assert.Equal(first, second);
    }

    // ── Miss ───────────────────────────────────────────────────────────────────

    [Fact]
    public void GuaranteedDodge_DealsZero_AndFlagsMiss()
    {
        var dodgyDefender = CombatantStats.Default with { Dodge = 1.0 };

        CombatantTurnResult result = ResolveTargetResult(CombatantStats.Default, dodgyDefender, CombatRules.Default);

        Assert.True(result.IsMiss);
        Assert.Equal(0.0, result.DamageReceived);
    }

    // ── Element advantage ──────────────────────────────────────────────────────

    [Fact]
    public void ElementAdvantage_IncreasesRecordedDamage()
    {
        var fireAttacker  = CombatantStats.Default with { Element = Element.Fire, ElementPower = 100.0 };
        var waterDefender = CombatantStats.Default with { Element = Element.Water, ElementResist = 0.0 };

        ElementTable table = ElementTable.FromJson(@"
{ ""schemaVersion"": 1, ""elements"": [""None"",""Fire"",""Water""],
  ""advantages"": [ { ""attacker"": ""Fire"", ""defender"": ""Water"", ""multiplier"": 1.25 } ] }");

        CombatantTurnResult neutral   = ResolveTargetResult(fireAttacker, waterDefender, CombatRules.Default);
        CombatantTurnResult advantaged = ResolveTargetResult(
            fireAttacker, waterDefender, new CombatRules(CombatTuning.Default, 1.0, table));

        Assert.True(advantaged.DamageReceived > neutral.DamageReceived);
    }

    // ── Mode multiplier ────────────────────────────────────────────────────────

    [Fact]
    public void ModeMultiplier_ScalesRecordedDamage()
    {
        CombatantTurnResult full = ResolveTargetResult(
            CombatantStats.Default, CombatantStats.Default, CombatRules.Default);
        CombatantTurnResult half = ResolveTargetResult(
            CombatantStats.Default, CombatantStats.Default, new CombatRules(CombatTuning.Default, 0.5, null));

        Assert.Equal(full.DamageReceived * 0.5, half.DamageReceived, 6);
    }
}
