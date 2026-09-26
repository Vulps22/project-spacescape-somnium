namespace SomniumSpace.Worlds.SpaceScape.Power
{
    /// A reactor on the grid. Its hold is what keeps the rod magnets gripping: the core tops it up
    /// before anything reaches the output, and the grid fills it only when the core cannot. Lose both
    /// and the rods fall, so the fail-safe is just the grid working normally.
    public sealed class ReactorBehaviour : IPowerSink, IPowerSource
    {
        private readonly FissionCore _core;

        public ReactorBehaviour(FissionCore core, LoadBehaviour control, CoolantLoop coolant = null)
        {
            _core = core;
            Control = control;
            Coolant = coolant;
        }

        public FissionCore Core => _core;

        /// What holds the rods up. Its capacity is how long the magnets keep their grip once the
        /// power stops, so it is the window the crew has to fix a fault before the reactor drops.
        public LoadBehaviour Control;

        /// Optional. Without one the core simply sits at its working temperature and no higher.
        public CoolantLoop Coolant;

        /// Degrees the coolant carried away on the last tick, for a gauge to read.
        public double LastCooling { get; private set; }

        /// Watts the core is feeding its own magnets from inside the housing. House load comes off
        /// the top, before anything reaches the output port, so a running reactor holds its own rods
        /// and the starter supply is only needed to get it going.
        public double HouseLoad { get; private set; }

        /// True while the magnets have their grip.
        public bool RodsHeld => Control == null || Control.Working;

        /// True on the tick the magnets let go and the rods started falling.
        public bool Dropped { get; private set; }

        // --- the draw side: the magnets are an ordinary load ---

        /// Only what the house load could not cover. Once the core is running this is nothing, and
        /// the reactor stops drawing on the ship entirely.
        public double WattsWanted
        {
            get
            {
                if (Control == null) return 0.0;
                double short_ = Control.WattsWanted - HouseLoad;
                return short_ > 0.0 ? short_ : 0.0;
            }
        }

        /// The magnets get what the core gave them plus whatever the grid made up.
        public void Receive(double watts, double seconds)
        {
            if (Control != null) Control.Receive(watts + HouseLoad, seconds);
        }

        // --- the supply side ---

        /// Offers the lot, and tells the tile how hot it ought to be while doing it, so the core is
        /// not damaged for merely running.
        public double WattsOffered(PowerNode node, double seconds)
        {
            node.BaselineCelsius = _core.BaselineCelsius;

            // House load first. The magnets are inside the housing, so the core holds its own rods
            // before a watt reaches the output port; only the shortfall is asked of the ship.
            double produced = _core.WattsProduced;
            double wanted = Control != null ? Control.WattsWanted : 0.0;
            HouseLoad = wanted < produced ? wanted : produced;
            if (HouseLoad < 0.0) HouseLoad = 0.0;

            return produced - HouseLoad;
        }

        /// Lets the rods go if the magnets have lost their grip, burns fuel, then cools what it can.
        public void ProvidePower(PowerNode node, double seconds)
        {
            // Without grip the rods fall; with it they head back for the dial.
            Dropped = _core.Gripped && !RodsHeld && _core.Withdrawal > 0.0;
            _core.Gripped = RodsHeld;

            _core.ProducePower(seconds);
            node.BaselineCelsius = _core.BaselineCelsius;

            LastCooling = 0.0;
            if (Coolant == null) return;

            double shed = Coolant.Draw(seconds);
            if (shed <= 0.0) return;

            double floor = node.BaselineCelsius;
            double cooled = node.Celsius - shed;
            node.Celsius = cooled < floor ? floor : cooled;
            LastCooling = shed;
        }

        public override string ToString() =>
            $"{_core}{(RodsHeld ? "" : " [RODS DROPPED]")}" +
            (HouseLoad > 0.0 ? $" | house {HouseLoad:0.##} W" : " | house from grid") +
            (Coolant != null ? $" | {Coolant}" : "");
    }
}
