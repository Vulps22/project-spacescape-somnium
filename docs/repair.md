# Repair

Restoring what heat and damage have broken: components (repair games in a component hologram), conduits,
and later the hull (`damage.md`). Not built yet.

## Repairing


A way for the crew to restore what heat has broken:

- **severed conduits:** popped is permanent today, so a blown cable can only ever be replaced, never
  mended (ties into live wiring)
- **damaged components:** `Integrity.Restore` exists in the sim but nothing in the world calls it

#### Repair games (decided 2026-10-01)

A component below its maximum integrity shows a **repair game** in its hologram. The only input is the
integrity missing; more missing means more to do. Nothing else is simulated: no invented faults.

- **Generic games, chosen per component.** Each game is its own module that knows only how much is
  missing and reports what it restored; a component picks which games it uses in the Inspector. The
  games so far:
  - **Weld the cracks:** cracks across the component's shell, traced with a fingertip (drag in 2D).
    More missing, more or longer cracks. Only on the face the hologram was opened from.
  - **Find the pairs:** coloured cells face down; click one, it flips, click another; no match and both
    flip back, a match stays up and turns emissive. More missing, more cells: 2x2, 2x3, 3x4, 4x4,
    4x5, then 5x5 with a blank centre cell (lit from the start). Each pair restores an equal share.
  - **Align the rings:** nested rings each turned out of line, twisted back until they click.
  - **Hold it steady:** keep a drifting needle in a band; more missing, longer to hold.
- **Local, like the conduit hologram's contents.** Only the player repairing sees the game. Its size
  is set from the integrity missing when it opens, and stays that size until it closes.
- **Progress is the integrity itself.** Each step (a pair matched, a crack welded) asks the master to
  `Integrity.Restore` its share; nothing else is stored. Leave halfway and the next player opens a
  smaller game.
- Undecided: a component with several games, split the missing integrity between them or pick one.

#### The component hologram

Where the repair games show: `component-hologram.md`.
