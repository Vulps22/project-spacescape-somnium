---
aliases:
  - "Loading"
tags:
  - mechanic
---
# Loading

How consumables in whole units get into a component: fuel rods into the reactor first, ammunition later. The items are
their own docs ([[fuel-rod]], [[packs]]); the component that feeds packs in is the [[loader]]; items travel between
components along [[item-conduit|item conduits]].

## Rules

**Status (2026-10-09):** refined.

- **One in use, one waiting: the standard for every loading behaviour** (decided 2026-10-09). Whatever an item
  port feeds holds one unit in use and one spare in its `LoadingModule`, as the reactor does now. Each of the
  S5 reactor's six chambers follows it, with its own port on the reactor's top face ([[reactor#Chambers|reactor → Six chambers]]).
  Loaders feeding top-face ports need their own behaviour: not designed yet.
- **Units only.** Loading moves whole items: fuel rods, ammunition. **Coolant is out of scope:** it is a
  liquid, and stays the drum and pump in `CoolantLoop` ([[reactor]]) until liquids get their own design.
- **A `LoadingModule` defines what the component's item port accepts** (one kind per module; the reactor has
  one, for fuel) **and holds the next item: one, or none.** That is all it knows. Consuming always means "now
  you have one less", never "a share of the item is used up". It knows nothing of how full or spent anything is.
- **The behaviour keeps its own level, and consumes when it accepts.** When the reactor is empty (0%) it asks
  its `LoadingModule` whether it has fuel. If it does, the reactor goes back to 100% and the item is consumed
  at that moment: the module is empty, and takes the next one when there is one. The reactor then burns down,
  99%, ... 0%, and asks again. So the item being burnt is already gone from the module; the module only ever
  holds what comes next. The pack shows what is left: watching it empty is the ship's endurance ([[reactor]]).
- **No pulling fuel out.** An item in a `LoadingModule` stays until the behaviour accepts it. This replaces
  the rack in [[reactor]] with a slot per rod and part-used rods.
- **No commands between components** ([[data]]). A component's `LoadingModule` **takes** an item when it is
  empty; nothing requests one. Consuming is the behaviour talking to its own module, inside one component.
- **The wrong kind does nothing.** An ammo crate offered to the reactor is not taken. Someone has to notice
  (principle 3).
- **Hand-feeding comes first,** and stays the plain way to load. A player holds a pack to the component's
  item port and the component takes a unit. This tests the item, the `LoadingModule` and the take before anything
  passes items between components.
- **Items reach a component from a loader along an item conduit** (decided 2026-10-08, replacing the item
  upgrade on branches): [[item-conduit]].

## The item port

**Status (2026-10-09):** built, untested in-world.

- **The item port** is a new kind of port, beside In, Out and Data. It is **square**, and
  bigger than the others: **a whole cell face** (provisional). **Its colour says which way items go**
  (decided 2026-10-08, replacing amber): **blue In, green Out**, as power ports are; the shape says it is an item
  port. A loader has an item Out port (on the mechanism; not placed yet). It has a collider that notices a
  `LoadableItem` inside it, and **takes nothing until the item has been let go**: a pack still in a hand is
  never taken. Once a conduit is wired to the port, the collider is turned off and items arrive along the
  conduit instead.
- **The item port:** `FlowDirection.Item`, set on a component's port like In, Out or Data (the port inspector
  has **Add Item**). It carries no power and no data. It shows as `Prefabs/Components/Item Port` in place of
  the round Port prefab: its opening (socket and two closed doors) is the whole cell face, 0.5 m, so a fuel rod
  (0.48 m across) goes in without clipping; an amber border (`ComponentItem.mat`) of 3 cm bars sits outside the
  cell (0.56 m overall, so it reaches 3 cm into neighbouring cells), on a 0.56 m container that sinks 0.29 m
  into the component to fill the gap to a model set in from its cell face. Set in
  `ComponentEdgeModule`'s **Item Port Prefab** (set on every component prefab). Gizmo and slot preview show
  it too. `ComponentEdgeModule.ItemAt(face, cell)` says where one is.
- **Its intake and doors** (`ItemPort`, on the Item Port prefab; every client runs it): a trigger box in front of
  the face, one cell (0.5 m) each way, looked inside every 0.1 s. The doors open (each half hangs from its outer
  edge and narrows into the frame, 0.5 s) while a `LoadableItem` that one of the component's `LoadingModule`s
  would take now (of the type it accepts, holding none) is inside, held or not, and close when there is none. Each client
  works this out from where it sees the item, so nothing is sent for the doors.
- **Door sounds** (`Vehicle_Essentials`, [[assets]]): `Vehicle_Car_Trunk_Open_Button` as the doors start to
  open, `Vehicle_Car_Trunk_Close_Impact` as they land shut (not if they reopen first), from the prefab's **Doors Sound** child (Environment layer, heard
  to 15 m). Each
  client plays them from its own doors, so nothing is sent.
- **Drawing an item in:** when a hand **on this client** lets go of such an item in the intake, this client
  (which held it, so has its authority through `NetworkGrabbable` and moves it for everyone through its
  `NetworkRigidbody3D`) turns off its grab and colliders, makes it kinematic, lines it up with the port over
  **Align Seconds** (0.75), draws it in end first over **Draw In Seconds** (5), loads it
  (`LoadingModule.LoadByHand`), waits for the doors to shut, and removes it (`Item.Remove`, set by
  `ShipNetwork` to `ItemNetwork.Remove`: despawn, taking authority first if needed, as for an addon
  item; destroyed locally with no network).
- **The load reaches the master:** `LoadByHand` loads here at once and raises `LoadedByHand`; on a non-master
  `ComponentNetwork` sends **ItemLoaded** (message 6: which of the component's `LoadingModule`s, by its place
  in `GetComponents`, the same on every client) and the master loads it too and writes
  it out with the rest, as the reactor's dial does.
- **Ports:** `FlowDirection.ItemOut` (appended) beside `Item` (in). The port inspector has **Add Item In** and
  **Add Item Out**. `ItemPort.Show` colours the border `ComponentInput` (blue) or `ComponentOutput` (green). An
  Out port's intake never takes anything; an In port's stops taking by hand once a run is wired to it
  (`ItemPort.Wired`). **A wired port's doors open and stay open** for as long as the run is wired to it
  (asked 2026-10-09), with the usual door sounds as they open and shut. Each client works this out from the
  runs, so nothing is sent.

## Loadable items

**Status (2026-10-08):** built, untested in-world.

- **Items are types, not a list of kinds** (decided 2026-10-08: a kind dropdown would grow without end).
  `LoadableItem` (`scripts/Ship`) is an empty abstract base; each kind of item is a subclass, so far
  `LoadableFuelRodItem`. A new item is a new subclass and touches nothing else.
- **`Item`** (`scripts/Ship`) is the base of everything carried: `AddonItem` and `LoadableItem` both derive from
  it. It holds the grab and the body, and lets an item that appeared out of physics fall once a hand first lets
  go of it. `ItemDispenser` puts out any `Item`, so a dispenser with the Fuel Rod in its **Item** slot spawns
  fuel rods (once the Fuel Rod is on `SceneNetworking`). Custom behaviour (a dropped
  rod making a radioactive zone) comes with health and environment.

## LoadingModule

**Status (2026-10-08):** built, untested in-world.

- **`LoadingModule`** (`scripts/Ship/Modules`): its **Accepts** slot takes an item prefab (the reactor's holds
  Fuel Rod), and it takes every item of that prefab's type (`Accepts(item)`; a behaviour finds its module with
  `Takes<LoadableFuelRodItem>()`). It holds one or none: `Load()` takes one in when it is empty, `Consume()`
  gives it up.
  Its one networked value (holds one or not) goes through `ComponentNetwork` like any `INetworkedState`.
  **Starts With One** and the context menu's **Load One** stand in for loading until items exist. The Reactor
  prefab has one, for fuel rods.

## Networking

| State | Owner | How it travels |
|---|---|---|
| A take (quantity −1, the `LoadingModule` holds one; an empty pack despawned) | the master only | state. Every client plays the feed animation when it sees the change; a late joiner sees the result |
| Whether a `LoadingModule` holds an item | the component, decided by the master | `ComponentNetwork` |
| The behaviour's fuel level, and the consume when it refills | the component; the consume is the master's | `ComponentNetwork`, like charge and heat |
| Whether an item in the port is still held | the item's holder | a replicated holder fact, never the local `isSelected` (rule 6): the master takes only an item nobody holds |
| A non-master's hand feeding an item | that client: it moves the item in, loads here, and despawns it | the load: an ItemLoaded message to the master; the item's motion: its `NetworkRigidbody3D` |
| The port's doors | nobody | worked out on every client from where the item is |
