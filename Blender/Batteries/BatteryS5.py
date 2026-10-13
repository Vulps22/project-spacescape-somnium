# The S5 battery, "The Ring" (docs/battery.md -> S5): a particle storage ring standing upright, facing the room, in a
# cradle on the plinth, wrapped in heavy magnet blocks and braced into the house frame. The tube is oval in section,
# deeper front to back than across. It arcs, so a guard rail closes the open front and danger signs hang on the rail
# and the posts. 10 x 10 x 10 cells, a 5 m cube.
#
# The readout is the ring's inner band, "Band": one mesh with one material (BatteryRing), which a shader draws as
# bunches of light racing round, lengthening and merging as it fills. No lights, no particles. Arcs are runtime lines.
#
#   Draft:  blender -b --python Blender/Batteries/BatteryS5.py -- Blender/Batteries/BatteryS5.blend
#   Final:  blender -b --python Blender/Batteries/BatteryS5.py -- Blender/Batteries/BatteryS5.blend "Assets/#User/SpaceScape/Meshes/BatteryS5.fbx"
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from battery_parts import *

b = Battery(2.5, 2.5, 2.5, post=0.22)
RING_MAT, SEAM_MAT = 5, 6
b.mats.append(material("BatteryRing", (0.9, 0.85, 1.0), 0.0, 0.2, emit=(0.85, 0.8, 1.0)))
b.mats.append(material("BatterySeam", (0.02, 0.02, 0.025), 0.3, 0.5))     # glows faintly at full (BatteryRing.cs)
CY = 0.12                       # the ring's centre height
R = 1.72                        # its centreline radius
TUBE_R, TUBE_Z = 0.36, 0.75     # the glass vacuum tube's half-section: across the ring, and front to back
BAND_R, BAND_Z = 0.18, 0.45     # the light inside it
MAGNETS = 12
MAG = 0.9                       # a magnet block's section across the ring
MAG_Z = 0.95                    # and its half-depth, front to back
MAG_ARC = 9                     # degrees of ring each block covers
CRADLE_TOP = CY - R - MAG / 2 + 0.03
CHARGE = 0.48                   # draft preview only
RAIL_Z = 1.75                   # the guard rail across the open front
RAIL_X = (-2.1, -0.7, 0.7, 2.1) # its stanchions
RAIL_Y = (0.55, 1.1)            # its two rails, above the plinth
SIGN_W, SIGN_H = 0.36, 0.46
FONT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "Assets", "Editor Default Resources",
                    "SomniumSpace", "Fonts", "SourceSansPro-Black.ttf")


def ring_point(deg, r=R):
    a = math.radians(deg)
    return (r * math.cos(a), CY + r * math.sin(a), 0.0)


def torus(bm, radius, tube, depth, mat, major=64, minor=12):
    """A ring in the plane z = 0 round (0, CY), its section an oval (tube across, depth front to back), as quads."""
    rows = []
    for i in range(major):
        a = 2 * math.pi * i / major
        row = []
        for j in range(minor):
            t = 2 * math.pi * j / minor
            rr = radius + tube * math.cos(t)
            row.append(bm.verts.new(U((rr * math.cos(a), CY + rr * math.sin(a), depth * math.sin(t)))))
        rows.append(row)
    for i in range(major):
        for j in range(minor):
            a, c = rows[i], rows[(i + 1) % major]
            bm.faces.new((a[j], c[j], c[(j + 1) % minor], a[(j + 1) % minor])).material_index = mat


def ring_block(bm, deg0, deg1, half, zh, mat=METAL):
    """A straight block on the ring between two angles: 'half' either side of the centreline, zh front and back."""
    vs = []
    for z in (-zh, zh):
        for deg, r in ((deg0, R - half), (deg0, R + half), (deg1, R + half), (deg1, R - half)):
            x, y, _ = ring_point(deg, r)
            vs.append(bm.verts.new(U((x, y, z))))
    for f in ((0, 3, 2, 1), (4, 5, 6, 7), (0, 1, 5, 4), (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)):
        bm.faces.new([vs[i] for i in f]).material_index = mat


def text_object(text, height, width):
    """Flat dark lettering facing Unity +z at the origin, fitted inside height x width."""
    bpy.ops.object.text_add()
    t = bpy.context.object
    t.data.body = text
    t.data.font = bpy.data.fonts.load(FONT, check_existing=True)
    t.data.align_x, t.data.align_y = "CENTER", "CENTER"
    t.data.extrude = 0.5
    t.data.resolution_u = 1
    bpy.context.view_layer.update()
    k = min(width / t.dimensions.x, height / t.dimensions.y)
    t.scale = (k, k, 0.006)
    t.rotation_euler = (math.radians(90), 0, 0)              # Blender +Z (its face) to -Y, Unity +z
    bpy.ops.object.convert(target="MESH")
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    t.data.materials.clear()
    for m in b.mats: t.data.materials.append(m)
    for p in t.data.polygons: p.material_index = DARK
    return t


BOLT = ((0.10, 0.5), (-0.22, -0.04), (0.0, -0.04), (-0.12, -0.5), (0.24, 0.08), (0.03, 0.08), (0.2, 0.5))


def sign(at, yaw):
    """A danger sign, its back against a face at Unity point 'at', facing +z turned by yaw (90: facing -x).
    Amber plate in a dark rim, a lightning bolt, DANGER above and KEEP CLEAR below."""
    o, bm = b.obj("Sign")
    w, h = SIGN_W / 2, SIGN_H / 2
    b.ub(bm, (-w, -h, 0), (w, h, 0.02), DARK, 0.005)
    b.ub(bm, (-w + 0.02, -h + 0.02, 0.02), (w - 0.02, h - 0.02, 0.026), AMBER)
    size = 0.2                                                  # the bolt: its face, then its sides
    front = [bm.verts.new(U((-u * size, v * size - 0.01, 0.032))) for u, v in BOLT]
    back = [bm.verts.new(U((-u * size, v * size - 0.01, 0.026))) for u, v in BOLT]
    bm.faces.new(front).material_index = DARK
    for i in range(len(BOLT)):
        j = (i + 1) % len(BOLT)
        bm.faces.new((front[i], front[j], back[j], back[i])).material_index = DARK
    finish(o, bm)
    parts = [o]
    for text, y, th in (("DANGER", h - 0.07, 0.07), ("KEEP CLEAR", -h + 0.06, 0.05)):
        t = text_object(text, th, SIGN_W - 0.08)
        t.location = U((0, y, 0.029))
        parts.append(t)
    bpy.ops.object.select_all(action="DESELECT")
    for p in parts: p.select_set(True)
    bpy.context.view_layer.objects.active = o
    bpy.ops.object.join()
    o.rotation_euler = (0, 0, math.radians(yaw))
    o.location = U(at)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    return o


frame, bm = b.frame()
for s in (-1, 1):                                                  # a mid post on each side, for the side struts
    b.ub(bm, (s * b.px - b.post / 2, b.base, -b.post / 2), (s * b.px + b.post / 2, b.top - TB, b.post / 2), METAL, 0.015)
b.ub(bm, (-b.px, b.top - TB, -TB / 2), (b.px, b.top, TB / 2), METAL, 0.012)     # a top beam across, for the top strut
# Cradle: a block on the plinth the bottom magnet sits in, and two legs up to the magnets either side of it.
b.ub(bm, (-1.0, b.base, -MAG_Z), (1.0, CRADLE_TOP, MAG_Z), METAL, 0.03)
b.ub(bm, (-1.05, b.base + 0.15, -MAG_Z - 0.05), (1.05, b.base + 0.3, MAG_Z + 0.05), DARK, 0.01)
for deg in (240, 300):
    p = ring_point(deg, R - MAG / 2)
    for z in (-MAG_Z / 2, MAG_Z / 2):
        b.ubeam(bm, (math.copysign(0.85, p[0]), CRADLE_TOP - 0.1, z), (p[0], p[1], z), 0.2)
# Struts into the frame: the side magnets to the mid posts, the top magnet to the top beam.
for deg, end in ((0, (b.px, CY, 0)), (180, (-b.px, CY, 0)), (90, (0, b.top - TB / 2, 0))):
    for z in (-MAG_Z / 2, MAG_Z / 2):
        p = ring_point(deg, R + MAG / 2 - 0.05)
        b.ubeam(bm, (p[0], p[1], z), (end[0], end[1], z), 0.18)
# The guard rail: stanchions on the plinth, two rails across.
for x in RAIL_X:
    b.ub(bm, (x - 0.05, b.base, RAIL_Z - 0.05), (x + 0.05, b.base + RAIL_Y[1] + 0.05, RAIL_Z + 0.05), METAL, 0.01)
    b.ub(bm, (x - 0.09, b.base, RAIL_Z - 0.09), (x + 0.09, b.base + 0.03, RAIL_Z + 0.09), DARK)
for y in RAIL_Y:
    b.ub(bm, (RAIL_X[0] - 0.08, b.base + y - 0.04, RAIL_Z - 0.04), (RAIL_X[-1] + 0.08, b.base + y + 0.04, RAIL_Z + 0.04), METAL, 0.01)
io_inner = b.io_panel(bm)
screen = b.control_panel(bm, 2.135 - PANEL_W, 2.135, 2.2, b.base)  # the low front corner, the viewer's left
finish(frame, bm)

ring, bm = b.obj("Ring")
torus(bm, R, TUBE_R, TUBE_Z, GLASS)
for k in range(MAGNETS):                                           # heavy magnet blocks
    deg = 360 * k / MAGNETS
    ring_block(bm, deg - MAG_ARC / 2, deg + MAG_ARC / 2, MAG / 2, MAG_Z)
finish(ring, bm)

seams, bm = b.obj("Seams")                                         # a dark seam round each block's middle, its own
for k in range(MAGNETS):                                           # object so it can glow when held back at full
    deg = 360 * k / MAGNETS
    ring_block(bm, deg - 0.8, deg + 0.8, MAG / 2 + 0.015, MAG_Z + 0.015, SEAM_MAT)
finish(seams, bm)

band, bm = b.cell_object("Band", CHARGE)
torus(bm, R, BAND_R, BAND_Z, CELL, minor=8)
finish(band, bm)
band.data.materials[CELL] = b.mats[RING_MAT]       # the band's own material, for its shader
for f in band.data.polygons: f.material_index = CELL

wires, bm = b.obj("Wiring")
IN_DEG = 120                                                       # In comes in to this magnet from behind
ix, iy, _ = ring_point(IN_DEG)
b.run(bm, [(IO_IN, b.io_top, -2.4), (IO_IN, iy, -2.4), (ix, iy, -2.4), (ix, iy, -MAG_Z + 0.05)])
b.run(bm, [(IO_OUT, b.floor + 0.25, io_inner), (IO_OUT, b.floor + 0.25, io_inner + 0.1), (IO_OUT, b.base, io_inner + 0.1)])
sx, sy, sz = screen
b.run(bm, [(IO_DATA, b.base + 0.03, io_inner), (IO_DATA, b.base + 0.03, io_inner + 0.1), (sx, b.base + 0.03, io_inner + 0.1),
           (sx, b.base + 0.03, sz - 0.08), (sx, sy, sz - 0.08), (sx, sy, sz)], t=0.05)
finish(wires, bm)

# Signs: on the rail's middle, on both front posts, and on both side mid posts facing out.
post = b.post / 2
signs = [sign((0, b.base + RAIL_Y[1] - SIGN_H / 2 + 0.04, RAIL_Z + 0.04), 0)]
for s in (-1, 1):
    signs.append(sign((s * b.px, -0.55, b.pz + post), 0))
    signs.append(sign((s * (b.px + post), -0.55, 0), -90 * s))
bpy.ops.object.select_all(action="DESELECT")
for o in signs: o.select_set(True)
bpy.context.view_layer.objects.active = signs[0]
bpy.ops.object.join()
signs[0].name = signs[0].data.name = "Signs"
for p in signs[0].data.polygons: p.use_smooth = False

b.finish_all(shots_for(2.5))
