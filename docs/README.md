# Project SpaceScape

Co-op ship management. VR-first, 2D supported. **One ship, one galaxy, unlimited crew.**

Status: **committed** (2026-09-17). The power model and a first-pass reactor are built, tested, and
running in the scene, on the module structure in `modules.md` (2026-09-26); the galaxy and courier
layer is still design.

Scene: `Assets/#User/SpaceScape/SpaceScape.unity`. Its own project at `~/Documents/project-spacescape-somnium`
since 2026-09-26, split out of Project Garden's working tree.

## The pitch

A crew runs courier contracts between stations across a largely empty procedural galaxy. The ship is
not a horseshoe of minigame consoles sharing an HP bar — it is a **machine**. Power flows through
conduits the crew physically reconfigures, heat accumulates wherever power is wasted, conduits blow,
components wear out, and every functional part is hot-swappable.

The reference point is **Barotrauma's submarine, not Star Trek Bridge Crew's bridge.** Barotrauma is
the only game in the genre where the ship is a diagnosable machine, and it is 2D. Nobody has built
one in 3D, and nobody has built one in VR.

## Why it is worth building

**One abstraction produces the simulation, the progression, the damage model, the diagnosis gameplay
and the upgrade system.** Every mechanic designed so far — heat, accumulators, switches, splitters,
faults, durability — slotted into the tile graph without needing a new rule. The two that looked like
they needed one (splitters, and a short circuit starving its siblings) turned out to be a tile with a
second face and an edge with a higher share.

## What is actually built

Everything in `Assets/#User/SpaceScape/scripts/Power/` — plain C#, **zero Unity references**, 242
tests. `scripts/Ship/` is the Unity layer that puts it in a scene.

| | |
|---|---|
| flow | shares at forks, only toward reachable receivers, endpoints, one tile per tick |
| holds | `Capacitor` — the charge every component keeps, owned by its `CapacitorModule` |
| sinks | `LoadBehaviour` — one type, relaxation oscillator, duty cycle = supply ÷ rated |
| sources | `IPowerProducer` / `IPowerSource` split, `ProducerSource`, `ConstantSourceBehaviour` |
| stores | `BatteryBehaviour` — sink and source over one hold, never offering more than it has |
| rings | a cycle of conduits blows its merge point; components never pass power through |
| state | `CanReceivePower()`, asked every tick, virtual |
| faults | `PowerEdge.Share` — a short behaves as several cables |
| heat | gain from waste, shed to ambient, conduction along conduits |
| failure | `power × heat` hazard; conduits sever, components take damage and misfire |
| footprints | a tile can occupy many cells; a face is a whole flank; overlaps warn |
| tiers | plain / safety / smart cells, each asking a harder question |
| reactor | cores, rods on magnets, house load off the top, coolant, a crew-owned dial |

**Running in the scene:** a reactor cold-started from a battery through a switch, feeding two
batteries and two loads across a branched grid, with heat and durability live underneath it.

`power.md` has the whole model, `reactor.md` the reactor. Not built: breakers, conduit capacity,
fire, and the fuel rack and coolant pad as real inventory.

## Running the tests

The sim has no Unity dependency, so it compiles and **runs** outside the Editor — which is why the
numbers in `power.md` are measured rather than asserted:

```bash
cd docs/simtest
dotnet run
```

The harness compiles the **real source files** in `scripts/Power/`, not copies; `-p:SimSrc=<dir>`
points it somewhere else. It lives here rather than in a scratch folder because `/tmp` is tmpfs and
a reboot took the first copy with it.

## Working in the Editor

The `unity` CLI drives the running Editor. Two things that have cost real time:

- **Never recompile while Play mode is running.** A domain reload wipes `PowerGrid._graph` and every
  tile's binding, `Awake` does not run again, and the sim silently stops with every readout frozen
  mid-value. It looks exactly like a logic bug. `PowerGrid` now detects and rebuilds, but check
  `Application.isPlaying` first anyway.
- **Unity defers compilation while playing**, so a change written during a Play session is not
  running until you stop. Check it reached the loaded assembly before believing a behaviour report:
  `typeof(PowerNode).GetField("...")` either exists or it does not.
- **Edit-mode `LateUpdate` only runs when the editor ticks** — a repaint, a mouse move over the scene
  view. `EditorApplication.QueuePlayerLoopUpdate()` forces it. Code that looks dead is often just
  waiting for a frame.
- **`OnValidate` fires only on the component you changed.** Move a tile and its neighbours never hear
  about it, which is why `Conduit` is `[ExecuteAlways]` rather than validation-driven.
- **`RevertPrefabInstance` destroys added children.** It wiped a `SplitterModule` off a conduit.
- Editor state read *after* a scene restart says nothing about what was on screen *before* it.

## The MVP

**The current scene, in VR, interactable.** The wiring stays fixed; what a hand can do is operate
what is already there. Rewiring the ship by hand comes after (`modules.md` → Rewiring live).

## The gate

Do not prototype a ship. Prototype **one fault.**

A source, a run of conduits, two consumers, and a way to break something. Two players, neither able
to see the other's readout.

> **Can they find it and fix it in under two minutes, by talking?**

Programmer art. No flying, no combat, no galaxy. If that is fun for ten minutes the rest is content.

## Documents

- `principles.md` — **read first.** The ten rules, including the ones learned by breaking them.
- `power.md` — the built model, with measured numbers.
- `reactor.md` — cores, rods, house load, the dial and coolant. Built.
- `damage.md` — weapons, armour layers, and how the hull is built from a kit on the grid. Design only.
- `the-loop.md` — galaxy, courier contracts, difficulty by depth, navigation. Design only.
- `modules.md` — the module stack every tile is built from. Built.
- `credits.md` — third-party work used, and the licence terms each needs; mirrored in-world on the Credits prefab.
- `networking.md` — the networking plan: authority rules, steps, notes.
- `todolist.md` — single-player work queued before networking: live wiring, repairing, loading, coolant.
- `open-questions.md` — what is undecided, what is parked, and what must not be re-proposed.

## Repo

Its own project, copied from Project Garden's Somnium template checkout with Garden's code, art
packs, docs and history removed. The scene depends on nothing outside `Assets/#User/SpaceScape/` but
TextMesh Pro. `Packages/com.somnilux.sdk` (the Linux upload workarounds) came with it and still uses
`GrowAGarden.*` namespaces; renaming them belongs in Somnilux's own repo.
