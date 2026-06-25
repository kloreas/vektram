namespace Sim.Content;

/// <summary>
/// The body slot an <see cref="EquipmentDefinition"/> occupies. First-pass set only; more
/// slots (mount, pet, secondary weapon, etc. from DDTank) arrive as the system grows.
/// </summary>
public enum EquipmentSlot
{
    /// <summary>Primary weapon — offence-leaning modifiers (Attack, crit, penetration).</summary>
    Weapon,

    /// <summary>Body armor — defence-leaning modifiers (Defense, BaseGuard, MaxHp).</summary>
    Armor,

    /// <summary>Accessory — small mixed modifiers (crit, element).</summary>
    Accessory,
}
