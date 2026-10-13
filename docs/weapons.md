---
aliases:
  - "Weapons"
tags:
  - component
sizes:
  - S3
  - G3
---
# Weapons

Guns and what feeds them. The radar is in [[radar]], and how its data reaches a gun in [[data]].

## Guns go on the hull

**Status (2026-10-13):** decided (Vulps); not built.

- **Physical and energy weapons both.** A physical gun's shot has to leave where it was loaded, so a gun cannot sit
  inside the ship and act at a distance: it has to be on the hull.
- **So guns are G-sized components,** G1 to G5, installed only in G slots around the hull, and nothing but a gun
  goes in a G slot ([[slots#G slots|slots → G slots]]). Every other component still goes in an S slot anywhere.
- **Installing a gun** replaces the empty G slot's box and the outer hull round it with the gun and an airtight
  framing for the gun's controller ([[slots#With a gun installed|slots → With a gun installed]]).

## Disruptor refactor to G3

**Status (2026-10-13):** planned; after the G slots' models and the fits check ([[slots#G slots|slots → G slots]]).

The disruptor below is an S3 that fires out of the open front of whatever slot it is in. It becomes a **G3**: a gun
in a G slot on the hull.

## The disruptor, phase 1

**Status (2026-10-02):** built, untested in-world.

`Prefabs/Components/Disruptor`: an **S3** component (800 kg), built from a copy of the reactor prefab so it is
networked, carried and slotted like any other. **Contained in its slot:** an open-fronted 4 m housing, carried
by its panels only; the barrel turns inside it and fires out of the open front. Put its slot with the front
facing where it should fire. One **In** port, on the back face, bottom row, middle.

- **Script:** `DisruptorBehaviourModule` (scripts/Ship/Weapons), a `LoadBehaviourModule` of its own since
  2026-10-07, when it stopped being a separate `Disruptor` script beside one. Its readout says **Ready**,
  **Charging** (its hold is filling) or **Not Charging**.
- **Power:** a load with Drain 0: it charges at 200 W into a 2,000 J hold (about 10 s from
  empty on a full feed) and holds. A charged gun stops drawing, so its branch wastes power ([[power]]: a ship
  at rest overheats; firing cools the line). Firing spends the whole charge (`LoadBehaviour.Discharge`). A
  worn gun can misfire as it fills: the charge is spent and nothing is ready.
- **Aim:** two of the reactor's rod levers on the back face (`RodLever`, which now sets any `ILeverDial`):
  **traverse** (axis 0; fully left aims 45° left) and **elevation** (axis 1; fully down aims 45° down). Each
  axis turns ±45° from straight out, so its field of fire is a square, with both levers at 0 its bottom left
  corner. A lever that moves the wrong way: swap its two angles.
- **Fire button:** fires when a hand **pushes** into it (once as the hand goes in, again only after it has
  come out; the cap sinks 1.5 cm), or when grabbed. Only fires when charged. Each press logs
  `[SpaceScape] Disruptor '…' fire pushed: charged / not charged (NN%), fires here / asks the master`.
- **Ready lamp:** red while charging, green when charged.
- **Shot:** a short orange beam from the muzzle, 500 m long. Nothing to hit yet.
- **Sound** (`Scifi Guns SFX Pack`, [[assets]]): once charged, `Gun1_Load`'s start plays once, then a short
  loop cut from it (Loop Start 1.05 s, Loop Length 0.1 s, both snapped to rising zero crossings so the loop
  does not click) repeats, scheduled on the audio clock, until it fires; firing plays `Gun3_1`. The Inspector
  has a **Preview** to play the charged sound, the loop alone and the shot in the Editor, restarting with the
  new cut while Loop Start or Length are changed (`DisruptorEditor`, Editor package).

> [!info] Networking (`ComponentNetwork`)
> The aim is the gun's state, written by whoever decides (its state
> authority, or anyone with no network), and saved for late joiners. A lever moved on another client moves there
> at once and is sent to the decider; levers and barrel follow the aim everywhere. Pressing fire on another
> client asks the decider, which fires and tells everyone, so the shot and sound are seen and heard by all. Charge,
> heat and integrity are shared as for every component; the charged sound follows the charge on every client.

Not done: the Disruptor must be in `SceneNetworking`'s network prefabs to be spawned into a slot.

## Phase 2: radar targeting

**Status (2026-10-07):** built, untested in-world.

The disruptor reads `RadarData` from its data network every frame (`DataGrid.Read`). With a powered radar there,
it turns at **Auto Aim Speed** (20°/s per axis) toward the first selected target inside its ±45° field and its
500 m reach, or back to straight ahead when there is none. With no radar on its network it is a manual gun.
Nothing gates a hand on a lever against it: both move the aim, so the lever fights the hand until someone
breaks the data connection (an open switch, a pulled radar, a cut conduit). It runs on every client from shared
state (the radar's selection, the targets); whoever decides writes the aim that everyone keeps. To connect:
run conduits from the radar's red data port to the gun's.

Earlier plan:

A radar (S1) with its own screen publishes its contacts and the selected target on the data network; the gun
reads the selected target each tick and aims at it ([[radar]], [[data]]). Someone presses fire, on the gun or, later, on
the bridge.

## Disruptor model

**Status (2026-10-09):** proposed, in the house look ([[component-models]]); after the radar.

- A long square barrel on a yoke in a braced cradle.
- The barrel visibly follows the aim, which is already networked.
- Coil rings along it fill with glow as the capacitor charges, and flash when it fires.
