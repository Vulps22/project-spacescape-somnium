using UnityEngine;

namespace SomniumSpace.Worlds.SpaceScape.Ship
{
    /// Makes a conduit a switch. Open, the whole tile is out of the grid, so where you fit one decides
    /// what it costs: beside a source the source stops, further along the cable before it cooks.
    public sealed class SwitchAddon : ConduitAddon
    {
        [Tooltip("Ticked: part of the grid. Unticked: the whole tile is off, and whatever feeds it has nowhere to go.")]
        [SerializeField] private bool _closed = true;

        /// True while the tile is part of the grid.
        public bool Closed
        {
            get => _closed;
            set
            {
                if (_closed == value) return;
                _closed = value;
                RaiseChanged();
            }
        }

        public override bool Conducts => _closed;

        public override int NetworkState
        {
            get => _closed ? 1 : 0;
            set => Closed = value != 0;
        }

        /// Sets it by hand: takes effect here at once and is passed on to the master.
        public void Press(bool closed)
        {
            Closed = closed;
            RaiseOperated();
        }

        /// Flips it, for a hand or a test to call.
        public void Toggle() => Closed = !_closed;

        private void OnValidate() => RaiseChanged();
    }
}
