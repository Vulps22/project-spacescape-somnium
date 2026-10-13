# The S0 battery (docs/battery.md -> S0): a square battery and nothing more. No readouts, no glow. In on its -x face
# (the viewer's right), Out on +x, each port in a metal terminal plate; every other face marked + on the Out side and
# - on the In side, so its polarity reads from any angle. 1 x 1 x 1 cell, a 0.5 m cube.
#
#   Draft:  blender -b --python Blender/Batteries/BatteryS0.py -- Blender/Batteries/BatteryS0.blend
#   Final:  blender -b --python Blender/Batteries/BatteryS0.py -- Blender/Batteries/BatteryS0.blend "Assets/#User/SpaceScape/Meshes/BatteryS0.fbx"
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from battery_parts import *

b = Battery(0.25, 0.25, 0.25)
H = 0.25
BASE_TOP = -H + 0.025           # a metal base plate filling the footprint
BODY = 0.235                    # the body's half-width on x and z: the terminal plates bring the ports out to the face
LID = 0.19                      # the lid's underside
PLATE = 0.17                    # the terminal plates, square
MARK, MARK_T, MARK_H = 0.09, 0.02, 0.006    # a mark's arm length, width, and how proud it stands
MARK_X = 0.11                   # + at Unity +x (Out), - at -x (In)

o, bm = b.obj("Body")
b.ub(bm, (-H, -H, -H), (H, BASE_TOP, H), METAL, 0.01)
b.ub(bm, (-BODY, BASE_TOP, -BODY), (BODY, LID, BODY), DARK, 0.015)
b.ub(bm, (-BODY + 0.01, LID, -BODY + 0.01), (BODY - 0.01, H - 0.008, BODY - 0.01), METAL, 0.012)
for s in (-1, 1):                                                          # terminal plates round the ports
    x0, x1 = sorted((s * BODY, s * H))
    b.ub(bm, (x0, -PLATE / 2, -PLATE / 2), (x1, PLATE / 2, PLATE / 2), METAL, 0.008)


def mark(plus, x, face):
    """A + or - centred at x on one face: 'front', 'back' (z), 'top' (y, on the lid) or 'bottom' (under the base)."""
    arms = [((-MARK / 2, -MARK_T / 2), (MARK / 2, MARK_T / 2))]
    if plus:
        arms.append(((-MARK_T / 2, -MARK / 2), (MARK_T / 2, MARK / 2)))
    for (a0, b0), (a1, b1) in arms:
        if face in ("front", "back"):
            z = BODY if face == "front" else -BODY
            zs = sorted((z, z + (MARK_H if face == "front" else -MARK_H)))
            b.ub(bm, (x + a0, b0 - 0.02, zs[0]), (x + a1, b1 - 0.02, zs[1]), METAL)
        elif face == "top":
            b.ub(bm, (x + a0, H - 0.008, b0), (x + a1, H, b1), DARK)
        else:
            b.ub(bm, (x + a0, -H - 0.0005, b0), (x + a1, -H + 0.004, b1), DARK)


for face in ("front", "back", "top", "bottom"):
    mark(True, MARK_X, face)
    mark(False, -MARK_X, face)
finish(o, bm)

b.finish_all(shots_for(0.25))
