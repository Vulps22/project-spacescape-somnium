using System;
using System.Collections.Generic;
using SomniumSpace.Worlds.SpaceScape.Power;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

namespace SomniumSpace.Worlds.SpaceScape.Ship
{
    /// A reactor. Its hold keeps the rod magnets gripping; the core tops it up before anything reaches
    /// the output, and the input fills it only when the core cannot, so cutting both drops the rods.
    ///
    /// It has one or more fuel chambers, each with its own rod, lever and fuel (reactor.md → Chambers). Its dials,
    /// for RodLevers: axis 0 is the master, which sets every chamber at once (SCRAM included); axis 1 to the
    /// chamber count is each chamber's own. Chamber i is fed by the i-th LoadingModule taking fuel rods.
    [RequireComponent(typeof(CapacitorModule))]
    public sealed class ReactorBehaviourModule : BehaviourModule, INetworkedState, ILeverDial
    {
        [Header("Core")]
        [Tooltip("Watts one chamber makes with its rods fully out.")]
        [FormerlySerializedAs("_outputPerCore")]
        [SerializeField] private double _outputPerChamber = 100.0;
        [Tooltip("How many fuel chambers it has, each with its own rod, lever and fuel. Full output is Output Per Chamber x Chambers. Chamber i is fed by the i-th LoadingModule taking fuel rods.")]
        [FormerlySerializedAs("_cores")]
        [SerializeField, Min(1)] private int _chambers = 1;
        [Tooltip("Heat per watt produced (about 15 RTG, 2 fission, under 1 fusion). Not used by the grid yet: changing it does nothing today.")]
        [SerializeField] private double _heatRatio = 2.0;          // ~15 RTG, ~2 fission, <1 fusion
        [Tooltip("Temperature it settles at with the rods fully out; lower output sits proportionally lower. It is never damaged for sitting at it.")]
        [SerializeField] private double _workingCelsius = 400.0;
        [Tooltip("Seconds one fuel rod runs at full output. It burns proportionally slower with the rods part way. Each chamber starts with one rod in; its next comes from its LoadingModule for fuel rods (loading.md).")]
        [FormerlySerializedAs("_fuelSeconds")]
        [SerializeField] private double _fuelSecondsPerRod = 3600.0;

        [Header("Rods")]
        [Tooltip("The master lever: 0 is rods fully in (off), 1 fully out (full power). Setting it sets every chamber; each chamber's rods head for their own lever while the magnets grip.")]
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
        [Tooltip("Text showing output, rods, core temperature against its working baseline, fuel left in the rod, and coolant. Optional.")]
        [SerializeField] private TMP_Text _readout;

        private ReactorBehaviour _reactor;
        private Readout _readoutShown;

        /// The sim object behind this module, built on first use so Awake order cannot matter.
        public ReactorBehaviour Reactor
        {
            get
            {
                if (_reactor == null)
                {
                    var core = new FissionCore
                    {
                        ChamberCount = _chambers,
                        OutputPerChamber = _outputPerChamber,
                        HeatRatio = _heatRatio,
                        WorkingCelsius = _workingCelsius,
                        FuelSecondsPerRod = _fuelSecondsPerRod,
                        RaisePerSecond = _raisePerSecond,
                        DropPerSecond = _dropPerSecond,
                    };
                    core.TargetWithdrawal = _targetWithdrawal;
                    for (int i = 0; i < core.ChamberCount; i++)
                    {
                        int chamber = i;
                        core.Chambers[i].Refuel = () => TakeRod(chamber);
                    }
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

        private LoadingModule[] _fuel;

        /// Each chamber's LoadingModule holding its next fuel rod, in chamber order: the component's LoadingModules
        /// taking fuel rods, in their order. A chamber past the last has none.
        public IReadOnlyList<LoadingModule> Fuel
        {
            get
            {
                if (_fuel != null) return _fuel;
                var found = new List<LoadingModule>();
                foreach (var loading in GetComponents<LoadingModule>())
                    if (loading.Takes<LoadableFuelRodItem>()) found.Add(loading);
                return _fuel = found.ToArray();
            }
        }

        /// A chamber is spent: takes its next rod, consuming it now, if there is one.
        private bool TakeRod(int chamber) => chamber < Fuel.Count && Fuel[chamber].Consume();

        /// How many fuel chambers it has.
        public int ChamberCount => Reactor.Core.ChamberCount;

        /// One chamber, for its visuals: its rod position, fuel and lever.
        public FuelChamber Chamber(int index) => Reactor.Core.Chambers[index];

        /// The master lever, from rods fully in (0) to fully out (1). Setting it sets every chamber.
        public float TargetWithdrawal
        {
            get => _targetWithdrawal;
            set
            {
                _targetWithdrawal = Mathf.Clamp01(value);
                if (_reactor != null) _reactor.Core.TargetWithdrawal = _targetWithdrawal;
            }
        }

        /// Raised when a hand on this client turns a dial (its axis and new value), for the network to pass on to the
        /// master.
        public event Action<int, float> DialTurned;

        // Which dials a hand on this client holds, by axis. The network leaves a held dial alone, so an older value
        // from the master cannot pull it out of the hand; the master held holds them all.
        private readonly HashSet<int> _held = new HashSet<int>();

        /// Where a dial is: axis 0 the master, 1 to ChamberCount a chamber's own lever.
        public float GetDial(int axis)
        {
            if (axis <= 0) return _targetWithdrawal;
            var chambers = Reactor.Core.Chambers;
            return axis <= chambers.Count ? (float)chambers[axis - 1].TargetWithdrawal : 0f;
        }

        /// Sets a dial: the master sets every chamber with it; a chamber's lever only that chamber.
        public void SetDial(int axis, float value)
        {
            value = Mathf.Clamp01(value);
            if (axis <= 0) { TargetWithdrawal = value; return; }
            var chambers = Reactor.Core.Chambers;
            if (axis <= chambers.Count) chambers[axis - 1].TargetWithdrawal = value;
        }

        /// Turns a dial by hand: takes effect here at once and is passed on to the master.
        public void TurnDial(int axis, float value)
        {
            SetDial(axis, value);
            DialTurned?.Invoke(axis, GetDial(axis));
        }

        private bool Held(int axis) => _held.Contains(0) || _held.Contains(axis);

        float ILeverDial.GetDial(int axis) => GetDial(axis);
        void ILeverDial.SetDialHeld(int axis, bool held)
        {
            if (held) _held.Add(axis);
            else _held.Remove(axis);
        }
        void ILeverDial.TurnDial(int axis, float value) => TurnDial(axis, value);

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

        /// The master lever, coolant, the flow and whether the magnets grip; then each chamber's lever, rods and
        /// fuel. The chamber count comes from the prefab, so every client has the same layout.
        public int StateCount => 4 + 3 * _chambers;

        public void WriteState(float[] to, int at)
        {
            var r = Reactor;
            to[at] = _targetWithdrawal;
            to[at + 1] = (float)r.Coolant.Litres;
            to[at + 2] = (float)_flow;
            to[at + 3] = r.Control.Working ? 1f : 0f;
            var chambers = r.Core.Chambers;
            for (int i = 0; i < _chambers; i++)
            {
                int c = at + 4 + 3 * i;
                bool there = i < chambers.Count;
                to[c] = there ? (float)chambers[i].TargetWithdrawal : 0f;
                to[c + 1] = there ? (float)chambers[i].Withdrawal : 0f;
                to[c + 2] = there ? (float)chambers[i].Fuel : 0f;
            }
        }

        public void ReadState(float[] from, int at)
        {
            var r = Reactor;
            if (!Held(0))
            {
                _targetWithdrawal = Mathf.Clamp01(from[at]);
                r.Core.CorrectMaster(_targetWithdrawal);
            }
            r.Coolant.Litres = from[at + 1];
            _flow = from[at + 2];
            r.Coolant.Flow = _flow;
            r.Control.CorrectWorking(from[at + 3] > 0.5f);
            var chambers = r.Core.Chambers;
            for (int i = 0; i < _chambers && i < chambers.Count; i++)
            {
                int c = at + 4 + 3 * i;
                if (!Held(i + 1)) chambers[i].TargetWithdrawal = from[c];
                chambers[i].CorrectWithdrawal(from[c + 1]);
                chambers[i].Fuel = from[c + 2];
            }
        }

        /// Hands the Inspector's values to the sim. Flow is the one a crew actually touches here; the levers write
        /// their dials themselves, and the chamber count is fixed when the sim is built.
        private void Push()
        {
            // The master only when the Inspector moved it, or it would undo every chamber's own lever.
            if (_reactor.Core.TargetWithdrawal != _targetWithdrawal) _reactor.Core.TargetWithdrawal = _targetWithdrawal;
            _reactor.Core.OutputPerChamber = _outputPerChamber;
            _reactor.Core.FuelSecondsPerRod = _fuelSecondsPerRod;
            _reactor.Core.HeatRatio = _heatRatio;
            _reactor.Core.WorkingCelsius = _workingCelsius;
            _reactor.Control.DrawWatts = _controlWatts;
            _reactor.Control.DrainWatts = _controlWatts;
            _reactor.Coolant.Flow = _flow;
            _reactor.Coolant.MaxFlowLitresPerSecond = _maxFlowLitresPerSecond;
            _reactor.Coolant.DegreesPerLitre = _degreesPerLitre;
        }

        private void Start()
        {
            if (_readout == null) return;
            _readoutShown = new Readout(_readout, ReadoutText);
            _readoutShown.Listen();
        }

        private void OnDestroy() => _readoutShown?.Stop();

        private string ReadoutText()
        {
            var r = Reactor;
            double celsius = Tile.Node != null ? Tile.Node.Celsius : 0.0;

            // The text is left-aligned so the table's columns line up; the lines above it are centred by tag.
            return
                "<align=center>" +
                $"{r.Core.WattsProduced:0} W   master {_targetWithdrawal:P0}{(r.RodsHeld ? "" : "  NO GRIP")}\n" +
                $"{celsius:0} / {r.Core.BaselineCelsius:0} C\n" +
                $"{r.Coolant.Litres:0} L @ {r.Coolant.ActualFlow:0.#}/s{(r.Coolant.AtCeiling ? " MAX" : "")}" +
                "</align>\n" +
                ChamberTable(r.Core, celsius);
        }

        /// A row per thing, a column per chamber: fuel and rod position in percent, and each chamber's temperature
        /// (its own load's share of the working temperature, plus whatever the core has above its baseline). A dot
        /// where a chamber has no rod.
        private static string ChamberTable(FissionCore core, double celsius)
        {
            int count = core.ChamberCount;
            const float label = 26f;
            float width = (100f - label) / count;
            double above = celsius - core.BaselineCelsius;
            var table = new System.Text.StringBuilder();
            void Row(string name, System.Func<FuelChamber, int, string> cell)
            {
                table.Append(name);
                for (int i = 0; i < count; i++)
                    table.Append($"<pos={label + width * i:0.#}%>").Append(cell(core.Chambers[i], i));
                table.Append('\n');
            }
            Row("rod", (c, i) => $"{i + 1}");
            Row("fuel", (c, i) => c.HasRod ? $"{c.Fuel * 100.0:0}" : "\u00b7");
            Row("position", (c, i) => $"{c.Withdrawal * 100.0:0}");
            Row("temp", (c, i) => c.HasRod
                ? $"{PowerGraph.AmbientCelsius + c.Load * (core.WorkingCelsius - PowerGraph.AmbientCelsius) + above:0}"
                : "\u00b7");
            return table.ToString().TrimEnd('\n');
        }

        private void OnValidate()
        {
            if (_reactor != null) Push();
        }
    }
}
