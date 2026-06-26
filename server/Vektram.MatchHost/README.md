# Vektram.MatchHost

The thinnest authoritative match host. It exists to prove one thing outside the test
harness: **the authoritative simulation runs host-side, in a real .NET process, on the
real `/content` data, and is deterministic.**

xUnit already locks the sim's behaviour, but tests run the sim in-process under the test
runner against fixtures. This host runs it for real — it does the file I/O the sim
deliberately doesn't, loads the canonical `/content/data/*.json`, parses it through the
sim's pure catalog parsers, then runs ONE full match server-side and prints the
authoritative result + turn log. The locked 1v1 scenario (seed 0) reproduces the
suite-locked outcome `Team0Wins / team 0`, so the host self-verifies: it prints
`Verification: PASS` when the live host run agrees with the test-locked answer.

## Loadout + item demo (systems #4 and #3, live in the product)

After the anchor, the host runs a second, **additive** demonstration that proves the
equipment/stat stack (#4) and item use (#3) work in the product, not only in unit tests:

- **#4 — loadout → stats.** It builds a `Loadout` from real `equipment.json` ids
  (`weapon_recruit_cannon` + `armor_recruit_plate`) and resolves it through
  `LoadoutResolver.Resolve` into effective `CombatantStats` that go into the roster — instead
  of `CombatantStats.Default`.
- **#3 — item consumed mid-match.** It drives `MatchController.ResolveTurn(TurnAction)`
  **directly** (the server-authoritative "validated TurnAction per turn" path; `MatchSimulator`
  and `IAgent` are untouched), so the equipped combatant genuinely consumes a `shell_heavy`
  (a `GrantBall` item) on its turn.

The demo prints and **self-checks four observable invariants**: the resolved `Attack` exceeds
the default, the item count decremented (`1 → 0`), the `ItemUse` was `Applied`, and the
equipped+item result differs from a Default/no-item control run. It reports a second line,
`Loadout+Item demo: PASS`. The loadout effect is deliberately *small* (anti-inflation,
ADR-0006) — the equipped+item side wins **faster** than the control, it does not one-shot.

The anchor match is **bit-for-bit unchanged** by all of this; the demo only appends output
after the anchor's `Verification: PASS`.

## Role

This is the embryo of the permanent **.NET match service** — the single place that runs
the authoritative simulation. Per ADR-0001 the sim is the one source of truth and must run
in exactly one component, and this is it: **the ONLY place that imports `/sim`**. Nakama
(Go/Lua/TS) will sit in front of this service for matchmaking, sessions, and transport and
**delegate** simulation to it — Nakama never hosts the sim, because it cannot load our
`netstandard2.1` assembly.

## Running

```bash
dotnet run --project server/Vektram.MatchHost
```

Run from inside the repo; the host walks up from the working directory (and its own base
directory) to locate `/content`.

### Flags

| Flag | Effect |
|------|--------|
| `--check` | Exit `0` only if **both** the anchor matches the suite-locked outcome **and** the loadout+item demo passes its four invariants; `1` if either diverges. For CI / determinism gating. (Without it the host always exits `0` on a clean run.) |
| `--content <dir>` | Use an explicit content root instead of auto-discovery. Must contain `data/balls.json`. |

Exit codes: `0` success, `1` verification mismatch (only under `--check`), `2` host error
(e.g. content not found).
