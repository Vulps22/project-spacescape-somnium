---
aliases:
  - "The component hologram"
tags:
  - interaction
  - component
---
# The component hologram

A hologram opened on a component's face, like a conduit's: one hand within a cell of the slot's face holds it
open, and it opens on that face. It is where the component's own work happens: repair games ([[repair]]), and
addon slots (below). Nothing is built yet.

## Opening it

**Status (2026-10-01):** decided; not built.

Repairs and addon wiring live here; everything else a component does is a physical control on it.

- **One hand holds it open**, as with conduits: a hand within one cell (0.5 m) of a slot's face, in
  the shell around the face rather than inside the component. That hand can play too, as with conduits.
- **It opens on the face the hand is at** (the nearest, at a corner), and the game shows there. Weld's
  cracks are on that face.
- **No bigger than S1** (2 m square), whatever the slot's size, centred on that face.

## Addon slots

**Status (2026-10-03):** idea.

**Addon slots at fixed places round the component**, shown in its hologram. An addon item put into one spawns a
list of **nodes**: the component's functions, behaviours and settings that the addon can be wired to. **Wiring
the addon to a node is done as with a conduit**: the same hologram handles, from the addon to the node wanted.
So an addon changes what a component does by being wired to one of its functions, the way a switch changes a
cable.

- **Junctions are rejected** by a component's addon slot in most cases, at this stage.
- Which addons go into components, which nodes each component offers, and what wiring one to a node does are
  still to be worked out.
- **Networking still to be discussed** before it is built (`CLAUDE.md`): an installed addon and its wiring are
  the component's state, presumably kept by the master like a conduit's edits, with a non-master's hand sending
  its edit there.
- **Maybe the foundation for logic** (2026-10-03): the nodes are a component's inputs and outputs, wired with the
  conduit grammar, so a logic block (comparator, timer, latch) could simply be an addon slotted into a component
  and wired between its nodes, reading the data network for anything else ([[data]], Logic board).
