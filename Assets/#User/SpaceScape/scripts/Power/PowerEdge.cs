namespace SomniumSpace.Worlds.SpaceScape.Power
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

        /// False when a switch or breaker has opened the line. Changing it changes the links (PowerNode.LinkRevision).
        public bool Enabled
        {
            get => _enabled;
            set { if (_enabled == value) return; _enabled = value; PowerNode.LinksChanged(); }
        }

        /// How many cables' worth of the fork this conduit takes. One for a sound cable; a short
        /// behaves as several, which is the only way the grid and the eye disagree.
        public double Share
        {
            get => _share;
            set { if (_share == value) return; _share = value; PowerNode.LinksChanged(); }
        }

        private bool _enabled = true;
        private double _share = 1.0;

        internal PowerEdge(PowerNode from, PowerNode to)
        {
            From = from;
            To = to;
        }

        public override string ToString() =>
            $"{From.Name} -> {To.Name} : {Units.Watts(Flow)}{(Enabled ? "" : " (open)")}" +
            (Share == 1.0 ? "" : $" [share {Share:0.##}]");
    }
}
