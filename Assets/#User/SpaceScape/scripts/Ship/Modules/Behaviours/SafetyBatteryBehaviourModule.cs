using SpaceScape.Power;

namespace SpaceScape.Ship
{
    /// A cell that will not push into a run ending in bare cable.
    public sealed class SafetyBatteryBehaviourModule : BatteryBehaviourModule
    {
        protected override string Label => "Safety";

        protected override BatteryBehaviour Create(Capacitor hold, double chargeWatts, double dischargeWatts) =>
            new SafetyBatteryBehaviour(hold, chargeWatts, dischargeWatts);
    }
}
