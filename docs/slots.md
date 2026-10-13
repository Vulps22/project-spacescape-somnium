---
aliases:
  - "Slot sizes"
tags:
  - structure
---
# Slot sizes

The six slot sizes, S0 to S5: what each is for, its shape and size, and the rules that come with it. Each
component's doc says which sizes it comes in. Guns go in their own family on the hull, G1 to G5 ([[#G slots]]).
How a slot behaves (the thin wrapper, its front, moving a component, networking) is in [[grid]].

**Decided 2026-09-28** (S1 to S5), **2026-10-10** (S0).

| Size | Shape | Cells (x × y × z) | Metres | In the scene |
|---|---|---|---|---|
| [[#S0]] | single cell | 1 × 1 × 1 | 0.5 × 0.5 × 0.5 | 0 |
| [[#S1]] | cube | 4 × 4 × 4 | 2 × 2 × 2 | 6 |
| [[#S2]] | rectangular | 6 × 4 × 4 | 3 × 2 × 2 | 1 |
| [[#S3]] | cube | 8 × 8 × 8 | 4 × 4 × 4 | 5 |
| [[#S4]] | rectangular | 10 × 8 × 8 | 5 × 4 × 4 | 1 |
| [[#S5]] | cube | 10 × 10 × 10 | 5 × 5 × 5 | 3 |

"In the scene" counts the `Slot Sx` instances in `SpaceScape.unity` (2026-10-10), tutorial pedestals included.

## Rules for every size

- **Odd-numbered sizes are cubes; even-numbered ones are rectangular**, with y and z two cells shorter
  than x. Two, not one: every dimension stays even, so a slot's centre is always a grid corner and a slot
  lands on whole cells whichever way it faces. [[#S0]] is the exception.
- **Only an Sx component fits an Sx slot.**
- **The finished ship has more slots than it needs,** many small and very few large, so a crew chops
  and changes to suit what it is doing.
- **One scale of energy** (Vulps, 2026-10-11): joules and watts mean the same on every part, whatever its size.
  What a thing does sets its cost, never its size: a small part can feed a large one.
- **Size buys capability and efficiency:** bigger parts do more (longer jumps, faster rates) and **waste less doing
  it**; smaller parts are less efficient for their size, as real engines and plants are. A check when balancing a
  new part, not the rule: how many same-size batteries does it need?
- **Each size is its own machine:** a component's S2 is not a shrunken S5 ([[component-models#Rules (decided with Vulps, 2026-10-09 and 10)|component-models → Rules]]).
- **In code:** `SlotSize` and `ComponentSlot.CellsOf` (`scripts/Ship/Slots/ComponentSlot.cs`); one slot prefab per
  size, `Prefabs/Slots/Slot S1` to `Slot S5`.
- **An empty slot shows a plinth with its size on top** (Vulps, 2026-10-11), so a slot reads at a glance: that it is
  there, and what size it takes. ^empty-slot-plinth
  - **The plinth** fills the slot's footprint, half a cell tall like a component's own plinth, gunmetal, with the
    size (`S2`) engraved into the top and inlaid amber, centred, reading from the slot's front, filling 70% of the top
    across or deep, whichever is smaller. Source Sans Pro Black.
  - **Hidden while a component is installed,** and back when it is taken out: `ComponentSlot._plinth`, a renderer
    turned off (`forceRenderingOff`) from `Install` and `Remove`. While editing, it is hidden on a slot that starts
    with a component, so it never fights that component's preview. Not saved, so editing never dirties the scene.
  - **Networking:** nothing new is sent. It follows the installed component, which every client already applies
    through `Install` and `Remove` (`SlotNetwork`).
  - **Model:** `Blender/Slots/SlotS1.py` and so on, one per slot prefab, sharing `slot_parts.py`; exported to
    `Meshes/SlotS1.fbx` and so on, its materials remapped like the loaders'. No collider, so it never gets in the
    way of placing a component.

## S0

**Status (2026-10-11):** decided; code compiled; `Slot S0` prefab built, untested in-world.

A single cell, for small, simple things.

- **1 × 1 × 1 cells** (0.5 m cube).
- **One cell, so neither rectangular nor anchored on a grid corner:** its centre is its cell's centre
  (`ComponentSlot.NearestAnchor`).
- **By convention, not rule: in or on a wall,** most likely in an alcove, not behind a panel.
- **In code:** `SlotSize.S0`, one cell in `CellsOf`, snapped to a cell centre while editing.
- **Prefab:** `Prefabs/Slots/Slot S0`, a variant of `Slot` like the other sizes (2026-10-11), its grab handle scaled
  to 0.6 to suit a half-metre slot.
- **Plinth** ([[#^empty-slot-plinth|empty-slot plinth]]): `Meshes/SlotS0.fbx`, on `Slot S0` as its `Plinth` child
  (2026-10-11); untested in-world. A plate 0.08 m thick rather than half a cell, which would fill half the slot.

## S1

**Status (2026-09-28):** built, tested in-world.

- **4 × 4 × 4 cells** (a 2 m cube).
- **Plinth** ([[#^empty-slot-plinth|empty-slot plinth]]): `Meshes/SlotS1.fbx`, on `Slot S1` as its `Plinth`
  child (2026-10-11); untested in-world.

## S2

**Status (2026-09-28):** built, tested in-world.

- **6 × 4 × 4 cells, long on x** (3 × 2 × 2 m).
- **Plinth** ([[#^empty-slot-plinth|empty-slot plinth]]): `Meshes/SlotS2.fbx`, on `Slot S2` as its `Plinth`
  child (2026-10-11); untested in-world.

## S3

**Status (2026-09-28):** built, tested in-world.

- **8 × 8 × 8 cells** (a 4 m cube).
- **Plinth** ([[#^empty-slot-plinth|empty-slot plinth]]): `Meshes/SlotS3.fbx`, on `Slot S3` as its `Plinth`
  child (2026-10-11); untested in-world.

## S4

**Status (2026-09-28):** built, tested in-world.

**There is exactly one S4 slot on the ship.** In honour of a numbering slip, S4 components are the rarest and most
powerful in the game, whatever they are: a gun that is overpowered, a reactor that practically never runs dry, a
radar that maps the galaxy in one pass. Examples, not decisions. That S4 is smaller than S5 makes no sense, and that
is fine.

- **10 × 8 × 8 cells, long on x** (5 × 4 × 4 m).
- **Plinth** ([[#^empty-slot-plinth|empty-slot plinth]]): `Meshes/SlotS4.fbx`, on `Slot S4` as its `Plinth`
  child (2026-10-11); untested in-world.
- **One can drop anywhere** (Vulps, 2026-10-10): in the first system or the billionth, unlike everything else, whose
  difficulty follows depth ([[galaxy#Difficulty is depth|galaxy → Difficulty is depth]]).
- S4 components are ship parts from a civilisation that died millenia before Hominids even evolved. Their technology is so advanced they've become highly sought after throughout the galaxy and are considered "god-tier" by some civilisations. If a human S5 battery can store 500 MJ... an S4 battery can store 10 GJ

## S5

**Status (2026-09-28):** built, tested in-world.

- **10 × 10 × 10 cells** (a 5 m cube). The endgame size.
- **Plinth** ([[#^empty-slot-plinth|empty-slot plinth]]): `Meshes/SlotS5.fbx`, on `Slot S5` as its `Plinth`
  child (2026-10-11); untested in-world.


## G slots

**Status (2026-10-13):** decided (Vulps); not built.

**G is for guns, and only guns.** Every other component goes in an S slot anywhere on board; a gun has to be on the
hull, because it needs the outside and because physical weapons need a shot to leave where it was loaded. So guns
are G-sized components, not S-sized, and G slots are placed around the hull.

- **Sizes:** G1, G2, G3, G4 and G5, the same cells as their S counterparts (`SlotSize`), the G marked by the slot being
  a gun slot. **One G4** on the ship, as for the [[#S4|S4]]: the rarest, most powerful guns.
- **Only a gun fits a G slot, and a gun fits only a G slot.** In code, the fits check
  (`ComponentSlot`, `slotted.Size == _size`) gains the match `slotted.IsGun == _isGunSlot`, so it works both ways:
  no gun goes into an S slot inside the ship, and nothing else goes into a G slot. Not changed yet: the G slot's
  models come first.

### The empty G slot

The G slot's equivalent of the [[#^empty-slot-plinth|empty-slot plinth]], so a G slot reads at a glance as a gun slot
and which size it is.

- **A black box in the hull:** a sheet-metal box with a horizontal oval window, yellow hazard stripes, and `Gx`
  (`G3` and so on) painted on its side and front to identify it.
- **The hull is not boxy:** the box is surrounded by walls sloping inward. The hull itself is designed in the ship
  construction phase, drawing on Star Citizen.

### With a gun installed

The outer hull and the box are replaced by the gun and an airtight framing for the gun's controller. Taking the gun
out brings the box and the hull back. The same swap as the S slots' plinth (`ComponentSlot._plinth`, hidden on
`Install` and shown on `Remove`), so nothing new is sent: it follows the installed component on every client.
