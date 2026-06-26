using System;
using System.Collections.Generic;
using System.IO;
using Sim.Content;
using Sim.Core;
using Sim.Match;
using Sim.Projectile;
using Sim.Stats;
using Sim.Terrain;
using Xunit;

namespace Sim.Tests.Match;

/// <summary>
/// Locks the loadout → match-setup seam that makes system #4 live in the product: a combatant's
/// effective <see cref="CombatantStats"/> are PRODUCED by <see cref="LoadoutResolver.Resolve"/>
/// from the shipped <c>equipment.json</c> (not <see cref="CombatantStats.Default"/>), and those
/// resolved stats deterministically alter the authoritative match outcome. The damage formula and
/// turn loop are untouched — they still consume a finished <see cref="CombatantStats"/>.
/// </summary>
public class LoadoutMatchSetupTests
{
    private static readonly IProjectileSimulator ProjectileSim = new ProjectileSimulator();
    private static readonly ITerrainQuery        Ground        = FlatTerrain.Ground;
    private static readonly WorldEnvironment     NoWind        = WorldEnvironment.Default;

    private const uint Seed = 0u;

    // The two stat-bearing recruit pieces from the shipped equipment.json. The crit accessory is
    // deliberately omitted so the resolved attacker draws no crit RNG — the outcome shift is then a
    // clean, RNG-free function of the gear.
    private const string RecruitCannonId = "weapon_recruit_cannon";
    private const string RecruitPlateId  = "armor_recruit_plate";

    // c0 fires straight up at near-zero speed → impact at its own feet (0,0); a target at x=1 is
    // inside the splash. Self/friendly damage are off so geometry stays trivial.
    private static readonly Weapon     SplashWeapon = new(50.0, 100.0, 10.0);
    private static readonly FireAction FeetShot     = new(90.0, 0.5, SplashWeapon);
    private static readonly Weapon     InertWeapon  = new(50.0, 0.0, 0.0);
    private static readonly FireAction NoopShot     = new(90.0, 0.5, InertWeapon);

    private static readonly MatchOptions IsolatedBlast = new(FriendlyFire: false, SelfDamage: false);

    private static EquipmentCatalog ShippedEquipment() =>
        EquipmentCatalog.FromJson(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "content", "equipment.json")));

    private static Loadout RecruitLoadout() =>
        new(CombatantStats.Default, new[] { RecruitCannonId, RecruitPlateId }, null, null);

    // ── Resolution produces real, non-default stats ───────────────────────────────

    [Fact]
    public void Resolve_FromShippedEquipmentJson_ProducesNonDefaultStats()
    {
        CombatantStats resolved = LoadoutResolver.Resolve(RecruitLoadout(), ShippedEquipment());

        Assert.NotEqual(CombatantStats.Default, resolved);
        Assert.Equal(2500.0, resolved.Attack, 6);     // base 0 + cannon flat 2500
        Assert.Equal(4000.0, resolved.Defense, 6);    // base 0 + plate flat 4000
        Assert.Equal(600.0, resolved.BaseGuard, 6);   // base 0 + plate flat 600
        Assert.Equal(150.0, resolved.MaxHp, 6);       // base 100 + plate flat 50
    }

    [Fact]
    public void Resolve_FromShippedEquipmentJson_IsDeterministic()
    {
        EquipmentCatalog catalog = ShippedEquipment();

        CombatantStats first  = LoadoutResolver.Resolve(RecruitLoadout(), catalog);
        CombatantStats second = LoadoutResolver.Resolve(RecruitLoadout(), catalog);

        Assert.Equal(first, second);
    }

    // ── Resolved stats alter the authoritative outcome ────────────────────────────

    [Fact]
    public void LoadoutResolvedAttacker_AltersAuthoritativeOutcome_VsDefault()
    {
        CombatantStats resolved = LoadoutResolver.Resolve(RecruitLoadout(), ShippedEquipment());

        MatchResult equipped = RunFeetShotMatch(c0Stats: resolved);
        MatchResult plain    = RunFeetShotMatch(c0Stats: CombatantStats.Default);

        // Both eventually wipe the lone enemy, but the +2500 Attack from the loadout makes the
        // resolved attacker land its kill in strictly fewer turns — the gear changed the result.
        Assert.Equal(MatchOutcome.Team0Wins, equipped.Outcome);
        Assert.Equal(MatchOutcome.Team0Wins, plain.Outcome);
        Assert.True(equipped.TurnCount < plain.TurnCount,
            $"expected equipped to win faster: equipped {equipped.TurnCount} vs plain {plain.TurnCount}");
    }

    // ── A loadout-driven match is deterministic ───────────────────────────────────

    [Fact]
    public void LoadoutDrivenMatch_SameSeed_ProducesIdenticalLog()
    {
        CombatantStats resolved = LoadoutResolver.Resolve(RecruitLoadout(), ShippedEquipment());

        IReadOnlyList<TurnEvent> first  = RunFeetShotMatch(c0Stats: resolved).Log;
        IReadOnlyList<TurnEvent> second = RunFeetShotMatch(c0Stats: resolved).Log;

        Assert.Equal(first.Count, second.Count);
        for (int i = 0; i < first.Count; i++)
            Assert.Equal(first[i], second[i]);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────

    private static MatchResult RunFeetShotMatch(CombatantStats c0Stats)
    {
        // c1 has enough HP that a single Default shot cannot one-shot it, so the Default and the
        // equipped runs differ by kill-turn rather than degenerating to the same one-turn match.
        var combatants = new[]
        {
            new Combatant(new Vec2D(0.0, 0.0), 100.0,   c0Stats),
            new Combatant(new Vec2D(1.0, 0.0), 5000.0, CombatantStats.Default),
        };
        var ctrl = new MatchController(
            ProjectileSim, combatants, new[] { 0, 1 }, IsolatedBlast, Ground, NoWind, Seed);

        var log = new List<TurnEvent>();
        while (!ctrl.IsOver)
            log.Add(ctrl.ResolveTurn(ctrl.CurrentActorIndex == 0 ? FeetShot : NoopShot));

        return ctrl.Result;
    }
}
