using System.Collections.Generic;
using SomniumSpace.Worlds.SpaceScape.Power;
using UnityEngine;

namespace SomniumSpace.Worlds.SpaceScape.Ship
{
    /// Builds the ship's grid from what is in the scene, then ticks it at a fixed rate.
    public sealed class PowerGrid : MonoBehaviour
    {
        [Tooltip("How many times a second the grid is simulated. Behaviour is per second, so this changes smoothness, not speed.")]
        [SerializeField] private float _ticksPerSecond = 20f;
        [Tooltip("Logs wiring problems (rings, unreachable loads, loose tiles) when the grid is built.")]
        [SerializeField] private bool _logValidation = true;

        private PowerGraph _graph;
        private readonly Dictionary<Vector3Int, GridNode> _byCell = new Dictionary<Vector3Int, GridNode>();
        private readonly Dictionary<PowerNode, GridNode> _tileOf = new Dictionary<PowerNode, GridNode>();
        private readonly List<PowerNode> _blown = new List<PowerNode>();
        private double _step;
        private double _carry;

        /// The grid itself, for anything that needs to read the whole picture.
        public PowerGraph Graph => _graph;

        /// While true the grid does not tick: for a client that has joined but not yet been told how the
        /// ship stands, which cannot simulate any of it until it has all of it.
        public bool Held { get; set; }

        private void Awake()
        {
            _graph = new PowerGraph();
            _step = 1.0 / Mathf.Max(1f, _ticksPerSecond);

            var layout = FindFirstObjectByType<ConduitLayout>();
            if (layout != null) layout.Spawn();
            Build();
            _graph.Popped += n => Report("Popped", n, "severed, carries nothing until it is repaired");
            _graph.Popped += n => _blown.Add(n);
            _graph.Damaged += n => Report("Damaged", n, $"condition {n.Integrity}");
            _graph.Lost += n => Report("Lost", n, "wrecked, still wired in and wasting what it gets");

            if (!_logValidation) return;
            foreach (var problem in _graph.Validate())
                Debug.LogWarning($"PowerGrid: {problem}", this);
        }

        /// Logs a thermal failure with what the tile was doing when it happened.
        private static void Report(string what, PowerNode node, string outcome) =>
            Debug.LogWarning($"[SpaceScape] {what}() '{node.Name}' — {outcome}; {node.Celsius:0} C " +
                             $"({node.HeatAboveBaseline:0} over), in {node.Inflow:0} W, wasting {node.Dumped:0} W");

        /// Turns every GridNode and Conduit in the scene into the sim's nodes and edges.
        private void Build()
        {
            var nodes = FindObjectsByType<GridNode>(FindObjectsSortMode.None);
            _tileOf.Clear();
            _blown.Clear();
            foreach (var placed in nodes)
            {
                var behaviours = placed.GetComponents<BehaviourModule>();
                if (behaviours.Length == 0)
                    Debug.LogWarning($"'{placed.name}' has no behaviour module, so it acts as bare cable", placed);
                else if (behaviours.Length > 1)
                    Debug.LogWarning($"'{placed.name}' has {behaviours.Length} behaviour modules; only the first counts", placed);

                var behaviour = behaviours.Length > 0 ? behaviours[0] : null;
                var source = behaviour != null ? behaviour.Source : null;
                var sink = behaviour != null ? behaviour.Sink : null;
                var node = source != null
                    ? _graph.AddSource(placed.name, source, sink)
                    : _graph.AddNode(placed.name, sink);
                if (placed.TryGetComponent<IntegrityModule>(out var integrity)) node.Integrity = integrity.Integrity;
                placed.Bind(node);
                _tileOf[node] = placed;
            }

            // Every cell of every footprint, so two things cannot be built through each other.
            _byCell.Clear();
            foreach (var placed in nodes)
            {
                foreach (var cell in placed.Cells)
                {
                    if (_byCell.TryGetValue(cell, out var clash))
                    {
                        if (clash != placed)
                            Debug.LogWarning(
                                $"'{placed.name}' stands in cell {cell}, which '{clash.name}' already occupies",
                                placed);
                        continue;
                    }
                    _byCell.Add(cell, placed);
                }
            }

            foreach (var placed in nodes) ConnectOutputs(placed, null, true);
        }

        /// Rewires one tile after its faces have changed: drops every conduit into and out of it, then joins
        /// it to its neighbours again, and them to it, from the faces as they stand now.
        public void Rewire(GridNode tile)
        {
            if (_graph == null || tile == null || tile.Node == null) return;

            var node = tile.Node;
            var edges = new List<PowerEdge>(node.OutgoingEdges);
            edges.AddRange(node.IncomingEdges);
            foreach (var edge in edges) _graph.Disconnect(edge);

            ConnectOutputs(tile, null, false);
            var neighbours = new HashSet<GridNode>();
            foreach (var face in AllFaces)
                foreach (var cell in tile.FaceCells(face))
                    if (_byCell.TryGetValue(cell + face.Offset(), out var neighbour) && neighbour != tile)
                        neighbours.Add(neighbour);
            foreach (var neighbour in neighbours) ConnectOutputs(neighbour, tile, false);
        }

        private static readonly GridDirection[] AllFaces =
        {
            GridDirection.XPlus, GridDirection.XMinus,
            GridDirection.YPlus, GridDirection.YMinus,
            GridDirection.ZPlus, GridDirection.ZMinus,
        };

        /// Joins a tile's outputs to whatever accepts them across each face, or only to one tile when given.
        /// A face is a whole side of the footprint, so a run can meet a big machine anywhere along it; two
        /// things touching over several cells are still one join, not several.
        private void ConnectOutputs(GridNode placed, GridNode onlyTo, bool warn)
        {
            if (placed.Node == null) return;
            var joined = new HashSet<GridNode>();
            foreach (var face in placed.Outputs)
            {
                joined.Clear();
                bool reachedAnything = false;
                var socket = GridNode.Opposite(face);

                foreach (var cell in placed.FaceCells(face))
                {
                    if (!_byCell.TryGetValue(cell + face.Offset(), out var neighbour)) continue;
                    if (neighbour == placed) continue;
                    reachedAnything = true;

                    if (onlyTo != null && neighbour != onlyTo) continue;
                    if (!joined.Add(neighbour) || neighbour.Node == null) continue;

                    if (neighbour.Edges == null || !neighbour.Edges.AcceptsFrom(socket))
                    {
                        if (warn)
                            Debug.LogWarning(
                                $"'{placed.name}' sends power {face} at '{neighbour.name}', which has no {socket} input",
                                placed);
                        continue;
                    }
                    _graph.Connect(placed.Node, neighbour.Node);
                }

                if (!reachedAnything && warn)
                    Debug.LogWarning($"'{placed.name}' sends power {face} at nothing", placed);
            }
        }

        private void Update()
        {
            // A domain reload during Play wipes the graph and every tile's binding, and Awake does not
            // run again. Without this the grid silently stops and every readout freezes mid-value.
            if (_graph == null)
            {
                Debug.LogWarning("PowerGrid: graph was lost (domain reload?) - rebuilding", this);
                Awake();
                if (_graph == null) return;
            }

            if (Held) return;

            _carry += Time.deltaTime;
            int guard = 0;
            while (_carry >= _step && guard++ < 8)
            {
                _graph.Tick(_step);
                _carry -= _step;
                ParkBlownOutputs();
            }
            if (guard >= 8) _carry = 0.0;
        }

        /// Parks every output segment of a conduit that has just blown, so it shows its inputs and nothing
        /// leaving: the visible sign there is no connection downstream. Done between ticks, never during one.
        private void ParkBlownOutputs()
        {
            if (_blown.Count == 0) return;
            foreach (var node in _blown)
            {
                if (!_tileOf.TryGetValue(node, out var tile) || tile == null) continue;
                if (!tile.TryGetComponent<ConduitBehaviourModule>(out _) || tile.Edges == null) continue;
                var outputs = new List<GridDirection>(tile.Edges.Outputs);
                foreach (var face in outputs) tile.Edges.SetFace(face, FlowDirection.None);
                Rewire(tile);
            }
            _blown.Clear();
        }

        /// Mends a tile that has blown, so it carries power again once it has an output.
        public void Repair(GridNode tile)
        {
            if (_graph != null && tile != null && tile.Node != null) _graph.Repair(tile.Node);
        }
    }
}
