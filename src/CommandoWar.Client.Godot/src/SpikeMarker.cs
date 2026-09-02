using CommandoWar.Client.Godot.Content;
using Godot;

namespace CommandoWar.Client.Godot;

/// <summary>
/// A typed content marker node. Placed and moved in the Godot editor; its
/// <see cref="Cell"/> is the authoritative authored grid position. Read by
/// <see cref="GreyboxScene"/> into a framework-neutral <see cref="MarkerDto"/>.
/// </summary>
public partial class SpikeMarker : Marker2D
{
    [Export] public MarkerKind Kind { get; set; } = MarkerKind.FriendlySpawn;
    [Export] public int Id { get; set; }
    [Export] public Vector2I Cell { get; set; }
}
