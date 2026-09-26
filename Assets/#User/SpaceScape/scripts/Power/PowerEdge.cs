namespace SpaceScape.Power
{
    /// A conduit. Carries whatever its upstream node passed on, and can be opened by a switch.
    public sealed class PowerEdge
    {
        public readonly PowerNode From;
        public readonly PowerNode To;

        /// Watts the conduit is carrying, as of the last completed tick.
        public double Flow { get; internal set; }

        /// Watts the current tick is assigning, not yet visible to anyone.
        internal double NextFlow;

        /// False when a switch or breaker has opened the line.
        public bool Enabled = true;

        /// How many cables' worth of the fork this conduit takes. One for a sound cable; a short
        /// behaves as several, which is the only way the grid and the eye disagree.
        public double Share = 1.0;

        internal PowerEdge(PowerNode from, PowerNode to)
        {
            From = from;
            To = to;
        }

        public override string ToString() =>
            $"{From.Name} -> {To.Name} : {Flow:0.##} W{(Enabled ? "" : " (open)")}" +
            (Share == 1.0 ? "" : $" [share {Share:0.##}]");
    }
}
