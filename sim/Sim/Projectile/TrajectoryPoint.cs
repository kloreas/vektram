using Sim.Core;

namespace Sim.Projectile;

/// <summary>
/// Projectile state at one simulation tick.
/// <see cref="Time"/> always equals the tick index multiplied by
/// <see cref="SimConstants.FixedTimestep"/>, computed directly to avoid
/// floating-point drift in the time axis.
/// </summary>
public readonly record struct TrajectoryPoint(
    Vec2D  Position,
    Vec2D  Velocity,
    double Time
);
