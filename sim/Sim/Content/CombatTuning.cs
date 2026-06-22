using System;
using System.Text.Json;

namespace Sim.Content;

/// <summary>
/// Divisors, caps, and curve constants for the damage formula. The formula shape is
/// adopted from DDTank (ADR-0005); these tuning values are authored in
/// <c>/content/data/combat.json</c> rather than hardcoded in the formula.
/// </summary>
/// <remarks>
/// <see cref="Default"/> mirrors the shipped <c>combat.json</c> first-pass values and
/// serves as the engine fallback for tests and callers that do not load content. The
/// canonical source is <c>/content</c>; a test pins <c>combat.json</c> to
/// <see cref="Default"/> so the two cannot drift.
/// </remarks>
/// <param name="GuardDivisor">guardReduce = (BaseGuard − SunderArmor) / this.</param>
/// <param name="GuardReduceCap">Maximum guard reduction fraction, in [0, 1].</param>
/// <param name="DefenceDivisor">defenceReduce = Defense / this.</param>
/// <param name="DefenceReduceCap">Maximum defence reduction fraction, in [0, 1].</param>
/// <param name="FalloffStrength">Distance falloff: factor = 1 − this × (distance / radius).</param>
/// <param name="AttackFloor">attackFactor = this + Attack × <paramref name="AttackScale"/>.</param>
/// <param name="AttackScale">Per-point Attack contribution to the attack factor.</param>
/// <param name="BaseDamageBonusDivisor">Self-scaling term: 1 + BaseDamage / this.</param>
public readonly record struct CombatTuning(
    double GuardDivisor,
    double GuardReduceCap,
    double DefenceDivisor,
    double DefenceReduceCap,
    double FalloffStrength,
    double AttackFloor,
    double AttackScale,
    double BaseDamageBonusDivisor)
{
    /// <summary>The only <c>schemaVersion</c> this loader understands.</summary>
    public const int SupportedSchemaVersion = 1;

    /// <summary>
    /// Engine fallback mirroring <c>/content/data/combat.json</c>'s first-pass values.
    /// Pinned to the shipped file by a test.
    /// </summary>
    public static CombatTuning Default { get; } = new(
        GuardDivisor: 12000.0,
        GuardReduceCap: 0.60,
        DefenceDivisor: 80000.0,
        DefenceReduceCap: 0.90,
        FalloffStrength: 0.25,
        AttackFloor: 0.6,
        AttackScale: 0.00008,
        BaseDamageBonusDivisor: 10000.0);

    /// <summary>
    /// Parses combat tuning from JSON text. Pure: same text → equal value.
    /// </summary>
    /// <exception cref="CombatDataException">
    /// The text is malformed JSON, has the wrong shape or schema version, or any field is
    /// missing or out of range.
    /// </exception>
    public static CombatTuning FromJson(string json)
    {
        if (json is null)
            throw new CombatDataException("Combat tuning JSON must not be null.");

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            throw new CombatDataException("Combat tuning is not valid JSON.", ex);
        }

        using (document)
        {
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                throw new CombatDataException("Combat tuning root must be a JSON object.");

            int schemaVersion = ReadInt(root, "schemaVersion");
            if (schemaVersion != SupportedSchemaVersion)
                throw new CombatDataException(
                    $"Unsupported combat tuning schemaVersion {schemaVersion}; expected {SupportedSchemaVersion}.");

            double guardDivisor     = ReadPositive(root, "guardDivisor");
            double guardReduceCap   = ReadFraction(root, "guardReduceCap");
            double defenceDivisor   = ReadPositive(root, "defenceDivisor");
            double defenceReduceCap = ReadFraction(root, "defenceReduceCap");
            double falloffStrength  = ReadFraction(root, "falloffStrength");
            double attackFloor      = ReadNonNegative(root, "attackFloor");
            double attackScale      = ReadNonNegative(root, "attackScale");
            double baseBonusDivisor = ReadPositive(root, "baseDamageBonusDivisor");

            return new CombatTuning(
                guardDivisor, guardReduceCap,
                defenceDivisor, defenceReduceCap,
                falloffStrength, attackFloor, attackScale, baseBonusDivisor);
        }
    }

    private static double ReadNumber(JsonElement root, string field)
    {
        if (!root.TryGetProperty(field, out JsonElement value) ||
            value.ValueKind != JsonValueKind.Number ||
            !value.TryGetDouble(out double number))
            throw new CombatDataException($"Combat tuning is missing numeric field '{field}'.");
        return number;
    }

    private static int ReadInt(JsonElement root, string field)
    {
        if (!root.TryGetProperty(field, out JsonElement value) ||
            value.ValueKind != JsonValueKind.Number ||
            !value.TryGetInt32(out int number))
            throw new CombatDataException($"Combat tuning is missing integer field '{field}'.");
        return number;
    }

    private static double ReadPositive(JsonElement root, string field)
    {
        double value = ReadNumber(root, field);
        if (value <= 0.0)
            throw new CombatDataException($"Combat tuning '{field}' must be > 0 (was {value}).");
        return value;
    }

    private static double ReadNonNegative(JsonElement root, string field)
    {
        double value = ReadNumber(root, field);
        if (value < 0.0)
            throw new CombatDataException($"Combat tuning '{field}' must be >= 0 (was {value}).");
        return value;
    }

    private static double ReadFraction(JsonElement root, string field)
    {
        double value = ReadNumber(root, field);
        if (value < 0.0 || value > 1.0)
            throw new CombatDataException($"Combat tuning '{field}' must be in [0, 1] (was {value}).");
        return value;
    }
}
