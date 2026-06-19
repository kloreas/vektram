using System.Collections.Generic;

namespace Sim.Match;

/// <summary>
/// Decides which combatant acts next given the current set of living combatants.
/// Implementations may be stateful (e.g. tracking per-team cursors or a delay queue).
/// The match engine creates one instance per match and calls <see cref="NextActor"/> once per turn.
/// </summary>
/// <remarks>
/// Planned future policy: delay/agility-based ordering (Gunbound-style turn economy), where each
/// combatant earns turns according to an agility stat. That policy will likely require this signature
/// to widen to include live combatant state. Keeping the current narrow signature is a conscious
/// trade-off accepted at this phase — the interface itself is the seam.
/// </remarks>
public interface ITurnOrderPolicy
{
    /// <summary>
    /// Returns the roster index of the combatant that acts this turn.
    /// The returned index must appear in <paramref name="livingCombatantIndices"/>.
    /// </summary>
    /// <param name="livingCombatantIndices">Roster indices of combatants not yet defeated, in ascending order.</param>
    int NextActor(IReadOnlyList<int> livingCombatantIndices);
}
