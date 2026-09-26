namespace SomniumSpace.Worlds.SpaceScape.Power
{
    /// A source that offers the same output every tick.
    public sealed class ConstantSourceBehaviour : IPowerSource
    {
        /// Watts it puts out, adjustable but not modelled as changing on its own.
        public double Watts;

        public ConstantSourceBehaviour(double watts)
        {
            Watts = watts;
        }

        /// A fixed output behaves like a producer: it offers regardless of anywhere to put it.
        public double WattsOffered(PowerNode node, double seconds) => Watts < 0.0 ? 0.0 : Watts;

        /// Nothing to advance; the output never varies on its own.
        public void ProvidePower(PowerNode node, double seconds) { }

        public override string ToString() => $"{Watts:0.##} W constant";
    }
}
