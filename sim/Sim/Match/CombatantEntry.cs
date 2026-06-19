namespace Sim.Match;

/// <summary>
/// A single roster slot in a match: the combatant's initial state, its team, and the agent that controls it.
/// </summary>
/// <param name="Combatant">Initial combatant state. <c>Position.Y</c> must be on the terrain surface.</param>
/// <param name="TeamId">
/// Team membership. Two teams use IDs 0 and 1; the identifier space is open for extension to
/// more teams or FFA without structural changes.
/// </param>
/// <param name="Agent">The decision-maker for this combatant.</param>
public readonly record struct CombatantEntry(Combatant Combatant, int TeamId, IAgent Agent);
