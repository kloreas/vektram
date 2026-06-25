namespace Sim.Content;

/// <summary>
/// Broad item category. Starter set only; equipment categories arrive with the
/// equipment / modifier-stack system (#4).
/// </summary>
public enum ItemCategory
{
    /// <summary>Grants/selects a shell to fire (references a <see cref="BallDefinition"/>).</summary>
    BallGrant,

    /// <summary>Single-use item applied for an immediate effect (e.g. heal).</summary>
    Consumable,

    /// <summary>
    /// An ownable equipment piece. A zero-cost forward tag: the stat data lives in
    /// <see cref="EquipmentCatalog"/>, not in an <see cref="ItemEffect"/>. Inventory ownership
    /// of equipment is wired by economy (#7); nothing in #4 authors an item of this category.
    /// </summary>
    Equipment,
}
