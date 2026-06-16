using Sim.Core;

namespace Sim.Projectile;

/// <summary>Complete inputs for a single projectile shot.</summary>
/// <param name="Origin">
/// Launch position, in metres. <c>Y</c> must be ≥ 0 (at or above ground).
/// </param>
/// <param name="AngleDegrees">
/// Aim angle in degrees, counter-clockwise from +X axis.
/// 0° = rightward, 90° = straight up, 180° = leftward.
/// </param>
/// <param name="Speed">Initial projectile speed, in m/s. Must be ≥ 0.</param>
/// <param name="Seed">
/// Reserved for future RNG-driven mechanics (scatter, item effects).
/// Pass 0 until those mechanics are implemented.
/// </param>
public readonly record struct FireCommand(
    Vec2D  Origin,
    double AngleDegrees,
    double Speed,
    uint   Seed
);
