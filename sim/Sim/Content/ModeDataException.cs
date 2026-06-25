using System;

namespace Sim.Content;

/// <summary>
/// Thrown when game-mode content data is malformed, invalid, or a requested mode is absent.
/// Messages name the offending id, field, or array index so designers can locate the problem in
/// <c>/content/data/modes.json</c>.
/// </summary>
public sealed class ModeDataException : Exception
{
    /// <summary>Creates the exception with a descriptive message.</summary>
    public ModeDataException(string message) : base(message)
    {
    }

    /// <summary>Creates the exception wrapping the underlying parse failure.</summary>
    public ModeDataException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
