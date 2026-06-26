using System;
using Sim.Content;
using Sim.Match;
using Xunit;

namespace Sim.Tests.Match;

/// <summary>
/// The pure mapper that splits an authored <see cref="ModeDefinition"/> into the engine's existing
/// provenance-free primitives (<see cref="MatchOptions"/> / <see cref="CombatRules"/> /
/// <see cref="MatchModeRules"/>).
/// </summary>
public class ModeSetupTests
{
    private static ModeDefinition SampleMode() => new(
        "skirmish",
        "Skirmish",
        new[] { 2, 2 },
        ModeMultiplier: 0.5,
        FriendlyFire: false,
        SelfDamage: true,
        TurnOrderPolicyKind.RoundRobin,
        MaxTurns: 60,
        new WinConditionDefinition(WinConditionKind.TurnLimitTiebreak, TiebreakMetric.TotalDamageDealt));

    [Fact]
    public void ToMatchOptions_MapsFriendlyFireAndSelfDamage()
    {
        ModeDefinition mode = SampleMode();

        MatchOptions options = ModeSetup.ToMatchOptions(mode);

        Assert.False(options.FriendlyFire);
        Assert.True(options.SelfDamage);
    }

    [Fact]
    public void ToCombatRules_FoldsModeMultiplier_KeepsTuningAndElements()
    {
        ModeDefinition mode = SampleMode();
        ElementTable elements = ElementTable.Neutral;

        CombatRules rules = ModeSetup.ToCombatRules(mode, CombatTuning.Default, elements);

        Assert.Equal(0.5, rules.ModeMultiplier);
        Assert.Equal(CombatTuning.Default, rules.Tuning);
        Assert.Same(elements, rules.Elements);
    }

    [Fact]
    public void ToModeRules_MapsWinConditionMaxTurnsAndTurnOrder()
    {
        ModeDefinition mode = SampleMode();

        MatchModeRules modeRules = ModeSetup.ToModeRules(mode);

        Assert.Equal(WinConditionKind.TurnLimitTiebreak, modeRules.WinCondition.Kind);
        Assert.Equal(TiebreakMetric.TotalDamageDealt, modeRules.WinCondition.Tiebreak);
        Assert.Equal(60, modeRules.MaxTurns);
        Assert.Equal(TurnOrderPolicyKind.RoundRobin, modeRules.TurnOrder);
    }

    [Fact]
    public void Resolve_ReturnsAllThreePrimitives()
    {
        ModeDefinition mode = SampleMode();

        (MatchOptions options, CombatRules rules, MatchModeRules modeRules) =
            ModeSetup.Resolve(mode, CombatTuning.Default, null);

        Assert.False(options.FriendlyFire);
        Assert.Equal(0.5, rules.ModeMultiplier);
        Assert.Equal(60, modeRules.MaxTurns);
    }

    [Fact]
    public void Resolve_ReturnsBoundResolvedMode_CarryingTheConsistentTriple()
    {
        ModeDefinition mode = SampleMode();
        ElementTable elements = ElementTable.Neutral;

        ResolvedMode resolved = ModeSetup.Resolve(mode, CombatTuning.Default, elements);

        // The bound value carries exactly the per-mapper primitives — the same mode in, so the
        // three pieces are guaranteed consistent (they can no longer be sourced from other modes).
        Assert.Equal(ModeSetup.ToMatchOptions(mode), resolved.Options);
        Assert.Equal(ModeSetup.ToCombatRules(mode, CombatTuning.Default, elements), resolved.Rules);
        Assert.Equal(ModeSetup.ToModeRules(mode), resolved.ModeRules);

        // And it still destructures into the legacy (options, rules, modeRules) shape.
        (MatchOptions options, CombatRules rules, MatchModeRules modeRules) = resolved;
        Assert.Equal(resolved.Options, options);
        Assert.Equal(resolved.Rules, rules);
        Assert.Equal(resolved.ModeRules, modeRules);
    }

    [Fact]
    public void DefaultMode_ResolvesToEngineDefaults()
    {
        // The default mode must map exactly onto the engine's pre-#5 primitives so the null path
        // and the explicit-default path are interchangeable.
        ModeDefinition mode = ModeDefinition.Default;

        (MatchOptions options, _, MatchModeRules modeRules) =
            ModeSetup.Resolve(mode, CombatTuning.Default, null);

        Assert.Equal(MatchOptions.Default, options);
        Assert.Equal(MatchModeRules.Default, modeRules);
    }
}
