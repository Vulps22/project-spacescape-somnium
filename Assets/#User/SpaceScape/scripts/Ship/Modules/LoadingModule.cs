using System;
using TMPro;
using UnityEngine;

namespace SomniumSpace.Worlds.SpaceScape.Ship
{
    /// What a component's item port accepts, and the next item of it: one, or none (loading.md). That is all it
    /// knows. The behaviour keeps its own level and consumes the item when it accepts it, so consuming always
    /// means one less, never a share used up. A component taking several kinds carries one of these per kind.
    public sealed class LoadingModule : Module, INetworkedState
    {
        [Tooltip("An item of the kind this takes in, such as the Fuel Rod prefab. It takes every item of that item's type.")]
        [SerializeField] private LoadableItem _accepts;
        [Tooltip("Starts the session holding one, for trying the component out without loading one.")]
        [SerializeField] private bool _startsWithOne;

        [Header("Port")]
        [Tooltip("Takes items only through one item port, for a component with a supply per port (each of a reactor's chambers). Off: through any of the component's item ports.")]
        [SerializeField] private bool _onePort;
        [Tooltip("That port's face, in the component's own axes.")]
        [SerializeField] private GridDirection _portFace = GridDirection.YPlus;
        [Tooltip("That port's cell on the face, as ComponentEdgeModule counts it.")]
        [SerializeField] private Vector2Int _portCell;

        [Header("Readout")]
        [Tooltip("Text showing what it accepts and whether it holds the next one. Optional.")]
        [SerializeField] private TMP_Text _readout;

        private int _held = -1;
        private Readout _readoutShown;

        /// True when this takes items of type T, for a behaviour finding the module that holds its supply.
        public bool Takes<T>() where T : LoadableItem => _accepts is T;

        /// True when items through the item port on this face and cell (the component's own axes) come here.
        public bool FedFrom(GridDirection face, Vector2Int cell) => !_onePort || (face == _portFace && cell == _portCell);

        /// True when items through the item port in this grid cell of the component come here.
        public bool FedFrom(ComponentEdgeModule edges, Vector3Int cell) =>
            !_onePort || (edges != null && edges.CellOf(_portFace, _portCell) == cell);

        /// Binds it to one item port, for an editor tool building a component.
        public void BindToPort(GridDirection face, Vector2Int cell)
        {
            _onePort = true;
            _portFace = face;
            _portCell = cell;
        }

        /// True when this takes in this item: it is of the type this accepts.
        public bool Accepts(LoadableItem item) => item != null && _accepts != null && _accepts.GetType().IsInstanceOfType(item);

        /// True while it holds the next item.
        public bool HasItem
        {
            get
            {
                if (_held < 0) _held = _startsWithOne ? 1 : 0;
                return _held > 0;
            }
        }

        /// Takes in one item, when it holds none. True when it was taken.
        public bool Load()
        {
            if (HasItem) return false;
            _held = 1;
            return true;
        }

        /// Raised when an item port on this client loads one, for the network to pass on to the master.
        public event Action LoadedByHand;

        /// Takes in an item from an item port on this client: here at once, and passed on to the master.
        public bool LoadByHand(LoadableItem item)
        {
            if (!Accepts(item) || !Load()) return false;
            LoadedByHand?.Invoke();
            return true;
        }

        /// Gives up the item it holds, for the behaviour accepting it. True when there was one.
        public bool Consume()
        {
            if (!HasItem) return false;
            _held = 0;
            return true;
        }

        [ContextMenu("Load One")]
        private void LoadOne() => Debug.Log($"[SpaceScape] '{name}' {AcceptsName}: {(Load() ? "loaded one" : "already holds one")}", this);

        private string AcceptsName => _accepts != null ? _accepts.name : "nothing";

        private void Start()
        {
            if (_readout == null) return;
            _readoutShown = new Readout(_readout, ReadoutText);
            _readoutShown.Listen();
        }

        private void OnDestroy() => _readoutShown?.Stop();

        private string ReadoutText() => $"{AcceptsName}: next {(HasItem ? "in" : "none")}";

        public int StateCount => 1;

        public void WriteState(float[] to, int at) => to[at] = HasItem ? 1f : 0f;

        public void ReadState(float[] from, int at) => _held = from[at] > 0.5f ? 1 : 0;
    }
}
