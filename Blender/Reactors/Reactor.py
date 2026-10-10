# The S5 reactor (component-models.md; mechanics in reactor.md -> Six chambers): a hexagonal vessel in a heavy
# braced frame. Six fuel chambers stand inside clear glass round a dark spine, each fed by its own item port on the
# top face; a spent rod drops out of its chamber's bottom into the base housing and never leaves the model. Coolant
# pipes loop out to the sides; the control bay at the front holds the six chamber levers and the master. Origin at
# the centre of the S5 volume, 5 x 5 x 5 m.
#
# Positions are written in UNITY coordinates (x, y, z): front +z, up +y. U() turns them into Blender's (Z up, front
# -Y): Blender (X, Y, Z) = (-x, -z, y). Shared parts and conventions: Blender/Loaders/loader_parts.py.
#
#   Draft (saves the .blend, exports nothing):
#     blender -b --python Blender/Reactors/Reactor.py -- Blender/Reactors/Reactor.blend
#   Final:
#     blender -b --python Blender/Reactors/Reactor.py -- Blender/Reactors/Reactor.blend "Assets/#User/SpaceScape/Meshes/Reactor.fbx"
import os, sys
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Loaders"))
from loader_parts import *

paths = args()
out_blend = paths[0]
out_fbx = paths[1] if len(paths) > 1 else None
metal, dark, amber = start()
GLASS, CORE = 3, 4
glass = material("ReactorGlass", (0.75, 0.88, 1.0), 0.0, 0.05)
glass.node_tree.nodes["Principled BSDF"].inputs["Alpha"].default_value = 0.12
glass.surface_render_method = "BLENDED"
core = material("ReactorCore", (0.2, 0.6, 1.0), 0.0, 0.3, emit=(0.15, 0.55, 1.0))
MATS = [metal, dark, amber, glass, core]

H = 2.5                         # half the volume, every way
FLOOR = -H
PLINTH_TOP = FLOOR + 0.25
POST = 0.22                     # the frame's corner posts: heavier than a loader's legs
POST_XZ = H - 0.20              # their centres, x and z
BRACE_T = 0.10

R = 2.16                        # the vessel's corner radius; corners on +-x, flats facing front and back. Sized so
                                # every port collar sits inside the cap's inset, which the frame's side braces still clear
INSET = 0.12                    # the cap's dark inset, this much in from R
FLAT = R * math.sqrt(3) / 2     # 1.871: the front flat, where the control bay stands
HOUSING_TOP = -0.9              # the base housing: spent rods drop into it and vanish
GLASS_TOP = 1.6
CAP_TOP = 2.1
CHAMBER_R = 0.33                # each chamber's glass tube, corner radius; a fuel rod is 0.24
ROD_R, ROD_LEN = 0.24, 1.0
# The six item ports' cells on the top face, as Unity (x, z) cell centres. A regular ring cannot land on the 0.5 m
# grid, so this is the closest: every chamber 1.20-1.27 m out, 1.12 m or more apart, a slight pinwheel twist.
PORTS = [(1.25, 0.25), (0.25, 1.25), (-0.75, 0.75), (-1.25, -0.25), (-0.25, -1.25), (0.75, -0.75)]
COLLAR = 0.72                   # solid block round each port (item port: 0.56 m square, sinks 0.29 m)
# The I/O panel: three cells wide and one tall on the back face's bottom row, In, Data and Out from the left seen
# from behind (every component has a data port: component-models.md). Unity x of each cell's centre.
IO_PORTS = [-0.75, -0.25, 0.25]
IO_X = (-1.0, 0.5)
IO_TOP = FLOOR + 0.5
# The control bay's deck, where the levers stand: its front edge and its back edge (Unity z, y). The editor tool that
# builds the draft prefab (ReactorDraftBuilder.cs) places the levers on it with the same numbers.
DECK_FRONT = (2.38, -1.6)       # just inside the plinth's front edge
DECK_BACK = (FLAT + 0.15, -1.38)
TOWER_TOP = -0.3
LEVERS_X = [0.78 - 0.24 * i for i in range(6)] + [-0.78]       # six chambers, then the master (viewer's right)


def U(p): return Vector((-p[0], -p[2], p[1]))


def ub(bm, lo, hi, mat=METAL, chamfer=0.0):
    a, b = U(lo), U(hi)
    box(bm, [min(a[i], b[i]) for i in range(3)], [max(a[i], b[i]) for i in range(3)], mat, chamfer)


def ubeam(bm, a, b, thick, mat=METAL):
    beam(bm, U(a), U(b), thick, mat)


def hexprism(bm, x, z, bottom, top, radius, mat, chamfer=0.0, segments=6):
    """A vertical prism (hexagonal by default) at Unity (x, z), corners on +-x (create_cone puts one on +Y, hence the turn)."""
    r = bmesh.ops.create_cone(bm, cap_ends=True, segments=segments, radius1=radius, radius2=radius, depth=top - bottom,
                              matrix=Matrix.Translation(U((x, (top + bottom) / 2, z))) @ Matrix.Rotation(math.radians(30), 4, "Z"))
    verts = r["verts"]
    faces = list({f for v in verts for f in v.link_faces})
    if chamfer > 0:
        edges = list({e for v in verts for e in v.link_edges})
        geom = bmesh.ops.bevel(bm, geom=verts + edges, offset=chamfer, segments=1, affect="EDGES", profile=0.5)
        faces = list(set(faces) | set(geom["faces"]))
    for f in faces:
        if f.is_valid: f.material_index = mat


def hex_corners(radius, y):
    return [(radius * math.cos(math.radians(60 * k)), y, radius * math.sin(math.radians(60 * k))) for k in range(6)]


def pipe(bm, pts, radius=0.09):
    """A round pipe along Unity points, a dark elbow block at every bend."""
    for a, b in zip(pts, pts[1:]):
        a, b = U(a), U(b)
        d = b - a
        rot = d.to_track_quat("Z", "Y").to_matrix().to_4x4()
        r = bmesh.ops.create_cone(bm, cap_ends=True, segments=12, radius1=radius, radius2=radius, depth=d.length,
                                  matrix=Matrix.Translation((a + b) / 2) @ rot)
        for f in {f for v in r["verts"] for f in v.link_faces}: f.material_index = DARK
    for p in pts[1:-1]:
        s = radius * 1.35
        ub(bm, (p[0] - s, p[1] - s, p[2] - s), (p[0] + s, p[1] + s, p[2] + s), METAL, 0.015)


# Frame: plinth, four corner posts, a top ring of beams, braces on the sides and back (the front's lower half is
# left open for the control bay).
frame, bm = new_object("Frame", MATS)
ub(bm, (-H, FLOOR, -H), (H, PLINTH_TOP, H), METAL, 0.03)            # the whole footprint: the feet stand inside it
for sx in (-1, 1):
    for sz in (-1, 1):
        x, z = sx * POST_XZ, sz * POST_XZ
        ub(bm, (x - POST / 2, PLINTH_TOP, z - POST / 2), (x + POST / 2, H, z + POST / 2), METAL, 0.02)
        ub(bm, (x - 0.2, PLINTH_TOP, z - 0.2), (x + 0.2, PLINTH_TOP + 0.05, z + 0.2), DARK)     # foot plate
TB = 0.18
for s in (-1, 1):
    ub(bm, (-POST_XZ, H - TB, s * POST_XZ - TB / 2), (POST_XZ, H, s * POST_XZ + TB / 2), METAL, 0.015)
    ub(bm, (s * POST_XZ - TB / 2, H - TB, -POST_XZ), (s * POST_XZ + TB / 2, H, POST_XZ), METAL, 0.015)
MID = 0.1                       # where the braces meet the posts
LOW = PLINTH_TOP - 0.05         # brace feet sink into the plinth
HIGH = H - TB / 2               # brace heads run into the top beam's middle
for s in (-1, 1):
    for t in (-1, 1):
        # sides (planes x = +-POST_XZ): lower V to the plinth, upper V from the top beam
        ubeam(bm, (s * POST_XZ, MID, t * POST_XZ), (s * POST_XZ, LOW, t * 0.7), BRACE_T)
        ubeam(bm, (s * POST_XZ, MID, t * POST_XZ), (s * POST_XZ, HIGH, t * 0.7), BRACE_T)
    # back (plane z = -POST_XZ) the same, the lower feet wider to clear the I/O panel; the front gets only the upper V
    ubeam(bm, (s * POST_XZ, MID, -POST_XZ), (s * 1.3, LOW, -POST_XZ), BRACE_T)
    ubeam(bm, (s * POST_XZ, MID, -POST_XZ), (s * 0.7, HIGH, -POST_XZ), BRACE_T)
    ubeam(bm, (s * POST_XZ, MID, POST_XZ), (s * 0.7, HIGH, POST_XZ), BRACE_T)
finish(frame, bm)

# Vessel: base housing, the glass section with corner mullions and bands, the top cap, and a collar round each port.
vessel, bm = new_object("Vessel", MATS)
hexprism(bm, 0, 0, PLINTH_TOP, HOUSING_TOP, R, METAL, 0.04)
hexprism(bm, 0, 0, PLINTH_TOP + 0.3, PLINTH_TOP + 0.45, R + 0.04, DARK, 0.01)          # a dark band low down
hexprism(bm, 0, 0, HOUSING_TOP, GLASS_TOP, R - 0.03, GLASS)
for c in hex_corners(R - 0.03, 0):
    ubeam(bm, (c[0], HOUSING_TOP, c[2]), (c[0], GLASS_TOP, c[2]), 0.09)
for y in (HOUSING_TOP, GLASS_TOP):
    hexprism(bm, 0, 0, y - 0.07, y + 0.07, R + 0.03, METAL, 0.015)
hexprism(bm, 0, 0, GLASS_TOP, CAP_TOP, R, METAL, 0.04)
hexprism(bm, 0, 0, CAP_TOP, CAP_TOP + 0.08, R - INSET, DARK, 0.02)
for (x, z) in PORTS:
    ub(bm, (x - COLLAR / 2, CAP_TOP, z - COLLAR / 2), (x + COLLAR / 2, H, z + COLLAR / 2), METAL, 0.025)
    ub(bm, (x - COLLAR / 2 - 0.01, H - 0.05, z - COLLAR / 2 - 0.01), (x + COLLAR / 2 + 0.01, H - 0.02, z + COLLAR / 2 + 0.01), AMBER)
# The I/O panel: a block from the base housing out to the back face, a dark plate flush with the face for the ports.
ub(bm, (IO_X[0], FLOOR, -1.6), (IO_X[1], IO_TOP, -H), METAL, 0.02)
ub(bm, (IO_X[0] + 0.03, FLOOR + 0.04, -H + 0.06), (IO_X[1] - 0.03, IO_TOP - 0.04, -H), DARK, 0.01)
# Coolant: two loops out of each side, from the base housing up past the glass and back into the cap.
for sx in (-1, 1):
    for z in (-0.55, 0.55):
        pipe(bm, [(sx * 1.6, -1.4, z), (sx * 2.05, -1.4, z), (sx * 2.05, 1.85, z), (sx * 1.6, 1.85, z)])
finish(vessel, bm)

# Chambers: a glass tube under each port with metal collars top and bottom, round a dark spine with glowing bands.
chambers, bm = new_object("Chambers", MATS)
hexprism(bm, 0, 0, HOUSING_TOP, GLASS_TOP, 0.42, DARK, 0.02)
for y in (-0.3, 0.35, 1.0):
    hexprism(bm, 0, 0, y - 0.03, y + 0.03, 0.45, CORE)
for (x, z) in PORTS:
    hexprism(bm, x, z, HOUSING_TOP, GLASS_TOP, CHAMBER_R, GLASS)
    for y in (HOUSING_TOP + 0.04, GLASS_TOP - 0.04):
        hexprism(bm, x, z, y - 0.05, y + 0.05, CHAMBER_R + 0.03, METAL, 0.01)
finish(chambers, bm)

# Control bay: a console on the front flat, one side profile carried across: a front face, a deck sloping up and back
# where the levers stand, and a tower rising in front of the glass carrying the screen.
def profile_prism(bm, pts, x0, x1, mats):
    """A prism across Unity x from x0 to x1, its side profile pts given as Unity (z, y); mats[i] is the material of
    the face along edge i (pts[i] to pts[i + 1])."""
    a = [bm.verts.new(U((x0, y, z))) for (z, y) in pts]
    b = [bm.verts.new(U((x1, y, z))) for (z, y) in pts]
    for f in (bm.faces.new(a), bm.faces.new(list(reversed(b)))):
        f.material_index = METAL
    n = len(pts)
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new((a[i], a[j], b[j], b[i])).material_index = mats[i]


bay, bm = new_object("Control Bay", MATS)
BW = 0.95
back = FLAT - 0.1
profile = [(back, PLINTH_TOP), (DECK_FRONT[0], PLINTH_TOP), DECK_FRONT, DECK_BACK, (DECK_BACK[0], TOWER_TOP), (back, TOWER_TOP)]
profile_prism(bm, profile, -BW, BW, [METAL, METAL, DARK, METAL, METAL, METAL])
fz, fy = DECK_FRONT
ub(bm, (-BW - 0.01, fy - 0.03, fz - 0.03), (BW + 0.01, fy + 0.02, fz + 0.02), AMBER, 0.008)       # amber on the deck's edge
ub(bm, (-BW + 0.1, PLINTH_TOP + 0.12, fz), (BW - 0.1, fy - 0.15, fz + 0.02), DARK, 0.01)          # front inset
tz = DECK_BACK[0]
ub(bm, (-0.78, -1.05, tz), (0.78, TOWER_TOP - 0.1, tz + 0.02), AMBER, 0.008)                       # screen frame, clear of the handles
ub(bm, (-0.74, -1.01, tz + 0.02), (0.74, TOWER_TOP - 0.14, tz + 0.035), DARK)                      # the screen: dark, for the readout's text
finish(bay, bm)

# Draft only, not exported: rods in the chambers and the levers, which are runtime parts (the fuel rods' state and
# the RodLever prefab). They show the look: rods burning at different fuel, one running hot, one spent and dropping.
DRAFT = []
rods, bm = new_object("Draft Rods", MATS)
looks = [(0.15, 0.55, 1.0), (0.4, 0.85, 1.0), (1.0, 0.45, 0.1), (0.25, 0.65, 1.0), (0.45, 0.25, 0.9), (0.1, 0.4, 1.0)]
rod_mats = []
for i, colour in enumerate(looks):
    rm = material(f"DraftRod{i}", colour, 0.0, 0.3, emit=colour)
    rods.data.materials.append(rm)
    rod_mats.append(len(MATS) + i)
spare = material("DraftSpare", (0.1, 0.25, 0.4), 0.0, 0.4, emit=(0.05, 0.15, 0.3))
rods.data.materials.append(spare)
for i, (x, z) in enumerate(PORTS):
    bottom = HOUSING_TOP + 0.12 if i != 4 else HOUSING_TOP - 0.55                    # chamber 4's rod is dropping out
    hexprism(bm, x, z, bottom, bottom + ROD_LEN, ROD_R, rod_mats[i])
    hexprism(bm, x, z, GLASS_TOP - 0.12 - ROD_LEN, GLASS_TOP - 0.12, ROD_R, len(MATS) + len(looks))  # the spare
finish(rods, bm)
levers, bm = new_object("Draft Levers", MATS)
levers.data.materials.append(material("DraftMaster", (0.9, 0.12, 0.08), 0.0, 0.4))
tilt = math.atan2(DECK_BACK[1] - DECK_FRONT[1], DECK_FRONT[0] - DECK_BACK[0])
c, sn = math.cos(tilt), math.sin(tilt)
mz, my = (DECK_FRONT[0] + DECK_BACK[0]) / 2, (DECK_FRONT[1] + DECK_BACK[1]) / 2
def on_deck(x, ly, lz): return (x, my + ly * c - lz * sn, mz + ly * sn + lz * c)   # a point in a lever's own frame
for i, x in enumerate(LEVERS_X):
    mat = AMBER if i < 6 else len(MATS)
    ubeam(bm, on_deck(x, 0.0, -0.16), on_deck(x, 0.0, 0.16), 0.06, DARK)       # base, along the swing
    ubeam(bm, on_deck(x, 0.03, 0.0), on_deck(x, 0.45, 0.0), 0.035, METAL)     # arm
    p = on_deck(x, 0.45, 0.0)
    ub(bm, (x - 0.04, p[1] - 0.04, p[2] - 0.04), (x + 0.04, p[1] + 0.04, p[2] + 0.04), mat, 0.01)
finish(levers, bm)
DRAFT = [rods, levers]

if out_fbx:
    for o in DRAFT: bpy.data.objects.remove(o)
    export(out_blend, out_fbx)
else:
    bpy.ops.wm.save_as_mainfile(filepath=out_blend)
    for obj in bpy.data.objects:
        print("DONE", obj.name, len(obj.data.polygons), "faces", [round(v, 3) for v in obj.dimensions])
