---
aliases:
  - "Loader"
tags:
  - component
sizes:
  - S2
  - S3
  - S4
  - S5
---
# Loader

A component that takes [[packs]] in at its front, queues them, and sends their items to a component along an
[[item-conduit]]. How loading works is [[loading]]. Fits [[slots#S2|S2]] to [[slots#S5|S5]]; there is no S1 loader.

## Rules

**Status (2026-10-08):** refined.

- **The loader is a separate component** that slots like any other, so it can be damaged, swapped and tiered.
  **Its size sets how much it holds**: a bigger loader takes more packs, not bigger ones. **It holds size − 1
  packs** (decided 2026-10-08): S2 one, S3 two, S4 three, S5 four; there is no S1 loader.
- **The loader's queue.** A pack goes in through a hole in the loader's front (a let-go zone like the item
  port's, with no port) and runners carry it along, and straight up or down where the size needs it, through
  the loader's places. **The queue is square and a pack is always upright:** each place is a pack-sized square
  (about 1.25 m), and packs move flat or straight up, never tilted. The runners are open, so every pack can be
  seen. **The end place** is under a solid mechanism the pack's top goes into, hiding its lid: that is the pack
  the loader draws from.
- **An empty pack leaves sideways:** it slides left (seen from the front) along runners that carry on to a
  hole in the loader's left face, and drops into the world. No item port and no ejector for this; a trash
  system and item conduits come later. The queue moves up and the next pack goes into the mechanism.
- **The eject switch** (a rocker switch on the loader): while it is on, every pack runs to the end and leaves
  the same way, the one in the mechanism too, with nothing more taken from any of them. **The front still takes
  packs while it is on** (changed 2026-10-08: ejecting is not refusing; a pack fed then runs straight through
  and out, never loaded). Turned off mid-run, the pack already past the mechanism finishes, then it stops.
- **Loaders are dumb** (decided 2026-10-08): a loader takes **any** `ItemPack`. It cannot send a pack's items
  to a component that does not accept them (that component's `LoadingModule` refuses the type), so the wrong
  pack sits in the mechanism and clogs the queue until someone notices and ejects it (principle 3).
- **In a loader, a pack is the loader's state, not an object.** A pack let go in the front hole is drawn in
  and despawned; the loader keeps one entry per place (which pack prefab, which of its items are filled) and
  shows a picture of each, as a pack shows its rods. A pack leaving is spawned with that state. Every client
  runs the conveying and the slide out from the state. A non-master's hand feeding a pack sends the master
  the pack and its filled places, as an item port's load does.
- **S2 fits one pack** (3 × 2 × 2 m): the pack (1.10 × 1.15 m) under the mechanism (about 0.35 m) on runners
  is under 2 m, and 3 m wide leaves room for the runway to the left. Bigger sizes are laid out when built;
  S2 comes first.
- **The S2 rig** (model only, 2026-10-08): four braced legs round the place (braces front to back, so the way
  out to the left is clear), runners with rollers from the right legs out to the left edge, short front runners
  (the loading zone, 32 cm, to the front of the volume), and the mechanism on top. A pack rests on the rollers
  with its top just under the mechanism, is **nudged up 14 cm** into it when it reaches the place, and lowered
  back onto the rollers before it leaves. The fins top out at the slot's ceiling. `Blender/Loader.py` →
  `Loader.blend` → `Meshes/Loader.fbx`.

## S2

**Status (2026-10-08):** built; the queue stepped by hand in Play mode offline, untested in-world.

- **The S2 loader** (`Prefabs/Components/Loader S2`; written 2026-10-08, the queue stepped by hand in Play mode
  offline, untested in-world). A component like the others (slot S2, power **In** on the back face at cell
  (0, 1), `NetworkBridgeData16`), with `LoaderBehaviourModule` (`scripts/Ship/Modules/Behaviours`), a
  `LoadBehaviourModule` drawing **50 W** ([[power#Open the scale of every number (logged 2026-10-08)|power → the scale of every number]]). Unpowered, nothing moves
  and nothing is taken in.
  - **The path:** the **Path** children, in order, are where a pack's origin (the bottom of its box) goes:
    **Entry** (on the front runners), **Place**, **Place Lifted** (14 cm up, in the mechanism), **Lowered**,
    **Out** (along the runners to the left until clear of the volume). **Places** lists the points where a
    pack waits, front of the queue to the end; the last is the one under the mechanism. Each size only needs
    its own path and places.
  - **Conveying:** each pack heads for the furthest place it may have, the one ahead first, at 0.5 m/s, and
    never moves into another pack (each is a 1.10 × 1.15 × 1.10 m box). The pack at the last place leaves
    when it is empty or the eject switch is on; one already past it always finishes. At the end of the path
    the master puts it back into the world as a spawned pack holding what it held, and it drops.
  - **The intake** (the **Intake** child, a 1.3 × 1.6 × 1.2 m box over the front runners): a pack a hand on
    this client lets go of in it settles onto **Entry** over 0.6 s, then becomes a picture there and is
    removed (`Item.Remove`). **In it means any part of the pack in the box** (2026-10-10, Vulps found packs the
    rails came out for being ignored): the same test that sends the rails out, so if they come out, letting go takes
    it. A pack let go outside the box is watched for 3 s in case it falls or slides in; only the client whose hand
    let go watches it. It waits in the box, and goes in when there is room: powered, a place free, and
    **Entry** clear. With the eject switch on it is still taken, and runs straight through and out. **A loader takes any
    type of pack** (Vulps's design: emergent "woopsies", which is what the eject switch is for). **Packs** lists every
    pack type (Fuel Rod Pack, Addon Pack); a pack whose items nothing wants just rides the conveyor until ejected. A
    pack's Contents (the addon pack's addons) ride through with it and come back out intact.
  - **The front rails** (decided and built 2026-10-08, untested): the sign that it is ready to take a pack.
    While a pack it would take now (powered, room, a type in **Packs**; held or not, eject on or off) is in the
    intake, an inner set of rails slides 1.1 m forward out from under the front runners at 0.8 m/s, as far
    out of the rig as the left runway, and back in once there is none. Rails staying in means it will not
    take it. `Vehicle_Car_Button_02` loops while they slide (**Rails Sound** child). Each client works this
    out from the packs it sees, as the item port's doors do, so nothing is sent. The intake box now reaches
    the end of the extended rails (1.3 × 1.6 × 1.8 m). The **Front Rails** model is drawn retracted, tucked
    under the place below every runner.
  - **Riding the rails in** (Vulps, 2026-10-10, untested): the intake box is stretched in code along the rails to
    Rails Reach past the path's first point (plus half its depth), so a pack can be put down anywhere from the
    extended rails' end to just inside the frame. A pack let go there lines up on the rails where it lies (no
    further out than they are), then the rails retract at their own speed and, once they reach it, carry it in
    on their end to the path's first point, where it is taken in as before. That stretch is the path's first
    stretch but not a place: never counted as room.
  - **Sound:** `Vehicle_Car_Engine_Idle_Interior_Loop_03` loops while any pack is moving, from the **Running
    Sound** child (Environment layer, heard to 15 m). Each client plays it from the packs it sees moving, so
    nothing is sent.
  - **Pictures:** `ItemPackPicture` copies a pack prefab's meshes, shut, with its count lamps showing.
  - **The eject switch** is the rocker (`RockerSwitch.prefab` without its switch addon) on the mechanism's
    front, right of the amber strip. `RockerSwitch` now throws any `ISwitch`; `SwitchAddon` and the loader
    are both one.
  - **Networking:** `LoaderBehaviourModule` is an `INetworkedState`: the eject switch, then per entry (one per
    place, plus one leaving) its kind, filled places and how far along the path it is. Every client conveys;
    the master's state corrects it (a pack within 15 cm is left where this client has it). A non-master
    feeding a pack sends **PackFed** (message 7: kind, filled) and throwing the switch sends **EjectSwitch**
    (message 8); for 1 s after either, that client keeps its own state. Only the master puts a leaving pack
    out (`Decides`, `PutOut`, spawned through `ComponentNetwork`). **Needs adding to `SceneNetworking`'s
    network prefabs** like every component.
  - Nothing draws from the pack in the mechanism yet: how the loader feeds a component is step 2's second half.


## Sending along an item conduit

**Status (2026-10-09):** built, stepped offline, untested in-world.

- **The loader's Out port** is on its top face at cell (1, 2), over the mechanism's housing (centre at
  -0.75, 1.0, 0.25 in the loader's space).
- **Sending** (`LoaderBehaviourModule`): when the component at the other end of its run has a
  `LoadingModule` that takes the type held by the pack in the mechanism, and holds none:
  1. The master takes one out (the highest filled place) and sends it at **1 m/s** (2 cells a second).
  2. On arrival it loads it into that module.
  3. One item is in the tube at a time.
  4. Unpowered, it stops where it is. If the run is cut, the item is lost, with a warning.
  5. The loader's state gains two values: the kind being sent and how far it has gone. It now needs 15 ints
     of its 16.
- **A stream, not one at a time:** the loader sends the next item once the last has fully left the Out port and
  gone on for the **send gap** (1 s, balance later), as long as a port in the tree wants one it has nothing on the
  way to. It never waits for an arrival. Six empty chambers: four sent, the empty crate ejected, two more. Up to
  six items in the tube per loader.

## S3, S4 and S5

**Status (2026-10-09):** built; awaiting Vulps's approval in the editor.


**Built:** `Prefabs/Components/Loader S3`, `Loader S4` and `Loader S5`, from `Blender/LoaderS3.py`, `LoaderS4.py`
and `LoaderS5.py` into `Meshes/LoaderS3.fbx` and the others, each copied from S2's prefab with its own path,
places, intake, rails, rocker and ports. Each uses `NetworkBridgeData32` (S3 needs 18 ints, S4 21, S5 24).
The queue was stepped in edit mode for each: packs settle at every place, and all run out when ejecting.
Untested in Play mode and in-world. They need adding to `SceneNetworking`.
- **Ports:** the item Out port is on the top face over the duct (S3 cell (1, 4), S4 (4, 4), S5 (7, 5)); the
  power In port is at back cell (0, 0) on a back panel.
- **The duct sits 0.25 m forward of the mechanism's centre:** top-face cells are centred on ±0.25, so a duct
  centred on the mechanism could not sit under a whole port.
- **A pack at the top of a lift column rests on guide channels or small catches,** not runners: runners there
  would block the pack rising from below.
- Mass (600 kg) and the root box collider are copied from S2; the collider is fitted at runtime anyway.
- **Revised after Vulps's review (2026-10-09):**
  - All four scripts are in `Blender/Loaders/` on the shared `loader_parts.py` ([[tooling#Models (Blender, by script)|tooling → Models]]).
  - **The brace rule** ([[tooling#Models (Blender, by script)|tooling → Models]]) for every leg.
  - **Power port:** the panel sits on a block standing on a floor beam that runs to the back, the block's top
    flush with the panel's.
  - **Eject rocker:** straight on the right-front leg's face, with no panel.
  - **Amber:** no strips on the ducts and no floating markers.
  - **Lift guides:** S3 and S5 have dark guide channels with amber markers at the lift. **Guides go only on the
    sides no pack passes through:** S3's are on the right (seen from the front) and the back, since packs come in
    from the front at the bottom and leave to the left at the top. S5 also has a 45° knee
    brace under its exit cantilever (local to its script).



The same look and parts as S2: braced legs, rollers, the 14 cm lift, the mechanism, front rails and the eject
rocker. Packs load at the lower right (seen from the front), are carried along the lower level, lifted
straight up a column to an upper level, and leave to the left from the upper level. They drop about 1.45 m to
the floor, which is fine. Upper runners are at floor + 1.50.

- **S3** (4 × 4 × 4, 2 packs): place 1 at the lower right; the end place is directly above it.
- **S4** (5 × 4 × 4, 3 packs): places 1 and 2 on the lower level; the end place is above place 2. **To be reworked**
  when the S4 components are done (Vulps, 2026-10-12): for inspiration see the Applied Energistics Minecraft mod.
- **S5** (5 × 5 × 5, 4 packs): places 1 and 2 on the lower level, place 3 above place 2, and the end place one
  step left of place 3 on the upper level.
- **A duct to the ceiling:** the mechanism sits below the top of the volume, so a square duct in its style
  rises to the ceiling, and the item Out port sits on top of it. The loader will **probably be revisited when
  the trash system comes**, to make room for an output item port for ejected packs. Not now.
- **The eject rocker** sits at hand height on the right-front leg, not up on the mechanism.
- **The power port sits on the model** (raised 2026-10-09: S2's port floated behind the rig). The power port
  is about 8.4 cm square and sinks 4 cm behind its face. Every loader has its power In port on the back face
  at cell (0, 0), on a dark panel a little bigger than the port, flush with the back face, on an arm or
  bracket tied into the rig. S2 got its panel, on an arm from the back-right leg, the same day.
- **Mesh colliders** (2026-10-10): every loader's Frame, Mechanism and Front Rails have one matching the model,
  on only while it is locked in its slot (`CarriedComponent`, as the reactor's). The rails' moves with them, so
  a pack dropped on extended rails rests on them. Ejected packs appear past the frame's end, clear of it.

## Next pass: holding more

**Status (2026-10-09):** proposed; sketch saved.


The S3, S4 and S5 layouts (two levels, packs loaded at the lower right, lifted to an upper level, leaving to the
left) leave room for far more places than size − 1. Come back to this in the next loader pass: how many packs
each size holds, and whether "size − 1" stays the rule. Vulps: "it is going to bug me".

**How it should be: a serpentine** (sketched by Vulps on S5, `images/loader-serpentine-queue.png`). Packs use
both levels in both directions instead of the lower level only feeding one lift:
1. In at the lower right (front loading).
2. Lifted up to the upper level on the right.
3. Along the upper level to the middle.
4. Down a column in the middle to the lower level.
5. Along the lower level to the left.
6. Up to the upper level on the left, under the mechanism, which is the end.
7. Out to the left.

Every leg of the route is a place or a run of places, so a loader holds as many packs as the route has room
for. The queue code is already generic: a path of points with places on it, packs moving straight between them.
Columns going down as well as up are just more path points. The model work is the part that changes: lift
columns and guides wherever the route turns vertical, and runners clear of each column.

![Serpentine queue](images/loader-serpentine-queue.png)

## Networking

| State | Owner | How it travels |
|---|---|---|
| The packs on a loader | the loader, decided by the master | the loader's state: per place, which pack prefab and its filled places, like a slot's occupant. Conveying and sliding out are run on every client from it |
| A pack fed into a loader by hand | that client draws it in and despawns it | a message to the master with the pack and its filled places; the master queues it |
| The eject switch | the switch, as every rocker switch | as now; the master runs the ejecting |
