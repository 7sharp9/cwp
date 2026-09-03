// TASK-007 follow-up: the ENTIRE C# client surface, written once, never per
// scene. Every .tscn entry point uses this node as its root and sets
// [Export] SceneType to the F# type that implements CwClientCore.IClientScene.
// This class contains no client logic - it resolves the F# scene and forwards
// lifecycle callbacks. Nothing scene-specific is ever added here.

using System;
using CwClientCore;
using Godot;

public partial class FSharpSceneHost : Node2D
{
    /// Fully-qualified name of the F# IClientScene implementation, e.g.
    /// "CwClientCore.TacticalScene". Set per scene in the editor inspector.
    [Export] public string SceneType { get; set; } = "";

    private IClientScene _scene;

    public override void _Ready()
    {
        if (string.IsNullOrWhiteSpace(SceneType))
        {
            GD.PushError("FSharpSceneHost: SceneType [Export] is empty");
            return;
        }
        Type t = typeof(IClientScene).Assembly.GetType(SceneType, throwOnError: true);
        _scene = (IClientScene)Activator.CreateInstance(t);
        _scene.Ready(this);
    }

    public override void _Process(double delta)
    {
        _scene?.Process(delta);
        QueueRedraw();
    }

    public override void _UnhandledInput(InputEvent @event) => _scene?.UnhandledInput(@event);

    public override void _Draw() => _scene?.Draw(this);

    public override void _ExitTree() => _scene?.ExitTree();
}
