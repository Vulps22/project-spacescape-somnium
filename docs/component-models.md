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

## Ideas per component

- **Battery (S1):**
  - A four-post frame around a 2×2 stack of tall hex cells, chunkier cousins of the fuel rod, in glass
    sleeves.
  - Each cell lights from the bottom up with charge, so the charge can be read from across the room.
  - Heavy bus bars run from the cells to the ports.
  - The three tiers (`power.md`) differ by trim: cell caps or a band colour.
- **Reactor (S5):**
  - A tall hexagonal pressure vessel in a heavy braced frame.
  - Glass slits show Cherenkov blue that brightens with output and turns amber-red as the reactor heats.
  - A ring of rod sockets on top.
  - A cradle at the item port showing the next rod: the `LoadingModule`'s state, visible.
  - Coolant pipes loop out to the sides.
  - The rod lever sits in an amber-trimmed control bay at the front.
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

- **Order:** battery (smallest, and several in the scene), radar, disruptor, reactor (most parts).
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
