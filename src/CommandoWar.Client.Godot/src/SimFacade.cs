// The thin C# facade over the unmodified F# authoritative simulation.
//
// This file contains NO Godot types. It is the "ClientFacade -> Contracts ->
// Sim" stage from ADR-0002: the only thing in this spike that calls
// CommandoWar.Sim. Everything crossing the boundary is a primitive, an array,
// or an F# value type. The facade owns a WorldState and advances it with
// Simulation.step; it never mutates authoritative state itself and never
// catches a simulation-step exception.

using System;
using System.Collections.Generic;
using System.Linq;
using CommandoWar.Client.Godot.Content;
using CommandoWar.Sim;
using Microsoft.FSharp.Collections;
using Microsoft.FSharp.Core;

namespace CommandoWar.Client.Godot.Sim;

/// <summary>A value snapshot of one agent for rendering. No sim references.</summary>
public readonly record struct AgentView(int Id, bool Friendly, int X, int Y, int? DestX, int? DestY);

/// <summary>What one authoritative tick produced, reduced to primitives.</summary>
public readonly record struct TickInfo(long Tick, ulong Hash, int HashFormat, int EventCount, int AcceptedThisTick);

public sealed class SimFacade
{
    private readonly SimConfig _config;
    private WorldState _state;
    private readonly List<PlayerCommand> _pending = new();
    private int _nextCommandId = 1;
    private readonly List<string> _acceptedLog = new();

    public int GridWidth { get; }
    public int GridHeight { get; }
    public long Tick => _state.Tick;
    public ulong StateHash { get; private set; }
    public int StateHashFormat { get; private set; }
    public string StateHashHex => $"0x{StateHash:X16}";
    public ulong RandomDraws => _state.Random.Draws;

    /// <summary>Accepted commands as "&lt;tick&gt; &lt;agent&gt; move &lt;x&gt; &lt;y&gt;" lines,
    /// in the CommandoWar.Headless command-log format for cross-checking.</summary>
    public IReadOnlyList<string> AcceptedCommandLog => _acceptedLog;

    public IReadOnlyList<AgentView> Agents { get; private set; } = Array.Empty<AgentView>();

    private SimFacade(SimConfig config, WorldState initial, int w, int h)
    {
        _config = config;
        _state = initial;
        GridWidth = w;
        GridHeight = h;
        var h0 = Hashing.hash(initial);
        StateHash = h0.Value;
        StateHashFormat = h0.Format;
        Agents = ToViews(initial.Agents);
    }

    /// <summary>
    /// Builds the authoritative world from a validated scenario. Friendly and
    /// enemy spawn markers become agents (ascending by id); everything else is
    /// client-only. Fails loudly if CommandoWar.Sim rejects the world.
    /// </summary>
    public static SimFacade Create(ScenarioDto scenario)
    {
        var bounds = new GridBounds(scenario.Width, scenario.Height);

        var agents = scenario.Markers
            .Where(m => m.Kind is MarkerKind.FriendlySpawn or MarkerKind.EnemySpawn)
            .OrderBy(m => m.Kind == MarkerKind.FriendlySpawn ? 0 : 1)
            .ThenBy(m => m.Id)
            .Select(m => Agent.create(
                AgentIdModule.ofInt(m.Id),
                m.Kind == MarkerKind.FriendlySpawn ? Side.Friendly : Side.Hostile,
                new Cell(m.X, m.Y)))
            .ToArray();

        var result = World.create(bounds, scenario.Seed, ListModule.OfArray(agents));

        if (result.IsError)
            throw new InvalidOperationException(
                $"CommandoWar.Sim rejected the authored world: {result.ErrorValue}");

        return new SimFacade(new SimConfig(20), result.ResultValue, scenario.Width, scenario.Height);
    }

    /// <summary>Queues a move command for the next <see cref="Step"/>. Returns the
    /// command id. Out-of-bounds targets are still submitted; the simulation
    /// rejects them explicitly at command intake (that is the behaviour under
    /// test).</summary>
    public int QueueMove(int agentId, int x, int y)
    {
        int id = _nextCommandId++;
        _pending.Add(Command.moveTo(CommandIdModule.ofInt(id), _state.Tick + 1L, AgentIdModule.ofInt(agentId), new Cell(x, y)));
        return id;
    }

    public bool HasPendingCommands => _pending.Count > 0;

    /// <summary>
    /// Advances the authoritative simulation by exactly one integer tick.
    /// Any exception from Simulation.step propagates unchanged.
    /// </summary>
    public TickInfo Step()
    {
        var commands = _pending.ToArray();
        _pending.Clear();

        StepResult result = Simulation.step(_config, commands, _state);
        _state = result.State;

        StateHash = result.StateHash.Value;
        StateHashFormat = result.StateHash.Format;
        Agents = ToViews(result.State.Agents);

        int accepted = 0;
        foreach (var ev in result.Events)
        {
            if (ev.Body.IsCommandAccepted)
            {
                accepted++;
                var body = (EventBody.CommandAccepted)ev.Body;
                _acceptedLog.Add(
                    $"{ev.Tick} {AgentIdModule.value(body.agent)} move {body.destination.X} {body.destination.Y}");
            }
        }

        return new TickInfo(result.State.Tick, result.StateHash.Value, result.StateHash.Format,
            result.Events.Length, accepted);
    }

    private static AgentView[] ToViews(AgentState[] agents) =>
        agents
            .OrderBy(a => AgentIdModule.value(a.Id))
            .Select(a =>
            {
                FSharpOption<Cell> d = a.Destination;
                return new AgentView(
                    AgentIdModule.value(a.Id),
                    a.Side.IsFriendly,
                    a.Position.X, a.Position.Y,
                    FSharpOption<Cell>.get_IsSome(d) ? d.Value.X : (int?)null,
                    FSharpOption<Cell>.get_IsSome(d) ? d.Value.Y : (int?)null);
            })
            .ToArray();
}
