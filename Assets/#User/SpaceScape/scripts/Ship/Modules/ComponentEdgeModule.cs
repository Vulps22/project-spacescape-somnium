using System;
using System.Collections.Generic;
using UnityEngine;

namespace SomniumSpace.Worlds.SpaceScape.Ship
{
    /// Where a component takes power in and sends it out: ports, each on one cell of one face, In or Out,
    /// at most one per cell face. Power crosses nowhere else, so a conduit touching the component anywhere
    /// but a port does not join it. Each port shows as a Port prefab on its cell face, its ring blue for In
    /// and green for Out.
    ///
    /// Ports are the component's own: faces and cells turn with it, in quarter turns, so a port stays on
    /// the same spot of the model however the component is placed. A port's cell is counted from the face's
    /// bottom left, looking at that face from outside: (0, 0) is the bottom-left cell, x goes right and y
    /// goes up. On the top and bottom faces, up is toward the component's front. The component's size
    /// (GridNode Size) is along its own axes, and its transform is its centre. The grid stays world-aligned;
    /// everything the grid asks is answered in world faces and cells.
    public sealed class ComponentEdgeModule : NodeEdgeModule
    {
        [Serializable]
        public struct Port
        {
            [Tooltip("The cell on the face, counted from its bottom left looking at it from outside: x right, y up. On the top and bottom faces, up is toward the component's front.")]
            public Vector2Int Cell;
            [Tooltip("In takes power from a neighbour's Out; Out sends to a neighbour's In.")]
            public FlowDirection Flow;
        }

        // Each face's ports as plain ints (x | y << 8 | flow << 16): arrays of a custom serializable type
        // arrived empty in an uploaded world, where a plain int[] survives. Edited through the Inspector as ports.
        [SerializeField] private int[] _packedXPlus = new int[0];
        [SerializeField] private int[] _packedXMinus = new int[0];
        [SerializeField] private int[] _packedYPlus = new int[0];
        [SerializeField] private int[] _packedYMinus = new int[0];
        [SerializeField] private int[] _packedZPlus = new int[0];
        [SerializeField] private int[] _packedZMinus = new int[0];

        [Tooltip("The Port prefab shown on each port.")]
        [SerializeField] private PortVisual _portPrefab;

        private static readonly GridDirection[] AllFaces =
        {
            GridDirection.XPlus, GridDirection.XMinus,
            GridDirection.YPlus, GridDirection.YMinus,
            GridDirection.ZPlus, GridDirection.ZMinus,
        };

        private Dictionary<(GridDirection, Vector3Int), FlowDirection> _byCell;
        private Vector3 _indexedAt;
        private Quaternion _indexedTurn;

        /// The ports on one face.
        public Port[] PortsOn(GridDirection face)
        {
            var packed = Packed(face);
            var ports = new Port[packed?.Length ?? 0];
            for (int i = 0; i < ports.Length; i++) ports[i] = Unpack(packed[i]);
            return ports;
        }

        /// Sets the ports on one face, for the tools that build components.
        public void SetPorts(GridDirection face, Port[] ports)
        {
            ports ??= new Port[0];
            var packed = new int[ports.Length];
            for (int i = 0; i < ports.Length; i++) packed[i] = Pack(ports[i]);
            switch (face)
            {
                case GridDirection.XPlus: _packedXPlus = packed; break;
                case GridDirection.XMinus: _packedXMinus = packed; break;
                case GridDirection.YPlus: _packedYPlus = packed; break;
                case GridDirection.YMinus: _packedYMinus = packed; break;
                case GridDirection.ZPlus: _packedZPlus = packed; break;
                case GridDirection.ZMinus: _packedZMinus = packed; break;
            }
            _byCell = null;
        }

        private int[] Packed(GridDirection face)
        {
            switch (face)
            {
                case GridDirection.XPlus: return _packedXPlus;
                case GridDirection.XMinus: return _packedXMinus;
                case GridDirection.YPlus: return _packedYPlus;
                case GridDirection.YMinus: return _packedYMinus;
                case GridDirection.ZPlus: return _packedZPlus;
                case GridDirection.ZMinus: return _packedZMinus;
                default: return new int[0];
            }
        }

        /// A port as one int: x in the low byte, y in the next, flow above them.
        public static int Pack(Port port) => (port.Cell.x & 0xFF) | ((port.Cell.y & 0xFF) << 8) | (((int)port.Flow & 0xFF) << 16);

        public static Port Unpack(int packed) => new Port
        {
            Cell = new Vector2Int(packed & 0xFF, (packed >> 8) & 0xFF),
            Flow = (FlowDirection)((packed >> 16) & 0xFF),
        };

        /// The world face one of the component's own faces points along, at its current quarter turn.
        public GridDirection WorldFace(GridDirection local) => WorldFace(local, transform.rotation);

        /// The world face one of the component's own faces points along, were it turned this way.
        public GridDirection WorldFace(GridDirection local, Quaternion rotation) => Nearest(Snap(rotation) * local.Vector());

        /// The Port prefab shown on each port.
        public PortVisual PortPrefab => _portPrefab;

        /// Every port that counts (In or Out, on its face, first on its cell face), as world face, cell and
        /// flow, were the component centred and turned as given. For drawing a component that is not there
        /// yet, like a slot's starting one while editing.
        public IEnumerable<(GridDirection face, Vector3Int cell, FlowDirection flow)> PortsAt(Vector3 centre, Quaternion rotation)
        {
            var taken = new HashSet<(GridDirection, Vector3Int)>();
            foreach (var local in AllFaces)
            {
                var face = WorldFace(local, rotation);
                foreach (var port in PortsOn(local))
                {
                    if (port.Flow == FlowDirection.None || !OnFace(local, port.Cell)) continue;
                    var cell = CellOf(local, port.Cell, centre, rotation);
                    if (taken.Add((face, cell))) yield return (face, cell, port.Flow);
                }
            }
        }

        /// The world cell a port stands on: the cell of the component's volume on that face, at the port's
        /// place counted from the face's bottom left.
        public Vector3Int CellOf(GridDirection local, Vector2Int onFace) =>
            CellOf(local, onFace, transform.position, transform.rotation);

        /// The world cell a port would stand on, were the component centred and turned as given.
        public Vector3Int CellOf(GridDirection local, Vector2Int onFace, Vector3 centre, Quaternion rotation)
        {
            Frame(local, out var normal, out var right, out var up, out int width, out int height, out int depth);
            Vector3 cells = normal * ((depth - 1) * 0.5f)
                + right * (onFace.x - (width - 1) * 0.5f)
                + up * (onFace.y - (height - 1) * 0.5f);
            return GridCell.ToCell(centre + Snap(rotation) * (cells * GridCell.Size));
        }

        /// Where a port sits on the component's surface, exactly: the middle of its cell's face, not snapped to
        /// the grid. Matches the grid once the component is centred on a grid corner, as a slot seats it; in
        /// a prefab, centred anywhere, it is still right against the model.
        public Vector3 PointOf(GridDirection local, Vector2Int onFace) =>
            transform.position + Snap(transform.rotation) * LocalPointOf(local, onFace);

        /// Where a port sits on the component's surface, in the component's own axes from its centre.
        private Vector3 LocalPointOf(GridDirection local, Vector2Int onFace)
        {
            Frame(local, out var normal, out var right, out var up, out int width, out int height, out int depth);
            Vector3 cells = normal * (depth * 0.5f)
                + right * (onFace.x - (width - 1) * 0.5f)
                + up * (onFace.y - (height - 1) * 0.5f);
            return cells * GridCell.Size;
        }

        /// True when a port's place lies on its face.
        public bool OnFace(GridDirection local, Vector2Int onFace)
        {
            Frame(local, out _, out _, out _, out int width, out int height, out _);
            return onFace.x >= 0 && onFace.x < width && onFace.y >= 0 && onFace.y < height;
        }

        /// A face as seen from outside: which way it points, right and up across it, and how many cells wide,
        /// high and deep the component is that way. All in the component's own axes.
        private void Frame(GridDirection local, out Vector3 normal, out Vector3 right, out Vector3 up,
            out int width, out int height, out int depth)
        {
            normal = local.Vector();
            up = Mathf.Abs(normal.y) > 0.5f ? Vector3.forward : Vector3.up;
            right = Vector3.Cross(up, -normal);
            Vector3 size = Tile.Size;
            width = Mathf.RoundToInt(Mathf.Abs(Vector3.Dot(right, size)));
            height = Mathf.RoundToInt(Mathf.Abs(Vector3.Dot(up, size)));
            depth = Mathf.RoundToInt(Mathf.Abs(Vector3.Dot(normal, size)));
        }

        /// A rotation snapped to the nearest quarter turns.
        private static Quaternion Snap(Quaternion r) =>
            Quaternion.LookRotation(Nearest(r * Vector3.forward).Vector(), Nearest(r * Vector3.up).Vector());

        /// True while the component is turned by whole quarter turns, which ports need.
        public bool IsSquare
        {
            get
            {
                var r = transform.rotation;
                return Vector3.Dot(r * Vector3.forward, Nearest(r * Vector3.forward).Vector()) > 0.999f
                    && Vector3.Dot(r * Vector3.up, Nearest(r * Vector3.up).Vector()) > 0.999f;
            }
        }

        private static GridDirection Nearest(Vector3 v)
        {
            var best = GridDirection.None;
            float bestDot = float.MinValue;
            foreach (var face in AllFaces)
            {
                float dot = Vector3.Dot(v, face.Vector());
                if (dot > bestDot) { bestDot = dot; best = face; }
            }
            return best;
        }

        /// Out where the face has any Out port, else In where it has any In port. A summary: a face can mix
        /// the two on different cells, and the grid asks cell by cell.
        public override FlowDirection GetFace(GridDirection face)
        {
            bool anyIn = false;
            foreach (var local in AllFaces)
            {
                if (WorldFace(local) != face) continue;
                foreach (var port in PortsOn(local))
                {
                    if (port.Flow == FlowDirection.Out) return FlowDirection.Out;
                    anyIn |= port.Flow == FlowDirection.In;
                }
            }
            return anyIn ? FlowDirection.In : FlowDirection.None;
        }

        public override IEnumerable<GridDirection> Outputs => FacesWith(FlowDirection.Out);

        public override IEnumerable<GridDirection> Inputs => FacesWith(FlowDirection.In);

        public override bool SendsAt(GridDirection face, Vector3Int cell) => FlowAt(face, cell) == FlowDirection.Out;

        public override bool AcceptsAt(GridDirection face, Vector3Int cell) => FlowAt(face, cell) == FlowDirection.In;

        private IEnumerable<GridDirection> FacesWith(FlowDirection flow)
        {
            foreach (var local in AllFaces)
                foreach (var port in PortsOn(local))
                    if (port.Flow == flow) { yield return WorldFace(local); break; }
        }

        private FlowDirection FlowAt(GridDirection face, Vector3Int cell)
        {
            // Rebuilt when the component has moved, as it does between slots.
            if (_byCell == null || transform.position != _indexedAt || transform.rotation != _indexedTurn) Index(false);
            return _byCell.TryGetValue((face, cell), out var flow) ? flow : FlowDirection.None;
        }

        /// Looks every port up by its cell. The first port on a cell face wins; a port off its face is skipped.
        private void Index(bool warn)
        {
            _byCell = new Dictionary<(GridDirection, Vector3Int), FlowDirection>();
            _indexedAt = transform.position;
            _indexedTurn = transform.rotation;
            if (warn && !IsSquare)
                Debug.LogWarning($"'{name}' is not turned by whole quarter turns; its ports are placed as if it were", this);
            foreach (var local in AllFaces)
            {
                var face = WorldFace(local);
                foreach (var port in PortsOn(local))
                {
                    if (port.Flow == FlowDirection.None) continue;
                    if (!OnFace(local, port.Cell))
                    {
                        if (warn) Debug.LogWarning($"'{name}': port {local} {port.Cell} is off its face; ignored", this);
                        continue;
                    }
                    var cell = CellOf(local, port.Cell);
                    if (_byCell.ContainsKey((face, cell)))
                    {
                        if (warn) Debug.LogWarning($"'{name}': two ports on {local} {port.Cell}; the first counts", this);
                        continue;
                    }
                    _byCell[(face, cell)] = port.Flow;
                }
            }
        }

        private void Awake()
        {
            int count = 0;
            foreach (var face in AllFaces) count += Packed(face)?.Length ?? 0;
            Debug.Log($"ComponentEdgeModule: '{name}' has {count} port(s)", this);
            Index(true);
            if (_portPrefab == null) return;

            // Placed against the model in the component's own axes, never from grid cells: a networked
            // component wakes wherever Fusion makes it, before it is seated, and cells worked out there
            // land half a cell off once it is moved into its slot.
            var taken = new HashSet<(GridDirection, Vector2Int)>();
            foreach (var local in AllFaces)
            {
                Frame(local, out var normal, out _, out var up, out _, out _, out _);
                foreach (var ported in PortsOn(local))
                {
                    if (ported.Flow == FlowDirection.None || !OnFace(local, ported.Cell)) continue;
                    if (!taken.Add((local, ported.Cell))) continue;
                    var port = Instantiate(_portPrefab, transform.position + transform.rotation * LocalPointOf(local, ported.Cell),
                        transform.rotation * Quaternion.LookRotation(normal, up));
                    port.transform.SetParent(transform, true);
                    port.name = $"Port {local} {ported.Flow}";
                    port.Show(ported.Flow);
                }
            }
        }

        private void OnValidate() => _byCell = null;

        private void OnDrawGizmos()
        {
            if (Application.isPlaying) return;
            foreach (var local in AllFaces)
            {
                Vector3 outward = WorldFace(local).Vector();
                foreach (var port in PortsOn(local))
                {
                    if (port.Flow == FlowDirection.None) continue;
                    Gizmos.color = !OnFace(local, port.Cell) ? Color.red
                        : port.Flow == FlowDirection.In ? new Color(0.3f, 0.55f, 1f) : new Color(0.3f, 0.9f, 0.4f);
                    Vector3 at = PointOf(local, port.Cell);
                    Gizmos.DrawSphere(at, GridCell.Size * 0.06f);
                    Gizmos.DrawLine(at, at + outward * (GridCell.Size * 0.2f));
                }
            }
        }
    }
}
