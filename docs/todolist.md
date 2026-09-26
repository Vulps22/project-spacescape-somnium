# To do

Single-player work to finish before networking. Networking comes after these, on purpose: every one
of them changes what there is to sync.

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

**One addon per tile, one upgrade per branch.** Whether a branch may take more than one upgrade is
undecided; one for now.

**A branch** is a run of conduits ending at any fork, merge or component. **Components are part of
the branch**, and a battery or reactor belongs to **both** branches it sits between, acting as the
separator. It may later widen to "any run of basic conduits ending in anything that is not a basic
conduit".

Conduits can be slim, because nothing about a switch or a junction needs a bulky separate part.
This replaces `modules.md` → *Conduit variants*.

### What the grid needs

From `modules.md` → *Rewiring live*: `NodeEdgeModule` raising `Changed`, `PowerGrid.Rewire(tile)`
reconnecting one tile and its neighbours, and `Disconnect` / `RemoveNode` in `PowerGraph`. Faces do
not need to become local to the tile, since the hologram edits faces directly rather than rotating
the tile.

## Building the grid from a script

The real ship will have hundreds of conduits, too many to place and face by hand. Generate them
instead.

- **Editor-time:** a tool writes the tiles into the scene, saved like hand-placed ones. No runtime
  cost and nothing new for the game to understand, but the tool cannot live in the SpaceScape
  assembly: the uploader rejects Editor code even behind `#if UNITY_EDITOR`. It would go in a local
  package, as Somnilux does, which is never uploaded.
- **Runtime:** the world builds the grid from a data file when it loads. Easy to change, but the
  scene is empty while editing, and every client builds the same grid for itself.
- open: which of those, and what the input looks like (a list of runs between points, a map, a
  layout file).

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
