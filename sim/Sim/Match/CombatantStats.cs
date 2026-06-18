namespace Sim.Match;

/// <summary>
/// Minimal stat block for a combatant. The full equipment/stat system is a later phase.
/// </summary>
/// <param name="MaxHp">Maximum hit points. Must be &gt; 0.</param>
/// <param name="DamageModifier">
/// Attacker-side multiplier applied to raw damage output before defense is subtracted.
/// 1.0 = no bonus. Scales all damage the combatant deals, including self-damage.
/// </param>
/// <param name="Defense">
/// Flat hit-point reduction subtracted from every incoming raw damage value.
/// Final damage is clamped to ≥ 0; defense cannot create healing.
/// </param>
public readonly record struct CombatantStats(
    double MaxHp,
    double DamageModifier,
    double Defense)
{
    /// <summary>100 HP, 1.0× modifier, 0 defense.</summary>
    public static CombatantStats Default { get; } = new(100.0, 1.0, 0.0);
}
