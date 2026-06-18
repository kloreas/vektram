using Sim.Core;
using Sim.Match;
using Xunit;

namespace Sim.Tests.Match;

public class DamageCalculatorTests
{
    // ── Test fixtures ─────────────────────────────────────────────────────────

    private const double BlastRadius = 10.0;
    private const double BaseDamage  = 100.0;
    private const double TestDefense = 20.0;

    private static readonly Vec2D ImpactCenter = Vec2D.Zero;

    private static readonly Vec2D AtCenter     = Vec2D.Zero;                          // distance 0
    private static readonly Vec2D AtHalfRadius = new(BlastRadius / 2.0, 0.0);        // distance 5
    private static readonly Vec2D AtEdge       = new(BlastRadius,        0.0);        // distance 10 — outside
    private static readonly Vec2D BeyondEdge   = new(BlastRadius + 1.0,  0.0);        // distance 11 — outside

    private static readonly Weapon TestWeapon = new(50.0, BaseDamage, BlastRadius);

    private static readonly CombatantStats NoMods       = CombatantStats.Default;                     // DM=1.0, Def=0
    private static readonly CombatantStats WithDefense  = new(100.0, 1.0, TestDefense);
    private static readonly CombatantStats HighDefense  = new(100.0, 1.0, BaseDamage + 50.0);         // defense > full damage
    private static readonly CombatantStats DoubleDamage = new(100.0, 2.0, 0.0);

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public void DirectHit_NoDefense_DealsFullBaseDamage()
    {
        double damage = DamageCalculator.Compute(ImpactCenter, AtCenter, TestWeapon, NoMods, NoMods);

        Assert.Equal(BaseDamage, damage);
    }

    [Fact]
    public void AtBlastRadiusEdge_DealsZeroDamage()
    {
        double damage = DamageCalculator.Compute(ImpactCenter, AtEdge, TestWeapon, NoMods, NoMods);

        Assert.Equal(0.0, damage);
    }

    [Fact]
    public void BeyondBlastRadius_DealsZeroDamage()
    {
        double damage = DamageCalculator.Compute(ImpactCenter, BeyondEdge, TestWeapon, NoMods, NoMods);

        Assert.Equal(0.0, damage);
    }

    [Fact]
    public void HalfwayToEdge_DealsHalfBaseDamage()
    {
        // distanceFactor = 1 − (5/10) = 0.5 → raw = 100 × 0.5 × 1.0 = 50
        const double ExpectedDamage = BaseDamage / 2.0;

        double damage = DamageCalculator.Compute(ImpactCenter, AtHalfRadius, TestWeapon, NoMods, NoMods);

        Assert.Equal(ExpectedDamage, damage);
    }

    [Fact]
    public void Defense_ReducesFinalDamage()
    {
        // Direct hit: raw = 100; after defense = 100 − 20 = 80
        const double ExpectedDamage = BaseDamage - TestDefense;

        double damage = DamageCalculator.Compute(ImpactCenter, AtCenter, TestWeapon, NoMods, WithDefense);

        Assert.Equal(ExpectedDamage, damage);
    }

    [Fact]
    public void HighDefense_CannotReduceBelowZero()
    {
        double damage = DamageCalculator.Compute(ImpactCenter, AtCenter, TestWeapon, NoMods, HighDefense);

        Assert.Equal(0.0, damage);
    }

    [Fact]
    public void DamageModifier_ScalesRawDamage()
    {
        // Direct hit: raw = 100 × 1.0 × 2.0 = 200
        const double ExpectedDamage = BaseDamage * 2.0;

        double damage = DamageCalculator.Compute(ImpactCenter, AtCenter, TestWeapon, DoubleDamage, NoMods);

        Assert.Equal(ExpectedDamage, damage);
    }
}
