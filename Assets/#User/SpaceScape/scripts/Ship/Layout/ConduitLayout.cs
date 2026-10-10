using System.Collections.Generic;
using UnityEngine;

namespace SomniumSpace.Worlds.SpaceScape.Ship
{
    /// The ship's conduits as drawn, one packed number each (see ConduitCode). Nothing of them is in the
    /// scene while editing; they show as gizmos, and are built from the conduit prefab when the world
    /// starts, before the grid is. A hand-placed tile in a drawn cell wins, and the drawn conduit is skipped.
    /// Every other cell a ConduitLayoutDefinition holds is built too, as a plain conduit with nothing wired.
    ///
    /// A drawn conduit on a ring stays part of it: the ring is traced through it, and it takes the ring's two
    /// faces on any face it leaves None. So a run is joined to a ring, or taken off one, by drawing the one cell
    /// where they meet, with a junction for its third face.
    public sealed class ConduitLayout : MonoBehaviour
    {
        private const string RootName = "Conduits (drawn)";

        [Tooltip("The conduit every drawn cell is built from.")]
        [SerializeField] private GameObject _conduitPrefab;
        [Tooltip("The addons a drawn conduit can have. Only ever add to the end: a conduit stores its addon by position in this list, so reordering or removing one changes what every later conduit has.")]
        [SerializeField] private ConduitAddon[] _addons = new ConduitAddon[0];
        [Tooltip("The drawn conduits, packed. Edit them with the drawing tool, not by hand.")]
        [SerializeField] private long[] _cells = new long[0];
        [Tooltip("Heights, in cells above y 0 (0.5 m each), of the rings: at each, every cell of a definition that Carries Rings is wired into one run from one open end to the other (down a corridor, round the room, back up the corridor). Each next height runs the other way. A height whose cells close into a loop or branch is left parked, with a warning: a closed loop would be a ring that blows (power.md).")]
        [SerializeField] private int[] _ringHeights = { 2, 4 };

        private Dictionary<Vector3Int, int> _index;

        /// The addons a conduit can have. A conduit's addon id is its position here plus one.
        public IReadOnlyList<ConduitAddon> Addons => _addons;

        /// How many conduits are drawn.
        public int Count => _cells.Length;

        /// The cell the drawing tool has selected, for the gizmos to show. Editor only; not saved.
        public Vector3Int? Selected { get; set; }

        /// Every drawn conduit.
        public IEnumerable<ConduitCode> All
        {
            get { foreach (var packed in _cells) yield return ConduitCode.Unpack(packed); }
        }

        /// The conduit drawn in a cell, if there is one.
        public bool TryGet(Vector3Int cell, out ConduitCode code)
        {
            if (Index.TryGetValue(cell, out int i)) { code = ConduitCode.Unpack(_cells[i]); return true; }
            code = default;
            return false;
        }

        /// Draws a conduit, or replaces the one in its cell. False when the cell is out of range.
        public bool Set(ConduitCode code)
        {
            if (!code.InRange) return false;
            if (Index.TryGetValue(code.Cell, out int i)) { _cells[i] = code.Pack(); return true; }

            var grown = new long[_cells.Length + 1];
            _cells.CopyTo(grown, 0);
            grown[_cells.Length] = code.Pack();
            _cells = grown;
            _index[code.Cell] = _cells.Length - 1;
            return true;
        }

        /// Rubs out the conduit in a cell, and every neighbour's face that pointed at it.
        public bool Remove(Vector3Int cell)
        {
            if (!Index.TryGetValue(cell, out int i)) return false;
            var kept = new List<long>(_cells);
            kept.RemoveAt(i);
            _cells = kept.ToArray();
            _index = null;

            foreach (GridDirection face in System.Enum.GetValues(typeof(GridDirection)))
            {
                if (face == GridDirection.None || !TryGet(cell + face.Offset(), out var neighbour)) continue;
                neighbour.SetFace(GridNode.Opposite(face), FlowDirection.None);
                Set(neighbour);
            }
            return true;
        }

        /// Forgets the lookup, for when the array changed underneath it (an undo).
        public void Invalidate() => _index = null;

        private Dictionary<Vector3Int, int> Index
        {
            get
            {
                if (_index != null) return _index;
                _index = new Dictionary<Vector3Int, int>(_cells.Length);
                for (int i = 0; i < _cells.Length; i++) _index[ConduitCode.Unpack(_cells[i]).Cell] = i;
                return _index;
            }
        }

        private void OnValidate() => _index = null;

        /// Builds every drawn conduit, then fills the definitions' remaining cells, once. Called by the grid before it builds, so they are part of it.
        public void Spawn()
        {
            if (_conduitPrefab == null || transform.Find(RootName) != null) return;

            var taken = new HashSet<Vector3Int>();
            foreach (var tile in FindObjectsByType<GridNode>(FindObjectsSortMode.None))
                foreach (var cell in tile.Cells) taken.Add(cell);

            // Built inside an inactive parent, so each is set up before any of its scripts wake.
            var root = new GameObject(RootName);
            root.SetActive(false);
            root.transform.SetParent(transform, false);

            var rings = WireRings(taken);

            foreach (var code in All)
            {
                if (taken.Contains(code.Cell))
                {
                    Debug.LogWarning($"ConduitLayout: cell {code.Cell} is drawn, but a placed tile is already there; skipped", this);
                    continue;
                }
                if (code.HasContradiction)
                    Debug.LogWarning($"ConduitLayout: cell {code.Cell} has a face that is both In and Out; built as None", this);

                var conduit = Instantiate(_conduitPrefab, GridCell.ToWorld(code.Cell), Quaternion.identity, root.transform);
                if (conduit.TryGetComponent<NodeEdgeModule>(out var edges))
                {
                    foreach (GridDirection face in System.Enum.GetValues(typeof(GridDirection)))
                        if (face != GridDirection.None) edges.SetFace(face, code.GetFace(face));
                    // On a ring: the ring's faces too, wherever the drawing leaves a face None.
                    if (rings.TryGetValue(code.Cell, out var through))
                    {
                        if (through.from != GridDirection.None && code.GetFace(through.from) == FlowDirection.None) edges.SetFace(through.from, FlowDirection.In);
                        if (through.to != GridDirection.None && code.GetFace(through.to) == FlowDirection.None) edges.SetFace(through.to, FlowDirection.Out);
                    }
                }

                if (code.Addon == 0) continue;
                var addon = code.Addon <= _addons.Length ? _addons[code.Addon - 1] : null;
                if (addon == null)
                {
                    Debug.LogWarning($"ConduitLayout: cell {code.Cell} has addon {code.Addon}, which is not in the list", this);
                    continue;
                }
                if (conduit.TryGetComponent<AddonModule>(out var slot))
                    slot.Configure(addon, code.AddonFacing, code.AddonTurns, code.AddonParked);
            }

            // Every other cell a definition holds gets a plain conduit, both segments parked, or its ring's
            // two faces when it is on one.
            foreach (var definition in FindObjectsByType<ConduitLayoutDefinition>(FindObjectsSortMode.None))
            {
                // Not isActiveAndEnabled: this runs from the grid's Awake, before a definition's own OnEnable,
                // and until then Unity reports every definition as disabled.
                if (!definition.enabled || !definition.gameObject.activeInHierarchy) continue;
                foreach (var cell in definition.Cells)
                {
                    if (taken.Contains(cell) || Index.ContainsKey(cell)) continue;
                    taken.Add(cell);
                    var plain = Instantiate(_conduitPrefab, GridCell.ToWorld(cell), Quaternion.identity, root.transform);
                    if (plain.TryGetComponent<NodeEdgeModule>(out var edges))
                    {
                        foreach (GridDirection face in System.Enum.GetValues(typeof(GridDirection)))
                            if (face != GridDirection.None) edges.SetFace(face, FlowDirection.None);
                        if (rings.TryGetValue(cell, out var ring))
                        {
                            if (ring.from != GridDirection.None) edges.SetFace(ring.from, FlowDirection.In);
                            if (ring.to != GridDirection.None) edges.SetFace(ring.to, FlowDirection.Out);
                        }
                    }
                }
            }

            root.SetActive(true);
        }

        private static readonly GridDirection[] Level =
            { GridDirection.XPlus, GridDirection.XMinus, GridDirection.ZPlus, GridDirection.ZMinus };

        /// The rings: at each Ring Height, every free cell of the definitions that carry rings, traced from one
        /// open end to the other, each cell given the face power comes in by and the face it goes on by. The
        /// first height starts at the end lowest in x then z, the next at the other end, and so on, so they
        /// run opposite ways.
        private Dictionary<Vector3Int, (GridDirection from, GridDirection to)> WireRings(HashSet<Vector3Int> taken)
        {
            var wired = new Dictionary<Vector3Int, (GridDirection, GridDirection)>();
            for (int h = 0; h < _ringHeights.Length; h++)
            {
                var cells = new HashSet<Vector3Int>();
                foreach (var definition in FindObjectsByType<ConduitLayoutDefinition>(FindObjectsSortMode.None))
                {
                    if (!definition.CarriesRings || !definition.enabled || !definition.gameObject.activeInHierarchy) continue;
                    foreach (var cell in definition.Cells)
                        if (cell.y == _ringHeights[h] && !taken.Contains(cell)) cells.Add(cell);
                }

                var done = new HashSet<Vector3Int>();
                foreach (var seed in cells)
                {
                    if (done.Contains(seed)) continue;
                    var piece = Piece(seed, cells, done);
                    var ends = new List<Vector3Int>();
                    bool branches = false;
                    foreach (var cell in piece)
                    {
                        int around = Around(cell, cells);
                        if (around == 1) ends.Add(cell);
                        else if (around > 2) branches = true;
                    }
                    if (piece.Count < 2) continue;
                    if (branches || ends.Count != 2)
                    {
                        Debug.LogWarning($"ConduitLayout: the ring at height {_ringHeights[h]} near {seed} {(branches ? "branches" : "closes into a loop")}; left parked", this);
                        continue;
                    }

                    ends.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.z.CompareTo(b.z));
                    var at = h % 2 == 0 ? ends[0] : ends[1];
                    var came = GridDirection.None;
                    while (true)
                    {
                        var next = GridDirection.None;
                        foreach (var face in Level)
                            if (face != came && cells.Contains(at + face.Offset())) { next = face; break; }
                        wired[at] = (came, next);
                        if (next == GridDirection.None) break;
                        at += next.Offset();
                        came = GridNode.Opposite(next);
                    }
                }
            }
            return wired;
        }

        /// Every cell joined to the seed across level faces, marking them done.
        private static List<Vector3Int> Piece(Vector3Int seed, HashSet<Vector3Int> cells, HashSet<Vector3Int> done)
        {
            var piece = new List<Vector3Int>();
            var queue = new Queue<Vector3Int>();
            queue.Enqueue(seed);
            done.Add(seed);
            while (queue.Count > 0)
            {
                var cell = queue.Dequeue();
                piece.Add(cell);
                foreach (var face in Level)
                {
                    var next = cell + face.Offset();
                    if (cells.Contains(next) && done.Add(next)) queue.Enqueue(next);
                }
            }
            return piece;
        }

        private static int Around(Vector3Int cell, HashSet<Vector3Int> cells)
        {
            int count = 0;
            foreach (var face in Level)
                if (cells.Contains(cell + face.Offset())) count++;
            return count;
        }

        private void OnDrawGizmos()
        {
            if (Application.isPlaying) return;
            float half = GridCell.Half;
            foreach (var code in All)
            {
                Vector3 centre = GridCell.ToWorld(code.Cell);
                Gizmos.color = code.HasContradiction ? Color.red : new Color(0.7f, 0.7f, 0.75f, 0.8f);
                Gizmos.DrawWireCube(centre, Vector3.one * (GridCell.Size * 0.15f));

                foreach (GridDirection face in System.Enum.GetValues(typeof(GridDirection)))
                {
                    var flow = face == GridDirection.None ? FlowDirection.None : code.GetFace(face);
                    if (flow == FlowDirection.None) continue;
                    Gizmos.color = flow == FlowDirection.Out ? new Color(1f, 0.85f, 0.3f) : new Color(0.4f, 0.8f, 1f);
                    Vector3 end = centre + face.Vector() * half;
                    Gizmos.DrawLine(centre, end);
                    if (flow == FlowDirection.Out) Gizmos.DrawSphere(end - face.Vector() * (half * 0.2f), GridCell.Size * 0.03f);
                }

                if (code.Addon > 0)
                {
                    var addon = code.Addon <= _addons.Length ? _addons[code.Addon - 1] : null;
                    float outward = addon != null ? addon.Outward * half : 0f;
                    var facing = code.AddonFacing == GridDirection.None ? GridDirection.ZPlus : code.AddonFacing;
                    Gizmos.color = code.AddonParked ? new Color(0.5f, 0.5f, 0.5f) : new Color(0.3f, 1f, 0.6f);
                    Gizmos.matrix = Matrix4x4.TRS(centre + facing.Vector() * outward,
                        Quaternion.LookRotation(facing.Vector(), AddonModule.UpFor(facing, code.AddonTurns)), Vector3.one);
                    Gizmos.DrawWireCube(Vector3.zero, new Vector3(0.1f, 0.18f, 0.02f));
                    Gizmos.DrawLine(Vector3.zero, Vector3.up * 0.12f);
                    Gizmos.matrix = Matrix4x4.identity;
                }
            }

            if (Selected.HasValue)
            {
                Gizmos.color = Color.white;
                Gizmos.DrawWireCube(GridCell.ToWorld(Selected.Value), Vector3.one * GridCell.Size);
            }
        }
    }
}
