using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace SomniumSpace.Worlds.SpaceScape.Ship
{
    /// The ship's starting wiring, one packed number per drawn power conduit (see ConduitCode), and the list of
    /// addons a conduit can have. Nothing of it is in the scene while editing; it shows as gizmos and is edited with
    /// the drawing tool. When the world starts, ConduitCells installs its runs as power conduit cells (conduits.md →
    /// Conduits V2), leaving out any run that reaches nothing; from then on the cells are the ship's wiring.
    public sealed class ConduitLayout : MonoBehaviour
    {
        [Tooltip("The addons a conduit can have, power or item. Only ever add to the end: a conduit cell stores its addon by position in this list (its id is the position plus one), so reordering or removing one changes what every later conduit has.")]
        [SerializeField] private ConduitAddon[] _addons = new ConduitAddon[0];
        [Tooltip("The drawn conduits, packed: the ship's starting wiring. Edit them with the drawing tool, not by hand.")]
        [SerializeField] private long[] _cells = new long[0];
        [Tooltip("The drawn item conduits, packed the same way (a used face is stored as In; item conduits have no arrows). Seeded as they are, after the power conduits.")]
        [SerializeField] private long[] _itemCells = new long[0];
        [Tooltip("Play mode, on the master's client only: after every change to a conduit or a slot's component (anyone's), log the whole layout, cell by cell, with the packed numbers for Cells. The last print is the wiring to copy into the scene.")]
        [SerializeField] private bool _logLayout;
        [Tooltip("Seconds of quiet after a change before the layout is logged, so one edit logs once.")]
        [SerializeField] private float _logDelay = 0.5f;

        private int _loggedRevision = -1;
        private int _loggedSlots;
        private float _changedAt = -1f;

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

        /// Every drawn item conduit.
        public IEnumerable<ConduitCode> Items
        {
            get { foreach (var packed in _itemCells) yield return ConduitCode.Unpack(packed); }
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

        private void Update()
        {
            if (!_logLayout) return;
            var cells = ConduitCells.Current;
            if (cells != null && !cells.Decides) return;   // only the master's client logs; it sees every change
            int revision = cells != null ? cells.Revision : 0;
            int slots = SlotsSignature();
            if (revision != _loggedRevision || slots != _loggedSlots)
            {
                _loggedRevision = revision;
                _loggedSlots = slots;
                _changedAt = Time.time;
            }
            if (_changedAt >= 0f && Time.time - _changedAt >= _logDelay)
            {
                _changedAt = -1f;
                LogLayout(cells);
            }
        }

        private static int SlotsSignature()
        {
            int hash = 17;
            foreach (var slot in ComponentSlot.All)
                hash = hash * 31 + (slot != null && slot.Component != null ? slot.Component.GetInstanceID() : 0);
            return hash;
        }

        /// The ship's wiring as it is now: every installed conduit cell, every slot's component, and the packed
        /// numbers for Cells and Item Cells.
        private void LogLayout(ConduitCells cells)
        {
            var text = new StringBuilder();
            var packed = new List<long>();
            var packedItems = new List<long>();
            var sorted = new List<KeyValuePair<Vector3Int, ConduitCells.Cell>>();
            if (cells != null) sorted.AddRange(cells.All);
            sorted.Sort((a, b) => a.Key.x != b.Key.x ? a.Key.x.CompareTo(b.Key.x) : a.Key.y != b.Key.y ? a.Key.y.CompareTo(b.Key.y) : a.Key.z.CompareTo(b.Key.z));

            text.AppendLine($"[SpaceScape] ConduitLayout: {sorted.Count} conduit cells, {ComponentSlot.All.Count} slots");
            foreach (var pair in sorted)
            {
                var c = pair.Value;
                var code = new ConduitCode { Cell = pair.Key, Addon = c.Addon, AddonFacing = c.Facing, AddonTurns = c.Turns, AddonParked = c.Parked };
                text.Append($"cell {pair.Key} {c.Kind}");
                foreach (GridDirection face in System.Enum.GetValues(typeof(GridDirection)))
                {
                    int bit = ConduitCells.Bit(face);
                    if (bit == 0 || (c.Used & bit) == 0) continue;
                    bool isOut = (c.Out & bit) != 0;
                    code.SetFace(face, isOut && c.Kind == ConduitCells.Kind.Power ? FlowDirection.Out : FlowDirection.In);
                    text.Append(c.Kind == ConduitCells.Kind.Power ? $" {face}:{(isOut ? "Out" : "In")}" : $" {face}");
                }
                if (c.Used == 0) text.Append(" (all parked)");
                if (c.Addon != 0)
                {
                    var addon = c.Addon <= _addons.Length ? _addons[c.Addon - 1] : null;
                    text.Append($" addon {c.Addon} '{(addon != null ? addon.name : "?")}' state {c.AddonState} facing {c.Facing} turns {c.Turns}{(c.Parked ? " parked" : "")}");
                }
                if (c.Popped) text.Append(" POPPED");
                text.AppendLine();
                if (c.Kind == ConduitCells.Kind.Power) packed.Add(code.Pack());
                else packedItems.Add(code.Pack());
            }
            foreach (var slot in ComponentSlot.All)
            {
                if (slot == null) continue;
                text.AppendLine($"slot '{slot.name}' at {slot.transform.position} {slot.Size}: {(slot.Component != null ? slot.Component.name : "empty")}");
            }
            text.AppendLine("cells: " + string.Join(",", packed));
            text.AppendLine("items: " + string.Join(",", packedItems));

            Debug.Log(text.ToString(), this);
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
