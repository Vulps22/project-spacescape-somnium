# Data

How information moves round the ship: data along conduits, and logic boards. The radar is in `radar.md`.
**The data network and data ports are built (2026-10-07), untested in-world;** the data upgrade and logic
boards are not.

## Built (2026-10-07, untested in-world)

- **Data ports:** `FlowDirection.Data`, set on a component's port like In or Out (the port inspector has
  **Add Data**). Its ring is red (`ComponentData.mat` on the Port prefab). No power crosses it:
  `SendsAt`/`AcceptsAt` never answer for it, and `DataAt` does. The Radar and the Disruptor each have one on
  the back face at (4, 0), beside their power In.
  **Every component gets one** (decided 2026-10-09): assume data is, or will be, on every component; its model's
  I/O panel carries In, Data and Out with Data between (`component-models.md`).
- **Joining a data port:** a conduit with a segment on that face. Point the segment's arrow **into the conduit
  (In)**: an Out segment there sends power at a port that takes none, which is wasted as heat like any dead end.
- **`IDataSource` / `DataObject`** (`scripts/Ship/Data`): `Module` and `ConduitAddon` answer `GetData()` with
  null until overridden.
- **`DataGrid`** (`PowerGrid.Current.Data`): groups power nodes into networks. Two conduits join when each has a
  segment on the face between them, whatever the arrows; a conduit joins a component when its segment is on the
  component's data port. A conduit that is off (an open switch) or blown joins nothing. Worked out again only
  when `PowerNode.LinkRevision` (switches, pops, nodes added or removed) or `DataGrid.Invalidate` (faces
  changed, a tile bound or unbound, as a slot's component changes) has moved; otherwise `Read<T>(tile)` is a
  lookup plus each publisher's `GetData()`. The list it returns is reused by the next read of the same kind.

## How data travels (in discussion, from 2026-10-03)


How information moves round the ship. The radar, a bridge fire button and the logic board all wait on this.

**Decided:**
- **Data travels along the conduit network, and only there.** A component, sensor or readout knows only what
  it is connected to. To see the reactor from the bridge, someone runs a connection there, and it can be cut.
- **Only along branches with a data upgrade.** The hologram's upgrade slot, which applies to a whole branch
  (coolant was the only upgrade planned). Power still flows everywhere. One big network of everything means
  finding data upgrades for every branch in it.
- **One upgrade per branch stays.** A branch is cooled or connected, not both: a hot trunk's data takes
  another route. That trade-off is a feature.
- **Data ports on components**, separate from power ports. Merge one into a trunk at a junction (when that
  branch carries the data upgrade), or run a dedicated data line: conduits with the upgrade carrying no power.
- **Both ways along a data line**, whatever way power goes on it.
- **Addons on the line report their state**, if they have one: a switch reports open or closed.
- **Networking: nothing new to send.** Every client has the same conduits, upgrades and component state, so
  each works out the same connections and reads its own copies of the values.
- **No commands. Reading is how one thing acts on another.** A component reads what its network carries and
  decides for itself; nothing tells another part what to do. (When logic boards come, their reading is what
  drives things.)
- **The network carries data, not objects.** A publisher (a module, or an addon) declares a small plain
  record of what it shares, something like `IDataSource<RadarData>`, and only that record travels. A reader
  gets copies of the records on its own network, one per publisher, never a reference to the publisher, so it
  cannot reach past what is sent. Roughly:
  `IReadOnlyList<RadarData> seen = DataGrid.Read<RadarData>(tile);` where `tile` is the reader's own tile,
  since what is connected depends on where you ask from. The list is kept by `DataGrid`, not made per call.
- **Event-driven, live reads, no throttle.** `DataGrid` works out its networks only when wiring, an upgrade or
  a slot's occupant changes; a read is a lookup. No request queue: an order of serving could differ between
  clients. If data should feel delayed, that is a later mechanic: each network snapshots its records a few
  times a second, the natural place for §6's fade with distance too.
- **Targeting** (who detects, who selects, what the radar publishes): `radar.md`.

- **What publishes and what travels (decided 2026-10-03).** Anything that shares data implements
  `IDataSource { DataObject GetData(); }`. `Module` implements it as a public virtual returning `null`
  (publishes nothing), so a behaviour, capacitor or integrity module overrides it when it has something to
  share; `ConduitAddon` implements it the same way, since addons report too (a switch: open or closed). Each
  publisher returns its own subclass of `DataObject` (`BatteryData`, `RadarData`, `SwitchData`), typed as
  `DataObject`: Unity cannot compile covariant return types (`override BatteryData GetData()`), so the override
  keeps the base type. `DataGrid.Read<BatteryData>(tile)` collects every `IDataSource` on the reader's network
  and filters to that type, so readers still get typed data. Data objects are read-only (set when made,
  `get`-only), so a reader holding one cannot change what other readers see; a fresh one per read is cheap.

  ```csharp
  public abstract class DataObject { }
  public sealed class BatteryData : DataObject { public float Charge { get; } public float Watts { get; } }
  public sealed class RadarData : DataObject { public IReadOnlyList<Contact> Contacts { get; } public Contact? Selected { get; } }
  ```

- **An open switch or a popped conduit cuts data** (decided 2026-10-03): a link is conduits that currently
  carry power across.
- **Components join through a data port** (decided 2026-10-07), separate from their power ports and shown
  **red**. A conduit with a segment on that cell face links the component to the data network, whichever way
  the segment's arrow points; data ignores power's direction. Power ports carry no data.
- **For this stage, data travels along any wired conduit** (decided 2026-10-03). The data upgrade requirement
  comes later as its own step: there are no upgrade items, nothing installs into the upgrade circle, and
  nothing works out branches yet.

**Open:** whether data is instant or fades with distance (§6); and the radar's own open questions (`radar.md`).

## Logic board (unrefined, logged 2026-10-03)


**Gated behind the data discussion** (how information moves round the ship), which the radar and a bridge fire
button are waiting on too. Nothing here is decided.

**The picture:** a component that sits in a slot like any other, with a board you open (as a hologram, or a
panel on its face). On it you place **logic blocks** as physical pieces, the way addon items go into a slot:
AND, OR, NOT, compare, timer, latch. You wire them from **inputs** (sensors that read one thing where they are
fitted: a battery's charge, a conduit's heat, a switch's position, the reactor's output) to **outputs** (things
it can drive: a switch addon, a breaker, a gun's fire, a lamp).

**Why it fits:**
- **Automation is progression (`principles.md` §7).** The board is the automation, and it is a component, so
  it fails: it can overheat, misfire when worn, starve when its branch does, or be set up wrong. The best
  fault in the principles is the board doing exactly what someone told it last week: "the gun will not fire
  because of a number someone set".
- **It makes the crew's knowledge physical.** "If battery B drops below 20%, open the gun's branch" stops being
  something one person remembers and becomes a thing in the ship another crew member can find, read and rewire.
- **It is the data layer** the radar and the bridge need, built from the same physical grammar as everything
  else rather than a menu.

**Risks:**
- **It must not become a global readout.** A board that shows any value from anywhere is the status screen §2
  forbids. Inputs come from sensors physically fitted at the thing they read, and signals travel over
  something you can trace.
- **It must not automate the ship away.** §7: the difficulty ceiling must rise faster than the automation
  ceiling. Boards have limited slots, cost power, and fail.

**Two tiers of logic (2026-10-03).** Logic comes in two layers, the second in addition to the first:

1. **Power logic: gates as conduit addons.** The ship already moves a signal everywhere: power. A gate is an addon
   that decides whether power crosses its conduit from what is arriving at its faces: input, rule, output. The
   switch is already one (its rule set by a hand). **AND / OR** are junctions with a rule (the In segments are
   inputs, the Out the output: all live, or any live); **NOT** and **relays** follow (pass the main line while a
   control input is live); a **threshold** gate passes only above X watts (the breaker the design wanted). Inputs
   are the power state of the conduit's own faces: on/off, or a number in watts. No data network involved: the
   most local, physical, traceable input there is.
   - Mostly built already: an addon decides whether power crosses (`ConduitAddon.Conducts`, as the switch does);
     a gate decides it from its inputs instead of a hand.
   - Each gate decides from the previous tick's inflows: one tick's delay per gate, the same on every client.
   - Logic costs power and makes heat: an input reaching a closed gate dead-ends and heats that tile (§7, §9).
   - Networking: a gate's rule and settings are its addon state; whether it is passing comes out of the sim the
     master already runs.
2. **Data logic: the richer grid.** Not on/off: real values (charge, watts, a selected target) carried on the data
   network to **component nodes** (below), for what has to be read from far away or is more than power.

**Possible foundation (2026-10-03):** the component hologram's addon slots (`component-hologram.md`). A
component's nodes (its functions and settings) are the logic's inputs and outputs, wired like a conduit, so a
logic block could be an addon slotted into a component and wired between nodes, e.g. "fire when charged and a
target is selected": the charge node and the selected target in, the fire node out.

**To decide (in the data discussion):**
1. **How signals travel:** their own cables, a second grid beside the conduits, or riding the power conduits.
   Their own cables read best; riding conduits is least new work.
2. **What a signal is:** on/off only, or numbers too (charge 0 to 100%, watts). On/off is simpler and more
   readable; numbers allow "below 20%".
3. **How it is programmed:** placing physical blocks and wiring them in a hologram suits VR and the 2D rule
   (`2d.md`); a text or code screen does not.
4. **Networking:** the board's layout is state, kept by the master like conduit edits; evaluation runs on the
   master (like failure rolls) or on every client from the same inputs.
