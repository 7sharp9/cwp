module CommandoWar.Sim.Tests.FixtureTests

open Xunit
open CommandoWar.Sim
open CommandoWar.Headless

// Regression guard for the shared framework-spike fixture
// (src/CommandoWar.Headless/Fixture.fs, content/fixtures/SPIKE-FIXTURE.md).
// Both TASK-004 (Godot) and TASK-005 (Mibo) reproduce these hashes from their
// own hosts; if CommandoWar.Sim changes the numbers here without an intentional
// format-version bump, that is a determinism regression and both spikes'
// evidence silently rots.

[<Fact>]
let ``fixture parameters are the pinned values`` () =
    Assert.Equal(32, Fixture.bounds.Width)
    Assert.Equal(32, Fixture.bounds.Height)
    Assert.Equal(20260902UL, Fixture.Seed)
    Assert.Equal(40L, Fixture.TickCount)
    Assert.Equal(3, AgentId.value Fixture.MovedAgent)
    Assert.Equal({ X = 20; Y = 14 }, Fixture.MoveTarget)

[<Fact>]
let ``fixture initial state hash is pinned`` () =
    let h = Hashing.hash (Fixture.initialState ())
    Assert.Equal(13, h.Format)
    Assert.Equal(0xC0A53D46AE5D7C80UL, h.Value)

[<Fact>]
let ``fixture per-tick hashes match the committed reference table`` () =
    let expected =
        [| 0xCD1C3578BFB0F320UL; 0xDF6BAA3629E6E1CCUL; 0x8AD488B567B67378UL; 0x9E239C8E0D2D18ECUL
           0x658F90E3F5B4C120UL; 0x9C9B93BB731F3974UL; 0xBECD10E0293787D8UL; 0x82DA58DF1D0F9704UL
           0xFB1E55FC6A366D80UL; 0x06CB4A3BAD7134BCUL; 0xBD8AD5CD8B360248UL; 0xE3FAFC8E328E223CUL
           0xA1C35C4CAE6D99E0UL; 0x2C0CF82ECDAFDF14UL; 0xDFE4D41A6F7B6C68UL; 0x9BE4B762D775E6C4UL
           0x7E6CA6120778A700UL; 0x2B07F5A03CA8CDECUL; 0x96DB4B0C6A83C0D8UL; 0x951A2ECAB348164CUL
           0x8D09C1F476F5CB46UL; 0xB07922DEB7AB64C0UL; 0x961ACE9C3E673EDEUL; 0xCE2AC7A8920D77DCUL
           0x81B017A77E96B926UL; 0x4CFC18A72044AE48UL; 0x7E46E093F803EAAEUL; 0xC66789C7E16FAF7CUL
           0x882921E58192C1C6UL; 0x3D46D3B2D7B34C20UL; 0x8814DF097CC85A89UL; 0xFAF2A8BC7038032BUL
           0xBA37563CD1B33264UL; 0x9CBFC071234528FDUL; 0xFFD60812D29AD256UL; 0x71F9DB27867DB69FUL
           0x9BE7C5592D3044A8UL; 0xC184B4458E90E0A1UL; 0x5DE12A8F954AB6DAUL; 0x447C32A5D599EAB3UL |]

    match Fixture.run () with
    | Error e -> Assert.Fail($"fixture replay failed: {e}")
    | Ok outcome ->
        Assert.Equal(40, outcome.TickHashes.Length)
        let actual = outcome.TickHashes |> Array.map (fun cp -> cp.Hash.Value)
        Assert.Equal<uint64[]>(expected, actual)

[<Fact>]
let ``canonical command-log file parses to the fixture command`` () =
    let text = "version 1\n# comment\n\n1 3 move 20 14\n"

    match CommandLogFile.parse "test" text with
    | Error e -> Assert.Fail(CommandLogFile.describeError e)
    | Ok cmds ->
        Assert.Single(cmds) |> ignore
        let c = cmds.[0]
        Assert.Equal(1L, c.Tick)
        Assert.Equal(0, c.Sequence)
        Assert.Equal(3, AgentId.value c.Command.Agent)
        Assert.Equal(Order(MoveTo { X = 20; Y = 14 }, Replace), c.Command.Body)

[<Fact>]
let ``command-log parser rejects an unknown verb with the line number`` () =
    match CommandLogFile.parse "test" "1 3 warp 4 4\n" with
    | Ok _ -> Assert.Fail("expected a parse error")
    | Error(CommandLogFile.UnknownDirective(line, verb)) ->
        Assert.Equal(1, line)
        Assert.Equal("warp", verb)
    | Error other -> Assert.Fail($"wrong error: {other}")
