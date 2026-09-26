using SpaceScape.Power;
using TMPro;
using UnityEngine;

namespace SpaceScape.Ship
{
    /// A reactor. Its hold keeps the rod magnets gripping; the core tops it up before anything reaches
    /// the output, and the input fills it only when the core cannot, so cutting both drops the rods.
    [RequireComponent(typeof(CapacitorModule))]
    public sealed class ReactorBehaviourModule : BehaviourModule
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

        [Header("Coolant")]
        [SerializeField] private double _coolantLitres = 200.0;
        [SerializeField] private double _degreesPerLitre = 20.0;
        [SerializeField] private double _maxFlowLitresPerSecond = 2.0;
        [SerializeField] private double _flow;                     // nobody turns this down for you

        [Header("Readout")]
        [SerializeField] private TMP_Text _readout;
        [SerializeField] private float _readoutInterval = 0.25f;

        private ReactorBehaviour _reactor;
        private float _nextReadout;

        /// The sim object behind this module, built on first use so Awake order cannot matter.
        public ReactorBehaviour Reactor
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
                    var magnets = new LoadBehaviour(Hold, _controlWatts) { Integrity = Integrity };
                    var coolant = new CoolantLoop
                    {
                        Litres = _coolantLitres,
                        DegreesPerLitre = _degreesPerLitre,
                        MaxFlowLitresPerSecond = _maxFlowLitresPerSecond,
                    };
                    _reactor = new ReactorBehaviour(core, magnets, coolant);
                }
                Push();
                return _reactor;
            }
        }

        public override IPowerSource Source => Reactor;

        public override IPowerSink Sink => Reactor;

        /// Turns the dial to zero, for a hand or a big red button to call.
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
            _reactor.Control.DrawWatts = _controlWatts;
            _reactor.Control.DrainWatts = _controlWatts;
            _reactor.Coolant.Flow = _flow;
            _reactor.Coolant.MaxFlowLitresPerSecond = _maxFlowLitresPerSecond;
            _reactor.Coolant.DegreesPerLitre = _degreesPerLitre;
        }

        private void Update()
        {
            if (_readout == null || !_readout.enabled) return;
            if (Time.time < _nextReadout) return;
            _nextReadout = Time.time + _readoutInterval;

            var r = Reactor;
            double celsius = Tile.Node != null ? Tile.Node.Celsius : 0.0;

            _readout.SetText(
                $"{r.Core.WattsProduced:0} W\n" +
                $"rods {r.Core.Withdrawal:P0} of {_targetWithdrawal:P0}{(r.RodsHeld ? "" : "  NO GRIP")}\n" +
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
