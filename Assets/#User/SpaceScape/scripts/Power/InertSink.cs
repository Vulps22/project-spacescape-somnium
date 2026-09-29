namespace SomniumSpace.Worlds.SpaceScape.Power
{
    /// A sink that wants nothing and does nothing with what it is given: an empty component slot. Having a
    /// sink at all is what stops the node forwarding power the way a bare conduit would.
    public sealed class InertSink : IPowerSink
    {
        public static readonly InertSink Instance = new InertSink();

        public double WattsWanted => 0.0;

        public void Receive(double watts, double seconds) { }
    }
}
