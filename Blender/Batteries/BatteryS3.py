# The S3 battery, "The Lift" (docs/battery.md -> S3): a dense near-black sphere lifted up a glass column by a magnetic
# field, its height the charge, a faint plasma beam trailing from it down to the base. Gunmetal field rings up the
# column, dark housings top and base. 8 x 8 x 8 cells, a 4 m cube.
#
# Exported with the sphere resting on the base (empty) as its own object, "Sphere", its origin at its centre, and the
# beam as "Beam", one metre tall from the base housing's top, its origin at its foot: the readout script (BatteryLift)
# raises the sphere and stretches the beam up to meet it.
#
#   Draft:  blender -b --python Blender/Batteries/BatteryS3.py -- Blender/Batteries/BatteryS3.blend
#   Final:  blender -b --python Blender/Batteries/BatteryS3.py -- Blender/Batteries/BatteryS3.blend "Assets/#User/SpaceScape/Meshes/BatteryS3.fbx"
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from battery_parts import *

b = Battery(2.0, 2.0, 2.0, post=0.18)
PLASMA = 5
b.mats.append(material("BatteryPlasma", (0.6, 0.35, 1.0), 0.0, 0.3, emit=(0.55, 0.3, 1.0)))
HOUSING_R = 1.0                 # both housings, to the corners
BASE_TOP = -0.9                 # the base housing's top: the column starts here
COLUMN_TOP = 1.2
TOP_TOP = 1.62
GLASS_R = 0.62
MULLION_R = 0.7
RING_R = 0.78                   # the field rings' centreline
RINGS = 5
SPHERE_R = 0.42
REST = BASE_TOP + SPHERE_R      # the sphere's centre, empty
FULL = COLUMN_TOP - SPHERE_R    # and full
CHARGE = 0.48                   # draft preview only

frame, bm = b.frame()
for (x, z) in ((0, 0.8), (0, -0.8), (0.8, 0), (-0.8, 0)):       # four struts from the top housing up into the top beams
    sx, sz = (0 if x == 0 else math.copysign(b.px, x)), (0 if z == 0 else math.copysign(b.pz, z))
    b.ubeam(bm, (x * 0.9, TOP_TOP - 0.05, z * 0.9), (sx, b.top - TB / 2, sz), 0.12)
io_inner = b.io_panel(bm)
screen = b.control_panel(bm, 1.65 - PANEL_W, 1.65, 1.7, b.base)   # beside the column, the viewer's left
finish(frame, bm)

column, bm = b.obj("Column")
b.prism(bm, (0, (b.base + BASE_TOP) / 2, 0), "y", BASE_TOP - b.base, HOUSING_R, DARK, segments=8, chamfer=0.03, turn=22.5)
b.prism(bm, (0, (COLUMN_TOP + TOP_TOP) / 2, 0), "y", TOP_TOP - COLUMN_TOP, HOUSING_R, DARK, segments=8, chamfer=0.03, turn=22.5)
for y in (b.base + 0.25, BASE_TOP - 0.06, COLUMN_TOP + 0.06, TOP_TOP - 0.12):           # metal bands on the housings
    b.prism(bm, (0, y, 0), "y", 0.07, HOUSING_R + 0.02, METAL, segments=8, turn=22.5)
b.prism(bm, (0, (BASE_TOP + COLUMN_TOP) / 2, 0), "y", COLUMN_TOP - BASE_TOP, GLASS_R, GLASS, segments=16)
for k in range(6):                                                                      # mullions
    a = math.radians(60 * k + 30)
    b.ubeam(bm, (MULLION_R * math.cos(a), BASE_TOP - 0.05, MULLION_R * math.sin(a)),
            (MULLION_R * math.cos(a), COLUMN_TOP + 0.05, MULLION_R * math.sin(a)), 0.07)
for i in range(RINGS):                                      # field rings, on the mullions: 16 sides, one piece, mitred
    y = BASE_TOP + (COLUMN_TOP - BASE_TOP) * (i + 0.5) / RINGS
    w = 0.05 / math.cos(math.pi / 16)                       # half the section, measured at a mitred corner
    corners = []
    for k in range(16):
        a = math.radians(360 * k / 16)
        corners.append([bm.verts.new(U((r * math.cos(a), y + dy, r * math.sin(a))))
                        for r, dy in ((RING_R - w, -0.05), (RING_R + w, -0.05), (RING_R + w, 0.05), (RING_R - w, 0.05))])
    for k in range(16):
        c, n = corners[k], corners[(k + 1) % 16]
        for j in range(4):
            bm.faces.new((c[j], n[j], n[(j + 1) % 4], c[(j + 1) % 4])).material_index = METAL
b.prism(bm, (0, BASE_TOP + 0.04, 0), "y", 0.08, 0.22, METAL, segments=8)                  # where the beam is anchored
finish(column, bm)

sphere = bpy.data.objects.new("Sphere", bpy.data.meshes.new("Sphere"))
bpy.context.collection.objects.link(sphere)
for m in b.mats: sphere.data.materials.append(m)
bm = bmesh.new()
bmesh.ops.create_icosphere(bm, subdivisions=2, radius=SPHERE_R, matrix=Matrix.Translation(U((0, REST, 0))))
for f in bm.faces: f.material_index = DARK
finish(sphere, bm)

beam_o, bm = b.obj("Beam")                                        # two crossed ribbons, a metre tall
for turn in (0, 90):
    w = 0.06
    a, c = math.cos(math.radians(turn)) * w, math.sin(math.radians(turn)) * w
    vs = [bm.verts.new(U(p)) for p in ((-a, BASE_TOP, -c), (a, BASE_TOP, c), (a, BASE_TOP + 1, c), (-a, BASE_TOP + 1, -c))]
    bm.faces.new(vs).material_index = PLASMA
bm.to_mesh(beam_o.data); bm.free()


def set_origin(obj, at):
    """Moves an object's origin to a Unity point, leaving its mesh where it is."""
    p = U(at)
    obj.data.transform(Matrix.Translation(-p))
    obj.location = p


set_origin(sphere, (0, REST, 0))
set_origin(beam_o, (0, BASE_TOP, 0))

wires, bm = b.obj("Wiring")
b.run(bm, [(IO_IN, b.io_top, -1.9), (IO_IN, (COLUMN_TOP + TOP_TOP) / 2, -1.9), (IO_IN, (COLUMN_TOP + TOP_TOP) / 2, -0.5)])
b.run(bm, [(IO_OUT, b.floor + 0.25, io_inner), (IO_OUT, b.floor + 0.25, io_inner + 0.1), (IO_OUT, b.base, io_inner + 0.1)])
sx, sy, sz = screen
b.run(bm, [(IO_DATA, b.base + 0.03, io_inner), (IO_DATA, b.base + 0.03, io_inner + 0.1), (sx, b.base + 0.03, io_inner + 0.1),
           (sx, b.base + 0.03, sz - 0.08), (sx, sy, sz - 0.08), (sx, sy, sz)], t=0.04)
finish(wires, bm)

if not b.out_fbx:                                                 # draft: the sphere and beam at the preview charge
    height = REST + (FULL - REST) * CHARGE
    sphere.location = U((0, height, 0))
    beam_o.scale = (1, 1, max(0.01, height - SPHERE_R - BASE_TOP))
b.finish_all(shots_for(2.0))
