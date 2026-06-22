namespace Sim.Content;

/// <summary>
/// Immutable, data-driven definition of one item. Canonical values are authored in
/// <c>/content/data/items.json</c>; never hardcode item tuning in C#.
/// </summary>
/// <param name="Id">Stable lookup key. Unique within a catalog.</param>
/// <param name="DisplayName">Display-only label. Has no effect on resolution.</param>
/// <param name="Category">Broad item category.</param>
/// <param name="MaxStack">Maximum count per inventory stack. Always ≥ 1.</param>
/// <param name="Effect">The data-defined effect of using this item.</param>
public readonly record struct ItemDefinition(
    string       Id,
    string       DisplayName,
    ItemCategory Category,
    int          MaxStack,
    ItemEffect   Effect);
