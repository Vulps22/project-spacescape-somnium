# The S2 reactor, "The Rail" (docs/reactor.md -> S2: The Rail): an accelerator-driven core. A glass accelerator tube
# runs along x, ringed by coils, from the gun on the viewer's left to the target vessel on the right, where the fuel
# rod stands under its item port on the top face. Low saddles cradle the tube; the control bay (the S5's profile: the
# lever on its deck, the screen above) is at the gun end on the front. 6 x 4 x 4 cells (3 x 2 x 2 m), long on x, front on a long side.
#
# Its own objects, for the prefab to light: Coil 1 ... Coil 7 (gun to target, chasing toward the target), "Beam" (its
# origin at the gun's muzzle, along -x Unity), "Vessel Bands" (the target's heat tint).
#
#   Draft:  blender -b --python Blender/Reactors/ReactorS2.py -- Blender/Reactors/ReactorS2.blend
#   Final:  blender -b --python Blender/Reactors/ReactorS2.py -- Blender/Reactors/ReactorS2.blend "Assets/#User/SpaceScape/Meshes/ReactorS2.fbx"
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from reactor_parts import *

b = Reactor(1.5, 1.0, 1.0)
AXIS_Y, AXIS_Z = 0.35, -0.25    # the beam line; z on the port's cell row
PORT = (-0.75, -0.25)           # the item port's cell on the top face, over the target
GUN = (0.8, 1.18)               # the gun housing, Unity x
MUZZLE = GUN[0]
VESSEL_R = 0.42                 # the target vessel, to its corners
VESSEL_GLASS = (-0.5, 0.62)     # its glass section, where the rod shows
VESSEL_CAP = 0.8
TUBE_R = 0.18
TUBE_END = PORT[0] + VESSEL_R - 0.05            # the tube runs into the vessel's glass
COILS = 7
COIL_IN, COIL_OUT, COIL_W = TUBE_R + 0.01, 0.27, 0.07
COIL_XS = [MUZZLE - 0.14 - i * (MUZZLE - 0.14 - (TUBE_END + 0.12)) / (COILS - 1) for i in range(COILS)]
SADDLES = [(COIL_XS[i] + COIL_XS[i + 1]) / 2 for i in (1, 4)]
BAY_X = (1.2 - PANEL_W, 1.2)    # the control bay, at the gun end
DECK_FRONT = 0.88               # just inside the plinth's front edge; the deck at the S5's height, as every size's
SCREEN_CLEAR = 0.05             # the screen just above the deck, so the tower fits under the top
IO = [-0.75, -0.25, 0.25]       # In, Data, Out: In on the viewer's right
OUTPUT = 0.7                    # draft preview only

frame, bm = b.frame()
io_inner = b.io_panel(bm, IO)
deck, screen = b.control_bay(bm, *BAY_X, DECK_FRONT, screen_clear=SCREEN_CLEAR)          # at the gun end, the viewer's left
finish(frame, bm)

rail, bm = b.obj("Rail")
g0, g1 = GUN
b.ub(bm, (g0, b.base, AXIS_Z - 0.37), (g1, 0.75, AXIS_Z + 0.37), DARK, 0.03)                       # gun housing
for y in (b.base + 0.2, 0.62):
    b.ub(bm, (g0 - 0.015, y, AXIS_Z - 0.385), (g1 + 0.015, y + 0.06, AXIS_Z + 0.385), METAL, 0.01)  # bands
b.prism(bm, (g0 - 0.04, AXIS_Y, AXIS_Z), "x", 0.08, TUBE_R + 0.09, METAL, segments=16, chamfer=0.01)   # muzzle collar
tube_len = MUZZLE - TUBE_END
b.prism(bm, (TUBE_END + tube_len / 2, AXIS_Y, AXIS_Z), "x", tube_len, TUBE_R, GLASS, segments=16)
for x in SADDLES:                                                                                  # saddles cradle the tube
    b.ub(bm, (x - 0.07, b.base, AXIS_Z - 0.24), (x + 0.07, AXIS_Y - TUBE_R - 0.02, AXIS_Z + 0.24), METAL, 0.02)
    ring(b, bm, (x, AXIS_Y, AXIS_Z), "x", TUBE_R, TUBE_R + 0.05, 0.09, DARK)
    b.ub(bm, (x - 0.12, b.base, AXIS_Z - 0.3), (x + 0.12, b.base + 0.05, AXIS_Z + 0.3), DARK)        # foot plate
vx, vz = PORT
b.prism(bm, (vx, (b.base + VESSEL_GLASS[0]) / 2, vz), "y", VESSEL_GLASS[0] - b.base, VESSEL_R, METAL, chamfer=0.03)
b.prism(bm, (vx, sum(VESSEL_GLASS) / 2, vz), "y", VESSEL_GLASS[1] - VESSEL_GLASS[0], VESSEL_R - 0.03, GLASS)
for k in range(6):                                                                                 # mullions
    a = math.radians(60 * k)
    c = (vx + (VESSEL_R - 0.03) * math.cos(a), vz + (VESSEL_R - 0.03) * math.sin(a))
    b.ubeam(bm, (c[0], VESSEL_GLASS[0], c[1]), (c[0], VESSEL_GLASS[1], c[1]), 0.06)
b.prism(bm, (vx, (VESSEL_GLASS[1] + VESSEL_CAP) / 2, vz), "y", VESSEL_CAP - VESSEL_GLASS[1], VESSEL_R, METAL, chamfer=0.03)
b.port_collar(bm, vx, vz, VESSEL_CAP - 0.02)
finish(rail, bm)

bands, bm = b.obj("Vessel Bands")
for y in VESSEL_GLASS:
    b.prism(bm, (vx, y, vz), "y", 0.08, VESSEL_R + 0.02, CORE, chamfer=0.01)
finish(bands, bm)
b.look(bands, (0.95, 0.45, 0.15), 1.4)

for i, x in enumerate(COIL_XS):
    o, bm = b.obj(f"Coil {i + 1}")
    ring(b, bm, (x, AXIS_Y, AXIS_Z), "x", COIL_IN, COIL_OUT, COIL_W, CORE)
    finish(o, bm)
    lit = (i / COILS) < OUTPUT
    b.look(o, (0.3, 0.65, 1.0) if lit else (0.03, 0.04, 0.06), 1.8 if lit else 0.0)

beam_o, bm = b.obj("Beam")
b.prism(bm, ((MUZZLE + vx) / 2, AXIS_Y, AXIS_Z), "x", MUZZLE - vx, 0.035, CORE, segments=8)
finish(beam_o, bm)
set_origin(beam_o, (MUZZLE, AXIS_Y, AXIS_Z))
b.look(beam_o, (0.75, 0.9, 1.0), 4.0)

b.draft_rod(vx, vz, VESSEL_GLASS[0] + 0.05, (0.3, 0.7, 1.0))
b.draft_lever(sum(BAY_X) / 2, deck)

# Wiring: In up the back and along it into the gun (the house load runs the beam); Out from the vessel's base into
# the I/O panel; Data along the floor into the back of the screen.
wires, bm = b.obj("Wiring")
WZ = -0.72
b.run(bm, [(IO[0], b.io_top, WZ), (IO[0], -0.3, WZ), (1.0, -0.3, WZ), (1.0, -0.3, AXIS_Z - 0.37)])
b.run(bm, [(IO[2], -0.6, io_inner), (IO[2], -0.6, -0.45), (vx + VESSEL_R - 0.08, -0.6, -0.45)])
sx, sy, sz = screen
b.run(bm, [(IO[1], b.base + 0.03, io_inner), (IO[1], b.base + 0.03, 0.55), (sx, b.base + 0.03, 0.55),
           (sx, b.base + 0.03, sz - 0.08), (sx, sy, sz - 0.08), (sx, sy, sz)], t=0.04)
finish(wires, bm)

b.finish_all(shots_for(1.5))
