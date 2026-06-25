using System.Collections.Generic;
using Sim.Match;

namespace Sim.Stats;

/// <summary>
/// A combatant's full gear configuration, resolved into effective stats at match SETUP by
/// <see cref="LoadoutResolver"/>. Pure data; carries no logic.
/// </summary>
/// <remarks>
/// <see cref="BaseStats"/> is the starting point the modifier stack sits on; progression (#6)
/// owns where those base values come from (class/level). <see cref="CosmeticId"/> is display
/// only and contributes zero modifiers — cosmetics never grant power (ADR-0006).
/// </remarks>
/// <param name="BaseStats">Base stat block (from class/level) the modifiers adjust.</param>
/// <param name="EquippedIds">
/// Equipment piece ids to resolve against the catalog, in equip order. This order, then each
/// piece's modifier-list order, then <see cref="Runes"/> order, fixes the gather order.
/// </param>
/// <param name="Runes">
/// Rune-sourced modifiers (the seam for the later rune system; no rune catalog yet).
/// </param>
/// <param name="CosmeticId">Display-only costume id, or null. Never affects stats.</param>
public readonly record struct Loadout(
    CombatantStats              BaseStats,
    IReadOnlyList<string>?      EquippedIds,
    IReadOnlyList<StatModifier>? Runes,
    string?                     CosmeticId)
{
    /// <summary>A bare loadout: the given base stats, no equipment, no runes, no costume.</summary>
    public static Loadout Bare(CombatantStats baseStats) => new(baseStats, null, null, null);
}
