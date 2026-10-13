---
aliases:
  - "Networking"
tags:
  - tech
---
# Networking

The rules every networked thing follows, and the plumbing they share. Steps 0 to 6 were tested in-world with many
players on 2026-09-29; each system's own networking is in its doc. Nothing here is decided until it says
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
- **Most interaction keeps private state** ([[interaction]]): the rod lever and
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
| `PlayerBridge` | Somnium's player list, rig, `SceneNetworking.IsMasterClient` | MonoBehaviour on Players, beside `PlayerHands` |
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
10. **Things that come and go own their state; conduit cells are stored by position in a host-owned list.** A
    component or a carried item is a `NetworkObject` with its own `NetworkBridgeData`. A conduit cell (and later
    a hull cell) is an entry in a list the master owns and writes into shared chunks; the entry carries its
    position, so its position is its identity. See [[conduits#Networking|conduits → Networking]].
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

## Each system's networking

Each system's state, owner and how it travels is in that system's doc, under **Networking**: [[conduits]],
[[item-conduit]], [[addon]], [[power]], [[grid]] (slots and carrying), [[reactor]], [[loading]], [[loader]], [[packs]],
[[radar]], [[weapons]], [[data]]. This doc holds the rules and the plumbing they all share.

## Steps

**Status (2026-09-29):** steps 0 to 6 written and compiled; tested in-world with many players. Each system's part now
lives in its own doc.

0. **Setup (done).** Vendored scripts in `scripts/Community/`; `SpaceScape.asmdef` references
   `SomniumSpace.Network`, `Fusion.Unity` and `Fusion.Addons.Physics`. Compiles clean.
1. **Bridges and a messenger (written 2026-09-28, compiles clean, untested in-world).**
   `Player/`: `PlayerBridge`, `PlayerIdentity`, `PlayerManager` (on Players, beside
   `SomniumPlayersContainer`). `Networking/`: `WorldBridge`, `WorldManager`, `NetworkMessenger`,
   `ShipNetwork`. In the scene: a `Networking` object carrying our `SceneNetworking`, and a
   `ShipNetwork` object (`NetworkObject`, `NetworkMessenger`, `ShipNetwork`).
   To check in-world, from the last `[SpaceScape] Logging Session Started` banner in `Player.log`:
   each client logs `PlayerManager: ... joined` for the other, `local player is ..., master=...`,
   and `ShipNetwork: hello from ...` for both itself and the other.
2. **Component state (written 2026-09-28, compiles clean, sim tests pass).**
   Components drive the conduits, so they come first. `ComponentNetwork` on each component writes, as
   the scene object's state authority (the master), its heat and every `INetworkedState` module's
   values into a `NetworkBridgeData`, 4 times a second, as float bits; slot 0 is a counter so a client
   can tell a real write from the empty array. Everyone else applies them and simulates on.

   | Module | Values |
   |---|---|
   | every component | heat, severed (a lost component that forwards power, or a ring's merge point) |
   | `CapacitorModule` | charge |
   | `IntegrityModule` | condition |
   | `LoadBehaviourModule` (bulbs) | working |
   | `ReactorBehaviourModule` only | dial, rod position, fuel, coolant litres, flow, magnets gripping |

   The reactor's rod lever is sent as it turns ([[reactor#Networking|reactor → Networking]]). The sim gained `Correct` setters for these values,
   and `PowerGraph.CorrectPopped` severs through the same path as a local pop.
   Reactor gets a `NetworkBridgeData12` (11 used), batteries and bulbs a `NetworkBridgeData6` (5 used).
3. **Conduit state:** [[conduits#Conduit cell state|conduits → Conduit cell state]].
4. **Addons:** their state is carried in their conduit's cell ([[addon#Networking|addon → Networking]]).
4b. **Holograms open elsewhere:** [[conduits#Holograms open elsewhere|conduits → Holograms open elsewhere]].
5. **Conduit edits from non-masters:** [[conduits#Edits from non-masters|conduits → Edits from non-masters]].
6. **Failures on the master only:** [[power#Networking|power → Networking]].

### Later, low priority

- **Grabbables.** Add `NetworkGrabbable` (with `NetworkObject` and `NetworkRigidbody3D`) to the addon
  item prefab.
- **Interaction rework** ([[interaction]]): the rod lever and hologram handles to stock XRI.


## Notes

Dated, newest last. What we tried, what broke, what we learned.

- **2026-09-28** Branch `add-networking` created. This draft written from the current code,
  [[todolist]], and Garden's `CLAUDE.md` and `world-bridge.md`.
- **2026-09-28** Community Modules vendored rather than referenced (the uploader question is still
  open). Six scripts into `scripts/Community/`, `SpaceScape.asmdef` gained `SomniumSpace.Network`,
  `Fusion.Unity` and `Fusion.Addons.Physics`. Compiles clean in the Editor; not uploaded.
- **2026-09-28** Found `NetworkBridgeData1`-`64` (pre-woven replicated int arrays) in Community
  Modules. Decided: the ship is a generated lattice of predefined conduits; players change only
  segments, addons and upgrades; the starting wiring is painted by hand and shipped, working but off.
- **2026-09-28** Step 1 written. `NetworkMessenger` dropped Garden's `SendToPlayer` (it hands out
  Fusion's `PlayerRef`, and late join no longer needs a push). `PlayerHands` stays as it is, reading
  Somnium's players for the hands only.
- **2026-09-28** Locked in who owns what: things that come and go (addons, components, items) own
  their state as networked objects; fixed things (conduits) are arrays by position. Conduit state is
  segments + popped and heat, 16 bits each, 128 conduits per Data64. Conduit heat is now sent.
- **2026-09-28** SceneManager split up. The grid scripts (`PowerGrid`, `ConduitLayout`, `TileEffects`,
  `ConduitHolograms`, `DebugVisuals`) moved onto the existing `Grid` object, which already parents the
  placed tiles; the conduits' network state will live there too. `SomniumPlayersContainer`,
  `PlayerHands`, `PlayerBridge` and `PlayerManager` moved to a new `Players` object. SceneManager is
  gone. Every moved component's settings checked against the last commit.
- **2026-09-29** In-world, joining threw `Type ...NetworkMessenger has not been weaved` from
  `RegisterSceneObjects`, and the whole scene registration stopped with it: scene network objects stayed
  off on other clients (reactors only the master could see), and slot handles were dead. `NetworkMessenger`
  inherited `NetworkBridgeEvents`, which makes it a Fusion type needing the weaver, and Somnium does not
  weave world code. **Rule: nothing in our assembly may inherit a Fusion network type** (`NetworkBehaviour`,
  `NetworkBridgeEvents`, `NetworkBridgeData`...). Hold Somnium's component beside ours and forward to it,
  as `ComponentNetwork`, `ConduitChunk`, `AddonNetwork` and `SlotNetwork` already did. `NetworkMessenger` is
  now a plain script over a `NetworkBridgeEvents` on the same object.
- **2026-09-29** In-world, component ports did nothing (no markers, no connections, no warnings) while the
  Editor was fine. `ComponentEdgeModule` kept them as `Port[]`, the only array of a custom serializable type
  in the project; the plain `long[]` conduit layout survives upload, so ports are now plain `int[]` per face
  (edited as ports by a custom Inspector) and each component logs "has N port(s)" at start to confirm.
  Also: a slot's local fallback fired in-world because Somnium's runner appears a moment after start,
  leaving everyone with unnetworked components (so the rod lever's dial never synced); it is now Editor-only.
- **2026-09-29** Ports stood half a cell off in-world. Fusion instantiates a spawned prefab at its own
  pose and runs Awake there; the position passed to `Spawn` (and `Place`) arrives after. **Rule: nothing on
  a spawnable prefab may work out world positions in Awake**; place children in the object's own axes,
  or wait until it is placed.
- **2026-09-29** First test with many players: components, slots, carrying and the handle all worked.
  The component prefabs had "Destroy When State Authority Leaves" on, so a component vanished when the
  player holding its authority left. **Rule: every NetworkObject is `V1 | AllowStateAuthorityOverride`
  (Flags 524289): Allow State Authority Override on, Master Client Object off, Destroy When State Authority
  Leaves off** (what the template's `SceneNetworking.NETWORK_OBJECT_DEFAULT_FLAGS` says, though nothing
  applies it). Vulps set it on Reactor, Battery and Switch Addon; the Slot, ConduitChunk and AddonState
  prefabs and the scene's ShipNetwork, Bulb and Bulb_Branch still had the destroy flag (262145) and were
  changed to match. With it on, a slot's NetworkObject was torn down when its authority left, and
  `SceneNetworking.ReassignNullObjectsAuthority` threw on it, stopping the reassignment of everything after
  it in the list.
