# The S5 item loader: four packs on two levels. Seen from the front, packs load at the lower right, are carried
# left to the middle (place 2), lifted straight up through a lift column to the upper runners (place 3), carried
# left to the end column under the mechanism, and leave left along the upper runway, dropping out of the volume.
# A square duct rises from the mechanism's housing to the ceiling, the item Out port on its top. Blender is Z up;
# front is -Y (Unity +Z after export), the exit is -X here (Unity +X, the viewer's left). Origin at the centre of
# the S5 volume, 5 x 5 x 5 m.
#   blender -b --python Blender/Loaders/LoaderS5.py -- Blender/Loaders/LoaderS5.blend "Assets/#User/SpaceScape/Meshes/LoaderS5.fbx"
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from loader_parts import *

out_blend, out_fbx = args()
metal, dark, amber = start()

H = 2.5                         # half the volume, every way
FLOOR = -H
BEAM_TOP = FLOOR + BEAM_H                  # the floor beams' top, where the legs stand
PACK = 1.10
LEG_Y = PACK / 2 + 0.05 + LEG / 2          # front and back legs at -LEG_Y, +LEG_Y
P1_X, P2_X, END_X = 1.75, 0.25, -1.25      # place 1 (lower right), place 2 / place 3 (the lift column), the end
RUN_TOP = FLOOR + 0.15                     # the lower runners' top: where a pack's bottom rides
UP_TOP = FLOOR + 1.50                      # the upper runners' top
PACK_TOP = UP_TOP + 1.15                   # an upper pack's top
LIFT = 0.14                                # the end pack is nudged up this far into the mechanism
MECH_BOTTOM = PACK_TOP - 0.15 + LIFT       # swallows the pack's top 15 cm once lifted
MECH_TOP = MECH_BOTTOM + 0.40
HOUSING_TOP = MECH_TOP + 0.25
RUN_Y = 0.38                               # runner channels at +-RUN_Y under a pack
# Leg columns (x), between and beside the packs' squares, never in a pack's way:
#   2.425 right of place 1, 1.0 between place 1 and the lift, -0.5 between the lift and the end, -1.925 left of the end.
LX_P1, LX_MID, LX_LIFT, LX_END = H - LEG / 2, 1.0, -0.5, END_X - PACK / 2 - 0.05 - LEG / 2
BEAM_LO, BEAM_HI = UP_TOP - 0.17, UP_TOP - 0.05   # the upper level's beams, under the upper channels
GUIDE_Y = PACK / 2 + 0.05                  # the lift guides' centre, front and back: outside the pack
GUIDE_X = (P2_X - 0.32, P2_X + 0.32)
GUIDE_TOP = PACK_TOP + 0.10                # the guides' header, just over a pack waiting at place 3
# Port cells (Unity), worked out from ComponentEdgeModule.PointOf: power In on the back at cell (0, 0), centre
# Unity (-2.25, -2.25, -2.5); item Out on the top over the duct, centre Unity (1.25, 2.5, 0.25).
PORT_X, PORT_Z = H - 0.25, FLOOR + 0.25
DUCT_X, DUCT_Y, DUCT_HALF = END_X, -0.25, 0.33


def channels_x(bm, x_lo, x_hi, top):
    """Two runner channels along x at +-RUN_Y, rollers along them; a pack's bottom rides at `top`."""
    base = top - 0.05
    for sy in (-1, 1):
        y = sy * RUN_Y
        box(bm, (x_lo, y - 0.08, base), (x_hi, y - 0.06, top + 0.02))   # channel sides
        box(bm, (x_lo, y + 0.06, base), (x_hi, y + 0.08, top + 0.02))
        box(bm, (x_lo, y - 0.08, base), (x_hi, y + 0.08, base + 0.02))  # channel floor
        for i in range(int((x_hi - x_lo) / 0.16)):
            roller(bm, x_lo + 0.08 + i * 0.16, y, top - 0.035, 0.12)


def cross_rail(bm, x, lo, hi):
    box(bm, (x - LEG / 2, -LEG_Y - LEG / 2, lo), (x + LEG / 2, LEG_Y + LEG / 2, hi), METAL, 0.01)


def knee(bm, x_leg, x_out, y, top):
    """Local helper (loader_parts has only front-to-back braces): a 45 degree knee brace in the x-z plane at the
    leg line y, from the leg's centreline up and out to the underside of a cantilevered beam at x_out (top)."""
    d = abs(x_leg - x_out)
    beam(bm, (x_leg, y, top - d), (x_out, y, top + 0.03), BRACE)


# Frame: four leg columns, the lower and upper runners, the front runners, the upper level's beams, the lift
# guides, the exit's knees and the power port panel.
frame, bm = new_object("Frame", [metal, dark, amber])
for x, top in ((LX_P1, BEAM_HI), (LX_MID, BEAM_HI), (LX_LIFT, MECH_BOTTOM), (LX_END, MECH_BOTTOM)):
    feet = []
    for sy in (-1, 1):
        leg(bm, x, sy * LEG_Y, BEAM_TOP, top)
        box(bm, (max(-H, x - 0.12), sy * LEG_Y - 0.12, BEAM_TOP), (min(H, x + 0.12), sy * LEG_Y + 0.12, BEAM_TOP + 0.03), DARK)  # foot plate
        feet.append(brace(bm, x, sy * LEG_Y, sy, BEAM_TOP, top, H))
    back = H - PANEL_T if x == LX_P1 else feet[1] + 0.06   # place 1's right beam runs back to the power panel
    floor_beam(bm, x, feet[0] - 0.06, back, FLOOR)
    cross_rail(bm, x, BEAM_LO, BEAM_HI)                    # the upper level, front to back
for x in (LX_LIFT, LX_END):
    cross_rail(bm, x, MECH_BOTTOM - 0.12, MECH_BOTTOM)     # under the mechanism
# the upper level's side beams, front and back, along x the whole width: outside a pack's depth, so the lift and
# the upper runway are clear between them. Past the end column they carry the exit runway, on a knee each and an
# edge rail across under the channels.
for sy in (-1, 1):
    y = sy * LEG_Y
    box(bm, (-H, y - LEG / 2, BEAM_LO), (H, y + LEG / 2, BEAM_HI), METAL, 0.01)
    knee(bm, LX_END, -H + LEG / 2, y, BEAM_LO)
cross_rail(bm, -H + LEG / 2, BEAM_LO, BEAM_HI)

# Lower runners: from place 1's right legs left to past place 2, the lift column's foot.
channels_x(bm, LX_LIFT - 0.05, H - 0.05, RUN_TOP)
# Upper runners: from the lift column's left edge to the exit, the left edge of the volume. Under place 3 the way
# up is clear: the pack waits there on four catches on the lift guides.
channels_x(bm, -H, P2_X - PACK / 2 - 0.02, UP_TOP)
# The lift column: dark guide channels front and back, outside the pack, from the floor to a header over place 3
# that ties them to the tall legs; the side beams cross them. Amber markers on their outer faces at place 3.
for sy in (-1, 1):
    gy = sy * GUIDE_Y
    for gx in GUIDE_X:
        box(bm, (gx - 0.04, gy - 0.03, FLOOR), (gx + 0.04, gy + 0.03, GUIDE_TOP), DARK)
        box(bm, (gx - 0.04, gy - 0.03, FLOOR), (gx + 0.04, gy + 0.03, FLOOR + 0.02), DARK)
        box(bm, (gx - 0.08, gy - 0.07, FLOOR), (gx + 0.08, gy + 0.07, FLOOR + 0.03), DARK)              # foot
        ya, yb = gy - sy * 0.03, sy * (PACK / 2 - 0.03)                                                 # catch
        box(bm, (gx - 0.04, min(ya, yb), UP_TOP - 0.08), (gx + 0.04, max(ya, yb), UP_TOP), DARK, 0.006)
        yo = gy + sy * 0.03
        box(bm, (gx - 0.025, min(yo, yo + sy * 0.006), UP_TOP + 0.10), (gx + 0.025, max(yo, yo + sy * 0.006), UP_TOP + 0.70), AMBER)
    # header: along x over both guides into the tall leg between the lift and the end
    box(bm, (LX_LIFT, gy - 0.03, GUIDE_TOP - 0.10), (GUIDE_X[1] + 0.04, gy + 0.03, GUIDE_TOP), METAL, 0.01)
# Front runners: the loading zone, from the front of the volume back to place 1's first runner along x.
front_lo, front_hi = -H, -RUN_Y - 0.08
for sx in (-1, 1):
    x = P1_X + sx * RUN_Y
    box(bm, (x - 0.08, front_lo, FLOOR + 0.10), (x - 0.06, front_hi, RUN_TOP + 0.02))
    box(bm, (x + 0.06, front_lo, FLOOR + 0.10), (x + 0.08, front_hi, RUN_TOP + 0.02))
    box(bm, (x - 0.08, front_lo, FLOOR + 0.10), (x + 0.08, front_hi, FLOOR + 0.12))
    for i in range(int((front_hi - front_lo) / 0.16)):
        roller(bm, x, front_lo + 0.08 + i * 0.16, RUN_TOP - 0.035, 0.12, axis="X")

# Amber: the front opening at place 1, on the inner faces of its front legs.
for x, side in ((LX_P1, -1), (LX_MID, 1)):
    xi = x + side * LEG / 2
    box(bm, (min(xi, xi + side * 0.01), -LEG_Y - LEG / 2 - 0.005, RUN_TOP), (max(xi, xi + side * 0.01), -LEG_Y - LEG / 2 + 0.03, BEAM_LO), AMBER)
# The eject rocker goes straight onto the front face of place 1's right-front leg (Unity (-2.425, -1.20, 0.78)),
# 1.30 m up; nothing is modelled for it.
# Power port panel: power In on the back face at cell (0, 0): Unity (-2.25, -2.25, -2.5), here (2.25, 2.5, -2.25),
# on a block standing on place 1's right floor beam, which runs back to it.
power_panel(bm, PORT_X, H, PORT_Z, LX_P1, BEAM_TOP)
finish(frame, bm)

# Front rails: an inner set that slides forward out from under the front runners, a pack's depth past the front
# of the volume. Drawn retracted, tucked under place 1's front runners; Unity slides the object toward the front
# (-Y here) by REACH.
REACH = 1.10
RAIL_TOP = FLOOR + 0.095                   # just under the front runners' channel floor
rails, bm = new_object("Front Rails", [metal, dark, amber])
rail_lo, rail_hi = front_lo + 0.02, front_lo + 0.02 + REACH   # the amber tip flush with the front
for sx in (-1, DARK):
    x = P1_X + sx * RUN_Y
    box(bm, (x - 0.06, rail_lo, FLOOR + 0.02), (x - 0.045, rail_hi, RAIL_TOP), METAL)    # channel sides
    box(bm, (x + 0.045, rail_lo, FLOOR + 0.02), (x + 0.06, rail_hi, RAIL_TOP), METAL)
    box(bm, (x - 0.06, rail_lo, FLOOR + 0.02), (x + 0.06, rail_hi, FLOOR + 0.035), METAL)  # channel floor
    n = int(REACH / 0.16)
    for i in range(n):
        roller(bm, x, rail_lo + 0.08 + i * 0.16, RAIL_TOP - 0.025, 0.085, radius=0.025, axis="X")
    box(bm, (x - 0.065, rail_lo - 0.02, FLOOR + 0.02), (x + 0.065, rail_lo, RAIL_TOP + 0.01), AMBER)   # amber tip
finish(rails, bm)

# Mechanism: a block over the end place on the legs, a housing on top, an amber strip across its front, and a
# square duct from the housing to the ceiling with the item Out port on its top.
mech, bm = new_object("Mechanism", [metal, dark, amber])
mx0, mx1 = LX_END - LEG / 2 - 0.03, LX_LIFT + LEG / 2 + 0.03
my = LEG_Y + LEG / 2 + 0.03
box(bm, (mx0, -my, MECH_BOTTOM), (mx1, my, MECH_TOP), METAL, 0.04)
box(bm, (mx0 + 0.20, -my + 0.20, MECH_TOP), (mx1 - 0.20, my - 0.20, HOUSING_TOP), DARK, 0.05)
for i in range(4):   # cooling fins on the housing, those clear of the duct
    fx = mx0 + 0.35 + i * (mx1 - mx0 - 0.7) / 3
    if abs(fx - DUCT_X) < DUCT_HALF + 0.08: continue
    box(bm, (fx - 0.02, -my + 0.25, HOUSING_TOP), (fx + 0.02, my - 0.25, HOUSING_TOP + 0.06), METAL)
box(bm, (END_X - 0.35, -my - 0.012, MECH_TOP - 0.16), (END_X + 0.35, -my + 0.01, MECH_TOP - 0.10), AMBER)   # engaged strip
# the duct: dark, with metal collars at its foot, its middle and its top; the top collar is flush with the ceiling
# and solid all the way down, under the whole port border (0.56 m square, sunk 0.29 m)
dx0, dx1, dy0, dy1 = DUCT_X - DUCT_HALF, DUCT_X + DUCT_HALF, DUCT_Y - DUCT_HALF, DUCT_Y + DUCT_HALF
box(bm, (dx0, dy0, HOUSING_TOP - 0.02), (dx1, dy1, H - 0.20), DARK, 0.03)
for z0, z1, grow in ((HOUSING_TOP - 0.02, HOUSING_TOP + 0.12, 0.05), (1.40, 1.48, 0.03), (H - 0.22, H, 0.05)):
    box(bm, (dx0 - grow, dy0 - grow, z0), (dx1 + grow, dy1 + grow, z1), METAL, 0.015)
finish(mech, bm)

export(out_blend, out_fbx)
print("DONE lift", LIFT, "up top", UP_TOP, "pack top", PACK_TOP, "mech", MECH_BOTTOM, MECH_TOP, HOUSING_TOP, "duct", DUCT_X, DUCT_Y, DUCT_HALF)
