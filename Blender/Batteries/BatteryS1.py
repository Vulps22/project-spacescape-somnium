# The S1 battery, "The Stack" (docs/battery.md -> S1): four stacks of five short hex cells in a light braced frame,
# clamped under a solid top plate flush with the frame's top. Each cell is 1/20th of the charge and is its own object
# (Cell 01 ... Cell 20, in fill order) so the readout can colour it. 4 x 4 x 4 cells, a 2 m cube.
#
#   Draft:  blender -b --python Blender/Batteries/BatteryS1.py -- Blender/Batteries/BatteryS1.blend
#   Final:  blender -b --python Blender/Batteries/BatteryS1.py -- Blender/Batteries/BatteryS1.blend "Assets/#User/SpaceScape/Meshes/BatteryS1.fbx"
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from battery_parts import *

b = Battery(1.0, 1.0, 1.0)
PLATE_BOTTOM = 0.82             # the top plate's underside; its top is the frame's top edge
INNER = b.px - TB / 2           # the top beams' inner edge, which the plate fills to
# Fill order (Vulps): front-right, then clockwise seen from above. The viewer's right is Unity -x.
STACKS = [(-0.25, 0.25), (0.25, 0.25), (0.25, -0.25), (-0.25, -0.25)]
CAP = 0.045                     # the near-black cap between cells
CELL_H = (PLATE_BOTTOM - b.base - 6 * CAP) / 5
SLEEVE_R = 0.2                  # 0.4 m across
CORE_R = 0.17
CHARGE = 0.48                   # draft preview only

frame, bm = b.frame()
b.ub(bm, (-INNER, PLATE_BOTTOM, -INNER), (INNER, b.top, INNER), METAL, 0.01)              # top plate, flush
b.ub(bm, (-INNER + 0.08, b.top - 0.008, -INNER + 0.08), (INNER - 0.08, b.top + 0.0005, INNER - 0.08), DARK)
io_inner = b.io_panel(bm)
screen = b.control_panel(bm, 0.71 - PANEL_W, 0.71, 0.72, b.base)    # in front of the stacks, the viewer's left
finish(frame, bm)

stacks, bm = b.obj("Stacks")
for (x, z) in STACKS:
    for k in range(6):                                                                      # caps
        y = b.base + k * (CELL_H + CAP)
        b.prism(bm, (x, y + CAP / 2, z), "y", CAP, SLEEVE_R + 0.015, DARK, chamfer=0.006)
    for k in range(5):                                                                      # glass sleeves
        y = b.base + CAP + k * (CELL_H + CAP)
        b.prism(bm, (x, y + CELL_H / 2, z), "y", CELL_H, SLEEVE_R, GLASS)
finish(stacks, bm)

for level in range(5):
    for s, (x, z) in enumerate(STACKS):
        n = level * 4 + s
        o, bm = b.cell_object(f"Cell {n + 1:02d}", min(1.0, max(0.0, CHARGE * 20 - n)))
        y = b.base + CAP + level * (CELL_H + CAP)
        b.prism(bm, (x, y + CELL_H / 2, z), "y", CELL_H - 0.01, CORE_R, CELL)
        finish(o, bm)

# Wiring: In up the back into the top; down through the stacks; Out from the plinth; Data round the stacks on the
# viewer's left into the back of the screen.
wires, bm = b.obj("Wiring")
b.run(bm, [(IO_IN, b.io_top, -0.92), (IO_IN, b.top - TB / 2, -0.92), (IO_IN, b.top - TB / 2, -b.pz)])
b.run(bm, [(IO_OUT, -0.6, io_inner), (IO_OUT, -0.6, -0.58), (IO_OUT, b.base, -0.58)])
sx, sy, sz = screen
b.run(bm, [(IO_DATA, b.base + 0.03, io_inner), (IO_DATA, b.base + 0.03, -0.5), (0.6, b.base + 0.03, -0.5),
           (0.6, b.base + 0.03, sz - 0.08), (0.6, sy, sz - 0.08), (0.6, sy, sz)], t=0.04)
finish(wires, bm)

b.finish_all(shots_for(1.0))
