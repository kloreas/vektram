using System.Collections.Generic;
using Sim.Stats;

namespace Sim.Content;

/// <summary>
/// Immutable, data-driven definition of one equipment piece. Canonical values are authored in
/// <c>/content/data/equipment.json</c>; never hardcode equipment tuning in C#.
/// </summary>
/// <remarks>
/// The piece grants stats through the shared modifier stack (<see cref="StatAssembler"/>), not
/// through a use-effect: its <see cref="Modifiers"/> are gathered at match setup by
/// <see cref="LoadoutResolver"/>. Each is tagged <see cref="ModifierSourceType.Equipment"/>
/// with this piece's <see cref="Id"/>, so the whole piece is removable on unequip.
/// Equality is element-wise over <see cref="Modifiers"/> so the same JSON text yields
/// value-equal definitions (the list member would otherwise compare by reference).
/// </remarks>
/// <param name="Id">Stable lookup key. Unique within a catalog.</param>
/// <param name="DisplayName">Display-only label. Has no stat effect.</param>
/// <param name="Slot">The body slot this piece occupies.</param>
/// <param name="Modifiers">The stat modifiers this piece grants, in authored order.</param>
public readonly record struct EquipmentDefinition(
    string                      Id,
    string                      DisplayName,
    EquipmentSlot               Slot,
    IReadOnlyList<StatModifier> Modifiers)
{
    /// <summary>Value equality including an element-wise comparison of <see cref="Modifiers"/>.</summary>
    public bool Equals(EquipmentDefinition other)
    {
        if (Id != other.Id || DisplayName != other.DisplayName || Slot != other.Slot)
            return false;
        if (Modifiers.Count != other.Modifiers.Count)
            return false;
        for (int i = 0; i < Modifiers.Count; i++)
            if (!Modifiers[i].Equals(other.Modifiers[i]))
                return false;
        return true;
    }

    /// <summary>Hash consistent with <see cref="Equals(EquipmentDefinition)"/> (id/slot/count).</summary>
    public override int GetHashCode()
    {
        var hash = new System.HashCode();
        hash.Add(Id);
        hash.Add(DisplayName);
        hash.Add(Slot);
        hash.Add(Modifiers.Count);
        return hash.ToHashCode();
    }
}
