namespace CommandoWar.Sim

/// Placeholder movement rule for the simulation skeleton.
///
/// This is NOT production navigation. It performs no pathfinding, no
/// obstacle avoidance, and no occupancy or reservation handling. It advances
/// an agent one logical cell per tick toward a destination, adjusting the X
/// axis first and then the Y axis, so the path is a sequence of cardinal
/// single-cell steps.
///
/// Deterministic grid pathfinding and full movement resolution replace this
/// entirely in later tasks (backlog B-010, B-011). Keeping the rule isolated
/// in this module means that replacement need not disturb the host-facing
/// contracts.
[<RequireQualifiedAccess>]
module PlaceholderMovement =

    /// The next cell for an agent at `current` heading toward `destination`,
    /// one cardinal step at a time. Returns `current` unchanged when the
    /// agent is already at the destination.
    ///
    /// In the skeleton the grid is a rectangle with no obstacles and the
    /// destination is validated in-bounds at command intake, so every step
    /// this produces is also in-bounds.
    let nextCell (current: Cell) (destination: Cell) : Cell =
        if current.X <> destination.X then
            { current with X = current.X + sign (destination.X - current.X) }
        elif current.Y <> destination.Y then
            { current with Y = current.Y + sign (destination.Y - current.Y) }
        else
            current
