# Loading

How consumables in whole units get into a component: fuel rods into the reactor first, ammunition later.
**In discussion (2026-10-08); hand-feeding the reactor is built, untested.** The item port, `LoadingModule`,
the reactor's refuelling, `LoadableItem`, the fuel rod, and the port drawing a let-go rod in.

## Built (2026-10-08, untested)

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
- **Door sounds** (`Vehicle_Essentials`, `assets.md`): `Vehicle_Car_Trunk_Open_Button` as the doors start to
  open, `Vehicle_Car_Trunk_Close_Impact` as they land shut (not if they reopen first), from the prefab's **Doors Sound** child (Environment layer, heard
  to 15 m). Each
  client plays them from its own doors, so nothing is sent.
- **Drawing an item in:** when a hand **on this client** lets go of such an item in the intake, this client
  (which held it, so has its authority through `NetworkGrabbable` and moves it for everyone through its
  `NetworkRigidbody3D`) turns off its grab and colliders, makes it kinematic, lines it up with the port over
  **Align Seconds** (0.75), draws it in end first over **Draw In Seconds** (5), loads it
  (`LoadingModule.LoadByHand`), waits for the doors to shut, and removes it (`LoadableItem.Remove`, set by
  `ShipNetwork` to `LoadableItemNetwork.Remove`: despawn, taking authority first if needed, as for an addon
  item; destroyed locally with no network).
- **The load reaches the master:** `LoadByHand` loads here at once and raises `LoadedByHand`; on a non-master
  `ComponentNetwork` sends **ItemLoaded** (message 6: which of the component's `LoadingModule`s, by its place
  in `GetComponents`, the same on every client) and the master loads it too and writes
  it out with the rest, as the reactor's dial does.
- **Items are types, not a list of kinds** (decided 2026-10-08: a kind dropdown would grow without end).
  `LoadableItem` (`scripts/Ship`) is an empty abstract base; each kind of item is a subclass, so far
  `LoadableFuelRodItem`. A new item is a new subclass and touches nothing else.
- **`Item`** (`scripts/Ship`) is the base of everything carried: `AddonItem` and `LoadableItem` both derive from
  it. It holds the grab and the body, and lets an item that appeared out of physics fall once a hand first lets
  go of it. `ItemDispenser` puts out any `Item`, so a dispenser with the Fuel Rod in its **Item** slot spawns
  fuel rods (once the Fuel Rod is on `SceneNetworking`). Custom behaviour (a dropped
  rod making a radioactive zone) comes with health and environment.
- **`LoadingModule`** (`scripts/Ship/Modules`): its **Accepts** slot takes an item prefab (the reactor's holds
  Fuel Rod), and it takes every item of that prefab's type (`Accepts(item)`; a behaviour finds its module with
  `Takes<LoadableFuelRodItem>()`). It holds one or none: `Load()` takes one in when it is empty, `Consume()`
  gives it up.
  Its one networked value (holds one or not) goes through `ComponentNetwork` like any `INetworkedState`.
  **Starts With One** and the context menu's **Load One** stand in for loading until items exist. The Reactor
  prefab has one, for fuel rods.
- **The reactor refuels from it.** `FissionCore` has **Fuel Seconds Per Rod** (the reactor's Inspector field,
  formerly Fuel Seconds; 500 on the prefab) and `FuelLevel`, 100% to 0%. Spent, it calls its `Refuel`, which
  the reactor module answers by consuming from its fuel-rod `LoadingModule`; if that held one, the core is
  full again at once. It starts with one rod in. The reactor's readout shows its own `fuel 87%`; the `LoadingModule` has its own
  **Readout** field, showing `FuelRod: next in / none`. Every client
  runs this in its sim and the master's state (fuel, held or not) corrects it, as for the rest of the reactor.
  Sim test: `SpentRodTakesTheNextOne`.
- **The fuel rod** (`Prefabs/Items/Fuel Rod`): grabbed and networked as the Switch Addon is (`Rigidbody`,
  `XRGrabInteractable`, `IgnoresPlayerBody`, `NetworkObject` with flags 524289, `NetworkRigidbody3D`,
  `NetworkGrabbable`) plus `LoadableFuelRodItem`. **Needs adding to `SceneNetworking`'s network prefabs to be
  spawned.** Its model: 1 × 2 × 1 cells,
  long axis Y, root at its centre. A hexagonal body 0.48 m across the corners with 0.6 m straight sides and a
  0.2 m metal pyramid at each end. Each side is a frame of 3 cm metal bars round a glass panel sunk 1 cm,
  glowing Cherenkov blue (`FuelRodMetal.mat`, `FuelRodGlass.mat`, URP Lit). One box collider on the root.
  Modelled in Blender: `Blender/FuelRod.blend` (outside Assets, never uploaded), exported to
  `Meshes/FuelRod.fbx`. After editing the .blend, export the FBX over that file with Forward -Z, Up Y and
  Apply Transform on; keep the material names, and Unity maps them to the URP materials.

- **The fuel pack** (`Prefabs/Items/Packs/Fuel Rod Pack`; written 2026-10-08, Play-mode checked offline for the
  lid, pictures and lamps only; grabbing and networking untested). `ItemPack : Item` (`scripts/Ship`) is the
  abstract base, `FuelRodItemPack` the fuel one; its **Contains** slot holds the Fuel Rod prefab. It keeps which
  of its 4 **Places** are filled (one bit each) and shows a local, never-spawned **picture** of the item in each
  (`LoadableItem.Picture`; ports ignore pictures). The **lid** is an `XRGrabInteractable` with a
  `SlideGrabTransformer` (6 cm up, 6 cm down, the slot handle bar's settings): a tug past 80% of that starts the
  **Lid Track** rising 1.08 m over 2 s, a push down on the open lid starts it sinking, and either lets go of the
  lid. Pictures show while the lid is off the box and can be grabbed once it is all the way up. Grabbing one
  empties its place and, next frame, swaps it for a spawned rod in the same hand (as an addon is taken from a
  hologram); a rod let go within 0.25 m of an empty place in the open pack is hidden, removed
  (`LoadableItem.Remove`) and fills the place (`LoadableItem.LetGo`). The 4 lamps show the count with
  `FuelPackGlow` lit, `FuelPackDark` unlit. Its own colliders ignore each other. **Sounds:** `Vehicle_Car_Trunk_Open` as the lid
  starts to rise, `Vehicle_Car_Trunk_Close` as it starts to sink, from the **Lid Sound** child of the Lid Track
  (Environment layer, heard to 15 m); each client plays them from its own lid, so nothing is sent. Mass 20 kg (provisional).
  **Networking:** `ItemPackNetwork` with a `NetworkBridgeData2` (filled + 1, lid 1 shut / 2 open). The pack's
  authority writes; others read. A hand on another client changes the pack at once and takes the authority;
  until it comes, that client keeps its own state. It also strips each picture's `NetworkGrabbable`. **Needs
  adding to `SceneNetworking`'s network prefabs, and a dispenser,** before it can be spawned in-world. Its
  origin is at the bottom of the box. Model: `Blender/FuelPack.blend`, built by `Blender/FuelPack.py`
  (`blender -b --python Blender/FuelPack.py -- <blend> <fbx>`), exported to `Meshes/FuelPack.fbx`.

## Decided

- **Units only.** Loading moves whole items: fuel rods, ammunition. **Coolant is out of scope:** it is a
  liquid, and stays the drum and pump in `CoolantLoop` (`reactor.md`) until liquids get their own design.
- **A pack is an item with a kind and a quantity**: a fuel rod pack holding N rods, an ammo crate holding N
  rounds. Feeding takes one unit and the quantity drops by one. Scavenged packs arrive with fewer units.
  **An empty pack stays** (changed 2026-10-08: a unit can go back in, so empty is not finished); it goes when
  thrown in the trash (a trash system comes later).
- **The fuel pack is a box you take rods out of by hand** (decided 2026-10-08). Futuristic and dramatic, not a
  chest: an angular box, 1.10 × 1.10 × 1.15 m, a blue glowing seam under the lid, and four blue count lamps on
  the front, one lit per rod inside. The rods (2 × 2) hang from clamps under the lid. **A short tug on the lid
  starts it**: it rises straight up on its own, staying horizontal, about 1.05 m, lifting the rods clear of the
  box (about 2.2 m tall open); a short push down starts it sinking shut. Nothing holds the lid up: it is a
  game, not a reality simulator. Grab a rod to pull it from its clamp; let a rod go under an empty clamp and
  it goes back in. **A pack never goes into an item port**: a hand
  feeds the port one rod at a time, as now, and only the loader (step 2) takes whole packs.
- **The rods in a pack are not networked.** The pack keeps a count and shows one model rod per unit while
  open. Grabbing one spawns a real, networked rod into that hand and the count drops by one; a rod put back
  is despawned and the count goes up. The lid is the pack's own (stock XRI: an `XRGrabInteractable` that moves only
  up and down), and others see it move.
- **A `LoadingModule` defines what the component's item port accepts** (one kind per module; the reactor has
  one, for fuel) **and holds the next item: one, or none.** That is all it knows. Consuming always means "now
  you have one less", never "a share of the item is used up". It knows nothing of how full or spent anything is.
- **The behaviour keeps its own level, and consumes when it accepts.** When the reactor is empty (0%) it asks
  its `LoadingModule` whether it has fuel. If it does, the reactor goes back to 100% and the item is consumed
  at that moment: the module is empty, and takes the next one when there is one. The reactor then burns down,
  99%, ... 0%, and asks again. So the item being burnt is already gone from the module; the module only ever
  holds what comes next. The pack shows what is left: watching it empty is the ship's endurance (`reactor.md`).
- **No pulling fuel out.** An item in a `LoadingModule` stays until the behaviour accepts it. This replaces
  the rack in `reactor.md` with a slot per rod and part-used rods.
- **No commands between components** (`data.md`). A component's `LoadingModule` **takes** an item when it is
  empty; nothing requests one. Consuming is the behaviour talking to its own module, inside one component.
- **The wrong kind does nothing.** An ammo crate offered to the reactor is not taken. Someone has to notice
  (principle 3).
- **Hand-feeding comes first,** and stays the plain way to load. A player holds a pack to the component's
  item port and the component takes a unit. This tests the item, the `LoadingModule` and the take before anything
  passes items between components.
- **The loader is a separate component** that slots like any other, so it can be damaged, swapped and tiered.
  **Its size sets how much it holds**: a bigger loader takes more packs, not bigger ones.
- **The item port** is a new kind of port, beside In, Out and Data. It is **amber** and **square**, and
  bigger than the others: **a whole cell face** (provisional). It has a collider that notices a
  `LoadableItem` inside it, and **takes nothing until the item has been let go**: a pack still in a hand is
  never taken. Once a conduit is wired to the port, the collider is turned off and items arrive along the
  conduit instead.
- **Items reach a component from a loader along conduits with an item upgrade,** a branch upgrade like
  coolant and data (`conduits.md` → Addons and upgrades). A branch carries one upgrade, so it is cooled,
  connected or fed, not all three.

## Steps

Each step is refined when we reach it, trialled, and reshaped before the next.

1. **Hand-feeding the reactor.** Built, untested in-world: the port, the module, refuelling, the rod, and the
   port drawing a let-go rod in. Next: the fuel pack (decided above).
2. **The loader.** A loader component holding a pack, feeding a component's item port across conduits. As with
   data (`data.md`), any wired conduit could carry items until upgrades exist.
3. **The item upgrade** on branches, once the upgrade circle takes items.

## Networking

| State | Owner | How it travels |
|---|---|---|
| A pack's kind and quantity | the pack (it comes and goes, rule 10) | its own `NetworkBridgeData`, like `AddonItemStateNetwork` |
| The pack's lid: open or shut | the hand that tugs it (it takes the pack's authority) | the pack's `NetworkBridgeData`. Every client runs the rise or fall from the flip, so all see it move |
| A rod taken from or put back in a pack | the client whose hand did it: takes the pack's authority, changes the count, spawns the rod into the hand or despawns it | the count is state; the rod is an ordinary spawn. Two hands on the last rod at once can both get one: accepted, as for two feeds at once |
| The packs on a loader | the loader, decided by the master | the loader's state, one entry per place it has for a pack, like a slot's occupant |
| A take (quantity −1, the `LoadingModule` holds one; an empty pack despawned) | the master only | state. Every client plays the feed animation when it sees the change; a late joiner sees the result |
| Whether a `LoadingModule` holds an item | the component, decided by the master | `ComponentNetwork` |
| The behaviour's fuel level, and the consume when it refills | the component; the consume is the master's | `ComponentNetwork`, like charge and heat |
| Whether an item in the port is still held | the item's holder | a replicated holder fact, never the local `isSelected` (rule 6): the master takes only an item nobody holds |
| A non-master's hand feeding an item | that client: it moves the item in, loads here, and despawns it | the load: an ItemLoaded message to the master; the item's motion: its `NetworkRigidbody3D` |
| The port's doors | nobody | worked out on every client from where the item is |

## Fuel rod glass: binocular luster (planned, logged 2026-10-08)

To build once Vulps says the loading work is done. The glass shows **bright blue to one eye and a very
transparent blue to the other.** The brain cannot fuse the two, and the panel seems to shimmer or glow from
within (binocular luster, with some rivalry): an unsettling look for something radioactive.

- **It works in Somnium:** Vulps hit it by accident in Grow A Garden, when a skybox rendered without its stereo
  pass. Across the whole view it was unbearable, so it stays on **small surfaces only** (the rod's glass panels).
- **Keep the two eyes the same hue and pattern; vary brightness and transparency only.** Mismatched patterns are
  what make rivalry uncomfortable. No flashing.
- **Each eye's look must stand on its own:** people with a strong dominant eye or no stereo vision see one of
  them. The 2D client, mirrors and screenshots render one eye, and should get the full blue one (principle 12).
- **How:** a URP shader on `FuelRodGlass` that picks the brightness and alpha by `unity_StereoEyeIndex`. The
  project renders single pass instanced (OpenXR render mode 1).

