using System.Collections.Generic;
using SomniumSpace.Worlds.SpaceScape.Power;
using TMPro;
using UnityEngine;

namespace SomniumSpace.Worlds.SpaceScape.Ship
{
    /// A tile on the grid. Every tile is the same kind of thing — a conduit, a battery and a bulb
    /// differ only by what else is on the object. Declares what it feeds; power flows that way.
    [SelectionBase]
    public sealed class GridNode : MonoBehaviour
    {
        [Tooltip("How many cells this occupies (x, y, z). A cable is 1,1,1. A face covers a whole side. Ignored on a slot (its volume) and on a component (its size class).")]
        [SerializeField] private Vector3Int _size = Vector3Int.one;
        [Tooltip("Part of the grid while ticked. Behaviours and switches set this; off means nothing enters or leaves.")]
        [SerializeField] private bool _on = true;

        [Header("Readout")]
        [Tooltip("Debug text showing power in, drawn, wasted and temperature. Optional.")]
        [SerializeField] private TMP_Text _readout;
        [Tooltip("Seconds between readout updates.")]
        [SerializeField] private float _readoutInterval = 0.25f;

        /// The sim node this stands for, handed over by PowerGrid once the graph is built.
        public PowerNode Node { get; private set; }

        /// Which faces power crosses on this tile, or null when it has none.
        public NodeEdgeModule Edges => TryGetComponent<NodeEdgeModule>(out var edges) ? edges : null;

        /// Every face power leaves by.
        public IEnumerable<GridDirection> Outputs
        {
            get
            {
                var edges = Edges;
                if (edges == null) yield break;
                foreach (var face in edges.Outputs) yield return face;
            }
        }

        /// Faces power arrives by: every neighbour that points at this tile. Read from the graph
        /// once it exists, and worked out by looking around when it does not, so the editor can
        /// draw a corner before anything is running.
        public IEnumerable<GridDirection> InputFaces
        {
            get
            {
                var here = Coordinate;

                if (Node != null)
                {
                    foreach (var edge in Node.IncomingEdges)
                    {
                        var from = FindByNode(edge.From);
                        if (from == null) continue;
                        var face = FaceBetween(here, from.Coordinate);
                        if (face != GridDirection.None) yield return face;
                    }
                    yield break;
                }

                // Look outward from every cell on our own surface: if the thing on the far side
                // sends power back this way, that is a face power arrives by.
                var seen = new HashSet<GridDirection>();
                foreach (var face in AllFaces)
                {
                    var inward = Opposite(face);
                    foreach (var cell in FaceCells(face))
                    {
                        var other = At(cell + face.Offset());
                        if (other == null || other == this) continue;

                        bool feedsUs = false;
                        foreach (var theirs in other.Outputs)
                            if (theirs == inward) { feedsUs = true; break; }

                        if (feedsUs && seen.Add(face)) yield return face;
                        if (seen.Contains(face)) break;
                    }
                }
            }
        }

        /// Every face this tile uses: its declared In and Out faces, which for a conduit are its segments and
        /// so the shape its cable takes. A tile with no edges module falls back to what is connected.
        public IEnumerable<GridDirection> UsedFaces
        {
            get
            {
                var edges = Edges;
                if (edges != null)
                {
                    foreach (var face in edges.Outputs) yield return face;
                    foreach (var face in edges.Inputs) yield return face;
                    yield break;
                }
                foreach (var face in Outputs) yield return face;
                foreach (var face in InputFaces) yield return face;
            }
        }

        private static readonly GridDirection[] AllFaces =
        {
            GridDirection.XPlus, GridDirection.XMinus,
            GridDirection.YPlus, GridDirection.YMinus,
            GridDirection.ZPlus, GridDirection.ZMinus,
        };

        /// The face on the far side of a join: what leaves by XPlus arrives by XMinus.
        public static GridDirection Opposite(GridDirection face)
        {
            switch (face)
            {
                case GridDirection.XPlus: return GridDirection.XMinus;
                case GridDirection.XMinus: return GridDirection.XPlus;
                case GridDirection.YPlus: return GridDirection.YMinus;
                case GridDirection.YMinus: return GridDirection.YPlus;
                case GridDirection.ZPlus: return GridDirection.ZMinus;
                case GridDirection.ZMinus: return GridDirection.ZPlus;
                default: return GridDirection.None;
            }
        }

        /// The face of 'from' that points at 'to', when they are one tile apart.
        private static GridDirection FaceBetween(Vector3Int from, Vector3Int to)
        {
            var step = to - from;
            if (step == new Vector3Int(1, 0, 0)) return GridDirection.XPlus;
            if (step == new Vector3Int(-1, 0, 0)) return GridDirection.XMinus;
            if (step == new Vector3Int(0, 1, 0)) return GridDirection.YPlus;
            if (step == new Vector3Int(0, -1, 0)) return GridDirection.YMinus;
            if (step == new Vector3Int(0, 0, 1)) return GridDirection.ZPlus;
            if (step == new Vector3Int(0, 0, -1)) return GridDirection.ZMinus;
            return GridDirection.None;
        }

        private static GridNode[] _all;
        private static Dictionary<Vector3Int, GridNode> _cells;
        private static int _allFrame = -1;

        /// Every tile and every cell they stand in, gathered once a frame and shared. Without the
        /// graph a tile has to look around to see who points at it, and doing that per tile is the
        /// whole scene squared.
        private static void Refresh()
        {
            if (_allFrame == Time.frameCount && _all != null) return;

            _all = FindObjectsByType<GridNode>(FindObjectsSortMode.None);
            if (_cells == null) _cells = new Dictionary<Vector3Int, GridNode>();
            _cells.Clear();
            for (int i = 0; i < _all.Length; i++)
                foreach (var cell in _all[i].Cells)
                    if (!_cells.ContainsKey(cell)) _cells.Add(cell, _all[i]);

            _allFrame = Time.frameCount;
        }

        /// Tiles to consider when there is no graph to ask.
        private GridNode[] Neighbours() { Refresh(); return _all; }

        /// Whatever stands in a cell, or null.
        private static GridNode At(Vector3Int cell)
        {
            Refresh();
            return _cells.TryGetValue(cell, out var found) ? found : null;
        }

        /// Finds the tile standing for a given sim node.
        private GridNode FindByNode(PowerNode node)
        {
            foreach (var candidate in Neighbours())
                if (candidate.Node == node) return candidate;
            return null;
        }

        /// The cell this tile is anchored in. See GridCell for how big a cell is.
        public Vector3Int Coordinate => GridCell.ToCell(transform.position);

        /// The slot on this tile, or null. A slot's footprint is its own volume.
        private ComponentSlot Slot => TryGetComponent<ComponentSlot>(out var slot) ? slot : null;

        /// How many cells this thing occupies. A cable is one; a reactor is not. A slot's is its volume.
        public Vector3Int Size
        {
            get
            {
                var slot = Slot;
                if (slot != null) return slot.WorldMax - slot.WorldMin + Vector3Int.one;
                if (TryGetComponent<SlottedComponent>(out var component)) return component.LocalCells;
                return new Vector3Int(Mathf.Max(1, _size.x), Mathf.Max(1, _size.y), Mathf.Max(1, _size.z));
            }
        }

        /// The low corner of the footprint. An odd size centres on the anchor; an even one leans
        /// toward the high side, because a box of four has no middle cell to sit on.
        public Vector3Int Min
        {
            get
            {
                var slot = Slot;
                if (slot != null) return slot.WorldMin;
                var size = Size;
                return Coordinate - new Vector3Int((size.x - 1) / 2, (size.y - 1) / 2, (size.z - 1) / 2);
            }
        }

        /// The high corner of the footprint.
        public Vector3Int Max
        {
            get
            {
                var slot = Slot;
                return slot != null ? slot.WorldMax : Min + Size - Vector3Int.one;
            }
        }

        /// False while something else decides whether the node is on: a slot holding a component, where the
        /// component's own tile does. Only one tile writes a node's On.
        public bool DrivesNode { get; set; } = true;

        /// This component's heat while it is in no slot, carried with it and handed back to a slot's node
        /// when it goes in.
        public double HeldCelsius { get; set; } = PowerGraph.AmbientCelsius;

        /// Every cell this thing stands in, so two things cannot be built through each other.
        public IEnumerable<Vector3Int> Cells
        {
            get
            {
                var min = Min;
                var size = Size;
                for (int x = 0; x < size.x; x++)
                    for (int y = 0; y < size.y; y++)
                        for (int z = 0; z < size.z; z++)
                            yield return new Vector3Int(min.x + x, min.y + y, min.z + z);
            }
        }

        /// True when this thing stands in a given cell.
        public bool Occupies(Vector3Int cell)
        {
            var min = Min;
            var max = Max;
            return cell.x >= min.x && cell.x <= max.x
                && cell.y >= min.y && cell.y <= max.y
                && cell.z >= min.z && cell.z <= max.z;
        }

        /// The cells along one face of the footprint. A cable has one; a reactor's flank has many,
        /// so a run can meet it anywhere along that side rather than having to reach its middle.
        public IEnumerable<Vector3Int> FaceCells(GridDirection face)
        {
            var min = Min;
            var max = Max;

            switch (face)
            {
                case GridDirection.XPlus:  min.x = max.x; break;
                case GridDirection.XMinus: max.x = min.x; break;
                case GridDirection.YPlus:  min.y = max.y; break;
                case GridDirection.YMinus: max.y = min.y; break;
                case GridDirection.ZPlus:  min.z = max.z; break;
                case GridDirection.ZMinus: max.z = min.z; break;
                default: yield break;
            }

            for (int x = min.x; x <= max.x; x++)
                for (int y = min.y; y <= max.y; y++)
                    for (int z = min.z; z <= max.z; z++)
                        yield return new Vector3Int(x, y, z);
        }

        /// False while whatever is on this tile is switched off. The graph then skips the tile, so
        /// whatever was feeding it has nowhere to push.
        public bool On
        {
            get => _on;
            set { _on = value; if (Node != null && DrivesNode) Node.On = value; }
        }

        private float _nextReadout;

        /// Called by PowerGrid while it builds the graph.
        public void Bind(PowerNode node)
        {
            Node = node;
            if (DrivesNode) node.On = _on;
        }

        /// Lets go of the node, for a component leaving its slot.
        public void Unbind() => Node = null;

        private void Update()
        {
            // Unity writes the serialized field directly, so the property setter never fires when
            // the checkbox is clicked. Push it across every frame instead; it only writes on change.
            if (Node != null && DrivesNode && Node.On != _on) Node.On = _on;

            if (Node == null || _readout == null || !_readout.enabled) return;
            if (Time.time < _nextReadout) return;
            _nextReadout = Time.time + _readoutInterval;

            _readout.SetText(Node.Dumped > 0.01
                ? $"{name}\nin {Node.Inflow:0} W\ndrew {Node.Drawn:0} W\nWASTE {Node.Dumped:0} W\n{Node.Celsius:0.0} C"
                : $"{name}\nin {Node.Inflow:0} W\ndrew {Node.Drawn:0} W\n{Node.Celsius:0.0} C");
        }

        private void OnValidate()
        {
            if (Node != null && DrivesNode) Node.On = _on;
        }

        private void OnDrawGizmos()
        {
            var size = Size;
            if (size != Vector3Int.one)
            {
                Gizmos.color = new Color(1f, 0.5f, 0.2f, 0.5f);
                Gizmos.DrawWireCube(GridCell.Centre(Min, Max), (Vector3)size * GridCell.Size);
            }

            Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, 0.08f);
            Gizmos.color = new Color(1f, 0.85f, 0.3f, 0.6f);
            foreach (var face in Outputs)
                Gizmos.DrawLine(transform.position, transform.position + face.Vector() * (0.9f * GridCell.Size));

            Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.6f);
            foreach (var face in InputFaces)
                Gizmos.DrawLine(transform.position, transform.position + face.Vector() * (0.6f * GridCell.Size));
        }
    }
}
