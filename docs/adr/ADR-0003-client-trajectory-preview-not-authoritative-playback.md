# ADR-0003: Client Uses Shared Sim for Aiming Preview Only — Not Authoritative Playback

**Date:** 2026-06-18
**Status:** Accepted
**Deciders:** Architecture session, Phase 1

---

## Context

ADR-0001 established that the server is authoritative and that the client uses the
shared `/sim` library locally. ADR-0002 noted that cross-platform bit-determinism is
not required under this architecture. A refinement is needed to record precisely *why*
it is not required and what the client is actually doing with the shared sim.

Two plausible client-side uses of the shared sim exist:

1. **Aiming preview** — show the player a live trajectory arc as they aim, before
   the shot is submitted. No game state is altered; the result is purely visual.
2. **Authoritative playback** — after the server broadcasts its result, the client
   re-runs the same sim to reproduce the trajectory for animation/replay purposes,
   relying on getting the same outcome as the server.

The choice between these two models has implications for determinism requirements
and network protocol design.

---

## Decision

### The client runs the shared sim for **aiming preview only**.

The server does not transmit a seed or any internal sim state to the client.
Instead, it transmits the **authoritative outcome directly**:

- The resolved trajectory (downsampled to a suitable playback frame rate if needed).
- The impact point and time.
- The damage dealt and resulting HP values.

The client receives this outcome and plays it back using the trajectory data from the
server — it does not re-run the sim to reproduce the authoritative result.

The client **does** run the sim locally for the **aiming preview arc** while the
player is aiming. This preview is:

- Ephemeral and purely cosmetic.
- Never used to make any game-state decision.
- Not required to match the server result bit-for-bit; it uses the player's current
  aim inputs as a best guess before the shot is committed.

---

## Consequences

### Positive

- **Cross-platform bit-determinism is not required.** The server's result is the
  only canonical result. The client's local sim is visual-only. Differences between
  JIT backends, FPU configurations, or floating-point rounding modes on different
  devices are irrelevant to correctness.
- **No RNG seed transmission.** The server never sends its RNG seed to clients.
  This is a meaningful cheat-resistance property: a client cannot predict or
  pre-compute randomised outcomes.
- **Simpler network protocol.** The server sends trajectory + impact + outcome as
  a structured event stream. No "re-simulate from seed" handshake is needed.
- **No silent desync class of bugs.** Client/server sim divergence cannot silently
  produce different game outcomes because the client never uses its local result
  authoritatively.
- **Preview quality is independent of server precision.** The client can tune the
  aiming preview (e.g. reduce step count for mobile battery) without risking any
  correctness issue.

### Negative / Trade-offs

- **Visible correction artifact.** If the player's aiming preview arc is
  meaningfully wrong (e.g. they are aiming at lag-adjusted wind values), the
  animation will visibly snap from the previewed arc to the server's authoritative
  trajectory on receipt. Mitigation: use the same wind values the server will use,
  transmitted with the match state, so the preview is accurate for typical inputs.
- **Server must transmit full trajectory.** For smooth client-side animation the
  server must include enough trajectory points in the event payload. Downsampling
  reduces bandwidth at the cost of animation fidelity; the exact rate is a
  per-weapon tuning concern, not an architecture constraint.

---

## Alternatives Considered

| Option | Reason Not Chosen |
|--------|------------------|
| Client re-simulates from server seed for playback | Requires seed transmission (cheat risk), requires cross-platform bit-determinism, adds a "desync detection" protocol. No benefit under server-authoritative architecture. |
| Client re-simulates from server seed but discards result if it diverges | Complex error-handling; still transmits seed; adds a reconciliation path with no clear correctness benefit. |
| Client runs no sim at all (server sends trajectory, client just renders) | Viable; eliminated only because local aiming preview requires running the sim. Preview-only use is acceptable and useful. |
