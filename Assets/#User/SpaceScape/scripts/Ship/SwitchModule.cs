using UnityEngine;

namespace SpaceScape.Ship
{
    /// A switch slotted into a tile. Opening it takes the whole tile out of the graph, so nothing can
    /// enter or leave — which is why where you fit one decides what it costs you. Beside a source the
    /// source simply stops; further along a run, the cable before it becomes a dead end and cooks.
    public sealed class SwitchModule : ConduitModule
    {
        private static readonly GridDirection[] None = new GridDirection[0];

        [SerializeField] private bool _closed = true;

        /// A switch opens no faces; it only decides whether this tile is on the grid at all.
        public override GridDirection[] ExtraOutputs => None;

        /// True while the tile is part of the grid.
        public bool Closed
        {
            get => _closed;
            set { _closed = value; Apply(); }
        }

        /// Flips it, for a hand or a test to call.
        public void Toggle() => Closed = !_closed;

        private void Awake() => Apply();

        /// Hands the switch position to the tile. The graph does the rest by skipping it.
        private void Apply()
        {
            var tile = Tile;
            if (tile != null) tile.On = _closed;
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            Apply();
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = _closed ? new Color(0.4f, 1f, 0.6f, 0.9f) : new Color(1f, 0.3f, 0.2f, 0.9f);
            Gizmos.DrawWireCube(transform.position, Vector3.one * 0.22f);
        }
    }
}
