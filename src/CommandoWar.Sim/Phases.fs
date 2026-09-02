namespace CommandoWar.Sim

/// The fixed authoritative tick phase order. Every tick runs these phases in
/// exactly this sequence, even when a phase is currently a no-op
/// (docs/03_ARCHITECTURE.md section 7, docs/04_SIMULATION_SPEC.md section
/// 12). Changing the order changes outcomes and requires an ADR.
type Phase =
    | CommandIntake
    | Communication
    | Perception
    | TacticalKnowledge
    | Appraisal
    | CommitmentAndLocalAction
    | NavigationAndMovement
    | Combat
    | StateConsequences
    | Mission
    | Output

[<RequireQualifiedAccess>]
module Phases =

    /// The single source of truth for tick phase order. The simulation step
    /// folds over exactly this list.
    let order: Phase list =
        [ CommandIntake
          Communication
          Perception
          TacticalKnowledge
          Appraisal
          CommitmentAndLocalAction
          NavigationAndMovement
          Combat
          StateConsequences
          Mission
          Output ]
