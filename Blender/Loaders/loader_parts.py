# Shared parts for every loader model (LoaderS2.py ... LoaderS5.py), so all sizes are built the same way.
#
# Conventions (docs/README.md -> Models):
#   - One script per prefab, named after it without the space: "Loader S3" -> LoaderS3.py, saving LoaderS3.blend
#     next to it and exporting Assets/#User/SpaceScape/Meshes/LoaderS3.fbx.
#   - Run: blender -b --python Blender/Loaders/LoaderS3.py -- Blender/Loaders/LoaderS3.blend "Assets/#User/SpaceScape/Meshes/LoaderS3.fbx"
#   - Origin at the centre of the slot volume. Blender is Z up and front is -Y; after export -Y is Unity +Z (the
#     front) and Blender +X is Unity -X, so the loading side (the viewer's right, seen from the front) is Blender +X
#     and the exit (the viewer's left) is Blender -X.
#   - Materials are named FuelPackMetal (0), FuelPackDark (1) and ComponentItem (2, amber); Unity remaps them.
#   - Amber only on the front opening, the mechanism's strip, the lift guides and the front rails' tips.
import bpy, bmesh, math, os, sys
from mathutils import Vector, Matrix

METAL, DARK, AMBER = 0, 1, 2

LEG = 0.15               # every leg is this square
BRACE = 0.08             # every diagonal brace is this square
# The brace rule (decided 2026-10-09): a brace meets its leg halfway between the floor and the leg's top, so taller
# legs get taller braces. Its foot lands at 45 degrees (as far out as the brace is high) unless that would leave the
# volume; then it lands at the volume's edge and the brace is steeper. Floor beams run out to the feet.
BEAM_H = 0.10            # floor beams' height
PANEL = 0.18             # the power port's panel: square, flush with the back face
PANEL_T = 0.06           # its thickness


def args():
    """The .blend and .fbx paths given after --."""
    return sys.argv[sys.argv.index("--") + 1:]


def start():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    return [
        material("FuelPackMetal", (0.16, 0.17, 0.19), 1.0, 0.3),
        material("FuelPackDark", (0.03, 0.03, 0.035), 0.8, 0.6),
        material("ComponentItem", (1.0, 0.36, 0.0), 0.0, 0.4, emit=(1.0, 0.26, 0.0)),
    ]


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


def box(bm, lo, hi, mat=METAL, chamfer=0.0):
    r = bmesh.ops.create_cube(bm, size=1.0)
    verts = r["verts"]
    for v in verts:
        v.co = Vector((hi[0] if v.co.x > 0 else lo[0], hi[1] if v.co.y > 0 else lo[1], hi[2] if v.co.z > 0 else lo[2]))
    faces = list({f for v in verts for f in v.link_faces})
    if chamfer > 0:
        edges = list({e for v in verts for e in v.link_edges})
        geom = bmesh.ops.bevel(bm, geom=verts + edges, offset=chamfer, segments=1, affect="EDGES", profile=0.5)
        faces = list(set(faces) | set(geom["faces"]))
    for f in faces:
        if f.is_valid: f.material_index = mat
    return verts


def beam(bm, a, b, thick, mat=METAL):
    """A square beam from point a to point b."""
    a, b = Vector(a), Vector(b)
    d = b - a
    r = bmesh.ops.create_cube(bm, size=1.0)
    rot = d.to_track_quat("Z", "Y").to_matrix().to_4x4()
    m = Matrix.Translation((a + b) / 2) @ rot @ Matrix.Diagonal((thick, thick, d.length, 1.0))
    bmesh.ops.transform(bm, matrix=m, verts=r["verts"])
    for f in {f for v in r["verts"] for f in v.link_faces}: f.material_index = mat


def roller(bm, x, y, z, length, radius=0.035, mat=DARK, axis="Y"):
    """A roller across a runner: axis Y for runners along X, axis X for runners along Y."""
    rot = Matrix.Rotation(math.radians(90), 4, "X" if axis == "Y" else "Y")
    r = bmesh.ops.create_cone(bm, cap_ends=True, segments=10, radius1=radius, radius2=radius, depth=length,
                              matrix=Matrix.Translation((x, y, z)) @ rot)
    for f in {f for v in r["verts"] for f in v.link_faces}: f.material_index = mat


def leg(bm, x, y, bottom, top):
    box(bm, (x - LEG / 2, y - LEG / 2, bottom), (x + LEG / 2, y + LEG / 2, top), METAL, 0.015)


def brace(bm, x, leg_y, sy, beam_top, leg_top, edge):
    """The one diagonal brace, in the front-to-back plane: from the centreline of the leg at (x, leg_y), halfway
    between the floor and leg_top, out along sy (+1 back, -1 front) to the floor beam, at 45 degrees unless the foot
    would pass edge (half the volume's depth); then it lands at the edge, steeper. Its top end is sunk into the leg.
    Returns the y of its foot, which the floor beam must reach."""
    floor = beam_top - BEAM_H
    top = floor + (leg_top - floor) / 2
    reach = min(top - beam_top, edge - 0.06 - abs(leg_y))
    foot_y = leg_y + sy * reach
    beam(bm, (x, leg_y, top), (x, foot_y, beam_top), BRACE)
    box(bm, (x - 0.06, foot_y - 0.06, beam_top), (x + 0.06, foot_y + 0.06, beam_top + 0.04), METAL, 0.01)  # shoe
    return foot_y


def floor_beam(bm, x, y0, y1, floor):
    """A floor beam front to back under a pair of legs, from y0 to y1."""
    box(bm, (x - LEG / 2, y0, floor), (x + LEG / 2, y1, floor + BEAM_H), METAL, 0.01)


def power_panel(bm, x, back_y, z, beam_x, beam_top):
    """The power In port's panel: a dark square flush with the back face (back_y) centred on the port at (x, z), on a
    block linking it back into the frame. The block stands on the floor beam at beam_x (which must reach back to
    the panel) and its top is flush with the panel's top."""
    h = PANEL / 2
    box(bm, (min(x - h, beam_x - 0.07), back_y - 0.22, beam_top), (max(x + h, beam_x + 0.07), back_y - PANEL_T, z + h), METAL, 0.01)
    box(bm, (x - h, back_y - PANEL_T, z - h), (x + h, back_y, z + h), DARK, 0.01)


def new_object(name, mats):
    mesh = bpy.data.meshes.new(name)
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    for m in mats: obj.data.materials.append(m)
    return obj, bmesh.new()


def finish(obj, bm):
    bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=0.0001)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    bm.to_mesh(obj.data)
    bm.free()
    for p in obj.data.polygons: p.use_smooth = False


def export(out_blend, out_fbx):
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
