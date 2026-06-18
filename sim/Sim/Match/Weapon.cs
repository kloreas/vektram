namespace Sim.Match;

/// <summary>
/// Data describing a single weapon type. Pure value type — no logic, no state.
/// Canonical values are authored in <c>/content</c>; never hardcode tuning numbers in C#.
/// </summary>
/// <param name="ProjectileSpeed">Launch speed of the projectile in m/s.</param>
/// <param name="BaseDamage">Maximum damage at the blast centre before modifiers, in HP.</param>
/// <param name="BlastRadius">
/// Radius of the damage area in metres.
/// Combatants at or beyond this distance take zero damage.
/// </param>
public readonly record struct Weapon(
    double ProjectileSpeed,
    double BaseDamage,
    double BlastRadius);
