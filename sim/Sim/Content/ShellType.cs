namespace Sim.Content;

/// <summary>
/// Shell behaviour tag, mapped from DDTank's <c>BombType</c>.
/// </summary>
/// <remarks>
/// Starter set only. Special-behaviour shells (frozen, cure, fly, etc.) are added
/// alongside the systems that implement those behaviours.
/// </remarks>
public enum ShellType
{
    /// <summary>Baseline shell with neutral handling.</summary>
    Standard,

    /// <summary>Heavier shell: falls faster, resists wind.</summary>
    Heavy,

    /// <summary>Lighter shell: floats, drifts more in wind.</summary>
    Light,
}
