using System;

namespace Sim.Content;

/// <summary>
/// Thrown when ball/shell content data is malformed, invalid, or a requested
/// definition is absent. Messages name the offending id, field, or array index so
/// designers can locate the problem in <c>/content/data/balls.json</c>.
/// </summary>
public sealed class BallDataException : Exception
{
    /// <summary>Creates the exception with a descriptive message.</summary>
    public BallDataException(string message) : base(message)
    {
    }

    /// <summary>Creates the exception wrapping the underlying parse failure.</summary>
    public BallDataException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
