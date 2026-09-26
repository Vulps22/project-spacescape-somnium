using SomniumSpace.Worlds.SpaceScape.Power;
using UnityEngine;

namespace SomniumSpace.Worlds.SpaceScape.Ship
{
    /// A cell that only releases while something is asking, at a rate the crew dials in.
    public sealed class SmartBatteryBehaviourModule : BatteryBehaviourModule
    {
        [Tooltip("Share of Discharge Watts the crew has dialled in, 0 to 1. It only releases while something on the grid is asking.")]
        [SerializeField, Range(0f, 1f)] private float _throttle = 1f;

        protected override string Label => "Smart";

        protected override string Extra => $"\n{_throttle:P0} dial";

        protected override BatteryBehaviour Create(Capacitor hold, double chargeWatts, double dischargeWatts) =>
            new SmartBatteryBehaviour(hold, chargeWatts, dischargeWatts);

        protected override void Push(BatteryBehaviour battery)
        {
            if (battery is SmartBatteryBehaviour smart) smart.Throttle = _throttle;
        }
    }
}
