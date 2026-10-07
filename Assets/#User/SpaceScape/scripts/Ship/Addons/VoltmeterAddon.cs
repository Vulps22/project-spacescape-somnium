using TMPro;
using UnityEngine;

namespace SomniumSpace.Worlds.SpaceScape.Ship
{
    /// Shows what its conduit is carrying, in watts, scaled to fit three digits: 450 W, 1.2 kW, 85 MW.
    /// Changes nothing about the cable. The reading is worked out on every client from the same sim, so it
    /// has no state of its own to send.
    public sealed class VoltmeterAddon : ConduitAddon
    {
        /// Above this the meter gives up on numbers.
        public const double HolyShitWatts = 900e9;

        private static readonly string[] Units = { "W", "kW", "MW", "GW" };

        [Header("Readout")]
        [Tooltip("Text showing the number, up to three digits.")]
        [SerializeField] private TMP_Text _value;
        [Tooltip("Text showing the unit the number is in: W, kW, MW or GW.")]
        [SerializeField] private TMP_Text _unit;

        private GridNode _tile;
        private Readout _valueShown;
        private Readout _unitShown;

        private void Start()
        {
            if (_value != null)
            {
                _valueShown = new Readout(_value, () => Reading().value);
                _valueShown.Listen();
            }
            if (_unit != null)
            {
                _unitShown = new Readout(_unit, () => Reading().unit);
                _unitShown.Listen();
            }
        }

        private void OnDestroy()
        {
            _valueShown?.Stop();
            _unitShown?.Stop();
        }

        // Installed, moved or parked, it may now sit on a different conduit.
        private void OnTransformParentChanged() => _tile = null;

        private (string value, string unit) Reading()
        {
            if (_tile == null) _tile = GetComponentInParent<GridNode>();
            var node = _tile != null ? _tile.Node : null;
            return Format(node != null ? node.Inflow : 0.0);
        }

        /// Watts as the meter shows them: a number of at most three digits and its unit.
        public static (string value, string unit) Format(double watts)
        {
            if (watts > HolyShitWatts) return ("Holy\nShit", "");
            if (watts < 0.0) watts = 0.0;

            int unit = 0;
            while (unit < Units.Length - 1 && watts >= 999.5)
            {
                watts /= 1000.0;
                unit++;
            }
            // A scaled-up reading keeps one decimal while it is small: 1.2 kW, not 1 kW.
            string value = unit > 0 && watts < 9.95 ? watts.ToString("0.0") : watts.ToString("0");
            return (value, Units[unit]);
        }
    }
}
