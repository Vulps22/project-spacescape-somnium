# To do

One line per piece of work, pointing to the doc that holds everything about its topic. Notes and design live
in the topic docs, never here; an item is removed once it is done. Build everything with networking in mind
(`networking.md` → Rules). What is being polished from the last test is in `polish.md`.

## NEXT (insisted on; only a major bug comes first)

- Replace every component model in the loader's look: battery, radar, disruptor, reactor. `component-models.md`

## Weapons and data

- Test in-world: the radar's data reaching the gun over conduits, auto-aim, and a hand fighting it. `radar.md`, `weapons.md`
- The data upgrade: data only along branches that carry it (any wired conduit carries it for now). `data.md`
- Logic board (unrefined). `data.md`

## Power, reactor and repair

- The component hologram. `component-hologram.md`
- Addon slots on components, wired to the component's functions in its hologram. `component-hologram.md`
- Repair games, in the component hologram. `repair.md`
- Repairing popped conduits. `repair.md`
- Reactor hum, generated in code. `reactor.md`
- Open: an overproducing reactor bakes its own cables. `reactor.md`
- Open: should a running reactor's input take a cell's full discharge as heat? `power.md`
- Open: the battery tier ladder. `power.md`
- Open: rebalance and scale up every power number (a 100 W cell feeding a cannon; a 1 MW reactor). `power.md`
- Coolant consumption, and the coolant upgrade for branches. `reactor.md`, `conduits.md`
- Loading: test in-world the loader's front rails, eject-takes-packs, and item conduits (growing runs by hand, tubes, sending to the reactor); then lay out S3, S4 and S5. `loading.md`
- Loaders, next pass: serpentine queues using both levels, holding more packs (sketch saved). `loading.md`
- Fuel rod glass: binocular luster shader, once the loading work is done. `loading.md`
- Weight, and carrying together. `grid.md`
- Sounds ignore Somnium's volume controls, even on the Environment layer (pinned). `README.md`

## Conduits

- Measure the cost: the world with every conduit wired in a line, and the frame rate. `conduits.md`

## The ship

- Room and corridor prefabs. `ship-structure.md`
- Loading bay doors for engineering. `ship-structure.md`
- Writeable signs. `ship-structure.md`
- Climbing. `ship-structure.md`
- A room built round the training displays; a one-hand rewiring display. `training-room.md`
- Private voiced tutorial in a local sub-sim (near the end of development). `training-room.md`

## Interaction

- Interaction rework: stock XRI for the rod lever and hologram handles. `interaction.md`
- Playing in 2D (parked). `2d.md`

## Someday

- Felix, the ship's cat. `felix.md`
