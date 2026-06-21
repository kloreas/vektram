namespace Sim.Ai;

/// <summary>
/// Immutable tuning parameters for a <see cref="BotAgent"/>.
/// The C# presets below are defaults for prototyping; canonical values should migrate to /content
/// once the data pipeline is wired up so they can be tweaked without recompiling.
/// </summary>
/// <param name="SearchBudget">Number of angle samples evaluated per turn during the grid search.</param>
/// <param name="AimNoiseDegrees">Maximum random ± deviation (in degrees) applied to the chosen angle each turn.</param>
/// <param name="WindCompensationFactor">
/// Fraction of the actual wind acceleration fed into the search model.
/// 0.0 = the bot ignores wind entirely; 1.0 = the bot models wind exactly.
/// </param>
public readonly record struct BotDifficulty(int SearchBudget, double AimNoiseDegrees, double WindCompensationFactor)
{
    /// <summary>Coarse search, high noise, no wind awareness.</summary>
    public static BotDifficulty Easy   { get; } = new(5,  15.0, 0.0);

    /// <summary>Moderate search, some noise, partial wind awareness.</summary>
    public static BotDifficulty Medium { get; } = new(20,  5.0, 0.5);

    /// <summary>Fine search, no noise, full wind awareness.</summary>
    public static BotDifficulty Hard   { get; } = new(60,  0.0, 1.0);
}
