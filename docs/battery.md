---
aliases:
  - "Batteries"
tags:
  - component
sizes:
  - S0
  - S1
  - S2
  - S3
  - S4
  - S5
---
# Batteries

What the batteries are today, one prefab per slot size, and what each is designed to be. The model side (look,
frames, materials) follows [[component-models]]; the power model they sit in is [[power]].

**Status (2026-10-11):** [[slots#S0|S0]], [[slots#S1|S1]], [[slots#S2|S2]], [[slots#S3|S3]] and [[slots#S5|S5]] are
built as prefabs, untested in-world. The [[slots#S4|S4]] is parked until after the ship is built.

## Today: the battery prefabs

`Prefabs/Components/Batteries/Battery S0` to `Battery S5` (no S4). **`Battery S1` is the old `Battery.prefab`,
moved and rebuilt in place** (2026-10-11), so every slot, tutorial and network registration that pointed at it still
does. Each is the old battery's set of modules on its own model, which is the readout.

| Prefab | Holds | Charges and discharges at up to | Mass | Readout script |
|---|---|---|---|---|
| [[slots#S0\|S0]] | 200 kJ | 13 kW | 25 kg | none (no readouts, by design) |
| [[slots#S1\|S1]] | 1 MJ | 67 kW | 400 kg | `BatteryCells` |
| [[slots#S2\|S2]] | 6 MJ | 400 kW | 700 kg | `BatteryCells` |
| [[slots#S3\|S3]] | 50 MJ | 3.2 MW | 1400 kg | `BatteryLift`, `BatteryFilaments`, `BatteryHum`, `BatteryArcs` |
| [[slots#S5\|S5]] | 500 MJ, 550 MJ overcharged | 32 MW | 2000 kg | `BatteryRing`, `BatteryHum`, `BatteryArcs`, `OverchargeVentModule` |

- **Capacities** are the ladder's ([[#Capacities (Vulps and Claude, 2026-10-10)|Capacities]]). **Rates and masses
  are presumptions, to be balanced** (Vulps, 2026-10-11): rates follow the capacity leaps, so every size empties in
  about 15 s at full draw, and land on the same-size reactor's output ([[reactor#Output and heat (Vulps, 2026-10-11)|reactor → Output and heat]]).
- **Ports:** In, Data and Out on the back's I/O panel (the [[slots#S0|S0]]: In on its -X face, Out on +X). Data
  publishes nothing yet. The old S1's In and Out were on its sides, so conduits laid to those no longer meet a port.
- **Screen:** the old three text readouts (Capacity, Node, Integrity) on the control panel's screen, stacked, each
  auto-sized to fit; Vulps lays the text out. **Every size has the same screen** (Vulps, 2026-10-11): 0.575 × 0.70 m,
  1.25 times the width of the [[slots#S5|S5]]'s first one, after the first screens were unreadably small. Each
  panel is sized to fit it: 0.685 m wide, 1.25 m tall (`SCREEN_W`, `SCREEN_H`, `PANEL_W`, `PANEL_H` in
  `battery_parts.py`).
- **Debug Charge** (2026-10-11): a slider on `CapacitorModule` (every component with a hold), play mode only. It
  follows the charge as a share of the hold, and dragging it sets the charge; on a network, set it on the master.
- **Colliders:** non-convex mesh colliders on the frame and the core part (stacks, rack, column, ring; the
  [[slots#S0|S0]]'s body), on only while locked in its slot.
- **Arcs** (`BatteryArcs`, 2026-10-11): rolled every 0.1 s of the shared clock (`WorldManager.SharedTime`, Fusion's
  simulation time) from a hash of the battery's place on the ship and the step, so every client rolls the same
  numbers with nothing sent. **Untested with a second player.** An arc is a pooled `LineRenderer` (`BatteryArc`
  material) re-jagged each frame; only that flicker is local.

- **One hold.** `BatteryBehaviour` (`scripts/Power/BatteryBehaviour.cs`) is a sink and a source over one
  `Capacitor`, owned by the prefab's `CapacitorModule`. It needs an input conduit (In) and an output conduit (Out).
- **It only lets go when there is somewhere to go:** the plain cell releases while its node has an outlet
  (`Release` → `node.HasOutlet`). It cannot see past its own conduit: a switch opened further down the run still
  gets cooked.
- **A cell mid-run is a UPS** ([[power#Batteries|power → Batteries]]): with 200 W arriving it banks 100 W and passes nothing on;
  what arrives beyond its charge rate is heat on the cell.
- **Isolated, it holds what it has** (`Enabled` false). Nothing on the prefab isolates it by hand yet.
- **Shared state:** its charge is networked state (`CapacitorModule` is `INetworkedState`, one float). Flow is not
  sent: every client runs the same sim and works it out ([[networking#Rules|networking → Rules]]).
- **Data:** none yet. `BatteryData` (charge, watts) is planned in [[data]], not built.
- **No controls.**
- **On the grid:** each fits its slot, and its footprint is the slot's: a slotted component's `GridNode.Size` comes
  from its slot size, not the node's own size field. Carried like any slotted component (`CarriedComponent`; [[polish]]).

> [!warning] Deprecated (Vulps, 2026-10-10)
> The three cell tiers (plain, safety, smart: `SafetyBatteryBehaviour`, `SmartBatteryBehaviour`, [[power#Three cell tiers|power → Three cell tiers]])
> are likely deprecated. Ignore them until their use is clearer; no battery design depends on them.

## The ladder

**Each rung is a huge leap over the one below** (Vulps, 2026-10-10). The comparisons Vulps used to show it (a car
battery, an EV, a stargate, a black hole) show **the size of the leap, not designs or values**. Each size is its own
machine ([[component-models#Rules (decided with Vulps, 2026-10-09 and 10)|component-models → Rules]]), and the numbers are set together in [[power#Open the scale of every number (logged 2026-10-08)|power → the scale of every
number]], not taken from real-world batteries.

### Capacities (Vulps and Claude, 2026-10-10)

Set from scratch, not from today's numbers in the game: **each leap is bigger than the one below it**, so the small
sizes sit close together and the top pulls away. Anchored on the [[slots#S4|S4]]: jump drives and engines will need
far more energy than anything built so far, and the rarest part in the game should be able to feed one.

| Size | Capacity | Leap from the rung below |
|---|---|---|
| [[slots#S0\|S0]] | **200 kJ** | |
| [[slots#S1\|S1]] | **1 MJ** | ×5 |
| [[slots#S2\|S2]] | **6 MJ** | ×6 |
| [[slots#S3\|S3]] | **50 MJ** | ×8 |
| [[slots#S5\|S5]] | **500 MJ** (550 MJ overcharged: [[#S5 The Ring\|S5]]) | ×10 |
| [[slots#S4\|S4]] | **10 GJ** | ×20: relic technology ([[slots#S4\|slots → S4]]) |

> [!question] Open
> - **Charge and discharge rates** per size: how fast each can deliver, next.
> - **Against the jump drive:** once a jump has an energy cost, check how many jumps an [[slots#S5|S5]] and an
>   [[slots#S4|S4]] are worth.
> - The rest of the game (reactor output, every draw) is rescaled to the same scale later
>   ([[power#Open the scale of every number (logged 2026-10-08)|power → the scale of every number]]).

**For scale, the jobs at the bottom:** the [[slots#S5|S5]] reactor's magnets draw 20 W of house load, so a cold start feeds about
20 W for as long as the rods take to come up ([[reactor]]). That is the smallest job on the ladder.

### S0

**Status (2026-10-11):** built as a prefab, untested in-world. Its slot is `Prefabs/Slots/Slot S0`.

- **A new slot size: one cell** (1 × 1 × 1, 0.5 m). Small loads: starting the reactor, a light or two.
- **By convention, not rule: in or on a wall,** most likely in an alcove, not behind a panel.
- **The exception to two grid rules** ([[slots#S0|slots → S0]]): its anchor is a cell centre, not a grid corner, and it is not
  rectangular like the other even sizes. **In code (compiled, 2026-10-11):** `SlotSize.S0`, one cell in
  `ComponentSlot.CellsOf`, and `ComponentSlot.NearestAnchor` puts an [[slots#S0|S0]] slot on the nearest cell centre while editing.
  Ports, footprint and volume needed no change: their maths centres odd sizes on a cell already.
- **Its slot is `Slot S0`,** a variant like the other sizes, its grab handle scaled to 0.6.
- **No room for the three-cell I/O panel:** its ports go on its own faces.

**Behaviour (Vulps, 2026-10-10): nice, simple, reliable, risk free because of its size.** It matters for restarting
a ship, so nothing about it can go wrong:

- **A simple battery and nothing more. No readouts at all:** no screen, no glow, no gauge. The one exception to
  the model being the readout.
- **A square battery** (Vulps, 2026-10-10): a battery body with a **+** and a **−**, like a real one.
- **Ports: In on the right, Out on the left,** seen from the front (In on its -X face, Out on +X, front on +Z), both
  plainly visible. Charge runs + to −: it leaves by the **+** (Out) and comes back into the **−** (In).
- **The other four faces** (front, back, top, bottom) are marked **+** on the Out side and **−** on the In side, so
  its polarity reads from any angle.
- **It fills on charge, and with nothing to output to it does nothing.**
- **Full, it stops accepting**, so power stops travelling down that branch, as if the branch were switched open
  (the flow only goes toward receivers that can take it, [[power#The flow model|power → The flow model]]). It is
  never a dump for anything upstream.
- **No non-combat damage, no heat, no waste heat.** While it has room, whatever comes down the conduit to its input
  it accepts, and anything beyond what it banks is destroyed, not turned into heat.
- Combat damage still applies.

### S1: The Stack

**Status (2026-10-11):** built as a prefab, untested in-world.

The existing battery, scaled down to its rung.

> [!info] Proposed (Claude, 2026-10-10, for Vulps to react to)
> **"The Stack":** four stacks of five short cells in a light braced frame, lighting one cell at a time as it
> charges (stacks and fill decided by Vulps, 2026-10-10).
> Numbers follow the S5 reactor's script (`Blender/Reactors/Reactor.py`) so the two read as one family.

**4 × 4 × 4 cells** (2 m cube), front on +Z, origin at the centre, so everything runs from -1 to +1 m.

```
FRONT (from the room)                  TOP (back at the top, front at the bottom)

 ┌──────────────────────────┐ beams     ┌──────────────────────────┐
 │█▓▓▓▓▓▓ top plate ▓▓▓▓▓▓▓█│           │█ [    |Out |Data| In ] █│ I/O panel, back
 │█   [   ] [   ]          █│           │                          │
 │█   [   ] [   ] 5 cells  █│           │      BL ⬡   ⬡ BR         │
 │█   [   ] [ ░ ] a stack  █│           │      FL ⬡   ⬡ FR         │
 │█   [ ▓ ] [ ▓ ]          █│           │                          │
 │█   [ ▓ ] [ ▓ ]          █│           │█▭ screen                █│
 │█▭ screen                █│           └──────────────────────────┘
 ╞══════════════════════════╡ plinth                 front (+Z)
```

- **Plinth:** the whole 4 × 4 footprint, **half a cell tall** (0.25 m, the reactor's), gunmetal, chamfered. The frame
  stands on it, flush with the slot's edge.
- **Frame:** four corner posts, **0.15 m square** (the loaders' legs; the reactor's 0.22 is too heavy at 2 m), 0.2 m
  in from the edges, joined by a ring of top beams. **Braces on the two sides and the back** by the brace rule
  ([[tooling#Models (Blender, by script)|tooling → Models]]), feet sunk into the plinth, heads into the top beams.
  **The front stays open** so the cells are seen.
- **Top plate:** a **solid plate filling the frame's top ring, flush with its top edge**, so the top of the battery
  is one surface. The stacks run up into its underside, so it reads as the lid the cells are clamped under (Vulps,
  2026-10-10: the first draft's floating bus plate looked random; flush, rather than a cross over a plate sunk below
  the beams).
- **Core: four stacks of five short hex cells** in a 2 × 2 cluster at the middle (**20 cells**), running **from the
  plinth up into the top plate**. Each cell is a chunkier cousin of the fuel rod (0.24 m): about **0.4 m across** and
  about **0.25 m tall**, in a **glass sleeve**, with a thin **near-black cap** between cells and at each stack's top
  and bottom.
- **The model is the readout: each cell is 1/20th of the battery's capacity** (5%), and fills on its own:
  - **Colour by its own fill:** **black** when empty, **violet** from 1%, brightening to **white** when full.
  - **Fill order:** the bottom cell of the **front-right** stack first, then the bottom cell of the next stack
    **clockwise** (seen from above: front-right → front-left → back-left → back-right), then back to the front-right
    stack for its second cell, and so on up: one level round all four stacks before the next level starts.
  - **Draining runs the same order backwards**, so the last cell lit is the first to go dark.
  - So at a glance: the count of white cells is the charge in twentieths, and the one violet cell is the twentieth in
    progress. One hold underneath (`CapacitorModule`); the cells only show it, so nothing new is networked.
- **Wiring, so the ports go somewhere** (Vulps, 2026-10-10): right-angled runs, like conduits on the grid.
  - **In** runs up the back from the I/O panel into the **top plate**.
  - Power goes **down through the stacks**.
  - **Out** leaves from the **plinth**, straight down out of the I/O panel.
  - So the power path reads by eye: in at the top, through the cells, out at the bottom.
  - **Data** runs from the middle port along the plinth, round the stacks on the viewer's left, into the back of the
    screen: it is where the screen's numbers come from.
- **No arcing.** Arcs are the [[slots#S4|S4]]'s signature; on the [[slots#S1|S1]] they would read as dangerous and
  powerful, the opposite of what it is for.
- **Control panel:** a dark panel **in front of the stacks**, low on the front at the viewer's left, amber trim,
  holding **one screen** (charge, watts in and out, integrity). At the shared screen size it cannot fit beside the
  stacks, so it stands in front of the front-left stack and hides most of it from the front (Vulps, 2026-10-11). No controls: the
  battery has none today.
- **I/O panel:** on the back at the base, **three cells wide, one tall**, from the model's base out to the slot's
  edge, at the reactor's offsets (x = -0.75, -0.25, +0.25; the fourth cell, +0.75, plain). **In on the right, Data
  in the middle, Out on the left** seen from the front, as on the [[slots#S0|S0]].

### S2: The Rack

**Status (2026-10-11):** built as a prefab, untested in-world.

> [!info] Proposed (Claude, 2026-10-10; refined with Vulps from a rough draft)
> **"The Rack":** cells lying front to back like shells in a rack, so the room sees a wall of glowing hexagon ends.

**6 × 4 × 4 cells, long on x** (3 × 2 × 2 m), front (+Z) on a long side.

```
FRONT (from the room)                          BACK (behind the back plate)

 ┌────────────────────────────────────┐ X     ┌────────────────────────────────────┐
 │█ ⬡  ⬡  ⬡  ⬡  ⬡                  █│ brace │█  ┳━━━━━━━━━━━┳━━━━━━━━━━━┳       █│ In, three T-junctions
 │█ ⬡  ⬡  ⬡  ⬡  ⬡   cells lie      █│ on top│█   ┃                              █│
 │█ ⬡  ⬡  ⬡  ⬡  ⬡   front to back  █│       │█   ┃          back plate          █│
 │█ ⬡  ⬡  ⬡  ⬡  ⬡   ends facing you█│       │█   ┃                              █│
 │█                          ▭ screen█│       │█ [ In |Data| Out|   ] ┗ Out to plinth│
 ╞════════════════════════════════════╡       ╞════════════════════════════════════╡
```

- **Plinth and frame:** the plinth fills the footprint, half a cell tall. Four corner posts and a ring of top beams,
  braced on the sides and back, the front open, and an **X brace across the top** of the frame.
- **Cells: 4 rows of 5 (20)**, lying **front to back**, filling the height from the plinth to the top beams. The
  same family as the [[slots#S1|S1]]'s: about **0.4 m across**, about **1.1 m long**, a dark sleeve along the body
  and a **glass end cap facing the room**, which carries the colour.
  - **The glass cap** (Vulps, 2026-10-11): glass walls pushed on over the cell's front end like a cap, over the
    glowing end and the front of the sleeve, sealed to the sleeve by a **thin metal collar**.
- **The readout:** the [[slots#S1|S1]]'s colour language, so batteries read alike across the ship. **Each cell is
  1/20th** of the capacity: **black** when empty, **violet** from 1% of its own fill, brightening to **white** when
  full. **Fill order:** the bottom row first, from the viewer's right to left, then the next row up. Draining runs
  the same order backwards.
- **Back plate:** a solid plate as wide and tall as the rack, which the cells' back ends meet. It **sits in from the
  frame's back**, leaving room for the wiring to run behind it.
- **Wiring** (right-angled runs, as on the [[slots#S1|S1]]):
  - **In** runs up from the I/O panel and across the back of the back plate, **T-junctioning straight into the
    plate at three points** along the way, seamlessly: no connector blocks.
  - **Out** leaves from the **plinth**.
  - **Data** runs from the middle port round the rack into the back of the screen.
- **Control panel:** low on the front at the viewer's right end, **in front of the rack's right end** and clear of
  the glass caps, hiding the right end of the lower rows (Vulps, 2026-10-11: too big to fit beside it), amber trim, one screen (charge, watts in and out,
  integrity).
- **I/O panel:** on the back at the base, at the reactor's offsets. In on the right, Data in the middle, Out on the
  left, seen from the front.
- **How it differs from the [[slots#S1|S1]]:** the [[slots#S1|S1]] is four upright towers seen from the side,
  charging a level at a time round the stacks; the [[slots#S2|S2]] is a wall of hexagon ends seen head-on, charging
  along the rows like a grid of lights.

### S3: The Lift

**Status (2026-10-11):** built as a prefab, untested in-world.

> [!info] Proposed (Vulps and Claude, 2026-10-10)
> **"The Lift":** a dense sphere lifted up a glass column by a magnetic field, a faint plasma beam trailing from it to
> the base. Leaning sci-fi: a levitated-mass battery crossed with a plasma column, like the plasma balls of the 90s,
> but vertical.

**8 × 8 × 8 cells** (a 4 m cube).

```
FRONT (from the room)

 ┌──────────────────────────────┐ top housing
 │█         ╔════╗             █│
 │█   ═══   ║ ●  ║   ═══       █│  sphere: its height is the charge
 │█         ║ ┊  ║  field rings █│  plasma beam, sphere to base
 │█   ═══   ║ ┊  ║   ═══       █│
 │█         ║ ┊  ║             █│
 │█   ═══   ║ ┊  ║   ═══       █│
 │█         ╚════╝   ▭ screen  █│
 │█        base housing         █│
 ╞══════════════════════════════╡ plinth
```

- **How it stores energy:** charging lifts a **dense near-black sphere** up a tall **glass column**, held in a
  magnetic field; discharging lets it sink, its fall turned back into power.
- **The readout is the sphere's height:** resting on the base when empty, at the top when full. It reads from across
  the room with no lights at all, and it moves as power flows in or out.
- **One plasma beam:** a **faint plasma streak from the sphere down to the base** of the column, growing as the
  sphere rises. It follows the family's colour language: a faint **violet** low down, whiter as it nears full.
- **When full: small arcs inside the column** pop off now and then. **Not discharges**, just intense stored energy,
  and **always inside the glass**. They stay distinct from the [[slots#S4|S4]]'s arcs, which are excess thrown off
  between its cells and onto its frame.
- **Rendering cost in VR:** the beam is **one** emissive ribbon mesh with a scrolling noise shader, with no particles
  and no lights. An arc is one short-lived line that flickers on for a few frames and is gone.
- **On the prefab** (2026-10-11): `BatteryLift` raises the `Sphere` 1.26 m from empty to full and stretches the
  `Beam` up to its underside, tinting it violet to white. The beam is `SpaceScape/PlasmaBeam` (additive, noise rising
  in world space so stretching does not stretch it). Arcs: from the sphere's surface to the glass wall, from 99%
  charge, 10 a minute (a presumption), no sound.
- **Plasma: a plasma ball laid along the column** (Vulps, 2026-10-11; `BatteryFilaments`). Thin filaments reach from
  the beam on the centre line to the inside of the glass, leaning upward, and where each touches the glass it walks
  up the tube to the sphere's underside over 1.5 to 3.5 s, drifting about a third of a turn round it, then starts
  again at the bottom, never in step. One filament at the first charge, up to five at full, none empty; violet
  turning white, thin at the beam and wider on the glass, re-jagged every frame so they flicker. Only the look, from
  the shared charge: nothing is sent.
- **The hum, from 50%** (Vulps, 2026-10-11; `BatteryHum`): `Machine_Electric_StaticLoop_01` (Electrified SFX Library,
  [[assets]]), a 36.8 s loop pulsing about twice a second. Silent below 50%; from there it fades in to full volume at
  100% as it speeds up from 0.6× to 1×, pitch and pulse together (about one pulse a second, deeper, to about two):
  the column charging up. Imported mono, Vorbis, 48 kHz.
- **Field rings:** gunmetal rings stacked up the outside of the column, holding the field.
- **Top and base housings:** dark housings capping the column; the base holds the field generator, where the beam
  is anchored.
- **Frame and plinth:** as every size, the plinth filling the footprint, half a cell tall, the frame braced on the
  sides and back, the front open.
- **Wiring**, as on the [[slots#S1|S1]] and [[slots#S2|S2]]: **In** up the back into the top housing, **Out** from
  the plinth, **Data** round to the back of the screen.
- **Control panel:** a panel on the front beside the column, amber trim, one screen.
- **I/O panel:** on the back at the base, three cells wide, In on the right, Data in the middle, Out on the left,
  seen from the front.

### S4: Relic cells in our cradle

**Status (2026-10-10):** talked through with Vulps; parked until after the ship is built.

> [!warning]
> Vulps note: As cool as it would be to add this right now, we should sit on this until after the ship has been created... there's a number of new mechanics in here that are not on the MVP path

[[slots#S4|S4]] components are the rarest and most powerful in the game (one [[slots#S4|S4]] slot on the ship); one can drop in the
first system or the billionth. The [[slots#S4|S4]] battery is far beyond its size.

**The frame (ours):** an [[slots#S4|S4]]-sized braced frame on its plinth, in the house look. **Three vertical supports hang
down from the top**, about one or two cells. Each ends in **horizontal X-shaped arms with vertical spindles**, so it
plainly looks made to hold something. What it holds are the cells.

**The cells (not ours):**

```
-------
|     |   metal cap
-------
|     |
|     |   glass
|     |
|     |
-------
|     |   metal cap
-------
```

- A rectangular cell with **metallic caps** top and bottom and a **glass** middle.
- **Floating in the glass, a ball of energy** with **three silver rings** round it, each turning in a different
  direction (a Bohr-model atom).
- **The ball's size is how much the cell holds;** it grows as it charges.
- **The rings' speed is the power coming in or going out:** faster as more flows.
- Silver is not in the house materials: it is kept for relics, so the cells read as foreign at a glance.

**Behaviour:**

- **The cells decide capacity.** Each cell holds an absurd amount (number to set); the battery holds what its fitted
  cells hold.
- **Cells are items, with a pack of their own** (like the fuel and addon packs, [[loading]]).
- **Not consumable, so no item port.** A cell is placed into its cradle by hand, and stays.
- **A cell burns out only if it takes damage.**
- **No waste heat.** It is advanced enough that excess is released as **arcs between the cells** (and also towards the frame) instead of turning into heat on the tile.

> [!question] Open
> - **How many cells:** one per arm tip (four per support, 12 in all) or one per support (3).
> - **Charge across cells:** one hold shown the same in every cell, or cells filling one after another.
> - **Does a cell carry its charge?** Pull a full cell and carry it to another [[slots#S4|S4]] battery, or does the charge stay
>   with the battery?
> - **Idle and empty look:** at no charge and no flow the ball is gone and the rings stop. A faint ember and a slow
>   drift (dormant, not dead), or dead on purpose?
> - **Direction:** whether the rings reverse between charging and discharging (needs the sim's flow direction).
> - **Arcs:** where the arc energy goes (simply lost?), and whether an arc can hurt someone standing in it.
> - **The other [[slots#S4|S4]] designs:** the components are the dead civilisation's ([[slots#S4|slots → S4]]), so the
>   Bottle reactor and the inertia drive ([[felix]]), designed as ours, become relics too.
> - **Networking**, before anything is built ([[networking#Rules|networking → Rules]]): cells as networked items,
>   which cradle holds which cell, the pack, and damage burning a cell out.

### S5: The Ring

**Status (2026-10-11):** built as a prefab, untested in-world.

> [!info] Proposed (Vulps and Claude, 2026-10-10)
> **"The Ring":** a particle storage ring standing upright in its grounded frame, bunches of light racing round it,
> merging into a solid band when full, and sharp, loud arcs onto the frame as it nears full.

**10 × 10 × 10 cells** (a 5 m cube). The endgame size: what sells it is the scale, the containment, and energy you
can feel being held back.

```
FRONT (from the room)

 ┌────────────────────────────────────┐ frame: the house style, grounded
 │█          ▄▄▄▀▀▀▀▀▄▄▄             █│
 │█       ▄▀ ·  magnet  · ▀▄          █│
 │█      █  ·            ·  █         █│  bunches of light race round inside the ring;
 │█      █ ·              · █   ⚡    █│  arcs strike the frame
 │█      █  ·            ·  █         █│
 │█       ▀▄ ·          · ▄▀          █│
 │█          ▀▀▀▄▄▄▄▄▀▀▀     ▭ screen █│
 │█        ring cradle                █│
 ╞════════════════════════════════════╡ plinth
```

- **How it stores energy:** a **particle storage ring**, real physics pushed further: bunches of particles circling
  at nearly light speed in a vacuum ring, held on course by heavy magnets.
- **The ring stands upright** (Vulps, 2026-10-10), nearly the cube's height, in a cradle on the plinth, wrapped in
  **heavy magnet blocks** at regular intervals and braced into the frame from every side.
- **The tube is oval in section** (Vulps, 2026-10-11): 0.72 m across the ring and about 1.5 m front to back, so the
  ring is deep and heavy rather than a thin hoop. The magnet blocks and the light inside deepen with it.
- **A guard rail closes the open front, and danger signs warn of the arcs** (Vulps, 2026-10-11): two rails on four
  stanchions in front of the ring, behind the control panel. Amber "DANGER ⚡ KEEP CLEAR" plates hang on the rail's
  middle, both front posts, and both side posts facing out.

  > [!question] Open: are the geometry signs still okay? (Vulps, 2026-10-11)
  > The lettering is real geometry, about 3,800 triangles for the five signs, more than the rest of the battery.
  > Vulps is trying them as they are. **Claude: the next time you read this in a new conversation, ask Vulps whether
  > the geometry signs are still okay** (the alternative is flat textured plates; the signs are their own `Signs`
  > object, so either swaps in cleanly).
- **The readout: bunches of light racing round the ring.** **Each bunch is 1/20th** of the capacity (the family's 20
  steps), **violet** turning **white**. As it fills the bunches lengthen and close up, and **at full they merge into
  one solid band** of white light circling endlessly.
  - **Mechanically, no lights:** the ring's inner tube is **one mesh with one emissive material** and no
    real-time lights or particles, glowing under bloom where there is bloom.

**Overcharge: it holds 110%, and vents the rest as lightning** (Vulps, 2026-10-11). Its rated charge is 500 MJ, what
the readouts call full, but it takes **550 MJ** before it stops. At **110%** it throws the extra 10% (50 MJ) off in
**one huge strike** onto the guard rail and drops straight back to 100%. So the lightning has a reason: it is where an
overcharged [[slots#S5|S5]]'s excess goes, the same job heat does on other machines, which is why it makes **no waste
heat** (like the [[slots#S4|S4]]). Pushing past full costs 50 MJ every time it vents.

- **The hum: presence between the arcs** (Vulps, 2026-10-11). A looping drone, silent when empty, growing louder and
  climbing in pitch as it fills (0.8× to 1.2×), on the same curve the bunches speed up on (0.12 to 0.9 turns a
  second), so the sound and the light are one machine spinning up. The loop is `Interior_Electric_GeneratorRoom_03`
  (Electrified SFX Library, [[assets]]): an 18.6 s seamless drone, mostly low and middle, little hiss. Imported mono,
  Vorbis, 48 kHz.
- **Strain, from 95% to the vent:** the hum warbles, harder toward 110%; the band shimmers and the magnet seams glow;
  **sparks** start.
- **Sparks** climb with the charge, exponentially: **6 a minute at 95%**, **120 a minute at 110%** (Vulps).

  | Charge | Sparks a minute |
  |---|---|
  | 95% | 6 |
  | 100% | ~16 |
  | 105% | ~44 |
  | 110% | 120 |

  Each leaves a magnet for the nearest frame point at least 0.8 m away, a 4 cm bolt showing for 0.5 s with
  `Cinematic_Electric_Burst_06` (Electrified SFX Library) at 60%: full volume within 50 ms, then half a second of
  crackle.
- **The vent strike** leaps from the front face of a side or lower magnet onto the **guard rail**, right in front of
  whoever is standing there: a 14 cm bolt lasting 1.2 s, very jagged, with six forks onto other rail points, like
  lightning. Its sound is `MenuHud_Robotic_Transition_02` (the same instant hit as the crack, heavier in the low end).
- **Harmless** (Vulps, 2026-10-11): sparks and the strike go to the grounded frame (the rail is part of it), which is
  always nearer the ring than any player, so the current runs round the frame to ground and never jumps to anyone (the
  frame doing a Faraday cage's job). The only cost is the 50 MJ a vent throws away.
- **The frame is the house style** (Vulps, 2026-10-10): posts, beams and braces as on every size, no mesh panels.

> [!info] Networking
> - **Charge** is already the battery's shared state; the hum, the band, the strain and the sparks are worked out on
>   every client from it, and nothing is sent for them.
> - **Sparks:** rolled every 0.1 s of the shared clock from a hash of the battery's place on the ship and the step,
>   so every client rolls the same numbers and a room full of players jumps together ([[#Today the battery prefabs|Today → Arcs]]).
> - **The vent** (Vulps, 2026-10-11): only the master vents (`OverchargeVentModule`, told it decides by
>   `ComponentNetwork`). It counts its vents into the battery's state (one value; the S5's bridge holds it exactly).
>   When the count goes up, every client shows the strike once, drawn from the vent's number so it is the same strike
>   everywhere; a late joiner takes the count as it finds it and replays nothing. **Untested with a second player.**

- **On the prefab** (2026-10-11): `OverchargeVentModule` (capacity 550 MJ), `BatteryRing` (band, seams, spin-up),
  `BatteryHum`, `BatteryArcs` (sparks: Charge From 0.95, 6 to 120 a minute at Top At 1.1; the vent strike). The band
  is `SpaceScape/BatteryRing`: its angle round the ring comes from object space (no UVs), and `BatteryRing` carries
  the bunches' travel each frame, so a change of speed never makes them jump. Sparks leave 24 magnet points (front
  and back of each block's outer face) for 93 frame points (posts and top beams); strikes leave 7 magnet fronts for
  30 rail points.

**The rest, as every size:** the plinth filling the footprint, half a cell tall; **In** up the back into the ring's
housing, **Out** from the plinth, **Data** round to the back of the screen; a control panel on the front
beside the ring, amber trim, one screen; the I/O panel on the back at the base, In on the right, Data in the middle,
Out on the left, seen from the front.

## Shared by every size

- **Models** (2026-10-11): `Blender/Batteries/BatteryS0.py` to `BatteryS5.py`, sharing `battery_parts.py`, export
  `Meshes/BatteryS0.fbx` to `BatteryS5.fbx`, each the `Model` on its prefab. Every part that shows charge carries
  `BatteryCell` (the S5's band `BatteryRing`, the S3's beam `BatteryPlasma`), coloured by the readout script through
  a property block. The S3's `Sphere` has its origin at its centre and its `Beam` at its foot, so the script raises
  one and stretches the other. No S4 model yet.
- **The model is the readout, from shared state** ([[component-models#Rules (decided with Vulps, 2026-10-09 and 10)|component-models → Rules]]): charge is already networked;
  flow is worked out on every client. Nothing new is sent for the looks.
- **I/O panel** (In, Data, Out) on the back at the base, from [[slots#S1|S1]] up; [[slots#S0|S0]] has no room for one.
- **Numbers** (capacity, charge and discharge rates per size) are set in [[power#Open the scale of every number (logged 2026-10-08)|power → the scale of every number]].
