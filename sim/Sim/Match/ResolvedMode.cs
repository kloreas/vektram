namespace Sim.Match;

/// <summary>
/// The three engine primitives a single <see cref="Sim.Content.ModeDefinition"/> resolves to,
/// bound together as one value so they can never be mismatched. Produced only by
/// <see cref="ModeSetup.Resolve"/>.
/// </summary>
/// <remarks>
/// Before this type the resolver returned a loose tuple that callers immediately split into three
/// independently-passed structs, so nothing stopped a caller pairing one mode's
/// <see cref="MatchOptions"/> with another mode's <see cref="MatchModeRules"/>. Bundling them at
/// the one production site removes that hazard. As a positional record it still
/// <c>Deconstruct</c>s into the three members, so existing call sites that destructure the old
/// tuple compile unchanged.
/// </remarks>
/// <param name="Options">Friendly-fire / self-damage rules (<see cref="MatchOptions"/>).</param>
/// <param name="Rules">Damage tuning + mode multiplier + element table (<see cref="CombatRules"/>).</param>
/// <param name="ModeRules">Win condition, turn cap, turn-order policy (<see cref="MatchModeRules"/>).</param>
public readonly record struct ResolvedMode(
    MatchOptions   Options,
    CombatRules    Rules,
    MatchModeRules ModeRules);
