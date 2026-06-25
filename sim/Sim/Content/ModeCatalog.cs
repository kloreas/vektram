using System;
using System.Collections.Generic;
using System.Text.Json;
using Sim.Core;

namespace Sim.Content;

/// <summary>
/// Immutable registry of <see cref="ModeDefinition"/> keyed by <see cref="ModeDefinition.Id"/>.
/// </summary>
/// <remarks>
/// <para>
/// Construct via <see cref="FromJson"/>, a pure function: the same JSON text always yields a
/// value-equal catalog. Parsing uses a non-reflective <see cref="JsonDocument"/> walk
/// (IL2CPP-safe for the Unity client).
/// </para>
/// <para>
/// HOST DOES I/O, SIM PARSES TEXT: <c>/sim</c> performs no file or network access. The host reads
/// the file and hands the string here, so the parse — the single source of truth for mode data —
/// cannot diverge between server and client.
/// </para>
/// </remarks>
public sealed class ModeCatalog
{
    /// <summary>The only <c>schemaVersion</c> this loader understands.</summary>
    public const int SupportedSchemaVersion = 1;

    private readonly IReadOnlyDictionary<string, ModeDefinition> _byId;

    private ModeCatalog(IReadOnlyDictionary<string, ModeDefinition> byId)
    {
        _byId = byId;
    }

    /// <summary>Number of definitions in the catalog.</summary>
    public int Count => _byId.Count;

    /// <summary>All defined mode ids.</summary>
    public IReadOnlyCollection<string> Ids => (IReadOnlyCollection<string>)_byId.Keys;

    /// <summary>Returns the definition for <paramref name="id"/>, or throws if absent.</summary>
    /// <exception cref="ModeDataException">No definition with that id exists.</exception>
    public ModeDefinition Get(string id)
    {
        if (id is null)
            throw new ModeDataException("Mode id must not be null.");
        if (!_byId.TryGetValue(id, out ModeDefinition def))
            throw new ModeDataException($"No mode definition with id '{id}'.");
        return def;
    }

    /// <summary>Attempts to fetch the definition for <paramref name="id"/>.</summary>
    public bool TryGet(string id, out ModeDefinition definition) =>
        _byId.TryGetValue(id, out definition);

    /// <summary>
    /// Parses a mode catalog from JSON text. Pure: same text → value-equal catalog.
    /// </summary>
    /// <exception cref="ModeDataException">
    /// The text is malformed JSON, has the wrong shape or schema version, or any definition has an
    /// invalid or duplicate field.
    /// </exception>
    public static ModeCatalog FromJson(string json)
    {
        if (json is null)
            throw new ModeDataException("Mode data JSON must not be null.");

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            throw new ModeDataException("Mode data is not valid JSON.", ex);
        }

        using (document)
        {
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                throw new ModeDataException("Mode data root must be a JSON object.");

            if (!root.TryGetProperty("schemaVersion", out JsonElement versionEl) ||
                versionEl.ValueKind != JsonValueKind.Number ||
                !versionEl.TryGetInt32(out int schemaVersion))
                throw new ModeDataException("Mode data must have an integer 'schemaVersion'.");
            if (schemaVersion != SupportedSchemaVersion)
                throw new ModeDataException(
                    $"Unsupported mode data schemaVersion {schemaVersion}; expected {SupportedSchemaVersion}.");

            if (!root.TryGetProperty("modes", out JsonElement modes) ||
                modes.ValueKind != JsonValueKind.Array)
                throw new ModeDataException("Mode data must have a 'modes' array.");

            var byId = new Dictionary<string, ModeDefinition>(StringComparer.Ordinal);
            int index = 0;
            foreach (JsonElement element in modes.EnumerateArray())
            {
                ModeDefinition def = ReadMode(element, index);
                if (!byId.ContainsKey(def.Id))
                    byId.Add(def.Id, def);
                else
                    throw new ModeDataException($"Duplicate mode id '{def.Id}'.");
                index++;
            }

            return new ModeCatalog(byId);
        }
    }

    private static ModeDefinition ReadMode(JsonElement element, int index)
    {
        if (element.ValueKind != JsonValueKind.Object)
            throw new ModeDataException($"Mode at index {index} must be a JSON object.");

        string id          = ReadNonEmptyString(element, "id", index);
        string displayName = ReadNonEmptyString(element, "displayName", index);

        IReadOnlyList<int> teamSizes = ReadTeamSizes(element, id);
        double modeMultiplier        = ReadPositive(element, "modeMultiplier", id);
        bool friendlyFire            = ReadBool(element, "friendlyFire", id);
        bool selfDamage              = ReadBool(element, "selfDamage", id);
        TurnOrderPolicyKind turnOrder = ReadEnum<TurnOrderPolicyKind>(element, "turnOrder", id);
        int maxTurns                 = ReadMaxTurns(element, id);
        WinConditionDefinition winCondition = ReadWinCondition(element, id);

        return new ModeDefinition(
            id, displayName, teamSizes, modeMultiplier,
            friendlyFire, selfDamage, turnOrder, maxTurns, winCondition);
    }

    private static IReadOnlyList<int> ReadTeamSizes(JsonElement element, string id)
    {
        if (!element.TryGetProperty("teamSizes", out JsonElement value) ||
            value.ValueKind != JsonValueKind.Array)
            throw new ModeDataException($"Mode '{id}' is missing array field 'teamSizes'.");

        var sizes = new List<int>();
        foreach (JsonElement sizeEl in value.EnumerateArray())
        {
            if (sizeEl.ValueKind != JsonValueKind.Number || !sizeEl.TryGetInt32(out int size))
                throw new ModeDataException($"Mode '{id}': every teamSizes entry must be an integer.");
            if (size < 1)
                throw new ModeDataException($"Mode '{id}': teamSizes entries must be >= 1 (was {size}).");
            sizes.Add(size);
        }
        return sizes;
    }

    private static WinConditionDefinition ReadWinCondition(JsonElement element, string id)
    {
        if (!element.TryGetProperty("winCondition", out JsonElement value) ||
            value.ValueKind != JsonValueKind.Object)
            throw new ModeDataException($"Mode '{id}' is missing object field 'winCondition'.");

        WinConditionKind kind = ReadEnum<WinConditionKind>(value, "kind", id);
        bool hasTiebreak = value.TryGetProperty("tiebreak", out JsonElement _);

        switch (kind)
        {
            case WinConditionKind.LastTeamStanding:
                if (hasTiebreak)
                    throw new ModeDataException(
                        $"Mode '{id}': winCondition LastTeamStanding must not specify a 'tiebreak'.");
                return new WinConditionDefinition(kind, null);

            case WinConditionKind.TurnLimitTiebreak:
                if (!hasTiebreak)
                    throw new ModeDataException(
                        $"Mode '{id}': winCondition TurnLimitTiebreak requires a 'tiebreak'.");
                TiebreakMetric metric = ReadEnum<TiebreakMetric>(value, "tiebreak", id);
                return new WinConditionDefinition(kind, metric);

            default:
                throw new ModeDataException($"Mode '{id}' has unsupported winCondition kind '{kind}'.");
        }
    }

    private static int ReadMaxTurns(JsonElement element, string id)
    {
        if (!element.TryGetProperty("maxTurns", out JsonElement value) ||
            value.ValueKind != JsonValueKind.Number ||
            !value.TryGetInt32(out int maxTurns))
            throw new ModeDataException($"Mode '{id}' is missing integer field 'maxTurns'.");
        if (maxTurns < 1 || maxTurns > SimConstants.MaxTurnsPerMatch)
            throw new ModeDataException(
                $"Mode '{id}': maxTurns must be in 1..{SimConstants.MaxTurnsPerMatch} (was {maxTurns}).");
        return maxTurns;
    }

    private static string ReadNonEmptyString(JsonElement element, string field, int index)
    {
        if (!element.TryGetProperty(field, out JsonElement value) ||
            value.ValueKind != JsonValueKind.String)
            throw new ModeDataException($"Mode at index {index} is missing string field '{field}'.");

        string? text = value.GetString();
        if (string.IsNullOrEmpty(text))
            throw new ModeDataException($"Mode at index {index} has empty field '{field}'.");
        return text!;
    }

    private static double ReadPositive(JsonElement element, string field, string id)
    {
        if (!element.TryGetProperty(field, out JsonElement value) ||
            value.ValueKind != JsonValueKind.Number ||
            !value.TryGetDouble(out double number))
            throw new ModeDataException($"Mode '{id}' is missing numeric field '{field}'.");
        if (number <= 0.0)
            throw new ModeDataException($"Mode '{id}': {field} must be > 0 (was {number}).");
        return number;
    }

    private static bool ReadBool(JsonElement element, string field, string id)
    {
        if (!element.TryGetProperty(field, out JsonElement value) ||
            (value.ValueKind != JsonValueKind.True && value.ValueKind != JsonValueKind.False))
            throw new ModeDataException($"Mode '{id}' is missing boolean field '{field}'.");
        return value.GetBoolean();
    }

    private static TEnum ReadEnum<TEnum>(JsonElement element, string field, string id)
        where TEnum : struct, Enum
    {
        if (!element.TryGetProperty(field, out JsonElement value) ||
            value.ValueKind != JsonValueKind.String)
            throw new ModeDataException($"Mode '{id}' is missing string field '{field}'.");

        string text = value.GetString()!;
        if (!Enum.TryParse(text, ignoreCase: false, out TEnum parsed) ||
            !Enum.IsDefined(typeof(TEnum), parsed))
            throw new ModeDataException($"Mode '{id}' has unknown {field} '{text}'.");
        return parsed;
    }
}
