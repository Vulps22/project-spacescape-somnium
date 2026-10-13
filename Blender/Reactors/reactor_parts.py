# Shared parts for the S1 to S3 reactor models (ReactorS1.py ... ReactorS3.py), so all sizes are built the same way.
# Design: docs/reactor.md -> Sizes. Conventions: docs/tooling.md -> Models; the house look: docs/component-models.md.
# The house parts (plinth, frame and braces, I/O panel, the shared screen) are the batteries' (battery_parts.py).
#
# Positions are written in UNITY coordinates (x, y, z): front +z, up +y, origin at the centre of the slot volume.
# U() turns them into Blender's (Z up, front -Y): Blender (X, Y, Z) = (-x, -z, y). The viewer's right, seen from the
# front, is Unity -x.
#
#   Draft (saves the .blend and renders review shots, exports nothing):
#     blender -b --python Blender/Reactors/ReactorS3.py -- Blender/Reactors/ReactorS3.blend
#   Final:
#     blender -b --python Blender/Reactors/ReactorS3.py -- Blender/Reactors/ReactorS3.blend "Assets/#User/SpaceScape/Meshes/ReactorS3.fbx"
import os, sys
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Batteries"))
from battery_parts import *

CORE = 4
COLLAR = 0.72                   # solid block round an item port (0.56 m square, sinks 0.29 m), as on the S5
ROD_R, ROD_LEN = 0.22, 1.0      # a fuel rod: 0.44 m across the corners, 1 m long
# The control bay, as the S5's (Reactor.py): the deck the lever stands on, 0.36 m deep rising 0.22 m to the back, and
# the screen clear of the lever's handle above it. The tower is deep enough to hold the screen.
DECK_DEPTH, DECK_RISE = 0.36, 0.22
SCREEN_CLEAR = 0.33             # where the size has room; a lower screen on a short model is fine
TOWER_DEPTH = 0.25


class Reactor(Battery):
    """One reactor model: the battery's house parts with the reactor's materials. Parts that only show the idea in a
    draft (fuel rods, levers) go in self.draft and are left out of the FBX."""

    def __init__(self, hx, hy, hz, post=0.15):
        self.hx, self.hy, self.hz, self.post = hx, hy, hz, post
        self.floor = -hy
        self.base = -hy + PLINTH
        self.top = hy
        self.px, self.pz = hx - 0.2, hz - 0.2
        self.io_top = self.floor + 0.5
        paths = args()
        self.out_blend = paths[0]
        self.out_fbx = paths[1] if len(paths) > 1 else None
        metal, dark, amber = start()
        glass = material("ReactorGlass", (0.75, 0.88, 1.0), 0.0, 0.05)              # the S5's glass and core
        glass.node_tree.nodes["Principled BSDF"].inputs["Alpha"].default_value = 0.12
        glass.surface_render_method = "BLENDED"
        core = material("ReactorCore", (0.2, 0.6, 1.0), 0.0, 0.3, emit=(0.15, 0.55, 1.0))
        self.mats = [metal, dark, amber, glass, core]
        self.preview = []
        self.draft = []
        self.looks = []                             # draft only: (object, colour, strength) for parts the prefab lights

    def add_material(self, name, colour, metallic, rough, emit=None, alpha=None):
        m = material(name, colour, metallic, rough, emit)
        if alpha is not None:
            m.node_tree.nodes["Principled BSDF"].inputs["Alpha"].default_value = alpha
            m.surface_render_method = "BLENDED"
        self.mats.append(m)
        return len(self.mats) - 1

    def io_panel(self, bm, xs, inner=None):
        """The I/O panel on the back at the base, one cell per port at the Unity x given, a dark plate flush with the
        back face carrying them."""
        inner = -self.hz + IO_DEPTH if inner is None else inner
        x0, x1 = min(xs) - 0.25, max(xs) + 0.25
        self.ub(bm, (x0, self.floor, inner), (x1, self.io_top, -self.hz), METAL, 0.02)
        self.ub(bm, (x0 + 0.03, self.floor + 0.04, -self.hz + 0.06), (x1 - 0.03, self.io_top - 0.04, -self.hz), DARK, 0.01)
        return inner

    def port_collar(self, bm, x, z, bottom, size=COLLAR):
        """The solid block round an item port on the top face at cell (x, z), from bottom up to the top, amber round
        its rim."""
        h = size / 2
        self.ub(bm, (x - h, bottom, z - h), (x + h, self.top, z + h), METAL, 0.025)
        self.ub(bm, (x - h - 0.01, self.top - 0.05, z - h - 0.01), (x + h + 0.01, self.top - 0.02, z + h + 0.01), AMBER)

    def control_bay(self, bm, x0, x1, deck_front, deck_y=0.65, screen_clear=SCREEN_CLEAR):
        """The control panel as the S5's control bay, one side profile carried across x0..x1: a front face, a deck
        sloping up and back where the lever stands (deck_y above the plinth at its front edge), and a tower behind it
        carrying the shared screen above the lever's handle. Returns (deck, screen): the deck's front and back edges
        as Unity (z, y) for the lever, and the point at the back of the screen, where Data arrives."""
        front = (deck_front, self.base + deck_y)
        back = (deck_front - DECK_DEPTH, front[1] + DECK_RISE)
        sy0 = back[1] + screen_clear
        sy1 = sy0 + SCREEN_H + 0.03
        top = sy1 + 0.06
        tower = back[0] - TOWER_DEPTH
        profile = [(tower, self.base), (front[0], self.base), front, back, (back[0], top), (tower, top)]
        profile_prism(self, bm, profile, x0, x1, [METAL, METAL, DARK, METAL, METAL, METAL])
        fz, fy = front
        self.ub(bm, (x0 - 0.01, fy - 0.03, fz - 0.03), (x1 + 0.01, fy + 0.02, fz + 0.02), AMBER, 0.008)     # amber on the deck's edge
        self.ub(bm, (x0 + 0.06, self.base + 0.1, fz), (x1 - 0.06, fy - 0.12, fz + 0.02), DARK, 0.01)          # front inset
        tz = back[0]
        cx = (x0 + x1) / 2
        self.ub(bm, (cx - SCREEN_W / 2 - 0.015, sy0, tz), (cx + SCREEN_W / 2 + 0.015, sy1, tz + 0.012), AMBER, 0.004)   # screen frame
        self.ub(bm, (cx - SCREEN_W / 2, sy0 + 0.015, tz + 0.012), (cx + SCREEN_W / 2, sy1 - 0.015, tz + 0.02), DARK)  # the screen
        return (front, back), (cx, (sy0 + sy1) / 2, tower)

    # -- draft only -----------------------------------------------------------------------------------------------
    def draft_rod(self, x, z, bottom, colour=(0.2, 0.6, 1.0), axis="y"):
        """A fuel rod where the runtime one would be, glowing by its fuel. Not exported: the prefab copies the Fuel
        Rod's own model."""
        o, bm = new_object(f"Draft Rod {len(self.draft)}", self.mats)
        m = material(f"DraftRod{len(self.draft)}", colour, 0.0, 0.3, emit=colour)
        m.node_tree.nodes["Principled BSDF"].inputs["Emission Strength"].default_value = 1.0
        o.data.materials.append(m)
        centre = (x, bottom + ROD_LEN / 2, z) if axis == "y" else (x, bottom, z)
        self.prism(bm, centre, axis, ROD_LEN, ROD_R, len(self.mats))
        finish(o, bm)
        self.draft.append(o)
        return o

    def draft_lever(self, x, deck):
        """A lever where the runtime RodLever stands on a control bay's deck, as the S5's draft levers: a base along
        its swing and an arm with an amber knob. Not exported."""
        o, bm = new_object(f"Draft Lever {len(self.draft)}", self.mats)
        (fz, fy), (bz, by) = deck
        tilt = math.atan2(by - fy, fz - bz)
        c, sn = math.cos(tilt), math.sin(tilt)
        mz, my = (fz + bz) / 2, (fy + by) / 2
        def on_deck(ly, lz): return (x, my + ly * c - lz * sn, mz + ly * sn + lz * c)
        self.ubeam(bm, on_deck(0.0, -0.16), on_deck(0.0, 0.16), 0.06, DARK)       # base, along the swing
        self.ubeam(bm, on_deck(0.03, 0.0), on_deck(0.45, 0.0), 0.035, METAL)     # arm
        tx, ty, tz = on_deck(0.45, 0.0)
        self.ub(bm, (tx - 0.045, ty - 0.045, tz - 0.045), (tx + 0.045, ty + 0.045, tz + 0.045), AMBER, 0.01)
        finish(o, bm)
        self.draft.append(o)

    def look(self, obj, colour, strength=2.0):
        """Draft only: how the prefab's readout script will light this part (it colours the part at runtime)."""
        self.looks.append((obj, colour, strength))

    # -- output -----------------------------------------------------------------------------------------------------
    def finish_all(self, shots, extra=None):
        """Draft: save the .blend and the review shots (extra: (pose, shots) to set a pose and shoot again). Final:
        drop the draft parts and export."""
        if self.out_fbx:
            for o in self.draft: bpy.data.objects.remove(o)
            export(self.out_blend, self.out_fbx)
            return
        for o, colour, strength in self.looks:
            m = material(f"Preview {o.name}", colour, 0.0, 0.3, emit=colour)
            m.node_tree.nodes["Principled BSDF"].inputs["Emission Strength"].default_value = strength
            o.data.materials.clear()
            for _ in self.mats: o.data.materials.append(m)
        bpy.ops.wm.save_as_mainfile(filepath=self.out_blend)
        stem = os.path.splitext(self.out_blend)[0]
        render(stem, shots)
        if extra:
            pose, more = extra
            pose()
            cameras(stem, more)
        for obj in bpy.data.objects:
            if obj.type == "MESH":
                print("DONE", obj.name, len(obj.data.polygons), "faces", [round(v, 3) for v in obj.dimensions])


def cameras(stem, shots):
    """More review shots in a scene render() has already lit."""
    scene = bpy.context.scene
    for name, cam_at, target in shots:
        bpy.ops.object.camera_add(location=U(cam_at))
        cam = bpy.context.object
        cam.rotation_mode = "QUATERNION"
        cam.rotation_quaternion = (U(target) - U(cam_at)).to_track_quat("-Z", "Y")
        cam.data.lens = 35
        scene.camera = cam
        scene.render.filepath = f"{stem}_{name}.png"
        bpy.ops.render.render(write_still=True)


def set_origin(obj, at):
    """Moves an object's origin to a Unity point, leaving its mesh where it is."""
    p = U(at)
    obj.data.transform(Matrix.Translation(-p))
    obj.location = p
    bpy.context.view_layer.update()


def attach(child, parent):
    """Parents child to parent, leaving it where it is, so it moves with the parent from here on."""
    bpy.context.view_layer.update()
    child.parent = parent
    child.matrix_parent_inverse = parent.matrix_world.inverted()


def outward(bm, faces):
    """Turns a hand-built solid's faces to face out of it, whichever way its corners were given: Unity draws only
    a face's front, so an inward face shows as a hole."""
    bmesh.ops.recalc_face_normals(bm, faces=list(faces))


def finish_solids(obj, bm):
    """finish() for a part built of solids that touch face to face (the S3's wall slabs): it keeps each solid apart
    and its faces as outward() turned them. Merging them first leaves faces shared inside the part, and the normals
    worked out over that face the wrong way."""
    bm.to_mesh(obj.data)
    bm.free()
    for p in obj.data.polygons: p.use_smooth = False


def profile_prism(b, bm, pts, x0, x1, mats):
    """A prism across Unity x from x0 to x1, its side profile pts given as Unity (z, y); mats[i] is the material of
    the face along edge i (pts[i] to pts[i + 1]). As the S5's control bay (Reactor.py)."""
    a = [bm.verts.new(U((x0, y, z))) for (z, y) in pts]
    c = [bm.verts.new(U((x1, y, z))) for (z, y) in pts]
    faces = [bm.faces.new(a), bm.faces.new(list(reversed(c)))]
    for f in faces: f.material_index = METAL
    n = len(pts)
    for i in range(n):
        j = (i + 1) % n
        f = bm.faces.new((a[i], a[j], c[j], c[i]))
        f.material_index = mats[i]
        faces.append(f)
    outward(bm, faces)


def ring(b, bm, centre, axis, r_in, r_out, width, mat, segments=16):
    """A flat ring (a coil, a band) round a Unity axis 'x' or 'y', mitred at every corner."""
    corners = []
    for k in range(segments):
        a = 2 * math.pi * k / segments
        ca, sa = math.cos(a), math.sin(a)
        quad = []
        for r, d in ((r_in, -width / 2), (r_out, -width / 2), (r_out, width / 2), (r_in, width / 2)):
            if axis == "x": p = (centre[0] + d, centre[1] + r * ca, centre[2] + r * sa)
            else: p = (centre[0] + r * ca, centre[1] + d, centre[2] + r * sa)
            quad.append(bm.verts.new(U(p)))
        corners.append(quad)
    faces = []
    for k in range(segments):
        c, n = corners[k], corners[(k + 1) % segments]
        for j in range(4):
            f = bm.faces.new((c[j], n[j], n[(j + 1) % 4], c[(j + 1) % 4]))
            f.material_index = mat
            faces.append(f)
    outward(bm, faces)
