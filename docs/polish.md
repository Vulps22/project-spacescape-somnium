# Polish

**The next focus (from 2026-09-29).** Components, slots, conduits, switches and carrying work with many
players; this is what the first big test turned up, plus what was already queued. Groups and the items
in them are in alphabetical order. Each item says what was seen, what is known about why, and how its
state reaches other players (`networking.md` → Rules).

## Addons

**Written 2026-09-29, compiles clean, untested.** Needs Switch Addon added to `SceneNetworking`'s
network prefabs (a scene change, made in Unity).

- **Disappears when the hologram closes:** the fallen shown item is a child of the hologram's slot (see
  Drops). Separately, `XRGrabInteractable` put any let-go item back under the parent it had when grabbed,
  which for an item taken from the slot is that slot; `AddonItem` now turns Retain Transform Parent off.
- **Drops to the floor when put into a slot** (it installs, then the item shown in the slot falls): that
  shown item is made from the networked prefab but never spawned, and its `NetworkGrabbable` records the
  rigidbody as non-kinematic in Awake, before `Hold()` pins it; on hover, lacking authority, it sets that
  back, so the item fell as soon as the hand came near. `AddonItemNetwork` removes its `NetworkGrabbable`
  the moment it is made. (The install check itself was fine and is unchanged.)
- **Offset from the slot when the hologram reopens:** the fallen item is still the hologram slot's child,
  so it keeps the offset it fell to.
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

**Written 2026-09-29, compiles clean, sim tests pass, untested in-world.**

- **Diagnostic logging:** the `[SlideGrab]` / `[SlotHandle]` logging is gone.
- **Integrity module debug text:** the battery and reactor readouts end with two lines from their
  `IntegrityModule`: condition out of maximum with its share and state (sound, WORN, WRECKED), then the
  worn line and the chance it misfires right now (`Integrity.FailureChance`, which `FailsToWork` now
  rolls against).

## Component handling

**Written 2026-09-29, compiles clean, untested.**

- **Grab points:** `CarriedComponent` fits the grab box to the model's own meshes at start, leaving out
  text, particles and anything with a hand interaction of its own (the reactor's lever), instead of the
  slot's whole volume.
- **Placement guide:** while a held component's centre is inside an empty slot of its size, the slot shows
  its volume as a transparent box: red (neither), orange (positioned or aligned), green (both: letting go
  snaps it in), with a buzz on the holding hand as it turns green. The checks are
  `ComponentSlot.IsPositioned` and `IsAligned` (centre within 0.5 m, within 30° of square), the same ones
  `Accepts` makes. Materials `SlotGuideRed/Orange/Green`, copies of `HoloUpgrade`. Local to the holder.
- **Solid, and pushing each other:** `IgnoresPlayerBody` is on only while a hand holds the component, and
  now lets its pairs collide again when switched off, so a component on the floor stops a player. When a
  held component hits a loose one, this client asks for the loose one's authority through its
  `NetworkGrabbable`, so the shove plays out on the holder's client (the suggestion from the list, built
  as proposed; Vulps to confirm after testing).
- **Weight:** the grab is velocity tracking, so a held component is physical: it follows the hand by a share
  of the gap each step (1 at no mass, 0.15 at 2000 kg) and hangs up to 0.25 m below the hold
  (`HeftGrabTransformer`, since XRI turns gravity off while held). Masses: Reactor 2000 kg (the figure in
  `todolist.md` → Weight), Battery 400 kg. All tunable on `CarriedComponent` and each prefab's Rigidbody.

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
