namespace CommandoWar.Sim

/// Canonical byte encoding of authoritative world state
/// (docs/04_SIMULATION_SPEC.md section 17).
///
/// The encoding is the single input to state hashing and to any future
/// authoritative serialisation. It is deliberately isolated here so a
/// different byte layout or serialiser can replace it without changing
/// simulation behaviour: nothing in the phase pipeline depends on these bytes.
///
/// Rules:
///   * fields are written in a fixed order;
///   * integers use fixed width and big-endian byte order (independent of the
///     host architecture);
///   * entities are written in ascending id order with an explicit sort;
///   * optional values carry an explicit present/absent tag;
///   * presentation state (snapshots, events, phase traces) is never included;
///   * the format version is the first field, so a reader can reject an
///     unknown layout.
[<RequireQualifiedAccess>]
module Canonical =

    /// The canonical-format version. Bump whenever the byte layout below
    /// changes in any way. Hashes and replay records record this value.
    ///
    /// 2 (TASK-018): `writeAgent` gained `Progress` (docs/04 section 8
    /// sub-cell movement progress) — genuine new per-tick agent state, not a
    /// derived cache, so every pinned hash moves. On every scenario pinned
    /// before this version every traversed cell costs `Terrain.BaseMoveCost`
    /// (threshold = increment = 1), so `Progress` is 0 at every post-tick
    /// checkpoint: the move is a byte-layout change, not a behaviour change.
    ///
    /// 3 (TASK-026): `encode` gained a tactical-knowledge section
    /// (`WorldState.TacticalKnowledge`, docs/04 section 12.4) — the friendly
    /// squad's retained contact picture, which carries per-tick memory
    /// (`Contact.LastSeenTick`, the decaying `Contact.Confidence`) that no
    /// other field reproduces, so under the ADR-0002 amendment it must be in
    /// the canonical image. `AgentState.VisibleContacts` (TASK-026) is NOT
    /// written — it is a derived cache, like `Route`. Every scenario pinned
    /// before this version has zero enemy deployments, so `TacticalKnowledge`
    /// is empty at every checkpoint and the moved hashes are a byte-layout
    /// change, not a behaviour change (tick counts and event counts unchanged;
    /// TASK-026 ledger).
    ///
    /// 4 (TASK-028): `writeAgent` gained the `AgentState.Order` and
    /// `AgentState.Disposition` sections (docs/04 section 12.5 order
    /// appraisal). Both carry per-tick memory no other field reproduces — an
    /// order's `IssuedAtTick` lives only in the command envelope, and a
    /// `Refused` disposition persists with its structured reasons for an idle
    /// agent — so under the ADR-0002 amendment they are in the canonical
    /// image. `AgentState.Discipline` (TASK-028) is NOT written: it is static
    /// authored scenario data, like `CommunicationAvailable`. Every scenario
    /// pinned before this version issues at most one order per agent along a
    /// clear enemy-free route (except `blocked-goal`, whose order is now
    /// `Unable(NoKnownRoute)` at appraisal — same tick count, same event
    /// count), so the moved hashes are a byte-layout change plus one
    /// `OrderAppraised` event per order; tick counts are unchanged (TASK-028
    /// ledger).
    ///
    /// 5 (TASK-032): `writeAgent` gained `AgentState.Suppression` (docs/04
    /// sections 12.8/12.9, backlog B-020). Genuine per-tick memory — it
    /// changes every tick from gameplay events (the Combat phase's
    /// `Suppression.gain`, the State-consequences phase's `Suppression.decay`)
    /// and cannot be recomputed from `Position` alone, the `Order` /
    /// `Disposition` precedent, not the `Discipline` one. Every scenario
    /// pinned before this version has no agent that is ever shot at, so
    /// `Suppression` is 0 at every checkpoint and the moved hashes are a
    /// byte-layout change, not a behaviour change; tick counts and event
    /// counts are unchanged everywhere (TASK-032 ledger).
    ///
    /// 6 (TASK-033): `writeAgent` gained `AgentState.SuppressionBand` and
    /// `AgentState.Stress` (docs/04 section 12.9, `docs/05` sections 14/15,
    /// backlog B-021). Both are genuine per-tick memory on the `Suppression`
    /// precedent: `SuppressionBand` is a hysteresis latch that cannot be
    /// recomputed from `Suppression` alone (it also depends on which side of
    /// the band it was already on), and `Stress` changes every tick from
    /// gameplay events (State consequences' `Stress.gain` / `.decay`) and
    /// cannot be recomputed from `Position` alone. **Unlike TASK-032,
    /// `AgentState.Discipline` is deliberately NOT written here** — B-021 as
    /// originally scoped in the backlog said it would move Discipline into
    /// the canonical image, but Discipline itself never mutates in this
    /// task's cut (only the new `Stress` field does), so by the same
    /// mutability rule that keeps `CommunicationAvailable` out, it stays
    /// static authored data (confirmed with Dave 2026-09-14; corrects the
    /// comment on `writeAgent` below and the B-021 backlog row). Every
    /// scenario pinned before this version has no agent ever shot at or ever
    /// in another side's `VisibleContacts`, so `SuppressionBand` is `false`
    /// and `Stress` is 0 at every checkpoint and the moved hashes are a
    /// byte-layout change, not a behaviour change; tick counts and event
    /// counts are unchanged everywhere (TASK-033 ledger).
    ///
    /// 7 (TASK-034): `encode` gained a hostile-tactical-knowledge section
    /// (`WorldState.HostileTacticalKnowledge`, docs/04 section 12.4, backlog
    /// B-022 partial), reusing `writeContact` — the identical
    /// `TacticalKnowledge` precedent, symmetric to the friendly side. Genuine
    /// per-tick memory (`LastSeenTick`, the decaying `Confidence`), so under
    /// the ADR-0002 amendment it is in the canonical image. Unlike TASK-026's
    /// original 2 -> 3 bump, this one is **not** behaviour-neutral: every
    /// scenario pinned before this version where a hostile currently gains a
    /// friendly in `VisibleContacts` (`open-engagement`, `perception-contact`,
    /// `exposed-approach`) now also carries a genuine new non-zero
    /// `HostileTacticalKnowledge` entry from the tick that first happens —
    /// real new state, not a byte-layout artefact. Every other pinned entry is
    /// enemy-free or one-sided and re-pins behaviour-neutrally. Tick counts
    /// and event counts are unchanged everywhere (TASK-034 ledger).
    [<Literal>]
    let FormatVersion = 7

    /// Fixed-width big-endian byte sink. Kept private: callers see only
    /// `encode`.
    type private Writer() =
        let buffer = ResizeArray<byte>(256)

        member _.U8(value: byte) = buffer.Add value

        member _.U32(value: uint32) =
            buffer.Add(byte (value >>> 24))
            buffer.Add(byte (value >>> 16))
            buffer.Add(byte (value >>> 8))
            buffer.Add(byte value)

        member _.U64(value: uint64) =
            for shift in [ 56; 48; 40; 32; 24; 16; 8; 0 ] do
                buffer.Add(byte (value >>> shift))

        member this.I32(value: int) = this.U32(uint32 value)
        member this.I64(value: int64) = this.U64(uint64 value)
        member _.ToArray() : byte[] = buffer.ToArray()

    let private sideCode (side: Side) : int =
        match side with
        | Friendly -> 0
        | Hostile -> 1

    let private writeRandom (w: Writer) (r: RandomState) =
        w.I32(int r.Algorithm)
        w.I32 r.AlgorithmVersion
        w.U64 r.Word
        w.U64 r.Draws

    // `AgentState.Route` (TASK-015) is deliberately NOT written: it is a
    // derived cache, a pure deterministic function of `Position`,
    // `Destination`, and the immutable `Terrain`, so it cannot diverge tick to
    // tick and hashing it would only re-pin every fixture for a constant
    // (docs/04 section 17, "derived caches either excluded or normalised"; the
    // ADR-0002 amendment made the same call for `Terrain`). `Progress`
    // (TASK-018) IS written: unlike `Route`, it cannot be recomputed from
    // `Position` alone (`Position` does not change while an edge is in
    // progress), so it is genuine new state, not a derived cache.
    //
    // `AgentState.CommunicationAvailable` (TASK-027) is also deliberately NOT
    // written: at this stage it is STATIC authored scenario data
    // (`Deployment.CommunicationAvailable`), set once at tick 0 and never
    // mutated during a run, so — like `Terrain` under the ADR-0002 amendment —
    // it cannot diverge and is out of the canonical image. A comms-derived
    // behaviour bug still surfaces in the hash within one tick via `Position`.
    // When B-016b makes comms availability per-tick mutable it enters the
    // image and `FormatVersion` bumps then.
    //
    // `AgentState.Discipline` (TASK-028) is likewise NOT written — static
    // authored scenario data (`Deployment.Discipline`), the same argument as
    // `CommunicationAvailable`. A discipline-driven behaviour difference
    // surfaces in the hash within one tick via `Disposition` / `Position`.
    // TASK-033 (backlog B-021) confirmed Discipline stays exactly here: it
    // never mutates even once B-021's Stress field lands, so it never enters
    // the canonical image (corrects this comment's earlier "B-021 makes
    // discipline dynamic" claim).
    //
    // `AgentState.Order` and `AgentState.Disposition` (TASK-028) ARE written:
    // they carry per-tick memory no other field reproduces (an order's
    // `IssuedAtTick`; a persisting `Refused` outcome and its reasons), so
    // under the ADR-0002 amendment they are in the canonical image.
    //
    // `AgentState.Suppression` (TASK-032) IS written, on the identical
    // argument: it changes every tick from gameplay events (Combat's gain,
    // State consequences' decay) and cannot be recomputed from `Position`
    // alone.
    //
    // `AgentState.SuppressionBand` and `AgentState.Stress` (TASK-033) ARE
    // written, the `Suppression` precedent: a hysteresis latch that depends
    // on more than the current `Suppression` value, and a field that changes
    // every tick from gameplay events and cannot be recomputed from
    // `Position` alone.

    let private reasonCode (r: DecisionReason) : int =
        match r with
        | NoKnownRoute -> 0
        | RouteTooExposed _ -> 1

    let private writeReason (w: Writer) (r: DecisionReason) =
        w.I32(reasonCode r)

        match r with
        | NoKnownRoute -> ()
        | RouteTooExposed threat ->
            match threat with
            | None -> w.U8 0uy
            | Some id ->
                w.U8 1uy
                w.I32(AgentId.value id)

    let private writeDisposition (w: Writer) (d: OrderDisposition) =
        match d with
        | Accepted -> w.I32 0
        | Refused(primary, supporting) ->
            w.I32 1
            writeReason w primary
            w.I32 supporting.Length
            for r in supporting do
                writeReason w r
        | Unable(primary, supporting) ->
            w.I32 2
            writeReason w primary
            w.I32 supporting.Length
            for r in supporting do
                writeReason w r

    let private writeOrder (w: Writer) (o: ReceivedOrder) =
        w.I32(CommandId.value o.Command)

        match o.Intent with
        | MoveTo target ->
            w.I32 0
            w.I32 target.X
            w.I32 target.Y

        w.I64 o.IssuedAtTick

        w.I32(
            match o.Urgency with
            | Routine -> 0
            | Immediate -> 1
        )

        w.I32(
            match o.RiskTolerance with
            | Cautious -> 0
            | Standard -> 1
            | Aggressive -> 2
        )

    let private writeAgent (w: Writer) (a: AgentState) =
        w.I32(AgentId.value a.Id)
        w.I32(sideCode a.Side)
        w.I32 a.Position.X
        w.I32 a.Position.Y
        w.I32 a.Progress

        match a.Destination with
        | None -> w.U8 0uy
        | Some cell ->
            w.U8 1uy
            w.I32 cell.X
            w.I32 cell.Y

        match a.Order with
        | None -> w.U8 0uy
        | Some o ->
            w.U8 1uy
            writeOrder w o

        match a.Disposition with
        | None -> w.U8 0uy
        | Some d ->
            w.U8 1uy
            writeDisposition w d

        w.I32 a.Suppression
        w.U8(if a.SuppressionBand then 1uy else 0uy)
        w.I32 a.Stress

    // The friendly squad's shared tactical picture (TASK-026,
    // `WorldState.TacticalKnowledge`, docs/04 section 12.4). Genuine per-tick
    // canonical state: `LastSeenTick` and the decaying `Confidence` cannot be
    // recomputed from the current tick's positions. Contacts are written in
    // ascending contact-id order with an explicit count, fixed-width
    // big-endian, exactly like agents. `AgentState.VisibleContacts` is a
    // derived cache and is NOT written (the `Route` precedent).
    let private writeContact (w: Writer) (c: Contact) =
        w.I32(AgentId.value c.Contact)
        w.I32 c.LastKnownCell.X
        w.I32 c.LastKnownCell.Y
        w.I64 c.LastSeenTick
        w.I32 c.Confidence

    /// Encodes authoritative world state to its canonical byte form.
    let encode (world: WorldState) : byte[] =
        let w = Writer()
        w.U32(uint32 FormatVersion)
        w.I64 world.Tick
        w.I32 world.Bounds.Width
        w.I32 world.Bounds.Height
        writeRandom w world.Random

        let agents = world.Agents |> Array.sortBy (fun a -> a.Id)
        w.I32 agents.Length
        for a in agents do
            writeAgent w a

        let contacts = world.TacticalKnowledge |> Array.sortBy (fun c -> c.Contact)
        w.I32 contacts.Length
        for c in contacts do
            writeContact w c

        // The Hostile side's own shared tactical picture (TASK-034, backlog
        // B-022, partial) — the identical `TacticalKnowledge` shape and
        // ordering, reusing `writeContact`.
        let hostileContacts = world.HostileTacticalKnowledge |> Array.sortBy (fun c -> c.Contact)
        w.I32 hostileContacts.Length
        for c in hostileContacts do
            writeContact w c

        w.ToArray()

    /// Names of the top-level canonical sections, in encoding order. Used by
    /// the divergence diagnostic to label the first differing region.
    let private topLevelSections (world: WorldState) : (string * byte[]) list =
        let section name (build: Writer -> unit) =
            let w = Writer()
            build w
            name, w.ToArray()

        [ section "Tick" (fun w -> w.I64 world.Tick)
          section "Bounds" (fun w ->
              w.I32 world.Bounds.Width
              w.I32 world.Bounds.Height)
          section "Random" (fun w -> writeRandom w world.Random)
          section "AgentCount" (fun w -> w.I32 world.Agents.Length)
          section "TacticalKnowledge" (fun w ->
              let contacts = world.TacticalKnowledge |> Array.sortBy (fun c -> c.Contact)
              w.I32 contacts.Length
              for c in contacts do
                  writeContact w c)
          section "HostileTacticalKnowledge" (fun w ->
              let contacts = world.HostileTacticalKnowledge |> Array.sortBy (fun c -> c.Contact)
              w.I32 contacts.Length
              for c in contacts do
                  writeContact w c) ]

    /// Best-effort identification of the first canonical section (or agent)
    /// that differs between two states. Returns `None` when the canonical
    /// encodings are identical. This is a diagnostic aid, not an authoritative
    /// output.
    let firstDifferingSection (expected: WorldState) (actual: WorldState) : string option =
        let expectedSections = topLevelSections expected
        let actualSections = topLevelSections actual

        let topLevelDiff =
            List.zip expectedSections actualSections
            |> List.tryPick (fun ((name, e), (_, a)) -> if e <> a then Some name else None)

        match topLevelDiff with
        | Some name -> Some name
        | None ->
            let expectedAgents = expected.Agents |> Array.sortBy (fun a -> a.Id)
            let actualAgents = actual.Agents |> Array.sortBy (fun a -> a.Id)

            let agentDiff =
                Seq.zip expectedAgents actualAgents
                |> Seq.tryPick (fun (e, a) ->
                    if e <> a then
                        Some $"Agent[{AgentId.value e.Id}]"
                    else
                        None)

            match agentDiff with
            | Some label -> Some label
            | None when encode expected <> encode actual -> Some "Agents"
            | None -> None
