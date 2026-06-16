namespace Sim.Terrain;

/// <summary>
/// Terrain with a constant height at all horizontal positions.
/// </summary>
public sealed class FlatTerrain : ITerrainQuery
{
    private readonly double _height;

    /// <summary>Creates a flat terrain at the specified constant <paramref name="height"/> (metres).</summary>
    public FlatTerrain(double height) => _height = height;

    /// <summary>
    /// Flat ground at y = 0. Reproduces the baseline simulation behaviour
    /// and is the default terrain for shots with no elevation data.
    /// </summary>
    public static FlatTerrain Ground { get; } = new FlatTerrain(0.0);

    /// <inheritdoc/>
    public double GetHeight(double x) => _height;
}
