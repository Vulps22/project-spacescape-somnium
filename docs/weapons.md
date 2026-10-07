# Weapons

Guns and what feeds them. The radar is in `radar.md`, and how its data reaches a gun in `data.md`.

## The disruptor, phase 1 (built 2026-10-02, untested in-world)

`Prefabs/Components/Disruptor`: an **S3** component (800 kg), built from a copy of the reactor prefab so it is
networked, carried and slotted like any other. **Contained in its slot:** an open-fronted 4 m housing, carried
by its panels only; the barrel turns inside it and fires out of the open front. Put its slot with the front
facing where it should fire. One **In** port, on the back face, bottom row, middle.

- **Script:** `DisruptorBehaviourModule` (scripts/Ship/Weapons), a `LoadBehaviourModule` of its own since
  2026-10-07, when it stopped being a separate `Disruptor` script beside one. Its readout says **Ready**,
  **Charging** (its hold is filling) or **Not Charging**.
- **Power:** a load with Drain 0: it charges at 200 W into a 2,000 J hold (about 10 s from
  empty on a full feed) and holds. A charged gun stops drawing, so its branch wastes power (`power.md`: a ship
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
- **Sound** (`Scifi Guns SFX Pack`, `assets.md`): once charged, `Gun1_Load`'s start plays once, then a short
  loop cut from it (Loop Start 1.05 s, Loop Length 0.1 s, both snapped to rising zero crossings so the loop
  does not click) repeats, scheduled on the audio clock, until it fires; firing plays `Gun3_1`. The Inspector
  has a **Preview** to play the charged sound, the loop alone and the shot in the Editor, restarting with the
  new cut while Loop Start or Length are changed (`DisruptorEditor`, Editor package).

**Networking** (`ComponentNetwork`): the aim is the gun's state, written by whoever decides (its state
authority, or anyone with no network), and saved for late joiners. A lever moved on another client moves there
at once and is sent to the decider; levers and barrel follow the aim everywhere. Pressing fire on another
client asks the decider, which fires and tells everyone, so the shot and sound are seen and heard by all. Charge,
heat and integrity are shared as for every component; the charged sound follows the charge on every client.

Not done: the Disruptor must be in `SceneNetworking`'s network prefabs to be spawned into a slot.

## Phase 2: radar targeting (planned)

A radar (S1) with its own screen publishes its contacts and the selected target on the data network; the gun
reads the selected target each tick and aims at it (`radar.md`, `data.md`). Someone presses fire, on the gun or, later, on
the bridge.
