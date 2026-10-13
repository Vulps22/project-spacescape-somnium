---
aliases:
  - "Component models"
tags:
  - art
---
# Component models

**Decided 2026-10-09: every component's model is replaced with one in the loader's look. This is the next
piece of work, ahead of everything except a major bug.** The house look, the rules and the process every model
follows. Each component's own model is in its doc: the S5 reactor ([[reactor#S5|reactor → S5]]) and the batteries,
S0 to S5 but the S4 ([[battery]], 2026-10-11), are done; the radar ([[radar#Model|radar → Model]]) and disruptor
([[weapons#Disruptor model|weapons → Disruptor model]]) are not started.

## Why

The S2 loader ([[loading]]) set a look that is nicer than the current models and gives far more creative
freedom. The current components come from three different sources, with three palettes and three levels of
detail. Next to the loader they don't belong to one ship:

- **Reactor:** a detailed store-bought look: a blue ribbed dome, tan and brown panels, speckled textures.
  Rounded, cartoon sci-fi. The furthest from the loader.
- **Battery:** pale grey boxes with cyan slits. Placeholder "Sample text" labels show on the top edge.
- **Radar:** a plain block with a cluster of skyscraper-like columns on top and a screen in front. It doesn't
  read as a radar.
- **Disruptor:** a flat hazard-striped slab with a small barrel. It reads as a placeholder.

Modelled by script in Blender (as `Blender/Loader.py`, `FuelPack.py`, `AddonPack.py`, `ItemConduit.py`,
`Reactors/Reactor.py`), every component fits the grid exactly, and its ports and controls go where we put them.

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

## Rules (decided with Vulps, 2026-10-09 and 10)

These come from the S5 reactor and the packs, and hold for every model after them. The Blender conventions (folders,
script names, axes, material names, the brace rule, port sizes) are in [[tooling#Models (Blender, by script)|tooling → Models]].

- **Each size is its own machine,** not a shrunken copy of the biggest (the S4 to S1 reactors are four different
  machines, [[reactor#Sizes|reactor → Sizes]]). The look is shared; the shape and how it works need not be.
- **The grid fixes the ports; the model is built round them.** Port cells come first (`ComponentEdgeModule` places
  each port on a cell centre), then the model. Every port sits on solid geometry, and where a face has an inset, its
  ports sit inside it: the reactor's vessel was widened until every top-port collar sat inside the cap's inset.
- **Braces are embedded, never balanced.** A brace's ends run into what it holds (the plinth below, the beams above),
  never stopping on a corner or floating short of it.
- **A component stands on its own base, filling its footprint.** The frame's feet stand on a plinth as wide as the
  slot, flush with its edge; nothing overhangs. It does not stand on the ship's floor, because a slot can face any way
  up ([[grid]]) and a component is carried and displayed out of its slot.
- **Models face +Z, with +Y up,** and every component has an I/O panel with In, Data and Out (both under The look).
- **Collision follows the model.** Non-convex `MeshCollider`s on the model's main parts, on only while the component
  is locked in its slot; free or in a hand, the carry box stands in (`CarriedComponent`: exact mesh colliders only work
  on a body that does not move). Moving parts that things rest on get one too and carry it with them (the loader's
  front rails).
- **The model is the readout, from shared state.** Glow, moving parts and lights follow what the component already
  shares (charge, output, heat, rod positions, aim); nothing new is sent for them. Pictures of items inside a model
  (the reactor's rods) copy the item's own model, not a stand-in.
- **Things held in a model or a pack are full size,** never shrunk, and turned so they can be read (the addon pack's
  addons stand upright facing out).
- **Never snap to a hand.** Anything grabbable is held where it was caught ([[interaction#Holding things|interaction → Holding things]]).
- **Render-only parts stay out of the FBX.** Placeholders that only show the idea in a draft (the reactor's rods and
  levers) are built in the draft and removed before export; the real ones come from the prefab.
- **World scripts never name `UnityEditor`,** not even inside `#if` or in a comment: Somnium's uploader rejects it.
  Anything editor-only (slot previews in prefab mode, builders) lives in `Packages/com.spacescape.editor`.

## How

- **Order:** reactor first (Vulps, 2026-10-09: it is endgame, and it is where the cool factor matters), then
  battery, radar, disruptor.
- **Moving parts and glow are visible motion,** so they follow networked state the component already shares
  (charge, output, heat, aim). Nothing new is sent.
- **Costs:** re-placing each component's controls and readouts, and keeping the flat look interesting without
  the store models' textures.

### The process (followed for the S5 reactor, the loaders and the packs)

1. **Talk it through first.** The shape, the ports and the controls are refined with Vulps and written here before
   anything is modelled. Mechanics that change go to their topic doc, and networking is settled ([[networking#Rules|networking → Rules]]).
2. **Script it in Blender** ([[tooling#Models (Blender, by script)|tooling → Models]]): one script per prefab in its family's folder, sharing a parts
   module (`Blender/Loaders/loader_parts.py`; the reactor imports it too). Write positions in Unity coordinates and
   convert once (the reactor's `U()`); derive port positions from their grid cells, and name every number. Run
   headless: `blender -b --python <script> -- <out.blend> [<out.fbx>]`.
3. **Draft.** Run without the FBX path: it saves the `.blend` only, placeholders included. For a component, a draft
   prefab is built from it beside the real one (`Reactor Draft`), so the real prefab is untouched until approved.
4. **Screenshots for review.** From several angles (eight for the reactor) with the Unity CLI's
   `capture_scene_view`; it writes under `Assets/Temp`, so the file is moved out and `Assets/Temp` and its `.meta`
   deleted ([[tooling#Driving the open editor from outside (Unity CLI)|tooling → Driving the open editor]]). Vulps reviews, then a close review against the screenshots:
   embedded braces, overhangs, ports in insets, clearances. Change only what was asked, then shoot again. (Once Vulps
   is at the PC, Vulps looks in the editor directly and no screenshots are needed.)
5. **Export.** Run with the FBX path: placeholders removed, `Meshes/<Name>.fbx` written. Refresh Unity
   (`unity command menu --path "Assets/Refresh"`) before touching the prefab, or the meshes are not there yet.
   Remap the FBX's materials onto the project's (`ModelImporter.AddRemap`: `FuelPackMetal`, `FuelPackDark`,
   `ComponentItem`, and any glow of its own), then check each renderer's material order against the FBX's.
6. **The real prefab,** edited straight through `unity command eval` (`PrefabUtility.LoadPrefabContents` → change →
   `SaveAsPrefabAsset`), not through a new editor tool: swap the meshes; move the existing controls and readouts onto
   the model; set the ports and the Front (ZPlus); add the mesh colliders; check nothing else on the prefab still
   points the old way (the reactor's `_front` stayed XMinus until caught). A new item made by copying a prefab (the
   addon pack from the fuel pack) gets its own network id and is added to `SceneNetworking`'s network prefabs.
7. **Compile, then Vulps tests in-world.** No errors is not working: only Vulps's test clears it. Then the docs,
   then a commit when asked.
