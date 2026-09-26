using SpaceScape.Power;
using UnityEngine;

namespace SpaceScape.Ship
{
    /// A lamp on the grid. It is lit while it holds charge, so the room is the readout.
    public sealed class LightModule : MonoBehaviour, IPowerSink
    {
        [SerializeField] private double _ratedWatts = 50.0;
        [SerializeField] private double _capacityJoules = 50.0;
        [SerializeField] private Light _light;
        [SerializeField] private float _maxIntensity = 1.6f;

        private Accumulator _accumulator;

        /// The sim object behind this component, built on first use so Awake order cannot matter.
        public Accumulator Accumulator
        {
            get
            {
                if (_accumulator == null) _accumulator = new Accumulator(_ratedWatts, _capacityJoules);
                _accumulator.Enabled = enabled;
                return _accumulator;
            }
        }

        public double WattsWanted => Accumulator.WattsWanted;

        /// Takes the power, then shows whatever charge is left as brightness.
        public void Receive(double watts, double seconds)
        {
            Accumulator.Receive(watts, seconds);
            if (_light == null) return;
            _light.intensity = Accumulator.Working
                ? _maxIntensity * (float)Accumulator.ChargeFraction
                : 0f;
        }

        private void OnEnable() { var tile = GetComponent<GridNode>(); if (tile != null) tile.On = true; }

        private void OnDisable() { var tile = GetComponent<GridNode>(); if (tile != null) tile.On = false; }

        private void OnValidate()
        {
            if (_light == null) _light = GetComponent<Light>();
            if (_accumulator == null) return;
            _accumulator.DrawWatts = _ratedWatts;
            _accumulator.DrainWatts = _ratedWatts;
            _accumulator.Capacity = _capacityJoules;
            _accumulator.Enabled = enabled;
        }
    }
}
