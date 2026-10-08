import bpy, bmesh, math, sys
from mathutils import Vector

out_blend, out_fbx = sys.argv[sys.argv.index("--") + 1:]
bpy.ops.wm.read_factory_settings(use_empty=True)

W = 1.10           # footprint, square
BASE_H = 1.05      # base height; rods (1 m) fit inside under a closed lid
LID_H = 0.10
CHAMFER = 0.04
ROD = 0.26         # rod centres at (+-ROD, +-ROD)
SOCKET_R = 0.27    # hex socket, centre to corner (rod is 0.24)
LAMP = (0.14, 0.03, 0.05)   # x, depth out of the face, z

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
glow = material("FuelPackGlow", (0.1, 0.45, 1.0), 0.0, 0.1, emit=(0.1, 0.45, 1.0))

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

# Base: chamfered box; each side a frame round a sunken dark panel; four hex sockets on top.
base, bm = new_object("Base", [metal, dark])
box(bm, -h, h, -h, h, 0.0, BASE_H, CHAMFER)
bm.normal_update()
for axis, sign in ((0, 1), (0, -1), (1, 1), (1, -1)):
    faces = [f for f in bm.faces if outward(f, axis, sign) and f.calc_area() > 0.5]
    r = bmesh.ops.inset_individual(bm, faces=faces, thickness=0.08, depth=-0.02, use_even_offset=True)
    for f in faces: f.material_index = 1
for sx in (-1, 1):
    for sy in (-1, 1):
        top, bottom, sides = hex_prism(bm, sx * ROD, sy * ROD, BASE_H, BASE_H + 0.02, SOCKET_R)
        bm.normal_update()
        bmesh.ops.inset_individual(bm, faces=[top], thickness=0.025, depth=-0.03, use_even_offset=True)
        top.material_index = 1
finish(base, bm)

# Seam: a thin glowing band round the top of the base, under the lid.
seam, bm = new_object("Seam", [glow])
o = h + 0.004
box(bm, -o + CHAMFER, o - CHAMFER, -o, o, BASE_H - 0.035, BASE_H - 0.015)
box(bm, -o, o, -o + CHAMFER, o - CHAMFER, BASE_H - 0.035, BASE_H - 0.015)
finish(seam, bm)

# Count lamps on the front (-Y), one per rod, left to right.
for i in range(4):
    x = (i - 1.5) * 0.2
    lamp, bm = new_object(f"Lamp{i + 1}", [glow])
    box(bm, x - LAMP[0] / 2, x + LAMP[0] / 2, -h - LAMP[1], -h + 0.005, 0.80, 0.80 + LAMP[2])
    finish(lamp, bm)

# Lid: origin at its underside, so it rises by moving up. Top panel sunk; a hex clamp under each rod.
lid, bm = new_object("Lid", [metal, dark], (0, 0, BASE_H))
box(bm, -h, h, -h, h, 0.0, LID_H, CHAMFER)
bm.normal_update()
tops = [f for f in bm.faces if outward(f, 2, 1) and f.calc_area() > 0.5]
bmesh.ops.inset_individual(bm, faces=tops, thickness=0.1, depth=-0.02, use_even_offset=True)
for f in tops: f.material_index = 1
for sx in (-1, 1):
    for sy in (-1, 1):
        top, bottom, sides = hex_prism(bm, sx * ROD, sy * ROD, -0.05, 0.0, SOCKET_R)
        bm.normal_update()
        bmesh.ops.inset_individual(bm, faces=[bottom], thickness=0.025, depth=-0.03, use_even_offset=True)
        bottom.material_index = 1
finish(lid, bm)

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
