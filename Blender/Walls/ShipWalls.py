# The ship's wall and floor, in the ship look sample's style (Ship Look Sample in the scene, Meshes/ShipSample.fbx).
#   WallPanel: one access panel's face for a ConduitContainer, 3.5 m wide (7 cells) and 4 m tall. A skin of
#              panels over a dark backing, a skirt low down, half a rib at each edge so two containers side
#              by side make one whole rib. Origin at the bottom middle of its back; it faces +Z in Unity.
#   FloorTile: 4 x 4 m of 1 m plates on a dark backing, seams showing between. Origin at the middle of its top.
# Run: blender -b --python Blender/Walls/ShipWalls.py -- <Meshes dir>
import bpy, bmesh, os, sys
from mathutils import Vector

OUT = sys.argv[sys.argv.index("--") + 1]

# Unity material names, remapped on import.
NAMES = ["ShipPanel", "FuelPackMetal", "FuelPackDark", "ShipSkirt", "ShipFloor"]
PANEL, RIB, SEAM, SKIRT, FLOOR = range(5)

W, H = 3.5, 4.0            # wall section: seven cells wide, from the floor (y -0.25) to 3.75
T = 0.12                   # skin thickness
RIB_W, RIB_D = 0.2, 0.14   # a whole rib's width, and how far it stands proud of the skin
GAP = 0.025                # panel seam
SKIN = 0.02                # panels' thickness on the skin

TILE, PLATE = 4.0, 1.0     # floor tile, and its plates
BACKING = 0.1


def unity(x, y, z):
    """Unity local (x, y, z) to Blender, for this export's axes: Unity y is Blender z, Unity z is Blender -y."""
    return Vector((x, -z, y))


def box(bm, lo, hi, mat, chamfer=0.0):
    """A box between two corners given in Unity local coordinates."""
    a, b = unity(*lo), unity(*hi)
    lo_b = Vector((min(a.x, b.x), min(a.y, b.y), min(a.z, b.z)))
    hi_b = Vector((max(a.x, b.x), max(a.y, b.y), max(a.z, b.z)))
    r = bmesh.ops.create_cube(bm, size=1.0)
    verts = r["verts"]
    for v in verts:
        v.co = Vector((hi_b.x if v.co.x > 0 else lo_b.x, hi_b.y if v.co.y > 0 else lo_b.y, hi_b.z if v.co.z > 0 else lo_b.z))
    faces = list({f for v in verts for f in v.link_faces})
    for f in faces:
        f.material_index = mat
    if chamfer > 0:
        edges = list({e for f in faces for e in f.edges})
        made = bmesh.ops.bevel(bm, geom=edges, offset=chamfer, segments=1, affect="EDGES", profile=0.5)
        for f in made["faces"]:
            f.material_index = mat   # a bevel's new faces would otherwise take material 0


def wall(bm):
    hw = W / 2
    box(bm, (-hw, 0, 0), (hw, H, T), SEAM)
    columns = [(-hw + RIB_W / 2 + GAP, -GAP / 2), (GAP / 2, hw - RIB_W / 2 - GAP)]
    for a, b in columns:
        box(bm, (a, 0.02, T), (b, 0.35, T + SKIN), SKIRT, 0.005)
        box(bm, (a, 0.35 + GAP, T), (b, H * 0.5 - GAP / 2, T + SKIN), PANEL, 0.01)
        box(bm, (a, H * 0.5 + GAP / 2, T), (b, H - 0.02, T + SKIN), PANEL, 0.01)
    for s in (-1, 1):
        x0, x1 = sorted((s * hw, s * (hw - RIB_W / 2)))
        box(bm, (x0, 0, T), (x1, H, T + RIB_D), RIB, 0.01)


def floor(bm):
    h = TILE / 2
    box(bm, (-h, -SKIN - BACKING, -h), (h, -SKIN, h), SEAM)
    n = round(TILE / PLATE)
    for i in range(n):
        for j in range(n):
            x0, z0 = -h + i * PLATE, -h + j * PLATE
            box(bm, (x0 + GAP, -SKIN, z0 + GAP), (x0 + PLATE - GAP, 0, z0 + PLATE - GAP), FLOOR, 0.006)


def export(name, build):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bm = bmesh.new()
    build(bm)
    # Only the materials this mesh uses, so Unity gets one submesh per material it shows.
    used = sorted({f.material_index for f in bm.faces})
    for f in bm.faces:
        f.material_index = used.index(f.material_index)
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    for i in used:
        mesh.materials.append(bpy.data.materials.new(NAMES[i]))
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    path = os.path.join(OUT, name + ".fbx")
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, apply_unit_scale=True, apply_scale_options="FBX_SCALE_UNITS",
                             axis_forward="-Z", axis_up="Y", bake_space_transform=True, mesh_smooth_type="FACE")
    print("EXPORTED", path, len(mesh.polygons), "faces", [round(v, 3) for v in obj.dimensions])


export("WallPanel", wall)
export("FloorTile", floor)
