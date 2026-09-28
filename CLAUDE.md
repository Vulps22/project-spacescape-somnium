# SpaceScape

## Next up: networking

**Networking is the next priority (decided 2026-09-27).** Players give the single-player build rave
reviews, but everything is isolated to one player, so making it multiplayer comes next, ahead of the
rest of `docs/todolist.md`.

Before starting, read `docs/todolist.md` → *Interaction rework*: several interactions keep private
state that nothing could sync, and the open questions there (how Somnium syncs an
`XRGrabInteractable`, whether the rest needs Photon Fusion) come first. Grow a Garden
(`~/Documents/project-garden`) already networks grabbables with Fusion (`NetworkObject`,
`NetworkRigidbody3D`, its own `NetworkGrabbable` and `AuthorityController`); look there before
designing anything new.

## Where things are

- `docs/README.md`: the project, how to run the sim tests, and Editor gotchas.
- `docs/principles.md`: the design rules. Read first.
- `docs/networking.md`: the networking plan: authority rules, steps, and notes as we go.
- `docs/todolist.md`: what is queued, and what was tried and failed (so it is not retried).
- Scene: `Assets/#User/SpaceScape/SpaceScape.unity`. Code: `Assets/#User/SpaceScape/scripts/`.
  Editor-only tools: `Packages/com.spacescape.editor` (never uploaded).
