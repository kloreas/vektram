using System.Collections.Generic;
using Sim.Projectile;
using Sim.Terrain;

namespace Sim.Match;

/// <summary>
/// World snapshot presented to an <see cref="IAgent"/> at the start of its turn.
/// All combatant views reflect live state after all prior damage has been applied.
/// </summary>
/// <param name="Self">The combatant whose turn it is.</param>
/// <param name="Allies">Living teammates on the same team as <see cref="Self"/>, excluding <see cref="Self"/>.</param>
/// <param name="Enemies">Living combatants on all opposing teams.</param>
/// <param name="Terrain">Read-only view of the ground surface.</param>
/// <param name="Environment">Gravity and wind for this match.</param>
/// <param name="TurnNumber">Zero-based index of the current turn.</param>
public readonly record struct MatchState(
    Combatant Self,
    IReadOnlyList<Combatant> Allies,
    IReadOnlyList<Combatant> Enemies,
    ITerrainQuery Terrain,
    WorldEnvironment Environment,
    int TurnNumber);
