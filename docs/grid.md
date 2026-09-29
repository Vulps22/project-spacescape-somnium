# The grid, slots and components

**Status: rules agreed 2026-09-28, nothing built yet.** Where things sit on the grid and how big they are.
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

**Decided 2026-09-28.**

| Size | Shape | Cells (x × y × z) | Metres |
|---|---|---|---|
| S1 | cube | 4 × 4 × 4 | 2 × 2 × 2 |
| S2 | rectangular | 6 × 4 × 4 | 3 × 2 × 2 |
| S3 | cube | 8 × 8 × 8 | 4 × 4 × 4 |
| S4 | rectangular | 10 × 8 × 8 | 5 × 4 × 4 |
| S5 | cube | 10 × 10 × 10 | 5 × 5 × 5 |

- **Odd-numbered sizes are cubes; even-numbered ones are rectangular**, with y and z two cells shorter
  than x. Two, not one: every dimension stays even, so a slot's centre is always a grid corner and a slot
  lands on whole cells whichever way it faces.
- **There is exactly one S4 slot on the ship.** In honour of a numbering slip, S4 components are the
  rarest and most powerful in the game, whatever they are: a gun that is overpowered, a reactor that
  practically never runs dry, a radar that maps the galaxy in one pass. Examples, not decisions; the loot
  side belongs in `the-loop.md`. That S4 is smaller than S5 makes no sense, and that is fine.
- **Only an Sx component fits an Sx slot.**
- **A slot's anchor is its centre** (a grid corner), and a component attaches there.
- **A slot has one front,** picked like a conduit's face, and its own up. A slotted component always
  faces the slot's front with the slot's up as its up, so a component never turns within a slot.
- **A slot's volume is reserved:** no conduits are generated inside it, empty or not. Cables run to its
  edge, where a component's ports meet them; to get past a slot, route round it.
- **Slots are seen only as gizmos** while editing. Where they go on the ship is set by hand, by
  convention.
- **The finished ship has more slots than it needs,** many small and very few large, so a crew chops
  and changes to suit what it is doing.
- **Later:** a dropped component snaps into a slot only when it is lined up closely enough.

- **Idea, not confirmed: S0, 1 × 1 × 1 cells,** for small utilities like lights, if it proves doable. It
  may turn out to be the addon system under another name; decide when it comes up.

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

**Decided 2026-09-28.**

- **A hand picks up the component itself,** full size. One player can pick up anything for now; weight
  and carrying together are in `todolist.md`.
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
   (position only, no throw) kept on a 10 cm track out of the front by `SlideGrabTransformer`, with
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
   networked. **Still to do in this phase:**
   **Components as spawnable network prefabs,** held by slots (the occupant in the slot's data); grabbed
   and carried with `NetworkGrabbable`; locked in when they pass the alignment, position and size checks,
   which is also what the handle's relock checks.
5. **Pulled out live, 10% of maximum integrity lost.**

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
addons are now (`networking.md`), and each carries its own state (`ComponentNetwork`), so a
half-charged battery stays half-charged whichever slot it is in. A carried component's pose follows the
hand through `NetworkGrabbable`.

## Tools

- Snapping in the editor: slots onto grid corners, quarter turns only.
- Gizmos for slot volumes, fronts, and components' ports.
- A warning when the grid is built for anything that breaks these rules.
