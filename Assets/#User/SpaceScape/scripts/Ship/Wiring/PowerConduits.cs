using System.Collections.Generic;
using UnityEngine;

namespace SomniumSpace.Worlds.SpaceScape.Ship
{
    /// The power conduits (conduits.md → Conduits V2): a tile on the grid for every installed power cell in ConduitCells,
    /// and none anywhere else. Each tile is the conduit prefab (GridNode, ConduitBehaviourModule, AddonModule, Conduit,
    /// NodeEdgeModule), made when its cell is installed and destroyed when the cell goes, so the addons, the voltmeter,
    /// the cable's look, pops and repair work on it as they always have.
    ///
    /// The cell is the truth and the tile follows it: its faces, its addon (which, how it sits, its state) and whether
    /// it is blown. What a hand does reaches the tile through the cell (a hologram's edit, a switch's press), and the
    /// master's grid blowing a conduit parks its outputs in the cell (ConduitCells.Blew).
    [RequireComponent(typeof(ConduitCells))]
    public sealed class PowerConduits : MonoBehaviour
    {
        private static readonly GridDirection[] AllFaces =
        {
            GridDirection.XPlus, GridDirection.XMinus,
            GridDirection.YPlus, GridDirection.YMinus,
            GridDirection.ZPlus, GridDirection.ZMinus,
        };

        [Tooltip("The conduit every installed power cell is built from.")]
        [SerializeField] private GameObject _conduitPrefab;

        private readonly Dictionary<Vector3Int, GridNode> _tiles = new Dictionary<Vector3Int, GridNode>();
        private readonly Dictionary<GridNode, ConduitAddon> _heard = new Dictionary<GridNode, ConduitAddon>();
        private ConduitCells _cells;
        private PowerGrid _grid;
        private Transform _root;

        /// The power conduits, or null.
        public static PowerConduits Current { get; private set; }

        /// How many power conduit tiles there are.
        public int Count => _tiles.Count;

        /// The power conduit tile in a cell, or null.
        public GridNode TileAt(Vector3Int cell) => _tiles.TryGetValue(cell, out var tile) ? tile : null;

        private void Awake()
        {
            Current = this;
            _cells = GetComponent<ConduitCells>();
            _cells.CellChanged += Sync;
            _cells.HeatOf = cell => _tiles.TryGetValue(cell, out var t) && t.Node != null ? t.Node.Celsius : 0.0;
            _cells.TakeHeat = (cell, celsius) => { if (_tiles.TryGetValue(cell, out var t) && t.Node != null) t.Node.Celsius = celsius; };
            _root = new GameObject("Power Conduits").transform;
            _root.SetParent(transform, false);
        }

        private void OnDestroy()
        {
            if (Current == this) Current = null;
            if (_cells != null) _cells.CellChanged -= Sync;
            if (_grid != null) _grid.Blown -= OnBlown;
        }

        private PowerGrid Grid
        {
            get
            {
                if (_grid != null) return _grid;
                _grid = PowerGrid.Current;
                if (_grid != null) _grid.Blown += OnBlown;
                return _grid;
            }
        }

        /// A conduit blew on this client's grid (only the master's rolls): its cell parks its outputs.
        private void OnBlown(GridNode tile)
        {
            if (tile != null) _cells.Blew(tile.Coordinate);
        }

        /// Makes a cell's tile match the cell: builds it, changes it, or takes it away.
        private void Sync(Vector3Int cell)
        {
            var grid = Grid;
            var c = _cells.Get(cell);
            _tiles.TryGetValue(cell, out var tile);

            if (c == null || c.Kind != ConduitCells.Kind.Power)
            {
                if (tile == null) return;
                _tiles.Remove(cell);
                Listen(tile, null);
                if (grid != null) grid.RemoveTile(tile);
                Destroy(tile.gameObject);
                return;
            }

            bool fresh = tile == null;
            if (fresh)
            {
                if (_conduitPrefab == null)
                {
                    Debug.LogWarning("PowerConduits: no conduit prefab set", this);
                    return;
                }
                var made = Instantiate(_conduitPrefab, GridCell.ToWorld(cell), Quaternion.identity, _root);
                tile = made.GetComponent<GridNode>();
                _tiles[cell] = tile;
            }

            bool rewire = false;
            var edges = tile.Edges;
            if (edges != null)
                foreach (var face in AllFaces)
                {
                    int bit = ConduitCells.Bit(face);
                    var flow = (c.Used & bit) == 0 ? FlowDirection.None : (c.Out & bit) != 0 ? FlowDirection.Out : FlowDirection.In;
                    if (edges.GetFace(face) == flow) continue;
                    edges.SetFace(face, flow);
                    rewire = true;
                }

            SyncAddon(tile, c);

            if (fresh)
            {
                if (grid != null) grid.AddTile(tile);
            }
            else if (rewire && grid != null) grid.Rewire(tile);

            // Blown or mended, as the cell says.
            if (grid != null && tile.Node != null && tile.Node.IsPopped != c.Popped)
            {
                if (c.Popped) grid.Graph.CorrectPopped(tile.Node, true);
                else grid.Repair(tile);
            }
        }

        /// Puts the cell's addon on the tile, sat and set as the cell says.
        private void SyncAddon(GridNode tile, ConduitCells.Cell c)
        {
            if (!tile.TryGetComponent<AddonModule>(out var module)) return;
            var prefab = _cells.AddonPrefab(c.Addon);
            if (module.Installed != prefab)
            {
                if (module.Addon != null) module.Remove();
                if (prefab != null) module.Install(prefab, c.Facing);
            }
            var addon = module.Addon;
            if (addon != null)
            {
                module.Arrange(c.Facing, c.Turns, c.Parked);
                if (addon.NetworkState != c.AddonState) addon.NetworkState = c.AddonState;
            }
            Listen(tile, addon);
        }

        /// Hears a hand work an addon here (a switch pressed), so it goes into the cell and on to the master.
        private void Listen(GridNode tile, ConduitAddon addon)
        {
            _heard.TryGetValue(tile, out var heard);
            if (heard == addon) return;
            if (heard != null) heard.Operated -= OnOperated;
            if (addon != null) { addon.Operated += OnOperated; _heard[tile] = addon; }
            else _heard.Remove(tile);
        }

        private void OnOperated(ConduitAddon addon)
        {
            var tile = addon != null ? addon.GetComponentInParent<GridNode>() : null;
            if (tile == null) return;
            _cells.EditByHand(ConduitCells.Edit.AddonState, tile.Coordinate, addon.NetworkState, 0);
        }
    }
}
