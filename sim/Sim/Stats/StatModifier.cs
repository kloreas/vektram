namespace Sim.Stats;

/// <summary>
/// One immutable, source-tagged adjustment to a single stat channel. Modifiers are pure
/// data; <see cref="StatAssembler"/> combines them deterministically into effective stats.
/// </summary>
/// <param name="Stat">The channel this modifier targets.</param>
/// <param name="Op">How it combines (flat / additive% / multiplicative%).</param>
/// <param name="Value">
/// The magnitude. For <see cref="ModifierOp.Flat"/> it is added directly; for the percent ops
/// it is a fraction (<c>0.10</c> = 10%).
/// </param>
/// <param name="Source">Provenance, so the modifier is auditable and removable.</param>
public readonly record struct StatModifier(
    StatKind       Stat,
    ModifierOp     Op,
    double         Value,
    ModifierSource Source);
