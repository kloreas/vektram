using Sim.Core;

namespace Sim.Projectile;

/// <summary>
/// Complete result of a simulated shot.
/// </summary>
/// <remarks>
/// <see cref="Trajectory"/> contains every physics tick from launch (index 0, t = 0)
/// through and including the first tick at or below the terrain surface, spaced at
/// <see cref="SimConstants.FixedTimestep"/> intervals.
/// <para>
/// <see cref="ImpactPoint"/> and <see cref="ImpactTime"/> are sub-step values obtained
/// by linearly interpolating the terrain-surface crossing between the last above-terrain
/// tick and the first at-or-below-terrain tick. The interpolated Y value lies on the
/// terrain surface to within floating-point precision.
/// </para>
/// </remarks>
public readonly struct ShotResult
{
    /// <summary>
    /// All physics ticks from launch through impact (inclusive).
    /// <c>Trajectory[i].Time == i × SimConstants.FixedTimestep</c> holds exactly.
    /// </summary>
    public TrajectoryPoint[] Trajectory  { get; }

    /// <summary>Interpolated world position of the terrain-surface crossing, in metres.</summary>
    public Vec2D             ImpactPoint { get; }

    /// <summary>Interpolated time of the terrain-surface crossing, in seconds since launch.</summary>
    public double            ImpactTime  { get; }

    /// <inheritdoc cref="ShotResult"/>
    public ShotResult(TrajectoryPoint[] trajectory, Vec2D impactPoint, double impactTime)
    {
        Trajectory  = trajectory;
        ImpactPoint = impactPoint;
        ImpactTime  = impactTime;
    }
}
