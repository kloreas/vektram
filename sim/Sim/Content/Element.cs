namespace Sim.Content;

/// <summary>
/// Damage element, mapped from DDTank's emblem element set.
/// </summary>
/// <remarks>
/// Elements feed the damage formula's element layer: a combatant's
/// <c>ElementPower</c> versus a defender's <c>ElementResist</c>, scaled by the
/// data-driven advantage multiplier in <see cref="ElementTable"/>. <see cref="None"/>
/// is the neutral default (no element interaction).
/// </remarks>
public enum Element
{
    /// <summary>No element; neutral. Advantage against any element is 1.0.</summary>
    None,

    /// <summary>Fire.</summary>
    Fire,

    /// <summary>Water.</summary>
    Water,

    /// <summary>Wind.</summary>
    Wind,

    /// <summary>Land.</summary>
    Land,

    /// <summary>Light.</summary>
    Light,

    /// <summary>Dark.</summary>
    Dark,
}
