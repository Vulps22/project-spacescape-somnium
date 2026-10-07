using SomniumSpace.Worlds.SpaceScape.Power;
using TMPro;
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

        [Header("Readout")]
        [Tooltip("Text showing the charge out of the capacity, and the watts it is gaining or losing. Optional.")]
        [SerializeField] private TMP_Text _readout;

        private Capacitor _capacitor;
        private Readout _readoutShown;

        /// The sim object behind this module, built on first use so Awake order cannot matter.
        public Capacitor Capacitor =>
            _capacitor ??= new Capacitor(_capacityJoules, _startFull ? _capacityJoules : 0.0);

        public int StateCount => 1;
        public void WriteState(float[] to, int at) => to[at] = (float)Capacitor.Charge;

        public void ReadState(float[] from, int at) => Capacitor.Correct(from[at]);

        private void Start()
        {
            if (_readout == null) return;
            _readoutShown = new Readout(_readout, ReadoutText);
            _readoutShown.Listen();
        }

        private void OnDestroy() => _readoutShown?.Stop();

        private string ReadoutText() =>
            $"Charge: {Capacitor.Charge:0}/{Capacitor.Capacity:0} J\nNet: {Capacitor.NetWatts:+0;-0;0} W";

        private void OnValidate()
        {
            if (_capacitor != null) _capacitor.Capacity = _capacityJoules;
        }
    }
}
