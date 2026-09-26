namespace SpaceScape.Power
{
    /// A cell that only releases while something on the grid is actually asking for watts, and lets
    /// the crew cap how fast it does so. Because a full component asks for nothing, it stops on its
    /// own once the ship is topped up, and so never feeds the waste heat of an idle grid.
    public sealed class SmartBatteryBehaviour : BatteryBehaviour
    {
        /// Share of its discharge rate the crew has dialled in, from nothing to everything.
        public double Throttle = 1.0;

        public SmartBatteryBehaviour(Capacitor hold, double chargeWatts, double dischargeWatts)
            : base(hold, chargeWatts, dischargeWatts) { }

        public SmartBatteryBehaviour(double chargeWatts, double dischargeWatts, double capacityJoules,
            double startingJoules = -1.0)
            : base(chargeWatts, dischargeWatts, capacityJoules, startingJoules) { }

        /// Nothing worth waking up for unless something is asking, and the dial is off zero.
        protected override bool Release(PowerNode node) => node.ReachesDemand && Throttle > 0.0;

        protected override double ReleaseWatts => DischargeWatts * Clamped;

        /// The dial, held between nothing and everything.
        public double Clamped => Throttle < 0.0 ? 0.0 : (Throttle > 1.0 ? 1.0 : Throttle);
    }
}
