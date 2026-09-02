namespace CommandoWar.Sim

/// Framework-neutral baseline surface established by TASK-001.
///
/// This module exists only to prove that the authoritative simulation library
/// can be referenced and unit tested from a separate project without any
/// graphical or host framework. The authoritative fixed-tick API, world state,
/// commands, events, and snapshots are introduced by TASK-002 and must not be
/// added here.
module Baseline =

    /// Stable identifier for the authoritative simulation contract.
    [<Literal>]
    let ContractName = "CommandoWar.Sim"

    /// Returns the authoritative tick that immediately follows the given tick.
    ///
    /// Authoritative time is an integer tick count (see ADR-0002 and
    /// docs/03_ARCHITECTURE.md section 6). This is a placeholder boundary
    /// function, not the simulation step.
    let nextTick (tick: int) : int = tick + 1
