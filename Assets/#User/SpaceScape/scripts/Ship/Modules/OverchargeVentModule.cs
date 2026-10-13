using System;
using UnityEngine;

namespace SomniumSpace.Worlds.SpaceScape.Ship
{
    /// A battery that can be pushed past full (battery.md → S5): its hold is Over larger than its rated charge, so it
    /// takes 110% before it stops. At the top it vents the excess in one strike, dropping straight back to 100%, so
    /// overcharging is what the lightning is for. It makes no heat: the strike is where the excess goes.
    ///
    /// Only the client that decides (the master, or anyone with no network) vents. Each vent bumps a count shared with
    /// the battery's state, so every client, the master included, raises Vented once for it and shows the same
    /// strike; a late joiner takes the count as it finds it and replays nothing.
    [RequireComponent(typeof(CapacitorModule))]
    public sealed class OverchargeVentModule : Module, INetworkedState
    {
        [Tooltip("How far past its rated charge it holds before venting, as a share of the rated charge (0.1 = 110%).")]
        [SerializeField] private float _over = 0.1f;

        /// Raised once per vent on every client, with the vent's number (the same everywhere, so it can seed the look).
        public event Action<int> Vented;

        /// Whether this client vents. Set by ComponentNetwork: the master, or anyone with no network.
        public bool Decides { get; set; } = true;

        private CapacitorModule _hold;
        private int _vents;
        private bool _heard;

        private CapacitorModule Hold => _hold != null ? _hold : _hold = GetComponent<CapacitorModule>();

        /// The rated charge: what the readouts call full.
        public double Rated => Hold.Capacitor.Capacity / (1.0 + _over);

        /// The charge against the rated charge: 1 is full, up to 1 + Over just before it vents.
        public float Level => (float)(Hold.Capacitor.Charge / Rated);

        /// Where venting happens on Level.
        public float VentsAt => 1f + _over;

        public int StateCount => 1;
        public void WriteState(float[] to, int at) => to[at] = _vents;

        public void ReadState(float[] from, int at)
        {
            int vents = Mathf.RoundToInt(from[at]);
            bool first = !_heard;
            _heard = true;
            if (vents == _vents) return;
            _vents = vents;
            if (!first) Vented?.Invoke(_vents);
        }

        private void Update()
        {
            if (!Decides) return;
            var hold = Hold.Capacitor;
            if (hold.Charge < hold.Capacity * 0.9999) return;
            hold.Draw(hold.Charge - Rated);
            _vents = _vents == int.MaxValue ? 1 : _vents + 1;
            Vented?.Invoke(_vents);
        }
    }
}
