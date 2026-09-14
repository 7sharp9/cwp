## 2026-09-14 - TASK-032 - Suppression and exposure model

**Owner:** Dave with coding-agent assistance
**Branch:** `task-032-suppression-and-exposure` off `main` at the TASK-031
merge (`Merge TASK-031: Basic hitscan combat and directional cover effects
(B-019)`). Committed locally; not pushed.
**Environment:** Windows 11 Pro 26200; .NET SDK 10.0.303; FSharp.Core
10.1.303; xUnit 2.9.3; FsCheck 3.3.4
**Status change:** TASK-032 drafted `-> ready -> review`; backlog B-020
`proposed -> review`

Realises `docs/04` section 12.8's "create suppression independent of a hit"
bullet and section 12.9 (State consequences, previously fully no-op) for its
"update suppression decay" bullet. Turns Combat into a phase that also writes
`AgentState.Suppression`, and State consequences into a real phase that
decays it every tick. No behavioural wiring into Appraisal, reappraisal,
movement, or executor behaviour — deliberately scoped to the raw mechanic
only (B-021's job).

### Central decisions (confirmed with Dave 2026-09-14 before the phase bodies)

- **A - mechanic only; no consequence to Appraisal, reappraisal, movement, or
  executor behaviour.** Confirmed directly with Dave via a scope question
  before drafting: `AgentState.Suppression` is real per-tick state Combat
  creates and State consequences decays, but nothing reads it yet. B-021
  ("stress, discipline, trust, and bounded reappraisal") is explicitly where
  suppression starts affecting decisions.
- **B - suppression created in Combat, independent of a hit, mitigated by
  directional cover.** A miss still suppresses, less than a hit
  (`docs/04` section 12.8's own words). "The exposure model" in the backlog
  title is the same directional-`Terrain.cover` geometry Combat already uses
  for hit chance (`Appraisal.attackDirection`), reused for suppression gain.
- **C - suppression decays every tick in State consequences, unconditionally.**
  A flat integer decay, floored at 0, applied every tick regardless of
  whether the agent was shot at that tick. Combat runs before State
  consequences, so a same-tick hit's gain and that tick's decay both apply,
  in that order — net gain on a hit/miss tick, net loss on a quiet one.
- **D - new leaf `src/CommandoWar.Sim/Suppression.fs`.** `SuppressionConfig` +
  `Suppression.gain` / `.raise` / `.decay`, pure, total. `Combat.fs` itself is
  completely unchanged.
- **E - `AgentState.Suppression: int` is genuine canonical state;
  `Canonical.FormatVersion` bumps 4 -> 5.** The `Order`/`Disposition`
  precedent, not the `Discipline` one — it changes every tick from gameplay
  events and cannot be recomputed from `Position` alone. Defaults to `0`, no
  scenario-authored override (the `Progress` precedent).
- **F - no new event type; a new sparse `Overlay.AgentSuppression`.** A rise
  is a deterministic function of the already-emitted `ShotFired`; decay is
  silent (the `Progress` precedent). The overlay follows the
  `UndeliveredOrder`/`KnownContact` sparse shape — one entry only for
  `Suppression > 0` — not `AgentCommitment`'s unconditional-per-agent one, to
  avoid a `0` line on every agent in every non-combat golden.
- **G - no new corpus entry; decay and clamping proved on the pure
  functions.** `open-engagement`'s agents are already in range/LOS from tick
  1, so its existing golden naturally shows a real `Suppression` value with
  no new scenario needed.
- **H - no ADR.** New leaf, new canonical field via the existing ADR-0002
  amendment, no new dependency.

### Correction found during implementation (Decision E/G wording)

The task file's acceptance criteria originally said "a hit raises the
target's Suppression by exactly `Suppression.gain` recomputed from the
post-tick cells and cover" — this is imprecise. Because Combat and State
consequences both run within the *same* tick (Decision C), the actually
observable post-tick value is `Suppression.gain(...) |> Suppression.raise 0
|> Suppression.decay` — the gain net of that same tick's unconditional decay,
not the raw gain alone. Confirmed by hand on the `open-engagement`
tick-1 golden: agent 0 is hit (`GainOnHit` 400, no cover) then decays
(`DecayPerTick` 50) to `350`; agent 1 is missed (`GainOnMiss` 150, no cover)
then decays to `100` — both match `docs/diagnostics/open-engagement-tick-001.*`.
All `SimulationTests` facts and the `DeterminismPropertyTests` property were
written against this corrected, actually-observable behaviour from the
start; only the task file's prose needed the correction (see `[[
suppression-decay-same-tick]]`-style note — this is the honest surface of a
same-tick gain-then-decay design, not a bug).

### Suppression constants (`src/CommandoWar.Sim/Suppression.fs`)

| Constant | Value | Rationale |
|---|---:|---|
| `MaxSuppression` | 1000 | Matches `Contact.Confidence` / `CombatConfig`'s `0..1000` scale — headroom for a future B-021 suppression-band trigger. |
| `GainOnHit` | 400 | A solid hit meaningfully suppresses without instantly maxing a single shot. |
| `GainOnMiss` | 150 | Noticeably less than a hit, but non-zero ("independent of a hit", docs/04 section 12.8). |
| `CoverMitigationPerLevel` | 100 | The `CombatConfig.CoverMitigationPerLevel` (150) precedent, same units, independently tunable — cover softens the psychological effect by less than it softens the physical hit chance. |
| `DecayPerTick` | 50 | "Decays when safe" (docs/05 section 8) — no numeric target given; roughly 8 ticks to clear a single hit's net gain, faster under continuous fire's net-positive balance. |

### Event-trace and hash impact (`Canonical.FormatVersion` 4 -> 5, full re-pin)

Every corpus entry, the fixture, and `envelope-full` re-pin — the format bump
touches every `writeAgent` call regardless of behaviour. **Tick counts and
event counts are unchanged everywhere** (no new event type, no new phase
branch that can fail or diverge control flow) — confirmed by diffing every
`.md` for a changed "Tick count" or "Domain events" line (none found, only
hash-line and (for the four affected entries) new overlay-text changes).

| Entry | Suppression ever non-zero | Note |
|---|---|---|
| `open-engagement` | yes, from tick 1 | Both agents fire and are fired on from tick 1 (TASK-031); tick 1's golden shows agent 0 at 350/1000 (hit, decayed), agent 1 at 100/1000 (miss, decayed). |
| `perception-contact` | yes, from tick 5 | The tick TASK-031 pinned as the first-combat tick. |
| `exposed-approach` | yes, from tick 2 | The tick TASK-031 pinned as the first-combat tick; tick 1 (the TASK-029 Godot demo's pinned frame) has no combat yet, so its `Suppression` is still 0 there — only its *hash* moves (format bump), not its behaviour. |
| `demo.html` (not a corpus entry) | yes, from tick 8 | The tick TASK-031 pinned as the first-combat tick. |
| every other entry, the fixture, `envelope-full` | no, always 0 | No agent is ever shot at — byte-layout-only re-pin. |

The TASK-029 Godot demo's `--selfcheck` pinned hash
(`src/CommandoWar.Client.Godot/src/AppraisalDemoScene.cs`,
`src/CommandoWar.Client.Godot/README.md`) also moved with the format bump
(`exposed-approach` tick 1: `0xB03F8419E55F3592` -> `0x2FA6E43B32599EE5`) —
not originally listed in "Allowed scope", found necessary because the bump
moves every hash regardless of behaviour; both updated. `content/replays/CORPUS.md`
gained a new "Re-pinned by TASK-032" paragraph (the TASK-031 "Re-pinned by
TASK-031" paragraph is left untouched as a historical record, the
`Canonical.fs` version-history-comment precedent — the new paragraph
supersedes its "unaffected" claim for current readers without rewriting it).

### Verification

- `dotnet build CommandoWar.slnx -c Release`: 0/0 before and after.
- `dotnet test CommandoWar.slnx -c Release`: `265 -> 274` green. New: +8
  `SimulationTests` facts (`Suppression.gain` hit > miss; `Suppression.gain`
  strictly decreases as cover increases, floored at 0; `Suppression.raise`
  accumulates and clamps at `MaxSuppression`; `Suppression.decay` floored at
  0; a qualifying shot raises the target's `Suppression` net of the
  same-tick decay, data-driven off the actual `ShotFired.hit` value; two
  shooters engaging the same target the same tick compose their gains; an
  already-suppressed agent decays by exactly `DecayPerTick` on a quiet tick;
  an agent never shot at stays at `Suppression = 0` indefinitely; the
  existing combat-determinism fact extended to also compare both runs'
  `Suppression` arrays), +1 `DeterminismPropertyTests` property 11 (`MaxTest
  = 200`, reusing `appraisalCaseGen` unchanged: every agent's post-tick
  `Suppression` reproducible from a fresh recompute — pre-tick value folded
  with `Suppression.gain`/`.raise` over this tick's targeting `ShotFired`
  events, then one `Suppression.decay`). The `open-engagement` `FireLine`
  `DiagnosticsTests` fact was extended in place (not a new fact) with
  `AgentSuppression` overlay assertions.
- `cwheadless corpus`: 12/12 PASS (no new entry); `--regenerate` twice in a
  row is a byte-identical zero diff (idempotent, confirmed via `md5sum`).
- `cwheadless fixture`: format 5, `36` events unchanged, `0` draws
  (enemy-free).
- `cwheadless replay-file content/replays/envelope-full.cwreplay`:
  checkpoints OK at canonical 5, `24` ticks / `78` events unchanged
  (enemy-free scenario).
- `dotnet list src/CommandoWar.Sim/CommandoWar.Sim.fsproj package
  --include-transitive`: `FSharp.Core` only.
- Source scan of `src/CommandoWar.Sim` for
  `float|stopwatch|datetime|system\.random|godot`: clean (comment mentions
  only, pre-existing).
- `git status --porcelain`: matches the task file's "Allowed scope" plus the
  two not-originally-listed Godot-client touches (the `--selfcheck` pinned
  hash); nothing under the client spikes, `src/_scratch`, `bench/`,
  `content/benchmarks/BASELINE.md`.

### Unresolved / follow-ups

- No behaviour yet reads `AgentState.Suppression` — B-021 (resolve threshold,
  suppression-band reappraisal trigger) and later executor/`Suppress`-order
  work (B-030) are what make it matter to gameplay, deliberately deferred
  (Decision A).
- `docs/05` section 8's "raises assault pressure" and "may trigger taking
  cover" remain named, not built — no system models assault pressure or a
  take-cover response yet.

### Review

- Reviewer: Dave
- Accepted: yes (2026-09-14)
- Notes: Re-verified independently before accepting rather than trusting the
  implementation-time claims: `dotnet build CommandoWar.slnx -c Release` 0/0;
  `dotnet test` `Passed: 274`; `cwheadless corpus` 12/12 PASS; `cwheadless
  fixture` format 5, `36` events unchanged; `cwheadless replay-file
  content/replays/envelope-full.cwreplay` checkpoints OK at canonical 5, `78`
  events unchanged; `git status --porcelain` clean; `dotnet list
  src/CommandoWar.Sim package --include-transitive` `FSharp.Core` only. All
  match the ledger detail above exactly. Mechanic-only scope (Decision A) is
  the right cut — `AgentState.Suppression` exists and is genuinely canonical
  but nothing reads it yet, which is honest given B-021 is where it starts
  mattering. Merged to `main` (`--no-ff`, branch
  `task-032-suppression-and-exposure` deleted; not pushed).
