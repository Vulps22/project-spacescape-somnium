# The S2 battery, "The Rack" (docs/battery.md -> S2): 4 rows of 5 cells lying front to back like shells in a rack,
# their glowing end caps facing the room under a glass cap pushed on over the front end, sealed by a thin metal collar.
# Their back ends meet a back plate set in from the frame's back. An X brace across the top. Each end cap is 1/20th of the charge and is its own object (Cell 01 ... Cell 20, in fill order:
# bottom row first, the viewer's right to left). 6 x 4 x 4 cells (3 x 2 x 2 m), long on x, front on a long side.
#
#   Draft:  blender -b --python Blender/Batteries/BatteryS2.py -- Blender/Batteries/BatteryS2.blend
#   Final:  blender -b --python Blender/Batteries/BatteryS2.py -- Blender/Batteries/BatteryS2.blend "Assets/#User/SpaceScape/Meshes/BatteryS2.fbx"
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from battery_parts import *

b = Battery(1.5, 1.0, 1.0)
CEILING = b.top - TB                                    # the top beams' underside: the rows fill up to it
ROW = (CEILING - b.base) / 4
CELL_R = ROW / 2 - 0.003                                # corner to corner fills the row
XS = [-0.75, -0.33, 0.09, 0.51, 0.93]                   # the viewer's right to left; the screen stands past -x
PLATE_FRONT, PLATE_BACK = -0.5, -0.56                   # the back plate, in from the frame's back
CAP_BACK, CAP_FRONT = 0.55, 0.61                        # the glowing end cap: the readout
GLASS_BACK, GLASS_FRONT = 0.5, 0.63                     # the glass cap pushed on over the cell's front end
GLASS_OVER = 0.006                                      # how far its walls stand out from the sleeve
COLLAR = (0.49, 0.515, 0.01)                            # the thin metal collar sealing it to the sleeve: z from, to, proud
WIRE_Z = -0.68                                          # In runs here, between the back plate and the back braces
CHARGE = 0.48                                           # draft preview only

frame, bm = b.frame()
for s in (-1, 1):                                                                               # X brace on top
    b.ubeam(bm, (-b.px, b.top - TB / 2, -s * b.pz), (b.px, b.top - TB / 2, s * b.pz), BRACE)
io_inner = b.io_panel(bm)
b.ub(bm, (XS[0] - CELL_R - 0.05, b.base, PLATE_BACK), (XS[-1] + CELL_R + 0.05, CEILING, PLATE_FRONT), METAL, 0.015)
screen = b.control_panel(bm, -1.2, -1.2 + PANEL_W, 0.84, b.base)    # in front of the rack's right end, clear of the caps
finish(frame, bm)

rack, bm = b.obj("Rack")
for row in range(4):
    y = b.base + ROW * (row + 0.5)
    for x in XS:
        b.prism(bm, (x, y, (PLATE_FRONT + CAP_BACK) / 2), "z", CAP_BACK - PLATE_FRONT, CELL_R, DARK, turn=0)
        b.prism(bm, (x, y, (GLASS_BACK + GLASS_FRONT) / 2), "z", GLASS_FRONT - GLASS_BACK, CELL_R + GLASS_OVER, GLASS, turn=0)
        b.prism(bm, (x, y, (COLLAR[0] + COLLAR[1]) / 2), "z", COLLAR[1] - COLLAR[0], CELL_R + COLLAR[2], METAL, turn=0)
finish(rack, bm)

for row in range(4):
    y = b.base + ROW * (row + 0.5)
    for k, x in enumerate(XS):
        n = row * 5 + k
        o, bm = b.cell_object(f"Cell {n + 1:02d}", min(1.0, max(0.0, CHARGE * 20 - n)))
        b.prism(bm, (x, y, (CAP_BACK + CAP_FRONT) / 2), "z", CAP_FRONT - CAP_BACK, CELL_R - 0.015, CELL, turn=0)
        finish(o, bm)

# Wiring: In up from the I/O panel and across the back of the back plate, T-junctioning straight into it at three
# points; Out down into the plinth; Data round the rack's right end into the back of the screen.
wires, bm = b.obj("Wiring")
IN_Y = (b.base + CEILING) / 2
b.run(bm, [(IO_IN, b.io_top, WIRE_Z), (IO_IN, IN_Y, WIRE_Z), (XS[-1] + 0.12, IN_Y, WIRE_Z)])
for x in (XS[1], XS[3], XS[-1]):
    b.run(bm, [(x, IN_Y, WIRE_Z), (x, IN_Y, PLATE_BACK)])
OUT_Z = (io_inner + PLATE_BACK) / 2
b.run(bm, [(IO_OUT, -0.6, io_inner), (IO_OUT, -0.6, OUT_Z), (IO_OUT, b.base, OUT_Z)])
sx, sy, sz = screen
b.run(bm, [(IO_DATA, b.base + 0.03, io_inner), (IO_DATA, b.base + 0.03, OUT_Z), (-1.1, b.base + 0.03, OUT_Z),
           (-1.1, b.base + 0.03, sz - 0.08), (-1.1, sy, sz - 0.08), (-1.1, sy, sz)], t=0.04)
finish(wires, bm)

b.finish_all(shots_for(1.5))
