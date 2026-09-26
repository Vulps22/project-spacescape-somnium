namespace SpaceScape.Power
{
    /// A battery. It takes power off the grid into its hold and puts power back out of the same hold.
    /// Isolate it and it simply holds what it has, because a store is allowed to decline.
    public class BatteryBehaviour : IPowerSink, IPowerSource
    {
        /// What it charges into and discharges from.
        public readonly Capacitor Hold;

        /// Most it will take in at once.
        public double ChargeWatts;

        /// Most it will put out at once.
        public double DischargeWatts;

        /// False when the cell is isolated by hand.
        public bool Enabled = true;

        private double _offered;

        public BatteryBehaviour(Capacitor hold, double chargeWatts, double dischargeWatts)
        {
            Hold = hold;
            ChargeWatts = chargeWatts;
            DischargeWatts = dischargeWatts;
        }

        /// A cell with a hold of its own, starting full unless told otherwise.
        public BatteryBehaviour(double chargeWatts, double dischargeWatts, double capacityJoules, double startingJoules = -1.0)
            : this(new Capacitor(capacityJoules, startingJoules < 0.0 ? capacityJoules : startingJoules), chargeWatts, dischargeWatts) { }

        /// Joules it holds right now.
        public double Charge => Hold.Charge;

        /// Joules it holds when full.
        public double Capacity => Hold.Capacity;

        /// How much is left, for a gauge to read.
        public double ChargeFraction => Hold.Fraction;

        /// Takes power in only while there is room for it.
        public double WattsWanted => (!Enabled || Hold.IsFull) ? 0.0 : ChargeWatts;

        /// Puts power out only when there is somewhere for it to go, which is what saves the cell
        /// when a switch beside it is opened. It cannot see past its own conduit: open a switch
        /// further down the run and this will happily cook everything between here and there.
        public double WattsOffered(PowerNode node, double seconds)
        {
            _offered = (!Enabled || !Release(node) || Hold.Charge <= 0.0) ? 0.0 : ReleaseWatts;
            if (seconds > 0.0 && _offered * seconds > Hold.Charge) _offered = Hold.Charge / seconds;
            return _offered;
        }

        /// What this cell needs to see before it lets go of anything. Overridden by better cells.
        protected virtual bool Release(PowerNode node) => node.HasOutlet;

        /// How fast it lets go once it has decided to. Overridden by a cell with a dial on it.
        protected virtual double ReleaseWatts => DischargeWatts;

        /// Spends what it put out. The intake half arrives through Receive.
        public void ProvidePower(PowerNode node, double seconds) => Hold.Draw(_offered * seconds);

        /// Banks what the grid gave it.
        public void Receive(double watts, double seconds)
        {
            if (!Enabled) return;
            Hold.Fill(watts * seconds);
        }

        public override string ToString() => $"{Hold}, offering {_offered:0.##} W";
    }
}
