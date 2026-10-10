import bpy, bmesh, math, sys
from mathutils import Vector

out_blend, out_fbx = sys.argv[sys.argv.index("--") + 1:]
bpy.ops.wm.read_factory_settings(use_empty=True)

W = 1.10           # footprint, square
BASE_H = 1.05      # base height, the fuel pack's
LID_H = 0.10
CHAMFER = 0.04
LAMP = (0.08, 0.03, 0.05)   # x, depth out of the face, z
CELL = 0.32        # a lattice cell; an addon item is 0.25 x 0.25 x 0.063 m and stands upright in one
ADDON_H = 0.25
CENTRE = 0.885     # an addon's centre below the lid's underside (the places, Unity y -0.885 under the lid track):
                   # with the lid risen 1.03 m the tray's underside sits on the seam, and closed it rests 2 cm off the bottom
FLOOR = -CENTRE - ADDON_H / 2   # the tray's floor, relative to the lid's underside
WALL_H = 0.10      # the lattice walls: low, so the addons can be read and lifted out


def material(name, colour, metallic, rough, emit=None):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    p = m.node_tree.nodes["Principled BSDF"]
    p.inputs["Base Color"].default_value = (*colour, 1)
    p.inputs["Metallic"].default_value = metallic
    p.inputs["Roughness"].default_value = rough
    if emit:
        p.inputs["Emission Color"].default_value = (*emit, 1)
        p.inputs["Emission Strength"].default_value = 3.0
    return m

metal = material("FuelPackMetal", (0.16, 0.17, 0.19), 1.0, 0.3)
dark = material("FuelPackDark", (0.03, 0.03, 0.035), 0.8, 0.6)
glow = material("AddonPackGlow", (1.0, 0.08, 0.05), 0.0, 0.1, emit=(1.0, 0.08, 0.05))

def new_object(name, mats, location=(0, 0, 0)):
    mesh = bpy.data.meshes.new(name)
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    for m in mats: obj.data.materials.append(m)
    obj.location = location
    return obj, bmesh.new()

def box(bm, x0, x1, y0, y1, z0, z1, chamfer=0.0):
    r = bmesh.ops.create_cube(bm, size=1.0)
    verts = r["verts"]
    for v in verts:
        v.co = Vector((x1 if v.co.x > 0 else x0, y1 if v.co.y > 0 else y0, z1 if v.co.z > 0 else z0))
    if chamfer > 0:
        edges = list({e for v in verts for e in v.link_edges})
        bmesh.ops.bevel(bm, geom=verts + edges, offset=chamfer, segments=1, affect="EDGES", profile=0.5)
    return [f for f in bm.faces if all(v in verts or True for v in f.verts)]

def hex_prism(bm, cx, cy, z0, z1, r):
    lo = [bm.verts.new((cx + r * math.cos(math.radians(60 * i)), cy + r * math.sin(math.radians(60 * i)), z0)) for i in range(6)]
    hi = [bm.verts.new((cx + r * math.cos(math.radians(60 * i)), cy + r * math.sin(math.radians(60 * i)), z1)) for i in range(6)]
    sides = [bm.faces.new((lo[i], lo[(i + 1) % 6], hi[(i + 1) % 6], hi[i])) for i in range(6)]
    top = bm.faces.new(hi)
    bottom = bm.faces.new(list(reversed(lo)))
    return top, bottom, sides

def finish(obj, bm):
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    bm.to_mesh(obj.data)
    bm.free()
    for p in obj.data.polygons: p.use_smooth = False

def outward(f, axis, sign):
    n = f.normal
    return n[axis] * sign > 0.9

h = W / 2

# The addon pack (loading.md → The addon pack): the fuel pack's base and lid, red where it is blue, eight count
# lamps, and a tray hung under the lid: a lattice of eight cells round a solid middle block with a red ring round
# its top. Each cell holds one addon item, upright, its front facing out.
#   blender -b --python Blender/AddonPack.py -- Blender/AddonPack.blend "Assets/#User/SpaceScape/Meshes/AddonPack.fbx"

# Base: chamfered box; each side a frame round a sunken dark panel; the top a sunken dark panel.
base, bm = new_object("Base", [metal, dark])
box(bm, -h, h, -h, h, 0.0, BASE_H, CHAMFER)
bm.normal_update()
for axis, sign in ((0, 1), (0, -1), (1, 1), (1, -1), (2, 1)):
    faces = [f for f in bm.faces if outward(f, axis, sign) and f.calc_area() > 0.5]
    bmesh.ops.inset_individual(bm, faces=faces, thickness=0.08, depth=-0.02, use_even_offset=True)
    for f in faces: f.material_index = 1
finish(base, bm)

# Seam: a thin glowing band round the top of the base, under the lid.
seam, bm = new_object("Seam", [glow])
o = h + 0.004
box(bm, -o + CHAMFER, o - CHAMFER, -o, o, BASE_H - 0.035, BASE_H - 0.015)
box(bm, -o, o, -o + CHAMFER, o - CHAMFER, BASE_H - 0.035, BASE_H - 0.015)
finish(seam, bm)

# Count lamps on the front (-Y), one per place, left to right.
for i in range(8):
    x = (i - 3.5) * 0.12
    lamp, bm = new_object(f"Lamp{i + 1}", [glow])
    box(bm, x - LAMP[0] / 2, x + LAMP[0] / 2, -h - LAMP[1], -h + 0.005, 0.80, 0.80 + LAMP[2])
    finish(lamp, bm)

# Lid: origin at its underside, so it rises by moving up. Top panel sunk.
lid, bm = new_object("Lid", [metal, dark], (0, 0, BASE_H))
box(bm, -h, h, -h, h, 0.0, LID_H, CHAMFER)
bm.normal_update()
tops = [f for f in bm.faces if outward(f, 2, 1) and f.calc_area() > 0.5]
bmesh.ops.inset_individual(bm, faces=tops, thickness=0.1, depth=-0.02, use_even_offset=True)
for f in tops: f.material_index = 1
finish(lid, bm)

# Tray, hung under the lid (origin at the lid's underside, like the lid): a floor, low lattice walls between the
# cells, four corner posts up to the lid, and the solid middle block.
tray, bm = new_object("Tray", [metal, dark], (0, 0, BASE_H))
edge = 1.5 * CELL
T = 0.02
box(bm, -edge, edge, -edge, edge, FLOOR - T, FLOOR, 0.005)
for c in (-edge, -CELL / 2, CELL / 2, edge):
    box(bm, c - T / 2, c + T / 2, -edge, edge, FLOOR, FLOOR + WALL_H)
    box(bm, -edge, edge, c - T / 2, c + T / 2, FLOOR, FLOOR + WALL_H)
for sx in (-1, 1):
    for sy in (-1, 1):
        box(bm, sx * edge - 0.015, sx * edge + 0.015, sy * edge - 0.015, sy * edge + 0.015, FLOOR, 0.0)
block = CELL / 2 - T / 2 - 0.005
box(bm, -block, block, -block, block, FLOOR, FLOOR + ADDON_H, 0.01)
bm.normal_update()
for f in bm.faces:
    c = f.calc_center_median()
    if abs(c.z - FLOOR) < 0.001 and f.normal.z > 0.9: f.material_index = 1   # the floor's top, dark
finish(tray, bm)

# The middle block's ring: a thin glowing band round its top, matching the seam.
ring, bm = new_object("Block Ring", [glow], (0, 0, BASE_H))
r = block + 0.004
top = FLOOR + ADDON_H
box(bm, -r, r, -r, r, top - 0.03, top - 0.012)
finish(ring, bm)

for obj in bpy.data.objects:
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
bpy.ops.object.mode_set(mode="EDIT")
bpy.ops.mesh.select_all(action="SELECT")
bpy.ops.uv.smart_project(island_margin=0.02)
bpy.ops.object.mode_set(mode="OBJECT")

bpy.ops.wm.save_as_mainfile(filepath=out_blend)
bpy.ops.export_scene.fbx(filepath=out_fbx, use_selection=True, apply_unit_scale=True, apply_scale_options="FBX_SCALE_UNITS",
                         axis_forward="-Z", axis_up="Y", bake_space_transform=True, mesh_smooth_type="FACE")
for obj in bpy.data.objects:
    print("DONE", obj.name, len(obj.data.polygons), "faces", [round(v, 3) for v in obj.dimensions], tuple(round(v, 3) for v in obj.location))
