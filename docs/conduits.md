# Conduits

**Written 2026-10-02.** What a conduit is once the ship is built out: where conduits come from, what a
parked one is, and what each one costs. Status per section; the cost work is on branch
`parked-conduits-as-data` (named before the plan changed: parked conduits are **not** data, see below).

## Conduits are everywhere a crew might need one

Components can be moved and replaced during play, and a new component's ports can be anywhere on its faces
(`grid.md`: swapping a component can mean re-running cables). Cables run for one component cannot follow it.
So **every cell a crew might route through holds a conduit, parked until someone wires it**:

| Where | How | Status |
|---|---|---|
| Inside walls | a `ConduitLayoutDefinition` on each conduit container fills its block | built |
| Hand-drawn runs | `ConduitLayout` on Grid (the drawing tool): the ship's starting wiring | built; to change |
| Around every slot | one cell around the slot's faces, so any component's ports have a conduit beside them | planned |
| Along every floor | **the layer of cells immediately above the floor, all parked** (centred at y 0, on a floor whose top is y -0.25): one definition per room or corridor, to route across it | rule decided; definitions not placed |

**Definitions route round slots.** A cell inside a component slot's volume is left out of every definition, in
the Editor's gizmos and when the world builds its conduits: a slot's volume is reserved (`grid.md`). So a
floor's layer runs round the slots standing on it, and a slot's own definitions (one on each face, on the slot
prefab) supply the conduits beside its ports. Where a floor's cells and a slot's face cells overlap, the cell
gets one conduit.

**Rings (built 2026-10-03).** Two runs come pre-wired along the walls, like a building's ring main: at the
layout's **Ring Heights** (1 m and 2 m), every cell of a definition that **Carries Rings** (wall containers and
corner pieces) is traced from one open end to the other: down a corridor, round the room, back up the other
side. The first height runs one way, the next the other, so power can reach any point from either direction.
A run never closes into a loop: a closed one-way loop is a ring that blows (`power.md`), so a height whose
cells close or branch is left parked, with a warning. Everything else stays parked.

**The layout definitions are the ship's grid.** `ConduitLayout`'s drawing tool will become the way to wire the
definitions' cells (the starting wiring painted onto conduits that are already there), not a way to put
conduits anywhere. Eventually no drawn run exists outside the cells the definitions hold.

Whether a slot's shell is exposed or behind housing panels is not decided.

**Grown instead of pre-placed (idea, 2026-10-08).** Item conduits (`loading.md` → Item conduits) are not
placed in every cell: a run grows from a port, and only its open tip has a parked node. If that works well, it
could one day replace pre-placing parked conduits everywhere.

## Parked and wired

**A parked conduit is still there:** an object, a grid node, a network slot, like any other conduit. It just
has no segment on any face, so it **draws nothing and does nothing**. It waits for a hand.

- **A hand in its cell opens its hologram.** `ConduitHolograms` finds the conduit from the cell the hand is
  in (a dictionary lookup), so conduits never check for hands themselves.
- **Wiring it** (a segment onto a face, or an addon in) makes it a cable: its stubs show, and it carries
  power and heat like any other.
- **Parking every segment again** makes it invisible and idle again. It stays where it is.
- **A shut access panel** in front of it stops its hologram opening (`AccessPanel.Blocks`).

## What a conduit costs (in progress)

The aim: **a parked conduit costs nothing**, and a wired one costs its drawing and its grid node, doing work
only when something actually changed.

| | Parked | Wired |
|---|---|---|
| Drawn | nothing: no body, no stubs | its body and one stub per face used, in shared materials |
| Per-frame scripts | none | none |
| Shape (stubs) | - | rebuilt when its faces or addon change (`NodeEdgeModule` and `AddonModule` events) |
| Colour | - | set when the grid ticks, and only when its colour band changes; one shared material per band, so URP still batches them |
| Data | - | on a data connection between two or more components (`DataGrid.Links`, 2026-10-07, untested): carrying no power, the whole conduit glows Data blue (a colour band like the others); carrying power, it keeps its colour and gets four thin blue lines along each segment. Nothing sent: every client works out the same data network |
| Data pulse (debugging) | - | on a network that reaches a component's data port but joins no second component (`DataGrid.Reaches`): pulses from its own colour to Data blue over 2.5 s, back over 2.5 s, then rests 3 s (all three on the prefab; off with Pulse Where Data Reaches). One pulsing twin per colour band, animated once a frame, so every pulse is in step |
| Name | set once, when built | set once |
| Grid | a node with no edges | a node, joined to its neighbours |
| Network | one slot in the conduit chunks | same |

### When a conduit updates

**Event driven: nothing on a conduit runs every frame.** A conduit does work only when told something
changed, and then only the part that changed:

| What changed | Who says so | What the conduit does |
|---|---|---|
| Its faces (wired, rewired, parked) | `NodeEdgeModule` raises `Changed` from `SetFace`, only when a face's value actually changes (to be added) | rebuilds its stubs; shows or hides its body |
| Its addon (in, out, moved, twisted, parked) | `AddonModule.Changed` (exists) | rebuilds its stubs |
| What it carries | the grid raises one event per tick, after ticking | works out its colour band; swaps material only if the band differs from the one it has |

Each handler compares with what it last applied and writes nothing when they match, so a repeated event costs a
comparison. A parked conduit hears the grid's tick and returns at once: it has nothing to colour. The label's
text is written on the grid's tick too, and only while the label is showing.

Today, before this work, every conduit runs `Rename`, `Fit`, `Tint` and a label update every frame, the
rename allocating a string each time, and colours each stub with a property block, which stops URP batching
them. That is fine for hundreds of conduits and not for thousands.

**Measuring it:** spawn the world with every conduit wired in one line and watch the frame rate. Not built;
eyeballed for now.

## Networking

Unchanged by any of this. Every conduit, parked or wired, is one slot in `ConduitNetwork`, numbered by cell
in the same order on every client; the master writes every slot's faces and heat to the conduit chunks and
everyone follows. A hand on a non-master client sends its edit to the master. Parked or wired is just what
the faces say, so it reaches everyone and every late joiner the way any edit does. Hologram occupancy is
shared as before (`ShipNetwork`).

## Live wiring: holograms, segments, addons (design 2026-09-26, built)


Changing the ship's wiring while it runs, by hand. Design in progress (2026-09-26).

#### Opening a conduit

A hand inside a conduit's cell **holds it open**, and **the same hand can configure it** (changed
2026-10-03: players kept grabbing the handles with the holding hand, which used to be ignored). A second hand
works too. Picking the conduit is unambiguous because a hand is in exactly one cell, and nothing changes by
brushing past a cable, since changing anything takes a deliberate grab of a handle. A handle being dragged
keeps the conduit open wherever the hand goes.

An open conduit shows a **hologram**: a ghost of the conduit. The conduit itself **does not change
until the hologram is released**; then it takes the ghost's shape and the grid rewires it as
normal. If the holding hand leaves mid-drag, the ghost is discarded and nothing happened.

**Everything in the hologram answers a hand in reach** (2026-10-07, untested). The addon slots swell while a
hand is inside one; segment handles, arrows and the addon handle swell while XRI says a hand could grab them
(hovered) or has hold of them. They all share `ConduitHolograms`' Hover settings (grow 25%, over 0.12 s).
This is local feedback only: no other player sees it.

#### Segments

A conduit has a **fixed number of segments**: two for a plain conduit. Each segment is either on a
face or parked. The hologram shows, on every segment:

- **a handle at its tip:** grab and swing it to another face. It snaps to the nearest free face
  (never one another segment is on), with a haptic tick each time the preview changes.
- **an arrow along it:** grab and slide it, relative to the segment's own direction from the
  conduit's centre:
  - slide **outward**, away from the centre: **Out**
  - slide **inward**, toward the centre: **In**
  - slide **across** it: **None**, which parks it (below)

A **parked** segment's handle sits in the middle of the conduit. Dragging it out toward a face puts
the segment back on that face, **paired with its neighbour**: Out toward a neighbour's In, In toward
a neighbour's Out. With no neighbour on that face it comes back as **Out**. That lets a Junction +2
or +4 be wired up in more complicated ways than a fixed default would.

**Any mix of segments is allowed**, including two Outs and no In, or every segment In. They are
mistakes a player can make and diagnose. A conduit with every segment In and power arriving has no
outlet, so the power becomes heat on that tile: the existing dead-end rule, no special case.

A conduit connects **only through its segments**; it no longer accepts power on every face that is
not an output. What you see is what is wired. The existing scene's conduits get their two segments
from the faces they actually use.

#### Addons and upgrades

The hologram also has two slots:

- **an addon box:** a small addon item the player installs, which applies to **this tile only** and
  reshapes the conduit to suit it:
  - **switch**: turns the conduit into a rocker switch (`RockerSwitch`). The rocker *is* the
    switch; there is nothing else to operate
  - **Junction +1 to +4**: one to four extra segments (3 to 6 in all), shown by a small box on the
    conduit. Splitters and mergers are gone: a junction's arrows make it a split, a merge, or
    anything else.
- **an upgrade circle** above it: applies to the **whole branch**. Planned: coolant (see *Coolant upgrade for
  branches*) and data (`data.md`). Items are not an upgrade: they travel in item conduits (`loading.md`).

**How an addon is built (2026-09-26, slot flow 2026-09-27).** An addon only ever *adds* to the cable,
never replaces it. Every conduit carries an `AddonModule`: the addon slot. Its **Installed** field is
an addon prefab (the rocker is `RockerSwitch.prefab`, carrying `SwitchAddon`), and the module puts it
on the cable at its **Mount** when the world starts, so **nothing of it is in the scene while
editing**; a gizmo shows where it will be. `ConduitBehaviourModule` asks it questions (`Conducts`,
`ExtraSegments`, the unlock point) and stays the only writer of the tile.

The **carried item** (`Conduit Addons/Switch Addon.prefab`) is an `AddonItem`: a grab and a
rigidbody that stays where it was put until a hand first takes it. The two prefabs point at each
other: the item's **Installs** is the rocker, and the rocker's **Item** is the item.

- **Taking one out:** an open conduit's hologram shows the installed addon's item, small, in the
  addon slot. Grabbing it takes the addon off the cable; the item comes away full size in the hand.
  A plain conduit conducts, so taking an open switch out closes the line.
- **Putting one in:** let go of an item within **Install Radius** of an open, empty addon slot.
- **A newly installed switch starts open**, since that is how the scene's cold-start switch began.

**Repositionable addons (2026-09-27).** An addon with **Repositionable** ticked gets a capsule handle
in the hologram, beside the segment handles. It is not a segment: it carries no power, and moving it
is not rewiring. `ExtraSegments` is the other thing an addon can add, and the two are separate.

- **Swing** it to any free face (one no segment is on; segments cannot take its face either).
- **Twist** the wrist while holding it to turn the addon about the way it faces, in quarter turns
  with a tick, counted from how the hand was turned when it took hold. The capsule stands along the
  addon's up, so the twist reads at a glance.
- **Park** it in the middle: the addon is off entirely and tucked away, the conduit is plain cable,
  and the addon stays installed. A parked switch keeps its position for when it comes back out.
- `AddonModule` stores **Facing**, **Turns** and **Parked** as plain fields. A newly installed addon
  faces the free side nearest the player's head.

**Hologram locks** are asked, not read: `ConduitBehaviourModule.ShouldBeLocked()` is virtual and asks
the addon slot, which asks its working addon (`ConduitAddon.ShouldLockHologram()`, also virtual). A
parked addon asks for nothing, since its unlock point is tucked away with it. The upgrade slot joins
the same question when it is built, and a conduit with its own behaviour can override it and ask base
first. The unlock cube was 2.5 mm, its scale applied twice; it is 10 cm on trial.

An addon with something to press ticks **Require Hologram Unlock** and sets an **Unlock Transform**:
the conduit then opens only after a hand passes through a pulsing cube there. With no transform set,
the tick is dropped at `Awake`.

**Voltmeter (written 2026-10-07, untested).** `VoltmeterAddon` on `Conduit/VoltMeterModule.prefab`
shows the watts its conduit carries (`Inflow`), scaled to three digits and a unit: 450 W, 1.2 kW,
85 MW. Over 900 GW it reads "Holy / Shit". It changes nothing about the cable and has no state
(`NetworkState` 0): every client works the reading out from the same sim. It draws through `Readout`
(`modules.md` → *Readouts*). Conduits themselves no longer carry text; a meter is how you read one. Its item is
`Conduit Addons/Voltmeter Addon.prefab`, built like the switch's; it is addon 3 in `ConduitLayout`'s list.

**Item dispensers (written 2026-10-07, untested).** `ItemDispenser` (scripts/Networking) puts an item
(any `Item`: an addon item, or a loadable item like a fuel rod, `loading.md`) in its box collider whenever nothing solid is in the box: take one out and another appears. A new item hangs
where it appears, out of physics (kinematic), until a hand first lets go of it; from then on it is an
ordinary physics object for good (`Item.OnLetGo`). The item is a
prefab set on the dispenser in the scene, and must be registered on SceneNetworking. A variant, like a +4
junction, is the same prefab with the dispenser's **State** set (-1 keeps the prefab's own): the master
spawns it and so holds its authority, and `AddonItemStateNetwork` carries the state to everyone. Only the master looks
and spawns, through `WorldManager.Spawn`, so everyone, late joiners too, has the same items, and there is no
other state. Triggers never count as something in the box. In the Editor with no network it makes the item
locally.

**One addon per tile, one upgrade per branch.** Whether a branch may take more than one upgrade is
undecided; one for now.

**A branch** is a run of conduits ending at any fork, merge or component. **Components are part of
the branch**, and a battery or reactor belongs to **both** branches it sits between, acting as the
separator. It may later widen to "any run of basic conduits ending in anything that is not a basic
conduit".

Conduits can be slim, because nothing about a switch or a junction needs a bulky separate part.
This replaces `modules.md` → *Conduit variants*.

#### Handles and addon items do not collide with the player

A handle that follows a hand has no rigidbody, so when it meets the player's upright capsule (which
stays under the head, so a leaning player reaches into it) the solver cannot move the handle and moves
the player instead; the held handle follows, and it repeats. Pushing grabbables in Grow a Garden is the
other way round (free rigidbodies the player moves), which is why this never came up there.

**Fix: `IgnoresPlayerBody`** (scripts/Player) on the segment hologram, the addon handle and addon items.
It asks `PlayerHands.BodyColliders` for the solid colliders under the avatar's `Body.Root` and calls
`Physics.IgnoreCollision` for each pair, re-applied four times a second, since Unity forgets a pair
whenever either collider is switched off. The colliders stay solid, so hands still grab them.

What did not work, tried 2026-09-27: excluding the Player layer per collider (still pushed);
the XRInteractable physics layer (nothing grabbable); trigger colliders (walk through, but Somnium's
hands would not grab them).

**Somnium's guidance is to use `XRGrabInteractable` for anything grabbed.** The addon item uses it.
Much of what is already built drives interaction by hand instead (the rocker reads hand positions; the
lever and hologram handles are `XRSimpleInteractable` moved by our own scripts). That is why they feel
clunky, and it will not survive networking: see *Interaction rework*.

#### What the grid needs

From `modules.md` → *Rewiring live*: `NodeEdgeModule` raising `Changed`, `PowerGrid.Rewire(tile)`
reconnecting one tile and its neighbours, and `Disconnect` / `RemoveNode` in `PowerGraph`. Faces do
not need to become local to the tile, since the hologram edits faces directly rather than rotating
the tile.

## Building the grid from a script (explored 2026-09-27; drawn conduits built, the rest superseded by layout definitions)


The real ship will have hundreds of conduits, too many to place and face by hand. Generate them
instead. Explored 2026-09-27; nothing built, nothing decided.

#### Order of work (2026-09-27)

1. **Engineering bay.** Lay the scene out as a room, less prototype, so the switch can sit on a wall.
2. **Populate the grid from a script** (this section).
3. **Component slots:** places on the grid where interchangeable components go. The scripted grid
   is the natural first step toward them.

**Conduits live behind walls; always the plan.** Walls, floors and ceilings are `conduitContainer`
prefabs with a channel for the conduit, fitting whole cells (not reporting a scale). Each has an
**access panel** in front: pressed with a tool until it dissolves, or pulled off to float or fall
(undecided, later). A panel that is still on is one more answer to `ShouldBeLocked()`; once it is
off, the cell is exposed and a hand reaching in opens the hologram as it does today.

**Where an addon sits is the addon's call:** `ConduitAddon.Outward`, 0 in the middle of the cable
(a junction's box) to 1 flush with the cell face, in the panel's opening (the switch). Kept separate
from Repositionable on purpose.

**Cell size: 0.5 m (decided 2026-09-27).** `GridCell` is the only place a cell becomes metres. A
wall holding a conduit layer is one cell thick with its room face on the cell face; `Prefabs/Walls/ConduitContainerWall` is
two 0.17 m skins with the channel hollow between them. Its `ConduitContainer` keeps it on the grid
while editing: width and height snap to whole cells (the root's scale is the size in metres), depth
stays one cell, it turns in quarter turns, and its edges land on cell faces with the channel on a line
of cell centres. Panels come later. Placed tiles keep their `Size` in cells, so the battery (1) and reactor (5) footprints are
now half their old size in metres; set them to match their models when placing (an even size leans
toward the high side). Power moves a tile a tick and heat conducts per tile, so `power.md`'s per-tile
numbers now mean half the distance.

#### Drawn conduits (built 2026-09-27)

Conduits are no longer placed. `ConduitLayout` (on Grid) holds them as one packed `long` each
(`ConduitCode`: faces, addon id, facing, turns, parked, and x/y/z in 13 bits each), shows them as gizmos
while editing, and builds them from `Conduit.prefab` when the world starts, just before `PowerGrid`
builds. A placed tile in a drawn cell wins. Its **Addons** list may only be added to: a conduit
stores its addon by position in it.

The drawing tool is `ConduitLayoutEditor`, in the local package `Packages/com.spacescape.editor`
(Editor-only, never uploaded). Select Grid and pick a mode in the layout's Inspector:

- **Draw:** drag to lay a run; power flows the way you drag. Start on a conduit to branch. Over a wall
  the cable goes in the cell behind it; over a placed tile, in the cell in front. Shift, or pointing
  at nothing, draws on the working plane.
- **Erase:** click or drag.
- **Select:** click a conduit to set its six faces and its addon (facing, quarter turns, parked).

**A drawn conduit on a ring joins it (2026-10-09).** Rings are traced through drawn cells, and a drawn cell on one
takes the ring's two faces wherever its own drawing leaves a face None. So a run joins a ring, or leaves one, by
drawing just the cell where they meet, with a Junction for its third face. Before this, drawing a ring cell took it
out of the ring, splitting the ring into pieces whose first cell had no way in.

**Engineering, rewired 2026-10-09 through the hall's wall rings:** the reactor's Out climbs the wall behind it into
the y=4 ring (east, down the east wall, west along the south wall) and drops off it into batteries S1 (3),
S1 (4) and S1 (5). Those three batteries' Outs all join **one trunk** along the battery room's back wall (floor
level), which branches to the loaders just past the last battery and runs on to the radar and disruptor:
everything but the reactor runs off it. Battery S1 (2)'s Out climbs into the y=2 ring (the other way round) and leaves it behind the reactor into the
reactor's In, through a **switch on the east hall wall** (cell 80, 2, -15, facing the hall): the reactor's feed.
The two floor runs that did this before are gone.

#### What the current code forces on any generator

- **Walls must sit on the lattice.** `GridNode.Coordinate` rounds to integers, so cells are centred on
  whole numbers and their boundaries are at halves. The scene's `Wall` is centred at (0,2,3) and
  scaled 10x4x0.2, so its edges land on cell boundaries and it is 10 or 11 cells wide depending on
  rounding. Reading rows and columns off a wall's scale is ambiguous. Fix: the component holds its
  size **in cells** and sets the wall's scale from that, never the other way round.
- **Cables would float off the wall.** The nearest cell centre to that wall's face is 0.9 m away.
  Because the coordinate is rounded, a conduit can be nudged up to 0.49 m toward the wall and still
  count as the same cell, so only the model moves and the grid logic stays the same. Side effect:
  at an inside corner a nudged cable will not line up exactly with its neighbour on the other surface.
- **Where each wall's conduits get stored matters.**
  - Edit-mode generation needs no Editor code: `[ExecuteAlways]` and `[ContextMenu]` are in
    `UnityEngine`, so a generator can live in the SpaceScape assembly. This corrects the earlier
    note that it could not.
  - Without `PrefabUtility` the copies are unlinked: about 20 KB each (the size of
    `Conduit.prefab`) against roughly 6 KB for a prefab instance. 500 baked conduits would add about
    10 MB to the scene, and prefab edits would stop reaching them.
- **A filled wall of wired conduits would destroy itself.** Every conduit forwarding in a rectangle
  is a set of rings, and a ring blows its merge point on the first tick. Generated conduits start
  with both segments parked, or with wiring you draw in.

#### Options

| | How it works | Good | Bad |
|---|---|---|---|
| **A. Surface component** | `GridSurface` on a wall, floor or ceiling: size in cells, which side faces the room | Placing a wall places its conduits; easy to follow | Corners, doors and overlaps need care; one wall at a time |
| **B. Room volumes** | Rooms are whole-cell boxes; the grid is each room's inner shell, minus doorways | Corners and floors for free; could build the walls too | Ship must be boxy (the lattice already makes it so) |
| **C. Layout file** | A text map per deck | Short, diffable, easy to rebuild | Nothing to see in the scene while editing; a schematic, not a place |
| **D. Paint brush** | Editor brush that paints cells | Full hand control | Editor code in the separate package; a lot of clicking at ship scale |

#### Proposed: A, feeding one shared set of cells

- **One set, many sources.** Every `GridSurface` adds its cells to one set, and one pass turns the set
  into conduits. An inside corner claimed by two walls is added once and joins both runs. Outside
  corners are diagonal and need one bridging cell. Room volumes (B) could later be another source.
- **Hand-placed wins.** The generator skips any cell that already holds a `GridNode`, so the current
  scene's reactor, batteries, switch and wiring keep working and generated conduits fill the gaps.
- **Doors cut holes.** A door carries a `GridBlocker` volume and cells inside it are skipped. Move the
  door and the hole follows.
- **Built at runtime, previewed as gizmos in edit mode.** `PowerGrid.Awake` calls the builder before
  `Build()`. The scene stays small, prefab edits keep reaching every conduit, and every client builds
  the same grid from the same inputs, so networking only syncs what the crew changes (segment faces,
  addons).

```csharp
[ExecuteAlways]
public sealed class GridSurface : MonoBehaviour
{
    [SerializeField] private Vector2Int _cells = new Vector2Int(3, 2);   // columns, rows
    [SerializeField] private GridDirection _facing = GridDirection.ZMinus; // into the room
    [SerializeField] private Transform _wallModel;                      // scaled from _cells

    public IEnumerable<Vector3Int> Cells { get { /* one layer of cells on the _facing side */ } }
    private void OnDrawGizmos() { /* wire cube per cell */ }
}
// GridBuilder: gather every surface's cells, drop blocked and occupied ones,
// Instantiate(Conduit) with segments parked. PowerGrid.Awake calls it before Build().
```

#### Open

1. **Every cell a conduit, or empty sockets?** A full lattice means the crew only change segments on
   conduits that are already there, but a 10x10x4 engine room alone is about 360 conduits. Sockets
   mean the crew carry conduits and fit them: more physical work (principle 1), far cheaper, but it
   changes live wiring, which assumes the conduit already exists.
2. **What wiring does a new ship start with?** **Decided 2026-09-28:** the hand-painted runs; every
   other conduit parked. The ship ships working but off (`networking.md` → *The ship's conduits*).
3. **Cost at scale, not measured.** Every conduit runs `Fit`, `Tint` and `Rename` in `LateUpdate` each
   frame; every `GridNode` becomes a `PowerNode`; the ring check walks every live conduit each tick.
   Parked conduits may need to skip all of that. Time 1,000 unconnected nodes in the simtest harness
   before committing.

## Upgrades: coolant for branches (planned)

An upgrade that can be applied to a branch of the grid. Once applied, **coolant flows between
adjacent `CoolantModule`s**, including upgraded conduits, and carries heat away so it does not
accumulate along that branch.

- a new `CoolantModule`, added to components and to conduits when they are upgraded
- coolant moves between neighbouring coolant modules, the way heat already conducts between tiles
- it counters heat build-up on the tiles it passes through, and is used up doing it (coolant
  consumption, above)
- open: where the coolant comes from (the reactor's drum, a separate tank, both), and whether flow
  follows the power's direction or just any adjacent coolant module

A branch takes one upgrade: coolant, data (`data.md`) or items (`loading.md`), only one.

## Logic gates (idea, 2026-10-03)

Gates are conduit addons that decide whether power crosses from what arrives at their faces: AND, OR, NOT,
relays, thresholds. The switch is already one. Everything about them is in `data.md` (Two tiers of logic).
