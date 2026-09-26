using SpaceScape.Power;
using UnityEngine;

namespace SpaceScape.Ship
{
    /// A source with fixed output. Stands in until stateful sources exist.
    public sealed class ConstantSourceModule : MonoBehaviour, IPowerSource
    {
        [SerializeField] private double _watts = 1000.0;

        private ConstantSource _source;

        /// The sim object behind this component, built on first use so Awake order cannot matter.
        public ConstantSource Source
        {
            get
            {
                if (_source == null) _source = new ConstantSource(_watts);
                return _source;
            }
        }

        public double WattsOffered(PowerNode node) => Source.WattsOffered(node);

        public void ProvidePower(PowerNode node, double seconds) => Source.ProvidePower(node, seconds);

        private void OnEnable() { var tile = GetComponent<GridNode>(); if (tile != null) tile.On = true; }

        private void OnDisable() { var tile = GetComponent<GridNode>(); if (tile != null) tile.On = false; }

        private void OnValidate()
        {
            if (_source != null) _source.Watts = _watts;
        }
    }
}
