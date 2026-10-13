using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SomniumSpace.Worlds.SpaceScape.Ship
{
    /// The ship's item conduits (loading.md → Item conduit rules): square glass tubes that carry items from a
    /// component's ItemOut port to others' Item (in) ports. Not part of the power grid.
    ///
    /// An item conduit is a conduit, one kind of the ship's conduit cells (ConduitCells, which holds them, installs them,
    /// prunes them and shares them): each installed item cell has its segments, two or more with a Junction addon, each
    /// on a face or parked, and its addon; its hologram is the conduit hologram (ItemConduitHolograms), with no arrows.
    /// Two cells join where both have a segment on the face between them; a cell joins a port where it has a segment
    /// on the face toward it. This draws them as tubes and routes items along them.
    ///
    /// Items take turns at a cell with more than two faces joined (Choose), side after side, skipping a side where
    /// nothing wants the item. Split only: one loader feeds a tree.
    ///
    /// The cells are the state; the tubes and routes are worked out from them, and from where the components stand,
    /// on every client.
    public sealed class ItemConduits : MonoBehaviour
    {
        /// The way an item goes from a sender's ItemOut port to a receiver's Item port: straight legs between solid
        /// corners and junctions. An item crosses a leg end to end, emerging whole from its start and swallowed whole
        /// at its end.
        public sealed class Route
        {
            internal readonly List<Vector3> Starts = new List<Vector3>();
            internal readonly List<Vector3> Ends = new List<Vector3>();
            internal readonly List<Vector3> Directions = new List<Vector3>();

            /// The way it leaves each junction it passes, in order from the sender.
            internal readonly List<(Vector3Int cell, GridDirection face)> Decisions = new List<(Vector3Int, GridDirection)>();

            /// The component the items arrive at.
            public ComponentEdgeModule Receiver { get; internal set; }

            /// The grid cell of the receiver's port the items arrive through, inside the receiver.
            public Vector3Int ReceiverCell { get; internal set; }

            /// How far an item this long travels from leaving the sender to being inside the receiver.
            public float Length(float itemLength)
            {
                float total = 0f;
                for (int i = 0; i < Starts.Count; i++) total += Vector3.Distance(Starts[i], Ends[i]) + itemLength;
                return total;
            }

            /// Where an item this long is once it has travelled this far, and the two planes it is cut to: the
            /// start and end of the leg it is in.
            public void Pose(float travelled, float itemLength, out Vector3 centre, out Quaternion turn, out Plane keepA, out Plane keepB)
            {
                int leg = 0;
                float along = travelled;
                for (; leg < Starts.Count - 1; leg++)
                {
                    float span = Vector3.Distance(Starts[leg], Ends[leg]) + itemLength;
                    if (along < span) break;
                    along -= span;
                }
                var dir = Directions[leg];
                centre = Starts[leg] + dir * (along - itemLength * 0.5f);
                turn = Quaternion.FromToRotation(Vector3.up, dir);
                keepA = new Plane(dir, Starts[leg]);
                keepB = new Plane(-dir, Ends[leg]);
            }
        }

        /// One cell as the conduit hologram sees it: installed, or one a hand could install, with a segment already
        /// on the face toward what points into it.
        private sealed class ItemConduit : IConduit
        {
            private readonly ItemConduits _owner;

            public ItemConduit(ItemConduits owner, Vector3Int cell)
            {
                _owner = owner;
                Coordinate = cell;
            }

            public Vector3Int Coordinate { get; }

            public Vector3 Centre => GridCell.ToWorld(Coordinate);

            public bool Occupies(Vector3Int cell) => cell == Coordinate;

            public int Across => 1;

            public int Segments => _owner._store.SegmentsAt(Coordinate);

            public bool HasArrows => false;

            public void GetSegments(List<GridDirection> faces, List<bool> outward)
            {
                int mask;
                if (_owner.TryCell(Coordinate, out var cell)) mask = cell.Used;
                else mask = _owner._store.EntryFor(Coordinate, out _, out var face, out _) ? Bit(face) : 0;
                foreach (var face in AllFaces)
                    if ((mask & Bit(face)) != 0) { faces.Add(face); outward.Add(true); }
            }

            public ConduitAddon Addon => _owner.AddonAt(Coordinate);

            public int AddonState => _owner.TryCell(Coordinate, out var cell) ? cell.AddonState : 0;

            public GridDirection AddonFacing => GridDirection.None;

            public int AddonTurns => 0;

            public bool AddonParked => false;

            public bool RemoveAddon() => _owner._store.EditByHand(ConduitCells.Edit.Addon, Coordinate, 0, 0);

            public bool PairsOutward(GridDirection face) => true;
        }

        private static readonly GridDirection[] AllFaces =
        {
            GridDirection.XPlus, GridDirection.XMinus,
            GridDirection.YPlus, GridDirection.YMinus,
            GridDirection.ZPlus, GridDirection.ZMinus,
        };

        [Header("Tubes")]
        [Tooltip("A straight tube one cell long, open at its local +Z and -Z faces.")]
        [SerializeField] private Mesh _straightMesh;
        [SerializeField] private Material[] _straightMaterials = new Material[0];
        [Tooltip("A solid corner one cell across, open at its local +Z and +X faces.")]
        [SerializeField] private Mesh _cornerMesh;
        [SerializeField] private Material[] _cornerMaterials = new Material[0];
        [Tooltip("A junction's solid block, one cell across: a cell joined on more than two faces, or holding a junction. Without one it is drawn as a corner.")]
        [SerializeField] private Mesh _junctionMesh;
        [SerializeField] private Material[] _junctionMaterials = new Material[0];
        [Tooltip("A junction's opening on its local +Z face, placed on each face a tube leaves by.")]
        [SerializeField] private Mesh _junctionMouthMesh;
        [SerializeField] private Material[] _junctionMouthMaterials = new Material[0];

        [Header("Open")]
        [Tooltip("While its hologram is open, a cell's tube smokes away with this (the access panels' SpaceScape/Dissolve) so the hologram inside can be seen, and a power conduit's cable shows its segments instead.")]
        [SerializeField] private Material _dissolveMaterial;
        [Tooltip("Seconds the tube takes to smoke away, or back.")]
        [SerializeField] private float _revealSeconds = 0.4f;
        [Tooltip("The power conduit's cable piece: a cylinder along its local Y, Length At Scale One long.")]
        [SerializeField] private Mesh _cableMesh;
        [SerializeField] private Material _cableMaterial;
        [Tooltip("The cable's thickness, in metres.")]
        [SerializeField] private float _cableThickness = 0.05f;
        [Tooltip("The cable mesh's length at scale 1, in metres (2 for Unity's cylinder).")]
        [SerializeField] private float _cableLengthAtScaleOne = 2f;

        [Header("Items")]
        [Tooltip("How fast an item moves through a tube, in metres a second (one cell is half a metre).")]
        [SerializeField] private float _itemSpeed = 1f;

        private ConduitCells _store;
        private readonly Dictionary<Vector3Int, ItemConduit> _views = new Dictionary<Vector3Int, ItemConduit>();
        private readonly List<GameObject> _tubes = new List<GameObject>();
        private readonly Dictionary<Vector3Int, List<Renderer>> _pieces = new Dictionary<Vector3Int, List<Renderer>>();
        private readonly Dictionary<Vector3Int, float> _gone = new Dictionary<Vector3Int, float>();     // cells smoking away or back
        private readonly List<Vector3Int> _goneCells = new List<Vector3Int>();
        private readonly Dictionary<Vector3Int, GameObject> _cables = new Dictionary<Vector3Int, GameObject>();
        private readonly Dictionary<Material, Material> _smoke = new Dictionary<Material, Material>();
        private Vector3Int? _revealed;
        private static readonly int DissolveId = Shader.PropertyToID("_Dissolve");
        private readonly Dictionary<Vector3Int, int> _turns = new Dictionary<Vector3Int, int>();
        private readonly Dictionary<ComponentEdgeModule, List<Route>> _routes = new Dictionary<ComponentEdgeModule, List<Route>>();
        private Transform _tubeRoot;

        /// The ship's item conduits, or null.
        public static ItemConduits Current { get; private set; }

        /// How fast an item moves through a tube, in metres a second.
        public float ItemSpeed => _itemSpeed;

        /// Bumped whenever the cells, or the ports they join, change.
        public int Revision { get; private set; }

        /// Raised whenever the cells change, from a hand here or from the network.
        public event Action Changed;

        private void Awake()
        {
            Current = this;
            _store = GetComponent<ConduitCells>();
            if (_store != null) _store.Changed += Rebuild;
        }

        private void OnDestroy()
        {
            if (Current == this) Current = null;
            if (_store != null) _store.Changed -= Rebuild;
            if (ItemPort.Wired == IsWired) ItemPort.Wired = null;
        }

        private void Start()
        {
            ItemPort.Wired = IsWired;
            _tubeRoot = new GameObject("Item Conduit Tubes").transform;
            _tubeRoot.SetParent(transform, false);
            Rebuild();
        }

        private void Update() => Animate();

        // ---- Questions

        /// An installed item cell, if this is one.
        private bool TryCell(Vector3Int cell, out ConduitCells.Cell c)
        {
            c = _store != null ? _store.Get(cell) : null;
            if (c != null && c.Kind == ConduitCells.Kind.Item) return true;
            c = null;
            return false;
        }

        /// True for a cell an item conduit is installed in.
        public bool Installed(Vector3Int cell) => TryCell(cell, out _);

        /// True for a cell a hand could install an item conduit in.
        public bool Installable(Vector3Int cell) => _store != null && _store.Installable(cell, ConduitCells.Kind.Item);

        /// The item conduit installed in a cell, for its hologram; null for any other cell. The same object each time, so
        /// the hologram knows it. An empty cell's parked conduit is ConduitCells.PendingAt.
        public IConduit ConduitAt(Vector3Int cell)
        {
            if (!Installed(cell)) return null;
            if (!_views.TryGetValue(cell, out var view)) _views[cell] = view = new ItemConduit(this, cell);
            return view;
        }

        /// A cell's addon, as the prefab, or null.
        private ConduitAddon AddonAt(Vector3Int cell) => TryCell(cell, out var c) ? _store.AddonPrefab(c.Addon) : null;

        /// The id of an addon an item conduit takes (a junction), or 0.
        public int AddonId(ConduitAddon addon) => addon is JunctionAddon && _store != null ? _store.AddonId(addon) : 0;

        /// True while an item conduit is installed in front of this port with a segment on the face toward it, so its
        /// intake takes nothing by hand.
        private bool IsWired(ItemPort port)
        {
            var t = port.transform;
            var front = GridCell.ToCell(t.position + t.forward * GridCell.Half);
            var into = Nearest(-t.forward);
            return TryCell(front, out var cell) && (cell.Used & Bit(into)) != 0;
        }

        private bool PortAt(Vector3Int front, GridDirection into, out ConduitCells.Port port)
        {
            port = default;
            return _store != null && _store.PortAt(front, into, out port) && ComponentEdgeModule.IsItem(port.Flow);
        }

        private static int Bit(GridDirection face) => ConduitCells.Bit(face);

        private static int Count(int mask) => ConduitCells.Count(mask);

        /// A grid cell in 30 bits, ten to an axis.
        public static int PackCell(Vector3Int c) => ConduitCells.PackCell(c);

        public static Vector3Int UnpackCell(int p) => ConduitCells.UnpackCell(p);

        // ---- Building

        private void Rebuild()
        {
            BuildTubes();
            _routes.Clear();
            Revision++;
            Changed?.Invoke();
        }

        /// A tube per installed cell, from the faces its segments are on, as a power conduit draws a stub per segment:
        /// half a tube for one segment on a face, a straight or a corner for two, a junction block with a mouth on each
        /// face for three or more (or a junction addon), and nothing for a fully parked cell.
        private void BuildTubes()
        {
            foreach (var tube in _tubes) if (tube != null) Destroy(tube);
            _tubes.Clear();
            _pieces.Clear();
            foreach (var cable in _cables.Values) if (cable != null) Destroy(cable);
            _cables.Clear();
            if (_tubeRoot == null) return;
            var faces = new List<GridDirection>();
            foreach (var pair in _store != null ? _store.All : Enumerable.Empty<KeyValuePair<Vector3Int, ConduitCells.Cell>>())
            {
                if (pair.Value.Kind != ConduitCells.Kind.Item) continue;
                faces.Clear();
                foreach (var face in AllFaces)
                    if ((pair.Value.Used & Bit(face)) != 0) faces.Add(face);
                if (faces.Count == 0 && pair.Value.Addon == 0) continue;
                var at = GridCell.ToWorld(pair.Key);
                if (faces.Count > 2 || pair.Value.Addon != 0)
                {
                    var block = Piece("Item Junction", at, Quaternion.identity,
                        _junctionMesh != null ? _junctionMesh : _cornerMesh, _junctionMesh != null ? _junctionMaterials : _cornerMaterials);
                    if (_junctionMouthMesh == null) continue;
                    foreach (var face in faces)
                    {
                        var dir = face.Vector();
                        var mouth = Piece("Item Junction Mouth", at, Quaternion.LookRotation(dir, Mathf.Abs(dir.y) > 0.5f ? Vector3.forward : Vector3.up),
                            _junctionMouthMesh, _junctionMouthMaterials);
                        mouth.transform.SetParent(block.transform, true);
                    }
                    continue;
                }
                Vector3 a = faces[0].Vector();
                if (faces.Count == 1)
                {
                    // One segment on a face: half a tube, from the middle out to that face.
                    var half = Piece("Item Tube Half", at + a * (GridCell.Size * 0.25f),
                        Quaternion.LookRotation(a, Mathf.Abs(a.y) > 0.5f ? Vector3.forward : Vector3.up), _straightMesh, _straightMaterials);
                    half.transform.localScale = new Vector3(1f, 1f, 0.5f);
                    continue;
                }
                Vector3 b = faces[1].Vector();
                bool straight = Vector3.Dot(a, b) < -0.5f;
                var turn = straight
                    ? Quaternion.LookRotation(b, Mathf.Abs(b.y) > 0.5f ? Vector3.forward : Vector3.up)
                    : Quaternion.LookRotation(a, Vector3.Cross(a, b));
                Piece(straight ? "Item Tube" : "Item Tube Corner", at, turn,
                    straight ? _straightMesh : _cornerMesh, straight ? _straightMaterials : _cornerMaterials);
            }
            foreach (var cell in _gone.Keys) MakeCable(cell);
        }

        private GameObject Piece(string name, Vector3 at, Quaternion turn, Mesh mesh, Material[] materials)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_tubeRoot, false);
            go.transform.SetPositionAndRotation(at, turn);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = materials;
            _tubes.Add(go);
            var cell = GridCell.ToCell(at);
            if (!_pieces.TryGetValue(cell, out var list)) _pieces[cell] = list = new List<Renderer>();
            list.Add(renderer);
            return go;
        }

        // ---- The open cell: its tube smokes away and a power conduit's cable shows its segments, while its hologram is open.

        /// The cell this client's hologram is open on, or null. Set by ItemConduitHolograms.
        public void Reveal(Vector3Int? cell)
        {
            if (cell == _revealed) return;
            _revealed = cell;
            if (cell.HasValue && !_gone.ContainsKey(cell.Value))
            {
                _gone[cell.Value] = 0f;
                MakeCable(cell.Value);
            }
        }

        /// Eases each open (or just closed) cell's tube toward gone or back, wearing the dissolve material between the
        /// two, with the cable shown while any of it is gone.
        private void Animate()
        {
            if (_gone.Count == 0) return;
            float step = Time.deltaTime / Mathf.Max(0.01f, _revealSeconds);
            _goneCells.Clear();
            _goneCells.AddRange(_gone.Keys);
            foreach (var cell in _goneCells)
            {
                float gone = Mathf.MoveTowards(_gone[cell], _revealed == cell ? 1f : 0f, step);
                _gone[cell] = gone;
                bool done = gone <= 0f && _revealed != cell;
                if (_pieces.TryGetValue(cell, out var pieces))
                    foreach (var piece in pieces)
                    {
                        if (piece == null) continue;
                        piece.enabled = gone < 1f;
                        var own = piece.GetComponent<MeshFilter>().sharedMesh == _straightMesh ? _straightMaterials
                            : piece.GetComponent<MeshFilter>().sharedMesh == _cornerMesh ? _cornerMaterials
                            : piece.GetComponent<MeshFilter>().sharedMesh == _junctionMesh ? _junctionMaterials : _junctionMouthMaterials;
                        if (gone <= 0f || _dissolveMaterial == null) { piece.sharedMaterials = own; continue; }
                        var worn = new Material[own.Length];
                        for (int i = 0; i < own.Length; i++) worn[i] = Smoke(own[i]);
                        piece.sharedMaterials = worn;
                        foreach (var material in worn) material.SetFloat(DissolveId, gone);
                    }
                if (_cables.TryGetValue(cell, out var cable) && cable != null) cable.SetActive(gone > 0f);
                if (!done) continue;
                _gone.Remove(cell);
                if (_cables.TryGetValue(cell, out var old) && old != null) Destroy(old);
                _cables.Remove(cell);
            }
        }

        /// The dissolve material standing in for one of the tube's, carrying its colour.
        private Material Smoke(Material own)
        {
            if (own == null) return _dissolveMaterial;
            if (_smoke.TryGetValue(own, out var made)) return made;
            made = new Material(_dissolveMaterial) { name = $"{_dissolveMaterial.name} ({own.name})" };
            if (own.HasProperty("_BaseMap") && made.HasProperty("_BaseMap")) made.SetTexture("_BaseMap", own.GetTexture("_BaseMap"));
            if (own.HasProperty("_BaseColor")) made.SetColor("_BaseColor", own.GetColor("_BaseColor"));
            if (own.HasProperty("_Metallic")) made.SetFloat("_Metallic", own.GetFloat("_Metallic"));
            if (own.HasProperty("_Smoothness")) made.SetFloat("_Smoothness", own.GetFloat("_Smoothness"));
            return _smoke[own] = made;
        }

        /// A power conduit's cable in a cell: a stub from the middle out to each face its segments are on.
        private void MakeCable(Vector3Int cell)
        {
            if (_cables.TryGetValue(cell, out var old) && old != null) Destroy(old);
            _cables.Remove(cell);
            if (_cableMesh == null || _tubeRoot == null || !TryCell(cell, out var c)) return;
            var root = new GameObject("Item Conduit Cable");
            root.transform.SetParent(_tubeRoot, false);
            root.transform.position = GridCell.ToWorld(cell);
            foreach (var face in AllFaces)
            {
                if ((c.Used & Bit(face)) == 0) continue;
                var dir = face.Vector();
                var stub = new GameObject("Stub");
                stub.transform.SetParent(root.transform, false);
                stub.transform.localPosition = dir * (GridCell.Size * 0.25f);
                stub.transform.localRotation = Quaternion.FromToRotation(Vector3.up, dir);
                stub.transform.localScale = new Vector3(_cableThickness, GridCell.Size * 0.5f / Mathf.Max(0.01f, _cableLengthAtScaleOne), _cableThickness);
                stub.AddComponent<MeshFilter>().sharedMesh = _cableMesh;
                stub.AddComponent<MeshRenderer>().sharedMaterial = _cableMaterial;
            }
            root.SetActive(_gone.TryGetValue(cell, out var gone) && gone > 0f);
            _cables[cell] = root;
        }

        // ---- Routes

        /// Every way items leave this component by its ItemOut port and reach an Item port, one per port the tree
        /// reaches. Empty when no item conduit leaves it.
        public IReadOnlyList<Route> RoutesFrom(ComponentEdgeModule sender)
        {
            if (sender == null) return Array.Empty<Route>();
            if (_routes.TryGetValue(sender, out var cached)) return cached;
            var routes = new List<Route>();
            foreach (var port in _store != null ? _store.Ports : (IReadOnlyList<ConduitCells.Port>)Array.Empty<ConduitCells.Port>())
            {
                if (port.Edges != sender || port.Flow != FlowDirection.ItemOut) continue;
                Walk(port.Front, port.Into, new List<(Vector3Int, GridDirection, GridDirection)>(),
                    new List<(Vector3Int, GridDirection)>(), new HashSet<Vector3Int>(), routes);
                if (routes.Count > 0) break;
            }
            _routes[sender] = routes;
            return routes;
        }

        /// The way from this component to the port in this cell, or null.
        public Route RouteTo(ComponentEdgeModule sender, Vector3Int receiverCell)
        {
            foreach (var route in RoutesFrom(sender))
                if (route.ReceiverCell == receiverCell) return route;
            return null;
        }

        /// The way the next item goes: at each junction, the side after the one it last sent down, skipping a side
        /// where no route wants the item. Null when none wants it. Moves each junction it passes on a turn, so
        /// only the master, about to send, asks.
        public Route Choose(ComponentEdgeModule sender, Func<Route, bool> wants)
        {
            var pool = new List<Route>(RoutesFrom(sender));
            for (int depth = 0; ; depth++)
            {
                var deeper = pool.Where(r => r.Decisions.Count > depth).ToList();
                if (deeper.Count == 0) return pool.FirstOrDefault(wants);
                var cell = deeper[0].Decisions[depth].cell;
                var sides = deeper.Select(r => r.Decisions[depth].face).Distinct().OrderBy(f => (int)f).ToList();
                int turn = _turns.TryGetValue(cell, out var t) ? t : 0;
                List<Route> taken = null;
                for (int k = 0; k < sides.Count && taken == null; k++)
                {
                    int side = (turn + k) % sides.Count;
                    var down = deeper.Where(r => r.Decisions[depth].face == sides[side]).ToList();
                    if (!down.Any(wants)) continue;
                    taken = down;
                    _turns[cell] = (side + 1) % sides.Count;
                }
                if (taken == null) return null;
                pool = taken;
            }
        }

        /// Follows the tubes from a cell entered by a face: on through each cell joined to the next, down every side
        /// where more than two are joined, and ends a route at each Item port it reaches.
        private void Walk(Vector3Int at, GridDirection inBy, List<(Vector3Int cell, GridDirection inBy, GridDirection outBy)> path,
            List<(Vector3Int, GridDirection)> decisions, HashSet<Vector3Int> seen, List<Route> routes)
        {
            if (!TryCell(at, out var cell) || (cell.Used & Bit(inBy)) == 0 || !seen.Add(at)) return;
            var outs = AllFaces.Where(f => f != inBy && (cell.Used & Bit(f)) != 0).ToList();
            bool junction = outs.Count > 1;
            foreach (var outBy in outs)
            {
                var way = new List<(Vector3Int, GridDirection, GridDirection)>(path) { (at, inBy, outBy) };
                var chosen = junction ? new List<(Vector3Int, GridDirection)>(decisions) { (at, outBy) } : decisions;
                if (PortAt(at, outBy, out var to))
                {
                    if (to.Flow == FlowDirection.Item) routes.Add(Build(way, chosen, to));
                    continue;
                }
                Walk(at + outBy.Offset(), GridNode.Opposite(outBy), way, chosen, seen, routes);
            }
        }

        /// A route's legs: straight between solid corners and junctions, each ending where the item goes in and the
        /// next starting where it comes out.
        private Route Build(List<(Vector3Int cell, GridDirection inBy, GridDirection outBy)> cells, List<(Vector3Int, GridDirection)> decisions, ConduitCells.Port to)
        {
            var route = new Route { Receiver = to.Edges, ReceiverCell = to.Front + to.Into.Offset() };
            route.Decisions.AddRange(decisions);
            Vector3 legStart = Face(cells[0].cell, cells[0].inBy);
            Vector3 legDir = GridNode.Opposite(cells[0].inBy).Vector();
            foreach (var (cell, inBy, outBy) in cells)
            {
                if (outBy == GridNode.Opposite(inBy) && !Solid(cell)) continue;
                route.Starts.Add(legStart);
                route.Ends.Add(Face(cell, inBy));
                route.Directions.Add(legDir);
                legStart = Face(cell, outBy);
                legDir = outBy.Vector();
            }
            var end = cells[cells.Count - 1];
            route.Starts.Add(legStart);
            route.Ends.Add(Face(end.cell, end.outBy));
            route.Directions.Add(legDir);
            return route;
        }

        /// True for a cell drawn as a junction block, which an item passes through hidden even going straight.
        private bool Solid(Vector3Int cell) => TryCell(cell, out var c) && (Count(c.Used) > 2 || c.Addon != 0);

        private static Vector3 Face(Vector3Int cell, GridDirection face) => GridCell.ToWorld(cell) + face.Vector() * GridCell.Half;

        /// The face nearest a direction.
        public static GridDirection Nearest(Vector3 direction)
        {
            var best = GridDirection.None;
            float most = float.MinValue;
            foreach (var face in AllFaces)
            {
                float dot = Vector3.Dot(direction, face.Vector());
                if (dot > most) { most = dot; best = face; }
            }
            return best;
        }
    }
}
