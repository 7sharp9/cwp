namespace CommandoWar.Sim

/// What a command asks an agent to do. Only movement is needed for the
/// framework spikes; Hold, Suppress, Assault and Withdraw
/// (docs/04_SIMULATION_SPEC.md section 13) are added by later tasks.
type PlayerIntent =
    | MoveTo of target: Cell

/// A player command envelope. Recipients beyond a single agent, urgency and
/// risk tolerance are deferred to the command-validation task (backlog
/// B-014). `IssueTick` is carried for replay; eligibility/scheduling by
/// issue tick is not yet enforced.
type PlayerCommand =
    { Id: CommandId
      IssueTick: int64
      Agent: AgentId
      Intent: PlayerIntent }

/// Why the simulation refused a command during command intake. Refusal is
/// explicit and deterministic. A syntactically valid command may still be
/// declined later by agent appraisal, which is a separate concept
/// (docs/04_SIMULATION_SPEC.md section 13).
type CommandRejection =
    | UnknownAgent of agent: AgentId
    | TargetOutOfBounds of target: Cell

[<RequireQualifiedAccess>]
module Command =

    /// Builds a move command.
    let moveTo (id: CommandId) (issueTick: int64) (agent: AgentId) (target: Cell) : PlayerCommand =
        { Id = id; IssueTick = issueTick; Agent = agent; Intent = MoveTo target }
