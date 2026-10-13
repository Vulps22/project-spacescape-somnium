---
aliases:
  - "Project SpaceScape"
tags:
  - index
---
# Project SpaceScape

Co-op ship management. VR-first, 2D supported. **One ship, one galaxy, unlimited crew.**

Committed 2026-09-17. Each doc carries the status of each of its parts.

Scene: `Assets/#User/SpaceScape/SpaceScape.unity`. Its own project at `~/Documents/project-spacescape-somnium`
since 2026-09-26, split out of Project Garden's working tree.

## The pitch

A crew runs courier contracts between stations across a largely empty procedural galaxy. The ship is
not a horseshoe of minigame consoles sharing an HP bar — it is a **machine**. Power flows through
conduits the crew physically reconfigures, heat accumulates wherever power is wasted, conduits blow,
components wear out, and every functional part is hot-swappable.

The reference point is **Barotrauma's submarine, not Star Trek Bridge Crew's bridge.** Barotrauma is
the only game in the genre where the ship is a diagnosable machine, and it is 2D. Nobody has built
one in 3D, and nobody has built one in VR.

## Why it is worth building

**One abstraction produces the simulation, the progression, the damage model, the diagnosis gameplay
and the upgrade system.** Every mechanic designed so far — heat, accumulators, switches, splitters,
faults, durability — slotted into the tile graph without needing a new rule. The two that looked like
they needed one (splitters, and a short circuit starving its siblings) turned out to be a tile with a
second face and an edge with a higher share.


## The MVP

**The current scene, in VR, interactable.** The wiring stays fixed; what a hand can do is operate
what is already there. Rewiring the ship by hand comes after ([[modules#Rewiring live — after the MVP|modules → Rewiring live]]).

## Documents

Every doc is tagged by the kinds of thing it defines; `Docs.base` lists them by kind. Start with [[principles]] (the
design rules), then the planning docs: [[todolist]], [[polish]], [[the-loop]], [[flight]].

- **Components:** [[battery]], [[reactor]], [[jump-drive]], [[radar]], [[weapons]], [[loader]].
- **Conduits:** [[conduits]] (the shared core), [[item-conduit]], [[addon]].
- **Items:** [[fuel-rod]], [[packs]], [[addon]].
- **Structure:** [[grid]], [[slots]], [[ship-structure]].
- **Systems:** [[power]], [[data]], [[damage]].
- **Mechanics:** [[loading]], [[repair]], [[navigation]].
- **Interaction:** [[interaction]], [[2d]], [[component-hologram]], [[training-room]].
- **Game:** [[galaxy]]. **Easter eggs:** [[felix]].
- **Tech and art:** [[modules]], [[networking]], [[tooling]], [[component-models]].
- **Reference:** [[assets]], [[credits]].

## Repo

Its own project, copied from Project Garden's Somnium template checkout with Garden's code, art
packs, docs and history removed. The scene depends on nothing outside `Assets/#User/SpaceScape/` but
TextMesh Pro. `Packages/com.somnilux.sdk` (the Linux upload workarounds) came with it and still uses
`GrowAGarden.*` namespaces; renaming them belongs in Somnilux's own repo.
