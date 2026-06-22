using System;

namespace Sim.Content;

/// <summary>
/// Thrown when combat content data (<c>combat.json</c> tuning or <c>elements.json</c>
/// advantage table) is malformed or invalid. Messages name the offending field, element,
/// or pair so designers can locate the problem in <c>/content/data</c>.
/// </summary>
public sealed class CombatDataException : Exception
{
    /// <summary>Creates the exception with a descriptive message.</summary>
    public CombatDataException(string message) : base(message)
    {
    }

    /// <summary>Creates the exception wrapping the underlying parse failure.</summary>
    public CombatDataException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
