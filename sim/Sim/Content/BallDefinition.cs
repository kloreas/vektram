using Sim.Projectile;

namespace Sim.Content;

/// <summary>
/// Immutable, data-driven definition of one shell/ball type.
/// Canonical values are authored in <c>/content/data/balls.json</c>; never hardcode
/// tuning numbers in C#.
/// </summary>
/// <remarks>
/// <see cref="BallDefinition"/> is the richer, content-backed superset of
/// <c>Sim.Match.Weapon</c>'s physics-relevant fields (<c>ProjectileSpeed</c>,
/// <c>BaseDamage</c>, <c>BlastRadius</c>) plus <see cref="Physics"/>, <see cref="Type"/>,
/// and <see cref="Id"/>. FUTURE SEAM: a later task will source <c>Weapon</c> from a
/// <see cref="BallDefinition"/> (or replace it); the match layer is intentionally left
/// untouched for now.
/// </remarks>
/// <param name="Id">Stable lookup key (DDTank <c>BallInfo.ID</c>). Unique within a catalog.</param>
/// <param name="DisplayName">Display-only label. Has no stat effect.</param>
/// <param name="Type">Shell behaviour tag (DDTank <c>BombType</c>).</param>
/// <param name="Physics">Per-shell gravity/wind character consumed by the simulator.</param>
/// <param name="BlastRadius">Damage-area radius in metres (DDTank <c>Radii</c>).</param>
/// <param name="BaseDamage">Max damage at blast centre before modifiers, in HP (DDTank <c>Power</c>).</param>
/// <param name="ProjectileSpeed">Default launch speed in m/s.</param>
public readonly record struct BallDefinition(
    string       Id,
    string       DisplayName,
    ShellType    Type,
    ShellPhysics Physics,
    double       BlastRadius,
    double       BaseDamage,
    double       ProjectileSpeed);
