# Polish

**The next focus (from 2026-09-29).** Components, slots, conduits, switches and carrying work with many
players; this is what the first big test turned up, plus what was already queued. Each item says what was
seen, what is known about why, and how its state reaches other players (`networking.md` → Rules).

## Components

**A loose component keeps running (decided 2026-09-29).** It behaves as a component with nothing
connected: a reactor taken out while running keeps producing, has nowhere to send it, and heats up, so
the crew have to shut it down before pulling it. Its controls stay live the whole time.
- Seen: a pulled reactor's lamp kept spinning and its readout kept its wattage, but the temperature did
  not rise and the rods did not answer the lever.
- Why: `PowerGrid` leaves every unslotted component out of the grid, so nothing ticks its source. The
  lamp and readout show the last values; the lever moves rods on a core that is no longer simulated.
- Fix: a loose component gets a node of its own with no edges (the one it had in its slot, carried with
  it, or a new one), so it goes on simulating; `Remove` stops unbinding it.
- Networking: nothing new. Its state already travels in its own `ComponentNetwork` data, and every
  client simulates it.

**Weight.** Not heavy enough to need several players yet (that is `todolist.md` → Weight), but sluggish:
it lags the hand and sags a little when held.
- Why it is weightless now: the grab is XRI's Instantaneous movement, which ignores mass. Velocity
  tracking with damping, or smoothing plus a downward offset scaled by mass, gives lag and sag.
- Each component gets a mass (a battery lighter than a reactor).

**Grab points do not line up with what you see.** Players have to get very close; on the S5 reactor so
close the laser starts inside it.
- Likely why: the new box collider fills the slot's whole volume (5 m for S5) while the model is smaller,
  so the box's surface floats outside the model. Size the collider from the model's renderers instead.

**Loose components are solid and push each other.** One not being carried should stop a player; a
heavier one should shove a lighter one (a reactor swung into a battery sends it flying).
- Why they are not: `IgnoresPlayerBody` is on the component all the time. It should only apply while a
  hand holds it (that is what it is for); on the floor it should block the player.
- Mass drives the shoving once it is set (see Weight).
- Networking: each loose component's physics runs on whoever has its state authority, so two
  components owned by different players only push each other properly on one side. Settle with Vulps
  before building: likely a component takes authority over whatever it hits while it is held.

**Placement guide.** While a held component is inside a slot's volume, the slot shows a transparent box:
- red: neither aligned nor positioned
- orange: aligned or positioned
- green: aligned and positioned; letting go snaps it in

plus a short vibration on the holding hand when it turns green. The checks are the ones
`ComponentSlot.Accepts` already makes (centre within 0.5 m, within 30° of square).
- Networking: local to the holder. It is feedback for the hand doing the placing; nothing about it is
  state.

**Pulled out live, 10% of maximum integrity lost** (`grid.md` build order 5). Not built.

**Integrity module debug text.** A debug readout on `IntegrityModule`, like the battery's, showing what it
knows (condition, maximum, damage taken).

## Tutorial pedestals

**The copies on the pedestals must be totally inert.** Seen: they can be grabbed, the reactor copy's
lever works, and their ports render wrongly at the shrunk size.
- Why: each copy switches its working parts off one override at a time, and anything added to the
  prefab later arrives switched on (the carrying parts did, and `PortVisual` never was off).
- Fix: one "display copy" script on each copy that, on Awake, switches off every behaviour, collider and
  interactable under it except what draws it, so new parts can never leak through again.

## Addons

- **Shrinking into the slot is not networked.** Others see it at full size. Its size must reach them.
- **Put into a slot, it drops to the floor.** It should install.
- **Dropped like that, it disappears when the hologram closes.**
- **When the hologram reopens it is shown fixed, offset from the slot.**
- **Taken out of the slot, it does not grow back and does not fall.**

What is known: `XRGrabInteractable` unparents whatever it grabs and, with Retain Transform Parent on,
puts it back under its old parent on release. The item shown in the hologram's addon slot starts as a
child of that scaled slot, so on release it goes back under it: that fits disappearing with the hologram,
the fixed offset, and sizes coming out wrong (`AddonItem` writes local scale). The same item is a local
copy of a networked prefab that is never spawned, so its `NetworkGrabbable` throws on grab (5 times in
the 2026-09-29 log) and nobody else sees it held.

The size fix itself: grow to a fixed size of 1 and shrink from that to the slot's size, instead of
whatever scale the item happened to have in `Awake`.

## Networking left over

- **The slot handle bar's motion.** The unlock is networked; the bar moving is not, so others do not see
  it pulled (visible motion is networked). Planned: the reactor dial pattern through the slot's data.
- **A taken-out addon item** (see Addons): spawned, like a component, so it exists for everyone.

## Clean-up

- Remove the `[SlideGrab]` / `[SlotHandle]` diagnostic logging now that the handle works.

## Done

- 2026-09-29: battery debug text restored (Vulps). Conduits pop.
