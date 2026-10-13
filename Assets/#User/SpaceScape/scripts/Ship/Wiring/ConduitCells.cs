using System;
using System.Collections.Generic;
using System.Linq;
using SomniumSpace.Worlds.SpaceScape.Player;
using UnityEngine;

namespace SomniumSpace.Worlds.SpaceScape.Ship
{
    /// Every installed conduit cell in the ship (conduits.md → Conduits V2): the one list the host owns and sends, and
    /// that a saved layout will hold. A cell is a power conduit or an item conduit, never both (item-conduit rule 7). Two
    /// carriers make the world match it: PowerConduits gives each power cell a tile on the grid, ItemConduits draws
    /// item cells as tubes and routes items along them.
    ///
    /// A cell can take a conduit only where a component's port, or an installed conduit's segment, points into it; it
    /// takes the kind of what points in (an installed neighbour first, power before items). It must be outside every
    /// slot's volume, with no tile already there, and not behind a shut access panel. Once installed it stays until
    /// every segment is parked, it has no addon, and nothing is beside it to reconnect to.
    ///
    /// A hand changes a cell through EditByHand (here at once; the networking passes it to the master); the master
    /// applies the same edit with Apply and the cells reach everyone in ConduitCellNetwork's chunks.
    public sealed class ConduitCells : MonoBehaviour
    {
        public enum Kind { Power = 0, Item = 1 }

        /// What a hand did to a cell.
        /// Faces: a, the faces its segments are on (bits 1 to 6, one per GridDirection); b, which of them are Out (power).
        /// Addon: a, the addon's id (0 takes it out); b, its state | facing << 8.
        /// Pose: a, facing | turns << 3 | parked << 5 (a power conduit's addon).
        /// AddonState: a, the addon's own state (a switch's open or closed).
        public enum Edit { Faces = 5, Addon = 6, Pose = 7, AddonState = 8 }

        public sealed class Cell
        {
            public Kind Kind;
            public int Used;       // faces with a segment, one bit per GridDirection
            public int Out;        // of those, the ones power leaves by (power only)
            public int Addon;      // id in the addon list, 0 for none
            public int AddonState;
            public GridDirection Facing = GridDirection.ZPlus;
            public int Turns;
            public bool Parked;
            public bool Popped;    // power only: blown, carrying nothing until mended

            public Cell Copy() => (Cell)MemberwiseClone();

            public bool SameAs(Cell o) =>
                o != null && Kind == o.Kind && Used == o.Used && Out == o.Out && Addon == o.Addon && AddonState == o.AddonState
                && Facing == o.Facing && Turns == o.Turns && Parked == o.Parked && Popped == o.Popped;
        }

        /// A component's port, from the cell in front of it.
        public struct Port
        {
            public ComponentEdgeModule Edges;
            public Vector3Int Front;        // the cell in front of the port
            public GridDirection Into;      // from that cell into the port
            public FlowDirection Flow;
        }

        private static readonly GridDirection[] AllFaces =
        {
            GridDirection.XPlus, GridDirection.XMinus,
            GridDirection.YPlus, GridDirection.YMinus,
            GridDirection.ZPlus, GridDirection.ZMinus,
        };

        [Tooltip("The most cells, of both kinds, that may be installed: the network's room. Installing past it is refused.")]
        [SerializeField] private int _maxCells = 1000;
        [Tooltip("Seconds between looks at where the components' ports are, as components come and go.")]
        [SerializeField] private float _portsCheckSeconds = 0.5f;
        [Tooltip("Seconds a cell a hand changed here is kept against the master's older state while the master catches up.")]
        [SerializeField] private float _editHoldSeconds = 2f;

        private readonly Dictionary<Vector3Int, Cell> _cells = new Dictionary<Vector3Int, Cell>();
        private readonly Dictionary<Vector3Int, float> _heldUntil = new Dictionary<Vector3Int, float>();
        private readonly List<Port> _ports = new List<Port>();
        private readonly Dictionary<Vector3Int, Pending> _pending = new Dictionary<Vector3Int, Pending>();
        private readonly Dictionary<Vector3Int, HashSet<string>> _openBy = new Dictionary<Vector3Int, HashSet<string>>();
        private readonly List<Vector3Int> _toUninstall = new List<Vector3Int>();
        private ConduitLayout _layout;
        private float _nextPortsCheck;
        private int _portsSeen;

        /// The ship's conduit cells, or null.
        public static ConduitCells Current { get; private set; }

        /// The most cells that may be installed.
        public int MaxCells => _maxCells;

        /// Bumped whenever the cells, or the ports they join, change.
        public int Revision { get; private set; }

        /// How many cells are installed.
        public int CellCount => _cells.Count;

        /// Every installed cell.
        public IEnumerable<KeyValuePair<Vector3Int, Cell>> All => _cells;

        /// Every component port, from the cell in front of it.
        public IReadOnlyList<Port> Ports => _ports;

        /// Raised for each cell that changed (installed, edited or gone), from a hand here, the grid or the network.
        public event Action<Vector3Int> CellChanged;

        /// Raised once after any change, the ports included.
        public event Action Changed;

        /// Raised when a hand on this client changed a cell: the edit, the cell and its two values, for the network to
        /// pass on to the master.
        public event Action<Edit, Vector3Int, int, int> EditedByHand;

        /// Raised where cells are decided when a conduit holding an addon is uninstalled: the cell, the addon and its
        /// state. The addon drops out there as an item (ConduitCellNetwork makes it).
        public event Action<Vector3Int, ConduitAddon, int> AddonDropped;

        /// True where conduits are uninstalled: the master, or anyone with no network. Set by ConduitCellNetwork.
        /// Everyone else takes the master's cells, so an uninstall and its dropped addon happen once.
        public bool Decides { get; set; } = true;

        /// A power cell's heat, for the network to send (set by PowerConduits). Degrees Celsius.
        public Func<Vector3Int, double> HeatOf;

        /// Gives a power cell the heat the network carries (set by PowerConduits).
        public Action<Vector3Int, double> TakeHeat;

        private void Awake() => Current = this;

        private void OnDestroy()
        {
            if (Current == this) Current = null;
        }

        private void Start()
        {
            _layout = FindFirstObjectByType<ConduitLayout>();
            var holograms = FindFirstObjectByType<ConduitHolograms>();
            if (holograms != null)
            {
                holograms.Claimed = IsClaimed;
                holograms.Opened += tile => SetOpen(tile.Coordinate, Here, true);
                holograms.Closed += tile => SetOpen(tile.Coordinate, Here, false);
            }
            var cellHolograms = FindFirstObjectByType<ItemConduitHolograms>();
            if (cellHolograms != null)
            {
                cellHolograms.Opened += cell => SetOpen(cell, Here, true);
                cellHolograms.Closed += cell => SetOpen(cell, Here, false);
            }
            RefreshPorts();
            Seed();
        }

        private void Update()
        {
            if (Time.time < _nextPortsCheck) return;
            _nextPortsCheck = Time.time + _portsCheckSeconds;
            bool ports = RefreshPorts();
            bool uninstalled = UninstallLoose();   // a component taken away can leave a parked conduit with nothing pointing in
            if (!ports && !uninstalled) return;
            Revision++;
            Changed?.Invoke();
        }

        // ---- Holograms open on cells, here and elsewhere: a conduit is never uninstalled while a hand has it open

        /// Who stands for this client's own holograms.
        private const string Here = "";

        /// Notes that a player (by id; this client's own holograms by none) has a cell's hologram open or has closed it.
        /// Closing it may uninstall the conduit there.
        public void SetOpen(Vector3Int cell, string player, bool open)
        {
            if (open)
            {
                if (!_openBy.TryGetValue(cell, out var who)) _openBy[cell] = who = new HashSet<string>();
                who.Add(player ?? Here);
                return;
            }
            if (_openBy.TryGetValue(cell, out var was) && was.Remove(player ?? Here) && was.Count == 0) _openBy.Remove(cell);
            if (UninstallLoose()) { Revision++; Changed?.Invoke(); }
        }

        /// A player left: every hologram they had open is closed.
        public void ClearOpen(string player)
        {
            _toUninstall.Clear();
            foreach (var pair in _openBy) if (pair.Value.Contains(player)) _toUninstall.Add(pair.Key);
            foreach (var cell in _toUninstall) SetOpen(cell, player, false);
        }

        /// True while any player, this one included, has the cell's hologram open.
        public bool IsOpen(Vector3Int cell) => _openBy.ContainsKey(cell);

        // ---- The starting layout

        /// Installs the ship's starting wiring: the layout's drawn power conduits, keeping only the runs that reach a
        /// slot or a placed tile; a run that starts and ends with nothing is left out (Vulps, 2026-10-12). Every client
        /// starts from it; a joining client's is then replaced by the master's cells as they arrive.
        private void Seed()
        {
            if (_layout == null) return;
            var drawn = new Dictionary<Vector3Int, ConduitCode>();
            foreach (var code in _layout.All) drawn[code.Cell] = code;

            var done = new HashSet<Vector3Int>();
            int kept = 0, dropped = 0, runsDropped = 0;
            foreach (var start in drawn.Keys)
            {
                if (done.Contains(start)) continue;
                // One run: the drawn cells joined to this one where both have a face toward the other.
                var run = new List<Vector3Int>();
                var queue = new Queue<Vector3Int>();
                queue.Enqueue(start);
                done.Add(start);
                bool reaches = false;
                while (queue.Count > 0)
                {
                    var cell = queue.Dequeue();
                    run.Add(cell);
                    var code = drawn[cell];
                    foreach (var face in AllFaces)
                    {
                        if (code.GetFace(face) == FlowDirection.None) continue;
                        var next = cell + face.Offset();
                        if (drawn.TryGetValue(next, out var other))
                        {
                            if (other.GetFace(GridNode.Opposite(face)) != FlowDirection.None && done.Add(next)) queue.Enqueue(next);
                        }
                        else if (Reserved(next) || TileThere(next)) reaches = true;
                    }
                }
                if (!reaches)
                {
                    dropped += run.Count;
                    runsDropped++;
                    continue;
                }
                foreach (var cell in run)
                {
                    var code = drawn[cell];
                    var c = new Cell { Kind = Kind.Power };
                    foreach (var face in AllFaces)
                    {
                        var flow = code.GetFace(face);
                        if (flow == FlowDirection.None) continue;
                        c.Used |= Bit(face);
                        if (flow == FlowDirection.Out) c.Out |= Bit(face);
                    }
                    if (code.Addon > 0 && AddonPrefab(code.Addon) != null)
                    {
                        c.Addon = code.Addon;
                        c.AddonState = AddonPrefab(code.Addon).NetworkState;
                        c.Facing = code.AddonFacing == GridDirection.None ? GridDirection.ZPlus : code.AddonFacing;
                        c.Turns = code.AddonTurns;
                        c.Parked = code.AddonParked;
                    }
                    _cells[cell] = c;
                    kept++;
                    CellChanged?.Invoke(cell);
                }
            }
            Debug.Log($"ConduitCells: starting layout {kept} power conduit cell(s); left out {dropped} cell(s) in {runsDropped} run(s) that reach nothing");
            Revision++;
            Changed?.Invoke();
        }

        // ---- Questions

        /// The cell installed here, or null.
        public Cell Get(Vector3Int cell) => _cells.TryGetValue(cell, out var c) ? c : null;

        /// True for a cell a conduit of either kind is installed in.
        public bool Installed(Vector3Int cell) => _cells.ContainsKey(cell);

        /// True for a cell a conduit of this kind is installed in.
        public bool Installed(Vector3Int cell, Kind kind) => _cells.TryGetValue(cell, out var c) && c.Kind == kind;

        /// True for an empty cell a hand could install a conduit in: free, with something pointing into it.
        public bool Installable(Vector3Int cell) => !_cells.ContainsKey(cell) && Free(cell) && EntryFor(cell, out _, out _, out _);

        /// True for an empty cell a hand could install a conduit of this kind in.
        public bool Installable(Vector3Int cell, Kind kind) =>
            !_cells.ContainsKey(cell) && Free(cell) && EntryFor(cell, out var k, out _, out _) && k == kind;

        /// True for a cell the power conduit hologram leaves alone: an installed item conduit's, or an empty one a hand
        /// could install a conduit in (the cell hologram opens there).
        public bool IsClaimed(Vector3Int cell) => Installed(cell, Kind.Item) || Installable(cell);

        /// True when something points into a cell: a port's face, or an installed conduit's segment.
        public bool PointedInto(Vector3Int cell) => EntryFor(cell, out _, out _, out _);

        /// The kind a conduit takes from the faces its segments go on: an item conduit when one faces an item port or an
        /// installed item conduit, a power conduit otherwise (conduits.md → Installing).
        private Kind KindFor(Vector3Int cell, int used)
        {
            foreach (var face in AllFaces)
            {
                if ((used & Bit(face)) == 0) continue;
                if (_cells.TryGetValue(cell + face.Offset(), out var n) && n.Kind == Kind.Item) return Kind.Item;
                if (PortAt(cell, face, out var port) && ComponentEdgeModule.IsItem(port.Flow)) return Kind.Item;
            }
            return Kind.Power;
        }

        /// How many segments a cell has: two, and a junction's extra.
        public int SegmentsAt(Vector3Int cell)
        {
            var c = Get(cell);
            var addon = c != null ? AddonPrefab(c.Addon) : null;
            if (addon == null) return ConduitHologram.PlainSegments;
            int extra = addon is JunctionAddon ? c.AddonState : addon.GetExtraSegments();
            return ConduitHologram.PlainSegments + Mathf.Max(0, extra);
        }

        /// The addons a conduit can have, in the order their ids count (the layout's list; append only).
        public IReadOnlyList<ConduitAddon> Addons => _layout != null ? _layout.Addons : (IReadOnlyList<ConduitAddon>)Array.Empty<ConduitAddon>();

        /// An addon's prefab by its id, or null.
        public ConduitAddon AddonPrefab(int id)
        {
            var addons = Addons;
            return id > 0 && id <= addons.Count ? addons[id - 1] : null;
        }

        /// An addon prefab's id, or 0.
        public int AddonId(ConduitAddon prefab)
        {
            var addons = Addons;
            for (int i = 0; i < addons.Count; i++)
                if (addons[i] == prefab) return i + 1;
            return 0;
        }

        /// What points into an empty cell, and so what a conduit installed there would be: its kind, the face toward
        /// what points in, and whether a segment on that face is Out. An installed neighbour's segment wins over a port,
        /// and power wins over items. False when nothing points into it.
        public bool EntryFor(Vector3Int cell, out Kind kind, out GridDirection face, out bool outward)
        {
            kind = Kind.Power;
            face = GridDirection.None;
            outward = true;
            bool found = false;
            foreach (var f in AllFaces)
            {
                if (!_cells.TryGetValue(cell + f.Offset(), out var n) || (n.Used & Bit(GridNode.Opposite(f))) == 0) continue;
                if (found && kind == Kind.Power) break;
                if (found && n.Kind != Kind.Power) continue;
                kind = n.Kind;
                face = f;
                // Meet the neighbour: power that leaves it comes in here, and the other way round.
                outward = n.Kind != Kind.Power || (n.Out & Bit(GridNode.Opposite(f))) == 0;
                found = true;
            }
            if (found) return true;
            foreach (var f in AllFaces)
            {
                if (!PortAt(cell, f, out var port)) continue;
                var k = ComponentEdgeModule.IsItem(port.Flow) ? Kind.Item : Kind.Power;
                if (found && (kind == Kind.Power || k != Kind.Power)) continue;
                kind = k;
                face = f;
                outward = k != Kind.Power || port.Flow == FlowDirection.In;
                found = true;
            }
            return found;
        }

        /// True where a conduit may go: outside every slot's volume, with no tile there, and not behind a shut panel.
        private bool Free(Vector3Int cell) => !Reserved(cell) && !TileThere(cell) && !AccessPanel.Blocks(cell);

        private static bool Reserved(Vector3Int cell) => ComponentSlot.InSlot(cell);

        /// True where the grid has a tile that is not a conduit: a slot, a placed component.
        private static bool TileThere(Vector3Int cell)
        {
            var grid = PowerGrid.Current;
            var tile = grid != null ? grid.TileAt(cell) : null;
            return tile != null && !tile.TryGetComponent<ConduitBehaviourModule>(out _);
        }

        /// The port a cell's face meets, if there is one.
        public bool PortAt(Vector3Int front, GridDirection into, out Port port)
        {
            foreach (var p in _ports)
                if (p.Front == front && p.Into == into) { port = p; return true; }
            port = default;
            return false;
        }

        public static int Bit(GridDirection face) => face == GridDirection.None ? 0 : 1 << (int)face;

        public static int Count(int mask)
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

        /// Which of a hologram's segments are Out, as Edit.Faces takes them.
        public static int OutMask(IReadOnlyList<GridDirection> faces, IReadOnlyList<bool> outward)
        {
            int mask = 0;
            for (int i = 0; i < faces.Count && i < outward.Count; i++)
                if (outward[i]) mask |= Bit(faces[i]);
            return mask;
        }

        /// An addon's facing, quarter turns and parked, as Edit.Pose takes them.
        public static int Pose(GridDirection facing, int turns, bool parked) => (int)facing | ((turns & 3) << 3) | (parked ? 1 << 5 : 0);

        // ---- Editing

        /// Changes a cell as a hand on this client did, and tells the network. True when it changed.
        public bool EditByHand(Edit edit, Vector3Int cell, int a, int b)
        {
            if (!Apply(edit, cell, a, b)) return false;
            _heldUntil[cell] = Time.time + _editHoldSeconds;
            EditedByHand?.Invoke(edit, cell, a, b);
            return true;
        }

        /// Makes a change to a cell. The master applies a hand's edit from elsewhere with this too. True when it changed.
        public bool Apply(Edit edit, Vector3Int cell, int a, int b)
        {
            _cells.TryGetValue(cell, out var c);
            if (edit != Edit.AddonState && AccessPanel.Blocks(cell)) return false;
            switch (edit)
            {
                case Edit.Faces:
                {
                    int used = a & 0x7E, outs = b & used;
                    if (c == null)
                    {
                        if (used == 0 || !Free(cell) || !EntryFor(cell, out _, out _, out _)) return false;
                        if (Count(used) > ConduitHologram.PlainSegments) return false;
                        if (_cells.Count >= _maxCells)
                        {
                            Debug.LogWarning("ConduitCells: no room left in the network for another conduit");
                            return false;
                        }
                        _cells[cell] = c = new Cell { Kind = KindFor(cell, used) };
                    }
                    else
                    {
                        if (Count(used) > SegmentsAt(cell)) return false;
                        // A parked conduit is unparked into its kind, unless its addon is one only power takes.
                        var addon = AddonPrefab(c.Addon);
                        if (c.Used == 0 && used != 0 && (addon == null || addon is JunctionAddon)) c.Kind = KindFor(cell, used);
                    }
                    if (c.Kind != Kind.Power) outs = 0;
                    if (c.Used == used && c.Out == outs) return false;
                    c.Used = used;
                    c.Out = outs;
                    if (c.Popped && outs != 0) c.Popped = false;   // a blown conduit given an output again is mended
                    break;
                }
                case Edit.Addon:
                {
                    if (c == null && a > 0 && AddonPrefab(a) != null && Installable(cell))
                    {
                        // An addon let go in an empty cell's parked conduit installs it, parked, as a power conduit.
                        if (_cells.Count >= _maxCells) return false;
                        _cells[cell] = c = new Cell { Kind = Kind.Power };
                    }
                    if (c == null) return false;
                    if (a > 0)
                    {
                        var prefab = AddonPrefab(a);
                        if (c.Addon != 0 || prefab == null) return false;
                        if (c.Kind == Kind.Item && !(prefab is JunctionAddon)) return false;   // an item conduit takes a junction
                        int state = b & 0xFF;
                        if (prefab is JunctionAddon && (state < JunctionAddon.MinExtra || state > JunctionAddon.MaxExtra)) return false;
                        var facing = (GridDirection)((b >> 8) & 7);
                        c.Addon = a;
                        c.AddonState = state;
                        c.Facing = facing == GridDirection.None ? GridDirection.ZPlus : facing;
                        c.Turns = 0;
                        c.Parked = false;
                    }
                    else
                    {
                        if (c.Addon == 0) return false;
                        c.Addon = 0;
                        c.AddonState = 0;
                        c.Parked = false;
                        // Its extra segments went with it: more on faces than it now has parks the lot.
                        if (Count(c.Used) > ConduitHologram.PlainSegments) { c.Used = 0; c.Out = 0; }
                    }
                    break;
                }
                case Edit.Pose:
                {
                    if (c == null || c.Addon == 0 || c.Kind != Kind.Power) return false;
                    var facing = (GridDirection)(a & 7);
                    int turns = (a >> 3) & 3;
                    bool parked = (a & (1 << 5)) != 0;
                    if (facing == GridDirection.None) facing = c.Facing;
                    if (c.Facing == facing && c.Turns == turns && c.Parked == parked) return false;
                    c.Facing = facing;
                    c.Turns = turns;
                    c.Parked = parked;
                    break;
                }
                case Edit.AddonState:
                {
                    if (c == null || c.Addon == 0 || c.AddonState == (a & 0xF)) return false;
                    c.AddonState = a & 0xF;
                    break;
                }
                default:
                    return false;
            }
            Changed1(cell);
            UninstallLoose();
            Revision++;
            Changed?.Invoke();
            return true;
        }

        /// A power conduit blew (the master's grid, between ticks): its outputs park, so it shows its inputs and nothing
        /// leaving, and it carries nothing until a hand gives it an output again.
        public void Blew(Vector3Int cell)
        {
            if (!_cells.TryGetValue(cell, out var c) || c.Kind != Kind.Power) return;
            c.Popped = true;
            c.Used &= ~c.Out;
            c.Out = 0;
            Changed1(cell);
            Revision++;
            Changed?.Invoke();
        }

        private void Changed1(Vector3Int cell) => CellChanged?.Invoke(cell);

        /// Uninstalls every conduit that should be (conduits.md → Installing): fully parked, nothing pointing into it,
        /// and no hologram open on it. An addon in one drops out as an item. Only where cells are decided; parked cells
        /// point nowhere, so taking one out never leaves another loose. True when any went.
        private bool UninstallLoose()
        {
            if (!Decides) return false;
            _toUninstall.Clear();
            foreach (var pair in _cells)
                if (pair.Value.Used == 0 && !IsOpen(pair.Key) && !PointedInto(pair.Key)) _toUninstall.Add(pair.Key);
            foreach (var cell in _toUninstall)
            {
                var c = _cells[cell];
                _cells.Remove(cell);
                Changed1(cell);
                var addon = AddonPrefab(c.Addon);
                if (addon != null) AddonDropped?.Invoke(cell, addon, c.AddonState);
            }
            return _toUninstall.Count > 0;
        }

        private bool Held(Vector3Int cell)
        {
            if (!_heldUntil.TryGetValue(cell, out var until)) return false;
            if (Time.time < until) return true;
            _heldUntil.Remove(cell);
            return false;
        }

        // ---- State: three ints per cell. Sorted by cell, so a client compares like with like.
        //   0: the cell (PackCell, 30 bits) | kind << 30
        //   1: used (bits 1-6) | out (7-12) | addon id (13-17) | addon state (18-21) | facing (22-24) | turns (25-26)
        //      | parked (27) | popped (28)
        //   2: heat, in Heat Step degrees above Heat Floor (16 bits); the high 16 bits are spare

        public const int IntsPerCell = 3;
        private const float HeatStep = 0.05f;   // 16 bits cover -100 to about 3177 C
        private const float HeatFloor = -100f;

        /// Three ints per installed cell.
        public int[] Write()
        {
            var ints = new int[_cells.Count * IntsPerCell];
            int i = 0;
            foreach (var pair in _cells.OrderBy(p => PackCell(p.Key)))
            {
                var c = pair.Value;
                ints[i++] = PackCell(pair.Key) | ((int)c.Kind << 30);
                ints[i++] = PackInfo(c);
                double heat = c.Kind == Kind.Power && HeatOf != null ? HeatOf(pair.Key) : 0.0;
                ints[i++] = Mathf.Clamp(Mathf.RoundToInt(((float)heat - HeatFloor) / HeatStep), 0, ushort.MaxValue);
            }
            return ints;
        }

        private static int PackInfo(Cell c) =>
            (c.Used & 0x7E) | ((c.Out & 0x7E) << 6) | ((c.Addon & 0x1F) << 13) | ((c.AddonState & 0xF) << 18)
            | (((int)c.Facing & 7) << 22) | ((c.Turns & 3) << 25) | (c.Parked ? 1 << 27 : 0) | (c.Popped ? 1 << 28 : 0);

        private static Cell UnpackInfo(Kind kind, int info) => new Cell
        {
            Kind = kind,
            Used = info & 0x7E,
            Out = (info >> 6) & 0x7E,
            Addon = (info >> 13) & 0x1F,
            AddonState = (info >> 18) & 0xF,
            Facing = (GridDirection)((info >> 22) & 7),
            Turns = (info >> 25) & 3,
            Parked = (info & (1 << 27)) != 0,
            Popped = (info & (1 << 28)) != 0,
        };

        /// Takes the cells the network carries: changes only the cells that differ, leaving alone any a hand here is
        /// still changing, and gives every power cell its heat.
        public void Read(int[] ints)
        {
            if (ints == null) return;
            var incoming = new Dictionary<Vector3Int, Cell>(ints.Length / IntsPerCell);
            for (int i = 0; i + IntsPerCell - 1 < ints.Length; i += IntsPerCell)
            {
                var cell = UnpackCell(ints[i] & 0x3FFFFFFF);
                var kind = (Kind)((ints[i] >> 30) & 1);
                incoming[cell] = UnpackInfo(kind, ints[i + 1]);
                if (kind == Kind.Power) TakeHeat?.Invoke(cell, HeatFloor + (ints[i + 2] & 0xFFFF) * HeatStep);
            }

            bool any = false;
            foreach (var pair in incoming)
            {
                if (Held(pair.Key)) continue;
                if (_cells.TryGetValue(pair.Key, out var mine) && mine.SameAs(pair.Value)) continue;
                _cells[pair.Key] = pair.Value;
                Changed1(pair.Key);
                any = true;
            }
            foreach (var cell in _cells.Keys.ToList())
            {
                if (incoming.ContainsKey(cell) || Held(cell)) continue;
                _cells.Remove(cell);
                Changed1(cell);
                any = true;
            }
            if (!any) return;
            Revision++;
            Changed?.Invoke();
        }

        /// A grid cell in 30 bits, ten to an axis.
        public static int PackCell(Vector3Int c) => ((c.x + 512) & 1023) | (((c.y + 512) & 1023) << 10) | (((c.z + 512) & 1023) << 20);

        public static Vector3Int UnpackCell(int p) => new Vector3Int((p & 1023) - 512, ((p >> 10) & 1023) - 512, ((p >> 20) & 1023) - 512);

        // ---- Ports

        /// Finds every seated component's ports. True when they changed.
        private bool RefreshPorts()
        {
            int before = _ports.Count, hash = 17;
            _ports.Clear();
            foreach (var edges in FindObjectsByType<ComponentEdgeModule>(FindObjectsSortMode.None))
            {
                if (edges.TryGetComponent<SlottedComponent>(out var slotted) && slotted.Slot == null) continue;
                foreach (var (face, cell, flow) in edges.PortsAt(edges.transform.position, edges.transform.rotation))
                {
                    if (flow == FlowDirection.None) continue;
                    var port = new Port { Edges = edges, Front = cell + face.Offset(), Into = GridNode.Opposite(face), Flow = flow };
                    _ports.Add(port);
                    hash = hash * 31 + port.Front.GetHashCode() * 7 + (int)port.Into * 3 + (int)flow + edges.GetInstanceID();
                }
            }
            bool changed = hash != _portsSeen || before != _ports.Count;
            _portsSeen = hash;
            return changed;
        }

        // ---- An empty cell a conduit could be installed in, as its hologram sees it

        /// The parked conduit a hand finds in an empty cell something points into, or null. The same object each time, so
        /// the hologram knows it.
        public IConduit PendingAt(Vector3Int cell)
        {
            if (!Installable(cell)) return null;
            if (!_pending.TryGetValue(cell, out var view)) _pending[cell] = view = new Pending(this, cell);
            return view;
        }

        /// Every segment parked, nothing drawn. Taken to be a power conduit (arrows) until a segment is unparked toward an
        /// item port or item conduit (conduits.md → Installing).
        private sealed class Pending : IConduit
        {
            private readonly ConduitCells _owner;

            public Pending(ConduitCells owner, Vector3Int cell)
            {
                _owner = owner;
                Coordinate = cell;
            }

            public Vector3Int Coordinate { get; }
            public Vector3 Centre => GridCell.ToWorld(Coordinate);
            public bool Occupies(Vector3Int cell) => cell == Coordinate;
            public int Across => 1;
            public int Segments => ConduitHologram.PlainSegments;
            public bool HasArrows => true;

            public void GetSegments(List<GridDirection> faces, List<bool> outward) { }

            public ConduitAddon Addon => null;
            public int AddonState => 0;
            public GridDirection AddonFacing => GridDirection.None;
            public int AddonTurns => 0;
            public bool AddonParked => false;
            public bool RemoveAddon() => false;

            public bool PairsOutward(GridDirection face) => _owner.PairsOutward(Coordinate, face);
        }

        /// The flow a power segment put on this face of a cell should have to meet what is there: true for Out. An
        /// installed power conduit's Out face, or a port that sends, is met by In; anything else by Out.
        public bool PairsOutward(Vector3Int cell, GridDirection face)
        {
            var next = cell + face.Offset();
            var opposite = GridNode.Opposite(face);
            if (_cells.TryGetValue(next, out var n) && n.Kind == Kind.Power && (n.Used & Bit(opposite)) != 0)
                return (n.Out & Bit(opposite)) == 0;
            if (PortAt(cell, face, out var port)) return port.Flow == FlowDirection.In;
            var grid = PowerGrid.Current;
            var tile = grid != null ? grid.TileAt(next) : null;
            var edges = tile != null ? tile.Edges : null;
            return edges == null || edges.GetFace(opposite) != FlowDirection.Out;
        }
    }
}
