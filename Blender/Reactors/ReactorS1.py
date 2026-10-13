# The S1 reactor, "RTG" (docs/reactor.md -> S1: RTG): a squat thermoelectric drum covered in radiating fins, in a
# light braced frame. A cartridge on rails slides out of its front like a heavy drawer; open at its sides, it holds a
# fuel rod upright between two clamps, put in by hand while it is out. No screen and no lever: the fins glow with heat and a strip on the
# cartridge's face shows the rod's fuel. 4 x 4 x 4 cells, a 2 m cube.
#
# Exported with the cartridge home as its own object, "Cartridge", its origin at its front face's centre; it slides
# out along +z (Unity). The fuel strip's seven segments (Fuel 1 ... Fuel 7, filling the viewer's right to left) are
# its children. "Fins" is its own object so the prefab can light it by heat.
#
#   Draft:  blender -b --python Blender/Reactors/ReactorS1.py -- Blender/Reactors/ReactorS1.blend
#   Final:  blender -b --python Blender/Reactors/ReactorS1.py -- Blender/Reactors/ReactorS1.blend "Assets/#User/SpaceScape/Meshes/ReactorS1.fbx"
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from reactor_parts import *

b = Reactor(1.0, 1.0, 1.0)
HEAT = b.add_material("ReactorHeat", (0.35, 0.12, 0.05), 0.6, 0.5, emit=(1.0, 0.35, 0.05))
DRUM_R = 0.42                   # the drum's body
FIN_R = 0.64                    # the fins' outer edge, clear of the frame's braces
DRUM_BOTTOM, DRUM_TOP = b.base + 0.06, 0.55
CAP_TOP = 0.63
FINS = 28
FIN_T = 0.035
GAP = math.radians(28)          # no fins this far either side of the front, where the cartridge housing stands
HOUSING = (0.33, b.base + 0.1, 0.76, 0.74)      # half-width, bottom, top, front face (Unity z)
CART_W, CART_BOTTOM, CART_TOP = 0.27, b.base + 0.14, 0.71   # tall enough for the rod and a point at each end
CART_BACK, CART_FRONT = -0.4, 0.76               # home; the rod stands at z 0 inside it
ROD_Z = 0.0
PLATE_T = 0.03                  # the cartridge's top and bottom plates
POINT_W, POINT_H = 0.12, 0.12   # the clamps: four-sided points, half-width at the plate and height, tip to the rod's tips
OPEN = 1.02                     # how far it slides out: enough for the rod's place to clear the housing
# The I/O panel: Data and Out only (no house load, so nothing to feed): Data on the viewer's right.
IO_DATA, IO_OUT = -0.25, 0.25
FUEL = 0.6                      # draft preview only

frame, bm = b.frame()
io_inner = b.io_panel(bm, [IO_DATA, IO_OUT])
finish(frame, bm)

drum, bm = b.obj("Drum")
b.prism(bm, (0, (DRUM_BOTTOM + DRUM_TOP) / 2, 0), "y", DRUM_TOP - DRUM_BOTTOM, DRUM_R, METAL, segments=20, chamfer=0.01)
b.prism(bm, (0, (DRUM_TOP + CAP_TOP) / 2, 0), "y", CAP_TOP - DRUM_TOP, DRUM_R + 0.05, DARK, segments=20, chamfer=0.02)
b.prism(bm, (0, CAP_TOP + 0.02, 0), "y", 0.04, 0.2, METAL, segments=12, chamfer=0.01)                 # top boss
b.prism(bm, (0, b.base + 0.03, 0), "y", 0.06, FIN_R + 0.03, DARK, segments=20, chamfer=0.01)       # foot ring
hw, hb, ht, hf = HOUSING
b.ub(bm, (-hw, hb, 0.2), (hw, ht, hf), METAL, 0.02)                                                # cartridge housing
b.ub(bm, (-hw - 0.02, ht - 0.03, hf - 0.06), (hw + 0.02, ht + 0.01, hf + 0.005), AMBER, 0.006)    # amber over the mouth
b.ub(bm, (-CART_W - 0.015, CART_BOTTOM - 0.015, hf - 0.01), (CART_W + 0.015, CART_TOP + 0.015, hf + 0.002), DARK)   # the mouth
for s in (-1, 1):                                                                                  # rails, out of the mouth
    b.ub(bm, (s * (CART_W + 0.005) - 0.02, CART_BOTTOM - 0.04, 0.3), (s * (CART_W + 0.005) + 0.02, CART_BOTTOM, hf + 0.03), METAL)
finish(drum, bm)

fins, bm = b.obj("Fins")
for k in range(FINS):
    a = 2 * math.pi * (k + 0.5) / FINS
    if abs(math.atan2(math.sin(a), math.cos(a))) < GAP: continue                 # a = 0 points at the front (+z)
    s, c = math.sin(a), math.cos(a)
    r0, r1 = DRUM_R - 0.02, FIN_R
    for y0, y1 in ((DRUM_BOTTOM + 0.04, DRUM_TOP - 0.03),):
        p = [(r0 * s - c * FIN_T / 2, r0 * c + s * FIN_T / 2), (r1 * s - c * FIN_T / 2, r1 * c + s * FIN_T / 2),
             (r1 * s + c * FIN_T / 2, r1 * c - s * FIN_T / 2), (r0 * s + c * FIN_T / 2, r0 * c - s * FIN_T / 2)]
        lo = [bm.verts.new(U((x, y0, z))) for x, z in p]
        hi = [bm.verts.new(U((x, y1, z))) for x, z in p]
        bm.faces.new(lo); bm.faces.new(list(reversed(hi)))
        for i in range(4):
            j = (i + 1) % 4
            bm.faces.new((lo[i], lo[j], hi[j], hi[i]))
for f in bm.faces: f.material_index = HEAT
finish(fins, bm)
b.look(fins, (0.85, 0.28, 0.04), 0.7)

def pyramid(bm, base, half, height, mat):
    """A four-sided point: a square base of half-width half at Unity point base, its tip height above it (below, if
    negative)."""
    x, y, z = base
    corners = [bm.verts.new(U((x + sx * half, y, z + sz * half))) for sx, sz in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
    tip = bm.verts.new(U((x, y + height, z)))
    faces = [bm.faces.new(corners)] + [bm.faces.new((corners[i], corners[(i + 1) % 4], tip)) for i in range(4)]
    for f in faces: f.material_index = mat
    outward(bm, faces)


cart, bm = b.obj("Cartridge")
# Open on both sides: a top and a bottom plate between the front and back panes, and a clamp through the middle of
# each plate. The rod stands upright between the clamps, put in from the side while the cartridge is out.
b.ub(bm, (-CART_W, CART_BOTTOM, CART_BACK), (CART_W, CART_BOTTOM + PLATE_T, CART_FRONT - 0.04), METAL, 0.01)    # bottom plate
b.ub(bm, (-CART_W, CART_TOP - PLATE_T, CART_BACK), (CART_W, CART_TOP, CART_FRONT - 0.04), METAL, 0.01)          # top plate
b.ub(bm, (-CART_W, CART_BOTTOM, CART_BACK), (CART_W, CART_TOP, CART_BACK + 0.04), METAL, 0.01)                  # back pane
b.ub(bm, (-CART_W - 0.01, CART_BOTTOM - 0.01, CART_FRONT - 0.04), (CART_W + 0.01, CART_TOP + 0.01, CART_FRONT), DARK, 0.012)  # front pane
for plate, sign in ((CART_BOTTOM + PLATE_T, 1), (CART_TOP - PLATE_T, -1)):     # the clamps, pointing at the rod
    b.ub(bm, (-POINT_W - 0.012, plate - 0.008, ROD_Z - POINT_W - 0.012),
         (POINT_W + 0.012, plate + 0.008, ROD_Z + POINT_W + 0.012), AMBER)                            # amber at each base
    pyramid(bm, (0, plate, ROD_Z), POINT_W, sign * POINT_H, DARK)
for s in (-1, 1):                                                                                  # handle
    b.ub(bm, (s * 0.15 - 0.025, -0.05, CART_FRONT), (s * 0.15 + 0.025, 0.0, CART_FRONT + 0.09), METAL, 0.008)
b.ub(bm, (-0.17, -0.05, CART_FRONT + 0.06), (0.17, 0.0, CART_FRONT + 0.1), METAL, 0.01)
b.ub(bm, (-0.2, 0.17, CART_FRONT), (0.2, 0.25, CART_FRONT + 0.01), DARK)                         # strip's dark bed
finish(cart, bm)
set_origin(cart, (0, (CART_BOTTOM + CART_TOP) / 2, CART_FRONT))

for k in range(7):                                                  # the fuel strip, filling the viewer's right to left
    x = -0.165 + k * 0.055
    o, bm = b.obj(f"Fuel {k + 1}")
    b.ub(bm, (x - 0.022, 0.18, CART_FRONT + 0.01), (x + 0.022, 0.24, CART_FRONT + 0.02), CORE)
    finish(o, bm)
    attach(o, cart)
    if k / 7 < FUEL: b.look(o, (0.3, 0.7, 1.0), 1.5)
    else: b.look(o, (0.02, 0.03, 0.05), 0.0)

rod = b.draft_rod(0, ROD_Z, (CART_BOTTOM + CART_TOP - ROD_LEN) / 2, (0.3, 0.7, 1.0))
attach(rod, cart)

# Wiring: Out from the drum's back down into the I/O panel; Data from the panel into the drum.
wires, bm = b.obj("Wiring")
b.run(bm, [(IO_OUT, -0.6, io_inner), (IO_OUT, -0.6, -0.5), (IO_OUT, -0.2, -0.5), (IO_OUT, -0.2, -0.4)])
b.run(bm, [(IO_DATA, b.base + 0.03, io_inner), (IO_DATA, b.base + 0.03, -0.5), (IO_DATA, 0.1, -0.5), (IO_DATA, 0.1, -0.4)], t=0.04)
finish(wires, bm)


def pulled_out():
    cart.location = U((0, (CART_BOTTOM + CART_TOP) / 2, CART_FRONT + OPEN))


d = 4.2
b.finish_all(shots_for(1.0), (pulled_out, [("open34", (-d * 0.55, 1.6, d * 0.85), (0, -0.1, 0.6)),
                                           ("opentop", (0.6, 3.2, 2.6), (0, 0.2, 1.0))]))
