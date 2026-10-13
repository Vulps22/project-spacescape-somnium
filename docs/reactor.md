---
aliases:
  - "The reactor"
tags:
  - component
sizes:
  - S1
  - S2
  - S3
  - S4
  - S5
---
# The reactor

**Built and running in the scene.** `FissionCore`, `CoolantLoop`, `ReactorBehaviour`, and the Unity
`ReactorBehaviourModule` + `CapacitorModule` + `ReactorLamp`, all covered by the sim tests. Coolant is a preset
stand-in, and fuel comes a rod at a time from a `LoadingModule` ([[loading]]); the coolant pad is not built.

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

> [!failure] Recorded so it is not re-derived
> The first sketch was `produced × cores ÷ rodPosition`. That
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

### Open: heat-ratio heat and its counters

**Status (2026-10-09):** talked through; not decided.

**Today the heat ratio does nothing.** `FissionCore.HeatWatts` (output × `HeatRatio`) is computed but nothing
reads it: a reactor's temperature comes from `WorkingCelsius` and how hard it runs, and only heat above that
conducts into the grid. So the ladder above is a design never connected, and the smaller reactors
([[#Sizes|Sizes]]) depend on it. Vulps: a 15 : 1 S1 would wreck a ship's grid
fast; not a reason not to have it, but there must be something to install that offsets or protects against it.

> [!info] Proposed
> Heat-ratio heat is real heat that must leave the reactor's tile every second, by one of three exits:
> 1. **Coolant**, consumed: the drum drains faster the more heat it carries (exists).
> 2. **Radiators**, passive: nothing spent, but each has a hard limit and costs a slot or wall space.
> 3. **The grid**, the bad exit: whatever coolant and radiators do not take conducts out along the cables.

**Counters a crew installs:**
- **Thermal break** (conduit addon): a ceramic section in one cable tile that carries current but blocks heat. The
  cheapest answer to the S1: one on its output keeps the heat at the reactor.
- **Thermostat** (conduit addon): opens the tile when too hot, closes once cool; can be set to stay open until reset
  by hand. An open switch still conducts heat ([[power]]), so it pairs with a thermal break.
- **Circuit breaker** (conduit addon): trips past a set current, cutting a wasteful branch loose before its heat
  reaches the trunk ([[power#Failure|power → Failure]]).
- **Radiator** (S1 or S2 component, or a wall panel): glowing fins shedding heat to space, more the hotter they get;
  the fins are the readout. S1 reactor plus radiator as the standard early-ship pair.
- **Coolant upgrade** (branch upgrade circle, already planned): cools a whole branch, the mid-game answer.
- **Data:** a thermostat sends its temperature to a logic board, which scrams the reactor or opens breakers. The
  late-game answer.

**The balancing lever is the output number:** the S1 makes 67 kW, so 1 MW of heat ([[#Output and heat (Vulps, 2026-10-11)|Output and heat]]); the
counters are sized against that.

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

> [!note] Maybe one day
> Electromagnetic control rods as their own component — separately damageable,
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

## Refuelling

**Status (2026-10-08):** built, untested in-world. How loading works is [[loading]]; the rod is [[fuel-rod]].

- **The reactor refuels from its `LoadingModule`.** `FissionCore` has **Fuel Seconds Per Rod** (the reactor's Inspector field,
  formerly Fuel Seconds; 500 on the prefab) and `FuelLevel`, 100% to 0%. Spent, it calls its `Refuel`, which
  the reactor module answers by consuming from its fuel-rod `LoadingModule`; if that held one, the core is
  full again at once. It starts with one rod in. The reactor's readout shows its own `fuel 87%`; the `LoadingModule` has its own
  **Readout** field, showing `FuelRod: next in / none`. Every client
  runs this in its sim and the master's state (fuel, held or not) corrects it, as for the rest of the reactor.
  Sim test: `SpentRodTakesTheNextOne`.

## Chambers

**Status (2026-10-09):** decided; built in code, untested in play.

The S5 reactor is endgame, so it gets the cool factor. The core becomes **six fuel chambers**, each burning
its own rod, seen through clear glass. Decided with Vulps alongside the new model ([[component-models]]).

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
  its own `LoadingModule`: one rod burning, one waiting ([[loading]] → the spare is the standard). A rod
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

> [!info] Networking
> The master owns every chamber. State per chamber: rod position, fuel, and whether a rod
> is in. All of it is sent as state, so a late joiner sees every chamber right. A non-master's hand on a
> lever asks the master, as the single lever does now. **The master lever moves on its own while held**
> (sent, so everyone sees it) **and sets every chamber only when let go** (2026-10-10): each chamber's lever
> turns to it as if by hand, through the chamber lever's own message, so it adds nothing new. SCRAM still sets
> every chamber at once. Pulse, glow and the spent-rod drop are derived on every client; nothing new
> is sent for them.

## Networking

**Status (2026-09-28):** written, compiles clean; tested in-world with many players. Every component's state travels
as [[networking#Steps|networking → Steps]] step 2 describes: the reactor's dial, rod position, fuel, coolant litres,
flow and magnets gripping, in a `NetworkBridgeData12` (11 used).

The rod lever turns the dial here at once (`TurnDial`) and `ComponentNetwork` sends it to the master up to 10 times a
second; while a hand holds it (`DialHeld`) the incoming dial is ignored, so an older value cannot pull the lever out of
the hand.

| State | Tier | Decided by | Travels as |
|---|---|---|---|
| Reactor rod lever / dial | fact | master, on the holder's request | a value, streamed while held? |

## Sizes

Each size is its own machine, not a shrunken S5 ([[component-models#Rules (decided with Vulps, 2026-10-09 and 10)|component-models → Rules]]),
each on its own rung of the heat-ratio ladder ([[#The heat ratio is the real upgrade]]). Slot sizes: [[slots]].

### Output and heat (Vulps, 2026-10-11)

**Status (2026-10-11):** decided; not yet on the prefabs (the S5 prefab makes 1000 W, in the old scale).

**Output is set by charging the same-size jump drive** ([[jump-drive#Sizes|jump-drive → Sizes]]) with all of it: the
[[slots#S1|S1]] in 5 minutes, the [[slots#S5|S5]] in 30 seconds. The leaps grow like the drives' efficiency (small,
decent, chunky). **Heat ratios fall on the same shape.** Heat = output × ratio.

| Size | Output | Leap | Charges its own drive in | Heat ratio | Efficiency | Heat at full output |
|---|---|---|---|---|---|---|
| [[slots#S1\|S1]] | **67 kW** | | 5 min | **15 : 1** (RTG) | 6% | 1 MW |
| [[slots#S2\|S2]] | **400 kW** | ×6 | 4 min 35 s | **10 : 1** (advanced thermoelectric) | 9% | 4 MW |
| [[slots#S3\|S3]] | **3.2 MW** | ×8 | 2 min 36 s | **4 : 1** (naval reactor) | 20% | 12.8 MW |
| [[slots#S5\|S5]] | **33 MW** | ×10.4 | 30 s | **1 : 1** (beyond today's fission) | 50% | 33 MW |
| [[slots#S4\|S4]] | | | | | | |

- **Heat grows with size, and falls against power:** each bigger reactor is a bigger heat problem in watts and a
  smaller one for what it makes. The early game runs hot.
- **House load is 2% of output, so a bigger reactor costs more to kick-start** (Vulps, 2026-10-11): S2 8 kW, S3
  64 kW, S5 660 kW. The magnets' capacitor is sized to keep a 2 s grip window (the S5's holds 1.32 MJ). The magnets
  grip only once it is full and it banks whatever arrives, so **any source starts any reactor, a bigger one just
  takes longer**: an [[#S1 RTG|S1]] fills the S5's in about 20 s, a 1 W trickle in about 15 days. The rods then reach
  the 2% that covers the house load well inside the window. The S1 has no house load at all.

### S5

**Status (2026-10-10):** built and tested in-world: the real Reactor prefab carries the model; the chambers'
mechanics are in [[#Chambers|Chambers]].

- A tall hexagonal vessel in a heavy braced frame, with **clear glass**: six fuel chambers inside, each
  rod visible, glowing by its own fuel (bright blue fresh, dim violet spent), tinted amber-red together
  as the reactor heats, pulsing slightly out of phase with each other.
- **Six item ports on the top face**, one over each chamber, in place of the ring of rod sockets. A rod
  drops straight into its chamber; a spent one drops visibly out of the chamber's bottom and vanishes
  inside the housing, never out of the model.
- Coolant pipes loop out to the sides.
- **The front control bay holds seven levers:** one per chamber and a master. The master moves on its own
  while held and sets all six when let go (2026-10-10).
- **The model:** `Blender/Reactors/Reactor.py` → `Meshes/Reactor.fbx` (the placeholder rods and levers in the script
  are for renders only and are left out of the FBX). `Prefabs/Components/Reactor Draft` (built by **SpaceScape > Build
  Reactor Draft**, `Packages/com.spacescape.editor/Editor/ReactorDraftBuilder.cs`) was the approval step.
- **On the real Reactor prefab** (2026-10-09/10): the new model, Front ZPlus; six item ports on top (YPlus), each
  feeding one chamber's `LoadingModule`; In, Data and Out on the back I/O panel at cells (3, 0), (4, 0), (5, 0);
  seven working `RodLever`s; non-convex mesh colliders on the Frame, Vessel and Control Bay, on only while it is
  locked in its slot (`CarriedComponent`).
- **Shown on the model** (`ReactorChamberRods`): each chamber's rod, glowing from spent to fresh by its fuel and by
  load, pulsing, tinted toward amber-red with heat; a spent rod drops out of the bottom and the waiting rod slides
  down into its place. A **blue core light** in the central column travels up and down faster as the average rod
  position rises, and from 70% holds still in the middle, bright and wide. The **screen** shows a readout table
  (rod | fuel | position | temp, in TMP). The old green lamp is gone.
- **Ports on the grid:** a regular ring of six cannot land on 0.5 m cells, so the chambers sit at (x, z) (1.25, 0.25),
  (0.25, 1.25), (-0.75, 0.75), (-1.25, -0.25), (-0.25, -1.25), (0.75, -0.75): 1.20 to 1.27 m out, a slight pinwheel.
  Five chambers fit worse (up to 34 degrees off even, against 11 for six).
- **The bay:** one side profile carried across: a front face, a deck sloping up and back where the levers stand, and a
  tower in front of the glass with the screen above the handles. The master lever is on the viewer's right.
- **The I/O panel** is on the back, at the base; the back braces' lower feet stand wider to clear it.
- **The vessel is sized to the ports**, which are fixed by the grid: 2.16 m to its corners, so every port collar sits
  inside the cap's inset, still clear of the side braces (2.25 m). Brace ends run into the plinth and the top beams.
- **The plinth fills the whole footprint** (5 × 5 m), so the frame's feet stand on it, flush with the slot's edge. The
  component keeps its own base rather than standing on the ship's floor: a slot can face any way up ([[grid]]), and
  a component is carried and displayed out of its slot.

### S4: The Bottle

**Status (2026-10-11):** an antimatter reactor, decided (Vulps); the rest proposed.

5 × 4 × 4 m; one slot on the ship, the rarest.

- **Antimatter** (Vulps, 2026-10-11): it makes power by annihilating antimatter with matter in the chamber. The
  densest fuel physics allows: 1 g of antimatter with 1 g of matter is **180 TJ**. Real antimatter is held in a
  vacuum by magnetic fields (a Penning trap), which is what the bottle is.
  - **The tank, not the tap:** its output rating is finite, like any reactor's; what the fuel buys is that it
    practically never runs dry. At 100 MW and about a third efficient, one gram lasts about a week of non-stop
    running.
  - **Neutrinos:** a share of each annihilation leaves as neutrinos, which pass through everything: neither power nor
    heat. They negate the jump fuel cell's reaction ([[jump-drive#Neutrinos negate the reaction (Vulps, 2026-10-11)|jump-drive → Neutrinos]]).
  - **Install or sell** (Vulps, 2026-10-11): one turning up is a choice between near-endless power in the one
    [[slots#S4|S4]] slot and selling it, highly sought after, for a fortune. Money is not designed yet.
- **Not a fission pile:** a long horizontal magnetic bottle running the full 5 m, a glass tube ringed by heavy
  magnet coils, with a thin white-violet plasma line burning down its middle, seen in the gaps between coils.
- **No rods.** The magnets hold a field instead of lifting rods: the same house-load mechanic. One large
  throttle wheel sets field strength; the line thickens and brightens as it opens.
- **SCRAM:** the field collapses and the line snaps out in a flash. The most dramatic shutdown in the game.
- **Frame:** low and wide, a cradle under the bottle rather than a cage round it, the console at one end like a
  ship's helm.

### S3: The Pool

**Status (2026-10-12):** built, untested in-world: `Prefabs/Components/Reactors/Reactor S3`, registered in
`SceneNetworking`, not yet on any slot.

**8 × 8 × 8 cells** (a 4 m cube). 3.2 MW, heat 4 : 1 (naval reactor), 12.8 MW of heat at full output. The workhorse
mid-game reactor.

```
FRONT (from the room)

 ┌──────────────────────────────┐
 │█ ═══╦═══ gantry ▣ ═══╦═══   █│  hatch in the middle, fin drive motors on the bridge
 │█ ▓▓▓▓▓▓ grated lid ▓▓▓▓▓▓▓  █│
 │█ ┃~~~~~~~~~~~~~~~~~~~~~~~┃ ▭ │  water line = coolant left
 │█ ┃      ║ ▓▓ ║          ┃ ═ │  gauge marks on the tank wall
 │█ ┃      ║ ▓▓ ║  fins     ┃ ═ │  the rod floats between the fins
 │█ ┃      ║ ▓▓ ║          ┃   │  glowing as every reactor's rod does
 │█ ┗━━━━━━━━━━━━━━━━━━━━━━━┛   │
 ╞══════════════════════════════╡ plinth
```

- **One chamber** (Vulps, 2026-10-11): one rod, one lever, one hatch. The [[#S5|S5]] keeps the six.
- **The tank:** armoured walls with a window band on the front, so the water and the rod read from the floor, not
  only from above.
- **The lid:** a grated walkway, fixed and solid (Vulps, 2026-10-11): a mesh collider like the rest of the model,
  never grabbed or moved, so it can be stood on to look down into the pool.
- **The water level is the coolant:** the `CoolantLoop` drum's litres as a share of a full drum. As coolant boils off
  the level drops down a marked gauge on the tank wall. No screen needed to know you are low.
- **Bubbles:** a fine stream rising off the rod, more with output; a rolling boil once the reactor is above its
  working temperature, the sign it is running hot.
- **No core** (Vulps, 2026-10-12): one of our fuel rods floats in the middle of the pool, held between the control
  blades, and glows as every reactor's rod does (`ReactorChamberRods`). That is the Cherenkov glow.
- **Control rods:** four radial fins round the rod, hanging from an overhead bridge gantry spanning the top, drive
  motors exposed. Fully in they hold the rod along its length; you watch them rise out of the water (20 s) and drop
  back in on a SCRAM (2 s) with a splash. A full drum's water line is low enough that withdrawn fins clear it.
- **Fuel:** a hatch on the gantry with the item port on top (YPlus), in the middle of the pool (Vulps, 2026-10-12): a
  square duct runs from under the port down through the lid. A rod goes straight down and is seen sinking through
  the water into its place between the fins. With 8 cells the slot's centre is a cell edge, which no port can sit on,
  so the port is on the nearest cell and the pool, the fins and the gantry are centred on it, a quarter-metre off the
  slot's centre.
- **Control panel:** the S5's control bay on the front, beside the window: a front face, a deck sloping up and back
  where the one rod lever stands, and a tower carrying the screen with the readout table (one column). No master
  lever with one chamber.
- **I/O panel:** on the back at the base, three cells wide, matching the S3 battery's: In on the right, Data in the
  middle, Out on the left, seen from the front.
- **Frame and plinth:** as every size: the plinth fills the footprint, the frame braced on the sides and back, the
  front open.
- **Numbers:** working temperature 325 °C (a naval PWR's, Vulps, 2026-10-11); house load 64 kW, with a 128 kJ magnet
  capacitor for the 2 s grip window; rods 20 s up, 2 s down; 500 s a rod; coolant as the S5's.
- **Networking:** one chamber is 7 of the reactor's ints, 13 with the rest of the component's, so a
  `NetworkBridgeData16`. The water level, bubbles, glow and blades all come from state already sent (coolant litres,
  rods, output); nothing new.
- **On the prefab** (2026-10-12): a copy of the S5's modules cut to one chamber: one `LoadingModule` bound to the port,
  one `RodLever` (**Rod Lever**, axis 1) on the bay's deck. `ReactorChamberRods` shows the floating rod with no waiting
  rod (no room in the duct); a spent one sinks to the floor and is gone. `ReactorPoolShow` scales the water by the
  coolant left, lifts the blades with the rods (their cables shortening to the motors), raises bubbles off the rod
  with output and a boil past the working temperature, and splashes as the blades hit the water coming down. Mesh
  colliders on the Frame (the lid and bridge can be stood on) and Tank; 1200 kg.
- **Sound:** on pause (Vulps, 2026-10-12). A bubbling loop following the output alone, as the S2's gun hum does, once a
  sound is found.
- **Files:** `Blender/Reactors/ReactorS3.py` → `Meshes/ReactorS3.fbx`, the `Model` on
  `Prefabs/Components/Reactors/Reactor S3`, sharing `reactor_parts.py` with the S5.

### S2: The Rail

**Status (2026-10-12):** built, untested in-world: `Prefabs/Components/Reactors/Reactor S2`, registered in
`SceneNetworking`, not yet on any slot.

**6 × 4 × 4 cells** (3 × 2 × 2 m), long on x. 400 kW, heat 10 : 1, 4 MW of heat at full output. The common reactor
for early ships, and a hot one.

An **accelerator-driven core**: a core that cannot keep itself going. A particle beam fired down a 3 m accelerator
keeps it burning; cut the beam and it stops at once. It runs 10 : 1 because it pays to run its own accelerator.

```
FRONT (from the room)

                                   ▣ fuel port
 ┌──────────────────────────────╨───┐
 │ ◉ gun  ║║ ║║ ║║ ║║ ║║ ║║  ┏━━━━┓│  coils light in sequence toward the target
 │ ══════ ━━━━━━ beam ━━━━━━━▶┃ ▓▓ ┃│  target vessel: the rod sits inside
 │ ▭ screen  ┃ lever          ┗━━━━┛│
 ╞══════════════════════════════════╡ plinth
```

- **One chamber:** one rod, one lever, one port.
- **The tube:** a glass accelerator tube along x, ringed by coils, nearly the full length. The gun is on the left
  and the target vessel holding the rod on the right, seen from the front, so the beam reads left to right.
- **The fail-safe is the beam:** house load powers the gun, so losing grip or a SCRAM cuts the beam and the core
  dies at once. Same mechanic as the others, a different feel. On a SCRAM the beam drains away down the tube and
  the coils go dark one after another, gun to target.
- **The throttle is beam current:** one lever, the same withdrawal as a rod lever in the sim; 20 s up, 2 s down.
- **The model is the readout:** the coils light in sequence toward the target, chasing faster with output; the beam
  thickens and brightens with current; the rod in the target glows by its fuel as every rod does, and the vessel
  goes from blue to amber-red with heat. Running hot, the beam goes ragged and flickers.
- **Fuel:** the item port on the top face over the target (YPlus), on the nearest cell centre, the vessel placed to
  match. A rod drops straight down into it, by hand, loader or conduit; a spent rod drops out of the bottom into
  the base.
- **Control panel:** at the gun end on the front, amber trim: one screen with the readout table (one column) and the
  beam-current lever. No master lever with one chamber.
- **I/O panel:** on the back at the base, three cells wide, matching the S2 battery's: In on the right, Data in the
  middle, Out on the left, seen from the front.
- **Frame and plinth:** the plinth fills the footprint; low saddle clamps cradle the tube, braced on the sides and
  back, the front open. Mesh colliders on the frame, tube and vessel, on only while locked in its slot.
- **Numbers:** working temperature 400 °C (MYRRHA, a real accelerator-driven design, runs up to about that); house
  load 8 kW, with a 16 kJ magnet capacitor for the 2 s grip window; 500 s a rod; coolant as the S5's, with no
  visual of its own.
- **Networking:** one chamber is 7 of the reactor's ints, 13 with the rest of the component's (stamp, heat, severed,
  capacitor, integrity, one fuel supply), so a `NetworkBridgeData16`. The coils, beam, target glow and flicker come
  from state already sent (rods, output, fuel, temperature); nothing new.
- **On the prefab** (2026-10-12): a copy of the S5's modules cut to one chamber: one `LoadingModule` bound to the port
  over the target, one `RodLever` (**Beam Lever**, axis 1: the chamber itself, no master) on the bay's deck at the S5's
  height. `ReactorChamberRods` shows the rod in the target with no waiting rod (`Show Waiting` off: no room above
  it). `ReactorRailShow` lights the coils, the beam (scaled across with current, flickering hot) and the vessel's
  bands. Mesh colliders on the Frame and Rail, on only while slotted; 600 kg. The screen is the shared battery
  screen; with one chamber the readout has no master.
- **Sound** (Vulps, 2026-10-12): the gun hums (`Loops_Electric_Testla_05`, looping, from a **Gun Hum** source at the
  muzzle) while the reactor makes power, following its output alone: louder and higher with it (pitch 0.85 up to 1 at
  full), silent with none. Each reactor starts the loop at a random point.
- **Files:** `Blender/Reactors/ReactorS2.py` → `Meshes/ReactorS2.fbx`, the `Model` on
  `Prefabs/Components/Reactors/Reactor S2`, sharing `reactor_parts.py`.

### S1: RTG

**Status (2026-10-12):** built, untested in-world: `Prefabs/Components/Reactors/Reactor S1`, registered in
`SceneNetworking`, not yet on any slot. The sound is a placeholder (the packs' lid, lower).

**4 × 4 × 4 cells** (a 2 m cube). 67 kW, heat 15 : 1 (a radioisotope thermoelectric generator's), 1 MW of heat at
full output. It is supposed to suck: tiny output, huge heat, a long life per rod.

```
FRONT (from the room)

 ┌────────────────────┐
 │█ ║║║║║║║║║║║║║║║ █│  fins: dull to orange-red with heat
 │█ ║║ ┌────────┐ ║║ █│
 │█ ║║ │cartridge│ ║║ █│  on rails; pulled out = off
 │█ ║║ │▮▮▮▮▯▯▯ │ ║║ █│  fuel strip
 │█ ║║ └────────┘ ║║ █│
 ╞════════════════════╡ plinth
```

- **A thermoelectric drum:** a squat cylinder covered in radiating fins. It always runs hot. You never see inside.
- **The cartridge, on rails** (Vulps, 2026-10-11): it slides out of the front like a heavy drawer, far enough to clear
  its top, and stays attached. Refuelling is pull it out, put a fuel rod in, push it back. Pushed fully home it is on
  at full output at once; pulled out it is off. That is the only way off.
- **Opening and closing are the packs' lid mechanic** (Vulps, 2026-10-12; [[packs#How a pack works|packs → How a pack works]]): a
  tug out on the shut cartridge starts it sliding fully out on its own, a push in on the open one starts it sliding
  home, and either lets go of the hand. A clunkier sound than the packs' lid, to be found by Vulps.
- **One rod, by hand only:** the cartridge is open at both sides (Vulps, 2026-10-12): a top and a bottom plate between
  its front and back panes, with a four-sided point in the middle of each (2026-10-12). The rod stands upright between
  them, held tip to tip by its own pointed ends, put in from the side while the cartridge is out. It is not a grid port, so no conduit or loader reaches it. It takes one rod and refuses another while one is in; no spare
  waits. A part-used rod is never taken back out (Vulps, 2026-10-11). A spent rod just vanishes: no animation, since
  the inside is never seen.
- **A rod lasts an hour** (3600 s, Vulps, 2026-10-11).
- **No rods, no throttle, no magnets:** so no house load, no capacitor and no starter. It cannot be throttled, and
  a producer cannot decline, so whatever has nowhere to go becomes heat on its own tile.
- **Its real job** (Vulps, 2026-10-11): cold-starting a bigger reactor, and backup power when the main reactor goes
  dark. Kept cold with the cartridge out, it is at full output the moment the cartridge goes in. Best kept somewhere
  well ventilated.
- **The model is the readout; no screen** (Vulps, 2026-10-11): the fins glow from dull to orange-red with heat, and a
  strip on the cartridge face shows the rod's fuel.
- **I/O panel:** on the back at the base, **Data and Out only** (Vulps, 2026-10-11): with no house load there is
  nothing to feed. Data on the right, Out on the left, seen from the front.
- **Frame and plinth:** as every size: the plinth fills the footprint, the frame braced on the sides and back, the
  front open. Mesh colliders on the frame, drum and cartridge, on only while locked in its slot.
- **Numbers:** working temperature 600 °C, where the fins glow orange; 3600 s a rod; coolant as the S5's.
- **Networking:** a `NetworkBridgeData16` (13 ints: no capacitor, one more for the cartridge). New to this size:
  whether the cartridge is out, the master's state in `ReactorCartridge`. A hand elsewhere slides it at once and sends
  `CartridgeTug` (message 10) to the master; every client runs the slide from the state. Whether a rod is in is the
  chamber's fuel and its fuel supply's, already shared. The fins and the fuel strip come from state already sent.
- **On the prefab** (2026-10-12): a copy of the S5's modules with no magnets (`Magnets` off, so no capacitor and no
  house load), starting with no rod (`Starts With Rods` off), its rods instant (on the moment the cartridge is home).
  One `LoadingModule`, fed only by the cartridge. A `TugSlide` (the packs' lid mechanic) slides the cartridge 1.02 m out
  of the front from a grip on its handle; `ReactorCartridge` sets the chamber's lever from it (home: full; out: off),
  takes a rod let go near its place between the points while it is out and empty, and shows the rod's model there.
  `ReactorRtgShow` lights the fins with heat and the fuel strip by the rod's fuel. No lever, no screen. Mesh colliders
  on the Frame, Drum and Cartridge (moving with it); 250 kg.
- **Files:** `Blender/Reactors/ReactorS1.py` → `Meshes/ReactorS1.fbx`, the `Model` on
  `Prefabs/Components/Reactors/Reactor S1`, sharing `reactor_parts.py`.

### What S1 to S4 would change

**Status (2026-10-09):** to decide here, with networking, before any is built.

Every one keeps the house rules ([[component-models]]). Several change mechanics, not just looks:

- **Mechanics each would add:** the S1 cannot be throttled; the S2's fail-safe is its
  beam, not gravity; the S3's water level is its coolant; the S4 controls a field instead of rods and eats
  spent rods.
- **They need counters** (Vulps: a 15 : 1 S1 would wreck a ship's grid fast; not a reason not to, but there must be
  something to install that offsets or protects against it). The proposed counters, and the fact that the heat
  ratio is not yet connected to anything, are in [[reactor#Open heat-ratio heat and its counters|reactor → Open: heat-ratio heat and its counters]].

## Physical handling

Both consumables are placed by hand, and the animation *is* the readout:

- **fuel** — rod packs, fed in one rod at a time by hand or from a loader: [[loading]]. The core burns
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
  ([[power#Failure|power → Failure]]). A SCRAM that fails to insert is a misfire on the most important part of the
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

See also: [[power]] (the built model), [[principles]].

## Open: an overproducing reactor bakes its own cables


Since power only goes where a receiver can be reached ([[power]]), a reactor with nowhere to send its
output takes it as heat itself. At the current settings that is 100 C a second at 2000 W, and heat
conducts along the metal with no current in it, so in the harness two minutes left an 8-tile run at
about 1300 C behind a 1384 C reactor. Nothing pops while nothing flows (pop chance is power x heat),
but reconnecting any receiver would send full power down cables already that hot.

Options: stop a producer's surplus conducting into conduits; give the reactor a heat ceiling it
scrams at; or treat it as the crew's problem (throttle the reactor), which is the lever it is for.
Undecided.

## Reactor hum, generated in code

**Status (2026-10-03):** planned.

The reactor's sound, synthesised live (`OnAudioFilterRead`) rather than a recording, since a good mechanical
hum is hard to find: a base tone around 50 to 60 Hz with two or three harmonics, a little filtered noise for
texture, and a slow wobble so it never sounds like a test tone. **It follows the reactor:** pitch and loudness
rise with output, it roughens as integrity drops, and it falls silent when the rods are in, so engineering hears
the reactor working hard or struggling before reading anything (principle 2). Built from the reactor's own
networked state, so every client hears the same; nothing new to send. A recorded engine-room bed could be
layered under it later.

## Coolant consumption

**Status:** planned.

Coolant exists only inside the reactor today (`CoolantLoop`: a drum, a pump ceiling, a flow the crew
sets, and litres boiling off as it carries heat away). It needs to become something the ship
consumes and the crew has to manage.

The coolant upgrade for branches is in [[conduits]].
