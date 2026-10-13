---
aliases:
  - "Addons"
tags:
  - item
  - conduit
---
# Addons

Small items installed into a conduit's addon slot ([[conduits]]) that change what that conduit does. The item you
carry and the installed addon are one thing. Components get addon slots later ([[component-hologram]]).

## Rules

**Status (2026-09-27):** built.

**How an addon is built (2026-09-26, slot flow 2026-09-27).** An addon only ever *adds* to the cable,
never replaces it. Every conduit carries an `AddonModule`: the addon slot. Its **Installed** field is
an addon prefab (the rocker is `RockerSwitch.prefab`, carrying `SwitchAddon`), and the module puts it
on the cable at its **Mount** when the world starts, so **nothing of it is in the scene while
editing**; a gizmo shows where it will be. `ConduitBehaviourModule` asks it questions (`Conducts`,
`ExtraSegments`, the unlock point) and stays the only writer of the tile.

The **carried item** (`Conduit Addons/Switch Addon.prefab`) is an `AddonItem`: a grab and a
rigidbody that stays where it was put until a hand first takes it. The two prefabs point at each
other: the item's **Installs** is the rocker, and the rocker's **Item** is the item.

- **Taking one out:** an open conduit's hologram shows the installed addon's item, small, in the
  addon slot. Grabbing it takes the addon off the cable; the item comes away full size in the hand.
  A plain conduit conducts, so taking an open switch out closes the line.
- **Putting one in:** let go of an item within **Install Radius** of an open, empty addon slot.
- **A newly installed switch starts open**, since that is how the scene's cold-start switch began.

**Repositionable addons (2026-09-27).** An addon with **Repositionable** ticked gets a capsule handle
in the hologram, beside the segment handles. It is not a segment: it carries no power, and moving it
is not rewiring. `ExtraSegments` is the other thing an addon can add, and the two are separate.

- **Swing** it to any free face (one no segment is on; segments cannot take its face either).
- **Twist** the wrist while holding it to turn the addon about the way it faces, in quarter turns
  with a tick, counted from how the hand was turned when it took hold. The capsule stands along the
  addon's up, so the twist reads at a glance.
- **Park** it in the middle: the addon is off entirely and tucked away, the conduit is plain cable,
  and the addon stays installed. A parked switch keeps its position for when it comes back out.
- `AddonModule` stores **Facing**, **Turns** and **Parked** as plain fields. A newly installed addon
  faces the free side nearest the player's head.

**Hologram locks** are asked, not read: `ConduitBehaviourModule.ShouldBeLocked()` is virtual and asks
the addon slot, which asks its working addon (`ConduitAddon.ShouldLockHologram()`, also virtual). A
parked addon asks for nothing, since its unlock point is tucked away with it. The upgrade slot joins
the same question when it is built, and a conduit with its own behaviour can override it and ask base
first. The unlock cube was 2.5 mm, its scale applied twice; it is 10 cm on trial.

An addon with something to press ticks **Require Hologram Unlock** and sets an **Unlock Transform**:
the conduit then opens only after a hand passes through a pulsing cube there. With no transform set,
the tick is dropped at `Awake`.
**Where an addon sits is the addon's call:** `ConduitAddon.Outward`, 0 in the middle of the cable
(a junction's box) to 1 flush with the cell face, in the panel's opening (the switch). Kept separate
from Repositionable on purpose.
**One addon per tile.**

## Taking out and putting in, networked

**Status (2026-09-29):** written, compiles clean, untested in-world.

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

## Switch

**Status (2026-09-29):** tested in-world with many players.

- **The switch** turns the conduit into a rocker switch (`RockerSwitch`). The rocker *is* the
  switch; there is nothing else to operate.

## Junction

**Status (2026-09-30):** built, untested in-world.

- **Junction +1 to +4**: one to four extra segments (3 to 6 in all), shown by a small box on the
  conduit. Splitters and mergers are gone: a junction's arrows make it a split, a merge, or
  anything else.
- On an item conduit, a junction splits the stream: [[item-conduit#Junctions|item-conduit → Junctions]].

One prefab, `Prefabs/Junction.prefab` (a small box in the middle of the cable, `JunctionAddon`), carried as
`Conduit Addons/Junction Addon.prefab`. Its **Extra Segments** (+1 to +4) is the junction's `NetworkState`,
so an installed junction's count reaches everyone in its conduit's cell, and a non-master's hand sends it
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

## Voltmeter

**Status (2026-10-07):** built, untested in-world.

**Voltmeter (written 2026-10-07, untested).** `VoltmeterAddon` on `Conduit/VoltMeterModule.prefab`
shows the watts its conduit carries (`Inflow`), scaled to three digits and a unit: 450 W, 1.2 kW,
85 MW. Over 900 GW it reads "Holy / Shit". It changes nothing about the cable and has no state
(`NetworkState` 0): every client works the reading out from the same sim. It draws through `Readout`
([[modules#Readouts|modules → Readouts]]). Conduits themselves no longer carry text; a meter is how you read one. Its item is
`Conduit Addons/Voltmeter Addon.prefab`, built like the switch's; it is addon 3 in `ConduitLayout`'s list.

## Black hole (debug)

**Status (2026-10-12):** built, untested in-world.

A debug addon that destroys every watt reaching its conduit, with no limit and no side effects. `BlackHoleAddon` on
`Conduit/BlackHoleModule.prefab` (the voltmeter's, in black) gives its conduit a sink (`ConduitAddon.Sink`, which
`ConduitBehaviourModule` hands to its tile) that asks for 10^18 W, so it draws all that arrives and passes nothing on.
Power a sink draws is not wasted, so its tile never heats or pops; cables carrying power to it still warm as cables
do. Its display counts the energy it has swallowed since it was installed (J, kJ, MJ and up), worked out on every client
from its own sim, so no state of its own: a late joiner's count starts when it joins, and taking it out and putting it
back starts it again. Its item is `Conduit Addons/Black Hole Addon.prefab`; it is addon 4 in `ConduitLayout`'s list. Sim test:
`BlackHoleSwallowsWithoutHeat`.

## Logic gates

**Status (2026-10-03):** idea.

Gates are conduit addons that decide whether power crosses from what arrives at their faces: AND, OR, NOT,
relays, thresholds. The switch is already one. Everything about them is in [[data]] (Two tiers of logic).

## Networking

**Status (2026-09-28):** written, compiles clean; switches tested in-world with many players (2026-09-29).

**Changed 2026-10-12 (Conduits V2):** an addon has no network object of its own. Which addon a conduit holds, how it
sits (facing, turns, parked) and its own `NetworkState` (a switch's closed, a junction's extra segments) are part of
its conduit's cell, in the conduit cell chunks the master writes ([[conduits#Networking|conduits → Networking]]).
`RockerSwitch` calls `SwitchAddon.Press`: the switch flips at once and its new state goes into the cell as an addon
state edit (message 8). A shut access panel refuses every other edit in its cells, but not this one, so a switch
can still be pressed through it. The presser's cell keeps its own state for 2 seconds while the master agrees.
**First upload to test: one player flips a switch, the other sees it and the grid behave the same.**

| State | Tier | Decided by | Travels as |
|---|---|---|---|
| An installed addon (switch, junction): which, facing, turns, parked, its state | fact | master | its conduit's cell, in the conduit cell chunks |
| Switch on/off | fact | master, on the presser's request | its cell's addon state; optimistic on the presser, message 8 to the master |
| An addon item's pose | simulation | whoever holds it | `NetworkRigidbody3D` + `NetworkGrabbable` |
| A loose addon item existing (taken out / put in) | fact | master | spawn / despawn via `WorldBridge` |

## Its pack: the addon pack

**Status (2026-10-10):** built, untested in-world.

For carrying addons from the dispensers to the work (a dozen trips was a dozen too many). `Prefabs/Items/Packs/Addon
Pack` (`AddonPack`, an `ItemPack`), modelled by `Blender/AddonPack.py` → `Meshes/AddonPack.fbx`: the fuel pack's base
and lid, **red** where it is blue (`AddonPackGlow`), with **eight count lamps**. Under the lid hangs a tray: **eight
places in a lattice round a solid middle block** as tall as the addons, with a red ring round its top. Each place holds
any addon item (switch, junction, voltmeter) **at full size, upright, its front facing out** so it can be read; the
corners face out diagonally. It **starts empty**. The tray hangs on long posts (2026-10-10): its lid rises 1.03 m
(`_rise`, the fuel pack's 1.08), so open, the tray's underside sits on the seam; closed, it rests 2 cm off the
bottom of the box.

- **In:** an addon item let go near an empty place in the open pack goes in, and is removed.
- **Out:** a hand taking one is given a real, spawned item, its state included (a +3 junction stays +3).
- **Contents:** each place's addon (its position in **Kinds**, at most four) and a junction's count, four bits a place,
  in one int (`ItemPack.Contents`). A loader carries it (two values per pack in its state) and the networking shares
  it: `ItemPackNetwork` writes it as a third int, so the pack has a `NetworkBridgeData4`. A pack fed by hand sends it
  to the master in **PackFed** (now three ints).
- **Getting one:** the **AddonPack Dispenser**, beside the fuel pack's. Registered on `SceneNetworking`.
