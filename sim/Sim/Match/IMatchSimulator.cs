using Sim.Projectile;
using Sim.Terrain;

namespace Sim.Match;

/// <summary>
/// Runs a complete 1v1 match from initial state to a terminal outcome.
/// </summary>
public interface IMatchSimulator
{
    /// <summary>
    /// Runs a complete match deterministically.
    /// Identical inputs (including <paramref name="seed"/>) always produce an identical <see cref="MatchResult"/>.
    /// </summary>
    /// <param name="combatant0">Initial state of player 0. <c>Position.Y</c> must be on the terrain surface.</param>
    /// <param name="combatant1">Initial state of player 1. <c>Position.Y</c> must be on the terrain surface.</param>
    /// <param name="agent0">Decision-maker for combatant 0.</param>
    /// <param name="agent1">Decision-maker for combatant 1.</param>
    /// <param name="terrain">Ground surface for projectile collision.</param>
    /// <param name="environment">Gravity and wind constants for the match.</param>
    /// <param name="seed">Forwarded to <see cref="Sim.Projectile.FireCommand"/> for future RNG mechanics.</param>
    MatchResult Run(
        Combatant combatant0,
        Combatant combatant1,
        IAgent agent0,
        IAgent agent1,
        ITerrainQuery terrain,
        WorldEnvironment environment,
        uint seed);
}
