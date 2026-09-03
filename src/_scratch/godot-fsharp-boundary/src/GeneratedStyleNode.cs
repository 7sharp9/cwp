// TASK-007 follow-up 2: HAND-WRITTEN to be exactly what a Myriad plugin would
// emit from CwClientCore.PatrolMarkerLogic. Pure mechanical forwarding - no
// logic. Godot's OWN source generator runs over this file and produces all the
// InvokeGodotClassMethod / GetGodotPropertyList / signal bridge metadata; this
// proof checks that the inspector round-trip and signal registration work when
// the [Export] / [Signal] members forward to a composed F# object.

using CwClientCore;
using Godot;

[GlobalClass]
public partial class GeneratedStyleNode : Node2D
{
    private PatrolMarkerLogic _logic;

    public GeneratedStyleNode()
    {
        _logic = new PatrolMarkerLogic(this);
    }

    [Export]
    public int Waypoints
    {
        get => _logic.Waypoints;
        set => _logic.Waypoints = value;
    }

    [Export]
    public string Label
    {
        get => _logic.Label;
        set => _logic.Label = value;
    }

    [Signal]
    public delegate void PatrolCompletedEventHandler(int lap);

    public override void _Ready() => _logic.OnReady();

    public override void _Process(double delta) => _logic.OnProcess(delta);
}
