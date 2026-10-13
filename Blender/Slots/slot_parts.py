# The empty slot's plinth (docs/slots.md -> Rules for every size): a plinth filling the slot's footprint with its size
# engraved into the top, inlaid amber, so a slot reads at a glance, both that it is there and what size it takes. The
# slot hides it while a component is installed (ComponentSlot). One script per slot prefab: "Slot S2" -> SlotS2.py,
# exporting Meshes/SlotS2.fbx.
#
#   Draft (saves the .blend and renders review shots):
#     blender -b --python Blender/Slots/SlotS2.py -- Blender/Slots/SlotS2.blend
#   Final:
#     blender -b --python Blender/Slots/SlotS2.py -- Blender/Slots/SlotS2.blend "Assets/#User/SpaceScape/Meshes/SlotS2.fbx"
import os, sys
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Loaders"))
from loader_parts import *

CELL = 0.5
LABEL = 0.7                     # the label fills this share of the top, across or deep, whichever binds first
ENGRAVE = 0.012                 # how deep the label is cut
INLAY_BELOW = 0.004             # how far below the top the amber inlay sits
FONT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "Assets", "Editor Default Resources",
                    "SomniumSpace", "Fonts", "SourceSansPro-Black.ttf")


def label_mesh(text, top, bottom, width, depth):
    """The label as a mesh object, centred on the top, reading from the front, fitted to LABEL of the top, filling
    from bottom to top (Blender z)."""
    bpy.ops.object.text_add()
    t = bpy.context.object
    t.data.body = text
    t.data.font = bpy.data.fonts.load(FONT, check_existing=True)
    t.data.align_x, t.data.align_y = "CENTER", "CENTER"
    t.data.extrude = 0.5
    t.data.resolution_u = 3                    # few faces: it is flat-shaded and small
    bpy.context.view_layer.update()
    w, d = t.dimensions.x, t.dimensions.y
    scale = min(LABEL * width / w, LABEL * depth / d)
    t.scale = (scale, scale, (top - bottom) / 1.0)
    t.location = (0, 0, (top + bottom) / 2)
    bpy.ops.object.convert(target="MESH")
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    return bpy.context.object


def build(size, cells):
    """The plinth for slot size 'S2' and its cells (x, y, z), origin at the slot's centre."""
    paths = args()
    out_blend, out_fbx = paths[0], (paths[1] if len(paths) > 1 else None)
    mats = start()
    hx, hy, hz = (c * CELL / 2 for c in cells)
    height = 0.08 if size == "S0" else 0.25          # the S0's is a plate; every other size's, a component's plinth
    floor, top = -hy, -hy + height

    plinth, bm = new_object("Plinth", mats)
    a, b = U((hx, floor, hz)), U((-hx, top, -hz))
    box(bm, [min(a[i], b[i]) for i in range(3)], [max(a[i], b[i]) for i in range(3)], METAL, 0.02 if size == "S0" else 0.03)
    finish(plinth, bm)

    cutter = label_mesh(size, top + 0.01, top - ENGRAVE, 2 * hx, 2 * hz)
    cut = plinth.modifiers.new("Engrave", "BOOLEAN")
    cut.operation, cut.object, cut.solver = "DIFFERENCE", cutter, "EXACT"
    bpy.context.view_layer.objects.active = plinth
    bpy.ops.object.modifier_apply(modifier=cut.name)
    bpy.data.objects.remove(cutter)

    inlay = label_mesh(size, top - INLAY_BELOW, top - ENGRAVE, 2 * hx, 2 * hz)
    inlay.data.materials.clear()
    for m in mats: inlay.data.materials.append(m)
    for p in inlay.data.polygons: p.material_index = AMBER
    bpy.ops.object.select_all(action="DESELECT")
    inlay.select_set(True)
    plinth.select_set(True)
    bpy.context.view_layer.objects.active = plinth
    bpy.ops.object.join()
    for p in plinth.data.polygons: p.use_smooth = False

    if out_fbx:
        export(out_blend, out_fbx)
        return
    bpy.ops.wm.save_as_mainfile(filepath=out_blend)
    render(os.path.splitext(out_blend)[0], shots_for(max(hx, hz)))
    print("DONE", plinth.name, len(plinth.data.polygons), "faces", [round(v, 3) for v in plinth.dimensions])
