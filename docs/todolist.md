# To do

**Networking is provisionally done (2026-09-28):** see `networking.md`, untested with a second player.
What follows is the rest of the queue; build it with networking in mind (`networking.md` → Rules).

## Open: an overproducing reactor bakes its own cables

Since power only goes where a receiver can be reached (`power.md`), a reactor with nowhere to send its
output takes it as heat itself. At the current settings that is 100 C a second at 2000 W, and heat
conducts along the metal with no current in it, so in the harness two minutes left an 8-tile run at
about 1300 C behind a 1384 C reactor. Nothing pops while nothing flows (pop chance is power x heat),
but reconnecting any receiver would send full power down cables already that hot.

Options: stop a producer's surplus conducting into conduits; give the reactor a heat ceiling it
scrams at; or treat it as the crew's problem (throttle the reactor), which is the lever it is for.
Undecided.

## Open: the battery tier ladder

Plain and safety cells now behave the same, since the grid refuses dead runs itself. The bottom rung
needs a new question, or the ladder becomes two tiers (`power.md` → Three cell tiers, `principles.md`
10).

## Live wiring

Changing the ship's wiring while it runs, by hand. Design in progress (2026-09-26).

### Opening a conduit

One hand inside a conduit's cell (its 1 m box) **holds it open**; the **other hand** configures it.
Either hand can do either job. Picking the conduit is unambiguous because a hand is in exactly one
cell, and nothing changes by brushing past a cable, since configuring always takes a second hand.

An open conduit shows a **hologram**: a ghost of the conduit. The conduit itself **does not change
until the hologram is released**; then it takes the ghost's shape and the grid rewires it as
normal. If the holding hand leaves mid-drag, the ghost is discarded and nothing happened.

### Segments

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

### Addons and upgrades

The hologram also has two slots:

- **an addon box:** a small addon item the player installs, which applies to **this tile only** and
  reshapes the conduit to suit it:
  - **switch**: turns the conduit into a rocker switch (`RockerSwitch`). The rocker *is* the
    switch; there is nothing else to operate
  - **Junction +1 to +4**: one to four extra segments (3 to 6 in all), shown by a small box on the
    conduit. Splitters and mergers are gone: a junction's arrows make it a split, a merge, or
    anything else.
- **an upgrade circle** above it: applies to the **whole branch**. Coolant is the only one planned
  (see *Coolant upgrade for branches*).

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

**One addon per tile, one upgrade per branch.** Whether a branch may take more than one upgrade is
undecided; one for now.

**A branch** is a run of conduits ending at any fork, merge or component. **Components are part of
the branch**, and a battery or reactor belongs to **both** branches it sits between, acting as the
separator. It may later widen to "any run of basic conduits ending in anything that is not a basic
conduit".

Conduits can be slim, because nothing about a switch or a junction needs a bulky separate part.
This replaces `modules.md` → *Conduit variants*.

### Handles and addon items do not collide with the player

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

### What the grid needs

From `modules.md` → *Rewiring live*: `NodeEdgeModule` raising `Changed`, `PowerGrid.Rewire(tile)`
reconnecting one tile and its neighbours, and `Disconnect` / `RemoveNode` in `PowerGraph`. Faces do
not need to become local to the tile, since the hologram edits faces directly rather than rotating
the tile.

## Training room

**The #1 player complaint is not understanding how to use the wiring system.** Phase 1 (2026-09-27,
branch `training-room`): six free-standing, looping displays in a row under `TrainingRoom`, each a
conduit with ghost hands acting out one use, under a static board listing every step so a player can
take it in at a glance before trying. In teaching order:

1. **Opening a conduit:** reach in, the hologram opens, take your hand out to close it.
2. **Rewiring, two hands:** one hand holds it open; the other swings a handle to another face (the
   cable follows), then slides an arrow to flip the flow.
3. **Parking a segment:** drag a handle into the middle to park it (that side is sealed), then out to
   a face again.
4. **Installing an addon:** carry it to the addon slot and let go; grab it from the slot to take it out.
5. **Opening a switch:** pass a hand through the pulsing unlock cube, then reach in.
6. **Moving a switch:** swing the capsule handle to another face, twist the wrist to turn it, drag it
   into the middle to park it, pull it out to bring it back.

`TutorialDisplay` builds each one from the real hologram, switch and addon prefabs when the world
starts and strips out every script, interactable, joint, rigidbody and collider, so it only shows and
the grid never sees it. The ghost hands are simple shapes in `GhostHand.mat`; there is no hand model
in the project.

Next: a room built around the displays.

## Interaction rework

**Found 2026-09-27.** Most of what a hand does in the scene is driven by our own scripts rather than
stock XRI components, which is why it all feels a little clunky, and it will not survive networking:
each piece keeps its own private state that nothing else could sync. Somnium's guidance is
`XRGrabInteractable` for anything grabbed.

| Piece | Today | Stock replacement to try |
|---|---|---|
| Rocker switch | **rebuilt 2026-09-27:** two solid box colliders, one per half of the paddle, on a rigid pivot. Each physics step `Physics.ComputePenetration` measures how far the avatar has pushed square into the raised half, and the paddle rocks in by that much; past the snap point it snaps over, let go early it eases back. No joint or spring (a hinged rigidbody bounced and flexed against the hand) | done; untested in-world |
| Rod lever | `XRSimpleInteractable`, dragged through our own damped spring | `XRGrabInteractable` held to a hinge (a joint or an axis-constrained grab transformer) |
| Hologram handles and arrows | `XRSimpleInteractable`, positions and snapping computed by `ConduitHologram` | `XRGrabInteractable`, reading where it was let go; keep the snapping |
| Addon slot | distance from the slot, checked when an item is let go | `XRSocketInteractor` on the slot |
| Addon item | `XRGrabInteractable` | already stock |

Open: whether Somnium's hands include a poke interactor; and how Somnium syncs an
`XRGrabInteractable` (and whether the rest needs Fusion) before networking.

## Building the grid from a script

The real ship will have hundreds of conduits, too many to place and face by hand. Generate them
instead. Explored 2026-09-27; nothing built, nothing decided.

### Order of work (2026-09-27)

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

### Drawn conduits (built 2026-09-27)

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

### What the current code forces on any generator

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

### Options

| | How it works | Good | Bad |
|---|---|---|---|
| **A. Surface component** | `GridSurface` on a wall, floor or ceiling: size in cells, which side faces the room | Placing a wall places its conduits; easy to follow | Corners, doors and overlaps need care; one wall at a time |
| **B. Room volumes** | Rooms are whole-cell boxes; the grid is each room's inner shell, minus doorways | Corners and floors for free; could build the walls too | Ship must be boxy (the lattice already makes it so) |
| **C. Layout file** | A text map per deck | Short, diffable, easy to rebuild | Nothing to see in the scene while editing; a schematic, not a place |
| **D. Paint brush** | Editor brush that paints cells | Full hand control | Editor code in the separate package; a lot of clicking at ship scale |

### Proposed: A, feeding one shared set of cells

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

### Open

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

## Climbing

Players scale their avatars, and many like being small. A small player cannot reach a high conduit or
control, so **walls and components must be climbable**: hand over hand up the wall to whatever is out
of reach.

**Flying is disabled** in the ship. Flying lets a head push through a wall and the body follows with
no questions asked, which puts players inside walls and behind panels. Garden already does this:
`PlayerBridge.SuppressSomniumLocomotion()` calls Somnium's `SetFlyModeDisableState(true)`, and
`SetLocalGravityScale` is there too for holding a climber up.

Open: what a hand grips (the conduits themselves, panel edges, rungs), and whether climbing is its
own interactor or Somnium has one.

## Repairing

A way for the crew to restore what heat has broken:

- **severed conduits:** popped is permanent today, so a blown cable can only ever be replaced, never
  mended (ties into live wiring)
- **damaged components:** `Integrity.Restore` exists in the sim but nothing in the world calls it

## Loading

A way to physically load consumables into a component: fuel and coolant into the reactor first. It
is the fuel rack and coolant pad that `reactor.md` lists as not built; today the fuel seconds and
coolant litres are preset numbers in the Inspector.

- **a `LoadingModule`**, not a reactor feature, so anything that consumes something loadable can use
  it: missiles and gatling guns taking ammunition, if they are added
- it owns what has been loaded and hands it to the behaviour as the behaviour uses it up; the
  behaviour decides what it accepts and how fast it burns through it, the way the capacitor holds
  power and the behaviour decides what to do with it
- **one `LoadingModule` holds one kind of thing.** A component that takes several carries one per
  kind, so the reactor has two: one for fuel, one for coolant

## Coolant consumption

Coolant exists only inside the reactor today (`CoolantLoop`: a drum, a pump ceiling, a flow the crew
sets, and litres boiling off as it carries heat away). It needs to become something the ship
consumes and the crew has to manage.

### Coolant upgrade for branches

An upgrade that can be applied to a branch of the grid. Once applied, **coolant flows between
adjacent `CoolantModule`s**, including upgraded conduits, and carries heat away so it does not
accumulate along that branch.

- a new `CoolantModule`, added to components and to conduits when they are upgraded
- coolant moves between neighbouring coolant modules, the way heat already conducts between tiles
- it counters heat build-up on the tiles it passes through, and is used up doing it (coolant
  consumption, above)
- open: where the coolant comes from (the reactor's drum, a separate tank, both), and whether flow
  follows the power's direction or just any adjacent coolant module
