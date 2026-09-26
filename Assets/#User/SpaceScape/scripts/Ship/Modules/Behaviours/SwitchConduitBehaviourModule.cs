using UnityEngine;

namespace SomniumSpace.Worlds.SpaceScape.Ship
{
    /// A conduit with a switch in it. Opening it takes the whole tile out of the grid, so where you fit
    /// one decides what it costs: beside a source the source stops, further along the cable before it cooks.
    public sealed class SwitchConduitBehaviourModule : ConduitBehaviourModule
    {
        [SerializeField] private bool _closed = true;

        /// True while the tile is part of the grid.
        public bool Closed
        {
            get => _closed;
            set { _closed = value; Apply(); }
        }

        /// Flips it, for a hand or a test to call.
        public void Toggle() => Closed = !_closed;

        protected override void OnEnable() => Apply();

        /// Hands the switch position to the tile. The graph does the rest by skipping it.
        private void Apply() => Tile.On = _closed && enabled;

        private void OnValidate() => Apply();
    }
}
