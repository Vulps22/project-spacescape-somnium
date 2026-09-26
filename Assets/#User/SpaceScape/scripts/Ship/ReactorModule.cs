using SpaceScape.Power;
using TMPro;
using UnityEngine;

namespace SpaceScape.Ship
{
    /// A reactor on a tile. It draws power for its rod magnets like any other load and produces
    /// power like nothing else, so cutting its supply drops the rods with no special case anywhere.
    public sealed class ReactorModule : MonoBehaviour, IPowerSink, IPowerSource
    {
        [Header("Core")]
        [SerializeField] private double _outputPerCore = 100.0;
        [SerializeField] private int _cores = 1;
        [SerializeField] private double _heatRatio = 2.0;          // ~15 RTG, ~2 fission, <1 fusion
        [SerializeField] private double _workingCelsius = 400.0;
        [SerializeField] private double _fuelSeconds = 3600.0;

        [Header("Rods")]
        [SerializeField, Range(0f, 1f)] private float _targetWithdrawal;
        [SerializeField] private double _raisePerSecond = 0.05;    // motors, slow
        [SerializeField] private double _dropPerSecond = 0.5;      // gravity, fast

        [Header("Control magnets")]
        [SerializeField] private double _controlWatts = 20.0;
        [SerializeField] private double _magnetHoldJoules = 40.0;  // the window after supply is cut

        [Header("Coolant")]
        [SerializeField] private double _coolantLitres = 200.0;
        [SerializeField] private double _degreesPerLitre = 20.0;
        [SerializeField] private double _maxFlowLitresPerSecond = 2.0;
        [SerializeField] private double _flow;                     // nobody turns this down for you

        [Header("Readout")]
        [SerializeField] private TMP_Text _readout;
        [SerializeField] private float _readoutInterval = 0.25f;

        private Reactor _reactor;
        private float _nextReadout;

        /// The sim object behind this component, built on first use so Awake order cannot matter.
        public Reactor Reactor
        {
            get
            {
                if (_reactor == null)
                {
                    var core = new FissionCore
                    {
                        OutputPerCore = _outputPerCore,
                        Cores = _cores,
                        HeatRatio = _heatRatio,
                        WorkingCelsius = _workingCelsius,
                        FuelSeconds = _fuelSeconds,
                        RaisePerSecond = _raisePerSecond,
                        DropPerSecond = _dropPerSecond,
                    };
                    var control = new Accumulator(_controlWatts, _magnetHoldJoules);
                    var coolant = new CoolantLoop
                    {
                        Litres = _coolantLitres,
                        DegreesPerLitre = _degreesPerLitre,
                        MaxFlowLitresPerSecond = _maxFlowLitresPerSecond,
                    };
                    _reactor = new Reactor(core, control, coolant);
                }
                Push();
                return _reactor;
            }
        }

        public double WattsWanted => Reactor.WattsWanted;

        public void Receive(double watts, double seconds) => Reactor.Receive(watts, seconds);

        public double WattsOffered(PowerNode node) => Reactor.WattsOffered(node);

        public void ProvidePower(PowerNode node, double seconds) => Reactor.ProvidePower(node, seconds);

        /// Drops the rods, for a hand or a big red button to call.
        public void Scram()
        {
            _targetWithdrawal = 0f;
            Reactor.Core.Scram();
        }

        /// Hands the Inspector's values to the sim. Rods and flow are the two a crew actually touches.
        private void Push()
        {
            _reactor.Core.TargetWithdrawal = _targetWithdrawal;
            _reactor.Core.OutputPerCore = _outputPerCore;
            _reactor.Core.Cores = _cores;
            _reactor.Core.HeatRatio = _heatRatio;
            _reactor.Core.WorkingCelsius = _workingCelsius;
            _reactor.Coolant.Flow = _flow;
            _reactor.Coolant.MaxFlowLitresPerSecond = _maxFlowLitresPerSecond;
            _reactor.Coolant.DegreesPerLitre = _degreesPerLitre;
        }

        private void OnEnable() { var tile = GetComponent<GridNode>(); if (tile != null) tile.On = true; }

        private void OnDisable() { var tile = GetComponent<GridNode>(); if (tile != null) tile.On = false; }

        private void Update()
        {
            // The crew's dial can be zeroed by the reactor itself when the magnets let go, so the
            // Inspector has to follow the rods rather than fight them.
            if (_reactor != null) _targetWithdrawal = (float)_reactor.Core.TargetWithdrawal;

            if (_readout == null || !_readout.enabled) return;
            if (Time.time < _nextReadout) return;
            _nextReadout = Time.time + _readoutInterval;

            var r = Reactor;
            var tile = GetComponent<GridNode>();
            double celsius = tile != null && tile.Node != null ? tile.Node.Celsius : 0.0;

            _readout.SetText(
                $"{r.Core.WattsProduced:0} W\n" +
                $"rods {r.Core.Withdrawal:P0}{(r.RodsHeld ? "" : "  DROPPED")}\n" +
                $"{celsius:0} / {r.Core.BaselineCelsius:0} C\n" +
                $"fuel {r.Core.FuelSeconds:0} s\n" +
                $"{r.Coolant.Litres:0} L @ {r.Coolant.ActualFlow:0.#}/s{(r.Coolant.AtCeiling ? " MAX" : "")}");
        }

        private void OnValidate()
        {
            if (_reactor != null) Push();
        }
    }
}
