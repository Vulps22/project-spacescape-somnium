using System;
using SomniumSpace.Worlds.SpaceScape.Power;
using TMPro;
using UnityEngine;

namespace SomniumSpace.Worlds.SpaceScape.Ship
{
    /// A reactor. Its hold keeps the rod magnets gripping; the core tops it up before anything reaches
    /// the output, and the input fills it only when the core cannot, so cutting both drops the rods.
    [RequireComponent(typeof(CapacitorModule))]
    public sealed class ReactorBehaviourModule : BehaviourModule, INetworkedState
    {
        [Header("Core")]
        [Tooltip("Watts one core makes with the rods fully out.")]
        [SerializeField] private double _outputPerCore = 100.0;
        [Tooltip("How many cores are installed. Full output is Output Per Core x Cores.")]
        [SerializeField] private int _cores = 1;
        [Tooltip("Heat per watt produced (about 15 RTG, 2 fission, under 1 fusion). Not used by the grid yet: changing it does nothing today.")]
        [SerializeField] private double _heatRatio = 2.0;          // ~15 RTG, ~2 fission, <1 fusion
        [Tooltip("Temperature it settles at with the rods fully out; lower output sits proportionally lower. It is never damaged for sitting at it.")]
        [SerializeField] private double _workingCelsius = 400.0;
        [Tooltip("Seconds of running at full output. It burns proportionally slower with the rods part way.")]
        [SerializeField] private double _fuelSeconds = 3600.0;

        [Header("Rods")]
        [Tooltip("The crew's dial: 0 is rods fully in (off), 1 fully out (full power). The rods head for it while the magnets grip.")]
        [SerializeField, Range(0f, 1f)] private float _targetWithdrawal;
        [Tooltip("How fast the rods come out, as a share of full travel per second. 0.05 takes 20 s end to end.")]
        [SerializeField] private double _raisePerSecond = 0.05;    // motors, slow
        [Tooltip("How fast the rods fall back in, as a share of full travel per second. 0.5 takes 2 s.")]
        [SerializeField] private double _dropPerSecond = 0.5;      // gravity, fast

        [Header("Control magnets")]
        [Tooltip("Watts the rod magnets need to keep their grip. A running core covers it; a cold one needs it from the input.")]
        [SerializeField] private double _controlWatts = 20.0;

        [Header("Coolant")]
        [Tooltip("Litres of coolant in the drum.")]
        [SerializeField] private double _coolantLitres = 200.0;
        [Tooltip("Degrees of heat each litre carries away as it boils off.")]
        [SerializeField] private double _degreesPerLitre = 20.0;
        [Tooltip("Pump ceiling, in litres per second. Asking for more does nothing.")]
        [SerializeField] private double _maxFlowLitresPerSecond = 2.0;
        [Tooltip("Litres per second the crew has dialled in. Nothing turns it down for you, so the drum can run dry.")]
        [SerializeField] private double _flow;                     // nobody turns this down for you

        [Header("Readout")]
        [Tooltip("Debug text showing output, rods, temperature, fuel and coolant. Optional.")]
        [SerializeField] private TMP_Text _readout;
        [Tooltip("Seconds between readout updates.")]
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

        /// The crew's dial, from rods fully in (0) to fully out (1).
        public float TargetWithdrawal
        {
            get => _targetWithdrawal;
            set
            {
                _targetWithdrawal = Mathf.Clamp01(value);
                if (_reactor != null) _reactor.Core.TargetWithdrawal = _targetWithdrawal;
            }
        }

        /// Raised when a hand on this client turns the dial, for the network to pass on to the master.
        public event Action<float> DialTurned;

        /// True while a hand on this client holds the dial. The network leaves the dial alone meanwhile,
        /// so an older value from the master cannot pull it out of the hand.
        public bool DialHeld { get; set; }

        /// Turns the dial by hand: takes effect here at once and is passed on to the master.
        public void TurnDial(float withdrawal)
        {
            TargetWithdrawal = withdrawal;
            DialTurned?.Invoke(_targetWithdrawal);
        }

        /// How far in the rods actually are, from fully out (0) to fully in (1).
        public double RodInsertion => 1.0 - Reactor.Core.Withdrawal;

        public override IPowerSource Source => Reactor;

        public override IPowerSink Sink => Reactor;

        /// Turns the dial to zero, for a hand or a big red button to call.
        public void Scram()
        {
            _targetWithdrawal = 0f;
            Reactor.Core.Scram();
        }

        /// The dial, the rods, fuel, coolant, the flow and whether the magnets grip.
        public int StateCount => 6;

        public void WriteState(float[] to, int at)
        {
            var r = Reactor;
            to[at] = _targetWithdrawal;
            to[at + 1] = (float)r.Core.Withdrawal;
            to[at + 2] = (float)r.Core.FuelSeconds;
            to[at + 3] = (float)r.Coolant.Litres;
            to[at + 4] = (float)_flow;
            to[at + 5] = r.Control.Working ? 1f : 0f;
        }

        public void ReadState(float[] from, int at)
        {
            var r = Reactor;
            if (!DialHeld) TargetWithdrawal = from[at];
            r.Core.CorrectWithdrawal(from[at + 1]);
            r.Core.FuelSeconds = from[at + 2];
            r.Coolant.Litres = from[at + 3];
            _flow = from[at + 4];
            r.Coolant.Flow = _flow;
            r.Control.CorrectWorking(from[at + 5] > 0.5f);
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
                $"{r.Coolant.Litres:0} L @ {r.Coolant.ActualFlow:0.#}/s{(r.Coolant.AtCeiling ? " MAX" : "")}{IntegrityText}");
        }

        private void OnValidate()
        {
            if (_reactor != null) Push();
        }
    }
}
