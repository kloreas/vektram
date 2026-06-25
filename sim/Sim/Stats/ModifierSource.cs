namespace Sim.Stats;

/// <summary>
/// The provenance of a <see cref="StatModifier"/>: its broad <see cref="Type"/> and a stable
/// <see cref="SourceId"/> (e.g. the equipment piece id). Carried on every modifier so the
/// stack stays auditable and a single source can be filtered out and re-assembled.
/// </summary>
/// <param name="Type">What kind of thing produced the modifier.</param>
/// <param name="SourceId">Stable identifier of the producing thing (piece/rune/buff id).</param>
public readonly record struct ModifierSource(
    ModifierSourceType Type,
    string             SourceId);
