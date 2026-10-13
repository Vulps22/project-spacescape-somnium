using SomniumSpace.Worlds.SpaceScape.Power;
using TMPro;
using UnityEngine;

namespace SomniumSpace.Worlds.SpaceScape.Ship
{
    /// Debug only: a conduit addon that takes every watt reaching its conduit and destroys it. It asks for more than
    /// any grid can carry, so it draws all that arrives and passes nothing on; power a sink draws is not wasted, so the
    /// tile never heats, and it keeps nothing. Cables carrying power to it still warm as cables do. It shows how much energy
    /// it has swallowed since it was installed, counting up. Each client counts its own from its own sim, so nothing is
    /// sent; a late joiner's count starts when it joins.
    public sealed class BlackHoleAddon : ConduitAddon
    {
        [Header("Readout")]
        [Tooltip("Text showing the energy swallowed so far, up to three digits.")]
        [SerializeField] private TMP_Text _value;
        [Tooltip("Text showing the unit the number is in: J, kJ, MJ, GJ and up.")]
        [SerializeField] private TMP_Text _unit;

        private readonly Hole _hole = new Hole();
        private Readout _valueShown;
        private Readout _unitShown;

        public override IPowerSink Sink => _hole;

        private void Start()
        {
            if (_value != null)
            {
                _valueShown = new Readout(_value, () => Format(_hole.Joules).value);
                _valueShown.Listen();
            }
            if (_unit != null)
            {
                _unitShown = new Readout(_unit, () => Format(_hole.Joules).unit);
                _unitShown.Listen();
            }
        }

        private void OnDestroy()
        {
            _valueShown?.Stop();
            _unitShown?.Stop();
        }

        /// Energy as up to three digits and a unit, as the voltmeter shows watts: 1.2 MJ, 450 GJ.
        public static (string value, string unit) Format(double joules) => Units.Split(joules < 0.0 ? 0.0 : joules, "J");

        /// Wants more than any grid can carry, keeps nothing it gets, and counts it.
        private sealed class Hole : IPowerSink
        {
            /// Joules it has taken since it was made.
            public double Joules { get; private set; }

            public double WattsWanted => 1e18;

            public void Receive(double watts, double seconds) => Joules += watts * seconds;
        }
    }
}
