using System;
using System.Collections.Generic;

namespace Sim.Match;

/// <summary>
/// Default turn-order policy: teams alternate, with combatants within each team cycling
/// through living members in original roster order. Defeated combatants are skipped.
/// </summary>
/// <remarks>
/// For a 2v2 roster [A(T0), B(T1), C(T0), D(T1)] the pattern is A B C D A B C D …
/// When C is defeated the pattern becomes … A B A D A B A D …
/// </remarks>
public sealed class RoundRobinTurnOrderPolicy : ITurnOrderPolicy
{
    // Per team: sorted list of combatant indices in original roster order.
    private readonly int[][] _rosterByTeam;

    // Per team: next position to try within that team's roster (wraps on use).
    private readonly int[] _cursors;

    // Which team acts next (cycles mod numTeams).
    private int _nextTeam;

    /// <summary>
    /// Creates a policy from team assignments of each combatant.
    /// </summary>
    /// <param name="teamIds">
    /// Team id for each combatant index in roster order.
    /// Team ids must start at 0 and be contiguous (0, 1, … N-1).
    /// </param>
    public RoundRobinTurnOrderPolicy(IReadOnlyList<int> teamIds)
    {
        int maxTeam = 0;
        for (int i = 0; i < teamIds.Count; i++)
            if (teamIds[i] > maxTeam) maxTeam = teamIds[i];

        var lists = new List<int>[maxTeam + 1];
        for (int t = 0; t <= maxTeam; t++) lists[t] = new List<int>();
        for (int i = 0; i < teamIds.Count; i++) lists[teamIds[i]].Add(i);

        _rosterByTeam = new int[maxTeam + 1][];
        for (int t = 0; t <= maxTeam; t++) _rosterByTeam[t] = lists[t].ToArray();

        _cursors  = new int[maxTeam + 1]; // all start at 0
        _nextTeam = 0;
    }

    /// <inheritdoc/>
    public int NextActor(IReadOnlyList<int> livingCombatantIndices)
    {
        int numTeams = _rosterByTeam.Length;

        for (int t = 0; t < numTeams; t++)
        {
            int   team   = (_nextTeam + t) % numTeams;
            int[] roster = _rosterByTeam[team];
            int   start  = _cursors[team];

            for (int j = 0; j < roster.Length; j++)
            {
                int pos          = (start + j) % roster.Length;
                int combatantIdx = roster[pos];

                if (IsLiving(combatantIdx, livingCombatantIndices))
                {
                    _cursors[team] = (pos + 1) % roster.Length;
                    _nextTeam      = (team + 1) % numTeams;
                    return combatantIdx;
                }
            }
        }

        // The match engine only calls NextActor while living.Count > 0.
        throw new InvalidOperationException("NextActor called with no living combatants.");
    }

    private static bool IsLiving(int idx, IReadOnlyList<int> livingCombatantIndices)
    {
        for (int i = 0; i < livingCombatantIndices.Count; i++)
            if (livingCombatantIndices[i] == idx)
                return true;
        return false;
    }
}
