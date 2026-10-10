using System;
using System.Collections.Generic;
using UnityEngine;

namespace SomniumSpace.Worlds.SpaceScape.Ship
{
    /// One conduit as its hologram sees it, whatever it carries: where it is, its segments and its addon. A power
    /// conduit (TileConduit) and an item conduit (ItemConduits) both answer it, so one hologram works them both
    /// (loading.md → Item conduit rules: an item conduit is a conduit).
    public interface IConduit
    {
        /// Its cell.
        Vector3Int Coordinate { get; }

        /// Its centre in the world.
        Vector3 Centre { get; }

        /// True when it takes up this cell.
        bool Occupies(Vector3Int cell);

        /// How many cells across it is at its narrowest, for the hologram's size.
        int Across { get; }

        /// How many segments it has: two, more with a junction.
        int Segments { get; }

        /// True when its segments have In and Out arrows (power); false when they only sit on faces (items).
        bool HasArrows { get; }

        /// The faces its segments are on, in order, and whether each is Out.
        void GetSegments(List<GridDirection> faces, List<bool> outward);

        /// The addon on it, or null.
        ConduitAddon Addon { get; }

        /// The addon's own state (a junction's extra segments), which goes out with its item.
        int AddonState { get; }

        GridDirection AddonFacing { get; }

        int AddonTurns { get; }

        bool AddonParked { get; }

        /// Takes its addon off, as a hand lifting the addon's item out of the hologram. True when there was one.
        bool RemoveAddon();

        /// The flow a segment newly put on this face should have to meet its neighbour: true for Out.
        bool PairsOutward(GridDirection face);
    }

    /// A power conduit, a GridNode with a ConduitBehaviourModule, as its hologram sees it.
    public sealed class TileConduit : IConduit
    {
        private readonly Func<Vector3Int, GridNode> _tileAt;

        public TileConduit(GridNode tile, Func<Vector3Int, GridNode> tileAt)
        {
            Tile = tile;
            _tileAt = tileAt;
        }

        public GridNode Tile { get; }

        public Vector3Int Coordinate => Tile.Coordinate;

        public Vector3 Centre => Tile.transform.position;

        public bool Occupies(Vector3Int cell) => Tile.Occupies(cell);

        public int Across
        {
            get
            {
                var size = Tile.Size;
                return Mathf.Max(1, Mathf.Min(size.x, Mathf.Min(size.y, size.z)));
            }
        }

        public int Segments => Tile.TryGetComponent<ConduitBehaviourModule>(out var conduit) ? conduit.Segments : ConduitHologram.PlainSegments;

        public bool HasArrows => true;

        public void GetSegments(List<GridDirection> faces, List<bool> outward)
        {
            var edges = Tile.Edges;
            foreach (var face in Tile.UsedFaces)
            {
                if (faces.Contains(face)) continue;
                faces.Add(face);
                outward.Add(edges != null && edges.GetFace(face) == FlowDirection.Out);
            }
        }

        private AddonModule Slot => Tile.TryGetComponent<AddonModule>(out var slot) ? slot : null;

        public ConduitAddon Addon => Slot != null ? Slot.Addon : null;

        public int AddonState => Addon != null ? Addon.NetworkState : 0;

        public GridDirection AddonFacing => Slot != null ? Slot.Facing : GridDirection.None;

        public int AddonTurns => Slot != null ? Slot.Turns : 0;

        public bool AddonParked => Slot != null && Slot.Parked;

        public bool RemoveAddon()
        {
            var slot = Slot;
            return slot != null && slot.Remove() != null;
        }

        public bool PairsOutward(GridDirection face)
        {
            var neighbour = _tileAt?.Invoke(Tile.Coordinate + face.Offset());
            var edges = neighbour != null ? neighbour.Edges : null;
            if (edges == null) return true;
            return edges.GetFace(GridNode.Opposite(face)) != FlowDirection.Out;
        }
    }
}
