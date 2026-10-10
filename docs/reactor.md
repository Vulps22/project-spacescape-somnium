# The reactor

**Built and running in the scene.** `FissionCore`, `CoolantLoop`, `ReactorBehaviour`, and the Unity
`ReactorBehaviourModule` + `CapacitorModule` + `ReactorLamp`, all covered by the sim tests. Coolant is a preset
stand-in, and fuel comes a rod at a time from a `LoadingModule` (`loading.md`); the coolant pad is not built.

Cold-started in-world on 2026-09-18: a battery through a switch lights the magnets, the rods come up
over twenty seconds, and the core sits at 401 °C against a 400 °C nominal while exporting to two
batteries and two loads.

Fuel, rods, heat ratio and coolant all live in the concrete classes rather than in `IPowerProducer`
or `IPowerSource`, which is the whole point of that split.

## Shape

A reactor produces until its **fuel runs out** or it is **told not to**. That power either finds a
conduit or becomes heat at the reactor's own tile — already true in the build, because a producer
cannot decline and an isolated one dumps its whole output where it stands.

```
output = OutputPerCore × Cores × withdrawal          // withdrawal 0..1
heat   = output × HeatRatio                          // watts of heat per watt delivered
```

**Cores** is the upgrade and scavenge axis: more cores, more output, and heat scales with it
automatically because heat is derived from output.

> **Recorded so it is not re-derived:** the first sketch was `produced × cores ÷ rodPosition`. That
> divides by zero at full insertion — infinite power at the exact moment the reactor should be off —
> and inverts the relationship, since output rises as rods go *in*. Rods absorb neutrons, so output
> is proportional to how far they are **withdrawn**. Multiplication throughout.

## House load comes off the top

**Decided and built.** The magnets are inside the housing, so the core holds its own rods **before a
watt reaches the output port**. Only the shortfall is asked of the ship.

```
cold     produces 0   →  house 0   →  asks the grid for 20 W
running  produces 100 →  house 20  →  exports 80 W, asks the grid for nothing
```

So the starter supply is genuinely a **starter**: needed to get the rods up, irrelevant afterwards.
Pull it, switch it off, take it away — a running reactor ignores losing it.

### Power fed in never comes out

**Decided and built.** A reactor is a component, and no component forwards, so power fed to the
magnets **cannot leave by the output port**. Without it, a 100 W control feed against 20 W of magnets pushes
80 W out into the ship — and in a cold-start loop that compounds on every lap, because the reactor
forwards what arrives *on top of* its own production.

So the output carries only what the core made, and arriving surplus it cannot use
becomes heat at the core. Conservation holds: `in = drawn + blocked + passed`.

Practical consequence: **over-feeding the control circuit cooks the reactor.** A 100 W starter
against 20 W of magnets puts 80 W of heat into the core while it boots. Size the starter to what the
magnets need, or throttle it — which is the plain cell's weakness and the smart cell's reason to
exist.

### Minimum stable output

**Nobody designed this; it falls out of house load.** A core throttled below its own house load
cannot hold its own magnets, so they let go and the rods drop — and then it needs the starter back.

```
rods 100%   100 W   house 20   exports 80    holds
rods  30%    30 W   house 20   exports 10    holds
rods  10%    10 W   house 20   CANNOT HOLD   self-scrams
```

The floor is `houseLoad ÷ (OutputPerCore × Cores)` — 20% for a one-core reactor. Which gives a quiet
second reason to want more cores: **a bigger reactor is easier to throttle.** Two cores making 200 W
carry the same 20 W house load, so the floor halves to 10%.

It also gives the starter switch a third job. It is the cold start, it is the SCRAM, and it is what
you leave closed while throttling low if you do not fancy a restart.

## The heat ratio is the real upgrade

Heat is expressed **watts per watt**, not kelvin per watt. Temperature is what results from
accumulating heat, and the grid already converts wasted watts into degrees.

Real thermal efficiencies give the ladder for free:

| | efficiency | heat per watt delivered |
|---|---|---|
| RTG (Voyager-class) | ~6 % | **~15 : 1** |
| Pressurised water | ~33 % | **~2 : 1** |
| Sodium fast / gas-cooled | ~40–45 % | **~1.3 : 1** |
| Aspirational fusion | ~50–60 % | **~0.8 : 1** |

A scavenged RTG technically works and cooks the ship. A good core makes *less heat than power*. So a
better reactor is not "+20% output", it is **survivable** — which is a far more interesting thing to
save up for.

Real operating temperatures, if a running baseline is wanted: PWR coolant ~325 °C, sodium fast
~500–550 °C, gas-cooled ~750–950 °C.

## Control rods

**The dial is the crew's.** `TargetWithdrawal` is written by the crew and nothing else. While the
magnets grip, the rods head for it; without grip they fall; when grip comes back they head for it
again, with nobody touching the dial. The scram button is the crew turning the dial to zero. So a
cold start is: set the dial, close the starter switch, and the rods climb once the magnets charge.

Rods are held up by **electromagnets**. Cut the current and they fall in under gravity. The reactor
is fail-safe by construction: losing power to the control system shuts it down rather than letting it
run away. A SCRAM is just de-energising the magnets.

Two things follow without being designed:

- **The asymmetry is physical.** Dropping is gravity — two seconds to full insertion. Raising is
  motors — twenty. Shutting down is instant; starting up is not. That is the thermal inertia already
  agreed, with a reason behind it rather than an arbitrary spin-down timer.
- **Rods travel both ways.** It is a throttle, not a switch: `1.0 → 0.5 → 0.8 → 0` all work, and the
  working temperature tracks the rod position. Subject to the minimum stable output above.

### ⚠ What self-sustaining traded away

The **blackout loop is gone.** An earlier design had the magnets drawing from the grid permanently,
so a fault that cut reactor control dropped the rods, which killed all power, which kept them down —
a self-inflicted blackout. With house load off the top, nothing external can scram a running
reactor, because it does not depend on anything external.

The rods can now only drop four ways: a manual SCRAM, running out of fuel, being throttled below the
minimum stable output, or the reactor being wrecked by damage.

That is a cleaner machine and a much less treacherous one. If the blackout risk is ever wanted back,
the lever is to make the house load exceed what the core can spare at low rod positions, so there is
a band during startup where the ship is genuinely depended on.

**The magnets are not a separate machine.** The reactor has an **input** and draws power like
anything else on the grid; the rods stay up while that draw is satisfied. The fail-safe is not a
special case, it is the grid working normally.

What holds them is an ordinary `LoadBehaviour` over the reactor's `CapacitorModule`, and its
**capacity is the window** — how long the
magnets keep their grip after the power stops, which is how long the crew has to fix a fault before
the reactor drops. Coasting down on stored charge is what every accumulator already does.

Two things fall out and neither was written:

- **You cannot raise the rods before the control system is live.** Ask for them while the magnets are
  still charging and the request is dropped on the spot. Powering up control is step one of a start.
- **Restoring power does not lift the rods.** A drop is a drop; someone walks over and raises them.

> **Maybe one day:** electromagnetic control rods as their own component — separately damageable,
> separately replaceable, with their own condition and their own failure to insert. That is where the
> fail-safe's own failure mode most naturally lives, and it would make "the rod gear is worn" a thing
> you can see and swap rather than a hidden number. Not first pass.

## Coolant

Coolant **carries heat away**; it does not make the reactor generate less. So it is not a
coefficient. Two numbers, both meaningful:

```
LatentJoulesPerLitre     heat one litre takes with it when it boils off
MaxFlowLitresPerSecond   what the pumps can actually push
```

Per tick: the heat to shed asks for a flow, the pump caps it, and the drum drains by whatever flow
actually ran. Water's latent heat of vaporisation is ~2.26 MJ/kg as a number to scale from.

Why this beats a coefficient:

- **running hot costs supplies** — the drum drains faster the harder you push, so heat has a running
  cost rather than a modifier
- **a reactor can outrun its cooling.** Max flow is a hard ceiling, so past a certain output no
  amount of coolant saves you — a limit the player discovers rather than a number that quietly scales
- upgrades split cleanly: a better **pump** raises the ceiling, better **coolant** gets more heat per
  litre, a better **core** makes less heat to begin with

Coolant is consumed, not recycled. It boils off and takes the heat with it. First pass; deliberately
lazy.

## Chambers (decided 2026-10-09; built in code 2026-10-09, untested in play)

The S5 reactor is endgame, so it gets the cool factor. The core becomes **six fuel chambers**, each burning
its own rod, seen through clear glass. Decided with Vulps alongside the new model (`component-models.md`).

**Any reactor has one or more chambers** (Vulps, 2026-10-09: a scalable class, not a six-chamber special case).
`FuelChamber` is one rod: its lever, its rod position, its fuel (a share of one rod, 1 full) and its own refuel.
`FissionCore` holds the chambers and replaces the old `Cores`: a chamber is a core, so `OutputPerChamber x
ChamberCount` is full output, and a one-chamber core behaves exactly as the old single rod did. The magnets,
rod speeds and rod life are shared. `ReactorBehaviourModule` (`_chambers`) gives its levers axes: **0 is the
master, 1 to N each chamber**; chamber i is fed by the i-th `LoadingModule` taking fuel rods. A `LoadingModule`
can be bound to one item port (`One Port`), so a chamber is filled only through the port above it, by hand, by
a loader or along a conduit. Network state is the master lever, coolant, flow and grip, then each chamber's
lever, rods and fuel (4 + 3N); a lever moved elsewhere goes to the master as `ReactorDial` (axis, value).
The Reactor prefab has six chambers sharing the old core's 1000 W (166.67 W each) and a `NetworkBridgeData64`
(33 ints).

**Shown (2026-10-09, reworked 2026-10-10 with Vulps):** `ReactorChamberRods` copies the Fuel Rod's model into each
chamber: the burning rod low down while the chamber has one, and the waiting rod (its `LoadingModule`'s) above it,
dim. The burning rod glows by its own fuel (bright blue fresh, dim violet spent) and its own rods: nearly dark
fully in, bright fully out, pulsing harder and faster the further out. Every rod leans toward amber-red as the
core nears its working temperature, and redder past it. A spent rod drops out of the bottom of its chamber into
the base housing, where it is hidden, as the waiting rod slides down into its place; with none waiting the
chamber is left empty. The core column's bands brighten with output, and a blue **Core Light** (replacing the old
reactor's green lamp, `ReactorLamp`, removed from the prefab) travels up and down inside the column, faster the
further the rods are out on average; from 70% it is too fast to see and holds in the middle, solid, bright and
wide. Dark with no output. All worked out on every client from the chamber state: nothing new is sent. The
readout is a table, a column per chamber: fuel and rod position in percent, and temperature (the chamber's own
load's share of the working temperature, plus the core's excess), with a dot where a chamber has no rod.

- **Each chamber is fed from above.** An item port on the reactor's top face over each chamber, each with
  its own `LoadingModule`: one rod burning, one waiting (`loading.md` → the spare is the standard). A rod
  goes straight down into its chamber. Height is not a problem: ships get gantries in engineering, and
  walls and roofs will be climbable.
- **Output scales with loaded chambers.** Each chamber makes its share at its own rod position; three
  loaded chambers at full make half power. Refuelling is topping up a chamber, not a stop.
- **A spent rod drops visibly out of the bottom of its chamber** and vanishes inside the housing. It never
  comes out of the model: no item, nothing networked; every client plays it from the chamber going empty.
- **Seven levers in the front control bay:** one per chamber, and a master that sets all six at once
  (SCRAM included). When the master moves, the six move with it visibly, so the bay always shows what the
  chambers are doing. The magnet fail-safe stays shared: losing grip drops all six.
- **Heat to the grid is unchanged.** The grid asks the reactor for heat watts and a baseline, never a
  temperature; the chambers hand it their sum and their combined load. That is the same as averaging,
  so the damage model does not change. A chamber's own heat is **visual only**: it glows by its own load,
  worked out locally. One chamber at full with five off glows hot but adds a sixth of the heat, and nothing
  is damaged. Accepted: the glow shows where the heat comes from.
- **What each chamber shows:** colour from its rod's fuel (bright blue fresh, fading to a dim violet spent),
  a heat tint toward amber-red shared by all six, and a pulse worked out locally from output, each chamber
  slightly out of phase so they shimmer.

**Networking.** The master owns every chamber. State per chamber: rod position, fuel, and whether a rod
is in. All of it is sent as state, so a late joiner sees every chamber right. A non-master's hand on a
lever asks the master, as the single lever does now. The master lever only writes those six positions,
so it adds nothing to send. Pulse, glow and the spent-rod drop are derived on every client; nothing new
is sent for them.

## Physical handling

Both consumables are placed by hand, and the animation *is* the readout:

- **fuel** — rod packs, fed in one rod at a time by hand or from a loader: `loading.md`. The core burns
  one rod and takes the next from its `LoadingModule` when spent; nothing loads the module yet. Becoming
  six chambers fed from the top (Six chambers, above).
- **coolant pad** — a metal drum set on a pad, draining as it is used.

No inventory screen, no gauge required. Consistent with the contract wall and the conduit run: the
physical act is the information.

## Open

- **⚠ The fail-safe fires even less often now.** Self-sustaining means no external fault can drop
  the rods at all, so the fail-safe is close to unreachable. It still needs a way to *fail* — see
  below — but it also needs a way to *fire*.

- **⚠ The fail-safe needs its own fail condition.** As written, losing power drops the rods and the
  reactor safely shuts down — which means a runaway **can never happen** and a whole game-over
  scenario disappears with it. Something has to be able to stop the rods falling: jammed mechanism,
  welded magnets, debris in the channel, a control system that has taken enough damage. The
  mechanism already exists — the rod gear is a component, and a **worn component misfires**
  (`power.md` → Failure). A SCRAM that fails to insert is a misfire on the most important part of the
  ship, and it only happens once the crew has let the machinery get that bad.
- ~~**Do the rod magnets draw from the grid?**~~ **Yes**, and they are not a separate machine — the
  reactor simply has an input and works or not on available power, like everything else. What holds
  them is an ordinary `LoadBehaviour` over the reactor's hold, whose capacity is the window. Built.
- ~~**Does a running reactor have a baseline temperature?**~~ **Yes.** Heat per watt delivered *is*
  the baseline, not the overflow — so a reactor holds a working temperature proportional to how hard
  it is running, and only waste accumulates on top of that. Damage measures what it is minus what it
  should be, so a reactor is never damaged for merely working. Built, and it forced conduction to
  move **excess** heat rather than absolute temperature, or a pinned baseline manufactures energy.
- ~~**Manual or automatic coolant flow?**~~ **Manual.** Someone turns it up and has to remember to
  turn it down, or the drum runs dry. Built.
- ~~**Meltdowns.**~~ **Not modelled, deliberately.** The damage system already covers it: a reactor
  run too hot takes hits and is eventually lost. No separate meltdown state, no positive void
  coefficient.

- ~~**Cold start.**~~ **Resolved.** A start needs a charged cell, and charging that cell from the
  reactor is a loop, but the reactor and the cell are components, so it is not a ring and nothing
  warns. The rods rise on their own once the starter has charged the magnets.

See also: `power.md` (the built model), `open-questions.md`, `principles.md`.

## Open: an overproducing reactor bakes its own cables


Since power only goes where a receiver can be reached (`power.md`), a reactor with nowhere to send its
output takes it as heat itself. At the current settings that is 100 C a second at 2000 W, and heat
conducts along the metal with no current in it, so in the harness two minutes left an 8-tile run at
about 1300 C behind a 1384 C reactor. Nothing pops while nothing flows (pop chance is power x heat),
but reconnecting any receiver would send full power down cables already that hot.

Options: stop a producer's surplus conducting into conduits; give the reactor a heat ceiling it
scrams at; or treat it as the crew's problem (throttle the reactor), which is the lever it is for.
Undecided.

## Reactor hum, generated in code (planned, logged 2026-10-03)


The reactor's sound, synthesised live (`OnAudioFilterRead`) rather than a recording, since a good mechanical
hum is hard to find: a base tone around 50 to 60 Hz with two or three harmonics, a little filtered noise for
texture, and a slow wobble so it never sounds like a test tone. **It follows the reactor:** pitch and loudness
rise with output, it roughens as integrity drops, and it falls silent when the rods are in, so engineering hears
the reactor working hard or struggling before reading anything (principle 2). Built from the reactor's own
networked state, so every client hears the same; nothing new to send. A recorded engine-room bed could be
layered under it later.

## Coolant consumption (planned)


Coolant exists only inside the reactor today (`CoolantLoop`: a drum, a pump ceiling, a flow the crew
sets, and litres boiling off as it carries heat away). It needs to become something the ship
consumes and the crew has to manage.

The coolant upgrade for branches is in `conduits.md`.
