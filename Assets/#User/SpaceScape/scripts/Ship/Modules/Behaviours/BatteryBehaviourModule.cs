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
        [Tooltip("Text showing what kind of cell it is, and any setting the crew has dialled in. Its charge is on the Capacitor Module's readout. Optional.")]
        [SerializeField] private TMP_Text _readout;

        private BatteryBehaviour _battery;
        private Readout _readoutShown;

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

        // Nothing it shows follows the grid: it changes only when a setting does. It still listens, so a
        // readout nobody could see when it last changed is drawn once someone can.
        private void Start()
        {
            if (_readout == null) return;
            _readoutShown = new Readout(_readout, () => $"{Label} cell{Extra}", () => false);
            _readoutShown.Listen();
        }

        private void OnDestroy() => _readoutShown?.Stop();

        /// A line of its own for the readout, for a cell with something more to say.
        protected virtual string Extra => "";

        private void OnValidate()
        {
            if (_battery == null) return;
            _battery.ChargeWatts = _chargeWatts;
            _battery.DischargeWatts = _dischargeWatts;
            _battery.Enabled = enabled;
            Push(_battery);
            _readoutShown?.Changed();
        }
    }
}
