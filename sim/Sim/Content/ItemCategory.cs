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
}
