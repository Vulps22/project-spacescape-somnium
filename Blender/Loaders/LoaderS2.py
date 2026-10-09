# The S2 item loader: a rig of four braced legs round one pack place, runners to the left (seen from the
# front) out of the volume, and a mechanism on top that swallows a pack's top. Blender is Z up; front is -Y
# (Unity +Z after export), the exit is -X (the left, seen from the front). Origin at the centre of the S2
# volume, 3 x 2 x 2 m.
#   blender -b --python Blender/Loaders/LoaderS2.py -- Blender/Loaders/LoaderS2.blend "Assets/#User/SpaceScape/Meshes/LoaderS2.fbx"
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from loader_parts import *

out_blend, out_fbx = args()
metal, dark, amber = start()

HX, HZ = 1.5, 1.0               # half the volume: x, and height (the volume is 2 m deep, so half y is 1.0 too)
FLOOR = -HZ
PLACE_X = 0.6                   # the pack's centre
PACK = 1.10
LEG = 0.15
LEG_X = (PLACE_X - PACK / 2 - 0.05 - LEG / 2, PLACE_X + PACK / 2 + 0.05 + LEG / 2)
LEG_Y = PACK / 2 + 0.05 + LEG / 2          # front and back legs at -LEG_Y, +LEG_Y
RUN_TOP = FLOOR + 0.15                     # the runners' top: where a pack's bottom rides
PACK_TOP = RUN_TOP + 1.15
LIFT = 0.14                                # a pack is nudged up this far into the mechanism, and down to leave
MECH_BOTTOM = PACK_TOP - 0.15 + LIFT       # swallows the pack's top 15 cm once lifted
MECH_TOP = MECH_BOTTOM + 0.40
HOUSING_TOP = MECH_TOP + 0.25

# Frame: legs, floor beams, front-to-back braces, runners with rollers, a top rail under the mechanism.
frame, bm = new_object("Frame", [metal, dark, amber])
leg_top = MECH_BOTTOM
for x in LEG_X:
    # a floor beam front to back under each pair of legs, the braces' feet on its ends
    box(bm, (x - LEG / 2, -0.95, FLOOR), (x + LEG / 2, 0.95, FLOOR + 0.10), 0, 0.01)
    for sy in (-1, 1):
        y = sy * LEG_Y
        box(bm, (x - LEG / 2, y - LEG / 2, FLOOR + 0.10), (x + LEG / 2, y + LEG / 2, leg_top), 0, 0.015)
        # foot plate
        box(bm, (x - 0.12, y - 0.12, FLOOR + 0.10), (x + 0.12, y + 0.12, FLOOR + 0.13), 1)
        # brace: from a third of the way up the leg out to the floor beam's end
        beam(bm, (x, y + sy * LEG / 2, FLOOR + 0.75), (x, sy * 0.90, FLOOR + 0.10), 0.08)
        # a second, shorter brace higher up, for the rig look
        beam(bm, (x, y + sy * LEG / 2, FLOOR + 1.0), (x, sy * 0.90, FLOOR + 0.62), 0.06)
        # gusset where the braces meet the beam end
        box(bm, (x - 0.06, sy * 0.90 - 0.06, FLOOR + 0.10), (x + 0.06, sy * 0.90 + 0.06, FLOOR + 0.66), 0, 0.01)
    # cross rail under the mechanism, front to back
    box(bm, (x - LEG / 2, -LEG_Y - LEG / 2, leg_top - 0.12), (x + LEG / 2, LEG_Y + LEG / 2, leg_top), 0, 0.01)

# Runners: two channels along x, from the right legs out to the left edge of the volume.
RUN_Y = 0.38
run_lo, run_hi = -HX, LEG_X[1] + LEG / 2
for sy in (-1, 1):
    y = sy * RUN_Y
    box(bm, (run_lo, y - 0.08, FLOOR + 0.10), (run_hi, y - 0.06, RUN_TOP + 0.02), 0)   # channel sides
    box(bm, (run_lo, y + 0.06, FLOOR + 0.10), (run_hi, y + 0.08, RUN_TOP + 0.02), 0)
    box(bm, (run_lo, y - 0.08, FLOOR + 0.10), (run_hi, y + 0.08, FLOOR + 0.12), 0)     # channel floor
    n = int((run_hi - run_lo) / 0.16)
    for i in range(n):
        roller(bm, run_lo + 0.08 + i * 0.16, y, RUN_TOP - 0.035, 0.12)
# Front runners: the loading zone, from the front of the volume back to the first runner along x.
front_lo, front_hi = -1.0, -RUN_Y - 0.08
for sx in (-1, 1):
    x = PLACE_X + sx * RUN_Y
    box(bm, (x - 0.08, front_lo, FLOOR + 0.10), (x - 0.06, front_hi, RUN_TOP + 0.02), 0)
    box(bm, (x + 0.06, front_lo, FLOOR + 0.10), (x + 0.08, front_hi, RUN_TOP + 0.02), 0)
    box(bm, (x - 0.08, front_lo, FLOOR + 0.10), (x + 0.08, front_hi, FLOOR + 0.12), 0)
    n = int((front_hi - front_lo) / 0.16)
    for i in range(n):
        m = Matrix.Translation((x, front_lo + 0.08 + i * 0.16, RUN_TOP - 0.035)) @ Matrix.Rotation(math.radians(90), 4, "Y")
        r = bmesh.ops.create_cone(bm, cap_ends=True, segments=10, radius1=0.035, radius2=0.035, depth=0.12, matrix=m)
        for f in {f for v in r["verts"] for f in v.link_faces}: f.material_index = 1

# under the cantilevered runway, a strut back to the left legs' floor beam
for sy in (-1, 1):
    beam(bm, (run_lo + 0.05, sy * RUN_Y, FLOOR + 0.10), (LEG_X[0], sy * RUN_Y, FLOOR + 0.05), 0.06)

# Amber: the front opening, on the inner faces of the front legs and along the front of the mechanism's base.
for x, side in ((LEG_X[0], 1), (LEG_X[1], -1)):
    xi = x + side * LEG / 2
    box(bm, (min(xi, xi + side * 0.01), -LEG_Y - LEG / 2 - 0.005, RUN_TOP), (max(xi, xi + side * 0.01), -LEG_Y - LEG / 2 + 0.03, MECH_BOTTOM), 2)
# Power port panel: the power In port is on the back face at cell (0, 0), seen from behind the bottom-left cell:
# Unity (-1.25, -0.75, -1.0), here (+1.25, +1.0, -0.75). A dark panel flush with the back face, a little bigger
# than the port (8.4 cm square, sunk 4 cm), on an arm from the back-right leg it lines up with.
PORT_X, PORT_Z = HX - 0.25, FLOOR + 0.25
leg_back = LEG_Y + LEG / 2
box(bm, (PORT_X - 0.05, leg_back, PORT_Z - 0.05), (PORT_X + 0.05, 1.0 - 0.06, PORT_Z + 0.05), 0, 0.01)   # arm
box(bm, (PORT_X - 0.09, 1.0 - 0.06, PORT_Z - 0.09), (PORT_X + 0.09, 1.0, PORT_Z + 0.09), 1, 0.01)         # panel
finish(frame, bm)

# Front rails: an inner set that slides forward out from under the front runners, a pack's depth past the front
# of the volume, while the loader would take the pack in front of it. Drawn retracted, tucked under the place below
# every runner; Unity slides the object toward the front (-Y here) by REACH.
REACH = 1.10
RAIL_TOP = FLOOR + 0.095                   # just under the front runners' channel floor
rails, bm = new_object("Front Rails", [metal, dark, amber])
rail_lo, rail_hi = front_lo, front_lo + REACH
for sx in (-1, 1):
    x = PLACE_X + sx * RUN_Y
    box(bm, (x - 0.06, rail_lo, FLOOR + 0.02), (x - 0.045, rail_hi, RAIL_TOP), 0)    # channel sides
    box(bm, (x + 0.045, rail_lo, FLOOR + 0.02), (x + 0.06, rail_hi, RAIL_TOP), 0)
    box(bm, (x - 0.06, rail_lo, FLOOR + 0.02), (x + 0.06, rail_hi, FLOOR + 0.035), 0)  # channel floor
    n = int(REACH / 0.16)
    for i in range(n):
        m = Matrix.Translation((x, rail_lo + 0.08 + i * 0.16, RAIL_TOP - 0.025)) @ Matrix.Rotation(math.radians(90), 4, "Y")
        r = bmesh.ops.create_cone(bm, cap_ends=True, segments=10, radius1=0.025, radius2=0.025, depth=0.085, matrix=m)
        for f in {f for v in r["verts"] for f in v.link_faces}: f.material_index = 1
    box(bm, (x - 0.065, rail_lo - 0.02, FLOOR + 0.02), (x + 0.065, rail_lo, RAIL_TOP + 0.01), 2)   # amber tip
finish(rails, bm)

# Mechanism: a block over the place on the legs, a housing on top, an amber strip across its front.
mech, bm = new_object("Mechanism", [metal, dark, amber])
mx0, mx1 = LEG_X[0] - LEG / 2 - 0.03, LEG_X[1] + LEG / 2 + 0.03
my = LEG_Y + LEG / 2 + 0.03
box(bm, (mx0, -my, MECH_BOTTOM), (mx1, my, MECH_TOP), 0, 0.04)
box(bm, (mx0 + 0.20, -my + 0.20, MECH_TOP), (mx1 - 0.20, my - 0.20, HOUSING_TOP), 1, 0.05)
for i in range(4):   # cooling fins on the housing
    fx = mx0 + 0.35 + i * (mx1 - mx0 - 0.7) / 3
    box(bm, (fx - 0.02, -my + 0.25, HOUSING_TOP), (fx + 0.02, my - 0.25, HOUSING_TOP + 0.06), 0)
box(bm, (PLACE_X - 0.35, -my - 0.012, MECH_TOP - 0.16), (PLACE_X + 0.35, -my + 0.01, MECH_TOP - 0.10), 2)   # engaged strip
finish(mech, bm)

export(out_blend, out_fbx)
print("DONE reach", REACH, "lift", LIFT, "fins top", HOUSING_TOP + 0.06, "place x", PLACE_X, "runner top", RUN_TOP, "pack top", PACK_TOP, "mech", MECH_BOTTOM, MECH_TOP, HOUSING_TOP)
