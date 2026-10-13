using System;
using System.Collections.Generic;
using SomniumSpace.Worlds.SpaceScape.Player;
using UnityEngine;

namespace SomniumSpace.Worlds.SpaceScape.Ship
{
    /// Opens the conduit hologram on a cell that is not a power conduit's tile (conduits.md → Conduits V2): an installed
    /// item conduit, or an empty cell a hand could install a conduit of either kind in. The same hologram as a power
    /// conduit's, built by ConduitHolograms from the same parts; arrows for power, none for items. A hand in the cell
    /// holds it open, and the same hand can work it: swing a segment to a face or park it in the middle, and put an
    /// addon in an item conduit's addon slot or lift it out. An empty cell starts with a segment on the face toward
    /// what points into it; putting the other on a face installs it, and a power conduit installed so becomes a tile,
    /// which ConduitHolograms opens from then on.
    ///
    /// Which cell each player has open is told to everyone (ShipNetwork), as a conduit's hologram is, so a cell
    /// open elsewhere does not open here, and is boxed in the handles' material as a power conduit is.
    public sealed class ItemConduitHolograms : MonoBehaviour
    {
        private const int MaxHands = 8;

        private readonly Vector3[] _hands = new Vector3[MaxHands];
        private readonly Dictionary<Vector3Int, HashSet<string>> _openElsewhere = new Dictionary<Vector3Int, HashSet<string>>();
        private readonly Dictionary<Vector3Int, GameObject> _boxes = new Dictionary<Vector3Int, GameObject>();
        private ConduitHolograms _parts;
        private ConduitHologram _hologram;
        private ItemConduits _conduits;
        private ConduitCells _cells;
        private Vector3Int? _shown;
        private int _holder = -1;

        /// Raised when this client's hologram opens on a cell.
        public event Action<Vector3Int> Opened;

        /// Raised when this client's hologram closes on a cell.
        public event Action<Vector3Int> Closed;

        /// The cell this client has open, or null.
        public Vector3Int? Open => _shown;

        /// Records that a player has a cell open, or has closed it.
        public void SetOpenElsewhere(Vector3Int cell, string player, bool open)
        {
            if (string.IsNullOrEmpty(player)) return;
            if (!_openElsewhere.TryGetValue(cell, out var who)) _openElsewhere[cell] = who = new HashSet<string>();
            if (open) who.Add(player);
            else who.Remove(player);
            ShowBox(cell, who.Count > 0);
        }

        /// Forgets every cell a player had open, for when they leave.
        public void ClearOpenElsewhere(string player)
        {
            foreach (var pair in _openElsewhere)
                if (pair.Value.Remove(player)) ShowBox(pair.Key, pair.Value.Count > 0);
        }

        /// Boxes a cell while another player has it open, as a power conduit is boxed.
        private void ShowBox(Vector3Int cell, bool show)
        {
            if (!show)
            {
                if (_boxes.TryGetValue(cell, out var old) && old != null) Destroy(old);
                _boxes.Remove(cell);
                return;
            }
            if (_boxes.ContainsKey(cell)) return;
            var parts = _parts != null ? _parts : FindFirstObjectByType<ConduitHolograms>();
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = $"Open elsewhere (item conduit {cell})";
            Destroy(box.GetComponent<Collider>());
            if (parts != null && parts.OpenElsewhereMaterial != null) box.GetComponent<Renderer>().sharedMaterial = parts.OpenElsewhereMaterial;
            box.transform.SetParent(transform, false);
            box.transform.SetPositionAndRotation(GridCell.ToWorld(cell), Quaternion.identity);
            box.transform.localScale = Vector3.one * (GridCell.Size + 2f * (parts != null ? parts.OpenElsewherePadding : 0.01f));
            _boxes[cell] = box;
        }

        private bool OpenElsewhere(Vector3Int cell) => _openElsewhere.TryGetValue(cell, out var who) && who.Count > 0;

        private void Start()
        {
            _parts = FindFirstObjectByType<ConduitHolograms>();
            if (_parts != null) _hologram = _parts.MakeHologram(Commit, _ => { });
        }

        private void OnEnable() => AddonItem.Released += Install;

        private void OnDisable() => AddonItem.Released -= Install;

        private void Update()
        {
            if (_conduits == null)
            {
                _conduits = ItemConduits.Current;
                if (_conduits != null) _conduits.Changed += OnChanged;
            }
            if (_cells == null) _cells = ConduitCells.Current;
            if (_conduits == null || _cells == null || _hologram == null) return;
            int count = PlayerHands.Positions(_hands);

            var open = _hologram.IsOpen ? _hologram.Conduit : null;
            if (open != null && !OpenElsewhere(open.Coordinate))
            {
                if (_hologram.Dragging) return;
                if (_holder >= 0 && _holder < count && GridCell.ToCell(_hands[_holder]) == open.Coordinate
                    && ConduitAt(open.Coordinate) == open) return;
            }

            _holder = -1;
            for (int i = 0; i < count; i++)
            {
                var cell = GridCell.ToCell(_hands[i]);
                if (OpenElsewhere(cell) || AccessPanel.Blocks(cell)) continue;
                var conduit = ConduitAt(cell);
                if (conduit == null) continue;
                _holder = i;
                _hologram.Holder = i;
                _hologram.Show(conduit);
                Shown(cell);
                return;
            }
            _hologram.Holder = -1;
            _hologram.Close();
            Shown(null);
        }

        /// The conduit this hologram opens in a cell: an installed item conduit, or the parked conduit in an empty cell
        /// something points into; null for any other cell.
        private IConduit ConduitAt(Vector3Int cell) => _conduits.ConduitAt(cell) ?? _cells.PendingAt(cell);

        private void OnDestroy()
        {
            if (_conduits != null) _conduits.Changed -= OnChanged;
        }

        /// The open cell changed under the hologram (from the network, or an edit here): lays it out again, unless a
        /// hand is mid-drag.
        private void OnChanged()
        {
            if (_hologram != null && _hologram.IsOpen && !_hologram.Dragging) _hologram.Refresh();
        }

        private void Shown(Vector3Int? cell)
        {
            if (cell == _shown) return;
            if (_shown.HasValue) Closed?.Invoke(_shown.Value);
            _shown = cell;
            if (_conduits != null) _conduits.Reveal(cell);
            if (cell.HasValue) Opened?.Invoke(cell.Value);
        }

        /// The hologram was let go in a new shape: the cell's segments go on those faces, with their arrows for power,
        /// installing it if it was not.
        private void Commit(IConduit conduit, IReadOnlyList<GridDirection> faces, IReadOnlyList<bool> outward)
        {
            if (_cells == null || conduit == null) return;
            int outs = conduit.HasArrows ? ConduitCells.OutMask(faces, outward) : 0;
            _cells.EditByHand(ConduitCells.Edit.Faces, conduit.Coordinate, ConduitCells.Mask(faces), outs);
            _hologram.Refresh();
        }

        /// Installs an addon item let go of in the open conduit's addon slot, when that slot is empty: on an item conduit,
        /// only a junction; on an empty cell's parked conduit, any addon, installing the conduit with it.
        private void Install(AddonItem item)
        {
            if (_conduits == null || _hologram == null || !_hologram.IsOpen || item == null || item.Installs == null) return;
            var conduit = _hologram.Conduit;
            var slot = _hologram.AddonSlot;
            float radius = _parts != null ? _parts.InstallRadius : 0.1f;
            if (slot == null || (item.transform.position - slot.position).sqrMagnitude > radius * radius) return;
            if (conduit.Addon != null) return;
            bool parked = !_cells.Installed(conduit.Coordinate);
            if (!parked && !_conduits.Installed(conduit.Coordinate)) return;
            int id = parked ? _cells.AddonId(item.Installs) : _conduits.AddonId(item.Installs);
            if (id == 0) return;
            int state = item.CarriesState ? item.State : item.Installs.NetworkState;
            int facing = parked ? (int)ConduitHolograms.FaceToward(conduit.Centre, null) << 8 : 0;
            if (!_cells.EditByHand(ConduitCells.Edit.Addon, conduit.Coordinate, id, (state & 0xFF) | facing)) return;

            item.Retire();
            if (_parts.RemoveItem == null || !_parts.RemoveItem(item)) Destroy(item.gameObject);
            _hologram.Refresh();
        }
    }
}
