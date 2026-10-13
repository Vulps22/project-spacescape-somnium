# The S3 reactor, "The Pool" (docs/reactor.md -> S3: The Pool): an open-topped octagonal tank of water, a fuel rod
# floating in the middle of it held between the control blades. The pool is centred on the item port's cell, a
# quarter-metre off the slot's centre (8 cells: the centre is a cell edge). A fixed grated walkway lid at the tank's rim and
# an open grated bridge over it, carrying the hatch (the item port on top) and the control blades' drive motors, so
# the pool can be seen into from above. A window band down the front with gauge marks; the control bay (the S5's
# profile: the lever on its deck, the screen above) on the front. 8 x 8 x 8 cells, a 4 m cube.
#
# Its own objects, for the prefab to move and light: "Water" (its origin at the tank's floor; its height is the
# coolant), "Blades" (its origin at their tops, fully in; withdrawn they clear the water), "Cables" (origin at the
# motors; they shorten as the blades rise). The rod is the runtime one (ReactorChamberRods), glowing as every
# reactor's does.
#
#   Draft:  blender -b --python Blender/Reactors/ReactorS3.py -- Blender/Reactors/ReactorS3.blend
#   Final:  blender -b --python Blender/Reactors/ReactorS3.py -- Blender/Reactors/ReactorS3.blend "Assets/#User/SpaceScape/Meshes/ReactorS3.fbx"
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from reactor_parts import *

b = Reactor(2.0, 2.0, 2.0, post=0.18)
WATER = b.add_material("ReactorWater", (0.1, 0.45, 1.0), 0.0, 0.05, emit=(0.05, 0.3, 0.9), alpha=0.3)
CX, CZ = -0.25, -0.25           # the item port's cell, nearest the centre; the pool, rod and bridge are centred on it
R_OUT, WALL = 1.5, 0.12         # the tank, to its outer corners, and its walls' thickness
TANK_FLOOR = b.base + 0.15      # the tank's floor inside, where spent rods drop into the base
TANK_TOP = 1.5                  # the rim; the lid's walking surface is flush with it
LID_T = 0.05
WATER_FULL = 0.25               # a full drum: over the rod, low enough that withdrawn blades clear it
WINDOW = (-1.35, 1.0)           # the front window band, Unity y
ROD_BOTTOM = -1.1               # the rod floats in the middle of the pool
BLADE_TOP = ROD_BOTTOM + ROD_LEN + 0.05     # fully in, the blades hold the rod along its length
BLADE_LEN = ROD_LEN + 0.05
BLADE_TRAVEL = (TANK_TOP - LID_T - 0.08) - BLADE_TOP    # withdrawn, their tops just under the lid, out of the water
BLADE_OFF = 0.36                # four radial fins round the rod, holding it, their inner edges just clear of it
MOTOR_OFF = 0.56                # the motors over them, on the bridge
BRIDGE_Y = 1.78                 # the bridge's underside; its deck is flush with the top
BRIDGE_Z = (CZ - 0.66, CZ + 0.66)
STRINGER = 0.09
GRATE, GRATE_PITCH = 0.02, 0.12 # grate bars: width and spacing, lid and bridge deck
PORT_BOTTOM = 2.0 - 0.36       # the port's block: the item port sinks 0.29 m
DUCT_IN, DUCT_WALL = 0.3, 0.05  # the duct, inside half-width (a rod is 0.44 across) and wall
BAY_X = (1.6 - PANEL_W, 1.6)    # the control bay, the viewer's left, clear of the window
DECK_FRONT = 1.88               # just inside the plinth's front edge
IO = [-0.75, -0.25, 0.25]
LEVEL, RODS = 0.8, 0.5          # draft preview only: coolant left, blades withdrawn
BLADES = ((BLADE_OFF, 0), (-BLADE_OFF, 0), (0, BLADE_OFF), (0, -BLADE_OFF))


def octagon(r):
    """The tank's corners at radius r, Unity (x, z): flats facing front, back and the sides."""
    return [(CX + r * math.cos(math.radians(22.5 + 45 * k)), CZ + r * math.sin(math.radians(22.5 + 45 * k))) for k in range(8)]


def slab(bm, a, b2, c, d, y0, y1, mat):
    """A solid from the quad a b2 c d (Unity x, z) between heights y0 and y1."""
    lo = [bm.verts.new(U((p[0], y0, p[1]))) for p in (a, b2, c, d)]
    hi = [bm.verts.new(U((p[0], y1, p[1]))) for p in (a, b2, c, d)]
    faces = [bm.faces.new(lo), bm.faces.new(list(reversed(hi)))]
    for i in range(4):
        j = (i + 1) % 4
        faces.append(bm.faces.new((lo[i], lo[j], hi[j], hi[i])))
    for f in faces: f.material_index = mat
    outward(bm, faces)


def disc(bm, r, y0, y1, mat):
    """A solid octagon of corner radius r between heights y0 and y1."""
    pts = octagon(r)
    lo = [bm.verts.new(U((x, y0, z))) for x, z in pts]
    hi = [bm.verts.new(U((x, y1, z))) for x, z in pts]
    faces = [bm.faces.new(lo), bm.faces.new(list(reversed(hi)))]
    for i in range(8):
        j = (i + 1) % 8
        faces.append(bm.faces.new((lo[i], lo[j], hi[j], hi[i])))
    for f in faces: f.material_index = mat
    outward(bm, faces)


frame, bm = b.frame()
io_inner = b.io_panel(bm, IO)
deck, screen = b.control_bay(bm, *BAY_X, DECK_FRONT)
# The bridge: two stringers along x into the side top beams, cross ties, and a grated deck flush with the top.
z0, z1 = BRIDGE_Z
for z in (z0, z1):
    b.ub(bm, (-b.px, BRIDGE_Y, z - STRINGER / 2), (b.px, b.top, z + STRINGER / 2), METAL, 0.012)
for x in [-1.5 + 0.5 * i for i in range(7)]:
    b.ub(bm, (x - 0.03, BRIDGE_Y + 0.04, z0), (x + 0.03, b.top - 0.03, z1), METAL, 0.006)
for x in [-b.px + 0.1 + GRATE_PITCH * i for i in range(int((2 * b.px - 0.2) / GRATE_PITCH) + 1)]:
    if abs(x - CX) < COLLAR / 2: continue                               # the hatch stands there
    b.ub(bm, (x - GRATE / 2, b.top - 0.03, z0), (x + GRATE / 2, b.top, z1), DARK)
# The hatch: the item port's block, as deep as the port sinks, amber round its top; under it a square duct down to the
# tank's rim, so a rod drops through it into the water. Its walls stand round the lid's opening, closing it off.
b.port_collar(bm, CX, CZ, PORT_BOTTOM)
di, do = DUCT_IN, DUCT_IN + DUCT_WALL
for (x0, za, x1, zb) in ((-do, -do, do, -di), (-do, di, do, do), (-do, -di, -di, di), (di, -di, do, di)):
    b.ub(bm, (CX + x0, TANK_TOP, CZ + za), (CX + x1, PORT_BOTTOM, CZ + zb), METAL, 0.008)
for y in (TANK_TOP + 0.04, PORT_BOTTOM - 0.04):                         # flanges at both ends
    b.ub(bm, (CX - do - 0.03, y - 0.025, CZ - do - 0.03), (CX + do + 0.03, y + 0.025, CZ + do + 0.03), DARK, 0.006)
for dx, dz in BLADES:                                                   # the drive motors, exposed in the bridge
    x, z = CX + dx / BLADE_OFF * MOTOR_OFF, CZ + dz / BLADE_OFF * MOTOR_OFF
    b.prism(bm, (x, (BRIDGE_Y + b.top - 0.03) / 2, z), "y", b.top - 0.03 - BRIDGE_Y, 0.1, DARK, segments=12)
    b.prism(bm, (x, BRIDGE_Y + 0.03, z), "y", 0.05, 0.12, METAL, segments=12)
finish(frame, bm)

tank, bm = b.obj("Tank")
outer, inner = octagon(R_OUT), octagon(R_OUT - WALL)
disc(bm, R_OUT, b.base, TANK_FLOOR, METAL)
for k in range(8):
    j = (k + 1) % 8
    quad = (outer[k], outer[j], inner[j], inner[k])
    if k == 1:                                                          # the front flat: the window band
        slab(bm, *quad, TANK_FLOOR, WINDOW[0], METAL)
        slab(bm, *quad, WINDOW[1], TANK_TOP, METAL)
        slab(bm, *quad, WINDOW[0], WINDOW[1], GLASS)
    else:
        slab(bm, *quad, TANK_FLOOR, TANK_TOP, METAL)
band_out, band_in = octagon(R_OUT + 0.02), octagon(R_OUT - 0.01)
for y in (b.base + 0.25, (TANK_FLOOR + TANK_TOP) / 2, TANK_TOP - 0.06):            # bands round the walls
    for k in range(8):
        if k == 1 and WINDOW[0] < y < WINDOW[1]: continue                           # not across the window
        j = (k + 1) % 8
        slab(bm, band_out[k], band_out[j], band_in[j], band_in[k], y - 0.04, y + 0.04, DARK)
hw, fz = outer[1][0] - CX, outer[1][1]                                  # the window's frame and the gauge marks
for y in WINDOW:
    b.ub(bm, (CX - hw, y - 0.03, fz - 0.02), (CX + hw, y + 0.03, fz + 0.03), METAL, 0.008)
for sx in (-1, 1):
    b.ub(bm, (CX + sx * hw - 0.04, WINDOW[0], fz - 0.02), (CX + sx * hw + 0.04, WINDOW[1], fz + 0.03), METAL, 0.008)
for i in range(1, 10):                                                  # from the window's foot to a full drum
    y = WINDOW[0] + (WATER_FULL - WINDOW[0]) * i / 9
    w = 0.12 if i % 2 == 0 else 0.07
    b.ub(bm, (CX + hw - 0.04 - w, y - 0.008, fz + 0.03), (CX + hw - 0.04, y + 0.008, fz + 0.04), AMBER)
# The lid: a fixed grate flush with the rim, solid to stand on, open only under the duct.
F = (R_OUT - WALL) * math.cos(math.radians(22.5)) - 0.01


def half_chord(o):
    return min(F, F * math.sqrt(2) - abs(o))


offsets = [-F + 0.08 + GRATE_PITCH * i for i in range(int((2 * F - 0.16) / GRATE_PITCH) + 1)]
def spans(o, h, c, other):
    """The lid's bar at offset o, half length h, split round the duct's opening (centred on c along the bar, other
    across it)."""
    if abs(o - other) >= DUCT_IN: return [(-h, h)]
    return [(-h, c - DUCT_IN), (c + DUCT_IN, h)]


for o in offsets:
    for a0, a1 in spans(o, half_chord(o), 0, 0):                                      # bars along x
        b.ub(bm, (CX + a0, TANK_TOP - LID_T, CZ + o - GRATE / 2), (CX + a1, TANK_TOP, CZ + o + GRATE / 2), DARK)
for o in offsets[::3]:
    for a0, a1 in spans(o, half_chord(o), 0, 0):                                      # bearers under them
        b.ub(bm, (CX + o - 0.025, TANK_TOP - LID_T - 0.03, CZ + a0), (CX + o + 0.025, TANK_TOP - LID_T, CZ + a1), METAL)
for i in range(8):                                                                   # the lid's rim, on the walls
    j = (i + 1) % 8
    slab(bm, outer[i], outer[j], inner[j], inner[i], TANK_TOP, TANK_TOP + 0.04, METAL)
finish_solids(tank, bm)

water, bm = b.obj("Water")
disc(bm, R_OUT - WALL - 0.005, TANK_FLOOR, WATER_FULL, WATER)
finish(water, bm)
set_origin(water, (CX, TANK_FLOOR, CZ))

blades, bm = b.obj("Blades")
for dx, dz in BLADES:
    w = (0.1, 0.025) if dx else (0.025, 0.1)                         # edge-on to the rod, so it shows between
    x, z = CX + dx, CZ + dz
    b.ub(bm, (x - w[0], BLADE_TOP - BLADE_LEN, z - w[1]), (x + w[0], BLADE_TOP, z + w[1]), DARK, 0.006)
    b.ub(bm, (x - w[0] - 0.01, BLADE_TOP - 0.06, z - w[1] - 0.01), (x + w[0] + 0.01, BLADE_TOP, z + w[1] + 0.01), METAL)
finish(blades, bm)
set_origin(blades, (CX, BLADE_TOP, CZ))

cables, bm = b.obj("Cables")                         # from each blade's top up through the lid to its motor
for dx, dz in BLADES:
    mx, mz = CX + dx / BLADE_OFF * MOTOR_OFF, CZ + dz / BLADE_OFF * MOTOR_OFF
    b.ubeam(bm, (mx, BRIDGE_Y, mz), (CX + dx, BLADE_TOP, CZ + dz), 0.02, METAL)
finish(cables, bm)
set_origin(cables, (CX, BRIDGE_Y, CZ))

b.draft_rod(CX, CZ, ROD_BOTTOM, (0.3, 0.7, 1.0))
b.draft_lever(sum(BAY_X) / 2, deck)

# Wiring: In up the back and forward over the rim to the bridge's motors (the house load holds the blades); Out from
# the tank's base into the I/O panel; Data round the tank's left side into the back of the screen.
wires, bm = b.obj("Wiring")
b.run(bm, [(IO[0], b.io_top, -1.72), (IO[0], 1.65, -1.72), (IO[0], 1.65, z0), (IO[0], BRIDGE_Y, z0)])
b.run(bm, [(IO[2], b.floor + 0.25, io_inner), (IO[2], b.floor + 0.25, -R_OUT * 0.9)])
sx, sy, sz = screen
b.run(bm, [(IO[1], b.base + 0.03, io_inner), (IO[1], b.base + 0.03, -1.7), (1.62, b.base + 0.03, -1.7),
           (1.62, b.base + 0.03, sz - 0.08), (1.5, b.base + 0.03, sz - 0.08), (1.5, sy, sz - 0.08), (1.5, sy, sz)], t=0.04)
finish(wires, bm)

if not b.out_fbx:                                    # draft: the water at its level, the blades part withdrawn
    water.scale = (1, 1, LEVEL)
    lift = BLADE_TRAVEL * RODS
    blades.location = U((CX, BLADE_TOP + lift, CZ))
    cables.scale = (1, 1, (BRIDGE_Y - BLADE_TOP - lift) / (BRIDGE_Y - BLADE_TOP))
b.finish_all(shots_for(2.0), (lambda: None, [("down", (1.4, 5.2, 3.0), (CX, 0.0, CZ)), ("hatch", (1.1, 1.66, 1.3), (CX, 1.66, CZ))]))
