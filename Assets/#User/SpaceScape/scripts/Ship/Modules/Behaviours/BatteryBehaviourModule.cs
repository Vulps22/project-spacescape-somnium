using SomniumSpace.Worlds.SpaceScape.Power;
using TMPro;
using UnityEngine;

namespace SomniumSpace.Worlds.SpaceScape.Ship
{
    /// A cell. Charges its hold from its input and discharges the same hold out of its output, as far
    /// as its release rule allows. The plain cell's rule is only "have I got a cable".
    [RequireComponent(typeof(CapacitorModule))]
    public class BatteryBehaviourModule : BehaviourModule
    {
        [Tooltip("Most it takes in, in watts, while it has room. Anything more arriving is heat on the cell.")]
        [SerializeField] private double _chargeWatts = 100.0;
        [Tooltip("Most it puts out, in watts, when its release rule allows. Never more than it holds.")]
        [SerializeField] private double _dischargeWatts = 100.0;

        [Header("Readout")]
        [Tooltip("Debug text showing charge and temperature. Optional.")]
        [SerializeField] private TMP_Text _readout;
        [Tooltip("Seconds between readout updates.")]
        [SerializeField] private float _readoutInterval = 0.25f;

        private BatteryBehaviour _battery;
        private float _nextReadout;

        /// The sim object behind this module, built on first use so Awake order cannot matter.
        public BatteryBehaviour Battery
        {
            get
            {
                if (_battery == null) _battery = Create(Hold, _chargeWatts, _dischargeWatts);
                _battery.Enabled = enabled;
                Push(_battery);
                return _battery;
            }
        }

        public override IPowerSource Source => Battery;

        public override IPowerSink Sink => Battery;

        /// What the readout calls this kind of cell.
        protected virtual string Label => "Plain";

        /// Builds the sim object for this kind of cell.
        protected virtual BatteryBehaviour Create(Capacitor hold, double chargeWatts, double dischargeWatts) =>
            new BatteryBehaviour(hold, chargeWatts, dischargeWatts);

        /// Hands any extra Inspector values to the sim object.
        protected virtual void Push(BatteryBehaviour battery) { }

        private void Update()
        {
            if (_readout == null || !_readout.enabled) return;
            if (Time.time < _nextReadout) return;
            _nextReadout = Time.time + _readoutInterval;

            double celsius = Tile.Node != null ? Tile.Node.Celsius : 0.0;
            _readout.SetText($"Type: {Label}\nCharge: {Battery.Charge:0} J\nAvailable: {Battery.ChargeFraction:P0}{Extra}{IntegrityText}");
        }

        /// A line of its own for the readout, for a cell with something more to say.
        protected virtual string Extra => "";

        private void OnValidate()
        {
            if (_battery == null) return;
            _battery.ChargeWatts = _chargeWatts;
            _battery.DischargeWatts = _dischargeWatts;
            _battery.Enabled = enabled;
            Push(_battery);
        }
    }
}
