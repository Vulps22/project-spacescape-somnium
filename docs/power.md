# Power, heat and the machine

The spine of the game. Everything here is implemented in `Assets/#User/SpaceScape/scripts/Power/` as
plain C# with **no Unity references**, and is covered by 242 tests that run outside the Editor
(`dotnet run` against the real source files, see `README.md`).

Status markers: **Decided** — settled and built. **Open** — see `open-questions.md`.

## The grid is tiles

**Decided.** Everything on the grid is the same kind of thing: a **tile**, one unit square, centred
on an integer coordinate. A battery, a cable, a bulb and a splitter differ only by what is attached
to them, never by being a different kind of node.

- a **conduit** is a tile that only passes power through
- a **component** is a tile with something that draws or produces
- a **junction** is a tile with more than one outgoing face

Adjacency is not authored. Tiles sit one unit apart, so a neighbour is `coordinate + face`. In the
sim this is a plain directed graph; the grid geometry lives entirely in the Unity layer.

### A thing can be bigger than one cell

**Decided and built.** `GridNode` carries a `Size`. A cable is `(1,1,1)`; a reactor is `(5,5,5)`. An
odd size centres on the anchor, an even one leans to the high side, because a box of four has no
middle cell to sit on.

**A face is a whole side of the footprint**, so a run can meet a big machine anywhere along its
flank instead of having to reach its centre. Two things touching over several cells are still **one
join, not several** — otherwise a five-cell contact would hand that neighbour five shares.

Every occupied cell is registered, so building a cable through a reactor warns by name and
coordinate instead of silently working.

### Faces are declared, in and out

**Decided and built.** `GridNode` carries `_outputs` and `_inputs`, both arrays, so everything on the
grid declares its faces the same way — cables, batteries and reactors alike.

**No inputs declared means it accepts power at any face.** Declare any and it only accepts on those,
so a reactor's control socket is a real socket and power cannot enter round the side. The grid warns
when something sends power at a neighbour with no matching socket.

Inference fills the gaps for rendering: a tile works out its input faces from the graph at runtime,
and by looking at which neighbours point at it when there is no graph, so the editor draws a corner
before anything runs.

### Components are endpoints

**Decided.** Nothing flows *through* a component. A bulb, a gun and a helm are leaves: they take what
they want and whatever is left has nowhere to go, so it becomes heat right there.

The one exception is a **store**, which needs an input and an output — see Batteries below.

This kills an earlier mechanism entirely: consumers drawing greedily in path order, with the
remainder carrying on to the next one. That only worked because components were wired inline, which
they never are. Priority now comes from **branch depth and cable count**, nothing else.

## The flow model

**Decided.** One rule, applied to every tile:

> Sum what arrived → take what you want → split the rest across the live outgoing faces by their
> shares → if there are none, it becomes heat here.

```
flow(edge) = remainder × edge.Share / (sum of live outgoing shares)
```

A junction is just a tile that wants nothing. There is no separate splitter type, no separate
consumer type, and no special case for dead ends.

### What a face is worth

`PowerEdge.Share` is 1 for a sound cable. Two cables to the same place is two shares, which is how a
ratio is expressed physically rather than with a dial.

**A fault raises the share.** A short behaves as though several cables run where one does — so the
grid and the eye disagree, which is the only reason to own a meter. A shorted branch at share 3
takes 75% where it should take 50%, and its sibling browns out to 25%. `Share < 1` is the opposite
fault: a corroded connection that starves itself and *brightens* everything around it.

**A fault only reaches what shares its wiring.** A branch shorting at share 4 halves its sibling but
leaves a different subtree completely untouched, so *"what else is on this branch?"* is the
diagnostic question.

### Priority

Two mechanisms, and only two:

- **branch depth** — one branch point out gets half, two gets a quarter. Routing the long way round
  demotes something.
- **cable count** — two cables to one endpoint gives it two thirds.

### Consequences that fall out

- **Conservation.** Verified across 200 randomly generated grids to 1e-6.
- **Nothing is demand-driven.** A module receives whatever the topology hands it. Demand-driven flow
  auto-balances, and auto-balancing deletes the player.
- **Power only goes where a receiver can be reached.** Decided 2026-09-26. A conduit carries power
  only if a receiver lies beyond it, through conduits (components do not pass it on), whether or not
  that receiver wants power right now. A dead end therefore takes nothing and stays cold, and a fork
  splits only among branches with someone on them. It replaced "a forgotten dead end steals power",
  because pushing into everything made one overfed load burn its whole run back to the reactor: each
  pop turned the tile before it into the new dead end, which took all the surplus and popped in turn.
  Now the load blows, its branch goes dark, and a producer with nowhere left to send its output takes
  it as heat itself.
- **Power moves one tile per tick**, so a node's readings sit one step behind the conduits leaving it
  until the grid settles. That lag is the visible surge when a conduit is reconnected. `Settle()`
  runs to the fixed point.

### Implementation

Double-buffered: every node reads last tick's conduits and writes to a fresh buffer, so tick order
cannot matter and a tile can be added or removed mid-tick without a topological sort. Flows are
recomputed from scratch every tick, so nothing drifts.

## On, off, and wrecked

**Decided.** Three different things, and conflating them caused real bugs.

```csharp
public virtual bool CanReceivePower() => On && !IsPopped;
```

**Asked every tick, never cached.** A stored copy went stale the moment Unity wrote a serialized
field directly and bypassed the property setter. Because the graph *asks*, several reasons to be
unavailable simply AND together — there is no shared flag for two things to fight over.

It is `virtual` so a breaker or a fault can answer with more than a flag: a breaker is a subclass
whose answer is `On && !IsPopped && flow < trip`.

| state | in the graph? | draws? | can be damaged? |
|---|---|---|---|
| **on** | yes | yes | yes |
| **off** (switch or module disabled) | **no** | no | **Open** — see below |
| **wrecked** (condition zero) | **yes** | no | already gone |

**Off means the tile leaves the graph**, so whatever was feeding it has nowhere to push. That single
fact gives both halves of the switch-placement lesson:

- a switch on the tile **beside a source** → the source has no outlet, declines, and keeps its charge
- a switch on a tile **mid-run** → the run before it goes dark: nothing beyond it can be reached, so
  nothing is sent. A producer that cannot decline takes its own output as heat instead.
- a component **switched off at the module** (the tile stays on) is still a receiver, so its branch is
  still fed and everything arriving is heat at the module. Where the switch is still matters.

### Does power pass through?

**Decided and built.** Only a conduit. `PowerNode.ForwardsPower` is true exactly when a tile has no
source and no sink, so every component is a diode: its input fills its own hold, and its output
carries only what it chose to release. Arriving surplus it did not take lands as heat on it.

- a **cable** passes through, obviously
- a **battery** does not. Everything downstream of one gets at most its discharge rate
- a **reactor** does not, so power fed to its magnets cannot leave by its output port

A component that forwarded would push arriving surplus out *on top of* its own production, which in
a loop compounds on every lap. Conservation: `in = drawn + blocked + passed`, with `blocked` as heat.

**A ring of conduits blows its merge point.** A cycle made only of forwarding tiles would circulate
power forever, so the tile where power enters the ring is severed the tick power reaches it. A loop
through any component is not a ring. See `modules.md` → One tick.

**Wrecked means broken, not removed.** A destroyed component stays wired in, keeps being fed, and
does nothing with it — so it becomes a dead end cooking its own compartment. Busted pistons, engine
still running. Whether a wrecked thing does anything is asked in exactly one place
(`PowerNode.IsWrecked`, consulted by the graph), so every source and sink type gets it free.

## Sinks: everything is a load

**Decided.** There is one sink type. A bulb, a helm and a gun differ only in their numbers.

It is a **relaxation oscillator**: charge to a threshold, work while burning it off, go dark at
empty, charge again.

```
Capacity     joules needed before it can work
DrawWatts    most it will pull at once
DrainWatts   watts it burns while working (0 = banks and holds, i.e. a gun)
```

That shape gives all three requirements with no special cases: **nothing switches on instantly**
(it must reach the threshold first), **nothing switches off instantly** (it coasts down on what it
holds), and **everything flickers when underfed**, because the cycle simply gets longer.

### Duty cycle is the deficit, exactly

**The fraction of time a component works equals the fraction of its rated power it receives.**

```
on-time  = Capacity / (drain − supply)
off-time = Capacity / supply
duty     = supply / rated          ← exact, by conservation
```

Measured: 100 W of 100 W → lit 1.00 of the time. 50 W → 0.4995. 25 W → 0.25. 10 W → 0.1004.

This is Pulsar's grid behaviour arriving from energy conservation rather than being tuned. `Capacity`
sets the *speed* of the flicker without touching the ratio — small capacity strobes, large pulses
slowly.

### A ship at rest overheats

A full accumulator stops drawing. Everything topped up means maximum surplus means maximum heat, so
**idling is dangerous and firing the gun cools the line.** Nobody wrote this; it falls out of
`WattsWanted` returning zero when full.

## Sources: producing and providing are different

**Decided.** Two interfaces, because a battery is a source that produces nothing.

```csharp
interface IPowerProducer { double WattsProduced { get; } void ProducePower(double seconds); }
interface IPowerSource   { double WattsOffered(bool hasOutlet); void ProvidePower(double seconds); }
```

Neither mentions fuel, rods, light or charge — those live in the concrete classes. The graph only
ever speaks to `IPowerSource`; the producer sits one level in, inside the component.

The split earns itself with one equation, which is the design restated:

> **produced = delivered + heat**

### Only a store may decline

`hasOutlet` is the whole distinction:

- a **producer cannot decline.** A reaction runs until the rods drop or the fuel is gone, so it
  offers everything whether the grid can take it or not. Isolate it and its output dumps at its own
  tile — which *is* reactor heat, with no second heat path and energy counted once.
- a **store may decline.** Nowhere to push simply means it does not release, so a switch on a
  battery's own conduit preserves the cell.

`ProducerSource` is the adapter and encodes that rule in one line.

### Batteries

`BatteryBehaviour` is one hold with a sink face and a source face, so it needs an input conduit and
an output conduit. It can never offer more than its hold can give in one tick.

**A sink draws only from what arrived, never from its own node's output**, or a battery charges from
its own discharge and swallows everything. `Arriving` and `Inflow` are separate for exactly this.

A cell mid-run is a UPS: 200 W arriving, it charges at 100 W and discharges at 100 W in the same
tick, and the 100 W it could not bank is heat on the cell. It never passes arriving power on.

### Three cell tiers

**Decided and built.** `BatteryBehaviour` is the base; each tier overrides one question.

| tier | asks | pushes into a dead run? | idle ship |
|---|---|---|---|
| **plain** | have I got a cable? | no — the grid never sends power down a dead run | keeps pushing |
| **safety** | does this run reach a component? | no | keeps pushing |
| **smart** | is anything *asking*, and at what rate? | no | **stops** |

**Open:** since the reachability rule, plain and safety behave the same, because the grid itself now
refuses dead runs. The bottom rung needs a new question, or the ladder becomes two tiers. Smart still
earns its place: it stops at a sated grid, where the other two keep pushing into full components.

The graph marks `ReachesConsumer` and `ReachesDemand` on every node in one backward walk per tick,
from each receiver's feeders — not the receiver itself, so a battery never counts its own intake as
somewhere to deliver — and stops at components, since power does not pass through them.

## Heat

**Decided.** Heat is the price of waste.

```
gain     Dumped × 0.05 °C per watt-second     (a wasting tile never cools)
shed     0.2 °C/s while not wasting           (down to its baseline, ambient by default)
conduct  4.0 × the excess per second, per conduit
```

### A tile can be meant to be hot

**Decided and built.** `PowerNode.BaselineCelsius` is where a tile *should* sit — ambient for
everything that is not making heat on purpose, and higher for a running reactor, whose heat ratio is
a baseline rather than a fault. Nothing sheds below it, and `HeatAboveBaseline` is what damage is
built from, so a reactor at its working temperature reads zero excess and is never damaged for
merely working.

**Only excess heat travels.** Conduction moves `HeatAboveBaseline` differences, not absolute
temperatures. Making the baseline a floor that `Celsius` snapped up to **manufactured energy**:
conduction drained the reactor every tick and the floor refilled it for free, so a 400 °C core became
an infinite heat source that climbed to 600 °C on heat bouncing off its own neighbours. A reactor at
its working temperature is not hot, it is correct, and correct does not spread.

Rates are **per second, not per tick**, so changing the 20 Hz tick never changes how the ship
behaves.

**Waste heats the tile that is wasting, then travels back along the cables.** Over 20 s a 10 W load
fed 100 W through a five-tile run gives `29.2 / 29.6 / 30.1 / 30.7 / 31.6 | 32.4 °C`, a clean
gradient falling away from the overfed load.

Conduction is computed against the tick's starting temperatures and applied afterwards, so no tile's
order matters and what one loses is exactly what the next gains.

**A cable conducts heat with its switch open** — an open switch stops the current, not the metal. A
*severed* cable does not, which is a different case.

**Shedding must be much weaker than conduction** or gradients cannot form. The first attempt used a
flat 1 °C/s and heat died two tiles from the source, because a five-tile run has five times the
shedding capacity.

**Heat has no ceiling and that is deliberate** — see the flash point note in `open-questions.md`.

## Failure

**Decided.** One hazard, two outcomes.

```
hazard      = k · Inflow · heatAboveBaseline
chance/tick = 1 − exp(−hazard · seconds)
```

The exponential is a hazard rate rather than a clamped linear chance: memoryless, never exceeds 1,
and composes correctly over time.

**`k` is never exposed.** The only dial is `PopSecondsAt100WAnd120C = 30`, which reads as what it is,
and `k` is derived. Measured over 400 runs: 30 s dial → 31.0 s mean; 5 s dial → 5.3 s; half the power
→ 60.3 s. Below `PopFloorCelsius = 50` nothing fails at all, verified with 5 kW at 49 °C for 1000 s.

Damage measures **what a tile is minus what it should be**, so a reactor is only ever damaged for
running *hotter than its rating*, never for running.

**The product is what makes diagnosis hard.** Heat is generated at an overfed load and conducts back,
but the tiles behind it carry more current — so `P · H` can peak on a tile that is not the hottest
and is wasting nothing.

### Conduits sever, components are damaged

- a **bare conduit** pops: severed for good, carries neither power nor heat, and no switch brings it
  back
- a **component** takes `PopDamage = 25` off its condition instead, so four hits end it

`Integrity` also makes a component unreliable before it is gone: below 20% condition it **misfires**
— spends its charge and does nothing — with the chance scaling from zero at the worn line to
`FailureChanceWhenSpent` at nothing left. At 2% condition: 95 misfires in 200 cycles. The gun
charges, fires, nothing comes out, and it keeps drawing power to recharge, so it keeps wearing itself
down.

That is where losing a run comes from: bad luck on a jump drive already at 15%.

### Cascades decapitate, they do not spread

Three branches sized exactly to their third run cool with zero waste. Kill one and the survivors are
over-supplied, start heating — and **the source blows**, 11 seconds later, because a source node
carries every watt it produces and heat conducts back toward it.

So a thermal fault in a tree kills upstream, all at once, rather than rippling outward. **A breaker's
job is to cut a wasteful branch loose before its heat reaches the trunk**, not to save the branch it
sits on.

`PopSeed` is seeded, so a run repeats and every client would agree once this is networked.

Three events for the scene: `Popped` (a conduit severed), `Damaged` (a component took a hit — the
hook for damage stages), `Lost` (condition reached zero).

## What is not built

Breakers, conduit capacity, fire, and the fuel rack and coolant pad as real inventory. The reactor
**is** built — see `reactor.md`. See `open-questions.md` for what is undecided.
