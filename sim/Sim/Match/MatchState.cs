using Sim.Projectile;
using Sim.Terrain;

namespace Sim.Match;

/// <summary>
/// World snapshot presented to an <see cref="IAgent"/> at the start of its turn.
/// <see cref="Self"/> and <see cref="Opponent"/> are always from the acting agent's perspective.
/// </summary>
/// <param name="Self">The combatant whose turn it is.</param>
/// <param name="Opponent">The other combatant.</param>
/// <param name="Terrain">Read-only view of the ground surface.</param>
/// <param name="Environment">Gravity and wind for this match.</param>
/// <param name="TurnNumber">Zero-based index of the current turn.</param>
public readonly record struct MatchState(
    Combatant Self,
    Combatant Opponent,
    ITerrainQuery Terrain,
    WorldEnvironment Environment,
    int TurnNumber);
