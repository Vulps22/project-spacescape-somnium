# Component models

**Decided 2026-10-09: every component's model is replaced with one in the loader's look. This is the next
piece of work, ahead of everything except a major bug.** Not started.

## Why

The S2 loader (`loading.md`) set a look that is nicer than the current models and gives far more creative
freedom. The current components come from three different sources, with three palettes and three levels of
detail. Next to the loader they don't belong to one ship:

- **Reactor:** a detailed store-bought look: a blue ribbed dome, tan and brown panels, speckled textures.
  Rounded, cartoon sci-fi. The furthest from the loader.
- **Battery:** pale grey boxes with cyan slits. Placeholder "Sample text" labels show on the top edge.
- **Radar:** a plain block with a cluster of skyscraper-like columns on top and a screen in front. It doesn't
  read as a radar.
- **Disruptor:** a flat hazard-striped slab with a small barrel. It reads as a placeholder.

Modelled by script in Blender (as `Blender/Loader.py`, `FuelPack.py`, `ItemConduit.py`), every component fits the
grid exactly, and its ports and controls go where we put them.

## The look

- **One material set:** gunmetal (`FuelPackMetal`), near-black (`FuelPackDark`), **amber** (`ComponentItem`)
  wherever a hand goes or a status shows, **blue glow** for energy, and glass where you watch something work.
- **Every component is a frame, a core and a control panel:**
  - **Frame:** exposed and braced, like the loader's rig (the frames of Satisfactory's miners).
  - **Core:** shows what the component is doing. **The model is the readout.**
  - **Control panel:** dark, with amber trim, holding the levers, buttons and screens.
- Flat-shaded and angular, chamfered edges, no textures. Interest comes from glow and moving parts.
- **Every component has an I/O panel** (decided 2026-10-09): a block from the model's base out to the slot's edge,
  three cells wide and one tall, carrying In, Data and Out, with Data between them. **Assume every component has, or
  will have, a data port**, and model for it now.
- **Models face +Z, with +Y up** (a precedent, not a hard rule; Vulps, 2026-10-09). The control panel or working face
  is on +Z, the prefab's Front is ZPlus and Up is YPlus, and the model sits unrotated under the prefab root, so a
  replacement model drops in without turning. It matches the slots' frame and Unity's forward, and on the
  rectangular sizes (long on x) it puts the front on a long side. Follow it where it fits and roll it out as models
  are replaced; a component that is better another way can be, with its Front set to match. Every component prefab
  already declares ZPlus; the Reactor did not (its old display was on -X) until its new model went in.

## Ideas per component

- **Battery (S1):**
  - A four-post frame around a 2×2 stack of tall hex cells, chunkier cousins of the fuel rod, in glass
    sleeves.
  - Each cell lights from the bottom up with charge, so the charge can be read from across the room.
  - Heavy bus bars run from the cells to the ports.
  - The three tiers (`power.md`) differ by trim: cell caps or a band colour.
- **Reactor (S5)** (refined with Vulps 2026-10-09; first to be modelled; mechanics in `reactor.md` → Six chambers):
  - A tall hexagonal vessel in a heavy braced frame, with **clear glass**: six fuel chambers inside, each
    rod visible, glowing by its own fuel (bright blue fresh, dim violet spent), tinted amber-red together
    as the reactor heats, pulsing slightly out of phase with each other.
  - **Six item ports on the top face**, one over each chamber, in place of the ring of rod sockets. A rod
    drops straight into its chamber; a spent one drops visibly out of the chamber's bottom and vanishes
    inside the housing, never out of the model.
  - Coolant pipes loop out to the sides.
  - **The front control bay holds seven levers:** one per chamber and a master that moves all six.
  - **Draft, 2026-10-09:** `Blender/Reactors/Reactor.py` → `Meshes/Reactor.fbx` (the placeholder rods and levers in
    the script are for renders only and are left out of the FBX). `Prefabs/Components/Reactor Draft` is built from it by
    **SpaceScape > Build Reactor Draft** (`Packages/com.spacescape.editor/Editor/ReactorDraftBuilder.cs`): slotted S5,
    the six top item ports, the I/O panel's In, Data and Out at back cells (3, 0), (4, 0), (5, 0), and seven copies of
    the Reactor's `RodLever` with no target. Nothing else works; the Reactor prefab is untouched.
  - **Ports on the grid:** a regular ring of six cannot land on 0.5 m cells, so the chambers sit at (x, z) (1.25, 0.25),
    (0.25, 1.25), (-0.75, 0.75), (-1.25, -0.25), (-0.25, -1.25), (0.75, -0.75): 1.20 to 1.27 m out, a slight pinwheel.
    Five chambers fit worse (up to 34 degrees off even, against 11 for six).
  - **The bay:** one side profile carried across: a front face, a deck sloping up and back where the levers stand, and a
    tower in front of the glass with the screen above the handles. The master lever is on the viewer's right.
  - **The I/O panel** is on the back, at the base; the back braces' lower feet stand wider to clear it.
  - **The vessel is sized to the ports**, which are fixed by the grid: 2.16 m to its corners, so every port collar sits
    inside the cap's inset, still clear of the side braces (2.25 m). Brace ends run into the plinth and the top beams.
  - **The plinth fills the whole footprint** (5 × 5 m), so the frame's feet stand on it, flush with the slot's edge. The
    component keeps its own base rather than standing on the ship's floor: a slot can face any way up (`grid.md`), and
    a component is carried and displayed out of its slot.
- **Radar (S3):**
  - A rig carrying a square phased-array panel on a turret that **sweeps while powered** and stops dead when
    it isn't.
  - A tilted console in front with the screen, the buttons and the zoom lever.
- **Disruptor (S3):**
  - A long square barrel on a yoke in a braced cradle.
  - The barrel visibly follows the aim, which is already networked.
  - Coil rings along it fill with glow as the capacitor charges, and flash when it fires.
- **Loader:** stays as it is: it is the reference.

## How

- **Order:** reactor first (Vulps, 2026-10-09: it is endgame, and it is where the cool factor matters), then
  battery, radar, disruptor.
- **For each:**
  1. A Blender draft and a screenshot for Vulps to approve before it replaces anything (shape requests are
     sketched first).
  2. Then the prefab: the new model, with its existing controls (`RodLever`, `PushButton`, screens) and readouts
     moved onto it.
  3. Ports stay where `ComponentEdgeModule` puts them; the model is built around them.
- **Moving parts and glow are visible motion,** so they follow networked state the component already shares
  (charge, output, heat, aim). Nothing new is sent.
- **Costs:** re-placing each component's controls and readouts, and keeping the flat look interesting without
  the store models' textures.
