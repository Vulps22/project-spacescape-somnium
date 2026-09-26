namespace SpaceScape.Power
{
    /// Joules held on a tile. Owns the charge and its limits; knows nothing about why it is filled or drained.
    public sealed class Capacitor
    {
        /// Joules it holds when full.
        public double Capacity;

        /// Joules it holds right now.
        public double Charge { get; private set; }

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

        /// Banks joules and returns whatever did not fit.
        public double Fill(double joules)
        {
            if (joules <= 0.0) return 0.0;
            Charge += joules;
            if (Charge <= Capacity) return 0.0;
            double over = Charge - Capacity;
            Charge = Capacity;
            return over;
        }

        /// Takes joules out and returns how many it actually had.
        public double Draw(double joules)
        {
            if (joules <= 0.0) return 0.0;
            double got = joules < Charge ? joules : Charge;
            Charge -= got;
            if (Charge < 0.0) Charge = 0.0;
            return got;
        }

        /// Spends everything at once.
        public void Empty() => Charge = 0.0;

        public override string ToString() => $"{Charge:0.##}/{Capacity:0.##} J ({Fraction:P0})";
    }
}
