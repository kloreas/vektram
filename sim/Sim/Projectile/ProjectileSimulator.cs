using System;
using System.Collections.Generic;
using Sim.Core;
using Sim.Terrain;

namespace Sim.Projectile;

/// <summary>
/// Default implementation of <see cref="IProjectileSimulator"/>.
/// Uses Velocity Verlet integration (exact for constant acceleration) at a
/// fixed <see cref="SimConstants.FixedTimestep"/> step.
/// </summary>
public sealed class ProjectileSimulator : IProjectileSimulator
{
    /// <inheritdoc/>
    public ShotResult Simulate(FireCommand command, WorldEnvironment environment, ITerrainQuery terrain) =>
        Simulate(command, environment, terrain, ShellPhysics.Neutral);

    /// <inheritdoc/>
    public ShotResult Simulate(FireCommand command, WorldEnvironment environment, ITerrainQuery terrain, ShellPhysics physics)
    {
        if (command.Origin.Y < terrain.GetHeight(command.Origin.X))
            throw new ArgumentException("Origin is below the terrain surface.", nameof(command));
        if (command.Speed < 0.0)
            throw new ArgumentException("Speed must be >= 0.", nameof(command));
        if (environment.Gravity < 0.0)
            throw new ArgumentException("Gravity must be >= 0.", nameof(environment));
        if (physics.GravityScale <= 0.0)
            throw new ArgumentException("ShellPhysics.GravityScale must be > 0.", nameof(physics));
        if (physics.WindSensitivity < 0.0)
            throw new ArgumentException("ShellPhysics.WindSensitivity must be >= 0.", nameof(physics));

        double angleRad = command.AngleDegrees * SimConstants.DegToRad;
        Vec2D  velocity = new Vec2D(
            command.Speed * Math.Cos(angleRad),
            command.Speed * Math.Sin(angleRad));
        Vec2D  position = command.Origin;
        Vec2D  accel    = new Vec2D(
            environment.WindX * physics.WindSensitivity,
            -(environment.Gravity * physics.GravityScale));

        const double dt       = SimConstants.FixedTimestep;
        double       halfDtSq = 0.5 * dt * dt;
        int          maxTicks = (int)(SimConstants.MaxShotDuration / dt) + 1;

        var trajectory = new List<TrajectoryPoint>(512);
        trajectory.Add(new TrajectoryPoint(position, velocity, 0.0));

        for (int tick = 0; tick < maxTicks; tick++)
        {
            // Velocity Verlet: exact for constant acceleration (gravity + wind are both constant).
            // x_new = x + v·dt + a·dt²/2 ; v_new = v + a·dt
            position = position + velocity * dt + accel * halfDtSq;
            velocity = velocity + accel * dt;

            // Time computed by multiplication, not accumulation, to prevent drift.
            double time = (tick + 1) * dt;
            trajectory.Add(new TrajectoryPoint(position, velocity, time));

            if (position.Y <= terrain.GetHeight(position.X))
                break;
        }

        var (impactPoint, impactTime) = InterpolateTerrainCrossing(trajectory, terrain);
        return new ShotResult(trajectory.ToArray(), impactPoint, impactTime);
    }

    private static (Vec2D point, double time) InterpolateTerrainCrossing(
        List<TrajectoryPoint> trajectory, ITerrainQuery terrain)
    {
        var curr = trajectory[trajectory.Count - 1];

        if (trajectory.Count < 2)
            return (curr.Position, curr.Time);

        double hCurr = terrain.GetHeight(curr.Position.X);

        // Reached max-duration cap without hitting terrain.
        if (curr.Position.Y > hCurr)
            return (curr.Position, curr.Time);

        var    prev      = trajectory[trajectory.Count - 2];
        double hPrev     = terrain.GetHeight(prev.Position.X);
        double abovePrev = prev.Position.Y - hPrev;  // ≥ 0 by loop invariant
        double aboveCurr = curr.Position.Y - hCurr;  // ≤ 0 by loop invariant

        // Denominator = abovePrev − aboveCurr ≥ 0; it is zero only when both
        // points are exactly on the surface (degenerate). Guard and return curr.
        double denom = abovePrev - aboveCurr;
        if (denom == 0.0)
            return (curr.Position, curr.Time);

        // α ∈ [0, 1]: fraction of the step where the trajectory crosses terrain.
        // When abovePrev == 0 (origin on surface) α = 0, recovering the origin.
        double alpha = abovePrev / denom;
        Vec2D  point = prev.Position + (curr.Position - prev.Position) * alpha;
        double time  = prev.Time     + (curr.Time     - prev.Time)     * alpha;
        return (point, time);
    }
}
