using SomniumSpace.Worlds.SpaceScape.Power;
using UnityEngine;

namespace SomniumSpace.Worlds.SpaceScape.Ship
{
    /// How much power a tile can hold, and how much it holds now.
    public sealed class CapacitorModule : Module
    {
        [SerializeField] private double _capacityJoules = 100.0;
        [SerializeField] private bool _startFull;

        private Capacitor _capacitor;

        /// The sim object behind this module, built on first use so Awake order cannot matter.
        public Capacitor Capacitor =>
            _capacitor ??= new Capacitor(_capacityJoules, _startFull ? _capacityJoules : 0.0);

        private void OnValidate()
        {
            if (_capacitor != null) _capacitor.Capacity = _capacityJoules;
        }
    }
}
