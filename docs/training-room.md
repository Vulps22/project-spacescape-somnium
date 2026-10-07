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

## Phase 2: a private, voiced tutorial (idea, 2026-10-07; not until near the end of development)

**The picture.** A new player arrives in a **sub-sim that runs only on their client**: a small ship section
of its own, with its own grid, components, conduits and items. Other players' avatars and voices are hidden
and muted for them, so it feels single-player. A **voice** tells them what to do (open a conduit, rewire it,
install an addon, swap a component) and **comments on what they actually do** ("notice the cable turned
red"). When it is done, every restriction is undone and the player is dropped at **0,0,0 of the floating
origin** (not built yet) into the shared ship.

**What already supports it:**
- **Local-only sim:** every system already decides for itself with no network (`Decides = true`, local
  `Instantiate`), which is how the Editor runs; `TutorialDisplay` already builds real prefabs stripped of
  their networking.
- **Steps and observations:** the events exist (a hologram opening, an addon installed, a switch flipped, a
  component pulled, a conduit's heat band changing). A tutorial script is a list of steps, each a clip and
  the event that moves it on; an observation is a clip tied to a condition.
- **Finishing:** one teleport once the floating origin exists, then lifting the restrictions.

**The work:**
- **More than one grid.** `PowerGrid.Current` is a static singleton, assumed by readouts, conduits,
  `ConduitLayout`, `Readout` and others (`TargetField.Current` likewise). The sub-sim needs a grid of its
  own that the ship's networking never sees: no chunks, slot networks or panel bits from it, and none of its
  items carrying a `NetworkObject`.

**Unknown: test in-world before anything is built**, since the SDK exposes none of it:
1. **Hiding other players and muting their voices locally.** There is no SDK call for avatar visibility or
   voice volume. Switching off remote avatar renderers (`somnium-avatar-layers`: PlayerTrigger on Triggers)
   and Photon Voice speakers on this client may work, or Somnium may block or undo it.
2. **Knowing a player is new.** There is no storage API in the SDK, so it is unknown whether a world can
   remember a player between visits. If not: offer the tutorial to everyone on arrival, with a skip.

**Things to design round:**
- **Everyone else still sees the tutorial player.** Somnium sends their avatar's position, so they stand in
  the tutorial space reaching at nothing. Put the tutorial somewhere out of the way.
- **The first player in an empty instance is new by definition, and is also the master.** Their client keeps
  doing the ship's master duties (failure rolls, spawning, panels) while they are in the tutorial; the ship
  must not need them to be present.

**Order, when it starts:** the two in-world tests, then more than one grid, then the tutorial script and its
voice lines.
