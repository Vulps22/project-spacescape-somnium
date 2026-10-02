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
| Along every floor | conduits **sitting on the floor**, not under it, to route across rooms and corridors | planned |

**The layout definitions are the ship's grid.** `ConduitLayout`'s drawing tool will become the way to wire the
definitions' cells (the starting wiring painted onto conduits that are already there), not a way to put
conduits anywhere. Eventually no drawn run exists outside the cells the definitions hold.

Whether a slot's shell is exposed or behind housing panels is not decided.

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
| Name | set once, when built | set once |
| Debug label | none unless debug visuals are on | same |
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
| Debug visuals switched on or off | `DebugVisuals` raises an event when the switch changes | shows or hides its label |
| Its hologram opens or closes | the hologram, as now (`DebugLabel.Revealed`) | shows or hides its label |

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
