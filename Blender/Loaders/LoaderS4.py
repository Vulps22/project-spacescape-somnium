# The S4 item loader: three packs on two levels. Packs come in at the lower right (seen from the front) onto place 1,
# run left along the lower runners to place 2, rise straight up a lift column to the upper level, where the end place
# sits under the mechanism, and leave to the left along an upper runway out of the volume. A square duct rises from
# the mechanism's housing to the ceiling, the item Out port on top of it. Conventions: loader_parts.py.
#   blender -b --python Blender/Loaders/LoaderS4.py -- Blender/Loaders/LoaderS4.blend "Assets/#User/SpaceScape/Meshes/LoaderS4.fbx"
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from loader_parts import *

out_blend, out_fbx = args()
metal, dark, amber = start()

HX, HY, HZ = 2.5, 2.0, 2.0       # half the volume: x (width), y (depth), z (height)
FLOOR, CEIL = -HZ, HZ
PACK = 1.10
LEG = 0.15
P1X = 1.75                       # place 1, the lower right (Unity x -1.75)
P2X = 0.25                       # place 2, the lower middle, and the lift column and end place above it (Unity x -0.25)
LEG_Y = PACK / 2 + 0.05 + LEG / 2          # front and back legs at -LEG_Y, +LEG_Y
LEG_P1 = P1X + PACK / 2 + 0.05 + LEG / 2   # 2.425: the outer legs at place 1, flush with the volume's side
LEG_MID = 1.0                              # one pair of legs between places 1 and 2, clear of both
LEG_COL = P2X - PACK / 2 - 0.05 - LEG / 2  # -0.425: the column's exit-side legs
LEG_END = -HX + LEG / 2                    # -2.425: the upper runway's far legs
RUN_TOP = FLOOR + 0.15                     # the lower runners' top: where a pack's bottom rides
UP_TOP = FLOOR + 1.50                      # the upper runners' top
PACK_TOP = UP_TOP + 1.15                   # the end pack's top, resting
LIFT = 0.14                                # a pack is nudged up this far into the mechanism, and down to leave
MECH_BOTTOM = PACK_TOP - 0.15 + LIFT       # swallows the pack's top 15 cm once lifted
MECH_TOP = MECH_BOTTOM + 0.40
HOUSING_TOP = MECH_TOP + 0.25
P1_TOP = RUN_TOP + 1.15 + 0.10 + 0.12      # the place 1 legs' top rails sit just over a pack there
RUN_Y = 0.38                               # the runner channels, either side of a pack's middle
# The item Out port is on the top face, on the cell over the duct: Unity (-0.25, 2, 0.25), here (0.25, -0.25).
DUCT_X, DUCT_Y, DUCT = P2X, -0.25, 0.64    # the duct's middle and side; the port's 0.56 border sits on its top

def runner_x(bm, lo, hi, top):
    """Two channels along x at +-RUN_Y with rollers, their top at `top`."""
    for sy in (-1, 1):
        y = sy * RUN_Y
        box(bm, (lo, y - 0.08, top - 0.05), (hi, y - 0.06, top + 0.02), 0)   # channel sides
        box(bm, (lo, y + 0.06, top - 0.05), (hi, y + 0.08, top + 0.02), 0)
        box(bm, (lo, y - 0.08, top - 0.07), (hi, y + 0.08, top - 0.05), 0)   # channel floor
        n = int((hi - lo) / 0.16)
        for i in range(n):
            roller(bm, lo + 0.08 + i * 0.16, y, top - 0.035, 0.12)

BEAM_TOP = FLOOR + BEAM_H

def post(bm, x, y, top):
    """A leg from the floor beam's top, with a dark foot plate kept inside the volume."""
    leg(bm, x, y, BEAM_TOP, top)
    box(bm, (max(-HX, x - 0.12), y - 0.12, BEAM_TOP), (min(HX, x + 0.12), y + 0.12, BEAM_TOP + 0.03), DARK)

def braced_pair(bm, x, top, back_to=None):
    """A front and a back leg at x up to `top`, each braced (loader_parts.brace) out front and back, on a floor
    beam running out to the braces' feet (or further back, to `back_to`)."""
    feet = []
    for sy in (-1, 1):
        post(bm, x, sy * LEG_Y, top)
        feet.append(brace(bm, x, sy * LEG_Y, sy, BEAM_TOP, top, HY))
    floor_beam(bm, x, feet[0] - 0.06, back_to if back_to is not None else feet[1] + 0.06, FLOOR)

def cross_rail(bm, x, z_top, h=0.12):
    box(bm, (x - LEG / 2, -LEG_Y - LEG / 2, z_top - h), (x + LEG / 2, LEG_Y + LEG / 2, z_top), 0, 0.01)

def side_rail(bm, x0, x1, z_top, h=0.12):
    """Rails along x joining the front legs and the back legs, outside a pack's depth."""
    for sy in (-1, 1):
        y = sy * LEG_Y
        box(bm, (x0, y - LEG / 2 + 0.02, z_top - h), (x1, y + LEG / 2 - 0.02, z_top), 0, 0.01)

# ---------------------------------------------------------------------------------------------------------------
# Frame
frame, bm = new_object("Frame", [metal, dark, amber])

# Place 1's outer legs and the shared middle legs, up to rails just over a pack at place 1.
braced_pair(bm, LEG_P1, P1_TOP, back_to=HY - PANEL_T)   # its floor beam runs to the back, under the power panel
cross_rail(bm, LEG_P1, P1_TOP)
# The middle legs (between places 1 and 2) and the column's exit-side legs run up to the mechanism.
for x in (LEG_MID, LEG_COL):
    braced_pair(bm, x, MECH_BOTTOM)
cross_rail(bm, LEG_MID, P1_TOP)
cross_rail(bm, LEG_MID, MECH_BOTTOM)
side_rail(bm, LEG_MID - LEG / 2, LEG_P1 + LEG / 2, P1_TOP)             # over place 1
side_rail(bm, LEG_COL - LEG / 2, LEG_MID + LEG / 2, MECH_BOTTOM)       # under the mechanism, outside the column
side_rail(bm, LEG_COL - LEG / 2, LEG_MID + LEG / 2, P1_TOP)            # a stiffener halfway up the column
# On the exit side the pack leaves under the mechanism, so its cross rail is two stubs outside the pack's depth.
for sy in (-1, 1):
    box(bm, (LEG_COL - LEG / 2, sy * (PACK / 2 + 0.03), MECH_BOTTOM - 0.12),
        (LEG_COL + LEG / 2, sy * (LEG_Y + LEG / 2), MECH_BOTTOM), 0, 0.01)

# The upper runway's far legs, at the volume's exit edge, and a cross beam under the runway at each end.
braced_pair(bm, LEG_END, UP_TOP - 0.07)
for x in (LEG_END, LEG_COL):
    cross_rail(bm, x, UP_TOP - 0.07)
side_rail(bm, LEG_END - LEG / 2, LEG_COL + LEG / 2, UP_TOP - 0.07)

# Lower runners: place 1 to place 2, along x.
low_lo, low_hi = P2X - PACK / 2 - 0.06, LEG_P1 + LEG / 2 - 0.02
for sy in (-1, 1):
    y = sy * RUN_Y
    box(bm, (low_lo, y - 0.08, FLOOR + 0.10), (low_hi, y - 0.06, RUN_TOP + 0.02), 0)
    box(bm, (low_lo, y + 0.06, FLOOR + 0.10), (low_hi, y + 0.08, RUN_TOP + 0.02), 0)
    box(bm, (low_lo, y - 0.08, FLOOR + 0.10), (low_hi, y + 0.08, FLOOR + 0.12), 0)
    n = int((low_hi - low_lo) / 0.16)
    for i in range(n):
        roller(bm, low_lo + 0.08 + i * 0.16, y, RUN_TOP - 0.035, 0.12)

# Front runners: the loading zone, from the front of the volume back to the lower runners, under place 1.
front_lo, front_hi = -HY, -RUN_Y - 0.08
for sx in (-1, 1):
    x = P1X + sx * RUN_Y
    box(bm, (x - 0.08, front_lo, FLOOR + 0.10), (x - 0.06, front_hi, RUN_TOP + 0.02), 0)
    box(bm, (x + 0.06, front_lo, FLOOR + 0.10), (x + 0.08, front_hi, RUN_TOP + 0.02), 0)
    box(bm, (x - 0.08, front_lo, FLOOR + 0.10), (x + 0.08, front_hi, FLOOR + 0.12), 0)
    n = int((front_hi - front_lo) / 0.16)
    for i in range(n):
        roller(bm, x, front_lo + 0.08 + i * 0.16, RUN_TOP - 0.035, 0.12, axis="X")
    # feet under the front runners' channel, on the floor
    for fy in (front_lo + 0.10, -1.0):
        box(bm, (x - 0.07, fy - 0.05, FLOOR), (x + 0.07, fy + 0.05, FLOOR + 0.10), 1)

# Upper runway: from the lift column out to the exit edge of the volume, along x.
up_lo, up_hi = -HX, P2X - PACK / 2 - 0.02
runner_x(bm, up_lo, up_hi, UP_TOP)

# The lift column: guide rails front and back of the column, outside the pack, from the lower runners to the mechanism.
GUIDE_Y = PACK / 2 + 0.05
for sy in (-1, 1):
    for gx in (P2X - 0.32, P2X + 0.32):
        box(bm, (gx - 0.04, sy * GUIDE_Y - 0.03, RUN_TOP), (gx + 0.04, sy * GUIDE_Y + 0.03, MECH_BOTTOM), 1)
        # brackets out to the legs' side rails
        for z in (P1_TOP - 0.06, UP_TOP - 0.13):
            box(bm, (gx - 0.03, sy * GUIDE_Y, z - 0.03), (gx + 0.03, sy * (LEG_Y - LEG / 2 + 0.02), z + 0.03), 0)
    # amber markers on the guides at the end place
    box(bm, (P2X - 0.36, sy * GUIDE_Y - 0.035 if sy < 0 else sy * GUIDE_Y + 0.03, UP_TOP + 0.10),
        (P2X - 0.28, sy * GUIDE_Y - 0.03 if sy < 0 else sy * GUIDE_Y + 0.035, UP_TOP + 0.70), 2)
    box(bm, (P2X + 0.28, sy * GUIDE_Y - 0.035 if sy < 0 else sy * GUIDE_Y + 0.03, UP_TOP + 0.10),
        (P2X + 0.36, sy * GUIDE_Y - 0.03 if sy < 0 else sy * GUIDE_Y + 0.035, UP_TOP + 0.70), 2)
# side rails stop short of the guides at the halfway stiffener and run under the runway level
side_rail(bm, LEG_COL - LEG / 2, LEG_MID + LEG / 2, UP_TOP - 0.07)

# Amber: the front opening at place 1, on the inner faces of its front legs.
for x, side in ((LEG_MID, 1), (LEG_P1, -1)):
    xi = x + side * LEG / 2
    box(bm, (min(xi, xi + side * 0.01), -LEG_Y - LEG / 2 - 0.005, RUN_TOP), (max(xi, xi + side * 0.01), -LEG_Y - LEG / 2 + 0.03, P1_TOP - 0.12), 2)
# The eject rocker is mounted straight onto the front face of place 1's right-front leg, 1.35 m up, facing the
# front: Unity (-2.425, -0.65, 0.78). Nothing is modelled for it.

# Power port panel: the power In port is on the back face at cell (0, 0): Unity (-2.25, -1.75, -2.0), here
# (+2.25, +2.0, -1.75). A dark panel flush with the back face on a block off the place 1 legs' floor beam.
power_panel(bm, HX - 0.25, HY, FLOOR + 0.25, LEG_P1, BEAM_TOP)
finish(frame, bm)

# ---------------------------------------------------------------------------------------------------------------
# Front rails: an inner set that slides forward out from under the front runners. Drawn retracted; Unity slides the
# object toward the front (-Y here) by REACH.
REACH = 1.10
RAIL_TOP = FLOOR + 0.095
rails, bm = new_object("Front Rails", [metal, dark, amber])
rail_lo, rail_hi = front_lo, front_lo + REACH
for sx in (-1, 1):
    x = P1X + sx * RUN_Y
    box(bm, (x - 0.06, rail_lo, FLOOR + 0.02), (x - 0.045, rail_hi, RAIL_TOP), 0)
    box(bm, (x + 0.045, rail_lo, FLOOR + 0.02), (x + 0.06, rail_hi, RAIL_TOP), 0)
    box(bm, (x - 0.06, rail_lo, FLOOR + 0.02), (x + 0.06, rail_hi, FLOOR + 0.035), 0)
    n = int(REACH / 0.16)
    for i in range(n):
        roller(bm, x, rail_lo + 0.08 + i * 0.16, RAIL_TOP - 0.025, 0.085, radius=0.025, axis="X")
    box(bm, (x - 0.065, rail_lo - 0.02, FLOOR + 0.02), (x + 0.065, rail_lo, RAIL_TOP + 0.01), 2)
finish(rails, bm)

# ---------------------------------------------------------------------------------------------------------------
# Mechanism: a block over the end place on the column legs, a housing, fins, an amber strip, and the duct.
mech, bm = new_object("Mechanism", [metal, dark, amber])
mx0, mx1 = LEG_COL - LEG / 2 - 0.03, LEG_MID + LEG / 2 + 0.03
my = LEG_Y + LEG / 2 + 0.03
box(bm, (mx0, -my, MECH_BOTTOM), (mx1, my, MECH_TOP), 0, 0.04)
hx0, hx1, hy = mx0 + 0.20, mx1 - 0.20, my - 0.20
box(bm, (hx0, -hy, MECH_TOP), (hx1, hy, HOUSING_TOP), 1, 0.05)
d0x, d1x, d0y, d1y = DUCT_X - DUCT / 2, DUCT_X + DUCT / 2, DUCT_Y - DUCT / 2, DUCT_Y + DUCT / 2
for i in range(7):   # cooling fins on the housing, round the duct
    fx = hx0 + 0.12 + i * (hx1 - hx0 - 0.24) / 6
    if d0x - 0.06 < fx < d1x + 0.06: continue
    box(bm, (fx - 0.02, -hy + 0.05, HOUSING_TOP), (fx + 0.02, hy - 0.05, HOUSING_TOP + 0.06), 0)
for fy in (d1y + 0.12, d1y + 0.24, d1y + 0.36):   # and behind it
    if fy > hy - 0.04: continue
    box(bm, (d0x, fy - 0.02, HOUSING_TOP), (d1x, fy + 0.02, HOUSING_TOP + 0.06), 0)
box(bm, (P2X - 0.35, -my - 0.012, MECH_TOP - 0.16), (P2X + 0.35, -my + 0.01, MECH_TOP - 0.10), 2)   # engaged strip
# The duct: square, in the housing's dark with metal collars, from the housing to the ceiling, solid at the top
# under the item Out port's border.
box(bm, (d0x - 0.06, d0y - 0.06, HOUSING_TOP - 0.02), (d1x + 0.06, d1y + 0.06, HOUSING_TOP + 0.08), 0, 0.02)   # base flange
box(bm, (d0x, d0y, HOUSING_TOP), (d1x, d1y, CEIL), 1, 0.03)
for z in (HOUSING_TOP + 0.26, CEIL - 0.12):   # collars
    box(bm, (d0x - 0.02, d0y - 0.02, z - 0.04), (d1x + 0.02, d1y + 0.02, z + 0.04), 0, 0.01)
finish(mech, bm)

export(out_blend, out_fbx)
