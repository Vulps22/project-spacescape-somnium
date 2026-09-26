using SomniumSpace.Worlds.SpaceScape.Power;
using UnityEngine;

namespace SomniumSpace.Worlds.SpaceScape.Ship
{
    /// Anything that banks power to work: charge the hold full, work while draining it, stop at empty.
    /// Leave drain below zero to match the rated watts, or set it to zero to hold charge until fired.
    [RequireComponent(typeof(CapacitorModule))]
    public class LoadBehaviourModule : BehaviourModule
    {
        [Tooltip("Watts it draws while filling its hold, and burns while working.")]
        [SerializeField] private double _ratedWatts = 100.0;
        [Tooltip("Watts it burns while working. Below 0 means the same as Rated Watts; 0 means it holds its charge until fired.")]
        [SerializeField] private double _drainWatts = -1.0;

        private LoadBehaviour _load;

        /// The sim object behind this module, built on first use so Awake order cannot matter.
        public LoadBehaviour Load
        {
            get
            {
                if (_load == null) _load = new LoadBehaviour(Hold, _ratedWatts, Drain) { Integrity = Integrity };
                _load.Enabled = enabled;
                return _load;
            }
        }

        public override IPowerSource Source => null;

        public override IPowerSink Sink => Load;

        /// True while it holds enough charge to be doing its job.
        public bool Working => Load.Working;

        /// How full it is, for anything that shows readiness or brightness.
        public double ChargeFraction => Load.ChargeFraction;

        private double Drain => _drainWatts < 0.0 ? _ratedWatts : _drainWatts;

        protected virtual void OnValidate()
        {
            if (_load == null) return;
            _load.DrawWatts = _ratedWatts;
            _load.DrainWatts = Drain;
            _load.Enabled = enabled;
        }
    }
}
