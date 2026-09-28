# SpaceScape

## Networking

**Provisionally done (2026-09-28), untested with a second player.** Components, conduits, addons and
switches, hologram occupancy and conduit edits are networked; only the master rolls failures.
`docs/networking.md` has the rules (who owns what, state over messages), what was built step by step,
and what is left (grabbables, the interaction rework). Read it before touching anything that holds
state.

## Where things are

- `docs/README.md`: the project, how to run the sim tests, and Editor gotchas.
- `docs/principles.md`: the design rules. Read first.
- `docs/networking.md`: the networking plan: authority rules, steps, and notes as we go.
- `docs/todolist.md`: what is queued, and what was tried and failed (so it is not retried).
- Scene: `Assets/#User/SpaceScape/SpaceScape.unity`. Code: `Assets/#User/SpaceScape/scripts/`.
  Editor-only tools: `Packages/com.spacescape.editor` (never uploaded).
