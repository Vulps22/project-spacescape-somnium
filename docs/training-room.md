# Training room

The looping displays that teach the wiring and component changes. Status in `polish.md` (Tutorials).

## Phase 1 (2026-09-27)


**The #1 player complaint is not understanding how to use the wiring system.** Phase 1 (2026-09-27,
branch `training-room`): six free-standing, looping displays in a row under `TrainingRoom`, each a
conduit with ghost hands acting out one use, under a static board listing every step so a player can
take it in at a glance before trying. In teaching order:

1. **Opening a conduit:** reach in, the hologram opens, take your hand out to close it.
2. **Rewiring, two hands:** one hand holds it open; the other swings a handle to another face (the
   cable follows), then slides an arrow to flip the flow.
3. **Parking a segment:** drag a handle into the middle to park it (that side is sealed), then out to
   a face again.
4. **Installing an addon:** carry it to the addon slot and let go; grab it from the slot to take it out.
5. **Opening a switch:** pass a hand through the pulsing unlock cube, then reach in.
6. **Moving a switch:** swing the capsule handle to another face, twist the wrist to turn it, drag it
   into the middle to park it, pull it out to bring it back.

`TutorialDisplay` builds each one from the real hologram, switch and addon prefabs when the world
starts and strips out every script, interactable, joint, rigidbody and collider, so it only shows and
the grid never sees it. The ghost hands are simple shapes in `GhostHand.mat`; there is no hand model
in the project.

Next: a room built around the displays.
