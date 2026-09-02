namespace CommandoWar.Headless

open CommandoWar.Sim

/// The single shared logical fixture for both framework spikes (TASK-004 Godot,
/// TASK-005 Mibo). It is intentionally defined once, here, in a
/// framework-neutral project that depends only on `CommandoWar.Sim`. Each
/// graphical host must reproduce the same per-tick `StateHash` sequence from
/// these exact parameters; `content/fixtures/SPIKE-FIXTURE.md` is the committed
/// reference table and `cwheadless fixture` regenerates it.
///
/// Nothing here is authoritative game content. It is a deterministic test
/// vector: `Setup.sixAgentWorld` on a fixed grid and seed, advanced by one
/// documented move command for a fixed number of ticks.
[<RequireQualifiedAccess>]
module Fixture =

    /// Logical grid: 32 x 32, matching the ADR-0001 "common result" map size.
    let bounds: GridBounds = { Width = 32; Height = 32 }

    /// Deterministic seed for the SplitMix64 stream carried on the world.
    /// Fixed and documented; the digits are the pack date (2026-09-02) and
    /// carry no meaning beyond being a stable, obviously-intentional constant.
    [<Literal>]
    let Seed = 20260902UL

    /// Ticks the fixture run covers. Agent 3 reaches its destination at tick 31
    /// (20 steps on X then 11 on Y under the placeholder movement rule); the
    /// run continues to tick 40 so the post-arrival rest state is also hashed.
    [<Literal>]
    let TickCount = 40L

    /// Issuer string recorded on every fixture command (non-authoritative
    /// provenance only).
    [<Literal>]
    let Issuer = "fixture:spike"

    /// The one documented move command: agent 3 (starting at column 0, row 3)
    /// is ordered to cell (20, 14) at tick 1.
    let MovedAgent: AgentId = AgentId.ofInt 3
    let MoveTarget: Cell = { X = 20; Y = 14 }
    let CommandIssueTick = 1L

    /// The authoritative world at tick 0 for the fixture.
    let initialState () : WorldState = Setup.sixAgentWorld bounds Seed

    /// The fixture command log (one recorded command), already in canonical
    /// (Tick, Sequence) order.
    let commandLog () : RecordedCommand[] =
        [| { Tick = CommandIssueTick
             Sequence = 0
             Command = Command.moveTo (CommandId.ofInt 1) CommandIssueTick MovedAgent MoveTarget
             Issuer = Issuer } |]

    /// A `ReplayRecord` for the fixture: initial state + the one command,
    /// covering `TickCount` ticks.
    let replayRecord () : ReplayRecord =
        Replay.record
            { Build = "cwheadless"; Scenario = "spike-fixture" }
            (initialState ())
            TickCount
            (CommandLog.create (commandLog ()))

    /// Runs the fixture through the simulation's replay runner.
    let run () : Result<ReplayOutcome, ReplayError> =
        Replay.run SimConfig.standard (replayRecord ())
