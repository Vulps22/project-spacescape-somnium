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

### Junction addon (written 2026-09-30, compiles clean, untested)

One prefab, `Prefabs/Junction.prefab` (a small box in the middle of the cable, `JunctionAddon`), carried as
`Conduit Addons/Junction Addon.prefab`. Its **Extra Segments** (+1 to +4) is the junction's `NetworkState`,
so an installed junction's count reaches everyone through `AddonNetwork`, and a non-master's hand sends it
with the edit. Taken out, the item keeps the count (`AddonItem.State`, shown on its screen as "+N") and
gives it to the junction it installs. The item syncs it through `AddonItemStateNetwork` (a
`NetworkBridgeData1`, written by whoever has the item's authority). An item's own **State** in the Inspector
sets the count for an item placed in the scene; -1 takes the junction prefab's. Only addons that say so
(`StateTravelsWithItem`) carry state on their item, so a switch still goes in as its prefab has it.

- **Drawn junctions start at the prefab's count.** A drawn conduit's code has no room left for a count.
- **First test, 2026-09-30 (fixed, untested):**
  - *New handles could not be seen:* they spawned parked, in the middle, inside the junction's box. Every
    addon's model now hides while its conduit's hologram is open, here or for another player
    (`AddonModule.Hidden`, set from `ConduitHolograms`' own and networked occupancy), so nobody works a switch
    mid-configure either.
  - *A third extra segment "moved" an original:* the conduit had 4 cable stubs, so a fifth face went undrawn
    and outputs, drawn first, pushed an input off the end. `Conduit` now adds stubs in Play mode as needed.
  - *Could not use Y+:* a new addon faces the free side nearest the head, and the hologram kept segments off
    the addon's face. Only an addon with a capsule handle (a switch) now holds its face.
- **Taking a junction out parks every segment (decided 2026-09-30).** Nothing says which faces were the
  extra segments', so all of them park and the conduit is rewired from nothing, through the hologram's
  `Commit`, which also sends the edit to the master.

## Clean-up and debug

**Written 2026-09-29, compiles clean, sim tests pass, untested in-world.**

- **Diagnostic logging:** the `[SlideGrab]` / `[SlotHandle]` logging is gone.
- **Integrity module debug text:** the battery and reactor readouts end with two lines from their
  `IntegrityModule`: condition out of maximum with its share and state (sound, WORN, WRECKED), then the
  worn line and the chance it misfires right now (`Integrity.FailureChance`, which `FailsToWork` now
  rolls against).

## Component handling

**Written 2026-09-29, compiles clean, untested.**

- **Grab box bigger than the reactor (bug, seen 2026-09-29).** Slotted, you cannot press up against the
  reactor's side, and the box gets in the way of grabbing the rod lever. Likely why: the fit leaves out
  meshes under an object with its own hand interaction, but the lever's interactable is only on
  `RodLever/Pivot/Handle`; `RodLever/Pivot/Arm` is outside it, so the box stretches out over the lever.
  Fix: see the next item; the box goes.
- **Components use their mesh colliders, not a box (Vulps, 2026-09-29).** The grab and the physics use
  colliders on the model's own meshes, so what you touch and bump into is the shape you see. Leave out
  the lever and anything else handled on its own; the grab interactable's collider list is these mesh
  colliders. Unity only lets a moving (non-kinematic) Rigidbody use convex mesh colliders, so each mesh
  collider is convex; a shape a single convex hull misrepresents is split into convex parts.
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

**Written 2026-09-29, compiles clean, sim tests pass (278), untested in-world.**

- **A loose component keeps running (decided 2026-09-29).** Taken out of its slot, a component gets a node
  of its own with no conduits (`PowerGrid.Loosen`), starting at the heat it carried out, and gives it back
  when it goes into a slot (`Unloosen`, keeping its heat; the grid gained `PowerGraph.RemoveNode`). Its
  controls stay live: a running reactor keeps its rods up, keeps producing, has nowhere to send it and
  heats up (sim: 100 W wasted, 271 C after a minute), and shuts down when its rods go in. Every client does
  this, as everyone simulates the grid; the component's own data corrects it.
- **Pulled out live, 10% of maximum integrity lost** (`grid.md` build order 5). A hand taking a component
  out while power is flowing through it (arriving, produced or drawn) costs it 10% of its maximum
  (`ComponentSlot`, tunable). Only where the lock is decided (the master), for a hand there or on another
  client, so it is taken once; the component's data carries the new condition to everyone.

## Networking left over

- **Addon item taken out:** done with Addons.
- **Slot handle bar motion.** The unlock is networked; the bar moving is not, so others do not see it
  pulled (visible motion is networked). Planned: the reactor dial pattern through the slot's data.

## Tutorials

- **Pedestal copies must be totally inert.** Done by hand in the scene (Vulps, 2026-09-30). Note for
  later: each copy is switched off one override at a time, so anything added to the component prefabs
  later arrives switched on in the copies; check them after changing a prefab.
- **Changing a component (written 2026-09-30, compiles clean, untested).** `SlotTutorialDisplay`: two
  shrunk copies of a real slot prefab side by side, one holding a real component. A ghost hand pulls the
  bar up until it turns green, takes the component out, carries it to the empty slot and lets go once the
  placement guide turns green. The guide is coloured by the slot's own rules (`SeatDistance`, `SeatAngle`)
  as the component moves. Next loop it goes back. Built and stripped the same way as `TutorialDisplay`, which
  now shares `TutorialParts` (the strip, easing, `GhostHand`) with it. Networking: none; it holds no state.
  Placed as `Tutorial - Changing a Component`, front row after Moving a Switch.
- **Slots pedestal (placed 2026-09-30, untested):** `Tutorial - Slots`, back row after Batteries: a Slot S1
  with a battery seated, and a board. Its copies have every script, collider, rigidbody and `NetworkObject`
  deleted, not switched off.
- **Switched-off copies are still found (seen 2026-09-30).** `PowerGrid.Build` finds switched-off
  `GridNode`s and `ComponentSlot`s, and `SceneNetworking` registers every `NetworkObject`, even switched-off
  or inactive ones. So the Batteries and Reactors pedestal copies register on the network. Rebuild them the
  way the Slots pedestal's copies were made.

## For Vulps

- **Revisit the existing wall** (2026-09-29). A battery intersects it and cannot be put back once it is
  unlocked. Fix: build a slot into the wall; Vulps is doing it.
- **A tutorial is in the wrong place** (seen 2026-10-01). Vulps is moving it.
- **Some tutorial models are grabbable** (seen 2026-10-01). Pedestal copies must be totally inert
  (Tutorials); Vulps is clearing them by hand.

## Done

- 2026-09-29: battery debug text restored (Vulps). Conduits pop.
