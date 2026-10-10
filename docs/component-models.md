# Component models

**Decided 2026-10-09: every component's model is replaced with one in the loader's look. This is the next
piece of work, ahead of everything except a major bug.** The S5 reactor is done (on the real prefab, 2026-10-10;
tested in-world); battery, radar and disruptor are not started. Smaller reactors (S4 to S1) are proposed below.

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

## Ideas per component

- **Battery (S1):**
  - A four-post frame around a 2×2 stack of tall hex cells, chunkier cousins of the fuel rod, in glass
    sleeves.
  - Each cell lights from the bottom up with charge, so the charge can be read from across the room.
  - Heavy bus bars run from the cells to the ports.
  - The three tiers (`power.md`) differ by trim: cell caps or a band colour.
- **Reactor (S5)** (refined with Vulps 2026-10-09; **built**: the real Reactor prefab carries it; mechanics in
  `reactor.md` → Chambers):
  - A tall hexagonal vessel in a heavy braced frame, with **clear glass**: six fuel chambers inside, each
    rod visible, glowing by its own fuel (bright blue fresh, dim violet spent), tinted amber-red together
    as the reactor heats, pulsing slightly out of phase with each other.
  - **Six item ports on the top face**, one over each chamber, in place of the ring of rod sockets. A rod
    drops straight into its chamber; a spent one drops visibly out of the chamber's bottom and vanishes
    inside the housing, never out of the model.
  - Coolant pipes loop out to the sides.
  - **The front control bay holds seven levers:** one per chamber and a master. The master moves on its own
    while held and sets all six when let go (2026-10-10).
  - **The model:** `Blender/Reactors/Reactor.py` → `Meshes/Reactor.fbx` (the placeholder rods and levers in the script
    are for renders only and are left out of the FBX). `Prefabs/Components/Reactor Draft` (built by **SpaceScape > Build
    Reactor Draft**, `Packages/com.spacescape.editor/Editor/ReactorDraftBuilder.cs`) was the approval step.
  - **On the real Reactor prefab** (2026-10-09/10): the new model, Front ZPlus; six item ports on top (YPlus), each
    feeding one chamber's `LoadingModule`; In, Data and Out on the back I/O panel at cells (3, 0), (4, 0), (5, 0);
    seven working `RodLever`s; non-convex mesh colliders on the Frame, Vessel and Control Bay, on only while it is
    locked in its slot (`CarriedComponent`).
  - **Shown on the model** (`ReactorChamberRods`): each chamber's rod, glowing from spent to fresh by its fuel and by
    load, pulsing, tinted toward amber-red with heat; a spent rod drops out of the bottom and the waiting rod slides
    down into its place. A **blue core light** in the central column travels up and down faster as the average rod
    position rises, and from 70% holds still in the middle, bright and wide. The **screen** shows a readout table
    (rod | fuel | position | temp, in TMP). The old green lamp is gone.
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
- **Smaller reactors, S4 to S1** (proposed 2026-10-09; Vulps likes all four; **not decided**): deliberately different
  machines, not shrunken S5s, each on its own rung of the heat-ratio ladder (`reactor.md` → The heat ratio is the
  real upgrade). Every one keeps the house rules: frame, core and control panel, the model as the readout, the shared
  materials, and an I/O panel with In, Data and Out. Several change mechanics, not just looks; each of those needs a
  decision in `reactor.md` and the networking talk before it is built (listed after the four).
  - **S4, "The Bottle"** (5 × 4 × 4 m; one slot on the ship, the rarest):
    - **Not a fission pile:** a long horizontal magnetic bottle running the full 5 m, a glass tube ringed by heavy
      magnet coils, with a thin white-violet plasma line burning down its middle, seen in the gaps between coils.
    - **No rods.** The magnets hold a field instead of lifting rods: the same house-load mechanic. One large
      throttle wheel sets field strength; the line thickens and brightens as it opens.
    - **SCRAM:** the field collapses and the line snaps out in a flash. The most dramatic shutdown in the game.
    - **Practically never runs dry:** a breeder that eats spent S5 rods through a side hopper, needing a fresh
      pellet only rarely. It gives the S5's waste a use.
    - **Heat about 0.8 : 1** (aspirational fusion): less heat than power. The one you save up for.
    - **Frame:** low and wide, a cradle under the bottle rather than a cage round it, the console at one end like a
      ship's helm.
  - **S3, "The Pool"** (4 m cube):
    - An open-topped well: an armoured tank with a grated walkway lid, looking down into water lit Cherenkov blue,
      the core at the bottom.
    - **The water level is the coolant.** The glow brightens with output; as coolant boils off the level drops down
      a marked gauge on the tank wall, and it bubbles when running hot. No screen needed to know you are low.
    - **Rods** hang from an overhead bridge gantry spanning the top, drive motors exposed: you watch them rise out of
      the water, and drop back in on a SCRAM with a splash.
    - **Fuel:** a top hatch on the gantry, matching the S5's top ports.
    - **Heat about 1.3 : 1** (sodium or gas). The workhorse mid-game reactor.
  - **S2, "The Breech"** (3 × 2 × 2 m, long on x):
    - A horizontal pressure tube along the long axis like a heavy gun breech, a slit window down its side showing
      one glowing core line.
    - **Fuel** loads horizontally through a breech door at one end, with a big amber handle you swing shut: like
      loading a shell.
    - **Rods are horizontal, so gravity cannot drop them.** The fail-safe is a spring: the magnets hold the rods
      back against springs, and a SCRAM slams them home with a loud clack. Same mechanic as the S5, a different
      feel.
    - **Heat about 2 : 1** (pressurised water). The common reactor for early ships.
    - **Frame:** low saddle clamps along the tube, the control panel and lever on top like a breech block.
  - **S1, "The Pile"** (2 m cube):
    - A thermoelectric drum: a squat cylinder covered in radiating fins, one sealed fuel cartridge pushed into the
      front like a magazine. The fins glow from dull to orange-red with heat; it always runs hot.
    - **No rods and no throttle.** On whenever a cartridge is in; the only way off is pulling the cartridge, with
      your hand on something hot. A cartridge lasts a very long time.
    - **Heat about 15 : 1** (RTG). A scavenged one technically works and cooks the ship.
    - **Its real job:** a near-permanent starter for a bigger reactor, or emergency power when everything else is
      down. Best kept somewhere well ventilated.
  - **Mechanics each would add** (to decide in `reactor.md`): the S1 cannot be throttled; the S2's fail-safe is a
    spring, not gravity; the S3's water level is its coolant; the S4 controls a field instead of rods and eats
    spent rods.
  - **They need counters** (Vulps: a 15 : 1 S1 would wreck a ship's grid fast; not a reason not to, but there must be
    something to install that offsets or protects against it). The proposed counters, and the fact that the heat
    ratio is not yet connected to anything, are in `reactor.md` → Open: heat-ratio heat and its counters.
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
