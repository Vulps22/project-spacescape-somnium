# Damage and the hull

**Status: design only (2026-09-28).** Nothing here is built and nothing is decided unless it says
**Decided.** A weapon will eventually do visible damage to the ship, and exposed components should be
particularly vulnerable to a direct hit. This is how that fits the machine we already have, and how
the hull is physically put together.

## The rule: a hit is energy delivered into the lattice

The ship already has per-tile heat, `Integrity`, pops, damage stages (`IntegrityModule`'s
`_damageThresholds` / `_damageModels`) and the `Popped` / `Damaged` / `Lost` events (`power.md`).
A weapon does not bring its own HP system. **A hit puts energy into cells, and the existing rules
decide what it does.** That is "one abstraction" (`README.md`) applied to combat.

## Layers, outside in

| Layer | Exists? | What a hit does |
|---|---|---|
| **Shield** | a load in `modules.md`, not built | spends its charge to absorb the hit; once empty, the hit passes through |
| **Hull** | no | armour with integrity: damaged, then breached into a real hole |
| **Wall panel** | planned (access panels, `todolist.md`) | a thin layer; once off, the cell is exposed |
| **Tile** (conduit, component) | yes | heat and integrity damage through the sim |

A hit passes through the layers in order and each one absorbs its share. **"Exposed components are
especially vulnerable" is not a special rule:** a component whose panel is off, or that sits behind
a breach, simply has nothing in front of it.

This keeps principle 3 (crises are self-inflicted). The enemy firing comes from outside; whether it
hurts is down to the crew: panels left off after maintenance, a shield on a starved branch, a breach
nobody patched.

And power × heat does the rest. A laser dumping heat into an idle cable does little; the same shot
into the trunk carrying the reactor's output pushes power × heat over the line and the trunk pops.
The same shot does different harm depending on how the ship is routed, with no extra rule.

## Two kinds of hit (proposed)

- **Thermal** (lasers): adds heat to the cells it hits, and the sim decides. The damage is delayed
  and spreads along the metal.
- **Kinetic** (projectiles): takes integrity off directly. It damages hull, severs conduits and
  knocks components down a damage stage.

**Hit-testing walks the lattice.** A shot is a ray stepped cell by cell through the grid, not a
physics query. It is cheap, deterministic, and meets the layers in order.

## How the hull is built

### The hull is a layer of the lattice

`ConduitContainerWall` is already two 0.17 m skins with the conduit channel between them. On an outer
wall, **the outer skin is the hull**:

```
space | hull skin | conduit channel | panel skin | room
```

So a shot passes hull, then conduits, then panel, then room: the layers above, built out of the wall
we already have.

### The grid holds the truth; nothing is modelled as one hull mesh

**The hull is never modelled as a mesh.** Three parts:

**1. A kit, made once in Blender.** A handful of small meshes, each exactly one cell (0.5 m) with its
pivot at the cell centre, exported as one FBX of separate objects:

| Piece | Where it goes |
|---|---|
| plate | flat panel; the one used almost everywhere |
| edge | where the hull turns outward |
| inner corner | where two walls meet on the inside |
| outer corner | where two walls meet on the outside |
| rim | a torn edge, around a hole |
| patch | a repaired plate, visibly different |

About six to ten pieces. Scorching, dents and cracks come from the **material** (the damage stage
fed in through vertex colour or a texture channel), so no piece needs a damaged copy.

**2. The ship's shape, painted in Unity.** Which cells have hull is grid data, painted the way
`ConduitLayoutEditor` paints conduits: a Hull mode beside Draw and Erase, stored as packed values
like `ConduitLayout`. It could instead be automatic: every cell face on the outside of a room gets
hull.

**3. The game assembles it at load.** The hull is divided into patches of, say, 4 × 4 cells
(2 × 2 m). For each patch:

1. For each cell, look at it and its neighbours to pick a piece: plate in the middle, edge or corner
   where the hull turns, nothing if breached, a rim beside a hole.
2. Copy that piece's triangles into position and merge the patch into **one mesh**
   (`Mesh.CombineMeshes`).
3. Give the patch one renderer and one collider.

When a cell changes (hit, breached, repaired) only its patch rebuilds, which takes milliseconds, and
changes are rare because hits are events. A whole hull is a few hundred objects, not thousands.

### Why not the alternatives

- **Separate small parts, one object per plate:** thousands of renderers and colliders (the same
  object-count problem as one GameObject per conduit), visible seams where they meet, and damage,
  networking and repair all having to find and ask thousands of objects instead of one table.
- **One solid mesh the game cuts into patches:** splitting arbitrary triangles into grid cells at
  runtime is genuinely hard (UVs, cut edges, triangles spanning cells), and there is nothing to gain.
- **Patches modelled in Blender, one FBX:** works for one fixed ship, but Blender becomes the source
  of truth. Every layout change means re-modelling, object names have to match grid coordinates by
  hand, and every patch needs its own damaged and holed versions. A kit covers any ship shape.
- **A transparent decal where the hull was:** right for *damage*, wrong for *missing*. A breach has
  to be a real hole: you see space through it, it stops blocking hands and shots, and a hit reaches
  whatever is behind it.

### Plates become real objects when they come off

When a cell breaches, spawn a loose plate or debris: a real physical `NetworkObject` that can be
grabbed. **Repair is the reverse:** carry a plate to the hole and let go, and the cell goes back to
being data. That is the addon item's install pattern (`XRGrabInteractable` into a socket), and it
makes repair physical work (principle 1).

The same rule as the conduits (`networking.md`): **data until someone needs to touch it.**

### Handmade art still has a place

Hero pieces (a bridge window, the engine housings, an airlock) are ordinary modelled objects placed
in the scene. Each takes up some cells and shows damage with `IntegrityModule`'s damage models, the
way components already do. The kit covers the large plain areas, handmade pieces cover the parts
players will actually look at.

### The outside

The crew mostly see the hull's inside face, so that is where damage matters most. If the exterior
ever needs to look less boxy, a purely decorative outer shell can show damage from the same cell
data. The grid hull stays underneath, and the true shape stays the lattice.

## Networking

It follows `networking.md`'s rules:

- **The master resolves every hit.** Where it landed and what it did are its decisions.
- **Results are state.** A small int per hull cell (present, material or tier, damage stage) in
  `NetworkBridgeData` banks, like the conduits; whether a panel is on, likewise. Popped bits go in the
  conduit bank, component integrity in each component's `NetworkBridgeData`. A late joiner gets a
  damaged ship for free.
- **The impact itself is a message.** The flash and sound happen once; anything lasting is already
  in the state.

## Repair

Ties into `todolist.md` → *Repairing*: patching hull by carrying plates, refitting panels, replacing
severed conduits, and `Integrity.Restore` for components. A patched cell uses the patch piece, so a
repaired ship shows its history.

## Open

1. **Hull thickness.** Is one skin per cell enough, or does armour stack into several layers
   (plating tiers as a progression axis)?
2. **What a breach does beyond the hole.** Atmosphere, pressure and people are the Barotrauma half of
   the design and a separate system (`the-loop.md` mentions losing life support).
3. **Walls between rooms.** Probably panels only, no hull, but damage could travel inward through
   them.
4. **Shields as a load on the grid,** which is how `modules.md` has them. Does a shield cover the
   whole ship, a side, or a set of cells?
5. **Patch size.** 4 × 4 cells is a guess; measure rebuild time and object count before fixing it.
