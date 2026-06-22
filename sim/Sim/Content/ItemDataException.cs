using System;

namespace Sim.Content;

/// <summary>
/// Thrown when item content data is malformed, invalid, a requested definition is absent,
/// or a referenced ball id does not resolve. Messages name the offending id, field, or
/// index so designers can locate the problem in <c>/content/data/items.json</c>.
/// </summary>
public sealed class ItemDataException : Exception
{
    /// <summary>Creates the exception with a descriptive message.</summary>
    public ItemDataException(string message) : base(message)
    {
    }

    /// <summary>Creates the exception wrapping the underlying parse failure.</summary>
    public ItemDataException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
