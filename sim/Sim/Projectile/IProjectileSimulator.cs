using Sim.Core;
using Sim.Terrain;

namespace Sim.Projectile;

/// <summary>
/// Runs a deterministic, fixed-step projectile simulation.
/// </summary>
/// <remarks>
/// <para>
/// Implementations must be pure functions: identical inputs always produce
/// identical outputs within a runtime. The sim controls the timestep
/// internally (<see cref="SimConstants.FixedTimestep"/>); callers do not
/// supply <c>dt</c> or a step count.
/// </para>
/// <para>
/// This interface is the contract shared by the authoritative match server
/// (which runs the canonical simulation) and the Unity client (which replays
/// the same simulation for visual preview). The server's result is always
/// final; the client's result is visual-only and corrected by server state.
/// </para>
/// </remarks>
public interface IProjectileSimulator
{
    /// <summary>
    /// Simulates a complete shot from launch to terrain impact.
    /// </summary>
    /// <param name="command">
    /// Aim angle, launch speed, origin, and reserved RNG seed.
    /// </param>
    /// <param name="environment">
    /// Gravity and wind constants for this round.
    /// </param>
    /// <param name="terrain">
    /// Ground surface to test for impact. Use <see cref="FlatTerrain.Ground"/>
    /// for a flat y = 0 baseline. Queried once per tick during flight and
    /// twice during impact interpolation; must be pure and inexpensive.
    /// </param>
    /// <returns>
    /// Full trajectory from t = 0 through the impact tick, plus the
    /// sub-step interpolated terrain-crossing point and time.
    /// </returns>
    ShotResult Simulate(FireCommand command, WorldEnvironment environment, ITerrainQuery terrain);

    /// <summary>
    /// Simulates a complete shot using per-shell physics. The shell's
    /// <see cref="ShellPhysics.GravityScale"/> and <see cref="ShellPhysics.WindSensitivity"/>
    /// modulate the environment into a per-shot constant acceleration, so Velocity Verlet
    /// remains analytically exact (ADR-0002).
    /// </summary>
    /// <param name="command">Aim angle, launch speed, origin, and reserved RNG seed.</param>
    /// <param name="environment">Gravity and wind constants for this round.</param>
    /// <param name="terrain">Ground surface to test for impact.</param>
    /// <param name="physics">
    /// Per-shell gravity/wind character. <see cref="ShellPhysics.Neutral"/> produces a
    /// trajectory bit-identical to the three-argument overload.
    /// </param>
    /// <returns>
    /// Full trajectory from t = 0 through the impact tick, plus the
    /// sub-step interpolated terrain-crossing point and time.
    /// </returns>
    ShotResult Simulate(FireCommand command, WorldEnvironment environment, ITerrainQuery terrain, ShellPhysics physics);
}
