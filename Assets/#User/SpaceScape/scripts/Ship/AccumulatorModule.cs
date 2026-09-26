using SpaceScape.Power;
using UnityEngine;

namespace SpaceScape.Ship
{
    /// A module on the grid that banks power to work. Leave drain at the rated watts for anything
    /// that runs continuously, or set it to zero for something that holds its charge until fired.
    public sealed class AccumulatorModule : MonoBehaviour, IPowerSink
    {
        [SerializeField] private double _ratedWatts = 100.0;
        [SerializeField] private double _capacityJoules = 100.0;
        [SerializeField] private double _drainWatts = -1.0;   // below zero means match the rated watts

        private Accumulator _accumulator;

        /// The sim object behind this component, built on first use so Awake order cannot matter.
        public Accumulator Accumulator
        {
            get
            {
                if (_accumulator == null)
                    _accumulator = new Accumulator(_ratedWatts, _capacityJoules,
                        _drainWatts < 0.0 ? _ratedWatts : _drainWatts);
                _accumulator.Enabled = enabled;
                return _accumulator;
            }
        }

        public double WattsWanted => Accumulator.WattsWanted;

        public void Receive(double watts, double seconds) => Accumulator.Receive(watts, seconds);

        /// True while it holds enough charge to be doing its job.
        public bool Working => Accumulator.Working;

        /// How full it is, for anything that wants to show readiness or brightness.
        public double ChargeFraction => Accumulator.ChargeFraction;

        private void OnEnable() { var tile = GetComponent<GridNode>(); if (tile != null) tile.On = true; }

        private void OnDisable() { var tile = GetComponent<GridNode>(); if (tile != null) tile.On = false; }

        private void OnValidate()
        {
            if (_accumulator == null) return;
            _accumulator.DrawWatts = _ratedWatts;
            _accumulator.DrainWatts = _drainWatts < 0.0 ? _ratedWatts : _drainWatts;
            _accumulator.Capacity = _capacityJoules;
            _accumulator.Enabled = enabled;
        }
    }
}
