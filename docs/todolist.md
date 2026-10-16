---
aliases:
  - "To do"
tags:
  - tracking
---
# To do

One line per piece of work, pointing to the doc that holds everything about its topic. Notes and design live
in the topic docs, never here; an item is removed once it is done. Build everything with networking in mind
([[networking#Rules|networking → Rules]]). What is being polished from the last test is in [[polish]].

## Weapons and data

- [ ] Test in-world: the radar's data reaching the gun over conduits, auto-aim, and a hand fighting it. [[radar]], [[weapons]]
- [ ] The data upgrade: data only along branches that carry it (any wired conduit carries it for now). [[data]]
- [ ] Logic board (unrefined). [[data]]
- [ ] G slots: the empty G slot's box (G1 to G5), then the two-way gun fits check. [[slots#G slots|slots → G slots]]
- [ ] Disruptor refactor to G3. [[weapons#Disruptor refactor to G3|weapons → Disruptor refactor to G3]]

## Power, reactor and repair

- [ ] The component hologram. [[component-hologram]]
- [ ] Addon slots on components, wired to the component's functions in its hologram. [[component-hologram]]
- [ ] Repair games, in the component hologram. [[repair]]
- [ ] Repairing popped conduits. [[repair]]
- [ ] Item conduit merges: several loaders onto one trunk, segment arrows, then splitters. [[item-conduit#Junctions|item-conduit → Junctions]]
- [ ] Item conduits: test in-world (installing, parking in the middle, junctions, items falling out, two players). [[item-conduit#Rules|item-conduit → Rules]]
- [ ] Reactor chambers: test in play and with a second player. [[reactor]]
- [ ] Reactor hum, generated in code. [[reactor]]
- [ ] Open: an overproducing reactor bakes its own cables. [[reactor]]
- [ ] Open: connect heat-ratio heat (coolant, radiators, the grid) and its counters (thermal break, thermostat, breaker, radiator). [[reactor]]
- [ ] Smaller reactors S4 to S1 (Bottle, Pool, Breech, Pile): proposed, not decided. [[reactor#Sizes|reactor → Sizes]]
- [ ] Open: should a running reactor's input take a cell's full discharge as heat? [[power#Why no conduit popped|power → Why no conduit popped]]
- [ ] Open: the battery tier ladder (likely deprecated). [[power#Three cell tiers|power → Three cell tiers]]
- [ ] Open: rebalance and scale up every power number (a 100 W cell feeding a cannon; a 1 MW reactor). [[power#Open the scale of every number (logged 2026-10-08)|power → the scale of every number]]
- [ ] Coolant consumption, and the coolant upgrade for branches. [[reactor]], [[conduits]]
- [ ] Loaders: test in-world the front rails, eject-takes-packs, and sending to the reactor; approve S3, S4 and S5. [[loader]]
- [ ] Loaders, next pass: serpentine queues using both levels, holding more packs (sketch saved). [[loader#Next pass holding more|loader → Next pass]]
- [ ] Fuel rod glass: binocular luster shader, once the loading work is done. [[fuel-rod#Glass binocular luster|fuel-rod → Glass]]
- [ ] Weight, and carrying together. [[grid#Weight and carrying together|grid → Weight and carrying together]]
- [ ] Sounds ignore Somnium's volume controls, even on the Environment layer (pinned). [[tooling]]

## Conduits

- [ ] Redraw engineering's starting wiring for the new S1 batteries' ports (batteries S1 (2) to S1 (5) are not joined). [[conduits#Drawn conduits|conduits → Drawn conduits]]
- [ ] Conduits V2: test in-world with two players and a late joiner (installing both kinds, switches through panels, pops, repair). [[conduits#Cells|conduits → Cells]]
- [ ] Thermal shielding upgrade: raises a branch's pop floor to 1000 °C. [[conduits#Upgrades thermal shielding|conduits → Upgrades: thermal shielding]]

## Flight ([[flight]], in order)

- [ ] Leg 0, the space frame: the ship on a floating origin, the universe drawn around it; check Somnium's far clip, double precision, the sun as a turning light, and outside things in the ship's frame. [[flight]]
- [ ] Leg 1: layout saving through SpacetimeDB (Conduits V2 is built). [[flight]], [[conduits]]
- [ ] Legs 2 to 5: a procgen system, propulsion and the helm, flying around it, then the ship itself. [[flight]]

## The ship

- [ ] Room and corridor prefabs. [[ship-structure]]
- [ ] Loading bay doors for engineering. [[ship-structure]]
- [ ] Writeable signs. [[ship-structure]]
- [ ] Climbing. [[ship-structure]]
- [ ] A room built round the training displays; a one-hand rewiring display. [[training-room]]
- [ ] Private voiced tutorial in a local sub-sim (near the end of development). [[training-room]]

## Interaction

- [ ] Interaction rework: stock XRI for the rod lever and hologram handles. [[interaction]]
- [ ] Playing in 2D (parked). [[2d]]

## Someday

- [ ] Felix, the ship's cat. [[felix]]
