using Sim.Content;
using Sim.Core;

namespace Sim.Match;

/// <summary>
/// The engine-facing, provenance-free slice of a mode: how the match is scheduled and decided.
/// Parallel to <see cref="MatchOptions"/> (friendly-fire/self-damage) and <see cref="CombatRules"/>
/// (damage/mode/element) — the match engine consumes it without knowing it came from a
/// <see cref="ModeDefinition"/>. Produced by the pure <see cref="ModeSetup"/> mapper.
/// </summary>
/// <param name="WinCondition">The data-driven win condition evaluated each turn.</param>
/// <param name="MaxTurns">
/// Per-mode turn cap. Must not exceed <see cref="SimConstants.MaxTurnsPerMatch"/>, the engine
/// safety ceiling (the loader enforces this on authored data).
/// </param>
/// <param name="TurnOrder">Which turn-order policy this match uses.</param>
public readonly record struct MatchModeRules(
    WinConditionDefinition WinCondition,
    int                    MaxTurns,
    TurnOrderPolicyKind    TurnOrder)
{
    /// <summary>
    /// Reproduces pre-#5 behavior bit-for-bit: last-team-standing, the 200-turn safety cap,
    /// round-robin order. Used when a controller is constructed without explicit mode rules, and
    /// the value <c>ModeDefinition.Default</c> resolves to.
    /// </summary>
    public static MatchModeRules Default { get; } = new(
        WinConditionDefinition.LastTeamStanding,
        SimConstants.MaxTurnsPerMatch,
        TurnOrderPolicyKind.RoundRobin);
}
