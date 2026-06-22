using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Sim.Content;

/// <summary>
/// Data-driven element advantage matrix. <see cref="Advantage"/> returns the multiplier
/// applied to the damage formula's element bonus when an attacker's element strikes a
/// defender's element.
/// </summary>
/// <remarks>
/// <para>
/// Faithful base: DDTank's element layer is additive (power − resist). The advantage
/// multiplier is a modern layer (ADR-0005) that defaults to 1.0 for every unspecified
/// pair, so with no overrides the behaviour is pure DDTank additive.
/// </para>
/// <para>
/// Constructed via <see cref="FromJson"/> (pure, non-reflective <see cref="JsonDocument"/>
/// parse). Hosts read the file; <c>/sim</c> parses the text.
/// </para>
/// </remarks>
public sealed class ElementTable
{
    /// <summary>The only <c>schemaVersion</c> this loader understands.</summary>
    public const int SupportedSchemaVersion = 1;

    private const double NeutralMultiplier = 1.0;

    private readonly IReadOnlyDictionary<(Element Attacker, Element Defender), double> _advantages;

    private ElementTable(IReadOnlyDictionary<(Element, Element), double> advantages)
    {
        _advantages = advantages;
    }

    /// <summary>An empty table: every pair is neutral (1.0). Pure DDTank additive behaviour.</summary>
    public static ElementTable Neutral { get; } =
        new(new Dictionary<(Element, Element), double>());

    /// <summary>Number of non-neutral advantage overrides.</summary>
    public int OverrideCount => _advantages.Count;

    /// <summary>
    /// Advantage multiplier for <paramref name="attacker"/>'s element striking
    /// <paramref name="defender"/>'s element. Unspecified pairs return 1.0.
    /// </summary>
    public double Advantage(Element attacker, Element defender) =>
        _advantages.TryGetValue((attacker, defender), out double multiplier)
            ? multiplier
            : NeutralMultiplier;

    /// <summary>
    /// Parses an element advantage table from JSON text. Pure: same text → equal table.
    /// </summary>
    /// <exception cref="CombatDataException">
    /// The text is malformed JSON, has the wrong shape or schema version, references an
    /// unknown element, repeats a pair, or has a negative multiplier.
    /// </exception>
    public static ElementTable FromJson(string json)
    {
        if (json is null)
            throw new CombatDataException("Element table JSON must not be null.");

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            throw new CombatDataException("Element table is not valid JSON.", ex);
        }

        using (document)
        {
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                throw new CombatDataException("Element table root must be a JSON object.");

            if (!root.TryGetProperty("schemaVersion", out JsonElement versionEl) ||
                versionEl.ValueKind != JsonValueKind.Number ||
                !versionEl.TryGetInt32(out int schemaVersion))
                throw new CombatDataException("Element table must have an integer 'schemaVersion'.");
            if (schemaVersion != SupportedSchemaVersion)
                throw new CombatDataException(
                    $"Unsupported element table schemaVersion {schemaVersion}; expected {SupportedSchemaVersion}.");

            if (!root.TryGetProperty("advantages", out JsonElement advantages) ||
                advantages.ValueKind != JsonValueKind.Array)
                throw new CombatDataException("Element table must have an 'advantages' array.");

            var map = new Dictionary<(Element, Element), double>();
            int index = 0;
            foreach (JsonElement entry in advantages.EnumerateArray())
            {
                if (entry.ValueKind != JsonValueKind.Object)
                    throw new CombatDataException($"Advantage at index {index} must be a JSON object.");

                Element attacker   = ReadElement(entry, "attacker", index);
                Element defender   = ReadElement(entry, "defender", index);
                double  multiplier = ReadMultiplier(entry, index);

                if (!map.ContainsKey((attacker, defender)))
                    map.Add((attacker, defender), multiplier);
                else
                    throw new CombatDataException(
                        $"Duplicate advantage pair ({attacker} vs {defender}) at index {index}.");
                index++;
            }

            return new ElementTable(map);
        }
    }

    private static Element ReadElement(JsonElement entry, string field, int index)
    {
        if (!entry.TryGetProperty(field, out JsonElement value) ||
            value.ValueKind != JsonValueKind.String)
            throw new CombatDataException($"Advantage at index {index} is missing string field '{field}'.");

        string text = value.GetString()!;
        if (!Enum.TryParse(text, ignoreCase: false, out Element element) ||
            !Enum.IsDefined(typeof(Element), element))
            throw new CombatDataException($"Advantage at index {index} has unknown element '{text}'.");
        return element;
    }

    private static double ReadMultiplier(JsonElement entry, int index)
    {
        if (!entry.TryGetProperty("multiplier", out JsonElement value) ||
            value.ValueKind != JsonValueKind.Number ||
            !value.TryGetDouble(out double multiplier))
            throw new CombatDataException($"Advantage at index {index} is missing numeric 'multiplier'.");
        if (multiplier < 0.0)
            throw new CombatDataException($"Advantage at index {index} multiplier must be >= 0 (was {multiplier}).");
        return multiplier;
    }
}
