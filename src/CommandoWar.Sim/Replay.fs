namespace CommandoWar.Sim

/// Command recording and replay (docs/04_SIMULATION_SPEC.md section 16,
/// docs/09_TEST_STRATEGY.md section 2.4).
///
/// The record types here are versioned and isolated from the simulation: the
/// phase pipeline has no knowledge of them, so a different on-disk shape or
/// serialiser can replace this module without changing authoritative
/// behaviour. Playback rejects unknown versions with typed errors rather than
/// guessing a migration.

/// One accepted command, with the metadata replay needs to reconstruct and
/// audit a run. The record schema is versioned through the enclosing
/// `CommandLog`.
type RecordedCommand =
    { /// The tick whose command-intake phase consumes this command — the
      /// delivery / submission tick, owned by whoever schedules the replay.
      /// Independent of the envelope's `PlayerCommand.IssuedAtTick` (when the
      /// order was *issued*); the two may differ, and the legacy `.cwlog`
      /// format collapses them to one field (TASK-024). Playback keys every
      /// per-tick command batch on this value.
      Tick: int64
      /// Deterministic tie-break order within the tick: unique and ascending.
      Sequence: int
      /// The authoritative command envelope, replayed verbatim.
      Command: PlayerCommand
      /// Who issued the command. Non-authoritative provenance for audit only.
      Issuer: string }

/// An ordered, versioned log of accepted commands.
type CommandLog =
    { /// Log/record schema version. Playback rejects unknown values.
      Version: int
      /// Commands sorted strictly ascending by (Tick, Sequence).
      Commands: RecordedCommand[] }

/// A per-tick authoritative-state hash captured during a run.
type Checkpoint = { Tick: int64; Hash: StateHash }

/// Non-authoritative provenance metadata for a replay record.
type ReplayMeta =
    { /// Source revision or build identifier.
      Build: string
      /// Human-readable scenario label.
      Scenario: string }

/// A self-contained replay: everything needed to reconstruct an authoritative
/// run from tick 0.
type ReplayRecord =
    { /// Replay container format version. Playback rejects unknown values.
      Version: int
      /// Canonical-state format version the checkpoints were hashed under.
      CanonicalFormat: int
      /// Provenance metadata, excluded from authority.
      Meta: ReplayMeta
      /// The seed the initial random stream was built from. Recorded for
      /// audit; the authoritative stream itself travels inside `InitialState`.
      Seed: uint64
      /// The authoritative world at tick 0.
      InitialState: WorldState
      /// Number of ticks the run covers.
      TickCount: int64
      /// The accepted command log.
      Log: CommandLog
      /// Optional per-tick checkpoint hashes captured on the reference run.
      Checkpoints: Checkpoint[] }

/// The reconstructed result of a replay run.
type ReplayOutcome =
    { /// Authoritative world after the final tick.
      FinalState: WorldState
      /// One checkpoint per simulated tick, in tick order.
      TickHashes: Checkpoint[]
      /// Authoritative world after each tick, in tick order. Retained in full
      /// for small-state divergence diagnosis; this does not scale to long
      /// runs and a checkpoint cadence replaces it when the world grows.
      TickStates: WorldState[]
      /// Every domain event emitted across the run, in order.
      Events: DomainEvent[] }

/// Why a replay could not be accepted or run. Every case is explicit; nothing
/// is silently repaired.
type ReplayError =
    | UnsupportedReplayVersion of found: int * supported: int
    | UnsupportedCommandLogVersion of found: int * supported: int
    | UnsupportedCanonicalFormat of found: int * supported: int
    | InitialStateNotAtTickZero of tick: int64
    | SeedInconsistentWithInitialState of recordedSeed: uint64 * initialWord: uint64
    | InvalidTickCount of tickCount: int64
    | NonMonotonicCommandLog of index: int * previous: struct (int64 * int) * current: struct (int64 * int)
    | CommandOutsideReplayRange of index: int * tick: int64 * tickCount: int64
    /// One `CommandId` appears on two different recorded commands in the log
    /// (`firstIndex` before `secondIndex`, in `(Tick, Sequence)` order). A
    /// command id is unique for the life of a run; a reused id is a malformed
    /// log, not a guessed repair (TASK-024). Within-tick duplicates are also
    /// rejected at command intake (`CommandRejection.DuplicateCommandId`);
    /// this is the cross-tick guard, kept in the replay layer because command
    /// identity is not authoritative state.
    | DuplicateCommandIdInLog of id: CommandId * firstIndex: int * secondIndex: int

[<RequireQualifiedAccess>]
module RecordedCommand =

    /// Builds the recorded commands for a single tick from an ordered array of
    /// command envelopes. `Sequence` is assigned from array position.
    let forTick (tick: int64) (issuer: string) (commands: PlayerCommand[]) : RecordedCommand[] =
        commands
        |> Array.mapi (fun i c ->
            { Tick = tick
              Sequence = i
              Command = c
              Issuer = issuer })

[<RequireQualifiedAccess>]
module CommandLog =

    /// The current command-log / command-record schema version.
    [<Literal>]
    let Version = 1

    /// Builds a log, sorting the commands into canonical (Tick, Sequence)
    /// order. Ordering is validated again by `Replay.run`.
    let create (commands: RecordedCommand[]) : CommandLog =
        { Version = Version
          Commands = commands |> Array.sortBy (fun c -> c.Tick, c.Sequence) }

[<RequireQualifiedAccess>]
module ReplayMeta =

    let unspecified: ReplayMeta =
        { Build = "unspecified"; Scenario = "unspecified" }

[<RequireQualifiedAccess>]
module Replay =

    /// The current replay container format version.
    [<Literal>]
    let FormatVersion = 1

    /// Builds a replay record for a run that starts from `initial` (which must
    /// be at tick 0) and covers `tickCount` ticks.
    let record (meta: ReplayMeta) (initial: WorldState) (tickCount: int64) (log: CommandLog) : ReplayRecord =
        { Version = FormatVersion
          CanonicalFormat = Canonical.FormatVersion
          Meta = meta
          Seed = initial.Random.Word
          InitialState = initial
          TickCount = tickCount
          Log = log
          Checkpoints = [||] }

    let private validate (record: ReplayRecord) : Result<unit, ReplayError> =
        if record.Version <> FormatVersion then
            Error(UnsupportedReplayVersion(record.Version, FormatVersion))
        elif record.Log.Version <> CommandLog.Version then
            Error(UnsupportedCommandLogVersion(record.Log.Version, CommandLog.Version))
        elif record.CanonicalFormat <> Canonical.FormatVersion then
            Error(UnsupportedCanonicalFormat(record.CanonicalFormat, Canonical.FormatVersion))
        elif record.InitialState.Tick <> 0L then
            Error(InitialStateNotAtTickZero record.InitialState.Tick)
        elif record.InitialState.Random <> SplitMix64.create record.Seed then
            Error(SeedInconsistentWithInitialState(record.Seed, record.InitialState.Random.Word))
        elif record.TickCount < 0L then
            Error(InvalidTickCount record.TickCount)
        else
            let commands = record.Log.Commands

            let monotonicError =
                commands
                |> Array.pairwise
                |> Array.mapi (fun i pair -> i, pair)
                |> Array.tryPick (fun (i, (a, b)) ->
                    if (a.Tick, a.Sequence) >= (b.Tick, b.Sequence) then
                        Some(NonMonotonicCommandLog(i + 1, struct (a.Tick, a.Sequence), struct (b.Tick, b.Sequence)))
                    else
                        None)

            let rangeError =
                commands
                |> Array.mapi (fun i c -> i, c)
                |> Array.tryPick (fun (i, c) ->
                    if c.Tick < 1L || c.Tick > record.TickCount then
                        Some(CommandOutsideReplayRange(i, c.Tick, record.TickCount))
                    else
                        None)

            // A CommandId is unique for the life of a run. Array.groupBy keeps
            // keys in first-appearance order and members in original order, so
            // the reported (id, firstIndex, secondIndex) is deterministic.
            let duplicateIdError =
                commands
                |> Array.mapi (fun i c -> i, c.Command.Id)
                |> Array.groupBy snd
                |> Array.tryPick (fun (id, occurrences) ->
                    if occurrences.Length > 1 then
                        let idx = occurrences |> Array.map fst |> Array.sort
                        Some(DuplicateCommandIdInLog(id, idx.[0], idx.[1]))
                    else
                        None)

            match monotonicError |> Option.orElse rangeError |> Option.orElse duplicateIdError with
            | Some err -> Error err
            | None -> Ok()

    /// Reconstructs the authoritative run described by `record`: initial state
    /// plus command log plus the random stream carried in the initial state.
    /// Returns a typed error for any unsupported version or malformed log.
    let run (config: SimConfig) (record: ReplayRecord) : Result<ReplayOutcome, ReplayError> =
        match validate record with
        | Error err -> Error err
        | Ok() ->
            let commands = record.Log.Commands
            let mutable state = record.InitialState
            let hashes = ResizeArray<Checkpoint>(int record.TickCount)
            let states = ResizeArray<WorldState>(int record.TickCount)
            let events = ResizeArray<DomainEvent>()

            for tick in 1L .. record.TickCount do
                // Linear scan per tick: O(commands x ticks). Adequate for the
                // harness; a cursor over the pre-sorted log replaces it if the
                // log grows large (noted in the progress ledger).
                let tickCommands =
                    commands
                    |> Array.filter (fun c -> c.Tick = tick)
                    |> Array.map (fun c -> c.Command)

                let result = Simulation.step config tickCommands state
                state <- result.State
                hashes.Add { Tick = tick; Hash = result.StateHash }
                states.Add result.State
                events.AddRange result.Events

            Ok
                { FinalState = state
                  TickHashes = hashes.ToArray()
                  TickStates = states.ToArray()
                  Events = events.ToArray() }
