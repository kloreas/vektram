using System;
using System.Globalization;
using System.IO;
using Sim;
using Sim.Content;
using Sim.Core;
using Sim.Match;
using Sim.Projectile;
using Sim.Terrain;

namespace Vektram.MatchHost;

/// <summary>
/// Thinnest authoritative host: loads <c>/content</c> from disk, parses it through the sim's
/// pure catalog parsers (host does the file I/O, sim parses the text), then runs ONE full match
/// server-side through the sim and prints the authoritative result + turn log.
/// </summary>
/// <remarks>
/// This is the embryo of the permanent .NET match service. Per ADR-0001 the authoritative sim
/// is the single source of truth and must run in a .NET process; Nakama (Go/Lua/TS) sits in
/// front of this service for matchmaking/sessions/transport and delegates simulation to it —
/// it never hosts the sim, because it cannot load a netstandard2.1 assembly.
/// </remarks>
internal static class Program
{
    // Locked scenario — mirrors MatchSimulatorTests.FullMatch_OnePerTeam_RunsToCompletion and
    // CorrectWinningTeam_Named exactly (seed 0, 1v1, a clean-win shot vs a no-op). The suite
    // locks the outcome to Team0Wins / team 0, so this host must reproduce the same answer.
    private const uint   Seed      = 0u;
    private const double StartHp   = 100.0;
    private const double ShotSpeed = 50.0;

    // CleanWinWeapon reaches the enemy at the impact point without catching the shooter at x=0.
    private static readonly Weapon CleanWinWeapon = new(ShotSpeed, 200.0, 50.0);
    private static readonly Weapon NoDamageWeapon = new(ShotSpeed, 0.0, 0.0);

    private static readonly FireAction CleanWinShot = new(45.0, ShotSpeed, CleanWinWeapon);
    private static readonly FireAction NoopShot     = new(90.0, 0.5, NoDamageWeapon);

    private const double CleanWinTargetX = 255.0;   // ≈ 45° / 50 m/s landing point

    private const MatchOutcome ExpectedOutcome      = MatchOutcome.Team0Wins;
    private const int          ExpectedWinningTeam  = 0;

    private static int Main(string[] args)
    {
        bool check = HasFlag(args, "--check");

        try
        {
            string contentRoot = ResolveContentRoot(args);

            Console.WriteLine($"Vektram Match Host — Sim v{SimVersion.Current}");
            Console.WriteLine(new string('-', 56));

            LoadAndReportContent(contentRoot);

            Console.WriteLine();
            MatchResult result = RunLockedMatch();
            PrintResult(result);

            bool ok = result.Outcome == ExpectedOutcome && result.WinningTeamId == ExpectedWinningTeam;
            Console.WriteLine();
            Console.WriteLine($"Expected (suite-locked): {ExpectedOutcome} / team {ExpectedWinningTeam}");
            Console.WriteLine($"Verification: {(ok ? "PASS" : "FAIL")}");

            if (check)
                return ok ? 0 : 1;

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Host error: {ex.Message}");
            return 2;
        }
    }

    // ── Content loading (host does I/O; sim parses the supplied text) ──────────────

    private static void LoadAndReportContent(string contentRoot)
    {
        string dataDir = Path.Combine(contentRoot, "data");
        string Read(string fileName) => File.ReadAllText(Path.Combine(dataDir, fileName));

        BallCatalog      balls     = BallCatalog.FromJson(Read("balls.json"));
        CombatTuning     tuning    = CombatTuning.FromJson(Read("combat.json"));
        ElementTable     elements  = ElementTable.FromJson(Read("elements.json"));
        ItemCatalog      items     = ItemCatalog.FromJson(Read("items.json"));
        EquipmentCatalog equipment = EquipmentCatalog.FromJson(Read("equipment.json"));

        // Cross-catalog integrity, exercised live outside the test harness.
        items.ValidateBallReferences(balls);

        Console.WriteLine($"Content: {contentRoot}");
        Console.WriteLine($"  balls.json     → {balls.Count} shells parsed");
        Console.WriteLine($"  items.json     → {items.Count} items parsed (ball refs validated)");
        Console.WriteLine($"  equipment.json → {equipment.Count} pieces parsed (modifier stack ready)");
        Console.WriteLine($"  combat.json    → tuning parsed (guardReduceCap {tuning.GuardReduceCap.ToString(CultureInfo.InvariantCulture)})");
        Console.WriteLine($"  elements.json  → table parsed (Fire vs Water advantage {elements.Advantage(Element.Fire, Element.Water).ToString(CultureInfo.InvariantCulture)})");
    }

    // ── Authoritative match (the sim is the source of truth) ───────────────────────

    private static MatchResult RunLockedMatch()
    {
        IProjectileSimulator projectileSim = new ProjectileSimulator();
        var sim = new MatchSimulator(projectileSim);

        var c0 = new Combatant(new Vec2D(0.0, 0.0), StartHp, CombatantStats.Default);
        var c1 = new Combatant(new Vec2D(CleanWinTargetX, 0.0), StartHp, CombatantStats.Default);

        var entries = new[]
        {
            new CombatantEntry(c0, 0, new FixedActionAgent(CleanWinShot)),
            new CombatantEntry(c1, 1, new FixedActionAgent(NoopShot)),
        };

        // rules: null → CombatRules.Default, exactly as the locked suite scenario runs.
        return sim.Run(entries, MatchOptions.Default, FlatTerrain.Ground, WorldEnvironment.Default, Seed);
    }

    private static void PrintResult(MatchResult result)
    {
        Console.WriteLine("Authoritative result:");
        Console.WriteLine($"  Outcome       : {result.Outcome}");
        Console.WriteLine($"  WinningTeamId : {(result.WinningTeamId.HasValue ? result.WinningTeamId.Value.ToString(CultureInfo.InvariantCulture) : "—")}");
        Console.WriteLine($"  TurnCount     : {result.TurnCount}");
        Console.WriteLine($"  Turn log ({result.Log.Count} turns):");

        foreach (TurnEvent turn in result.Log)
        {
            Console.WriteLine(
                $"    turn {turn.TurnNumber}: actor {turn.ActingCombatantIndex} " +
                $"impact ({Fmt(turn.ImpactPoint.X)}, {Fmt(turn.ImpactPoint.Y)})" +
                ItemUseSuffix(turn.ItemUse));

            for (int i = 0; i < turn.CombatantResults.Count; i++)
            {
                CombatantTurnResult r = turn.CombatantResults[i];
                if (r.DamageReceived <= 0.0)
                    continue;

                Console.WriteLine(
                    $"        combatant {i}: -{Fmt(r.DamageReceived)} HP " +
                    $"({Fmt(r.HpBefore)} → {Fmt(r.HpAfter)})" +
                    (r.IsCrit ? " CRIT" : string.Empty) +
                    (r.IsMiss ? " MISS" : string.Empty));
            }
        }
    }

    private static string ItemUseSuffix(TurnItemUse? itemUse)
    {
        if (itemUse is null)
            return string.Empty;

        TurnItemUse use = itemUse.Value;
        string state = use.Applied ? "applied" : "rejected";
        return $" [item {use.ItemId}: {state}]";
    }

    private static string Fmt(double value) =>
        value.ToString("0.###", CultureInfo.InvariantCulture);

    // ── Content path resolution ────────────────────────────────────────────────────

    private static string ResolveContentRoot(string[] args)
    {
        string? overridePath = GetOption(args, "--content");
        if (overridePath is not null)
        {
            if (!File.Exists(Path.Combine(overridePath, "data", "balls.json")))
                throw new DirectoryNotFoundException(
                    $"--content '{overridePath}' does not contain data/balls.json.");
            return overridePath;
        }

        foreach (string start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            DirectoryInfo? dir = new(start);
            while (dir is not null)
            {
                string candidate = Path.Combine(dir.FullName, "content");
                if (File.Exists(Path.Combine(candidate, "data", "balls.json")))
                    return candidate;
                dir = dir.Parent;
            }
        }

        throw new DirectoryNotFoundException(
            "Could not locate the /content directory. Run from inside the repo or pass --content <dir>.");
    }

    // ── Tiny arg helpers ────────────────────────────────────────────────────────────

    private static bool HasFlag(string[] args, string flag)
    {
        foreach (string arg in args)
            if (string.Equals(arg, flag, StringComparison.Ordinal))
                return true;
        return false;
    }

    private static string? GetOption(string[] args, string name)
    {
        for (int i = 0; i < args.Length - 1; i++)
            if (string.Equals(args[i], name, StringComparison.Ordinal))
                return args[i + 1];
        return null;
    }

    /// <summary>
    /// Minimal host-side <see cref="IAgent"/> that always submits one fixed action. Implementing
    /// the agent seam is a host responsibility; the sim stays agnostic to the input source.
    /// </summary>
    private sealed class FixedActionAgent : IAgent
    {
        private readonly FireAction _action;

        public FixedActionAgent(FireAction action) => _action = action;

        public FireAction ChooseAction(MatchState state) => _action;
    }
}
