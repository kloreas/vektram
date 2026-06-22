using System;
using System.Collections.Generic;
using System.Text.Json;
using Sim.Projectile;

namespace Sim.Content;

/// <summary>
/// Immutable registry of <see cref="BallDefinition"/> keyed by <see cref="BallDefinition.Id"/>.
/// </summary>
/// <remarks>
/// <para>
/// Construct via <see cref="FromJson"/>, a pure function: the same JSON text always
/// yields a value-equal catalog. Parsing is done with a non-reflective
/// <see cref="JsonDocument"/> walk (IL2CPP-safe for the Unity client).
/// </para>
/// <para>
/// HOST DOES I/O, SIM PARSES TEXT: <c>/sim</c> performs no file or network access.
/// The host (match server / Unity content pipeline) reads the file and hands the
/// string here, so the parse — the single source of truth for shell data — cannot
/// diverge between server and client.
/// </para>
/// </remarks>
public sealed class BallCatalog
{
    /// <summary>The only <c>schemaVersion</c> this loader understands.</summary>
    public const int SupportedSchemaVersion = 1;

    private readonly IReadOnlyDictionary<string, BallDefinition> _byId;

    private BallCatalog(IReadOnlyDictionary<string, BallDefinition> byId)
    {
        _byId = byId;
    }

    /// <summary>Number of definitions in the catalog.</summary>
    public int Count => _byId.Count;

    /// <summary>All defined ball ids.</summary>
    public IReadOnlyCollection<string> Ids => (IReadOnlyCollection<string>)_byId.Keys;

    /// <summary>Returns the definition for <paramref name="id"/>, or throws if absent.</summary>
    /// <exception cref="BallDataException">No definition with that id exists.</exception>
    public BallDefinition Get(string id)
    {
        if (id is null)
            throw new BallDataException("Ball id must not be null.");
        if (!_byId.TryGetValue(id, out BallDefinition def))
            throw new BallDataException($"No ball definition with id '{id}'.");
        return def;
    }

    /// <summary>Attempts to fetch the definition for <paramref name="id"/>.</summary>
    public bool TryGet(string id, out BallDefinition definition) =>
        _byId.TryGetValue(id, out definition);

    /// <summary>
    /// Parses a ball catalog from JSON text. Pure: same text → value-equal catalog.
    /// </summary>
    /// <param name="json">The full balls.json document text.</param>
    /// <exception cref="BallDataException">
    /// The text is malformed JSON, has the wrong shape or schema version, or any
    /// definition has an invalid or duplicate field.
    /// </exception>
    public static BallCatalog FromJson(string json)
    {
        if (json is null)
            throw new BallDataException("Ball data JSON must not be null.");

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            throw new BallDataException("Ball data is not valid JSON.", ex);
        }

        using (document)
        {
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                throw new BallDataException("Ball data root must be a JSON object.");

            int schemaVersion = ReadSchemaVersion(root);
            if (schemaVersion != SupportedSchemaVersion)
                throw new BallDataException(
                    $"Unsupported ball data schemaVersion {schemaVersion}; expected {SupportedSchemaVersion}.");

            if (!root.TryGetProperty("balls", out JsonElement balls) ||
                balls.ValueKind != JsonValueKind.Array)
                throw new BallDataException("Ball data must have a 'balls' array.");

            var byId = new Dictionary<string, BallDefinition>(StringComparer.Ordinal);
            int index = 0;
            foreach (JsonElement element in balls.EnumerateArray())
            {
                BallDefinition def = ReadBall(element, index);
                if (!byId.ContainsKey(def.Id))
                    byId.Add(def.Id, def);
                else
                    throw new BallDataException($"Duplicate ball id '{def.Id}'.");
                index++;
            }

            return new BallCatalog(byId);
        }
    }

    private static int ReadSchemaVersion(JsonElement root)
    {
        if (!root.TryGetProperty("schemaVersion", out JsonElement version) ||
            version.ValueKind != JsonValueKind.Number ||
            !version.TryGetInt32(out int value))
            throw new BallDataException("Ball data must have an integer 'schemaVersion'.");
        return value;
    }

    private static BallDefinition ReadBall(JsonElement element, int index)
    {
        if (element.ValueKind != JsonValueKind.Object)
            throw new BallDataException($"Ball at index {index} must be a JSON object.");

        string id          = ReadNonEmptyString(element, "id", index);
        string displayName = ReadNonEmptyString(element, "displayName", index);
        ShellType type      = ReadShellType(element, id);

        double gravityScale    = ReadNumber(element, "gravityScale", id);
        double windSensitivity = ReadNumber(element, "windSensitivity", id);
        double blastRadius     = ReadNumber(element, "blastRadius", id);
        double baseDamage      = ReadNumber(element, "baseDamage", id);
        double projectileSpeed = ReadNumber(element, "projectileSpeed", id);

        if (gravityScale <= 0.0)
            throw new BallDataException($"Ball '{id}': gravityScale must be > 0 (was {gravityScale}).");
        if (windSensitivity < 0.0)
            throw new BallDataException($"Ball '{id}': windSensitivity must be >= 0 (was {windSensitivity}).");
        if (blastRadius <= 0.0)
            throw new BallDataException($"Ball '{id}': blastRadius must be > 0 (was {blastRadius}).");
        if (baseDamage < 0.0)
            throw new BallDataException($"Ball '{id}': baseDamage must be >= 0 (was {baseDamage}).");
        if (projectileSpeed < 0.0)
            throw new BallDataException($"Ball '{id}': projectileSpeed must be >= 0 (was {projectileSpeed}).");

        return new BallDefinition(
            id,
            displayName,
            type,
            new ShellPhysics(gravityScale, windSensitivity),
            blastRadius,
            baseDamage,
            projectileSpeed);
    }

    private static string ReadNonEmptyString(JsonElement element, string field, int index)
    {
        if (!element.TryGetProperty(field, out JsonElement value) ||
            value.ValueKind != JsonValueKind.String)
            throw new BallDataException($"Ball at index {index} is missing string field '{field}'.");

        string? text = value.GetString();
        if (string.IsNullOrEmpty(text))
            throw new BallDataException($"Ball at index {index} has empty field '{field}'.");
        return text!;
    }

    private static double ReadNumber(JsonElement element, string field, string id)
    {
        if (!element.TryGetProperty(field, out JsonElement value) ||
            value.ValueKind != JsonValueKind.Number ||
            !value.TryGetDouble(out double number))
            throw new BallDataException($"Ball '{id}' is missing numeric field '{field}'.");
        return number;
    }

    private static ShellType ReadShellType(JsonElement element, string id)
    {
        if (!element.TryGetProperty("type", out JsonElement value) ||
            value.ValueKind != JsonValueKind.String)
            throw new BallDataException($"Ball '{id}' is missing string field 'type'.");

        string text = value.GetString()!;
        if (!Enum.TryParse(text, ignoreCase: false, out ShellType type) ||
            !Enum.IsDefined(typeof(ShellType), type))
            throw new BallDataException($"Ball '{id}' has unknown shell type '{text}'.");
        return type;
    }
}
