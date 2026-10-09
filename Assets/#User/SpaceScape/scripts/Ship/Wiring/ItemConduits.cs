using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SomniumSpace.Worlds.SpaceScape.Ship
{
    /// The ship's item conduits (loading.md → Item conduits): square glass tubes that carry items from a
    /// component's ItemOut port to another's Item (in) port. Not part of the power grid.
    ///
    /// A run starts in the cell in front of an item port and goes cell by cell, each cell leaving by one face. Its
    /// open end has a tip: the next cell, which a hand grows the run into (ItemConduitHolograms). A run grows into
    /// any cell outside a slot's volume that no run holds; where that cell has a power conduit it must be parked,
    /// and it cannot be opened while the run, or a tip, is there. A run whose last cell leaves into an item port is connected and has no tip. Every item port with
    /// no run has a tip in front of it. No branching: one run joins two ports.
    ///
    /// The runs are the state (shared by ItemConduitNetwork); the tips, the tubes and the routes are worked out
    /// from them, and from where the components stand, on every client.
    public sealed class ItemConduits : MonoBehaviour
    {
        public enum Edit { Grow = 1, Aim = 2, Cut = 3 }

        /// One run: its first cell, the way from that cell to the port it starts at, and the face each cell
        /// leaves by.
        public sealed class Run
        {
            public Vector3Int Start;
            public GridDirection Into;
            public readonly List<GridDirection> Exits = new List<GridDirection>();
        }

        /// A cell a run can grow into: the run it would extend (-1 for a port's first cell) and the face it is
        /// entered by.
        public struct Tip
        {
            public Vector3Int Cell;
            public GridDirection Entry;
            public int Run;
        }

        private struct ItemPortAt
        {
            public ComponentEdgeModule Edges;
            public Vector3Int Front;        // the cell in front of the port
            public GridDirection Into;      // from that cell into the port
            public FlowDirection Flow;
        }

        /// The way an item goes from a sender's ItemOut port to a receiver's Item port: straight legs between
        /// solid corners. An item crosses a leg end to end, emerging whole from its start and swallowed whole at
        /// its end.
        public sealed class Route
        {
            internal readonly List<Vector3> Starts = new List<Vector3>();
            internal readonly List<Vector3> Ends = new List<Vector3>();
            internal readonly List<Vector3> Directions = new List<Vector3>();

            /// The component the items arrive at.
            public ComponentEdgeModule Receiver { get; internal set; }

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

        [Header("Items")]
        [Tooltip("How fast an item moves through a tube, in metres a second (one cell is half a metre).")]
        [SerializeField] private float _itemSpeed = 1f;

        [Header("Limits")]
        [Tooltip("The most ints the runs may take written out: the network's room. A run that would need more does not grow.")]
        [SerializeField] private int _maxInts = 62;
        [Tooltip("Seconds between looks at where the components' item ports are, as components come and go.")]
        [SerializeField] private float _portsCheckSeconds = 0.5f;

        private readonly List<Run> _runs = new List<Run>();
        private readonly Dictionary<Vector3Int, (int run, int index)> _cellOf = new Dictionary<Vector3Int, (int, int)>();
        private readonly List<Tip> _tips = new List<Tip>();
        private readonly HashSet<Vector3Int> _tipCells = new HashSet<Vector3Int>();
        private readonly List<ItemPortAt> _ports = new List<ItemPortAt>();
        private readonly Dictionary<Vector3Int, GridNode> _conduits = new Dictionary<Vector3Int, GridNode>();
        private readonly HashSet<Vector3Int> _reserved = new HashSet<Vector3Int>();
        private readonly List<GameObject> _tubes = new List<GameObject>();
        private readonly Dictionary<ComponentEdgeModule, Route> _routes = new Dictionary<ComponentEdgeModule, Route>();
        private Transform _tubeRoot;
        private float _nextPortsCheck;
        private int _portsSeen;

        /// The ship's item conduits, or null.
        public static ItemConduits Current { get; private set; }

        /// How fast an item moves through a tube, in metres a second.
        public float ItemSpeed => _itemSpeed;

        /// Bumped whenever the runs, or the ports they join, change.
        public int Revision { get; private set; }

        /// Raised whenever the runs change, from a hand here or from the network.
        public event Action Changed;

        /// Raised when a hand on this client changed a run: the edit, the cell, the face it is entered by, and
        /// the face it leaves by, for the network to pass on to the master.
        public event Action<Edit, Vector3Int, GridDirection, GridDirection> EditedByHand;

        /// The runs, for drawing and the network. Not to be changed from outside.
        public IReadOnlyList<Run> Runs => _runs;

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
            if (Time.time < _nextPortsCheck) return;
            _nextPortsCheck = Time.time + _portsCheckSeconds;
            if (RefreshPorts()) { _routes.Clear(); Revision++; }
            RebuildTips();
        }

        // ---- Questions

        /// True for a cell a run holds, or a tip waits in: its power conduit stays shut.
        public bool IsClaimed(Vector3Int cell) => _cellOf.ContainsKey(cell) || _tipCells.Contains(cell);

        /// The tip in a cell, if there is one.
        public bool TipAt(Vector3Int cell, out Tip tip)
        {
            foreach (var t in _tips)
                if (t.Cell == cell) { tip = t; return true; }
            tip = default;
            return false;
        }

        /// The run whose last cell this is, or -1.
        public int RunEndingAt(Vector3Int cell) =>
            _cellOf.TryGetValue(cell, out var at) && at.index == _runs[at.run].Exits.Count - 1 ? at.run : -1;

        /// The face a run's cell is entered by.
        public static GridDirection EntryOf(Run run, int index) => index == 0 ? run.Into : GridNode.Opposite(run.Exits[index - 1]);

        /// A run's cell.
        public static Vector3Int CellOf(Run run, int index)
        {
            var cell = run.Start;
            for (int i = 0; i < index; i++) cell += run.Exits[i].Offset();
            return cell;
        }

        /// True while a run is wired to this port, so its intake takes nothing by hand.
        private bool IsWired(ItemPort port)
        {
            var t = port.transform;
            var front = GridCell.ToCell(t.position + t.forward * GridCell.Half);
            var into = Nearest(-t.forward);
            foreach (var run in _runs)
            {
                if (run.Start == front && run.Into == into) return true;
                int last = run.Exits.Count - 1;
                if (CellOf(run, last) == front && run.Exits[last] == into) return true;
            }
            return false;
        }

        /// True where a run may go: a cell outside every slot's volume that no run holds, whose power conduit, if it
        /// has one, is parked.
        private bool IsFree(Vector3Int cell)
        {
            if (_cellOf.ContainsKey(cell) || _reserved.Contains(cell)) return false;
            if (!_conduits.TryGetValue(cell, out var tile) || tile == null) return true;
            if (tile.UsedFaces.Any()) return false;
            return !tile.TryGetComponent<AddonModule>(out var addon) || addon.Addon == null;
        }

        private bool PortAt(Vector3Int front, GridDirection into, out ItemPortAt port)
        {
            foreach (var p in _ports)
                if (p.Front == front && p.Into == into) { port = p; return true; }
            port = default;
            return false;
        }

        private bool Connected(Run run)
        {
            int last = run.Exits.Count - 1;
            return PortAt(CellOf(run, last), run.Exits[last], out _);
        }

        // ---- Editing

        /// Changes a run as a hand on this client did, and tells the network. True when it changed.
        public bool EditByHand(Edit edit, Vector3Int cell, GridDirection entry, GridDirection exit)
        {
            if (!Apply(edit, cell, entry, exit)) return false;
            EditedByHand?.Invoke(edit, cell, entry, exit);
            return true;
        }

        /// Grows a run from a tip, turns a run's last cell to leave by another face, or cuts it off. The master
        /// applies a hand's edit from elsewhere with this too. True when it changed.
        public bool Apply(Edit edit, Vector3Int cell, GridDirection entry, GridDirection exit)
        {
            switch (edit)
            {
                case Edit.Grow:
                {
                    if (exit == GridDirection.None || exit == entry) return false;
                    int found = -2;
                    foreach (var tip in _tips)
                        if (tip.Cell == cell && tip.Entry == entry) { found = tip.Run; break; }
                    if (found == -2) return false;
                    if (found >= 0) _runs[found].Exits.Add(exit);
                    else
                    {
                        var run = new Run { Start = cell, Into = entry };
                        run.Exits.Add(exit);
                        _runs.Add(run);
                    }
                    if (Write().Length > _maxInts)
                    {
                        if (found >= 0) _runs[found].Exits.RemoveAt(_runs[found].Exits.Count - 1);
                        else _runs.RemoveAt(_runs.Count - 1);
                        Debug.LogWarning("ItemConduits: no room left in the network for a longer run");
                        return false;
                    }
                    break;
                }
                case Edit.Aim:
                {
                    int r = RunEndingAt(cell);
                    if (r < 0 || exit == GridDirection.None) return false;
                    var run = _runs[r];
                    int last = run.Exits.Count - 1;
                    if (exit == EntryOf(run, last) || exit == run.Exits[last]) return false;
                    run.Exits[last] = exit;
                    break;
                }
                case Edit.Cut:
                {
                    int r = RunEndingAt(cell);
                    if (r < 0) return false;
                    var run = _runs[r];
                    run.Exits.RemoveAt(run.Exits.Count - 1);
                    if (run.Exits.Count == 0) _runs.RemoveAt(r);
                    break;
                }
                default:
                    return false;
            }
            Rebuild();
            return true;
        }

        // ---- State: count, then per run its first cell, its way in and length, and its exits ten to an int.

        public int[] Write()
        {
            var ints = new List<int> { _runs.Count };
            foreach (var run in _runs)
            {
                ints.Add(PackCell(run.Start));
                ints.Add((int)run.Into | (run.Exits.Count << 3));
                for (int i = 0; i < run.Exits.Count; i += 10)
                {
                    int packed = 0;
                    for (int k = 0; k < 10 && i + k < run.Exits.Count; k++) packed |= (int)run.Exits[i + k] << (k * 3);
                    ints.Add(packed);
                }
            }
            return ints.ToArray();
        }

        /// Takes the runs the network carries. Rebuilds only when they differ from these.
        public void Read(int[] ints)
        {
            if (ints == null || ints.Length == 0) return;
            if (ints.SequenceEqual(Write())) return;
            var runs = new List<Run>();
            int at = 0, count = ints[at++];
            for (int r = 0; r < count && at + 1 < ints.Length; r++)
            {
                var run = new Run { Start = UnpackCell(ints[at++]) };
                int head = ints[at++];
                run.Into = (GridDirection)(head & 7);
                int length = head >> 3;
                for (int i = 0; i < length; i += 10)
                {
                    if (at >= ints.Length) return;
                    int packed = ints[at++];
                    for (int k = 0; k < 10 && i + k < length; k++) run.Exits.Add((GridDirection)((packed >> (k * 3)) & 7));
                }
                runs.Add(run);
            }
            _runs.Clear();
            _runs.AddRange(runs);
            Rebuild();
        }

        private static int PackCell(Vector3Int c) => ((c.x + 512) & 1023) | (((c.y + 512) & 1023) << 10) | (((c.z + 512) & 1023) << 20);

        private static Vector3Int UnpackCell(int p) => new Vector3Int((p & 1023) - 512, ((p >> 10) & 1023) - 512, ((p >> 20) & 1023) - 512);

        // ---- Building

        private void Rebuild()
        {
            _cellOf.Clear();
            for (int r = 0; r < _runs.Count; r++)
                for (int i = 0; i < _runs[r].Exits.Count; i++) _cellOf[CellOf(_runs[r], i)] = (r, i);
            BuildTubes();
            RebuildTips();
            _routes.Clear();
            Revision++;
            Changed?.Invoke();
        }

        private void RebuildTips()
        {
            _tips.Clear();
            _tipCells.Clear();
            for (int r = 0; r < _runs.Count; r++)
            {
                var run = _runs[r];
                if (Connected(run)) continue;
                int last = run.Exits.Count - 1;
                var cell = CellOf(run, last) + run.Exits[last].Offset();
                if (IsFree(cell)) AddTip(cell, GridNode.Opposite(run.Exits[last]), r);
            }
            foreach (var port in _ports)
            {
                bool used = false;
                foreach (var run in _runs)
                {
                    int last = run.Exits.Count - 1;
                    if ((run.Start == port.Front && run.Into == port.Into) ||
                        (CellOf(run, last) == port.Front && run.Exits[last] == port.Into)) { used = true; break; }
                }
                if (!used && IsFree(port.Front)) AddTip(port.Front, port.Into, -1);
            }
        }

        private void AddTip(Vector3Int cell, GridDirection entry, int run)
        {
            _tips.Add(new Tip { Cell = cell, Entry = entry, Run = run });
            _tipCells.Add(cell);
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

        private void BuildTubes()
        {
            foreach (var tube in _tubes) if (tube != null) Destroy(tube);
            _tubes.Clear();
            if (_tubeRoot == null) return;
            foreach (var run in _runs)
                for (int i = 0; i < run.Exits.Count; i++)
                {
                    var entry = EntryOf(run, i);
                    var exit = run.Exits[i];
                    bool straight = exit == GridNode.Opposite(entry);
                    var go = new GameObject(straight ? "Item Tube" : "Item Tube Corner");
                    go.transform.SetParent(_tubeRoot, false);
                    Vector3 a = entry.Vector(), b = exit.Vector();
                    var turn = straight
                        ? Quaternion.LookRotation(b, Mathf.Abs(b.y) > 0.5f ? Vector3.forward : Vector3.up)
                        : Quaternion.LookRotation(a, Vector3.Cross(a, b));
                    go.transform.SetPositionAndRotation(GridCell.ToWorld(CellOf(run, i)), turn);
                    go.AddComponent<MeshFilter>().sharedMesh = straight ? _straightMesh : _cornerMesh;
                    go.AddComponent<MeshRenderer>().sharedMaterials = straight ? _straightMaterials : _cornerMaterials;
                    _tubes.Add(go);
                }
        }

        // ---- Routes

        /// The way items leave this component by its ItemOut port and reach another's Item port, or null when no
        /// run joins them.
        public Route RouteFrom(ComponentEdgeModule sender)
        {
            if (sender == null) return null;
            if (_routes.TryGetValue(sender, out var cached)) return cached;
            Route route = null;
            foreach (var port in _ports)
            {
                if (port.Edges != sender || port.Flow != FlowDirection.ItemOut) continue;
                route = Trace(port);
                if (route != null) break;
            }
            _routes[sender] = route;
            return route;
        }

        private Route Trace(ItemPortAt from)
        {
            foreach (var run in _runs)
            {
                int last = run.Exits.Count - 1;
                bool forward = run.Start == from.Front && run.Into == from.Into;
                bool backward = CellOf(run, last) == from.Front && run.Exits[last] == from.Into;
                if (!forward && !backward) continue;

                // The cells from the sender, each with the face the item comes in by and the face it leaves by.
                var cells = new List<(Vector3Int cell, GridDirection inBy, GridDirection outBy)>();
                for (int i = 0; i <= last; i++) cells.Add((CellOf(run, i), EntryOf(run, i), run.Exits[i]));
                if (backward)
                {
                    cells.Reverse();
                    for (int i = 0; i < cells.Count; i++) cells[i] = (cells[i].cell, cells[i].outBy, cells[i].inBy);
                }
                var end = cells[cells.Count - 1];
                if (!PortAt(end.cell, end.outBy, out var to) || to.Flow != FlowDirection.Item) return null;

                var route = new Route { Receiver = to.Edges };
                Vector3 legStart = Face(cells[0].cell, cells[0].inBy);
                Vector3 legDir = GridNode.Opposite(cells[0].inBy).Vector();
                foreach (var (cell, inBy, outBy) in cells)
                {
                    if (outBy == GridNode.Opposite(inBy)) continue;
                    // A solid corner: this leg ends where the item goes in, the next starts where it comes out.
                    route.Starts.Add(legStart);
                    route.Ends.Add(Face(cell, inBy));
                    route.Directions.Add(legDir);
                    legStart = Face(cell, outBy);
                    legDir = outBy.Vector();
                }
                route.Starts.Add(legStart);
                route.Ends.Add(Face(end.cell, end.outBy));
                route.Directions.Add(legDir);
                return route;
            }
            return null;
        }

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
