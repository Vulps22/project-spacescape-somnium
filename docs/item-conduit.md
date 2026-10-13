---
aliases:
  - "Item conduit"
tags:
  - conduit
---
# Item conduit

A conduit that carries items between item ports ([[loading]]). It lives on the conduit core every conduit shares
([[conduits#Cells|conduits → Cells]]): once installed it has segments, parking, the hologram and addons like any
conduit.

## Rules

**Status (2026-10-09):** built, untested in-world.

**An item conduit is a conduit.** Once installed it behaves exactly as a power conduit does: segments,
parking, the hologram, addons. Installing, staying installed and one kind per cell are the core's rules
([[conduits#Installing|conduits → Installing]]); these are the item conduit's own.

1. **Segments.** Two, more with a Junction addon. Each is on a face or parked. **No In/Out arrows for now**: which
   way items go comes from the loader's end. Merges will need them (to-do).
2. **Installing:** an empty cell opens as a parked conduit, taken to be power; unparking a segment toward an item port
   or an installed item conduit makes it an item conduit ([[conduits#Installing|conduits → Installing]]).
3. **Uninstalling:** only when fully parked with nothing pointing into it, and only once its hologram closes; its
   addon then drops out as an item.
4. **Detaching in the middle:** parking a cell's segments splits a line in two. Both sections stay installed.
   **Items travelling in a section cut off from its loader fall out of the open end as real items.**
5. **Addons behave the same on any conduit**; the addon decides what happens when it is taken out. Taking a
   Junction out parks its extra segments and gives the item back, in the hologram's addon slot as for power.
6. **Destroyed** (later, with weapon damage): only that cell goes; its neighbours stay installed.
7. **One kind per cell:** a cell holds a power conduit or an item conduit, never both.

- **The want travels along the item conduit, not the data network.** The receiving component's
  `LoadingModule` shows that it has room for an item of its type; the loader at the other end of the run
  reads that and decides for itself to send one, if its end pack holds that type. Nothing commands anything
  ([[data#^no-commands|data → No commands]]). The loader takes one out of the end pack (its filled places drop by one) and
  sends it.

## Built

**Status (2026-10-09):** built, untested in-world.

- **`ItemConduits`** is the item carrier: it reads the item cells from `ConduitCells` and draws their tubes, finds
  the item ports and works out routes. A cell's addon is an id in `ConduitLayout`'s shared **Addons** list; an item
  conduit takes only a Junction.
- **One hologram for every conduit:** `ConduitHologram` now works an `IConduit` (`scripts/Ship/Wiring/IConduit.cs`):
  `TileConduit` for a power conduit, a cell of `ItemConduits` for an item conduit (no arrows), and a pending power
cell for an empty cell a power port or conduit points into. `ItemConduitHolograms`
  gets its hologram from `ConduitHolograms.MakeHologram`, so the parts, settings and addon-item handling are the
  power conduit's. An empty cell something points into opens as a parked conduit
  ([[conduits#Installing|conduits → Installing]]).
- **Parity with power conduits (fixed 2026-10-09 after Vulps tried it):** an addon item shrinks to fit the item
  conduit's addon slot as it does a power conduit's; a cell another player has open is boxed; each segment shows on
  its own (one segment on a face is half a tube), so parking one side hides that side.
- **While its hologram is open** a cell's tube smokes away in `PanelDissolve` (the access panels' dissolve) and a
  power conduit's cable shows its segments, since nothing inside a solid one-cell tube can be seen.
- **Tubes** from each cell's faces: half a tube for one face, straight, corner, and a junction block
  with a mouth per face for three or more faces or a junction addon. Routes go through any cell with three or more
  faces as a junction, taking turns.
- **Networking:** the shared conduit cell chunks and message 8 ([[conduits#Networking|conduits → Networking]]).
- **Falling out:** an item whose route to its port is gone is made, by the master, as a real item where it was last
  shown in the tube (`LoaderBehaviourModule.DropOut`, spawned by `ComponentNetwork`). It drops where it is rather
  than travelling on to the open end.

## Tubes, and items in them

**Status (2026-10-09):** built, untested in-world.

- **What they are:** a conduit wired to an item port becomes an item conduit: a new model, a **square
  pneumatic tube** of glass you can see the item pass through. **Its outside is exactly the cell (0.5 m)**,
  with thin glass walls and about 0.46 m clear inside. The fuel rod shrinks a little to fit (0.44 m across the
  corners). The port keeps its 0.5 m opening and 0.56 m border, so the tube fits inside the border.
- **An item in the tube is a picture, not a spawned object**, as a pack in a loader is: what it is and how
  far along the run. It moves at **2 cells a second** (provisional); every client moves it, and only the master
  decides it has arrived and loads it into the `LoadingModule`.
- **Corners are solid, no glass.** An item is longer than a corner (a rod is 1 m, a corner 0.5 m), so it is
  cut at the walls (on the CPU, as built; first planned as a clip shader): going in, nothing past the corner's far wall is drawn, so the item
  is swallowed by the solid corner; once all of it is inside it is turned to the way out; coming out, nothing
  behind the exit wall is drawn, so it grows out of the wall. The same tidies an item going into a port.
- **Tubes** (`Blender/ItemConduit.py` → `Meshes/ItemConduit.fbx`):
  - **Straight** pieces have glass walls (`ItemConduitGlass`, transparent URP Lit) between 3 cm metal edge
    bars, with a ring at each end. The clear inside is 0.46 m.
  - **Corner** pieces are solid metal, dark inside.
  - Everything is exactly one cell across.
- **Clipping is done on the CPU, not in a shader** (changed while building, 2026-10-09). `ClippedItemPicture`
  copies an item's meshes and, each frame, cuts every triangle to the kept side of two planes. It looks the
  same as the planned shader: swallowed by a corner, growing out of a wall. It works with any item's own
  materials, the luster shader included, and needs no custom shader in the build. A rod is 72 triangles.
  **Every item's meshes must be Read/Write enabled**; one that isn't is shown uncut, with a warning.
- **Routes:** a route is the straight legs between solid corners, from the sender's port face to the
  receiver's. An item crosses each leg from fully hidden behind its start plane to fully hidden past its end
  plane, so a leg costs its length plus the item's length.

## Junctions

**Status (2026-10-09):** splitting built, untested in-world; merging next.

For the six-port reactor. **Split only**: one loader feeds a tree. Merging (several loaders onto one trunk,
each port asking for its own type) is next and on the to-do list; every item already carries its destination,
so it needs spacing on a shared trunk, routes from several senders and segment arrows, not a redesign.

- **A Junction is an ordinary addon** (the same +1 to +4 items as power conduits): let go of it in an open item
  conduit's addon slot, and lift it out of the slot to take it out, as on a power conduit. It gives the cell its
  extra segments; taking it out parks every segment, as on a power conduit, and the item comes back in the hand.
- **Items take turns, as Satisfactory's splitter:** each junction sends its next item down the side after the one
  it last used (sides in face order), skipping a side where nothing wants the item. Up to five outputs (a +4).
- **An item arriving where it is no longer wanted** (filled by hand meanwhile) is lost with a warning, as before.


## Networking

| State | Owner | How it travels |
|---|---|---|
| A cell, its junction and its extra segments | the master | its entry in the `ConduitCellState` chunks, as every conduit's ([[conduits#Networking\|conduits → Networking]]) |
| Putting a junction in or taking it out | the hand's client | ship message 8 (Addon edit); the item taken out is spawned as for power (`AddonItemNetwork`) |
| Each junction's turn | the master only | not sent: an item's destination is fixed when it leaves. A new master starts every junction at side 1 |
| Items in the tube | the master, on the loader | the loader's state: per item its kind, distance, and destination cell (two halves); every loader has a `NetworkBridgeData64` (37 to 46 ints) |
| Who has an item conduit's hologram open | that player's client | told to everyone by cell (ship messages 5 and 6), as every conduit's is; nothing kept for late joiners |
| A component wanting an item | nobody new | each client reads the receiving `LoadingModule`, already networked, at the far end of the run |
| Sending one | the master, on the loader | the loader's networked state: what is in the tube and how far along; every client moves the picture, the master decides the arrival and loads it |

## Tried and replaced

- **Runs** (built 2026-10-08, replaced 2026-10-09): a chain from a port that grew only at its open tip, kept as a
  first cell and one exit face per cell. It could not lose a cell in the middle, so the conduit core above replaced
  it.
- **The clip shader** (superseded 2026-10-09 by cutting on the CPU, above): a Shader Graph copy of URP Lit
  with two optional clip planes. Kept here in case the CPU cut ever costs too much.
