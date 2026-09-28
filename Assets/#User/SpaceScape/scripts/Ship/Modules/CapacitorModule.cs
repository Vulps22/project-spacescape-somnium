using SomniumSpace.Worlds.SpaceScape.Power;
using UnityEngine;

namespace SomniumSpace.Worlds.SpaceScape.Ship
{
    /// How much power a tile can hold, and how much it holds now.
    public sealed class CapacitorModule : Module, INetworkedState
    {
        [Tooltip("Joules it can hold. A load must fill it before it works; on a reactor it is the magnets' grip after power stops (joules / magnet watts = seconds).")]
        [SerializeField] private double _capacityJoules = 100.0;
        [Tooltip("Starts the session full rather than empty.")]
        [SerializeField] private bool _startFull;

        private Capacitor _capacitor;

        /// The sim object behind this module, built on first use so Awake order cannot matter.
        public Capacitor Capacitor =>
            _capacitor ??= new Capacitor(_capacityJoules, _startFull ? _capacityJoules : 0.0);

        public int StateCount => 1;
        public void WriteState(float[] to, int at) => to[at] = (float)Capacitor.Charge;
        public void ReadState(float[] from, int at) => Capacitor.Correct(from[at]);

        private void OnValidate()
        {
            if (_capacitor != null) _capacitor.Capacity = _capacityJoules;
        }
    }
}
