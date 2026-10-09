# Item conduit pieces: one cell (0.5 m) each, outside exactly the cell (loading.md -> Item conduits).
#   Straight: a square glass tube along Unity Z (Blender Y), thin glass walls between metal edge bars, a metal
#   ring at each end. Clear inside about 0.46 m.
#   Corner: solid metal, open on Unity +Z and +X (Blender -Y and -X), dark inside.
#   blender -b --python Blender/ItemConduit.py -- Blender/ItemConduit.blend "Assets/#User/SpaceScape/Meshes/ItemConduit.fbx"
import bpy, bmesh, sys
from mathutils import Vector

out_blend, out_fbx = sys.argv[sys.argv.index("--") + 1:]
bpy.ops.wm.read_factory_settings(use_empty=True)

H = 0.25          # half a cell
BAR = 0.03        # edge bars
GLASS = 0.008     # glass wall
RING = 0.02       # end ring depth along the tube
INNER = 0.23      # half the clear opening

def material(name, colour, metallic, rough, alpha=1.0):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    p = m.node_tree.nodes["Principled BSDF"]
    p.inputs["Base Color"].default_value = (*colour, 1)
    p.inputs["Metallic"].default_value = metallic
    p.inputs["Roughness"].default_value = rough
    p.inputs["Alpha"].default_value = alpha
    return m

metal = material("FuelPackMetal", (0.16, 0.17, 0.19), 1.0, 0.3)
dark = material("FuelPackDark", (0.03, 0.03, 0.035), 0.8, 0.6)
glass = material("ItemConduitGlass", (0.6, 0.85, 1.0), 0.0, 0.05, 0.15)

def box(bm, lo, hi, mat):
    r = bmesh.ops.create_cube(bm, size=1.0)
    for v in r["verts"]:
        v.co = Vector((hi[0] if v.co.x > 0 else lo[0], hi[1] if v.co.y > 0 else lo[1], hi[2] if v.co.z > 0 else lo[2]))
    for f in {f for v in r["verts"] for f in v.link_faces}: f.material_index = mat

def new_object(name, mats):
    obj = bpy.data.objects.new(name, bpy.data.meshes.new(name))
    bpy.context.collection.objects.link(obj)
    for m in mats: obj.data.materials.append(m)
    return obj, bmesh.new()

def finish(obj, bm):
    bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=0.0001)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    bm.to_mesh(obj.data)
    bm.free()
    for p in obj.data.polygons: p.use_smooth = False

# Straight, along Blender Y.
straight, bm = new_object("Straight", [metal, glass])
for sx in (-1, 1):
    for sz in (-1, 1):
        x0, x1 = sorted((sx * H, sx * (H - BAR)))
        z0, z1 = sorted((sz * H, sz * (H - BAR)))
        box(bm, (x0, -H, z0), (x1, H, z1), 0)
for sy in (-1, 1):   # end rings
    y0, y1 = sorted((sy * H, sy * (H - RING)))
    box(bm, (-H, y0, INNER), (H, y1, H), 0)
    box(bm, (-H, y0, -H), (H, y1, -INNER), 0)
    box(bm, (INNER, y0, -INNER), (H, y1, INNER), 0)
    box(bm, (-H, y0, -INNER), (-INNER, y1, INNER), 0)
g = H - BAR
for s in (-1, 1):    # glass walls between the bars and rings
    box(bm, (-g, -H + RING, s * H - (GLASS if s > 0 else 0)), (g, H - RING, s * H + (0 if s > 0 else GLASS)), 1)
    box(bm, (s * H - (GLASS if s > 0 else 0), -H + RING, -g), (s * H + (0 if s > 0 else GLASS), H - RING, g), 1)
finish(straight, bm)

# Corner: a solid block with two square tunnels meeting in the middle, dark inside.
corner, bm = new_object("Corner", [metal, dark])
C = 0.012   # chamfer-ish lip
box(bm, (-H, -H, -H), (H, H, H), 0)
bm.normal_update()
for axis, sign in ((1, -1), (0, -1)):   # Blender -Y (Unity +Z) and -X (Unity +X)
    faces = [f for f in bm.faces if f.normal[axis] * sign > 0.9]
    r = bmesh.ops.inset_individual(bm, faces=faces, thickness=H - INNER, depth=0.0, use_even_offset=True)
    for f in faces: f.material_index = 1
    bm.normal_update()
    ext = bmesh.ops.extrude_discrete_faces(bm, faces=faces)
    for f in ext["faces"]:
        for v in f.verts: v.co[axis] -= sign * H   # push the opening in to the middle
        f.material_index = 1
    for f in faces:
        if f.is_valid: f.material_index = 1
# side walls of the tunnels are dark too
bm.normal_update()
for f in bm.faces:
    c = f.calc_center_median()
    if max(abs(c.x), abs(c.y), abs(c.z)) < H - 0.001: f.material_index = 1
finish(corner, bm)

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
    print("DONE", obj.name, len(obj.data.polygons), "faces", [round(v, 3) for v in obj.dimensions])
