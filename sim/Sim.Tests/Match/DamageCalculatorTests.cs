using Sim.Content;
using Sim.Core;
using Sim.Match;
using Xunit;

namespace Sim.Tests.Match;

public class DamageCalculatorTests
{
    // ── Fixtures ──────────────────────────────────────────────────────────────

    private const double BlastRadius = 10.0;
    private const double BaseDamage  = 100.0;

    // Tuning chosen for clean arithmetic:
    //   attackFactor = AttackFloor (scale 0) = 1.0
    //   baseScale    = 1 + 100/1000 = 1.1
    //   falloff      = 1 − 1.0 × (d/r)   (linear)
    //   guard/defence divisors = 1000
    private static readonly CombatTuning CleanTuning = new(
        GuardDivisor: 1000.0,
        GuardReduceCap: 0.6,
        DefenceDivisor: 1000.0,
        DefenceReduceCap: 0.9,
        FalloffStrength: 1.0,
        AttackFloor: 1.0,
        AttackScale: 0.0,
        BaseDamageBonusDivisor: 1000.0);

    private static readonly Vec2D ImpactCenter = Vec2D.Zero;
    private static readonly Vec2D AtCenter     = Vec2D.Zero;
    private static readonly Vec2D AtHalfRadius = new(BlastRadius / 2.0, 0.0);
    private static readonly Vec2D AtEdge       = new(BlastRadius, 0.0);

    private static readonly CombatantStats Neutral = CombatantStats.Default;

    private static DamageInputs Inputs(
        Vec2D targetPosition,
        CombatantStats? attacker = null,
        CombatantStats? defender = null,
        CombatTuning? tuning = null,
        double modeMultiplier = 1.0,
        double elementAdvantage = 1.0,
        bool isCrit = false,
        bool isMiss = false)
        => new(
            ImpactCenter, targetPosition,
            BaseDamage, BlastRadius,
            attacker ?? Neutral, defender ?? Neutral,
            tuning ?? CleanTuning,
            modeMultiplier, elementAdvantage, isCrit, isMiss);

    // ── Falloff / range ───────────────────────────────────────────────────────

    [Fact]
    public void DirectHit_NeutralStats_DealsBaseTimesAttackFactorAndBaseScale()
    {
        // 100 × 1.0 (attackFactor) × 1.1 (baseScale) × 1.0 (falloff) = 110
        DamageResult r = DamageCalculator.Compute(Inputs(AtCenter));

        Assert.Equal(110.0, r.FinalDamage, 6);
        Assert.Equal(1.0, r.Falloff, 6);
    }

    [Fact]
    public void HalfwayToEdge_AppliesLinearFalloff()
    {
        // falloff = 1 − 5/10 = 0.5 → 110 × 0.5 = 55
        DamageResult r = DamageCalculator.Compute(Inputs(AtHalfRadius));

        Assert.Equal(0.5, r.Falloff, 6);
        Assert.Equal(55.0, r.FinalDamage, 6);
    }

    [Fact]
    public void AtOrBeyondRadius_DealsZero()
    {
        DamageResult r = DamageCalculator.Compute(Inputs(AtEdge));

        Assert.Equal(0.0, r.FinalDamage);
    }

    // ── Guard / defence reduction ──────────────────────────────────────────────

    [Fact]
    public void GuardReduction_MatchesFormula()
    {
        // guardReduce = 300/1000 = 0.3 → 110 × 0.7 = 77
        var defender = Neutral with { BaseGuard = 300.0 };

        DamageResult r = DamageCalculator.Compute(Inputs(AtCenter, defender: defender));

        Assert.Equal(0.3, r.GuardReduce, 6);
        Assert.Equal(77.0, r.FinalDamage, 6);
    }

    [Fact]
    public void SunderArmor_LowersGuardReduction()
    {
        // (300 − 100)/1000 = 0.2
        var attacker = Neutral with { SunderArmor = 100.0 };
        var defender = Neutral with { BaseGuard = 300.0 };

        DamageResult r = DamageCalculator.Compute(Inputs(AtCenter, attacker, defender));

        Assert.Equal(0.2, r.GuardReduce, 6);
    }

    [Fact]
    public void GuardReduction_IsCapped()
    {
        // 10000/1000 = 10 → capped at 0.6
        var defender = Neutral with { BaseGuard = 10000.0 };

        DamageResult r = DamageCalculator.Compute(Inputs(AtCenter, defender: defender));

        Assert.Equal(0.6, r.GuardReduce, 6);
    }

    [Fact]
    public void DefenceReduction_MatchesFormula()
    {
        // defenceReduce = 200/1000 = 0.2 → 110 × 0.8 = 88
        var defender = Neutral with { Defense = 200.0 };

        DamageResult r = DamageCalculator.Compute(Inputs(AtCenter, defender: defender));

        Assert.Equal(0.2, r.DefenceReduce, 6);
        Assert.Equal(88.0, r.FinalDamage, 6);
    }

    [Fact]
    public void DefenceReduction_IsCapped()
    {
        var defender = Neutral with { Defense = 1_000_000.0 };

        DamageResult r = DamageCalculator.Compute(Inputs(AtCenter, defender: defender));

        Assert.Equal(0.9, r.DefenceReduce, 6);
    }

    [Fact]
    public void HeavyReductions_NeverDriveDamageBelowZero()
    {
        var defender = Neutral with { BaseGuard = 1_000_000.0, Defense = 1_000_000.0 };

        DamageResult r = DamageCalculator.Compute(Inputs(AtCenter, defender: defender));

        Assert.True(r.FinalDamage >= 0.0);
    }

    // ── Attack scaling ─────────────────────────────────────────────────────────

    [Fact]
    public void AttackFactor_UsesFloorPlusScaleFromDefaultTuning()
    {
        // Default tuning: 0.6 + 10000 × 0.00008 = 1.4
        var attacker = Neutral with { Attack = 10000.0 };

        DamageResult r = DamageCalculator.Compute(
            Inputs(AtCenter, attacker: attacker, tuning: CombatTuning.Default));

        Assert.Equal(1.4, r.AttackFactor, 6);
    }

    // ── Element layer ──────────────────────────────────────────────────────────

    [Fact]
    public void ElementBonus_AddsPowerMinusResistTimesAdvantage()
    {
        // (50 − 20) × 1.5 = 45, added on top of 110
        var attacker = Neutral with { ElementPower = 50.0 };
        var defender = Neutral with { ElementResist = 20.0 };

        DamageResult r = DamageCalculator.Compute(
            Inputs(AtCenter, attacker, defender, elementAdvantage: 1.5));

        Assert.Equal(45.0, r.ElementBonus, 6);
        Assert.Equal(155.0, r.FinalDamage, 6);
    }

    [Fact]
    public void ElementBonus_FlooredAtZeroWhenResistExceedsPower()
    {
        var attacker = Neutral with { ElementPower = 10.0 };
        var defender = Neutral with { ElementResist = 50.0 };

        DamageResult r = DamageCalculator.Compute(
            Inputs(AtCenter, attacker, defender, elementAdvantage: 2.0));

        Assert.Equal(0.0, r.ElementBonus, 6);
    }

    // ── Crit / miss ────────────────────────────────────────────────────────────

    [Fact]
    public void Crit_AppliesCritMultiplier()
    {
        var attacker = Neutral with { CritMultiplier = 2.0 };

        DamageResult r = DamageCalculator.Compute(Inputs(AtCenter, attacker: attacker, isCrit: true));

        Assert.True(r.IsCrit);
        Assert.Equal(220.0, r.FinalDamage, 6);   // 110 × 2.0
    }

    [Fact]
    public void Miss_DealsZero()
    {
        DamageResult r = DamageCalculator.Compute(Inputs(AtCenter, isMiss: true));

        Assert.True(r.IsMiss);
        Assert.Equal(0.0, r.FinalDamage);
    }

    // ── Mode multiplier ────────────────────────────────────────────────────────

    [Fact]
    public void ModeMultiplier_ScalesDamage()
    {
        // 110 × 0.5 = 55
        DamageResult r = DamageCalculator.Compute(Inputs(AtCenter, modeMultiplier: 0.5));

        Assert.Equal(55.0, r.FinalDamage, 6);
        Assert.Equal(0.5, r.ModeMultiplier, 6);
    }

    // ── Determinism ────────────────────────────────────────────────────────────

    [Fact]
    public void SameInputs_ProduceIdenticalResultAndBreakdown()
    {
        var attacker = Neutral with { Attack = 500.0, SunderArmor = 50.0, ElementPower = 40.0, CritMultiplier = 1.7 };
        var defender = Neutral with { BaseGuard = 200.0, Defense = 150.0, ElementResist = 10.0 };
        DamageInputs inputs = Inputs(AtHalfRadius, attacker, defender, elementAdvantage: 1.25, isCrit: true);

        DamageResult a = DamageCalculator.Compute(inputs);
        DamageResult b = DamageCalculator.Compute(inputs);

        Assert.Equal(a, b);
    }
}
