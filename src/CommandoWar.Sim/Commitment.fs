namespace CommandoWar.Sim

/// A commitment's move-specific payload (TASK-030, backlog B-018;
/// `docs/05_COMMAND_AND_AGENT_AI.md` section 9). `Command` identifies which
/// delivered order this commitment realises, so a superseding order (a
/// different `Command`) is distinguishable from the same order continuing;
/// `Target` mirrors `AgentState.Destination` for the duration of the
/// commitment — both are written from the same Appraisal-`Accepted` target.
type MoveCommitment = { Command: CommandId; Target: Cell }

/// What an agent is currently committed to (`docs/04_SIMULATION_SPEC.md`
/// section 12.6; `docs/05` section 9). TASK-030 subset: only the two cases
/// current systems can produce or act on. `Suppressing` / `Assaulting` /
/// `Withdrawing` need `PlayerIntent` cases that do not exist yet (`Hold` /
/// `Suppress` / `Assault` / `Withdraw` — backlog B-030) and get no case, per
/// `AGENTS.md` "do not build speculative type machinery".
type Commitment =
    | Holding
    | Moving of MoveCommitment

[<RequireQualifiedAccess>]
module Commitment =

    /// Derives the agent's current commitment from its order-appraisal state
    /// (`docs/04` section 12.6). Pure and total: `Moving` iff the agent holds
    /// an order, it was `Accepted`, and a destination is still outstanding;
    /// `Holding` otherwise (no order, a `Refused` / `Unable` order, or an
    /// `Accepted` order already fulfilled — `Destination` cleared by the
    /// `commitmentAndLocalAction` phase's housekeeping, relocated from
    /// `appraisal` by TASK-030).
    ///
    /// This is fully recoverable from fields already in `Canonical.encode`
    /// (`Order`, `Disposition`, `Destination`) — the identical argument that
    /// keeps `AgentState.Route` out of the canonical image (`docs/04` section
    /// 17 "derived caches either excluded or normalised"). `Commitment` is
    /// therefore deliberately **not** a stored `AgentState` field: storing it
    /// again would duplicate state that can silently drift from its source
    /// fields, where deriving it on demand cannot drift by construction.
    let ofAgent (order: ReceivedOrder option) (disposition: OrderDisposition option) (destination: Cell option) : Commitment =
        match order, disposition, destination with
        | Some o, Some Accepted, Some target -> Moving { Command = o.Command; Target = target }
        | _ -> Holding
