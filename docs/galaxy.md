---
aliases:
  - "The galaxy"
tags:
  - game
---
# The galaxy

Where the ship flies: one procedurally generated galaxy, and how hard it is depends on how deep in you are. Where it
sits in the game is [[the-loop]]; how the crew find their way through it is [[navigation]].

## The galaxy

**Status (2026-09-17):** decided; nothing built.

- **Procedurally generated**, so every distance is already decided before anyone looks at it.
- **Seeded from the timestamp of midnight on the current day.** The seed is set when the first player
  joins and replicated to every subsequent player on join. The galaxy therefore reseeds daily.
- **System locations are known from the start.** The map is not fogged.
- **One ship, one galaxy.** No instancing, no per-crew vessels, no server-synchronised parallel
  worlds — there are not enough players in Somnium for that to be worth the architecture.

## Difficulty is depth

**Status (2026-09-17):** decided; nothing built.

**Decided.** Difficulty is distance from the galactic centre:

- the deeper in, the more jumps between stations
- the closer to the centre, the harder the enemies

Missions are **finite**, and a **hidden completion level** decides how close to the centre newly
spawned contracts will send the crew.

Why depth is the right axis for this game specifically: more jumps between stations is not "harder
enemies", it is **longer since you could stop and fix anything.** The difficulty curve is accumulated
entropy without a maintenance window, which is exactly the curve a machine-entropy game wants, and it
falls straight out of the map.


> [!question] Open
> **Does the hidden completion level reseed with the galaxy at midnight?** Daily reset means every session starts at
> the rim and nobody ever sees the centre. Persistent completion means tomorrow's crew inherits today's difficulty
> *and* a galaxy they have never mapped.

## Tried and rejected

- **Multiple ships / a station as lobby.** Twenty players as four crews of five, with per-ship progression, or
  instances synchronised through a server so players see each other across worlds. One ship, one galaxy: there are
  not enough players in Somnium to justify the architecture.
