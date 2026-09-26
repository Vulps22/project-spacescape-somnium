using System.Collections.Generic;
using SomniumSpace.Worlds.SpaceScape.Power;
using UnityEngine;

namespace SomniumSpace.Worlds.SpaceScape.Ship
{
    /// Builds the ship's grid from what is in the scene, then ticks it at a fixed rate.
    public sealed class PowerGrid : MonoBehaviour
    {
        [SerializeField] private float _ticksPerSecond = 20f;
        [SerializeField] private bool _logValidation = true;

        private PowerGraph _graph;
        private double _step;
        private double _carry;

        /// The grid itself, for anything that needs to read the whole picture.
        public PowerGraph Graph => _graph;

        private void Awake()
        {
            _graph = new PowerGraph();
            _step = 1.0 / Mathf.Max(1f, _ticksPerSecond);

            Build();

            if (!_logValidation) return;
            foreach (var problem in _graph.Validate())
                Debug.LogWarning($"PowerGrid: {problem}", this);
        }

        /// Turns every GridNode and Conduit in the scene into the sim's nodes and edges.
        private void Build()
        {
            var nodes = FindObjectsByType<GridNode>(FindObjectsSortMode.None);
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
            }

            // Every cell of every footprint, so two things cannot be built through each other.
            var byCell = new Dictionary<Vector3Int, GridNode>();
            foreach (var placed in nodes)
            {
                foreach (var cell in placed.Cells)
                {
                    if (byCell.TryGetValue(cell, out var clash))
                    {
                        if (clash != placed)
                            Debug.LogWarning(
                                $"'{placed.name}' stands in cell {cell}, which '{clash.name}' already occupies",
                                placed);
                        continue;
                    }
                    byCell.Add(cell, placed);
                }
            }

            // A face is a whole side of the footprint, so a run can meet a big machine anywhere
            // along it. Two things touching over several cells are still one join, not several.
            var joined = new HashSet<GridNode>();
            foreach (var placed in nodes)
            {
                foreach (var face in placed.Outputs)
                {
                    joined.Clear();
                    bool reachedAnything = false;
                    var socket = GridNode.Opposite(face);

                    foreach (var cell in placed.FaceCells(face))
                    {
                        var target = cell + face.Offset();
                        if (!byCell.TryGetValue(target, out var neighbour)) continue;
                        if (neighbour == placed) continue;
                        reachedAnything = true;

                        if (!joined.Add(neighbour)) continue;
                        if (neighbour.Node == null) continue;

                        if (neighbour.Edges == null || !neighbour.Edges.AcceptsFrom(socket))
                        {
                            Debug.LogWarning(
                                $"'{placed.name}' sends power {face} at '{neighbour.name}', which has no {socket} input",
                                placed);
                            continue;
                        }
                        _graph.Connect(placed.Node, neighbour.Node);
                    }

                    if (!reachedAnything)
                        Debug.LogWarning($"'{placed.name}' sends power {face} at nothing", placed);
                }
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

            _carry += Time.deltaTime;
            int guard = 0;
            while (_carry >= _step && guard++ < 8)
            {
                _graph.Tick(_step);
                _carry -= _step;
            }
            if (guard >= 8) _carry = 0.0;
        }
    }
}
