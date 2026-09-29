# Polish

**The next focus (from 2026-09-29).** Components, slots, conduits, switches and carrying work with many
players; this is what the first big test turned up, plus what was already queued. Groups and the items
in them are in alphabetical order. Each item says what was seen, what is known about why, and how its
state reaches other players (`networking.md` → Rules).

## Addons

**Written 2026-09-29, compiles clean, untested.** Needs Switch Addon added to `SceneNetworking`'s
network prefabs (a scene change, made in Unity).

- **Disappears when the hologram closes:** `XRGrabInteractable` put a let-go item back under the parent
  it had when grabbed, and the item shown in the hologram's addon slot started under that slot. `AddonItem`
  now turns Retain Transform Parent off.
- **Drops to the floor when put into a slot:** it only installed within 0.1 m of the slot's centre, far
  tighter than the conduit's cell it shrinks in. Let go anywhere in the open conduit's cell now installs.
- **Offset from the slot when the hologram reopens:** the same re-parenting as above.
- **Shrinking is not networked:** the Switch Addon's `NetworkRigidbody3D` now syncs scale, and only
  whoever holds an item (or has its authority once it is let go) resizes it.
- **Size: a fixed full size.** Full size is the prefab's own scale (0.25 for the Switch Addon), and the
  item shrinks from that to the slot's size. The shown item no longer measures itself against the slot.
- **Taken out of the slot, it neither grows back nor falls:** the item shown in the slot is a local
  picture that was never on the network. When a hand lifts it out, it is swapped the next frame for a
  spawned item of the same prefab, at the same place and size, in the same hand (`AddonItemNetwork`); an
  item let go anywhere but a slot grows back to full size. An installed item is despawned for everyone
  instead of destroyed on one client. The picture has its `NetworkGrabbable` removed, which ends the grab
  errors in the log.

## Clean-up and debug

- **Diagnostic logging:** remove `[SlideGrab]` / `[SlotHandle]` now that the handle works.
- **Integrity module debug text:** a readout on `IntegrityModule`, like the battery's, showing what it
  knows (condition, maximum, damage taken).

## Component handling

- **Grab points do not line up with what you see.** Players have to get very close; on the S5 reactor
  so close the laser starts inside it. Likely why: the box collider fills the slot's whole volume (5 m
  for S5) while the model is smaller, so the box's surface floats outside the model. Size the collider
  from the model's renderers instead.
- **Placement guide.** While a held component is inside a slot's volume, the slot shows a transparent
  box: red, neither aligned nor positioned; orange, one of the two; green, both, and letting go snaps it
  in. A short vibration on the holding hand when it turns green. The checks are the ones
  `ComponentSlot.Accepts` already makes (centre within 0.5 m, within 30° of square). Networking: local to
  the holder; it is feedback for the hand doing the placing, not state.
- **Solid, and pushing each other.** A component not being carried should stop a player; a heavier one
  should shove a lighter one (a reactor swung into a battery sends it flying). Why they are not:
  `IgnoresPlayerBody` is on all the time; it should apply only while a hand holds it. Mass drives the
  shoving once set (see Weight). Networking, to settle with Vulps before building: each loose
  component's physics runs on whoever has its state authority, so two components owned by different
  players only push each other properly on one side. Suggested: a held component takes authority over
  whatever it hits.
- **Weight.** Not heavy enough to need several players yet (that is `todolist.md` → Weight), but
  sluggish: it lags the hand and sags a little when held. Each component gets a mass, a battery lighter
  than a reactor. It is weightless now because the grab is XRI's Instantaneous movement, which ignores
  mass; velocity tracking with damping, or smoothing plus a downward offset scaled by mass, gives lag and
  sag.

## Component simulation

- **A loose component keeps running (decided 2026-09-29).** It behaves as a component with nothing
  connected: a reactor taken out while running keeps producing, has nowhere to send it, and heats up, so
  the crew have to shut it down before pulling it. Its controls stay live the whole time. Seen: a pulled
  reactor's lamp kept spinning and its readout kept its wattage, but the temperature did not rise and the
  rods did not answer the lever. Why: `PowerGrid` leaves every unslotted component out of the grid, so
  nothing ticks its source; the lamp and readout show the last values, and the lever moves rods on a
  core that is no longer simulated. Fix: a loose component gets a node of its own with no edges, so it
  goes on simulating; `Remove` stops unbinding it. Networking: nothing new; its state already travels
  in its own `ComponentNetwork` data, and every client simulates it.
- **Pulled out live, 10% of maximum integrity lost** (`grid.md` build order 5). Not built.

## Networking left over

- **Addon item taken out:** done with Addons.
- **Slot handle bar motion.** The unlock is networked; the bar moving is not, so others do not see it
  pulled (visible motion is networked). Planned: the reactor dial pattern through the slot's data.

## Tutorials

- **Pedestal copies must be totally inert.** Seen: they can be grabbed, the reactor copy's lever works,
  and their ports render wrongly at the shrunk size. Why: each copy switches its working parts off one
  override at a time, so anything added to the prefab later arrives switched on (`PortVisual` never was
  off). The overrides written for the carrying parts were lost: the scene file was edited on disk while
  it was open in Unity, and Unity saved its own copy over it. Fix: one "display copy" script on each copy
  that, on Awake, switches off every behaviour, collider and interactable under it except what draws it,
  so new parts can never leak through again.
- **Slots tutorial (new):** taking a component out of a slot and putting one in, in the style of the
  existing tutorials (a looping display with ghost hands, or a static board of steps): pull the handle up
  until the bar turns green, take the component, carry it, line it up with an empty slot of its size,
  let go and it snaps in and locks. It should show the placement guide once that exists. Networking:
  none; like the other tutorial displays it runs the same on every client and holds no state.

## Done

- 2026-09-29: battery debug text restored (Vulps). Conduits pop.
