namespace Sim.Terrain;

/// <summary>
/// Read-only view of ground surface height at any horizontal position.
/// </summary>
/// <remarks>
/// <para>
/// <b>Modeling boundary — heightfield only.</b>
/// <see cref="GetHeight"/> models terrain as a single-valued function of
/// horizontal position: for every x there is exactly one ground height y.
/// This representation cannot express overhangs, caves, arches, or vertical
/// walls. That is the correct tradeoff for a top-down-view artillery genre
/// where the ground is always a surface, never a volume. If a future map
/// feature requires true volumetric geometry, a separate abstraction will
/// be needed.
/// </para>
/// <para>
/// Implementations must be pure and deterministic: <see cref="GetHeight"/>
/// must return the same value for the same <paramref name="x"/> on every
/// call within a simulation run. No I/O, no mutable state, no randomness.
/// </para>
/// </remarks>
public interface ITerrainQuery
{
    /// <summary>
    /// Returns the ground surface height in metres at horizontal world
    /// position <paramref name="x"/>. Defined for all finite values of
    /// <paramref name="x"/>; implementations must not throw for any input.
    /// </summary>
    double GetHeight(double x);
}
