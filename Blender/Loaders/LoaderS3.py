# The S3 item loader: two packs. A pack is loaded at the front onto the lower place (the viewer's lower right),
# lifted straight up a column of four braced legs to the upper level, nudged up into the mechanism over it, then
# leaves to the left (seen from the front) along an upper runway carried on braced posts, out of the volume. A
# square duct rises from the mechanism's housing to the ceiling for the item Out port. Origin at the centre of the
# S3 volume, 4 x 4 x 4 m. Shared parts and conventions: loader_parts.py.
#
# Positions below are written in UNITY coordinates (x, y, z): front +z, the viewer's left +x (the exit side). The
# local helpers ub/ubeam/uroller turn them into Blender's (Z up, front -Y): Blender (X, Y, Z) = (-x, -z, y). The
# shared leg/floor_beam/brace/power_panel take Blender coordinates, so their calls use bx(x) = -x, by(z) = -z.
#   blender -b --python Blender/Loaders/LoaderS3.py -- Blender/Loaders/LoaderS3.blend "Assets/#User/SpaceScape/Meshes/LoaderS3.fbx"
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from loader_parts import *

out_blend, out_fbx = args()
metal, dark, amber = start()

H = 2.0                         # half the volume, every way
FLOOR = -H
BEAM_TOP = FLOOR + BEAM_H       # every leg stands on a floor beam
PX = -1.25                      # the lift column's centre (x): the lower place and the end place above it
PACK = 1.10
LEG_X = (PX - PACK / 2 - 0.05 - LEG / 2, PX + PACK / 2 + 0.05 + LEG / 2)   # -1.925, -0.575
LEG_Z = PACK / 2 + 0.05 + LEG / 2                                            # front and back legs at +LEG_Z, -LEG_Z
RUN_TOP = FLOOR + 0.15          # the lower runners' top: where a pack's bottom rides at the lower place
UP_TOP = FLOOR + 1.50           # the upper runway's top
PACK_TOP = UP_TOP + 1.15
LIFT = 0.14                     # nudged up this far into the mechanism, and down to leave
MECH_BOTTOM = PACK_TOP - 0.15 + LIFT
MECH_TOP = MECH_BOTTOM + 0.40
HOUSING_TOP = MECH_TOP + 0.25
RUN_O = 0.38                    # each runner's offset from the pack's centre line
POSTS = (0.70, H - LEG / 2)     # the upper runway's post pairs (x)

def bx(x): return -x
def by(z): return -z
def U(p): return Vector((-p[0], -p[2], p[1]))

def ub(bm, lo, hi, mat=METAL, chamfer=0.0):
    """box() between two corners given in Unity coordinates."""
    a, b = U(lo), U(hi)
    box(bm, [min(a[i], b[i]) for i in range(3)], [max(a[i], b[i]) for i in range(3)], mat, chamfer)

def uroller(bm, p, along_x, length, radius=0.035):
    """A roller at p (Unity) across a runner along Unity x (along_x) or along Unity z."""
    q = U(p)
    roller(bm, q.x, q.y, q.z, length, radius, DARK, "Y" if along_x else "X")

def braced_pair(bm, x, top, back_to=None):
    """A floor beam front to back under a pair of legs at Unity x, the legs from it up to `top`, and one brace from
    each leg out to the beam's ends (front to back, so nothing crosses the ways along x). back_to: run the beam on
    back to this Blender y instead of stopping at the back brace's foot."""
    feet = []
    for sz in (-1, 1):              # Unity front (+1) and back (-1)
        leg(bm, bx(x), by(sz * LEG_Z), BEAM_TOP, top)
        feet.append(brace(bm, bx(x), by(sz * LEG_Z), -sz, BEAM_TOP, top, H))
    y0, y1 = min(feet) - 0.06, max(feet) + 0.06
    if back_to is not None: y1 = max(y1, back_to)
    floor_beam(bm, bx(x), y0, y1, FLOOR)

frame, bm = new_object("Frame", [metal, dark, amber])

# The lift column: four legs round the place, up to the mechanism. The back-right (Unity -x) floor beam runs on
# to the back face under the power panel's block.
PORT_X, PORT_Y = -1.75, FLOOR + 0.25                      # power In: back face, cell (0, 0)
braced_pair(bm, LEG_X[0], MECH_BOTTOM, back_to=H - PANEL_T)
braced_pair(bm, LEG_X[1], MECH_BOTTOM)
for x in LEG_X:
    # cross rail under the mechanism, front to back
    ub(bm, (x - LEG / 2, MECH_BOTTOM - 0.12, -LEG_Z - LEG / 2), (x + LEG / 2, MECH_BOTTOM, LEG_Z + LEG / 2), METAL, 0.01)
for sz in (-1, 1):
    # ties along x between the column's legs under the mechanism, outside the pack's depth
    z = sz * LEG_Z
    ub(bm, (LEG_X[0], MECH_BOTTOM - 0.12, z - 0.05), (LEG_X[1], MECH_BOTTOM, z + 0.05), METAL, 0.01)
# sleepers along x between the column's floor beams, under the front runners: at the back (under the runners'
# back ends) and on the front legs' line (behind the retracted front rails)
ub(bm, (LEG_X[0], FLOOR, -LEG_Z - 0.05), (LEG_X[1], BEAM_TOP, -PACK / 2 + 0.03), METAL, 0.01)
ub(bm, (LEG_X[0], FLOOR, LEG_Z - 0.05), (LEG_X[1], BEAM_TOP, LEG_Z + 0.05), METAL, 0.01)
# Lift guides: dark channels on the column's right (seen from the front: Unity -X) and back, the two sides no pack
# passes through (packs come in from the front at the bottom and leave to the left at the top). From the floor up to
# the mechanism, tied to the legs by a header there. The end pack waits at the upper runway's height on four catches
# reaching 3 cm under its edges. Amber markers on the guides' outer faces.
GUIDE = PACK / 2 + 0.05

def guide(gx, gz, ox, oz):
    """One guide channel at (gx, gz); (ox, oz) is its outward direction, away from the pack."""
    wx, wz = (0.03, 0.04) if ox else (0.04, 0.03)
    ub(bm, (gx - wx, FLOOR, gz - wz), (gx + wx, MECH_BOTTOM, gz + wz), DARK)
    ub(bm, (gx - 0.08, FLOOR, gz - 0.08), (gx + 0.08, FLOOR + 0.03, gz + 0.08), DARK)                              # foot
    ix, iz = gx - ox * (wx + 0.05 - 0.03 + 0.03), gz - oz * (wz + 0.05 - 0.03 + 0.03)                               # catch reaches 3 cm under the pack
    ub(bm, (min(gx, ix) - (0 if ox else wx), UP_TOP - 0.08, min(gz, iz) - (0 if oz else wz)),
           (max(gx, ix) + (0 if ox else wx), UP_TOP, max(gz, iz) + (0 if oz else wz)), DARK, 0.006)
    mx, mz = gx + ox * wx, gz + oz * wz                                                                             # marker
    ub(bm, (min(mx, mx + ox * 0.006) - (0 if ox else 0.025), UP_TOP + 0.10, min(mz, mz + oz * 0.006) - (0 if oz else 0.025)),
           (max(mx, mx + ox * 0.006) + (0 if ox else 0.025), UP_TOP + 0.70, max(mz, mz + oz * 0.006) + (0 if oz else 0.025)), AMBER)

for off in (-0.20, 0.20):
    guide(PX + off, -GUIDE, 0, -1)          # back
    guide(PX - GUIDE, off, -1, 0)           # right
ub(bm, (min(LEG_X), MECH_BOTTOM - 0.10, -GUIDE - 0.03), (max(LEG_X), MECH_BOTTOM, -GUIDE + 0.03), METAL, 0.01)       # back header
ub(bm, (PX - GUIDE - 0.03, MECH_BOTTOM - 0.10, -LEG_Z), (PX - GUIDE + 0.03, MECH_BOTTOM, LEG_Z), METAL, 0.01)     # right header

# Lower place and front runners: two channels along z, from the back of the place to the front face.
front_lo, front_hi = -PACK / 2, H
for sx in (-1, 1):
    x = PX + sx * RUN_O
    ub(bm, (x - 0.08, BEAM_TOP, front_lo), (x - 0.06, RUN_TOP + 0.02, front_hi))
    ub(bm, (x + 0.06, BEAM_TOP, front_lo), (x + 0.08, RUN_TOP + 0.02, front_hi))
    ub(bm, (x - 0.08, BEAM_TOP, front_lo), (x + 0.08, BEAM_TOP + 0.02, front_hi))
    n = int((front_hi - front_lo) / 0.16)
    for i in range(n):
        uroller(bm, (x, RUN_TOP - 0.035, front_lo + 0.08 + i * 0.16), False, 0.12)
    ub(bm, (x - 0.09, BEAM_TOP, front_lo - 0.04), (x + 0.09, RUN_TOP + 0.06, front_lo), DARK)   # back stop

# Upper runway: two channels along x at the upper level, from the column's edge out to the left face, on cross
# beams on the column's left legs and on two braced post pairs, with stringers along the legs' lines.
run_lo, run_hi = LEG_X[1] - LEG / 2 + 0.01, H
for x in POSTS:
    braced_pair(bm, x, UP_TOP - 0.10)
for x in (LEG_X[1],) + POSTS:
    ub(bm, (x - LEG / 2, UP_TOP - 0.22, -LEG_Z - LEG / 2), (x + LEG / 2, UP_TOP - 0.10, LEG_Z + LEG / 2), METAL, 0.01)
for sz in (-1, 1):
    z = sz * LEG_Z
    ub(bm, (LEG_X[1] + LEG / 2, UP_TOP - 0.22, z - 0.06), (H, UP_TOP - 0.10, z + 0.06), METAL, 0.01)
for sz in (-1, 1):
    z = sz * RUN_O
    ub(bm, (run_lo, UP_TOP - 0.10, z - 0.08), (run_hi, UP_TOP + 0.02, z - 0.06))
    ub(bm, (run_lo, UP_TOP - 0.10, z + 0.06), (run_hi, UP_TOP + 0.02, z + 0.08))
    ub(bm, (run_lo, UP_TOP - 0.10, z - 0.08), (run_hi, UP_TOP - 0.08, z + 0.08))
    n = int((run_hi - run_lo) / 0.16)
    for i in range(n):
        uroller(bm, (run_lo + 0.08 + i * 0.16, UP_TOP - 0.035, z), True, 0.12)

# Amber: the front opening, on the inner faces of the column's front legs, from the lower runners to the mechanism.
for x, side in ((LEG_X[0], 1), (LEG_X[1], -1)):
    xi = x + side * LEG / 2
    ub(bm, (xi, RUN_TOP, LEG_Z + LEG / 2 + 0.005), (xi + side * 0.01, RUN_TOP + 1.15, LEG_Z + LEG / 2 - 0.03), AMBER)   # the opening only, a pack high

# Power port panel: back face, cell (0, 0) at Unity (-1.75, -1.75, -2.0), on a block standing on the column's
# back-right floor beam (Unity x -1.925), which runs back to it.
power_panel(bm, bx(PORT_X), H, PORT_Y, bx(LEG_X[0]), BEAM_TOP)
finish(frame, bm)

# Front rails: an inner set that slides forward out from under the front runners, a pack's depth past the front of
# the volume. Drawn retracted, under the front end of the runners; Unity slides them toward the front (+z) by REACH.
REACH = 1.10
RAIL_TOP = FLOOR + 0.095
rails, bm = new_object("Front Rails", [metal, dark, amber])
rail_front, rail_back = H, H - REACH
for sx in (-1, 1):
    x = PX + sx * RUN_O
    ub(bm, (x - 0.06, FLOOR + 0.02, rail_back), (x - 0.045, RAIL_TOP, rail_front))
    ub(bm, (x + 0.045, FLOOR + 0.02, rail_back), (x + 0.06, RAIL_TOP, rail_front))
    ub(bm, (x - 0.06, FLOOR + 0.02, rail_back), (x + 0.06, FLOOR + 0.035, rail_front))
    n = int(REACH / 0.16)
    for i in range(n):
        uroller(bm, (x, RAIL_TOP - 0.025, rail_front - 0.08 - i * 0.16), False, 0.085, radius=0.025)
    ub(bm, (x - 0.065, FLOOR + 0.02, rail_front), (x + 0.065, RAIL_TOP + 0.01, rail_front + 0.02), AMBER)   # tip
finish(rails, bm)

# Mechanism: a block over the end place on the column's legs, a housing, fins, the amber strip, and a square duct
# from the housing to the ceiling under the item Out port (top face, cell (1, 4): centre (-1.25, 2.0, 0.25)).
mech, bm = new_object("Mechanism", [metal, dark, amber])
mx0, mx1 = max(-H, LEG_X[0] - LEG / 2 - 0.03), LEG_X[1] + LEG / 2 + 0.03
mz = LEG_Z + LEG / 2 + 0.03
ub(bm, (mx0, MECH_BOTTOM, -mz), (mx1, MECH_TOP, mz), METAL, 0.04)
ub(bm, (mx0 + 0.20, MECH_TOP, -mz + 0.20), (mx1 - 0.20, HOUSING_TOP, mz - 0.20), DARK, 0.05)
DUCT_X, DUCT_Z, DUCT = PX, 0.25, 0.64
dz0 = DUCT_Z - DUCT / 2
for i in range(4):   # cooling fins on the housing, stopping at the duct
    fx = mx0 + 0.35 + i * (mx1 - mx0 - 0.7) / 3
    under = abs(fx - DUCT_X) < DUCT / 2 + 0.02
    ub(bm, (fx - 0.02, HOUSING_TOP, -mz + 0.25), (fx + 0.02, HOUSING_TOP + 0.06, dz0 - 0.03 if under else mz - 0.25))
ub(bm, (PX - 0.35, MECH_TOP - 0.16, mz - 0.01), (PX + 0.35, MECH_TOP - 0.10, mz + 0.012), AMBER)   # engaged strip
# the duct: a collar on the housing, the shaft, and a dark head flush with the ceiling, solid under the port
ub(bm, (DUCT_X - DUCT / 2 - 0.04, HOUSING_TOP - 0.02, dz0 - 0.04), (DUCT_X + DUCT / 2 + 0.04, HOUSING_TOP + 0.10, DUCT_Z + DUCT / 2 + 0.04), DARK, 0.02)
ub(bm, (DUCT_X - DUCT / 2, HOUSING_TOP, dz0), (DUCT_X + DUCT / 2, H - 0.12, DUCT_Z + DUCT / 2), METAL, 0.03)
ub(bm, (DUCT_X - DUCT / 2 - 0.03, H - 0.12, dz0 - 0.03), (DUCT_X + DUCT / 2 + 0.03, H, DUCT_Z + DUCT / 2 + 0.03), DARK, 0.02)
finish(mech, bm)

export(out_blend, out_fbx)
print("DONE reach", REACH, "lift", LIFT, "runner top", RUN_TOP, "up top", UP_TOP, "pack top", PACK_TOP, "mech", MECH_BOTTOM, MECH_TOP, HOUSING_TOP)
