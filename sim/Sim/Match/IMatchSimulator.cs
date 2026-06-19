using System.Collections.Generic;
using Sim.Projectile;
using Sim.Terrain;

namespace Sim.Match;

/// <summary>
/// Runs a complete team-based match from initial roster to a terminal outcome.
/// </summary>
public interface IMatchSimulator
{
    /// <summary>
    /// Runs a complete match deterministically.
    /// Identical inputs (including <paramref name="seed"/>) always produce an identical <see cref="MatchResult"/>.
    /// </summary>
    /// <param name="combatants">
    /// Full match roster. Each entry carries the combatant's initial state, its team id, and its
    /// controlling agent. Must contain at least two entries on at least two distinct teams.
    /// A 1v1 match is two entries with team ids 0 and 1.
    /// </param>
    /// <param name="options">Match rules (friendly fire, self-damage).</param>
    /// <param name="terrain">Ground surface for projectile collision.</param>
    /// <param name="environment">Gravity and wind constants for the match.</param>
    /// <param name="seed">Forwarded to <see cref="Sim.Projectile.FireCommand"/> for future RNG mechanics.</param>
    MatchResult Run(
        IReadOnlyList<CombatantEntry> combatants,
        MatchOptions options,
        ITerrainQuery terrain,
        WorldEnvironment environment,
        uint seed);
}
