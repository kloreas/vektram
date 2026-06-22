using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Sim.Content;

/// <summary>
/// Immutable registry of <see cref="ItemDefinition"/> keyed by <see cref="ItemDefinition.Id"/>.
/// </summary>
/// <remarks>
/// <para>
/// Construct via <see cref="FromJson"/>, a pure function: the same JSON text always yields a
/// value-equal catalog. Parsing uses a non-reflective <see cref="JsonDocument"/> walk
/// (IL2CPP-safe for the Unity client).
/// </para>
/// <para>
/// HOST DOES I/O, SIM PARSES TEXT: <c>/sim</c> performs no file or network access. The host
/// reads the file and hands the string here, so the parse — the single source of truth for
/// item data — cannot diverge between server and client.
/// </para>
/// </remarks>
public sealed class ItemCatalog
{
    /// <summary>The only <c>schemaVersion</c> this loader understands.</summary>
    public const int SupportedSchemaVersion = 1;

    private readonly IReadOnlyDictionary<string, ItemDefinition> _byId;

    private ItemCatalog(IReadOnlyDictionary<string, ItemDefinition> byId)
    {
        _byId = byId;
    }

    /// <summary>Number of definitions in the catalog.</summary>
    public int Count => _byId.Count;

    /// <summary>All defined item ids.</summary>
    public IReadOnlyCollection<string> Ids => (IReadOnlyCollection<string>)_byId.Keys;

    /// <summary>Returns the definition for <paramref name="id"/>, or throws if absent.</summary>
    /// <exception cref="ItemDataException">No definition with that id exists.</exception>
    public ItemDefinition Get(string id)
    {
        if (id is null)
            throw new ItemDataException("Item id must not be null.");
        if (!_byId.TryGetValue(id, out ItemDefinition def))
            throw new ItemDataException($"No item definition with id '{id}'.");
        return def;
    }

    /// <summary>Attempts to fetch the definition for <paramref name="id"/>.</summary>
    public bool TryGet(string id, out ItemDefinition definition) =>
        _byId.TryGetValue(id, out definition);

    /// <summary>
    /// Fail-fast cross-catalog check: verifies every <see cref="ItemEffectKind.GrantBall"/>
    /// effect references a ball present in <paramref name="ballCatalog"/>. Resolution-time
    /// <see cref="BallCatalog.Get"/> also throws on a dangling id; this lets the host catch
    /// the problem at load instead.
    /// </summary>
    /// <exception cref="ItemDataException">An item references a ball id not in the ball catalog.</exception>
    public void ValidateBallReferences(BallCatalog ballCatalog)
    {
        if (ballCatalog is null)
            throw new ItemDataException("Ball catalog must not be null.");

        foreach (KeyValuePair<string, ItemDefinition> entry in _byId)
        {
            ItemEffect effect = entry.Value.Effect;
            if (effect.Kind == ItemEffectKind.GrantBall &&
                !ballCatalog.TryGet(effect.BallId!, out _))
                throw new ItemDataException(
                    $"Item '{entry.Key}' references unknown ball id '{effect.BallId}'.");
        }
    }

    /// <summary>
    /// Parses an item catalog from JSON text. Pure: same text → value-equal catalog.
    /// </summary>
    /// <exception cref="ItemDataException">
    /// The text is malformed JSON, has the wrong shape or schema version, or any definition
    /// has an invalid or duplicate field.
    /// </exception>
    public static ItemCatalog FromJson(string json)
    {
        if (json is null)
            throw new ItemDataException("Item data JSON must not be null.");

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            throw new ItemDataException("Item data is not valid JSON.", ex);
        }

        using (document)
        {
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                throw new ItemDataException("Item data root must be a JSON object.");

            if (!root.TryGetProperty("schemaVersion", out JsonElement versionEl) ||
                versionEl.ValueKind != JsonValueKind.Number ||
                !versionEl.TryGetInt32(out int schemaVersion))
                throw new ItemDataException("Item data must have an integer 'schemaVersion'.");
            if (schemaVersion != SupportedSchemaVersion)
                throw new ItemDataException(
                    $"Unsupported item data schemaVersion {schemaVersion}; expected {SupportedSchemaVersion}.");

            if (!root.TryGetProperty("items", out JsonElement items) ||
                items.ValueKind != JsonValueKind.Array)
                throw new ItemDataException("Item data must have an 'items' array.");

            var byId = new Dictionary<string, ItemDefinition>(StringComparer.Ordinal);
            int index = 0;
            foreach (JsonElement element in items.EnumerateArray())
            {
                ItemDefinition def = ReadItem(element, index);
                if (!byId.ContainsKey(def.Id))
                    byId.Add(def.Id, def);
                else
                    throw new ItemDataException($"Duplicate item id '{def.Id}'.");
                index++;
            }

            return new ItemCatalog(byId);
        }
    }

    private static ItemDefinition ReadItem(JsonElement element, int index)
    {
        if (element.ValueKind != JsonValueKind.Object)
            throw new ItemDataException($"Item at index {index} must be a JSON object.");

        string id          = ReadNonEmptyString(element, "id", index);
        string displayName = ReadNonEmptyString(element, "displayName", index);

        ItemCategory category = ReadEnum<ItemCategory>(element, "category", id);

        if (!element.TryGetProperty("maxStack", out JsonElement maxStackEl) ||
            maxStackEl.ValueKind != JsonValueKind.Number ||
            !maxStackEl.TryGetInt32(out int maxStack))
            throw new ItemDataException($"Item '{id}' is missing integer field 'maxStack'.");
        if (maxStack < 1)
            throw new ItemDataException($"Item '{id}': maxStack must be >= 1 (was {maxStack}).");

        if (!element.TryGetProperty("effect", out JsonElement effectEl) ||
            effectEl.ValueKind != JsonValueKind.Object)
            throw new ItemDataException($"Item '{id}' is missing object field 'effect'.");

        ItemEffect effect = ReadEffect(effectEl, id);

        return new ItemDefinition(id, displayName, category, maxStack, effect);
    }

    private static ItemEffect ReadEffect(JsonElement element, string id)
    {
        ItemEffectKind kind = ReadEnum<ItemEffectKind>(element, "kind", id);

        switch (kind)
        {
            case ItemEffectKind.GrantBall:
                string ballId = ReadNonEmptyEffectString(element, "ballId", id);
                return new ItemEffect(kind, ballId, 0.0);

            case ItemEffectKind.RestoreHp:
                if (!element.TryGetProperty("amount", out JsonElement amountEl) ||
                    amountEl.ValueKind != JsonValueKind.Number ||
                    !amountEl.TryGetDouble(out double amount))
                    throw new ItemDataException($"Item '{id}' effect RestoreHp is missing numeric 'amount'.");
                if (amount < 0.0)
                    throw new ItemDataException($"Item '{id}' effect amount must be >= 0 (was {amount}).");
                return new ItemEffect(kind, null, amount);

            default:
                throw new ItemDataException($"Item '{id}' has unsupported effect kind '{kind}'.");
        }
    }

    private static string ReadNonEmptyString(JsonElement element, string field, int index)
    {
        if (!element.TryGetProperty(field, out JsonElement value) ||
            value.ValueKind != JsonValueKind.String)
            throw new ItemDataException($"Item at index {index} is missing string field '{field}'.");

        string? text = value.GetString();
        if (string.IsNullOrEmpty(text))
            throw new ItemDataException($"Item at index {index} has empty field '{field}'.");
        return text!;
    }

    private static string ReadNonEmptyEffectString(JsonElement element, string field, string id)
    {
        if (!element.TryGetProperty(field, out JsonElement value) ||
            value.ValueKind != JsonValueKind.String)
            throw new ItemDataException($"Item '{id}' effect is missing string field '{field}'.");

        string? text = value.GetString();
        if (string.IsNullOrEmpty(text))
            throw new ItemDataException($"Item '{id}' effect has empty field '{field}'.");
        return text!;
    }

    private static TEnum ReadEnum<TEnum>(JsonElement element, string field, string id)
        where TEnum : struct, Enum
    {
        if (!element.TryGetProperty(field, out JsonElement value) ||
            value.ValueKind != JsonValueKind.String)
            throw new ItemDataException($"Item '{id}' is missing string field '{field}'.");

        string text = value.GetString()!;
        if (!Enum.TryParse(text, ignoreCase: false, out TEnum parsed) ||
            !Enum.IsDefined(typeof(TEnum), parsed))
            throw new ItemDataException($"Item '{id}' has unknown {field} '{text}'.");
        return parsed;
    }
}
