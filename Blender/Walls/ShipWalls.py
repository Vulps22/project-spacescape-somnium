# The ship's wall and floor, in the ship look sample's style (Ship Look Sample in the scene, Meshes/ShipSample.fbx).
#   WallPanel: one access panel's face for a ConduitContainer, 3.5 m wide (7 cells) and 4 m tall. A skin of
#              panels over a dark backing, a skirt low down, half a rib at each edge so two containers side
#              by side make one whole rib. Origin at the bottom middle of its back; it faces +Z in Unity.
#   WallPanelS0: the same, with an alcove for an S0 slot in the middle of the bottom-right panel (seen from the
#              room): the slot's cell (0.5 m) sunk a whole cell into the wall, in the conduit channel, with 1 cm
#              spare at the top, sides and back, its floor level with the cell's bottom; lined, and framed in gunmetal.
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

TRIM_W, TRIM_D = 0.03, 0.01  # the alcove's frame: width round the opening, and how far it stands proud of the panels
SPARE = 0.01               # room round a slotted component in its alcove

LINER = 0.02               # the alcove's lining

# The S0 alcove, in the panel's own frame (the container's local frame, less the panel's offset of (0, -0.25, -4.271)).
# The slot's cell is the container's (-1, 1, -4.5) cell, in the channel: x -1.25..-0.75, y 0.75..1.25 (here 1.0..1.5),
# z -4.75..-4.25, so its back is at z -0.479 here.
PANEL_Z = -4.271
S0_ALCOVE = (-1.25 - SPARE, -0.75 + SPARE, 1.0, 1.5 + SPARE, (-4.75 - PANEL_Z) - SPARE)


TILE, PLATE = 4.0, 1.0     # floor tile, and its plates
BACKING = 0.1


def unity(x, y, z):
    """Unity local (x, y, z) to Blender, for this export's axes: Unity x is Blender -x (checked by a raycast in Unity),
    y is Blender z, z is Blender -y."""
    return Vector((-x, -z, y))


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


def holed(bm, lo, hi, hole, mat, chamfer=0.0):
    """A box with a rectangular hole through it front to back, as up to four boxes round the hole.
    hole is (x0, x1, y0, y1); a hole that misses the box leaves it whole."""
    x0, x1, y0, y1 = hole
    if x1 <= lo[0] or x0 >= hi[0] or y1 <= lo[1] or y0 >= hi[1]:
        box(bm, lo, hi, mat, chamfer)
        return
    x0, x1, y0, y1 = max(x0, lo[0]), min(x1, hi[0]), max(y0, lo[1]), min(y1, hi[1])
    pieces = [((lo[0], lo[1]), (x0, hi[1])), ((x1, lo[1]), (hi[0], hi[1])),
              ((x0, lo[1]), (x1, y0)), ((x0, y1), (x1, hi[1]))]
    for (a, b), (c, d) in pieces:
        if c - a > 1e-4 and d - b > 1e-4:
            box(bm, (a, b, lo[2]), (c, d, hi[2]), mat, chamfer)


def wall(bm, alcoves=()):
    """The wall section, with an alcove (x0, x1, y0, y1, back z) cut through the skin and panels for each one given."""
    hw = W / 2
    holes = [a[:4] for a in alcoves]
    # The panels are cut 5 mm back from the opening, under the trim, so their edges are not in the plane of the trim's
    # inner faces (they z-fought). The skin's cut edge is the opening's dark front edge; the lining starts behind it.
    def wider(hole, by): return (hole[0] - by, hole[1] + by, hole[2] - by, hole[3] + by)
    skin = [((-hw, 0, 0), (hw, H, T))]
    for hole in holes:
        skin = cut(skin, hole)
    for lo, hi in skin:
        box(bm, lo, hi, SEAM)
    for x0, x1, y0, y1, back in alcoves:
        # The lining: a back, two sides, a top and a floor, from the alcove's back to the back of the skin.
        box(bm, (x0 - LINER, y0 - LINER, back - LINER), (x1 + LINER, y1 + LINER, back), SKIRT)
        box(bm, (x0 - LINER, y0 - LINER, back), (x0, y1 + LINER, 0), PANEL)
        box(bm, (x1, y0 - LINER, back), (x1 + LINER, y1 + LINER, 0), PANEL)
        box(bm, (x0, y1, back), (x1, y1 + LINER, 0), PANEL)
        box(bm, (x0, y0 - LINER, back), (x1, y0, 0), SKIRT)
    columns = [(-hw + RIB_W / 2 + GAP, -GAP / 2), (GAP / 2, hw - RIB_W / 2 - GAP)]
    for a, b in columns:
        for lo, hi, mat, ch in [((a, 0.02, T), (b, 0.35, T + SKIN), SKIRT, 0.005),
                                ((a, 0.35 + GAP, T), (b, H * 0.5 - GAP / 2, T + SKIN), PANEL, 0.01),
                                ((a, H * 0.5 + GAP / 2, T), (b, H - 0.02, T + SKIN), PANEL, 0.01)]:
            parts = [(lo, hi)]
            for hole in holes:
                parts = cut(parts, wider(hole, 0.005))
            for plo, phi in parts:
                box(bm, plo, phi, mat, ch)
    for x0, x1, y0, y1 in holes:
        holed(bm, (x0 - TRIM_W, y0 - TRIM_W, T), (x1 + TRIM_W, y1 + TRIM_W, T + SKIN + TRIM_D), (x0, x1, y0, y1), RIB, 0.004)
    for s in (-1, 1):
        x0, x1 = sorted((s * hw, s * (hw - RIB_W / 2)))
        box(bm, (x0, 0, T), (x1, H, T + RIB_D), RIB, 0.01)


def cut(boxes, hole):
    """Boxes (lo, hi) with a rectangular hole (x0, x1, y0, y1) taken out of each, front to back."""
    x0, x1, y0, y1 = hole
    out = []
    for lo, hi in boxes:
        if x1 <= lo[0] or x0 >= hi[0] or y1 <= lo[1] or y0 >= hi[1]:
            out.append((lo, hi))
            continue
        cx0, cx1, cy0, cy1 = max(x0, lo[0]), min(x1, hi[0]), max(y0, lo[1]), min(y1, hi[1])
        for (a, b), (c, d) in [((lo[0], lo[1]), (cx0, hi[1])), ((cx1, lo[1]), (hi[0], hi[1])),
                               ((cx0, lo[1]), (cx1, cy0)), ((cx0, cy1), (cx1, hi[1]))]:
            if c - a > 1e-4 and d - b > 1e-4:
                out.append(((a, b, lo[2]), (c, d, hi[2])))
    return out


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
export("WallPanelS0", lambda bm: wall(bm, [S0_ALCOVE]))
export("FloorTile", floor)
