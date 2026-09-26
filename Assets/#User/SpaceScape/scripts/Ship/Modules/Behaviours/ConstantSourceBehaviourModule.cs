using SpaceScape.Power;
using UnityEngine;

namespace SpaceScape.Ship
{
    /// A source with fixed output. Stands in until stateful sources exist.
    public sealed class ConstantSourceBehaviourModule : BehaviourModule
    {
        [SerializeField] private double _watts = 1000.0;

        private ConstantSourceBehaviour _source;

        /// The sim object behind this module, built on first use so Awake order cannot matter.
        public ConstantSourceBehaviour Constant => _source ??= new ConstantSourceBehaviour(_watts);

        public override IPowerSource Source => Constant;

        public override IPowerSink Sink => null;

        private void OnValidate()
        {
            if (_source != null) _source.Watts = _watts;
        }
    }
}
