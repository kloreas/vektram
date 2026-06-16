using System;

namespace Sim.Core;

/// <summary>
/// Immutable double-precision 2D vector. Coordinate convention: +X right, +Y up.
/// </summary>
public readonly record struct Vec2D(double X, double Y)
{
    /// <summary>The zero vector (0, 0).</summary>
    public static Vec2D Zero => new(0.0, 0.0);

    /// <summary>Squared magnitude. Cheaper than <see cref="Length"/> when ordering is all that is needed.</summary>
    public double LengthSquared => X * X + Y * Y;

    /// <summary>Euclidean magnitude.</summary>
    public double Length => Math.Sqrt(LengthSquared);

    /// <summary>Dot product of <paramref name="a"/> and <paramref name="b"/>.</summary>
    public static double Dot(Vec2D a, Vec2D b) => a.X * b.X + a.Y * b.Y;

    public static Vec2D operator +(Vec2D a, Vec2D b)  => new(a.X + b.X, a.Y + b.Y);
    public static Vec2D operator -(Vec2D a, Vec2D b)  => new(a.X - b.X, a.Y - b.Y);
    public static Vec2D operator -(Vec2D v)            => new(-v.X, -v.Y);
    public static Vec2D operator *(Vec2D v, double s)  => new(v.X * s, v.Y * s);
    public static Vec2D operator *(double s, Vec2D v)  => new(v.X * s, v.Y * s);
    public static Vec2D operator /(Vec2D v, double s)  => new(v.X / s, v.Y / s);
}
