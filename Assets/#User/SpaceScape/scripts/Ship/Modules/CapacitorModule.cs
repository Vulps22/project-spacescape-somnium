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

        [Header("Debug")]
        [Tooltip("Play mode only: follows the held charge as a share of the capacity, and dragging it sets the charge. On a network, set it on the master: anyone else is corrected back to the master's.")]
        [Range(0f, 1f)] [SerializeField] private float _debugCharge;

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
            $"Charge: {Units.Joules(Capacitor.Charge)} / {Units.Joules(Capacitor.Capacity)}\nNet: {Units.Watts(Capacitor.NetWatts, plus: true)}";

#if UNITY_EDITOR
        // Keeps Debug Charge showing the charge, so the slider starts from where the charge is.
        private void Update()
        {
            if (_capacitor != null) _debugCharge = (float)_capacitor.Fraction;
        }
#endif

        private void OnValidate()
        {
            if (_capacitor == null) return;
            _capacitor.Capacity = _capacityJoules;
            // A drag of Debug Charge in play mode: set the charge to it. Anything else leaves it as it is.
            if (Application.isPlaying && Mathf.Abs(_debugCharge - (float)_capacitor.Fraction) > 0.0001f)
                _capacitor.Correct(_debugCharge * _capacitor.Capacity);
        }
    }
}
