using Sim.Content;

namespace Sim.Match;

/// <summary>
/// Pure mapper that splits an authored <see cref="ModeDefinition"/> into the match engine's
/// existing provenance-free primitives: <see cref="MatchOptions"/> (friendly-fire/self-damage),
/// <see cref="CombatRules"/> (folding in the mode's damage scalar over the shared tuning/elements),
/// and <see cref="MatchModeRules"/> (win condition / turn cap / turn order).
/// </summary>
/// <remarks>
/// This lives in <c>/sim</c> (not the host) so the server and the Unity client derive identical
/// match configuration from the same mode data — the engine keeps consuming primitives it does not
/// have to understand the origin of (ADR-0006, Decision 1).
/// </remarks>
public static class ModeSetup
{
    /// <summary>Maps a mode's blast rules to <see cref="MatchOptions"/>.</summary>
    public static MatchOptions ToMatchOptions(in ModeDefinition mode) =>
        new(mode.FriendlyFire, mode.SelfDamage);

    /// <summary>
    /// Builds the <see cref="CombatRules"/> for a mode: the shared <paramref name="tuning"/> and
    /// <paramref name="elements"/> with the mode's <see cref="ModeDefinition.ModeMultiplier"/>
    /// folded into the existing <see cref="CombatRules.ModeMultiplier"/> seam.
    /// </summary>
    public static CombatRules ToCombatRules(in ModeDefinition mode, CombatTuning tuning, ElementTable? elements) =>
        new(tuning, mode.ModeMultiplier, elements);

    /// <summary>Maps a mode's scheduling/win rules to <see cref="MatchModeRules"/>.</summary>
    public static MatchModeRules ToModeRules(in ModeDefinition mode) =>
        new(mode.WinCondition, mode.MaxTurns, mode.TurnOrder);

    /// <summary>
    /// Resolves a mode into the three engine primitives in one call. The host hands
    /// <paramref name="tuning"/> and <paramref name="elements"/> (loaded from <c>/content</c>);
    /// the mode supplies everything else.
    /// </summary>
    public static (MatchOptions Options, CombatRules Rules, MatchModeRules ModeRules) Resolve(
        in ModeDefinition mode, CombatTuning tuning, ElementTable? elements) =>
        (ToMatchOptions(mode), ToCombatRules(mode, tuning, elements), ToModeRules(mode));
}
