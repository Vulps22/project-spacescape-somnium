# SpaceScape

## Networking

**Provisionally done (2026-09-28), untested with a second player.** Components, conduits, addons and
switches, hologram occupancy and conduit edits are networked; only the master rolls failures.
`docs/networking.md` has the rules (who owns what, state over messages), what was built step by step,
and what is left (grabbables, the interaction rework). Read it before touching anything that holds
state.

**Networking must ALWAYS be considered and discussed before adding a new feature to the world.**
Before building anything, settle with Vulps who owns its state, how that state reaches other
players and late joiners, and what a non-master's hand does to it (`docs/networking.md` → Rules).
A feature that works for one player and has not had that conversation is not ready to build.

## Where things are

- `docs/README.md`: the project, how to run the sim tests, and Editor gotchas.
- `docs/principles.md`: the design rules. Read first.
- `docs/grid.md`: slot sizes and the rules for sizing and placing components on the grid.
- `docs/networking.md`: the networking plan: authority rules, steps, and notes as we go.
- `docs/2d.md`: how a mouse-and-keyboard player does everything a VR player does. Draft.
- `docs/polish.md`: **the current focus**: what the first many-player test turned up, and why.
- `docs/todolist.md`: what is queued, and what was tried and failed (so it is not retried).
- Scene: `Assets/#User/SpaceScape/SpaceScape.unity`. Code: `Assets/#User/SpaceScape/scripts/`.
  Editor-only tools: `Packages/com.spacescape.editor` (never uploaded).
