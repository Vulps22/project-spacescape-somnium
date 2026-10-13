---
aliases:
  - "Ship structure"
tags:
  - structure
---
# Ship structure

Walls, floors, ceilings, rooms and corridors: how the ship is built in the Editor, and the parts of it players
touch. The conduits inside them are in [[conduits]].

## Conduit containers

**Status (2026-10-01):** built.

`Prefabs/Ship Parts/ConduitContainer`: a wall section (the Sci-Fi Styled Modular Pack's art) with an `AccessPanel`
on its root, whose **Cover** is the channel of cells behind it ([[conduits#Access panels|conduits → Access panels]]).
Its art stands on the floor (art children at local y -0.25, on a floor whose top is y -0.25). The art sits
about a quarter cell off the grid (local z -4.27), so where two walls meet there is a small gap or an overlap,
never flush; overlaps hide inside the art.

**The panel's art is the ship look** (2026-10-13, untested in-world): the access panel was the pack's `decorative_wall_2`;
it is now **Panel**, `Meshes/WallPanel.fbx` with a non-convex mesh collider, at local (0, -0.25, -4.271), facing the
room. The ship look sample's wall (`Ship Look Sample` in the scene): a 3.5 × 4 m section, panels in two columns over a
dark backing, a skirt low down, and half a rib at each edge, so containers side by side make a whole rib every 3.5 m.
0.26 m deep. The back skin (`wall_big`) is unchanged, and the corridor corners keep their own panels. Built by
`Blender/Walls/ShipWalls.py`, with **FloorTile** (`Meshes/FloorTile.fbx`: 4 × 4 m of 1 m plates on a dark backing, its
origin the middle of its top, so it sits on the floor at y -0.25 with its edges on cell boundaries when centred on one).
Both use the sample's materials (`ShipPanel`, `ShipSkirt`, `ShipFloor`, `FuelPackMetal`, `FuelPackDark`).

## Access panels

**Status (2026-10-01):** built.

`AccessPanel` on a container's root. A hand passed through any of its **sockets** (pulsing cubes, the
`HologramUnlock` prefab, previewed in the Editor) opens it: the panel's art **smokes away** (the
`SpaceScape/Dissolve` shader, `PanelDissolve` material) and its colliders go off at once, so the conduits behind
can be reached. While shut, the conduits it covers cannot be opened. It stays open while anything on its
**Occupant Layers** is in its trigger box (Somnium gives each player a `PlayerTrigger` on the Triggers layer;
nothing of the avatar is on Player), checked by overlap test a few times a second because trigger events need
a Rigidbody, and never shuts sooner than 3 s after opening; it then smokes back in, solid only once fully back.

A switch on a conduit behind a panel, facing it, reaches through it: its housing is made deeper until its front
stands 3 cm (Stand Off) proud of the panel's mesh at that spot, found by one ray at the panel's colliders when
the switch is placed. A locked conduit's unlock cube is at the conduit's middle.

Networking: `PanelNetwork` keeps every panel's open or shut as one bit in one `NetworkBridgeData`, panels
numbered by position. The master opens a panel when a hand there or a message asks, and shuts it once nobody
is in its box; everyone else follows. The scene object `PanelNetwork` (added 2026-10-07; it was missing,
so panels were not shared at all) carries a NetworkObject (Flags 524289, [[networking]] rule), a
`NetworkBridgeData4` (128 panels) and `PanelNetwork`.

The dissolve plays only where a camera can see the panel as it starts to open or shut (its bounds against
each camera's view; walls in the way do not count). Anywhere else it jumps straight to open or shut.

## Pinning to the grid

`GridPin` pins an object's origin and its **direct children's** to the 0.5 m grid while editing, one layer
down only (a corridor segment pins its containers, not the art inside them). Left alone: its Exclude list,
empties (sockets, markers), a panel's Cover Origin, and slots (whose centres sit on .25 and .75).
`AccessPanel` keeps a container's root on a cell centre and its Cover Origin on the grid by parity.

## Corridors and rooms

`Prefabs/Ship Parts/CorridorSegment`: two containers facing each other with a ceiling (on its `GridPin`'s
Exclude list); about 2.94 m wide inside. `CorridorCornerOutside`: a corner piece whose panel covers one column.
The engineering room (2026-10-02) is one root, `engineering`, with its walls and corners under it.

## Item dispensers

**Status (2026-10-07):** built, untested in-world.

`ItemDispenser` (scripts/Networking) puts an item
(any `Item`: an addon item, or a loadable item like a fuel rod, [[loading]]) in its box collider whenever nothing solid is in the box: take one out and another appears. A new item hangs
where it appears, out of physics (kinematic), until a hand first lets go of it; from then on it is an
ordinary physics object for good (`Item.OnLetGo`). The item is a
prefab set on the dispenser in the scene, and must be registered on SceneNetworking. A variant, like a +4
junction, is the same prefab with the dispenser's **State** set (-1 keeps the prefab's own): the master
spawns it and so holds its authority, and `AddonItemStateNetwork` carries the state to everyone. Only the master looks
and spawns, through `WorldManager.Spawn`, so everyone, late joiners too, has the same items, and there is no
other state. Triggers never count as something in the box. In the Editor with no network it makes the item
locally.

## Room and corridor prefabs

**Status:** planned.

Building a room means placing dozens of conduit containers by hand. Make **room and corridor prefabs** that
are dropped in whole: walls (conduit containers with their access panels), ceiling, and the floor's layer of
parked conduits ([[conduits]]), already lined up on the grid. A corridor segment (`Ship Parts/CorridorSegment`)
is the first of these. Nothing about sizes or how they join is decided.

## Loading bay doors for engineering

**Status:** planned.

Components have to get into and out of the engineering bay, and the corridors are far too narrow: the inside
is about 3.4 m wide, so an S5 (5 m) could never be changed out, and even an S1 (2 m) is a squeeze. The bay
needs **loading bay doors** big enough for the largest component it holds: the ceiling opening, or a wall,
or something else. Nothing decided.

## Writeable signs

**Status:** planned.

Signs the crew can write on, to label wiring and controls ("REACTOR FEED", "DO NOT OPEN"). Two shapes in
mind, undecided: a conduit addon, or a sign placed anywhere in the world that pins itself to the grid cells
it sits on without changing the cells or the grid. How the text is written (VR and 2D) and how it reaches
other players and late joiners (it is state) still to be discussed.

## Climbing

**Status:** planned.

Players scale their avatars, and many like being small. A small player cannot reach a high conduit or
control, so **walls and components must be climbable**: hand over hand up the wall to whatever is out
of reach.

**Flying is disabled** in the ship. Flying lets a head push through a wall and the body follows with
no questions asked, which puts players inside walls and behind panels. Garden already does this:
`PlayerBridge.SuppressSomniumLocomotion()` calls Somnium's `SetFlyModeDisableState(true)`, and
`SetLocalGravityScale` is there too for holding a climber up.

Open: what a hand grips (the conduits themselves, panel edges, rungs), and whether climbing is its
own interactor or Somnium has one.
