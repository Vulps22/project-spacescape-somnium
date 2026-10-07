# To do

One line per piece of work, pointing to the doc that holds everything about its topic. Notes and design live
in the topic docs, never here; an item is removed once it is done. Build everything with networking in mind
(`networking.md` → Rules). What is being polished from the last test is in `polish.md`.

## Weapons and data

- Test the disruptor in-world: push to fire, the charged and firing sounds, aim from another client. `weapons.md`
- Register the Disruptor and the Radar in `SceneNetworking`'s network prefabs. `weapons.md`, `radar.md`
- Test the radar in-world: contacts, cycling, selecting, from another client too. `radar.md`
- The radar publishing to the data network, and the gun auto-aiming at its selected target. `radar.md`, `data.md`
- Data along conduits: settle what is still open, then build `DataGrid`. `data.md`
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
- Coolant consumption, and the coolant upgrade for branches. `reactor.md`, `conduits.md`
- Loading: a `LoadingModule` for fuel, coolant and ammunition. `modules.md`
- Weight, and carrying together. `grid.md`

## Conduits

- Measure the cost: the world with every conduit wired in a line, and the frame rate. `conduits.md`

## The ship

- Room and corridor prefabs. `ship-structure.md`
- Loading bay doors for engineering. `ship-structure.md`
- Writeable signs. `ship-structure.md`
- Climbing. `ship-structure.md`
- A room built round the training displays; a one-hand rewiring display. `training-room.md`

## Interaction

- Interaction rework: stock XRI for the rod lever and hologram handles. `interaction.md`
- Playing in 2D (parked). `2d.md`

## Someday

- Felix, the ship's cat. `felix.md`
