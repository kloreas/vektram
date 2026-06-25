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
| `--check` | Exit `0` if the live result matches the suite-locked outcome, `1` if it diverges. For CI / determinism gating. (Without it the host always exits `0` on a clean run.) |
| `--content <dir>` | Use an explicit content root instead of auto-discovery. Must contain `data/balls.json`. |

Exit codes: `0` success, `1` verification mismatch (only under `--check`), `2` host error
(e.g. content not found).
