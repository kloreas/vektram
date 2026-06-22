using Sim.Content;

namespace Sim.Match;

/// <summary>
/// The data-driven combat configuration for a match: damage tuning, the room/mode damage
/// scalar, and the element advantage table. Supplied by the host (which loads it from
/// <c>/content</c>); the match engine consumes it without knowing how it was assembled.
/// </summary>
/// <remarks>
/// The <see cref="ModeMultiplier"/> seam lets room/mode variety (system #5) scale damage as
/// data rather than by switching formula shapes. <see cref="Elements"/> may be
/// <see langword="null"/>, in which case every element advantage is the neutral 1.0.
/// </remarks>
/// <param name="Tuning">Damage formula divisors, caps, and curves.</param>
/// <param name="ModeMultiplier">Room/mode damage scalar (1.0 = no adjustment).</param>
/// <param name="Elements">Element advantage table, or <see langword="null"/> for all-neutral.</param>
public readonly record struct CombatRules(
    CombatTuning  Tuning,
    double        ModeMultiplier,
    ElementTable? Elements)
{
    /// <summary>
    /// Engine fallback: <see cref="CombatTuning.Default"/>, no mode adjustment, no element
    /// advantages. Used when a caller does not supply rules.
    /// </summary>
    public static CombatRules Default { get; } = new(CombatTuning.Default, 1.0, null);

    /// <summary>Advantage multiplier for the given element pair, or 1.0 when no table is set.</summary>
    public double ElementAdvantage(Element attacker, Element defender) =>
        Elements?.Advantage(attacker, defender) ?? 1.0;
}
