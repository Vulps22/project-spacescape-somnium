using SpaceScape.Power;

namespace SpaceScape.Ship
{
    /// A length of cable. Stores and makes nothing, so the grid carries whatever arrives straight on.
    public class ConduitBehaviourModule : BehaviourModule
    {
        public override IPowerSource Source => null;

        public override IPowerSink Sink => null;
    }
}
