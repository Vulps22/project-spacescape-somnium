# Shared parts for every battery model (BatteryS0.py ... BatteryS5.py), so all sizes are built the same way.
# Design: docs/battery.md. Conventions: docs/tooling.md -> Models; the house look: docs/component-models.md.
#
# Positions are written in UNITY coordinates (x, y, z): front +z, up +y, origin at the centre of the slot volume.
# U() turns them into Blender's (Z up, front -Y): Blender (X, Y, Z) = (-x, -z, y). The viewer's right, seen from the
# front, is Unity -x.
#
#   Draft (saves the .blend and renders review shots, exports nothing):
#     blender -b --python Blender/Batteries/BatteryS1.py -- Blender/Batteries/BatteryS1.blend
#   Final:
#     blender -b --python Blender/Batteries/BatteryS1.py -- Blender/Batteries/BatteryS1.blend "Assets/#User/SpaceScape/Meshes/BatteryS1.fbx"
import os, sys
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Loaders"))
from loader_parts import *

GLASS, CELL = 3, 4
PLINTH = 0.25                   # half a cell, every size
TB = 0.14                       # top beams' height and width
WIRE = 0.06                     # a wire run's square section
# The I/O panel: three cells wide and one tall on the back face's bottom row. In, Data and Out are Unity x -0.75,
# -0.25, +0.25: In on the viewer's right, Out on the left, seen from the front.
IO_IN, IO_DATA, IO_OUT = -0.75, -0.25, 0.25
IO_X = (-1.0, 0.5)
IO_DEPTH = 0.35                 # from the slot's back edge in to the model
# Every size's screen is the same (Vulps, 2026-10-11), and its control panel is sized to fit it.
SCREEN_W, SCREEN_H = 0.575, 0.70    # the dark plate the readouts sit on
SCREEN_BELOW = 0.4375               # panel below the screen's frame
PANEL_W = SCREEN_W + 0.11           # the panel: the plate, its amber frame and the panel's edges
PANEL_H = SCREEN_BELOW + SCREEN_H + 0.03 + 0.08


class Battery:
    """One battery model: half-sizes hx, hy, hz (Unity), its materials, and what it draws into."""

    def __init__(self, hx, hy, hz, post=0.15):
        self.hx, self.hy, self.hz, self.post = hx, hy, hz, post
        self.floor = -hy
        self.base = -hy + PLINTH                    # the plinth's top, where everything stands
        self.top = hy                               # the frame's top edge
        self.px, self.pz = hx - 0.2, hz - 0.2       # the corner posts' centres
        self.io_top = self.floor + 0.5
        paths = args()
        self.out_blend = paths[0]
        self.out_fbx = paths[1] if len(paths) > 1 else None
        metal, dark, amber = start()
        glass = material("BatteryGlass", (0.75, 0.88, 1.0), 0.0, 0.05)
        glass.node_tree.nodes["Principled BSDF"].inputs["Alpha"].default_value = 0.15
        glass.surface_render_method = "BLENDED"
        # Every part that shows charge carries this one material; the prefab's readout script colours each renderer
        # (black empty, violet from 1%, white full) from the battery's shared charge.
        cell = material("BatteryCell", (1.0, 1.0, 1.0), 0.0, 0.2, emit=(1.0, 1.0, 1.0))
        self.mats = [metal, dark, amber, glass, cell]
        self.preview = []                           # (object, fill 0..1): coloured in the draft only

    # -- drawing ------------------------------------------------------------------------------------------------
    def ub(self, bm, lo, hi, mat=METAL, chamfer=0.0):
        a, b = U(lo), U(hi)
        box(bm, [min(a[i], b[i]) for i in range(3)], [max(a[i], b[i]) for i in range(3)], mat, chamfer)

    def ubeam(self, bm, a, b, thick, mat=METAL):
        beam(bm, U(a), U(b), thick, mat)

    def prism(self, bm, centre, axis, length, radius, mat, segments=6, chamfer=0.0, turn=30):
        """A prism (hexagonal by default) centred on a Unity point, its length along Unity axis 'x', 'y' or 'z'."""
        rot = {"y": Matrix.Rotation(math.radians(turn), 4, "Z"),
               "z": Matrix.Rotation(math.radians(90), 4, "X") @ Matrix.Rotation(math.radians(turn), 4, "Z"),
               "x": Matrix.Rotation(math.radians(90), 4, "Y") @ Matrix.Rotation(math.radians(turn), 4, "Z")}[axis]
        r = bmesh.ops.create_cone(bm, cap_ends=True, segments=segments, radius1=radius, radius2=radius, depth=length,
                                  matrix=Matrix.Translation(U(centre)) @ rot)
        verts = r["verts"]
        faces = list({f for v in verts for f in v.link_faces})
        if chamfer > 0:
            edges = list({e for v in verts for e in v.link_edges})
            geom = bmesh.ops.bevel(bm, geom=verts + edges, offset=chamfer, segments=1, affect="EDGES", profile=0.5)
            faces = list(set(faces) | set(geom["faces"]))
        for f in faces:
            if f.is_valid: f.material_index = mat

    def run(self, bm, pts, mat=DARK, t=WIRE):
        """A right-angled wire run through Unity points, one straight box per leg, a metal block at every bend."""
        for a, b in zip(pts, pts[1:]):
            self.ub(bm, [min(p, q) - t / 2 for p, q in zip(a, b)], [max(p, q) + t / 2 for p, q in zip(a, b)], mat)
        for p in pts[1:-1]:
            s = t * 0.75
            self.ub(bm, (p[0] - s, p[1] - s, p[2] - s), (p[0] + s, p[1] + s, p[2] + s), METAL, 0.01)

    def obj(self, name):
        return new_object(name, self.mats)

    # -- the house parts ----------------------------------------------------------------------------------------
    def frame(self, braces=True):
        """Plinth filling the footprint, four corner posts, a ring of top beams, braces on the sides and back. The
        front stays open. Returns (object, bmesh) so a size can add to it before finish()."""
        o, bm = self.obj("Frame")
        hx, hz, px, pz, P = self.hx, self.hz, self.px, self.pz, self.post
        self.ub(bm, (-hx, self.floor, -hz), (hx, self.base, hz), METAL, 0.03)
        for sx in (-1, 1):
            for sz in (-1, 1):
                x, z = sx * px, sz * pz
                self.ub(bm, (x - P / 2, self.base, z - P / 2), (x + P / 2, self.top, z + P / 2), METAL, 0.015)
                f = min(P, hx - px, hz - pz)                                                    # foot plate,
                self.ub(bm, (x - f, self.base, z - f), (x + f, self.base + 0.04, z + f), DARK)       # inside the footprint
        for s in (-1, 1):
            self.ub(bm, (-px - P / 2, self.top - TB, s * pz - TB / 2), (px + P / 2, self.top, s * pz + TB / 2), METAL, 0.012)
            self.ub(bm, (s * px - TB / 2, self.top - TB, -pz - P / 2), (s * px + TB / 2, self.top, pz + P / 2), METAL, 0.012)
        if braces:
            mid = (self.base + self.top - TB / 2) / 2
            for s in (-1, 1):
                for t in (-1, 1):
                    self.v_brace(bm, lambda u, y, s=s: (s * px, y, u), t * pz, pz, mid)      # sides: x = +-px
                self.v_brace(bm, lambda u, y: (u, y, -pz), s * px, px, mid, back=True)       # back: z = -pz
        return o, bm

    def v_brace(self, bm, at, u, half, mid, back=False):
        """The brace rule on one post of one face: from the post's middle, a brace down into the plinth and one up
        into the top beam, each at 45 degrees unless that would cross the face's middle, then steeper. 'at' maps
        (position along the face, height) to a Unity point; u is the post's position along the face."""
        sign = 1 if u > 0 else -1
        lo_run = min(mid - self.base, half * 0.75)
        hi_run = min(self.top - TB / 2 - mid, half * 0.75)
        foot = sign * (abs(u) - lo_run)
        foot_y = self.base - 0.03
        if back and IO_X[0] - 0.05 < foot < IO_X[1] + 0.05:
            foot_y = self.io_top - 0.03                                          # lands on the I/O panel instead
        self.ubeam(bm, at(u, mid), at(foot, foot_y), BRACE)
        self.ubeam(bm, at(u, mid), at(sign * (abs(u) - hi_run), self.top - TB / 2), BRACE)

    def io_panel(self, bm, inner=None):
        """The I/O panel: a block from the model out to the slot's back edge, three cells wide and one tall, a dark
        plate flush with the back face carrying the three ports."""
        inner = -self.hz + IO_DEPTH if inner is None else inner
        self.ub(bm, (IO_X[0], self.floor, inner), (IO_X[1], self.io_top, -self.hz), METAL, 0.02)
        self.ub(bm, (IO_X[0] + 0.03, self.floor + 0.04, -self.hz + 0.06), (IO_X[1] - 0.03, self.io_top - 0.04, -self.hz), DARK, 0.01)
        return inner

    def control_panel(self, bm, x0, x1, z, bottom, facing=1):
        """A dark panel standing on the plinth, PANEL_W x PANEL_H, amber trim round its top, the shared screen on its
        face (z, facing +z). Returns the point at the back of the screen, where Data arrives."""
        d = 0.14
        top = bottom + PANEL_H
        zb = z - facing * d
        self.ub(bm, (x0, bottom, min(z, zb)), (x1, top, max(z, zb)), DARK, 0.015)
        self.ub(bm, (x0 - 0.015, top - 0.03, min(z, zb) - 0.015), (x1 + 0.015, top + 0.01, max(z, zb) + 0.015), AMBER, 0.006)
        sy0 = bottom + SCREEN_BELOW
        sy1 = sy0 + SCREEN_H + 0.03
        self.ub(bm, (x0 + 0.04, sy0, z), (x1 - 0.04, sy1, z + facing * 0.012), AMBER, 0.004)         # screen frame
        self.ub(bm, (x0 + 0.055, sy0 + 0.015, z + facing * 0.012), (x1 - 0.055, sy1 - 0.015, z + facing * 0.02), DARK)
        return ((x0 + x1) / 2, (sy0 + sy1) / 2, zb)

    def cell_object(self, name, fill):
        """A part that shows charge, as its own object so the prefab can colour it."""
        o, bm = self.obj(name)
        self.preview.append((o, fill))
        return o, bm

    # -- output -------------------------------------------------------------------------------------------------
    def finish_all(self, shots):
        if self.out_fbx:
            export(self.out_blend, self.out_fbx)
            return
        self.colour_preview()
        bpy.ops.wm.save_as_mainfile(filepath=self.out_blend)
        render(os.path.splitext(self.out_blend)[0], shots)
        for obj in bpy.data.objects:
            if obj.type == "MESH":
                print("DONE", obj.name, len(obj.data.polygons), "faces", [round(v, 3) for v in obj.dimensions])

    def colour_preview(self):
        """Draft only: what the readout script will do, at the fill each part was given."""
        for o, fill in self.preview:
            if fill <= 0:
                c, e = (0.01, 0.01, 0.012), None
            elif fill >= 1:
                c = e = (0.95, 0.93, 1.0)
            else:
                c = e = (0.45 + 0.5 * fill, 0.12 + 0.8 * fill, 1.0)
            m = material(f"Preview {o.name}", c, 0.0, 0.2, emit=e)
            if e: m.node_tree.nodes["Principled BSDF"].inputs["Emission Strength"].default_value = 0.6 + 1.6 * fill
            o.data.materials[CELL] = m
