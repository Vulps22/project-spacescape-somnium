namespace SomniumSpace.Worlds.SpaceScape.Power
{
    /// Something on a conduit that wants power. A node without one is a junction.
    public interface IPowerSink
    {
        /// Watts this sink would take right now, before it knows what is available.
        double WattsWanted { get; }

        /// Hands the sink what it actually got this tick.
        void Receive(double watts, double seconds);
    }
}
