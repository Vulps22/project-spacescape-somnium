---
aliases:
  - "Flight: the course to a ship flying round a procgen system"
tags:
  - arc
---
# Flight: the course to a ship flying round a procgen system

From the ship as it is now (its insides working, nothing outside it) to a ship that flies around a procedurally
generated star system. Plotted with Vulps on 2026-10-10. Each leg gets refined, and its networking settled with
Vulps ([[networking#Rules|networking → Rules]]), when it is started. The galaxy, jumps and contracts beyond it are in
[[the-loop]].

**Where it stands:** inside the ship, power, heat, the reactor, loading, conduits, data, the radar and the gun all
exist as prototypes. Outside it, nothing: no idea of where the ship is, no space, no engines, no helm, no hull.

**Why this order:** the core systems are built before the ship they sit in, since Conduits V2 and layout saving
will change how a ship is put together; and there must be somewhere to fly before there is anything to fly with.
Until Leg 5, the current scene and its test hall are the ship.

## Leg 0: the space frame

**Status (2026-10-10):** approach decided; small checks when started.

- **Players and the ship stay at the world origin in use now.** Players walk the ship freely and Fusion sees
  nothing unusual; nothing in the ship ever moves in Unity space. Moving players on a floating origin while their
  clients report a fixed one is the distant future, if ever: it means fighting Fusion.
- **The universe is drawn around the ship: the ship sits on a floating origin.** Its position and heading in the
  system are numbers the host keeps. Everything outside is placed at its position less the ship's, turned by the
  ship's heading; turning the ship turns the universe outside the windows.
- **Distance:** real geometry out to the camera's far limit; past it, the usual trick: anything still big enough
  to matter (and not dropped by its LOD) is pulled in to a fixed distance and shrunk in proportion, so it looks
  the same size. The stars are the skybox.
- **Windows are plain transparent glass**, since the outside is real geometry.

**Checks when it is started:**
1. **Somnium's camera far clip.** Somnium owns the player camera, so its far clip is read, not set. It is where
   real geometry hands over to shrunk stand-ins.
2. **Precision.** System positions are kept in doubles; only the offset from the ship becomes floats.
3. **The sun as a light that turns with the ship**, and how that sits with however the ship's rooms are lit.
4. **Things outside meeting the ship** happen in the ship's frame: radar targets, gunfire, later collisions. The
   radar's test targets become objects in space, drawn relative to the ship like everything else.

> [!info] Networking
> The host owns the ship's position, velocity and heading and sends them; late joiners get them
> with the rest. Everything placed by the seed and the clock (Leg 2) is worked out on every client.

## Leg 1: Conduits V2 and layout saving

- **Conduits V2** (**Status (2026-10-12):** built, untested in-world): every conduit, power or item, is installed
  where a port or another conduit's segment points into a cell, in one host-owned list of cells
  ([[conduits#Cells|conduits → Cells]]). No parked conduits fill the walls, and wiring is not limited to predefined
  places.
- **Layout saving through SpacetimeDB.** The host saves the ship's layout as it changes and loads it at runtime.
  When it is built it comes before the ship exists, but it is the same idea: save, and load at runtime. The layout
  is the state the host already owns and already sends to everyone (each conduit cell's faces and addons, what is
  in each slot), so the database is one more reader and writer of the host's state; who owns what does not change.
  SpacetimeDB works from a Somnium world (Vulps, confirmed many times): no spike needed.
- **The payoff:** Vulps can host an event to let other people wire the ship, and the wiring stays.
- **To design:** who may write. Anything in a world build is public, the database's address included, so its
  reducers decide who may change the layout (the host only, or anyone during an event Vulps opens).

## Leg 2: a procgen system

- Built from the daily seed ([[galaxy]]): a star, planets on orbits timed from the shared clock, a
  belt, a station or two. Every client places them the same with nothing sent.
- Drawn around the ship (Leg 0), the star as the scene's light.
- The radar detects bodies; a system map on a screen. Not a route planner: there is categorically no route
  planning ([[navigation]]).

## Leg 3: propulsion and the helm

- **Engines**, and thrusters, on the power grid: a big load that makes heat and wears like everything else.
  **Fixed to the hull and not alterable** (Vulps, 2026-10-13; to be refined): an engine is connected to a drive,
  and the drive sets its stats. Not in a slot, S or G ([[slots]]).
- **A helm** with throttle and turn levers (the rod lever's dial already fits, `ILeverDial`).
- **The engines read the helm over the data network**, as the gun reads the radar: nothing commands them
  ([[data]]). Cut the data line and the helm goes dead; cut the power and the engines do.

## Leg 4: flying around it

- Fly to things, match speed, park beside a station.
- Hazards: hits from things in space go into the existing damage model ([[damage]]).
- Hostiles placed by the procgen become what the radar sees and the gun shoots.

## Leg 5: the ship

- The component models ([[component-models]], the insisted NEXT on the to-do list).
- Room and corridor prefabs ([[ship-structure]]), and the hull as a layer of the grid ([[damage]]) with windows.

## Beyond

The jump drive between systems, contracts off the wall, the galaxy and difficulty as depth: [[the-loop]].

## Open

To settle with Vulps along the way.

- How real the flight is: Newtonian drift, or arcade.
- The helm: a lever or two, or a whole station.
- What a jump does to the ship's position, and the system it arrives in.
- Networking per leg: the space frame is cheap; hostiles moving through space need more thought.
