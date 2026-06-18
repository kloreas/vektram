namespace Sim.Match;

/// <summary>
/// The action an <see cref="IAgent"/> submits for a single turn.
/// </summary>
/// <param name="AngleDegrees">
/// Aim angle in degrees, counter-clockwise from +X axis. 0° = right, 90° = straight up.
/// </param>
/// <param name="Speed">
/// Actual launch speed in m/s. Kept separate from <see cref="Weapon.ProjectileSpeed"/>
/// to allow charge / power mechanics without an interface change.
/// </param>
/// <param name="Weapon">Weapon used this turn (determines blast radius and base damage).</param>
public readonly record struct FireAction(
    double AngleDegrees,
    double Speed,
    Weapon Weapon);
