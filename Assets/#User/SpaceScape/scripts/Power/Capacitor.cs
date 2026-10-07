namespace SomniumSpace.Worlds.SpaceScape.Power
{
    /// Joules held on a tile. Owns the charge and its limits; knows nothing about why it is filled or drained.
    public sealed class Capacitor
    {
        /// Joules it holds when full.
        public double Capacity;

        /// Joules it holds right now.
        public double Charge { get; private set; }

        /// Watts it gained (positive) or lost (negative) over the last grid tick, from what was filled and
        /// drawn. A correction from the network is not counted: it is the copy catching up, not power moving.
        public double NetWatts { get; private set; }

        private double _moved;   // joules filled minus joules drawn since the last tick ended

        public Capacitor(double capacityJoules, double startingJoules = 0.0)
        {
            Capacity = capacityJoules;
            Charge = startingJoules < 0.0 ? 0.0 : (startingJoules > capacityJoules ? capacityJoules : startingJoules);
        }

        /// Joules of space left.
        public double Room => Charge >= Capacity ? 0.0 : Capacity - Charge;

        /// True when it has no space left.
        public bool IsFull => Charge >= Capacity;

        /// How full it is, from nothing to everything.
        public double Fraction
        {
            get
            {
                if (Capacity <= 0.0) return 0.0;
                double f = Charge / Capacity;
                return f < 0.0 ? 0.0 : (f > 1.0 ? 1.0 : f);
            }
        }

        /// Sets the charge outright, for the network to bring a copy into line with the master's.
        public void Correct(double joules) => Charge = joules < 0.0 ? 0.0 : (joules > Capacity ? Capacity : joules);

        /// Banks joules and returns whatever did not fit.
        public double Fill(double joules)
        {
            if (joules <= 0.0) return 0.0;
            Charge += joules;
            if (Charge <= Capacity) { _moved += joules; return 0.0; }
            double over = Charge - Capacity;
            Charge = Capacity;
            _moved += joules - over;
            return over;
        }

        /// Takes joules out and returns how many it actually had.
        public double Draw(double joules)
        {
            if (joules <= 0.0) return 0.0;
            double got = joules < Charge ? joules : Charge;
            Charge -= got;
            if (Charge < 0.0) Charge = 0.0;
            _moved -= got;
            return got;
        }

        /// Spends everything at once.
        public void Empty()
        {
            _moved -= Charge;
            Charge = 0.0;
        }

        /// Called by the grid once every tick has done its filling and drawing: turns what moved into watts.
        public void EndTick(double seconds)
        {
            NetWatts = seconds > 0.0 ? _moved / seconds : 0.0;
            _moved = 0.0;
        }

        public override string ToString() => $"{Charge:0.##}/{Capacity:0.##} J ({Fraction:P0})";
    }
}
