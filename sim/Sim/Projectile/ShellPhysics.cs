namespace Sim.Projectile;

/// <summary>
/// Per-shell physics character: dimensionless multipliers applied to the round's
/// <see cref="WorldEnvironment"/> to produce a per-shot <em>constant</em> acceleration.
/// </summary>
/// <remarks>
/// <para>
/// These knobs capture the spirit of DDTank's shell character (ADR-0005) without
/// copying its legacy 25 Hz Euler integrator. In DDTank the gravity force divides
/// by mass and cancels, so the real per-shell levers reduce to a gravity scale
/// (<c>Weight</c>) and a wind scale (<c>Wind</c> mult / <c>Mass</c>). Both keep the
/// acceleration constant, so Velocity Verlet stays analytically exact (ADR-0002).
/// </para>
/// <para>
/// KNOWN DEFERRED ITEM: velocity-dependent air-resistance drag (DDTank
/// <c>DragIndex</c>) is intentionally not modeled. It would make acceleration
/// non-constant and void ADR-0002's exactness guarantee; adding it requires its own
/// ADR-0002 amendment and integrator re-validation.
/// </para>
/// </remarks>
/// <param name="GravityScale">
/// Multiplier on the round's gravity. &gt;1 falls faster (heavy), &lt;1 floats. Must be &gt; 0.
/// </param>
/// <param name="WindSensitivity">
/// Multiplier on the round's horizontal wind acceleration. Higher drifts more. Must be ≥ 0.
/// </param>
public readonly record struct ShellPhysics(double GravityScale, double WindSensitivity)
{
    /// <summary>
    /// Neutral physics (<c>GravityScale = 1</c>, <c>WindSensitivity = 1</c>): the shell
    /// is affected by the environment exactly as authored, with no per-shell modulation.
    /// A shot using <see cref="Neutral"/> produces a trajectory bit-identical to the
    /// pre-shell simulator path.
    /// </summary>
    public static ShellPhysics Neutral { get; } = new(1.0, 1.0);
}
