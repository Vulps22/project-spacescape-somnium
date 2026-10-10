using System;
using System.Collections.Generic;
using SomniumSpace.Worlds.SpaceScape.Player;
using UnityEngine;

namespace SomniumSpace.Worlds.SpaceScape.Ship
{
    /// Opens the conduit hologram on an item conduit (loading.md → Item conduit rules): the same hologram as a power
    /// conduit's, built by ConduitHolograms from the same parts, with no arrows. A hand in a cell an item conduit is
    /// installed in, or could be installed in, holds it open, and the same hand can work it: swing a segment to a
    /// face or park it in the middle, and put an addon in its addon slot or lift it out. Unparking a segment in a
    /// cell that could take one installs it; it starts with a segment on the face toward what points into it.
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
            if (_conduits == null || _hologram == null) return;
            int count = PlayerHands.Positions(_hands);

            var open = _hologram.IsOpen ? _hologram.Conduit : null;
            if (open != null && !OpenElsewhere(open.Coordinate))
            {
                if (_hologram.Dragging) return;
                if (_holder >= 0 && _holder < count && GridCell.ToCell(_hands[_holder]) == open.Coordinate
                    && _conduits.ConduitAt(open.Coordinate) == open) return;
            }

            _holder = -1;
            for (int i = 0; i < count; i++)
            {
                var cell = GridCell.ToCell(_hands[i]);
                if (OpenElsewhere(cell) || AccessPanel.Blocks(cell)) continue;
                var conduit = _conduits.ConduitAt(cell);
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

        /// The hologram was let go in a new shape: the cell's segments go on those faces, installing it if it was not.
        private void Commit(IConduit conduit, IReadOnlyList<GridDirection> faces, IReadOnlyList<bool> outward)
        {
            if (_conduits == null || conduit == null) return;
            _conduits.EditByHand(ItemConduits.Edit.Faces, conduit.Coordinate, ItemConduits.Mask(faces), 0);
            _hologram.Refresh();
        }

        /// Installs an addon item let go of in the open item conduit's addon slot, when that slot is empty and the
        /// addon is one item conduits take.
        private void Install(AddonItem item)
        {
            if (_conduits == null || _hologram == null || !_hologram.IsOpen || item == null || item.Installs == null) return;
            var conduit = _hologram.Conduit;
            var slot = _hologram.AddonSlot;
            float radius = _parts != null ? _parts.InstallRadius : 0.1f;
            if (slot == null || (item.transform.position - slot.position).sqrMagnitude > radius * radius) return;
            if (!_conduits.Installed(conduit.Coordinate) || conduit.Addon != null) return;
            int index = _conduits.AddonIndex(item.Installs);
            if (index < 0) return;
            int state = item.CarriesState ? item.State : item.Installs.NetworkState;
            if (!_conduits.EditByHand(ItemConduits.Edit.Addon, conduit.Coordinate, index + 1, state)) return;

            item.Retire();
            if (_parts.RemoveItem == null || !_parts.RemoveItem(item)) Destroy(item.gameObject);
            _hologram.Refresh();
        }
    }
}
