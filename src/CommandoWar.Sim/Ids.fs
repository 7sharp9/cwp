namespace CommandoWar.Sim

/// Stable, session-unique identifiers for authoritative entities.
///
/// Constructors are private: identifiers are minted through the smart
/// constructors below so that ordering and equality stay structural and
/// stable (docs/03_ARCHITECTURE.md section 10, docs/04_SIMULATION_SPEC.md
/// section 6). Identifiers are never reused within a session.

/// Identifies one agent for the life of a session.
[<Struct>]
type AgentId = private AgentId of int

/// Identifies one player command within a session.
[<Struct>]
type CommandId = private CommandId of int

[<RequireQualifiedAccess>]
module AgentId =

    /// Creates an agent id from a non-negative integer.
    let ofInt (value: int) : AgentId =
        if value < 0 then invalidArg (nameof value) "AgentId must be non-negative"
        AgentId value

    /// The underlying integer. For explicit ordering and diagnostics only.
    let value (AgentId v) : int = v

[<RequireQualifiedAccess>]
module CommandId =

    /// Creates a command id from a non-negative integer.
    let ofInt (value: int) : CommandId =
        if value < 0 then invalidArg (nameof value) "CommandId must be non-negative"
        CommandId value

    /// The underlying integer. For explicit ordering and diagnostics only.
    let value (CommandId v) : int = v
