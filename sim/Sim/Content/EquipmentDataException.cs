using System;

namespace Sim.Content;

/// <summary>
/// Thrown when equipment content data is malformed or invalid, or a requested piece is absent.
/// Messages name the offending id, field, or index so designers can locate the problem in
/// <c>/content/data/equipment.json</c>.
/// </summary>
public sealed class EquipmentDataException : Exception
{
    /// <summary>Creates the exception with a descriptive message.</summary>
    public EquipmentDataException(string message) : base(message)
    {
    }

    /// <summary>Creates the exception wrapping the underlying parse failure.</summary>
    public EquipmentDataException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
