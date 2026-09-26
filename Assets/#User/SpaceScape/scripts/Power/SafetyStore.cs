namespace SpaceScape.Power
{
    /// A cell that will not push into a run ending in bare cable. It looks past its own conduit for
    /// a component, so opening a switch anywhere downstream stops it rather than cooking everything
    /// between the two. It does not care whether that component currently wants anything.
    public sealed class SafetyStore : EnergyStore
    {
        public SafetyStore(double chargeWatts, double dischargeWatts, double capacityJoules,
            double startingJoules = -1.0)
            : base(chargeWatts, dischargeWatts, capacityJoules, startingJoules) { }

        protected override bool Release(PowerNode node) => node.ReachesConsumer;
    }
}
