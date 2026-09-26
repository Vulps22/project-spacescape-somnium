namespace SpaceScape.Power
{
    /// A battery. One charge, a face on each side of it: it takes power off the grid and puts power
    /// back. Isolate it and it simply holds what it has, because a store is allowed to decline.
    public class EnergyStore : IPowerSink, IPowerSource
    {
        /// Most it will take in at once.
        public double ChargeWatts;

        /// Most it will put out at once.
        public double DischargeWatts;

        /// Joules it holds when full.
        public double Capacity;

        /// Joules it holds right now.
        public double Charge { get; private set; }

        /// False when the cell is isolated by hand.
        public bool Enabled = true;

        private double _offered;

        public EnergyStore(double chargeWatts, double dischargeWatts, double capacityJoules, double startingJoules = -1.0)
        {
            ChargeWatts = chargeWatts;
            DischargeWatts = dischargeWatts;
            Capacity = capacityJoules;
            Charge = startingJoules < 0.0 ? capacityJoules : startingJoules;
        }

        /// How much is left, for a gauge to read.
        public double ChargeFraction
        {
            get
            {
                if (Capacity <= 0.0) return 0.0;
                double f = Charge / Capacity;
                return f < 0.0 ? 0.0 : (f > 1.0 ? 1.0 : f);
            }
        }

        /// Takes power in only while there is room for it.
        public double WattsWanted => (!Enabled || Charge >= Capacity) ? 0.0 : ChargeWatts;

        /// Puts power out only when there is somewhere for it to go, which is what saves the cell
        /// when a switch beside it is opened. It cannot see past its own conduit: open a switch
        /// further down the run and this will happily cook everything between here and there.
        public double WattsOffered(PowerNode node)
        {
            _offered = (!Enabled || !Release(node) || Charge <= 0.0) ? 0.0 : ReleaseWatts;
            return _offered;
        }

        /// What this cell needs to see before it lets go of anything. Overridden by better cells.
        protected virtual bool Release(PowerNode node) => node.HasOutlet;

        /// How fast it lets go once it has decided to. Overridden by a cell with a dial on it.
        protected virtual double ReleaseWatts => DischargeWatts;

        /// Spends what it put out. The intake half arrives through Receive.
        public void ProvidePower(PowerNode node, double seconds)
        {
            Charge -= _offered * seconds;
            if (Charge < 0.0) Charge = 0.0;
        }

        /// Banks what the grid gave it.
        public void Receive(double watts, double seconds)
        {
            if (!Enabled) return;
            Charge += watts * seconds;
            if (Charge > Capacity) Charge = Capacity;
        }

        public override string ToString() =>
            $"{Charge:0.##}/{Capacity:0.##} J ({ChargeFraction:P0}), offering {_offered:0.##} W";
    }
}
