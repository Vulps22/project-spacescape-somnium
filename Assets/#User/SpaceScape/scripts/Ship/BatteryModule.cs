using SpaceScape.Power;
using TMPro;
using UnityEngine;

namespace SpaceScape.Ship
{
    /// What a cell is willing to look at before it lets go of anything.
    public enum CellTier
    {
        /// Has it got a cable? Will happily cook a run that ends in nothing.
        Plain = 0,
        /// Does the run reach a component? Stops for a switch opened anywhere downstream.
        Safety,
        /// Is anything actually asking? Stops at a sated grid, and takes a throttle.
        Smart,
    }

    /// A cell on the grid. It takes power in and puts power back out, and how much it checks before
    /// discharging is what separates a scavenged cell from a good one.
    public sealed class BatteryModule : MonoBehaviour, IPowerSink, IPowerSource
    {
        [SerializeField] private CellTier _tier = CellTier.Plain;
        [SerializeField, Range(0f, 1f)] private float _throttle = 1f;
        [SerializeField] private double _chargeWatts = 100.0;
        [SerializeField] private double _dischargeWatts = 100.0;
        [SerializeField] private double _capacityJoules = 2000.0;
        [SerializeField] private bool _startFull = true;

        [Header("Readout")]
        [SerializeField] private TMP_Text _readout;
        [SerializeField] private float _readoutInterval = 0.25f;

        private EnergyStore _store;
        private float _nextReadout;

        /// The sim object behind this component, built on first use so Awake order cannot matter.
        public EnergyStore Store
        {
            get
            {
                if (_store == null)
                {
                    double start = _startFull ? -1.0 : 0.0;
                    switch (_tier)
                    {
                        case CellTier.Safety:
                            _store = new SafetyStore(_chargeWatts, _dischargeWatts, _capacityJoules, start);
                            break;
                        case CellTier.Smart:
                            _store = new SmartStore(_chargeWatts, _dischargeWatts, _capacityJoules, start);
                            break;
                        default:
                            _store = new EnergyStore(_chargeWatts, _dischargeWatts, _capacityJoules, start);
                            break;
                    }
                }
                _store.Enabled = enabled;
                if (_store is SmartStore smart) smart.Throttle = _throttle;
                return _store;
            }
        }

        public double WattsWanted => Store.WattsWanted;

        public double WattsOffered(PowerNode node) => Store.WattsOffered(node);

        public void Receive(double watts, double seconds) => Store.Receive(watts, seconds);

        public void ProvidePower(PowerNode node, double seconds) => Store.ProvidePower(node, seconds);

        private void Update()
        {
            if (_readout == null || !_readout.enabled) return;
            if (Time.time < _nextReadout) return;
            _nextReadout = Time.time + _readoutInterval;
            var tile = GetComponent<GridNode>();
            double celsius = tile != null && tile.Node != null ? tile.Node.Celsius : 0.0;
            string dial = _tier == CellTier.Smart ? $"\n{_throttle:P0} dial" : "";
            _readout.SetText($"{_tier}\n{Store.Charge:0} J\n{Store.ChargeFraction:P0}{dial}\n{celsius:0.0} C");
        }

        private void OnEnable() { var tile = GetComponent<GridNode>(); if (tile != null) tile.On = true; }

        private void OnDisable() { var tile = GetComponent<GridNode>(); if (tile != null) tile.On = false; }

        private void OnValidate()
        {
            if (_store == null) return;
            _store.ChargeWatts = _chargeWatts;
            _store.DischargeWatts = _dischargeWatts;
            _store.Capacity = _capacityJoules;
            _store.Enabled = enabled;
            if (_store is SmartStore s) s.Throttle = _throttle;
        }
    }
}
