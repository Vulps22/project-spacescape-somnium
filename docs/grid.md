---
aliases:
  - "The grid, slots and components"
tags:
  - structure
---
# The grid, slots and components

Where things sit on the grid and how big they are.
Written because there were no fixed rules for either, and every component solved it differently: a
reactor sinking a quarter-metre into the floor, a footprint leaning one way while its model leaned the
other once turned, a model 0.15 m off its own footprint, a 5 m cylinder in a 5.5 m footprint.

Components will be moved and replaced during play, so they sit in **slots**, and the slots are where the
rules live.

## The grid

- **Cells are 0.5 m.** `GridCell` is the only place a cell becomes metres.
- **Cell centres are on multiples of 0.5 m, so boundaries are on the quarter-metres** (-0.25, 0.25,
  0.75...). Decided 2026-09-28: rather than shift the grid half a cell, the floor moved down to y = -0.25,
  a boundary. Only the floor was placed by hand at a round number; walls and conduits are placed by tools
  that snap to the grid, and components' footprints already fit it. Anything placed by hand goes on
  boundaries: floors, ceilings and wall faces at 0.25 + 0.5k. Slot centres (grid corners) land on .25 and
  .75 too; the tools snap them.
- **The grid is world-aligned.** Conduits, ports and connections are asked about in world faces and cells.

## Slots

**Decided 2026-09-28.** The sizes, S0 to S5, and what each holds are in [[slots]].

- **A slot's anchor is its centre** (a grid corner; for S0, a cell centre), and a component attaches there.
- **A slot has one front,** picked like a conduit's face, and its own up. A slotted component always
  faces the slot's front with the slot's up as its up, so a component never turns within a slot.
  By precedent (not a hard rule) a component's own front is +Z and its up +Y, models included:
  [[component-models#The look|component-models → The look]].
- **A slot's volume is reserved:** no conduits are generated inside it, empty or not. Cables run to its
  edge, where a component's ports meet them; to get past a slot, route round it.
- **Slots are seen only as gizmos** while editing. Where they go on the ship is set by hand, by
  convention.
- **Later:** a dropped component snaps into a slot only when it is lined up closely enough.

## The slot is a thin wrapper

**Decided 2026-09-28.** From the component's side the slot is the grid; from the grid's side the slot is
the component.

- **The slot is the grid tile, and it never moves,** so the grid never changes shape. When the grid asks
  "do you send power out of cell xyz, face x-?", the slot asks its component, in the component's own
  frame, and passes the answer back. Source, sink and ports all come from whatever is installed.
- **All the data is on the component:** charge, integrity, heat, ports, behaviour. The component answers
  as if it sat on the grid itself and never knows the slot is there.
- **The slot only delegates:** every call is its component's answer (`Output() => component.Output()`),
  or the do-nothing answer when it is empty.
- **An empty slot is inert.** Each tick it reports no output, no draw, no heat and no ports; anything
  done to it, like damage, is accepted and ignored, since there is nothing to respond. A conduit sending
  power at an empty slot finds no port there, so it is a dead end like any other: the power stays on the
  conduit as heat.
- **Swapping a component** hands the grid a different source and sink for the same node, then rewires
  that one tile (`PowerGrid.Rewire`). No node is ever added or removed.
- **A component in no slot is in no grid, so it is inert:** nothing charges, drains or works until it is
  slotted again. Its heat goes with it, so a hot reactor carried away stays hot; an empty slot has none.

## Moving a component

**Status (2026-09-29):** decided 2026-09-28; tested in-world with many players.

- **A hand picks up the component itself,** full size. One player can pick up anything for now; weight
  and carrying together are below (Weight and carrying together).
- **Every slot has a grab handle** on top of its front bottom edge: the one thing a slot shows in-world.
  Pulled, it turns from red to green and unlocks the component, which can then be grabbed for 10
  seconds.
- **After 10 seconds the slot tries to lock again.** If the component is still in place it locks back
  in. After that attempt the slot listens for a component touching it and checks its alignment, position
  and size; one that passes is locked in.
- **A component pulled out while live** (power flowing through it) **loses 10% of its maximum integrity.**

### Build order

1. **Slot prefabs (done 2026-09-28).** `ComponentSlot` (size, front, up; sits on a grid corner and turns
   to its front while editing; volume, front and up as gizmos) on `Prefabs/Slots/Slot.prefab`, with a
   variant per size (`Slot S1` to `Slot S5`). The grab handle (red bar on two posts,
   `SlotHandleLocked.mat`; `SlotHandleUnlocked.mat` is the green) sits on the middle of the front bottom
   edge.
2. **The handle (written 2026-09-28, compiles clean, untested).** The bar is a stock `XRGrabInteractable`
   (position only, no throw) kept on a 10 cm track straight up by `SlideGrabTransformer` (it slid out of the front until 2026-09-29), with
   `IgnoresPlayerBody`. Pulled 80% of the way, `SlotHandle` calls `ComponentSlot.Pull()`; let go, it slides
   home. The slot unlocks for 10 seconds, then relocks (for now unconditionally; build order 4 makes it
   check). `SlotNetwork` (a `NetworkBridgeData1` on the slot) carries the lock: the master decides and
   writes it, another client's pull is sent to the master. The bar is red while locked, green while
   unlocked.
3. **Slots as grid tiles (written 2026-09-28, compiles clean, sim tests pass, untested in Play).** A slot's
   `GridNode` takes its footprint from the slot's volume. `PowerGrid` builds each slot as a tile with an
   inert node (`InertSink`: wants nothing, and stops the node forwarding like a bare conduit), leaves
   out every component, and has each slot `Attach` its component: the node takes the component's source,
   sink and integrity, the component's own `GridNode` is bound to the slot's node (so everything on it,
   `ComponentNetwork` included, reads the slot's node), and its held heat goes onto the node.
   `ComponentSlot.Install` and `Remove` do the same at runtime and rewire the one tile; a removed component
   keeps its heat (`GridNode.HeldCelsius`). `SlotEdgeModule` answers port questions from the component.
   Only one tile writes a node's On (`GridNode.DrivesNode`). **A component in no slot is not a tile**, so
   the scene's reactor and batteries are inert until they are put in slots (the slot's Component field).
4. **Started (2026-09-29): the slot's occupant is networked.** Reactor and Battery are registered network
   prefabs. The master spawns each slot's starting component through Fusion, installs it, and writes its
   network id into the slot's `NetworkBridgeData2` (lock, occupant); every other client finds that object
   and installs it in the same slot, so `ComponentNetwork` is live again. With no network runner (the
   Editor without Photon) the slot makes it locally. Grid wiring checks wait until slots are filled.
   Offline Play checked: all three slots filled and connected, both bulbs lit, no warnings. Untested
   networked.
   **Carrying (written 2026-09-29, compiles clean, untested).** Reactor and Battery have it; for a new
   component, `SpaceScape → Make Slotted Components Carriable` gives every slotted component prefab the
   same. The tutorial stand's display copies have all of it switched off, as they do the rest. Each gets a
   box collider filling its size, a Rigidbody (kinematic
   from the start, so it cannot fall before it is installed), a stock `XRGrabInteractable` (dynamic
   attach, no throw, only its own box as collider), `NetworkRigidbody3D` + `NetworkGrabbable` as the addon
   items have, `IgnoresPlayerBody`, and `CarriedComponent`. A component can be grabbed only while it is
   loose or its slot is unlocked; grabbing it takes it out of the slot. Its box is switched off with the grab while
   it sits in a locked slot (2026-10-08), so it never stands between a hand and the component's own controls. Let go where a slot accepts it
   (empty, its size, centre within 0.5 m, within 30° of square) it is seated and the slot locks; anywhere
   else it falls. The client whose hand did it acts at once and tells the master through the slot's
   messages (`TakeOut`, `PutIn`), keeping its own occupant for 2 seconds while the master catches up. A
   slot emptied by a hand writes occupant -1, so the master never spawns a new starting component into it.
5. **Pulled out live, 10% of maximum integrity lost (written 2026-09-29, untested).** A hand taking a component
  out while power is flowing through it (arriving, produced or drawn) costs it 10% of its maximum
  (`ComponentSlot`, tunable). Only where the lock is decided (the master), for a hand there or on another
  client, so it is taken once; the component's data carries the new condition to everyone.

### Handling a component

**Status (2026-09-29):** written, compiles clean, untested in-world.

- **Grab box bigger than the reactor (bug, seen 2026-09-29).** Slotted, you cannot press up against the
  reactor's side, and the box gets in the way of grabbing the rod lever. Likely why: the fit leaves out
  meshes under an object with its own hand interaction, but the lever's interactable is only on
  `RodLever/Pivot/Handle`; `RodLever/Pivot/Arm` is outside it, so the box stretches out over the lever.
  Fix: see the next item; the box goes.
- **Components use their mesh colliders, not a box (Vulps, 2026-09-29).** The grab and the physics use
  colliders on the model's own meshes, so what you touch and bump into is the shape you see. Leave out
  the lever and anything else handled on its own; the grab interactable's collider list is these mesh
  colliders. Unity only lets a moving (non-kinematic) Rigidbody use convex mesh colliders, so each mesh
  collider is convex; a shape a single convex hull misrepresents is split into convex parts.
- **Grab points:** `CarriedComponent` fits the grab box to the model's own meshes at start, leaving out
  text, particles and anything with a hand interaction of its own (the reactor's lever), instead of the
  slot's whole volume.
- **Placement guide:** while a held component's centre is inside an empty slot of its size, the slot shows
  its volume as a transparent box: red (neither), orange (positioned or aligned), green (both: letting go
  snaps it in), with a buzz on the holding hand as it turns green. The checks are
  `ComponentSlot.IsPositioned` and `IsAligned` (centre within 0.5 m, within 30° of square), the same ones
  `Accepts` makes. Materials `SlotGuideRed/Orange/Green`, copies of `HoloUpgrade`. Local to the holder.
- **Solid, and pushing each other:** `IgnoresPlayerBody` is on only while a hand holds the component, and
  now lets its pairs collide again when switched off, so a component on the floor stops a player. When a
  held component hits a loose one, this client asks for the loose one's authority through its
  `NetworkGrabbable`, so the shove plays out on the holder's client (the suggestion from the list, built
  as proposed; Vulps to confirm after testing).
- **Weight:** the grab is velocity tracking, so a held component is physical: it follows the hand by a share
  of the gap each step (1 at no mass, 0.15 at 2000 kg) and hangs up to 0.25 m below the hold
  (`HeftGrabTransformer`, since XRI turns gravity off while held). Masses: Reactor 2000 kg (the figure in
  [[#Weight and carrying together|Weight and carrying together]]), Battery 400 kg. All tunable on `CarriedComponent` and each prefab's Rigidbody.

### A loose component keeps running

**Status (2026-09-29):** written, compiles clean, sim tests pass, untested in-world.

- **A loose component keeps running (decided 2026-09-29).** Taken out of its slot, a component gets a node
  of its own with no conduits (`PowerGrid.Loosen`), starting at the heat it carried out, and gives it back
  when it goes into a slot (`Unloosen`, keeping its heat; the grid gained `PowerGraph.RemoveNode`). Its
  controls stay live: a running reactor keeps its rods up, keeps producing, has nowhere to send it and
  heats up (sim: 100 W wasted, 271 C after a minute), and shuts down when its rods go in. Every client does
  this, as everyone simulates the grid; the component's own data corrects it.

### The handle bar's motion

**Status:** planned.

- **Slot handle bar motion.** The unlock is networked; the bar moving is not, so others do not see it
  pulled (visible motion is networked). Planned: the reactor dial pattern through the slot's data.

## Components

- **A component declares what it is to a slot** (`SlottedComponent`): its size, a constant from the
  table, and which of its own sides is its front and which its top. A slot turns it so its front faces
  the slot's front and its top is the slot's up, and takes only its own size. A component's footprint
  comes from its size, never from `GridNode` Size.
- **A slot's Starts With is a prefab,** drawn in place while editing and made when the world starts.
- **A component fills its size's volume.** The model is authored so its outer faces sit on the slot's
  boundaries. A component's root is never scaled.
- **Ports stay on the component** (`ComponentEdgeModule`), on the faces of its volume. A port's cell is
  counted from the face's bottom left, looking at it from outside (x right, y up; on the top and bottom
  faces, up is toward the component's front). The component's `GridNode` Size is its size along its own
  axes, and its transform is its centre. Swapping a
  component can mean re-running cables to its new ports, which is work for the crew (principle 9). A
  slot's housing panels come off when the wall panel in front of them does, to reach the ports.
- **Today's batteries (0.5 to 1.5 m) grow to S1,** 2 m, the smallest size.
- **Only real components are grid tiles.** Display copies (the tutorial stands) carry no `GridNode`.

## Networking

**The slot owns which component it holds.** Slots are scene objects, so the occupant (a component's
network id) goes in the slot's own `NetworkBridgeData`, beside its lock; the master decides it.
Components become registered network prefabs that the first master spawns into their slots, the way
addons are now ([[networking]]), and each carries its own state (`ComponentNetwork`), so a
half-charged battery stays half-charged whichever slot it is in. A carried component's pose follows the
hand through `NetworkGrabbable`.

| State | Tier | Decided by | Travels as |
|---|---|---|---|
| A component's pose while carried | simulation | whoever holds it | `NetworkRigidbody3D` + `NetworkGrabbable` |
| Which component a slot holds | fact | master, on the hand's request | the slot's `NetworkBridgeData` (occupant id, -1 once emptied) |

## Tools

- Snapping in the editor: slots onto grid corners, quarter turns only.
- Gizmos for slot volumes, fronts, and components' ports.
- A warning when the grid is built for anything that breaks these rules.

## Weight and carrying together

**Status (2026-09-28):** planned.

Components have a configurable weight, and every player can lift up to 500 kg. Each hand on a component
takes its share, so the weight each carries drops until it can be lifted: a 2,000 kg reactor needs four
players. Logged 2026-09-28; for now one player can pick up anything ([[#Moving a component|Moving a component]]).
