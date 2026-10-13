---
aliases:
  - "Packs"
tags:
  - item
---
# Packs

A box that carries several items of one style, so nobody makes a dozen trips. Each item's own doc describes its pack
(the fuel pack in [[fuel-rod]], the addon pack in [[addon]]); this doc is what every pack shares. Loaders take packs
whole ([[loader]]).

## Rules

**Status (2026-10-10):** refined.

- **Every style of item has a pack that carries it** (Vulps, 2026-10-11): fuel rods have the fuel pack, addons the
  addon pack. A new style of item gets its pack.
- **A pack is an item with a kind and a quantity**: a fuel rod pack holding N rods, an ammo crate holding N
  rounds. Feeding takes one unit and the quantity drops by one. Scavenged packs arrive with fewer units.
  **An empty pack stays** (changed 2026-10-08: a unit can go back in, so empty is not finished); it goes when
  thrown in the trash (a trash system comes later).
- **The rods in a pack are not networked.** The pack keeps a count and shows one model rod per unit while
  open. Grabbing one spawns a real, networked rod into that hand and the count drops by one; a rod put back
  is despawned and the count goes up. The lid is the pack's own (stock XRI: an `XRGrabInteractable` that moves only
  up and down), and others see it move.

## How a pack works

**Status (2026-10-10):** built; checked offline; grabbing and networking untested in-world.

- **`ItemPack : Item`** (`scripts/Ship`) is the abstract base; each pack is a subclass (`FuelRodItemPack`,
  `AddonPack`). It keeps which of its **Places** are filled and shows a local, never-spawned **picture** of the item
  in each (`LoadableItem.Picture`; ports ignore pictures). Its own colliders ignore each other.
- **Pictures do not interpolate** (2026-10-12, untested in-world): items in a pack were seen creeping out of their
  places on every client but the one that put them in. A picture is a kinematic copy of the item, interpolated like
  the item prefab; on any client but the holder's, Fusion moves the pack by its transform, and the interpolation kept
  writing a lagging pose back over the parent's, so each step lost a little of the pack's motion. `ItemPack.Seat` and
  `AddonPack.MakePicture` turn it off.
- **The lid** is an `XRGrabInteractable` with a `SlideGrabTransformer` (6 cm up, 6 cm down, the slot handle bar's
  settings): a tug past 80% of that starts the **Lid Track** rising (1.08 m over 2 s on the fuel pack), a push down
  on the open lid starts it sinking, and either lets go of the lid. Pictures show while the lid is off the box and
  can be grabbed once it is all the way up. The lid is run by a **`TugSlide`** on the pack (2026-10-12, untested
  in-world), shared with the S1 reactor's cartridge ([[reactor#S1 RTG|reactor → S1]]); the pack reads it and keeps its
  places and pictures.
- **Taking and putting back:** grabbing a picture empties its place and, next frame, swaps it for a spawned item in
  the same hand (as an addon is taken from a hologram); an item let go within 0.25 m of an empty place in the open
  pack is hidden, removed (`Item.Remove`) and fills the place (`LoadableItem.LetGo`).
- **Count lamps** show how many are inside, one lit per item.
- **Sounds:** `Vehicle_Car_Trunk_Open` as the lid starts to rise, `Vehicle_Car_Trunk_Close` as it starts to sink,
  from the **Lid Sound** child of the Lid Track (Environment layer, heard to 15 m); each client plays them from its
  own lid, so nothing is sent.
- **Networking:** `ItemPackNetwork` with a `NetworkBridgeData` (filled + 1, lid 1 shut / 2 open, and a pack with
  contents of its own a third int). The pack's authority writes; others read. A hand on another client changes the
  pack at once and takes the authority; until it comes, that client keeps its own state. It also strips each
  picture's `NetworkGrabbable`.

## Networking

| State | Owner | How it travels |
|---|---|---|
| A pack's kind and quantity | the pack (it comes and goes, rule 10) | its own `NetworkBridgeData`, like `AddonItemStateNetwork` |
| The pack's lid: open or shut | the hand that tugs it (it takes the pack's authority) | the pack's `NetworkBridgeData`. Every client runs the rise or fall from the flip, so all see it move |
| A rod taken from or put back in a pack | the client whose hand did it: takes the pack's authority, changes the count, spawns the rod into the hand or despawns it | the count is state; the rod is an ordinary spawn. Two hands on the last rod at once can both get one: accepted, as for two feeds at once |
