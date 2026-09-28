# Networking

**Status: rough draft (2026-09-28), branch `add-networking`.** Nothing here is decided until it says
**Decided.** It is the plan for taking the single-player build to multiplayer, and the place for notes
while we do it. Three jobs:

1. **Rules:** who holds authority over what, and how each kind of state travels.
2. **Steps:** the order of work from here to a networked ship, one upload per step.
3. **Notes:** what we tried, what broke, what we learned, dated, at the bottom.

Grow a Garden (`~/Documents/project-garden`) has already been through all of this with the same SDK,
the same transport and the same hands. Its `CLAUDE.md` (*Authority model*, *Layers*) and
`docs/world-bridge.md` are the source of most of what follows. Take its rules as they are and
re-derive only what is actually different here.

---

## Where we start

- **Nothing is networked.** No `NetworkObject`, no RPC, no `SceneNetworking` in the scene.
- `SpaceScape.asmdef` references only TextMesh Pro, UGUI and XRI. Fusion, `SomniumSpace.Network`
  and `CommunityModules` are all in the template (`Assets/Photon/`, `Assets/#User/Community Modules/`,
  untracked) but not referenced yet.
- **The sim is plain C#** (`scripts/Power/`, zero Unity references). `PowerGrid.Update` feeds
  `Time.deltaTime` into a fixed-step `PowerGraph.Tick`. The only randomness is seeded
  (`PowerGraph.PopSeed`, `Integrity`'s `Random(seed)`).
- **Conduits are not scene objects.** `ConduitLayout` holds each one as a packed `long`
  (`ConduitCode`) and builds them when the world starts. Every client therefore builds the same grid
  from the same inputs, and a conduit's cell coordinate is already a stable, shared id.
- **Most interaction keeps private state** (`todolist.md` → *Interaction rework*): the rod lever and
  the hologram handles are `XRSimpleInteractable` driven by our own scripts, the addon slot is a
  distance check, the rocker reads hand penetration. Only the addon item is a stock
  `XRGrabInteractable`.

## Community Modules: vendored

**Decided (2026-09-28).** Whether the uploader accepts a `CommunityModules` reference is still
unknown (Garden recorded it being rejected, and its branch that referenced CM was never uploaded), so
we do not depend on it. The Community Modules scripts we use are **copied into
`scripts/Community/`**, in namespace `SomniumSpace.Worlds.SpaceScape.Community`.

| Vendored | From CM | Why |
|---|---|---|
| `SceneNetworking` | `Scripts/Networking Plugin/` | the runner callbacks, master flag, prefab table |
| `NetworkGrabbable` | same | grab takes state authority |
| `TeleportNetworkRigidbody3D` | same | moving a kinematic `NetworkRigidbody3D` |
| `Utils/BytesReader`, `BytesWriter`, `NetValue` | `Scripts/Networking Plugin/Utils/` | message payloads |

Not vendored: `NetworkAnimator`, `NetworkAnimatorParameterSetter`, `NetworkOwnershipEvent` (nothing
uses them), and `SomniumSpace.Network` (`NetworkBridgeEvents`), which is its own assembly and on the
uploader's accepted list, so `SpaceScape.asmdef` references it along with `Fusion.Unity` and
`Fusion.Addons.Physics`.

The rules, from Garden:

- **Vendor one file at a time, from the V3 Community Modules** in this template, and only when
  something uses it.
- **Fresh `.meta` GUIDs**, never the CM ones. Duplicate GUIDs under `Assets/#User/` must stay at 0:
  `find Assets/#User -name '*.meta' -exec grep -h '^guid:' {} \; | sort | uniq -d`
- **No editor code.** An `#if UNITY_EDITOR` guard does not help: the server compile still sees the
  reference.
- **Keep them diffable against upstream.** Change only what has to change, and list every change
  here. Our own API goes beside them (the bridges), never into them.

Changes from upstream:

- namespace `CommunityModules` → `SomniumSpace.Worlds.SpaceScape.Community`
- `SceneNetworking.OnValidate`: `UnityEditor.PrefabUtility.IsPartOfPrefabAsset(no)` →
  `!no.gameObject.scene.IsValid()` (Garden's same divergence)

## Layers and bridges

**Proposed**, lifted from Garden's *Layers*:

```
components (conduit modules, lever, switch, addon item)   what a hand touches
orchestrators (e.g. a ShipLifecycle for joins/leaves)     listen, then command
managers (Ship, Player, World)                            own state; the only callers of a bridge
bridges (Player, World, NetworkMessenger)                  the only code naming anything outside SpaceScape/
```

- **A bridge is where our code touches anything that is not ours:** Fusion, Photon, the ProSDK,
  Community Modules, including our vendored copies in `scripts/Community/`. Unity itself
  (`transform`, `Rigidbody`, XRI) is not behind a bridge.
- **Each layer calls the one below it and no further, and never sideways.** A manager does not call a
  manager; an orchestrator coordinates them.
- **A bridge does not re-export the types it fronts.** It answers in our own types (Garden's
  `PlayerIdentity`, `PlayerRig`), never `PlayerRef` or `ISomniumPlayer`.
- **A pass-through manager is still doing its job.** It is the one place a guard lands later.

**The power sim stays below all of it.** `scripts/Power/` has no Unity reference and must not gain a
network one. Networking talks to `PowerGrid` and the modules, which talk to the sim.

Bridges to bring over, adapted rather than copied (Garden's are namespaced `GrowAGarden` and some
carry crop-specific bits). The vendored `Community/` scripts sit below the bridges, like Fusion:

| Bridge | Fronts | Notes |
|---|---|---|
| `PlayerBridge` | Somnium's player list, rig, `SceneNetworking.IsMasterClient` | MonoBehaviour on SceneManager; SpaceScape's `PlayerHands` probably moves behind it |
| `WorldBridge` | Fusion spawn / despawn / take authority / find by id | static; only needed for the few real `NetworkObject`s (addon items) |
| `NetworkMessenger` | `NetworkBridgeEvents` (`SomniumSpace.Network`) | frames a `byte` message id into byte 0; each user defines its own append-only `enum ...MessageType : byte` |

Not needed yet: `StorageBridge` (nothing persists per player).

## Rules

Garden's, which apply unchanged unless a line here says otherwise.

1. **The master is the source of truth.** Every peer can ask the master what something is and get
   back facts, not inferences. The axis is authorship: one client decides, everyone else observes.
2. **Three tiers:**

   | Tier | Owner | How it travels |
   |---|---|---|
   | **Facts** | the master, always | message to all, from the master, when it decides |
   | **Derivations** | nobody, computed | not sent; every client computes it from facts |
   | **Simulation** | the state authority | Fusion transform replication |

   Reach for derivations first. If every client can compute it, do not send it.
3. **Optimistic locally, authoritative eventually.** A hand must never wait for the master. A
   decision may be slow.
4. **One owner per shared property.**
5. **Idle means idle.** A component writes nothing until told to act.
6. **Local state never drives a networked decision.** `isSelected` is true on one machine only.
   The replicated equivalent is a `HolderId`-style fact.
7. **Every function states its own rules.** A master-only function guards itself, whoever calls it.
8. **Message ids are append-only.** Never insert or renumber.

New for SpaceScape (proposed):

9. **State over messages.** Anything that persists lives in replicated state (`NetworkBridgeData`,
   below), so a late joiner and a new master get it for free. Messages are for momentary things only:
   a request, a refusal, the flash when a conduit pops.
10. **A conduit is a slot, not a `NetworkObject`.** See *The ship's conduits*. Only things that move
    freely (addon items) are `NetworkObject`s.
11. **Readouts must agree between players.** Principle 2 is that Engineering says it out loud *and is
    believed*. If two players standing at the same meter can read different numbers, the design
    breaks. Readouts stay local to a place, but the value at that place is the same for everyone.
12. **The master does not get a status screen.** Being master is a network role, not a crew role.
    Nothing it knows is shown to its player (principle 2).

## Replicated state: `NetworkBridgeData`

**Found 2026-09-28** in `Community Modules/Plugins/SomniumSpace/Networking/`. `NetworkBridgeData1`
to `NetworkBridgeData64` (1, 2, 4, 6, 8, 12, 16, 32, 64) each carry one
`[Networked, Capacity(n)] NetworkArray<int>`, exposed as `IntArray`, and inherit
`NetworkBridgeEvents`, so one component has both replicated state and the usual messages.
`ARS Controls.prefab` uses one.

**This is how world code gets `[Networked]` state.** Somnium's Fusion config weaves
`SomniumSpace.Network`, not our assembly, so we use these rather than writing `[Networked]` ourselves.
They ship in Somnium's SDK and other worlds use them: we take them as working.

What it gives: late join, ordering and lost messages handled by Fusion, and state that outlives the
master who wrote it. What it costs: ints only, fixed sizes, only the state authority writes, and no
change callback, so readers poll (`OnRender`) and diff against what they last saw.

The same config has `NetworkConditions`, Fusion's lag and loss simulator, for if we can ever run two
clients locally.

## The ship's conduits

**Decided 2026-09-28.**

- **The ship is a lattice of conduits, always there, visible or not.** For now along the walls.
  Generated by script, on every client, from the scene. Nothing about generating it is sent.
- **Conduits are never added or removed at runtime.** Players change a conduit's **segments** and put
  an **addon** or an **upgrade** into it; that is all.
- **The ship ships working but off.** The starting wiring is painted by hand (today's
  `ConduitLayout` drawing) onto the lattice and built into the world: an inefficient but working
  vessel, off, when the master joins. Every other conduit starts with its segments parked.

What follows (proposed):

- **A conduit's slot is its index in the generated list.** Every client builds the same list, so the
  cell never needs sending.
- **Slot order must be deterministic:** sorted by cell coordinate, never scene order or `HashSet`
  order. Two clients ordering differently would put every edit on the wrong cable with no error. Log
  a hash of the generated list at startup so a mismatch shows immediately.
- **One `int` per conduit**, in banks of `NetworkBridgeData64` placed in the scene (64 conduits
  each; a room of ~600 is ~10 banks). The master writes; everyone else polls and rewires changed slots.

  | Bits | Holds |
  |---|---|
  | 0-11 | segment faces (as `ConduitCode`) |
  | 12-24 | addon, facing, turns, parked (as `ConduitCode`) |
  | 25 | popped |
  | 26 | addon state (a switch's on/off) |
  | 27-31 | upgrade (per branch, written onto each conduit in it) |

  Fusion sends only what changed, so idle banks cost nothing while playing; a joiner gets 4 bytes a
  conduit once. If the object count hurts, fall back to a sparse bank of (slot, value) pairs.
- **Every change is a request to the master,** applied at once on the requester (rule 3) and corrected
  if refused.

## Authority map

What exists, and who decides it. **All proposed.** This table is the thing to argue over.

| State | Tier | Decided by | Travels as |
|---|---|---|---|
| The conduit lattice and its starting wiring, placed tiles | derivation | the scene and build | nothing; every client generates it |
| A conduit's segments, addon, upgrade, popped, switch state | fact | master | its int in a conduit bank |
| Who holds a conduit open | fact | master | holder id per cell; refuses a second editor |
| The hologram's ghost while dragging | local | the editor's client | nothing until release; discarded if the hand leaves or the holder disconnects |
| Switch on/off | fact | master, on the presser's request | bit 26 of the conduit's int, optimistic on the presser |
| Reactor rod lever / dial | fact | master, on the holder's request | a value, streamed while held? |
| An addon item's pose | simulation | whoever holds it | `NetworkRigidbody3D` + `NetworkGrabbable` |
| An addon item existing (taken out / installed) | fact | master | spawn / despawn via `WorldBridge` |
| A component's charge, heat, integrity, fuel, coolant, dial | fact (correction) | master | a `NetworkBridgeData` on the component |
| Conduit flow, charge, heat | derivation | nobody | not sent; every client runs the sim |
| A conduit popping | fact | master rolls it | popped bit, plus a message for the effect |

## The grid: everyone simulates, the master corrects

**Decided 2026-09-28.** Every client runs the grid from the same state. The master alone rolls pops
and damage, and each component carries a `NetworkBridgeData` with its charge, heat, integrity, fuel
and coolant, which the master writes and everyone else moves toward. Conduit flow, charge and heat
are never sent: every client derives them by running the sim.

**Correct what remembers, derive what forgets.** A conduit's state is short-lived: power moves on a
tile a tick and heat sheds to ambient, so a conduit that drifts settles back once the components at
either end agree. A component's charge, heat, integrity and fuel build up over minutes and would keep
any error, so that is what gets corrected.

What it needs:

- the tick counted from Fusion's network tick, not `Time.deltaTime`, so every client ticks the same
  number of steps
- pops and damage taken out of the local tick on non-masters, applied only when the master says
- the component values as scaled ints, written a few times a second

## Steps

One phase per upload (Garden's rule; an upload is a slow manual round trip). Each interaction is
reworked to stock XRI in the step that networks it, since that is when its private state has to go.

0. **Setup (done).** Vendored scripts in `scripts/Community/`; `SpaceScape.asmdef` references
   `SomniumSpace.Network`, `Fusion.Unity` and `Fusion.Addons.Physics`. Compiles clean.
1. **Bridges and a messenger.** `PlayerBridge`, `WorldBridge`, `NetworkMessenger`, a `ShipNetwork`
   on SceneManager with the ship's messenger. A session banner in the log (Garden's
   `Logger.BeginSession`). Two clients join and each logs the other and who is master.
2. **Addon items.** `NetworkObject`, `NetworkRigidbody3D`, `NetworkGrabbable`, a `SingleHolderFilter`.
   Two players can pass one between them. Lowest risk, already stock.
3. **Switches.** The rocker's on/off, bit 26 of its conduit's int. Optimistic on the presser, confirmed by the
   master. Rapid re-toggling must still work (no re-press guards).
4. **Conduit edits.** The lattice generator (deterministic order, startup hash) and the conduit
   banks. Holding a conduit open is a fact; release sends a request; the master writes the slot;
   everyone rewires. Hologram handles move to `XRGrabInteractable`.
   Addon install / remove goes through the master (the slot becomes an `XRSocketInteractor`).
5. **The reactor lever.** Moves to an `XRGrabInteractable` on a hinge; its value becomes a fact.
6. **The grid.** Sim on the network tick, pops rolled by the master, a `NetworkBridgeData` per
   component for corrections.
7. **Late join and master change.** The state is all in `NetworkBridgeData`, so a joiner and a new
   master already have it. What is left is checking nothing else was kept privately.
8. **The gate** (`README.md`): two players, one fault, neither able to see the other's readout. Can
   they find and fix it in under two minutes, by talking?

## Notes

Dated, newest last. What we tried, what broke, what we learned.

- **2026-09-28** Branch `add-networking` created. This draft written from the current code,
  `todolist.md`, and Garden's `CLAUDE.md` and `world-bridge.md`.
- **2026-09-28** Community Modules vendored rather than referenced (the uploader question is still
  open). Six scripts into `scripts/Community/`, `SpaceScape.asmdef` gained `SomniumSpace.Network`,
  `Fusion.Unity` and `Fusion.Addons.Physics`. Compiles clean in the Editor; not uploaded.
- **2026-09-28** Found `NetworkBridgeData1`-`64` (pre-woven replicated int arrays) in Community
  Modules. Decided: the ship is a generated lattice of predefined conduits; players change only
  segments, addons and upgrades; the starting wiring is painted by hand and shipped, working but off.
