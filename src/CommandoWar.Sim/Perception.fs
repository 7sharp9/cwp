namespace CommandoWar.Sim

/// Deterministic perception: what each agent can see, and how the friendly
/// squad's shared tactical picture is built from it (TASK-026, backlog B-015;
/// `docs/04_SIMULATION_SPEC.md` sections 12.3 and 12.4;
/// `docs/05_COMMAND_AND_AGENT_AI.md` section 3).
///
/// This is the **first phase consumer of the `Sight` module** (TASK-012):
/// `Simulation.perception` calls `sweep` and `Simulation.tacticalKnowledge`
/// calls `mergeKnowledge`. It is a leaf, exactly as `Sight` and `Pathfinding`
/// are: pure, total, integer-only, `Sight`- and `Terrain`-only, no event
/// emission (the phase owns that), no mutation of its inputs.
///
/// ## The two operations
///
///   * `sweep terrain agents` derives, for every agent, the opposing-side
///     agents it can currently see — within `PerceptionConfig.SightRange`
///     Chebyshev cells and with `Sight.visible` line of sight. Symmetric:
///     both sides are swept (`docs/05` section 12). The result is a
///     **derived cache** (`AgentState.VisibleContacts`), excluded from
///     `Canonical.encode`.
///   * `mergeKnowledge tick prior sightings` folds this tick's friendly
///     sightings into the retained squad picture: upsert what was seen,
///     age what has gone stale, drop what has expired. The result **is**
///     canonical state (`WorldState.TacticalKnowledge`); it carries memory
///     (`Contact.LastSeenTick`, the decaying `Contact.Confidence`) that the
///     current tick's positions cannot reproduce.
///
/// ## What is deliberately absent (later backlog items)
///
///   * communication range / delay / failure and report aging beyond the
///     simple stale / expire bands — B-016;
///   * order appraisal reading known threats — B-017;
///   * line of fire / combat — B-019;
///   * a hostile squad picture, enemy doctrine reacting to contacts, "the
///     enemy does not target an unobserved position" — B-022;
///   * per-agent private or persistent beliefs, confidence divergence between
///     squad members, a squad / formation grouping — `docs/05` section 17.

/// Every perception threshold, in one place (`docs/05` section 15 "Record
/// every threshold in one configuration structure"). Module literals rather
/// than a `WorldState` field: these values affect authoritative outcomes (so
/// not `SimConfig`, which is non-authoritative host config), but no scenario
/// needs to tune them yet.
[<RequireQualifiedAccess>]
module PerceptionConfig =

    /// Sight-range cap, in cells, as a Chebyshev radius: an agent perceives an
    /// opposing agent only when `max |dx| |dy| <= SightRange` **and**
    /// `Sight.visible` holds. Chebyshev (not Manhattan) because `Sight` is a
    /// diagonal-capable supercover walk, so a diamond cap would exclude
    /// diagonal contacts the walk actually reaches. 10 keeps 50-agent
    /// perception O(n^2) bounded, and on a slice-sized map leaves the far side
    /// unseen — the reason the "Unknown threat" scenario (`docs/05` section 16)
    /// has an unspotted machine gun.
    [<Literal>]
    let SightRange = 10

    /// Full confidence in a contact, on the `docs/04` section 4 `0..1000`
    /// normalised scale. Assigned whenever a friendly sees the contact this
    /// tick.
    [<Literal>]
    let ConfidenceFull = 1000

    /// The single band a stale contact's confidence drops by (`1000 -> 750`).
    /// One drop, not a per-tick decay: `docs/05` section 3 speaks of a
    /// "confidence band", and a small number of integer steps is easier to
    /// reason about and tune.
    [<Literal>]
    let ConfidenceBandDrop = 250

    /// Ticks a contact may go unseen before its confidence drops a band
    /// (`~1 second` at 20 ticks/second). Longer than any brief line-of-sight
    /// flicker, so a contact that blinks in and out of sight does not thrash
    /// (`docs/05` section 15 hysteresis).
    [<Literal>]
    let StaleAfter = 20

    /// Ticks a contact may go unseen before it is removed from the squad
    /// picture entirely and a `ContactExpired` event fires (`~3 seconds` at
    /// 20 ticks/second). Must exceed `StaleAfter`.
    [<Literal>]
    let ExpireAfter = 60

[<RequireQualifiedAccess>]
module Perception =

    /// Chebyshev (chessboard) distance between two cells.
    let chebyshev (a: Cell) (b: Cell) : int =
        max (abs (a.X - b.X)) (abs (a.Y - b.Y))

    /// The opposing-side agents `observer` can currently see, ascending by id.
    /// An agent never sees itself or an ally; range is capped before the
    /// (more expensive) line-of-sight trace.
    let visibleContactsFor (terrain: Terrain) (observer: AgentState) (agents: AgentState[]) : AgentId[] =
        agents
        |> Array.choose (fun other ->
            if
                other.Side <> observer.Side
                && chebyshev observer.Position other.Position <= PerceptionConfig.SightRange
                && Sight.visible terrain observer.Position other.Position
            then
                Some other.Id
            else
                None)
        |> Array.sort

    /// Every agent's current visible contacts, as an array parallel to
    /// `agents` (index `i` is `agents.[i]`'s contacts). Both sides are swept.
    let sweep (terrain: Terrain) (agents: AgentState[]) : AgentId[][] =
        agents |> Array.map (fun a -> visibleContactsFor terrain a agents)

    /// The shared squad picture after this tick's merge, plus the contacts
    /// removed this tick (for `ContactExpired` events).
    ///
    ///   * `tick` — the tick being processed.
    ///   * `prior` — last tick's `WorldState.TacticalKnowledge`, ascending by
    ///     contact id.
    ///   * `seenThisTick` — every contact a friendly saw this tick, mapped to
    ///     the cell it was seen in (its current `Position`). Ascending-by-key
    ///     iteration is guaranteed by `Map`.
    ///
    /// For every contact seen this tick: upsert with the observed cell,
    /// `LastSeenTick = tick`, `Confidence = ConfidenceFull`. For every retained
    /// contact not seen this tick: drop a band once it has been unseen for
    /// `StaleAfter` ticks, remove it once unseen for `ExpireAfter` ticks. The
    /// result is kept ascending by contact id (`docs/04` section 6).
    let mergeKnowledge
        (tick: int64)
        (prior: Contact[])
        (seenThisTick: Map<AgentId, Cell>)
        : Contact[] * Contact[] =
        let expired =
            prior
            |> Array.filter (fun c ->
                not (Map.containsKey c.Contact seenThisTick)
                && tick - c.LastSeenTick >= int64 PerceptionConfig.ExpireAfter)

        let retained =
            prior
            |> Array.choose (fun c ->
                match Map.tryFind c.Contact seenThisTick with
                | Some cell ->
                    Some
                        { c with
                            LastKnownCell = cell
                            LastSeenTick = tick
                            Confidence = PerceptionConfig.ConfidenceFull }
                | None ->
                    let unseen = tick - c.LastSeenTick

                    if unseen >= int64 PerceptionConfig.ExpireAfter then
                        None
                    elif unseen >= int64 PerceptionConfig.StaleAfter then
                        Some { c with Confidence = PerceptionConfig.ConfidenceFull - PerceptionConfig.ConfidenceBandDrop }
                    else
                        Some c)

        let priorIds = prior |> Array.map (fun c -> c.Contact) |> Set.ofArray

        let fresh =
            seenThisTick
            |> Map.toArray
            |> Array.choose (fun (id, cell) ->
                if Set.contains id priorIds then
                    None
                else
                    Some
                        { Contact = id
                          LastKnownCell = cell
                          LastSeenTick = tick
                          Confidence = PerceptionConfig.ConfidenceFull })

        let store = Array.append retained fresh |> Array.sortBy (fun c -> c.Contact)
        store, expired
