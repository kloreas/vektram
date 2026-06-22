namespace Sim.Items;

/// <summary>
/// Result of attempting to consume from an <see cref="Inventory"/>.
/// </summary>
/// <param name="Success">
/// <see langword="true"/> if the inventory held enough to consume; otherwise
/// <see langword="false"/> and <see cref="Inventory"/> is unchanged.
/// </param>
/// <param name="Inventory">The resulting inventory (reduced on success, unchanged on failure).</param>
public readonly record struct ItemUseOutcome(bool Success, Inventory Inventory);
