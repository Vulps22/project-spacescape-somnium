using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SomniumSpace.Worlds.SpaceScape.Ship
{
    /// The ship's item conduits (loading.md → Item conduit rules): square glass tubes that carry items from a
    /// component's ItemOut port to others' Item (in) ports. Not part of the power grid.
    ///
    /// An item conduit is a conduit: each installed cell has its segments, two or more with a Junction addon, each on
    /// a face or parked, and its addon; its hologram is the conduit hologram (ItemConduitHolograms), with no arrows.
    /// Two cells join where both have a segment on the face between them; a cell joins a port where it has a segment
    /// on the face toward it. The only difference from a power conduit is installing: a cell can take one only where
    /// an item port, or an installed item conduit's segment, points into it. Once installed it stays until it is
    /// fully parked (no segment on a face, no addon) with no port facing it and no installed item conduit beside it.
    ///
    /// Items take turns at a cell with more than two faces joined (Choose), side after side, skipping a side where
    /// nothing wants the item. Split only: one loader feeds a tree.
    ///
    /// The cells are the state (shared by ItemConduitNetwork); the tubes and routes are worked out from them, and
    /// from where the components stand, on every client.
    public sealed class ItemConduits : MonoBehaviour
    {
        /// What a hand did to a cell. Faces: a, the faces its segments are on, one bit per GridDirection. Addon: a, the
        /// index in Addons plus one (0 takes it out), and b, its state (a junction's extra segments).
        public enum Edit { Faces = 5, Addon = 6 }

        private sealed class Cell
        {
            public int Faces;          // one bit per GridDirection
            public int Addon = -1;     // index in _addons, or -1
            public int AddonState;
        }

        private struct ItemPortAt
        {
            public ComponentEdgeModule Edges;
            public Vector3Int Front;        // the cell in front of the port
            public GridDirection Into;      // from that cell into the port
            public FlowDirection Flow;
        }

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

            public int Segments => _owner.SegmentsAt(Coordinate);

            public bool HasArrows => false;

            public void GetSegments(List<GridDirection> faces, List<bool> outward)
            {
                int mask = _owner._cells.TryGetValue(Coordinate, out var cell) ? cell.Faces : Bit(_owner.EntryFor(Coordinate));
                foreach (var face in AllFaces)
                    if ((mask & Bit(face)) != 0) { faces.Add(face); outward.Add(true); }
            }

            public ConduitAddon Addon => _owner.AddonAt(Coordinate);

            public int AddonState => _owner._cells.TryGetValue(Coordinate, out var cell) ? cell.AddonState : 0;

            public GridDirection AddonFacing => GridDirection.None;

            public int AddonTurns => 0;

            public bool AddonParked => false;

            public bool RemoveAddon() => _owner.EditByHand(Edit.Addon, Coordinate, 0, 0);

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

        [Header("Addons")]
        [Tooltip("The addons an item conduit takes, as the part that goes on a conduit (the junction's). A cell's addon is its index here, so every client must have the same list.")]
        [SerializeField] private ConduitAddon[] _addons = new ConduitAddon[0];

        [Header("Items")]
        [Tooltip("How fast an item moves through a tube, in metres a second (one cell is half a metre).")]
        [SerializeField] private float _itemSpeed = 1f;

        [Header("Limits")]
        [Tooltip("The most cells that may be installed: the network's room (ItemConduitNetwork). Installing past it is refused.")]
        [SerializeField] private int _maxCells = 240;
        [Tooltip("Seconds between looks at where the components' item ports are, as components come and go.")]
        [SerializeField] private float _portsCheckSeconds = 0.5f;

        private readonly Dictionary<Vector3Int, Cell> _cells = new Dictionary<Vector3Int, Cell>();
        private readonly Dictionary<Vector3Int, ItemConduit> _views = new Dictionary<Vector3Int, ItemConduit>();
        private readonly List<ItemPortAt> _ports = new List<ItemPortAt>();
        private readonly Dictionary<Vector3Int, GridNode> _conduits = new Dictionary<Vector3Int, GridNode>();
        private readonly HashSet<Vector3Int> _reserved = new HashSet<Vector3Int>();
        private readonly List<GameObject> _tubes = new List<GameObject>();
        private readonly Dictionary<Vector3Int, List<Renderer>> _pieces = new Dictionary<Vector3Int, List<Renderer>>();
        private readonly Dictionary<Vector3Int, float> _gone = new Dictionary<Vector3Int, float>();     // cells smoking away or back
        private readonly Dictionary<Vector3Int, GameObject> _cables = new Dictionary<Vector3Int, GameObject>();
        private readonly Dictionary<Material, Material> _smoke = new Dictionary<Material, Material>();
        private Vector3Int? _revealed;
        private static readonly int DissolveId = Shader.PropertyToID("_Dissolve");
        private readonly Dictionary<Vector3Int, int> _turns = new Dictionary<Vector3Int, int>();
        private readonly Dictionary<ComponentEdgeModule, List<Route>> _routes = new Dictionary<ComponentEdgeModule, List<Route>>();
        private Transform _tubeRoot;
        private float _nextPortsCheck;
        private int _portsSeen;

        /// The ship's item conduits, or null.
        public static ItemConduits Current { get; private set; }

        /// How fast an item moves through a tube, in metres a second.
        public float ItemSpeed => _itemSpeed;

        /// The most cells that may be installed.
        public int MaxCells => _maxCells;

        /// Bumped whenever the cells, or the ports they join, change.
        public int Revision { get; private set; }

        /// Raised whenever the cells change, from a hand here or from the network.
        public event Action Changed;

        /// Raised when a hand on this client changed a cell: the edit, the cell and its two values, for the network
        /// to pass on to the master.
        public event Action<Edit, Vector3Int, int, int> EditedByHand;

        private void Awake() => Current = this;

        private void OnDestroy()
        {
            if (Current == this) Current = null;
            if (ItemPort.Wired == IsWired) ItemPort.Wired = null;
        }

        private void Start()
        {
            foreach (var tile in FindObjectsByType<GridNode>(FindObjectsSortMode.None))
            {
                if (tile.TryGetComponent<ConduitBehaviourModule>(out _))
                    foreach (var cell in tile.Cells) _conduits[cell] = tile;
                if (tile.TryGetComponent<ComponentSlot>(out _))
                    foreach (var cell in tile.Cells) _reserved.Add(cell);
            }
            var holograms = FindFirstObjectByType<ConduitHolograms>();
            if (holograms != null) holograms.Claimed = IsClaimed;
            ItemPort.Wired = IsWired;
            _tubeRoot = new GameObject("Item Conduit Tubes").transform;
            _tubeRoot.SetParent(transform, false);
            RefreshPorts();
            Rebuild();
        }

        private void Update()
        {
            Animate();
            if (Time.time < _nextPortsCheck) return;
            _nextPortsCheck = Time.time + _portsCheckSeconds;
            if (RefreshPorts()) { _routes.Clear(); Revision++; }
        }

        // ---- Questions

        /// True for a cell an item conduit is installed in, or could be installed in: its power conduit stays shut.
        public bool IsClaimed(Vector3Int cell) => _cells.ContainsKey(cell) || Installable(cell);

        /// True for a cell an item conduit is installed in.
        public bool Installed(Vector3Int cell) => _cells.ContainsKey(cell);

        /// True for a cell a hand could install an item conduit in: free, with an item port or an installed item
        /// conduit's segment pointing into it.
        public bool Installable(Vector3Int cell) => !_cells.ContainsKey(cell) && Free(cell) && EntryFor(cell) != GridDirection.None;

        /// The conduit in a cell, installed or installable, for its hologram; null for any other cell. The same object
        /// each time, so the hologram knows it.
        public IConduit ConduitAt(Vector3Int cell)
        {
            if (!_cells.ContainsKey(cell) && !Installable(cell)) return null;
            if (!_views.TryGetValue(cell, out var view)) _views[cell] = view = new ItemConduit(this, cell);
            return view;
        }

        /// How many segments a cell has: two, and a junction's extra.
        public int SegmentsAt(Vector3Int cell)
        {
            var addon = AddonAt(cell);
            if (addon == null) return ConduitHologram.PlainSegments;
            int extra = addon is JunctionAddon ? _cells[cell].AddonState : addon.GetExtraSegments();
            return ConduitHologram.PlainSegments + Mathf.Max(0, extra);
        }

        /// A cell's addon, as the prefab, or null.
        private ConduitAddon AddonAt(Vector3Int cell) =>
            _cells.TryGetValue(cell, out var c) && c.Addon >= 0 && c.Addon < _addons.Length ? _addons[c.Addon] : null;

        /// The index of an addon an item conduit takes, or -1.
        public int AddonIndex(ConduitAddon addon) => addon == null ? -1 : Array.IndexOf(_addons, addon);

        /// The face of a free cell toward an item port in front of it, or toward an installed item conduit with a
        /// segment pointing into it; None when nothing points into it.
        private GridDirection EntryFor(Vector3Int cell)
        {
            foreach (var face in AllFaces)
            {
                if (PortAt(cell, face, out _)) return face;
                if (_cells.TryGetValue(cell + face.Offset(), out var neighbour) && (neighbour.Faces & Bit(GridNode.Opposite(face))) != 0) return face;
            }
            return GridDirection.None;
        }

        /// True when an installed cell has something to reconnect to: an item port facing it, or an installed item
        /// conduit beside it.
        private bool HasNeighbour(Vector3Int cell)
        {
            foreach (var face in AllFaces)
                if (PortAt(cell, face, out _) || _cells.ContainsKey(cell + face.Offset())) return true;
            return false;
        }

        /// True where an item conduit may be: outside every slot's volume, where the power conduit, if there is one,
        /// is parked with no addon.
        private bool Free(Vector3Int cell)
        {
            if (_reserved.Contains(cell)) return false;
            if (!_conduits.TryGetValue(cell, out var tile) || tile == null) return true;
            if (tile.UsedFaces.Any()) return false;
            return !tile.TryGetComponent<AddonModule>(out var addon) || addon.Addon == null;
        }

        /// True while an item conduit is installed in front of this port with a segment on the face toward it, so its
        /// intake takes nothing by hand.
        private bool IsWired(ItemPort port)
        {
            var t = port.transform;
            var front = GridCell.ToCell(t.position + t.forward * GridCell.Half);
            var into = Nearest(-t.forward);
            return _cells.TryGetValue(front, out var cell) && (cell.Faces & Bit(into)) != 0;
        }

        private bool PortAt(Vector3Int front, GridDirection into, out ItemPortAt port)
        {
            foreach (var p in _ports)
                if (p.Front == front && p.Into == into) { port = p; return true; }
            port = default;
            return false;
        }

        private static int Bit(GridDirection face) => face == GridDirection.None ? 0 : 1 << (int)face;

        private static int Count(int mask)
        {
            int n = 0;
            for (; mask != 0; mask &= mask - 1) n++;
            return n;
        }

        /// The faces a hologram's segments are on, as Edit.Faces takes them.
        public static int Mask(IReadOnlyList<GridDirection> faces)
        {
            int mask = 0;
            foreach (var face in faces) mask |= Bit(face);
            return mask;
        }

        // ---- Editing

        /// Changes a cell as a hand on this client did, and tells the network. True when it changed.
        public bool EditByHand(Edit edit, Vector3Int cell, int a, int b)
        {
            if (!Apply(edit, cell, a, b)) return false;
            EditedByHand?.Invoke(edit, cell, a, b);
            return true;
        }

        /// Puts a cell's segments on faces (installing it, if a hand could), or puts an addon in or takes it out. The
        /// master applies a hand's edit from elsewhere with this too. True when it changed.
        public bool Apply(Edit edit, Vector3Int cell, int a, int b)
        {
            switch (edit)
            {
                case Edit.Faces:
                {
                    int mask = a & 0x7E;
                    if (!_cells.TryGetValue(cell, out var c))
                    {
                        if (mask == 0 || !Installable(cell) || Count(mask) > ConduitHologram.PlainSegments) return false;
                        if (_cells.Count >= _maxCells)
                        {
                            Debug.LogWarning("ItemConduits: no room left in the network for another item conduit");
                            return false;
                        }
                        _cells[cell] = c = new Cell();
                    }
                    else if (Count(mask) > SegmentsAt(cell) || c.Faces == mask) return false;
                    c.Faces = mask;
                    break;
                }
                case Edit.Addon:
                {
                    if (!_cells.TryGetValue(cell, out var c)) return false;
                    if (a > 0)
                    {
                        int index = a - 1;
                        if (c.Addon >= 0 || index >= _addons.Length || _addons[index] == null) return false;
                        if (_addons[index] is JunctionAddon && (b < JunctionAddon.MinExtra || b > JunctionAddon.MaxExtra)) return false;
                        c.Addon = index;
                        c.AddonState = b;
                    }
                    else
                    {
                        if (c.Addon < 0) return false;
                        c.Addon = -1;
                        c.AddonState = 0;
                        // Its extra segments went with it: more on faces than it now has parks the lot.
                        if (Count(c.Faces) > ConduitHologram.PlainSegments) c.Faces = 0;
                    }
                    break;
                }
                default:
                    return false;
            }
            Prune(cell);
            Rebuild();
            return true;
        }

        /// Takes out a fully parked cell with nothing left to reconnect to, and then any neighbour that leaves the
        /// same way.
        private void Prune(Vector3Int start)
        {
            var check = new Stack<Vector3Int>();
            check.Push(start);
            while (check.Count > 0)
            {
                var cell = check.Pop();
                if (!_cells.TryGetValue(cell, out var c) || c.Faces != 0 || c.Addon >= 0 || HasNeighbour(cell)) continue;
                _cells.Remove(cell);
                foreach (var face in AllFaces) check.Push(cell + face.Offset());
            }
        }

        // ---- State: per installed cell two ints: its cell, then its faces (bits 1 to 6), its addon's index plus one
        // (bits 8 to 11) and its addon's state (bits 12 to 15). Sorted by cell, so a client compares like with like.

        /// Two ints per installed cell.
        public int[] Write()
        {
            var ints = new List<int>(_cells.Count * 2);
            foreach (var pair in _cells.OrderBy(p => PackCell(p.Key)))
            {
                ints.Add(PackCell(pair.Key));
                ints.Add((pair.Value.Faces & 0x7E) | (((pair.Value.Addon + 1) & 0xF) << 8) | ((pair.Value.AddonState & 0xF) << 12));
            }
            return ints.ToArray();
        }

        /// Takes the cells the network carries. Rebuilds only when they differ from these.
        public void Read(int[] ints)
        {
            if (ints == null) return;
            if (ints.SequenceEqual(Write())) return;
            _cells.Clear();
            for (int i = 0; i + 1 < ints.Length; i += 2)
            {
                int info = ints[i + 1];
                _cells[UnpackCell(ints[i])] = new Cell { Faces = info & 0x7E, Addon = ((info >> 8) & 0xF) - 1, AddonState = (info >> 12) & 0xF };
            }
            Rebuild();
        }

        /// A grid cell in 30 bits, ten to an axis.
        public static int PackCell(Vector3Int c) => ((c.x + 512) & 1023) | (((c.y + 512) & 1023) << 10) | (((c.z + 512) & 1023) << 20);

        public static Vector3Int UnpackCell(int p) => new Vector3Int((p & 1023) - 512, ((p >> 10) & 1023) - 512, ((p >> 20) & 1023) - 512);

        // ---- Building

        private void Rebuild()
        {
            BuildTubes();
            _routes.Clear();
            Revision++;
            Changed?.Invoke();
        }

        /// Finds every seated component's item ports. True when they changed.
        private bool RefreshPorts()
        {
            int before = _ports.Count, hash = 17;
            _ports.Clear();
            foreach (var edges in FindObjectsByType<ComponentEdgeModule>(FindObjectsSortMode.None))
            {
                if (edges.TryGetComponent<SlottedComponent>(out var slotted) && slotted.Slot == null) continue;
                foreach (var (face, cell, flow) in edges.PortsAt(edges.transform.position, edges.transform.rotation))
                {
                    if (!ComponentEdgeModule.IsItem(flow)) continue;
                    var port = new ItemPortAt { Edges = edges, Front = cell + face.Offset(), Into = GridNode.Opposite(face), Flow = flow };
                    _ports.Add(port);
                    hash = hash * 31 + port.Front.GetHashCode() * 7 + (int)port.Into * 3 + (int)flow + edges.GetInstanceID();
                }
            }
            bool changed = hash != _portsSeen || before != _ports.Count;
            _portsSeen = hash;
            return changed;
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
            foreach (var pair in _cells)
            {
                faces.Clear();
                foreach (var face in AllFaces)
                    if ((pair.Value.Faces & Bit(face)) != 0) faces.Add(face);
                if (faces.Count == 0 && pair.Value.Addon < 0) continue;
                var at = GridCell.ToWorld(pair.Key);
                if (faces.Count > 2 || pair.Value.Addon >= 0)
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
            foreach (var cell in _gone.Keys.ToList())
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
            if (_cableMesh == null || _tubeRoot == null || !_cells.TryGetValue(cell, out var c)) return;
            var root = new GameObject("Item Conduit Cable");
            root.transform.SetParent(_tubeRoot, false);
            root.transform.position = GridCell.ToWorld(cell);
            foreach (var face in AllFaces)
            {
                if ((c.Faces & Bit(face)) == 0) continue;
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
            foreach (var port in _ports)
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
            if (!_cells.TryGetValue(at, out var cell) || (cell.Faces & Bit(inBy)) == 0 || !seen.Add(at)) return;
            var outs = AllFaces.Where(f => f != inBy && (cell.Faces & Bit(f)) != 0).ToList();
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
        private Route Build(List<(Vector3Int cell, GridDirection inBy, GridDirection outBy)> cells, List<(Vector3Int, GridDirection)> decisions, ItemPortAt to)
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
        private bool Solid(Vector3Int cell) => _cells.TryGetValue(cell, out var c) && (Count(c.Faces) > 2 || c.Addon >= 0);

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
