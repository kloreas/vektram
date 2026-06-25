using System.Collections.Generic;
using Sim.Content;
using Sim.Match;

namespace Sim.Stats;

/// <summary>
/// Resolves a <see cref="Loadout"/> into effective <see cref="CombatantStats"/> by gathering
/// every active modifier and handing it to <see cref="StatAssembler"/>. This is the seam the
/// host/controller calls at match SETUP — never inside the turn loop, so the match and damage
/// code keep consuming finished stats and stay untouched.
/// </summary>
/// <remarks>
/// GATHER ORDER (fixed, for determinism): equipment pieces in <see cref="Loadout.EquippedIds"/>
/// order → each piece's <see cref="EquipmentDefinition.Modifiers"/> order →
/// <see cref="Loadout.Runes"/> order. The costume contributes nothing. Pure: no RNG, no I/O,
/// no Unity.
/// </remarks>
public static class LoadoutResolver
{
    /// <summary>
    /// Builds the effective stats for <paramref name="loadout"/> against
    /// <paramref name="equipment"/>.
    /// </summary>
    /// <exception cref="EquipmentDataException">
    /// An id in <see cref="Loadout.EquippedIds"/> is not present in the catalog.
    /// </exception>
    public static CombatantStats Resolve(in Loadout loadout, EquipmentCatalog equipment)
    {
        if (equipment is null)
            throw new EquipmentDataException("Equipment catalog must not be null.");

        var modifiers = new List<StatModifier>();

        IReadOnlyList<string>? equippedIds = loadout.EquippedIds;
        if (equippedIds is not null)
        {
            for (int i = 0; i < equippedIds.Count; i++)
            {
                EquipmentDefinition piece = equipment.Get(equippedIds[i]);
                IReadOnlyList<StatModifier> pieceModifiers = piece.Modifiers;
                for (int m = 0; m < pieceModifiers.Count; m++)
                    modifiers.Add(pieceModifiers[m]);
            }
        }

        IReadOnlyList<StatModifier>? runes = loadout.Runes;
        if (runes is not null)
        {
            for (int i = 0; i < runes.Count; i++)
                modifiers.Add(runes[i]);
        }

        // loadout.CosmeticId is deliberately ignored: cosmetics never grant power.

        return StatAssembler.Assemble(loadout.BaseStats, modifiers);
    }
}
