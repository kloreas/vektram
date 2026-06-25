using System;
using System.Collections.Generic;
using System.Text.Json;
using Sim.Stats;

namespace Sim.Content;

/// <summary>
/// Immutable registry of <see cref="EquipmentDefinition"/> keyed by
/// <see cref="EquipmentDefinition.Id"/>.
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
/// equipment data — cannot diverge between server and client.
/// </para>
/// <para>
/// Each modifier's <see cref="ModifierSource"/> is stamped by the parser
/// (<see cref="ModifierSourceType.Equipment"/> + the piece id); provenance is derived, not
/// authored, so designers edit only <c>stat</c>/<c>op</c>/<c>value</c>.
/// </para>
/// </remarks>
public sealed class EquipmentCatalog
{
    /// <summary>The only <c>schemaVersion</c> this loader understands.</summary>
    public const int SupportedSchemaVersion = 1;

    private readonly IReadOnlyDictionary<string, EquipmentDefinition> _byId;

    private EquipmentCatalog(IReadOnlyDictionary<string, EquipmentDefinition> byId)
    {
        _byId = byId;
    }

    /// <summary>Number of definitions in the catalog.</summary>
    public int Count => _byId.Count;

    /// <summary>All defined equipment ids.</summary>
    public IReadOnlyCollection<string> Ids => (IReadOnlyCollection<string>)_byId.Keys;

    /// <summary>Returns the definition for <paramref name="id"/>, or throws if absent.</summary>
    /// <exception cref="EquipmentDataException">No definition with that id exists.</exception>
    public EquipmentDefinition Get(string id)
    {
        if (id is null)
            throw new EquipmentDataException("Equipment id must not be null.");
        if (!_byId.TryGetValue(id, out EquipmentDefinition def))
            throw new EquipmentDataException($"No equipment definition with id '{id}'.");
        return def;
    }

    /// <summary>Attempts to fetch the definition for <paramref name="id"/>.</summary>
    public bool TryGet(string id, out EquipmentDefinition definition) =>
        _byId.TryGetValue(id, out definition);

    /// <summary>
    /// Parses an equipment catalog from JSON text. Pure: same text → value-equal catalog.
    /// </summary>
    /// <exception cref="EquipmentDataException">
    /// The text is malformed JSON, has the wrong shape or schema version, or any definition
    /// has an invalid or duplicate field.
    /// </exception>
    public static EquipmentCatalog FromJson(string json)
    {
        if (json is null)
            throw new EquipmentDataException("Equipment data JSON must not be null.");

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            throw new EquipmentDataException("Equipment data is not valid JSON.", ex);
        }

        using (document)
        {
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                throw new EquipmentDataException("Equipment data root must be a JSON object.");

            if (!root.TryGetProperty("schemaVersion", out JsonElement versionEl) ||
                versionEl.ValueKind != JsonValueKind.Number ||
                !versionEl.TryGetInt32(out int schemaVersion))
                throw new EquipmentDataException("Equipment data must have an integer 'schemaVersion'.");
            if (schemaVersion != SupportedSchemaVersion)
                throw new EquipmentDataException(
                    $"Unsupported equipment data schemaVersion {schemaVersion}; expected {SupportedSchemaVersion}.");

            if (!root.TryGetProperty("equipment", out JsonElement pieces) ||
                pieces.ValueKind != JsonValueKind.Array)
                throw new EquipmentDataException("Equipment data must have an 'equipment' array.");

            var byId = new Dictionary<string, EquipmentDefinition>(StringComparer.Ordinal);
            int index = 0;
            foreach (JsonElement element in pieces.EnumerateArray())
            {
                EquipmentDefinition def = ReadPiece(element, index);
                if (!byId.ContainsKey(def.Id))
                    byId.Add(def.Id, def);
                else
                    throw new EquipmentDataException($"Duplicate equipment id '{def.Id}'.");
                index++;
            }

            return new EquipmentCatalog(byId);
        }
    }

    private static EquipmentDefinition ReadPiece(JsonElement element, int index)
    {
        if (element.ValueKind != JsonValueKind.Object)
            throw new EquipmentDataException($"Equipment at index {index} must be a JSON object.");

        string id          = ReadNonEmptyString(element, "id", index);
        string displayName  = ReadNonEmptyString(element, "displayName", index);
        EquipmentSlot slot  = ReadEnum<EquipmentSlot>(element, "slot", id);

        if (!element.TryGetProperty("modifiers", out JsonElement modifiersEl) ||
            modifiersEl.ValueKind != JsonValueKind.Array)
            throw new EquipmentDataException($"Equipment '{id}' must have a 'modifiers' array.");

        var source = new ModifierSource(ModifierSourceType.Equipment, id);
        var modifiers = new List<StatModifier>();
        int modIndex = 0;
        foreach (JsonElement modEl in modifiersEl.EnumerateArray())
        {
            modifiers.Add(ReadModifier(modEl, id, modIndex, source));
            modIndex++;
        }

        return new EquipmentDefinition(id, displayName, slot, modifiers);
    }

    private static StatModifier ReadModifier(JsonElement element, string id, int modIndex, ModifierSource source)
    {
        if (element.ValueKind != JsonValueKind.Object)
            throw new EquipmentDataException($"Equipment '{id}' modifier {modIndex} must be a JSON object.");

        StatKind   stat = ReadEnum<StatKind>(element, "stat", id);
        ModifierOp op   = ReadEnum<ModifierOp>(element, "op", id);

        if (!element.TryGetProperty("value", out JsonElement valueEl) ||
            valueEl.ValueKind != JsonValueKind.Number ||
            !valueEl.TryGetDouble(out double value))
            throw new EquipmentDataException(
                $"Equipment '{id}' modifier {modIndex} is missing numeric field 'value'.");

        return new StatModifier(stat, op, value, source);
    }

    private static string ReadNonEmptyString(JsonElement element, string field, int index)
    {
        if (!element.TryGetProperty(field, out JsonElement value) ||
            value.ValueKind != JsonValueKind.String)
            throw new EquipmentDataException($"Equipment at index {index} is missing string field '{field}'.");

        string? text = value.GetString();
        if (string.IsNullOrEmpty(text))
            throw new EquipmentDataException($"Equipment at index {index} has empty field '{field}'.");
        return text!;
    }

    private static TEnum ReadEnum<TEnum>(JsonElement element, string field, string id)
        where TEnum : struct, Enum
    {
        if (!element.TryGetProperty(field, out JsonElement value) ||
            value.ValueKind != JsonValueKind.String)
            throw new EquipmentDataException($"Equipment '{id}' is missing string field '{field}'.");

        string text = value.GetString()!;
        if (!Enum.TryParse(text, ignoreCase: false, out TEnum parsed) ||
            !Enum.IsDefined(typeof(TEnum), parsed))
            throw new EquipmentDataException($"Equipment '{id}' has unknown {field} '{text}'.");
        return parsed;
    }
}
