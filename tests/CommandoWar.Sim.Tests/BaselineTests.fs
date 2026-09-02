module CommandoWar.Sim.Tests.BaselineTests

open Xunit
open CommandoWar.Sim

[<Fact>]
let ``simulation library is referenceable across the project boundary`` () =
    Assert.Equal("CommandoWar.Sim", Baseline.ContractName)

[<Fact>]
let ``nextTick advances authoritative time by one integer tick`` () =
    Assert.Equal(1, Baseline.nextTick 0)
    Assert.Equal(21, Baseline.nextTick 20)
