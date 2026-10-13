---
aliases:
  - "Conduits"
tags:
  - conduit
---
# Conduits

The conduit core every kind of conduit shares: where conduits can be installed, the cells they live in, the
hologram, segments, and what each costs. Each kind of conduit has its own doc: [[item-conduit]] carries items, and
power conduits are what most of this doc describes. What goes in a conduit's addon slot is [[addon]].

## Cells

**Status (2026-10-12):** built (Conduits V2), compiles clean, run in play mode on one client; untested in-world.

**A conduit exists only where the crew installed one.** Nothing is pre-placed and nothing fills the walls: the ship
holds a list of installed cells, and the grid, the effects and the data network only ever see those.
`ConduitCells` (on Item Conduits) is that list, keyed by cell, for both kinds:

| Per cell | |
|---|---|
| Kind | power or item: **one kind per cell** |
| Faces | which of the six faces a segment is on |
| Out | power only: which of those faces are Out; every other used face is In. Item conduits have no arrows |
| Addon | an id in `ConduitLayout`'s **Addons** list, shared by both kinds and only ever added to |
| Addon state, facing, turns, parked | the addon's own state and how it sits |
| Popped | power only |
| Heat | power only, sent for readouts |

**Two carriers read it.** `PowerConduits` makes a tile (`Conduit.prefab`: `GridNode`, `ConduitBehaviourModule`,
`AddonModule`, `Conduit`, `NodeEdgeModule`) for every installed power cell and adds it to the grid
(`PowerGrid.AddTile`), changes it when the cell changes, and destroys it (`RemoveTile`) when the cell goes. So
addons, the voltmeter, pops and repair work on it as on any tile. `ItemConduits` draws tubes and moves items for
the item cells. **The cell is the truth:** a hologram commit, an addon going in or out, a switch press and a pop all
change the cell first, and the carrier follows.

### Installing

**Status (2026-10-12):** rules 2, 5 and 6 refined with Vulps after the first try in-world, and built; untested
in-world. The empty cell's parked conduit is `ConduitCells.PendingAt`; uninstalling is `ConduitCells.UninstallLoose`,
which the master runs after every edit, as a hologram closes (its own, or another player's by messages 5 and 6), and
twice a second (a component taken away can leave a conduit with nothing pointing in).

1. **A cell can take a conduit only where something points into it:** a port's face, or an installed conduit's
   segment. Any installed conduit counts, whether or not its run still reaches anything, so a break can be rebuilt
   from either side.
2. **An empty cell opens as a parked conduit:** every segment parked, its handles in the middle, nothing drawn in
   the cell. It is taken to be a **power conduit** unless a segment is unparked onto a face toward an item port or an
   installed item conduit, which makes it an item conduit. Unparking a segment pairs its arrow as any parked segment
   does ([[#Segments]]): toward a neighbour's Out it is In, toward a neighbour's In it is Out, toward nothing it is Out.
   A fully parked conduit is decided again the same way when a segment is unparked, unless it holds an addon only a
   power conduit takes (anything but a junction): then it stays a power conduit.
3. **Never** inside a slot's volume, in a cell another tile fills, or behind a shut access panel.
4. **Both kinds pointing in:** an installed conduit's segment wins over a port, and power wins a tie.
5. **A parked conduit takes an addon** like any other: an addon let go in its slot goes in, and the conduit is
   installed with it, still fully parked.
6. **Parked is not grounds for uninstalling.** A conduit should be uninstalled only when it is fully parked **and**
   nothing (no port, no installed conduit's segment) points into it. Even then it waits for the hand: it is
   uninstalled only once its hologram closes (the hand leaves the cell). **Only then** does an addon or upgrade in it
   drop out as an item where the conduit was, as one taken out by hand does. A parked conduit with something pointing
   into it stays installed and keeps its addon.
7. **At most 1,000 cells** of both kinds together (`Max Cells`).

> [!info] Networking
> Only the master uninstalls (`ConduitCells.Decides`), so a conduit goes and its addon drops once. It knows every open
> hologram: its own, and everyone else's from messages 5 and 6 (a player leaving closes theirs). The dropped addon is
> spawned by the master where the conduit was (`ConduitCellNetwork`), a junction keeping its count; with no network it
> is made locally. Everyone else sees the cell go in the chunks.

### Access panels

A shut panel (`AccessPanel.Blocks`) still covers its cells: no hologram opens there and the master refuses a faces,
addon or pose edit in them. A switch behind it can still be pressed: an addon's state goes through. What a panel
covers is its own **Cover**: a block of cells along its own axes, centred on its **Cover Origin** (an empty child
where the conduits run), leaving out any cell inside a slot (`ComponentSlot.InSlot`).

### The starting layout

The ship starts with the hand-drawn wiring in engineering ([[#Drawn conduits]]). When the world starts, the master
seeds `ConduitCells` from `ConduitLayout`'s drawn cells: they are grouped into runs (two cells are joined where each
has a segment on the face between them), and a run is kept if any of its segments points into a slot or a component.
The rest, drawn runs that start and end with nothing, are left out, with one log line saying how many.

## What a conduit costs

**Status (2026-10-07):** in progress, on branch `parked-conduits-as-data` (named before the plan changed: parked
conduits are **not** data).

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
| Network | its cell's three ints in the conduit cell chunks | same |

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

## The hologram

**Status (2026-10-07):** built; hover feedback untested in-world.

Changing the ship's wiring while it runs, by hand. 

### Opening a conduit

A hand inside a conduit's cell **holds it open**, and **the same hand can configure it** (changed
2026-10-03: players kept grabbing the handles with the holding hand, which used to be ignored). A second hand
works too. Picking the conduit is unambiguous because a hand is in exactly one cell, and nothing changes by
brushing past a cable, since changing anything takes a deliberate grab of a handle. A handle being dragged
keeps the conduit open wherever the hand goes.

An open conduit shows a **hologram**: a ghost of the conduit. The conduit itself **does not change
until the hologram is released**; then it takes the ghost's shape and the grid rewires it as
normal. If the holding hand leaves mid-drag, the ghost is discarded and nothing happened.

**Handing over a cell** (2026-10-13, untested in-world): an empty cell's parked conduit and an installed power conduit
are shown by different holograms, so laying the first segment swapped them: the old one played its close while the
new one opened from nothing, and the conduit could not be worked for about a second. Now a hologram opening on a cell
another is showing carries on from that one's opening, and the other goes at once (`ConduitHologram.TakeOver`).

**Landing rings** (2026-10-13, untested in-world): while a handle is held, a ring the size of a handle stands on every
place it could land (each free face, and the middle, where letting go parks it), turned toward the player: white, and
green on the one it would land on if let go now. Local, like the rest of the hologram's feedback. Set on
`ConduitHolograms` (**Landing Material**, colours, width); line renderers in `HighlightWhite.mat`.

**Everything in the hologram answers a hand in reach** (2026-10-07, untested). The addon slots swell while a
hand is inside one; segment handles, arrows and the addon handle swell while XRI says a hand could grab them
(hovered) or has hold of them. They all share `ConduitHolograms`' Hover settings (grow 25%, over 0.12 s).
This is local feedback only: no other player sees it.

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

### The addon slot and the upgrade circle

The hologram also has two slots:

- **an addon box:** one addon item, which applies to **this tile only** and reshapes the conduit to suit it.
  What each addon does: [[addon]].
- **an upgrade circle** above it: applies to the **whole branch**. Planned: coolant (see *Coolant upgrade for
  branches*) and data ([[data]]). Items are not an upgrade: they travel in [[item-conduit|item conduits]].

**One upgrade per branch.** Whether a branch may take more than one upgrade is undecided; one for now.

**A branch** is a run of conduits ending at any fork, merge or component. **Components are part of
the branch**, and a battery or reactor belongs to **both** branches it sits between, acting as the
separator. It may later widen to "any run of basic conduits ending in anything that is not a basic
conduit".


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
clunky, and it will not survive networking: see [[interaction#Interaction rework|interaction → Interaction rework]].

### What the grid needs

`PowerGrid.AddTile` and `RemoveTile` put a tile in and take it out while the grid runs, keeping its cell lookups and
its neighbours' edges; `Rewire(tile)` reconnects one tile when its faces change ([[power#Conduits come and go|power →
Conduits come and go]]).

> [!warning] Concern
> **Flow direction has to be visible in the ship.** Allocation is readable from cable count and branch
> depth, but only if you can see *which way power moves* — and a tile's output face is currently
> invisible in game. Conduits need etched arrows, a travelling glow, or something equivalent.

## Drawn conduits

**Status (2026-10-12):** built; the drawing is now only the starting layout.

`ConduitLayout` (on Grid) holds the ship's starting wiring as one packed `long` per cell (`ConduitCode`: faces,
addon id, facing, turns, parked, and x/y/z in 13 bits each) and shows it as gizmos while editing. When the world
starts it is seeded into `ConduitCells` ([[#The starting layout]]). Its **Addons** list may only be added to: every
conduit, of either kind, stores its addon by position in it.

The drawing tool is `ConduitLayoutEditor`, in the local package `Packages/com.spacescape.editor`
(Editor-only, never uploaded). Select Grid and pick a mode in the layout's Inspector:

- **Draw:** drag to lay a run; power flows the way you drag. Start on a conduit to branch. Over a wall
  the cable goes in the cell behind it; over a placed tile, in the cell in front. Shift, or pointing
  at nothing, draws on the working plane.
- **Erase:** click or drag.
- **Select:** click a conduit to set its six faces and its addon (facing, quarter turns, parked).

> [!warning] Engineering's starting wiring is broken (2026-10-12)
> Seeded in play mode: 254 cells kept, 52 in 4 runs left out. Only the reactor S5, the radar and the disruptor are
> joined. **Batteries S1 (2) to S1 (5) are not:** the drawn runs beside them point into the side of each battery at
> y 1, where the new S1 battery has no port (its In, Data and Out face +Z at y 0, z -64), and the reactor's feed into
> them went through the wall rings, which V2 removed. The test hall's wiring was made by hand at runtime and never
> drawn, so it is not there either. Needs redrawing.

## Networking

**Status (2026-10-12):** built (Conduits V2), compiles clean; untested with a second player. The rules every system
follows are in [[networking#Rules|networking → Rules]].

| State | Owner | How it travels |
|---|---|---|
| Every installed cell, both kinds: kind, faces, Out, addon, its state and pose, popped | the master | `ConduitCellState` chunks |
| Power conduit heat | the master | the same chunks, 4 times a second |
| A hand's edit (faces, addon in or out, pose, a switch press) | the hand's client | applied there at once, then ship message 8, **ConduitCellEdited**, to the master |
| Who has a cell's hologram open | the opener | ship messages 5 and 6 by cell, to everyone; nothing kept for late joiners |
| The hologram's ghost while dragging | the editor's client | nothing until release; discarded if the hand leaves |
| The starting layout | the scene | nothing: the master seeds it, and everyone else reads the chunks |

### Conduit cell state

`ConduitCellNetwork` (on Item Conduits) owns the transport. The master spawns as many `ConduitCellState` chunks
(`Prefabs/Networking/ConduitCellState.prefab`, a Data64) as the cells need, twenty cells each, and writes them when the
cells change and 4 times a second for heat. A chunk is four header ints (stamp, index, length, total chunks), then
three ints a cell:

1. the cell, 10 bits an axis (±512), and its kind
2. faces, Out, addon id, addon state, facing, turns, parked, popped
3. heat in 16 bits (0.05 °C steps from -100 °C); 16 bits spare for layout saving

Everyone else puts the chunks back together in order and makes its cells match, cell by cell, so its own pops are
undone too. **Conduit cells are stored by position in a host-owned list**: the cell is in the entry, so nothing
depends on two clients building the same list.

### Joining

The sim is one machine, so a client cannot simulate any of it until it has all of it. A joiner holds its grid still
(`PowerGrid.Held`) until it has read chunks 0 to total-1 at one write; the master skips the wait, since it writes
them. With no network at all the grid simply runs. Only the master's grid rolls failures.

### Edits from non-masters

A hand on a non-master changes its own cell at once (`ConduitCells.EditByHand`) and sends message 8 (edit, cell,
kind, two values) to the master, which checks it by the same rules (`ConduitCells.Apply`, which refuses an edit
behind a shut panel unless it is a switch's state) and writes it to everyone. **That one cell** keeps the editor's
version against corrections for 2 seconds while the master catches up; every other cell, and its heat, keeps
updating. An addon item taken out is spawned as before (`AddonItemNetwork`).

### Holograms open elsewhere

Opening and closing a hologram is a `ShipNetwork` message to everyone (5 and 6: the cell and the player id), for
either kind. Another player's open conduit is boxed tight in `HoloHandle.mat` and cannot be opened here. Two opening
at once: the lower player id keeps it. A player leaving clears theirs.

## Upgrades: thermal shielding

**Status (2026-10-12):** idea (Vulps).

A branch upgrade that raises the **pop floor** of every conduit on its branch from 250 °C to **1000 °C**: below it a
conduit never pops, however much it carries ([[power#Failure|power → Failure]]). For runs next to hot machines.

## Upgrades: coolant for branches

**Status:** planned.

An upgrade that can be applied to a branch of the grid. Once applied, **coolant flows between
adjacent `CoolantModule`s**, including upgraded conduits, and carries heat away so it does not
accumulate along that branch.

- a new `CoolantModule`, added to components and to conduits when they are upgraded
- coolant moves between neighbouring coolant modules, the way heat already conducts between tiles
- it counters heat build-up on the tiles it passes through, and is used up doing it (coolant
  consumption, above)
- open: where the coolant comes from (the reactor's drum, a separate tank, both), and whether flow
  follows the power's direction or just any adjacent coolant module


A branch takes one upgrade: coolant or data ([[data]]), only one.

## Tried and rejected

- **Building the grid from a script** (explored 2026-09-27): a `GridSurface` on each wall, floor or ceiling feeding
  one shared set of cells, against room volumes, a layout file per deck, or a paint brush. Replaced by layout
  layout definitions on the conduit containers (since removed: Conduits V2 needs no wall volumes, and a panel holds
  its own cover). What it taught: walls must hold their size **in cells** and set
  their scale from it, never the other way round; a filled wall of wired conduits is a set of rings and blows on
  the first tick, so generated conduits start parked; unlinked baked copies cost about 20 KB each against roughly
  6 KB for a prefab instance.
- **A lattice of parked conduits in every wall, with two pre-wired rings** (built 2026-10-03, replaced 2026-10-12 by
  Conduits V2): `ConduitLayout` generated 14,544 power conduits from the layout definitions (one per wall container, corner, slot
  face and the engineering floor), each a grid node ticked
  20 times a second, and the tick's ring search allocated over every node. With every machine off the world ran at
  about 25 FPS (`PowerGrid.Update` 20 ms a frame, 2 MB of garbage). Installing on demand: 90 FPS, 0.14 ms.
- **Every cell a conduit, or empty sockets the crew fit?** Asked 2026-09-27. Conduits V2 answers it: a conduit is
  installed where a port or segment points, so nothing is pre-placed.
