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
    [<Literal>]
    let FormatVersion = 3

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
