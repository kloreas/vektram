using System;

namespace Sim.Core;

/// <summary>
/// Canonical simulation constants. All physics code reads from here and never hardcodes.
/// Changing a value here propagates uniformly to server and client.
/// </summary>
public static class SimConstants
{
    /// <summary>Default gravitational acceleration, in m/s².</summary>
    public const double DefaultGravity  = 9.8;

    /// <summary>Duration of one physics tick, in seconds (60 Hz).</summary>
    public const double FixedTimestep   = 1.0 / 60.0;

    /// <summary>
    /// Hard cap on simulated flight time, in seconds.
    /// Guards against infinite loops on degenerate inputs (e.g. gravity = 0).
    /// </summary>
    public const double MaxShotDuration = 30.0;

    /// <summary>Multiply degrees by this to convert to radians.</summary>
    public const double DegToRad        = Math.PI / 180.0;
}
