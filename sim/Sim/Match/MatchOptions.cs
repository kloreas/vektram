namespace Sim.Match;

/// <summary>
/// Configurable rules for a match instance.
/// </summary>
/// <param name="FriendlyFire">
/// When <see langword="true"/>, the blast damages allies (same-team combatants other than the shooter)
/// within its radius. When <see langword="false"/>, allies in the radius take no damage.
/// </param>
/// <param name="SelfDamage">
/// When <see langword="true"/>, the shooter is vulnerable to their own blast.
/// When <see langword="false"/>, the shooter is immune to their own blast regardless of
/// <see cref="FriendlyFire"/>. Self is treated as distinct from ally for this check.
/// </param>
public readonly record struct MatchOptions(bool FriendlyFire, bool SelfDamage)
{
    /// <summary>
    /// Both flags <see langword="true"/> — every combatant in the blast radius takes damage.
    /// Preserves the original 1v1 duel behaviour.
    /// </summary>
    public static MatchOptions Default { get; } = new(FriendlyFire: true, SelfDamage: true);
}
