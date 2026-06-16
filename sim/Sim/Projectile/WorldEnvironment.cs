using Sim.Core;

namespace Sim.Projectile;

/// <summary>Environmental constants that govern projectile physics for a round.</summary>
/// <param name="Gravity">
/// Gravitational acceleration in m/s², applied as negative Y acceleration. Must be ≥ 0.
/// </param>
/// <param name="WindX">
/// Constant horizontal wind acceleration in m/s². Positive = rightward push, negative = leftward.
/// </param>
public readonly record struct WorldEnvironment(double Gravity, double WindX)
{
    /// <summary>Standard gravity (<see cref="SimConstants.DefaultGravity"/>), no wind.</summary>
    public static WorldEnvironment Default { get; } = new(SimConstants.DefaultGravity, 0.0);
}
