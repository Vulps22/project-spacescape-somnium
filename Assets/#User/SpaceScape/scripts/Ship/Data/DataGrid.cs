using System.Collections.Generic;
using SomniumSpace.Worlds.SpaceScape.Power;
using UnityEngine;

namespace SomniumSpace.Worlds.SpaceScape.Ship
{
    /// Which tiles share a data network, and what is published on each (data.md). Data travels along conduits,
    /// both ways, whatever way power goes: two neighbouring conduits are joined when each has a segment on the
    /// face between them, and a component when a conduit's segment is on its data port. A conduit that is off
    /// (an open switch) or has blown parts the network there.
    ///
    /// Worked out again only when something that joins or parts tiles has changed (PowerNode.LinkRevision for
    /// switches, pops and nodes coming and going; Invalidate for faces and slots), so a read is a lookup.
    /// Networking: nothing to send. Every client has the same conduits, switches, pops and slots, so each works
    /// out the same networks and reads its own copies of the values.
    public sealed class DataGrid
    {
        private static int _revision;

        private static readonly GridDirection[] AllFaces =
        {
            GridDirection.XPlus, GridDirection.XMinus,
            GridDirection.YPlus, GridDirection.YMinus,
            GridDirection.ZPlus, GridDirection.ZMinus,
        };

        private static readonly List<IDataSource> Sources = new List<IDataSource>();

        private readonly Dictionary<Vector3Int, GridNode> _byCell;
        private readonly Dictionary<PowerNode, GridNode> _tileOf;
        private readonly Dictionary<PowerNode, PowerNode> _parent = new Dictionary<PowerNode, PowerNode>();
        private readonly Dictionary<PowerNode, int> _network = new Dictionary<PowerNode, int>();
        private readonly List<List<PowerNode>> _members = new List<List<PowerNode>>();
        private readonly List<int> _components = new List<int>();   // per network: how many components it joins
        private int _builtRevision = -1;
        private int _builtLinks = -1;

        /// Says the faces of a tile or what sits in a slot have changed, so networks must be worked out again.
        public static void Invalidate() => _revision++;

        /// Reads the grid's own maps of cells and tiles, which it keeps up to date.
        public DataGrid(Dictionary<Vector3Int, GridNode> byCell, Dictionary<PowerNode, GridNode> tileOf)
        {
            _byCell = byCell;
            _tileOf = tileOf;
        }

        /// Everything of one kind published on the reader's network, one record per publisher, the reader's own
        /// included. Empty when the tile is in no grid. The list is reused by the next read of the same kind:
        /// copy it to keep it.
        public IReadOnlyList<T> Read<T>(GridNode tile) where T : DataObject
        {
            var found = Found<T>.List;
            found.Clear();
            var node = tile != null ? tile.Node : null;
            if (node == null) return found;
            Refresh();
            if (!_network.TryGetValue(node, out int id)) return found;

            foreach (var member in _members[id])
            {
                if (!_tileOf.TryGetValue(member, out var at) || at == null) continue;
                // A slot's modules are its component's.
                var holder = at.TryGetComponent<ComponentSlot>(out var slot)
                    ? (slot.Component != null ? slot.Component.gameObject : null)
                    : at.gameObject;
                if (holder == null) continue;
                holder.GetComponentsInChildren(Sources);   // a conduit's addon sits on a child
                foreach (var source in Sources)
                    if (source.GetData() is T data) found.Add(data);
            }
            return found;
        }

        /// True when the tile is on a network that joins two or more components: a data connection, which its
        /// conduits show.
        public bool Links(GridNode tile)
        {
            var node = tile != null ? tile.Node : null;
            if (node == null) return false;
            Refresh();
            return _network.TryGetValue(node, out int id) && _components[id] >= 2;
        }

        /// True when the tile is on a network that reaches at least one component's data port, whether or not
        /// anything is on the other end. For showing, while debugging, where data would go.
        public bool Reaches(GridNode tile)
        {
            var node = tile != null ? tile.Node : null;
            if (node == null) return false;
            Refresh();
            return _network.TryGetValue(node, out int id) && _components[id] >= 1;
        }

        private void Refresh()
        {
            if (_builtRevision == _revision && _builtLinks == PowerNode.LinkRevision) return;
            _builtRevision = _revision;
            _builtLinks = PowerNode.LinkRevision;

            // Every node its own network first, in a pass of its own: setting it while joining would cut loose a
            // node already joined to a neighbour earlier in the loop.
            _parent.Clear();
            foreach (var node in _tileOf.Keys) _parent[node] = node;
            foreach (var pair in _tileOf)
            {
                var node = pair.Key;
                var tile = pair.Value;
                if (tile == null || !tile.TryGetComponent<Conduit>(out _) || !node.CanReceivePower()) continue;
                var edges = tile.Edges;
                if (edges == null) continue;

                var here = tile.Coordinate;
                foreach (var face in AllFaces)
                {
                    if (edges.GetFace(face) == FlowDirection.None) continue;
                    var there = here + face.Offset();
                    if (!_byCell.TryGetValue(there, out var neighbour) || neighbour == tile || neighbour.Node == null) continue;
                    var socket = GridNode.Opposite(face);
                    var theirs = neighbour.Edges;
                    if (theirs == null) continue;
                    bool joins = neighbour.TryGetComponent<Conduit>(out _)
                        ? neighbour.Node.CanReceivePower() && theirs.GetFace(socket) != FlowDirection.None
                        : theirs.DataAt(socket, there);
                    if (joins) Union(node, neighbour.Node);
                }
            }

            _network.Clear();
            _members.Clear();
            _components.Clear();
            var ids = new Dictionary<PowerNode, int>();
            foreach (var node in _tileOf.Keys)
            {
                var root = Find(node);
                if (!ids.TryGetValue(root, out int id))
                {
                    id = _members.Count;
                    ids[root] = id;
                    _members.Add(new List<PowerNode>());
                    _components.Add(0);
                }
                _network[node] = id;
                _members[id].Add(node);
                // Only a data port joins a component to a network, so any component in one with others is linked.
                if (_tileOf.TryGetValue(node, out var at) && at != null && !at.TryGetComponent<Conduit>(out _)
                    && (!at.TryGetComponent<ComponentSlot>(out var slot) || slot.Component != null))
                    _components[id]++;
            }
        }

        private PowerNode Find(PowerNode node)
        {
            if (!_parent.TryGetValue(node, out var parent)) { _parent[node] = node; return node; }
            while (parent != node)
            {
                var up = _parent.TryGetValue(parent, out var next) ? next : parent;
                _parent[node] = up;   // halves the path
                node = parent;
                parent = up;
            }
            return node;
        }

        private void Union(PowerNode a, PowerNode b)
        {
            var ra = Find(a);
            var rb = Find(b);
            if (ra != rb) _parent[ra] = rb;
        }

        private static class Found<T>
        {
            public static readonly List<T> List = new List<T>();
        }
    }
}
