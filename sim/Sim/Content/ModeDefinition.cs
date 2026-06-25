using System;
using System.Collections.Generic;
using Sim.Core;

namespace Sim.Content;

/// <summary>
/// Immutable, data-driven definition of one game mode — the ruleset a match runs under.
/// Canonical values are authored in <c>/content/data/modes.json</c>; never hardcode mode tuning
/// in C#. A "room" (the future <c>/server</c> lobby) merely selects a mode by id.
/// </summary>
/// <remarks>
/// This is the single authored record per ADR-0006 Decision 1 ("a new mode is new data, not new
/// code"). The match engine never consumes a <see cref="ModeDefinition"/> directly; the pure
/// <c>Sim.Match.ModeSetup</c> mapper splits it into the engine's existing provenance-free
/// primitives (<c>MatchOptions</c>, <c>CombatRules</c>, and <c>MatchModeRules</c>), so the engine
/// keeps consuming data it does not have to understand the origin of.
///
/// Equality is element-wise over <see cref="TeamSizes"/> so the same JSON text yields value-equal
/// definitions (the list member would otherwise compare by reference) — this is what the
/// <c>elimination == Default</c> drift-lock test relies on.
/// </remarks>
/// <param name="Id">Stable lookup key. Unique within a catalog.</param>
/// <param name="DisplayName">Display-only label. Has no rule effect.</param>
/// <param name="TeamSizes">
/// Intended team structure: list length is the team count, each entry that team's roster size
/// (<c>[1,1]</c> = 1v1, <c>[2,2]</c> = 2v2, <c>[1,1,1,1]</c> = FFA-4). An EMPTY list means
/// unconstrained — any roster is accepted (the default mode uses this so the existing varied test
/// rosters run unchanged). Roster construction itself is matchmaking and stays in the host.
/// </param>
/// <param name="ModeMultiplier">Damage scalar folded into <c>CombatRules.ModeMultiplier</c> (1.0 = no adjustment).</param>
/// <param name="FriendlyFire">Maps to <c>MatchOptions.FriendlyFire</c>.</param>
/// <param name="SelfDamage">Maps to <c>MatchOptions.SelfDamage</c>.</param>
/// <param name="TurnOrder">Selects the turn-order policy.</param>
/// <param name="MaxTurns">Per-mode turn cap; must be in 1..<see cref="SimConstants.MaxTurnsPerMatch"/>.</param>
/// <param name="WinCondition">The data-driven win condition evaluated each turn.</param>
public readonly record struct ModeDefinition(
    string                 Id,
    string                 DisplayName,
    IReadOnlyList<int>     TeamSizes,
    double                 ModeMultiplier,
    bool                   FriendlyFire,
    bool                   SelfDamage,
    TurnOrderPolicyKind    TurnOrder,
    int                    MaxTurns,
    WinConditionDefinition WinCondition)
{
    /// <summary>
    /// The engine-default mode, mirroring the shipped <c>elimination</c> row in
    /// <c>/content/data/modes.json</c> (pinned to it by a drift-lock test, like
    /// <c>CombatTuning.Default</c> ↔ <c>combat.json</c>). Reproduces pre-#5 behavior bit-for-bit:
    /// unconstrained roster, no damage scaling, friendly fire and self-damage on, round-robin
    /// order, the 200-turn safety cap, last-team-standing.
    /// </summary>
    public static ModeDefinition Default { get; } = new(
        "elimination",
        "Elimination",
        Array.Empty<int>(),
        1.0,
        FriendlyFire: true,
        SelfDamage: true,
        TurnOrderPolicyKind.RoundRobin,
        SimConstants.MaxTurnsPerMatch,
        WinConditionDefinition.LastTeamStanding);

    /// <summary>
    /// Validate-only check that a roster's per-team head counts match <see cref="TeamSizes"/>.
    /// An unconstrained mode (empty <see cref="TeamSizes"/>) accepts any roster. This never
    /// builds a roster — it only lets the host reject a mismatched one.
    /// </summary>
    /// <param name="teamIds">Team id for each roster slot.</param>
    public bool AcceptsRoster(IReadOnlyList<int> teamIds)
    {
        if (teamIds is null)
            return false;
        if (TeamSizes.Count == 0)
            return true;

        var counts = new int[TeamSizes.Count];
        for (int i = 0; i < teamIds.Count; i++)
        {
            int team = teamIds[i];
            if (team < 0 || team >= TeamSizes.Count)
                return false;
            counts[team]++;
        }

        for (int t = 0; t < TeamSizes.Count; t++)
            if (counts[t] != TeamSizes[t])
                return false;
        return true;
    }

    /// <summary>Value equality including an element-wise comparison of <see cref="TeamSizes"/>.</summary>
    public bool Equals(ModeDefinition other)
    {
        if (Id != other.Id ||
            DisplayName != other.DisplayName ||
            ModeMultiplier != other.ModeMultiplier ||
            FriendlyFire != other.FriendlyFire ||
            SelfDamage != other.SelfDamage ||
            TurnOrder != other.TurnOrder ||
            MaxTurns != other.MaxTurns ||
            !WinCondition.Equals(other.WinCondition))
            return false;
        if (TeamSizes.Count != other.TeamSizes.Count)
            return false;
        for (int i = 0; i < TeamSizes.Count; i++)
            if (TeamSizes[i] != other.TeamSizes[i])
                return false;
        return true;
    }

    /// <summary>Hash consistent with <see cref="Equals(ModeDefinition)"/>.</summary>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Id);
        hash.Add(DisplayName);
        hash.Add(ModeMultiplier);
        hash.Add(FriendlyFire);
        hash.Add(SelfDamage);
        hash.Add(TurnOrder);
        hash.Add(MaxTurns);
        hash.Add(WinCondition);
        hash.Add(TeamSizes.Count);
        return hash.ToHashCode();
    }
}
