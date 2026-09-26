namespace SpaceScape.Power
{
    /// Anything that banks power to do its job. A bulb, a helm and a gun differ only in the numbers.
    /// Charge to the threshold, work while burning it off, stop at empty, charge again. Underfeed it
    /// and the cycle simply gets longer, which is the flicker.
    public sealed class Accumulator : IPowerSink
    {
        /// Most it can pull at once, in watts, regardless of what the line offers.
        public double DrawWatts;

        /// Joules it must hold before it can start working.
        public double Capacity;

        /// Watts it burns while working. Zero means it holds its charge until something discharges it.
        public double DrainWatts;

        /// False when the module itself is switched off. It draws nothing and coasts down on what it holds.
        public bool Enabled = true;

        /// Condition of the thing this drives, used only for the chance of misfiring. Whether a
        /// wrecked component draws at all is the grid's business, not this class's.
        public Durability Durability;

        /// True on the tick it reached its threshold but failed to do anything with it.
        public bool Misfired { get; private set; }

        /// Joules stored right now.
        public double Charge { get; private set; }

        /// True while it holds enough to be doing its job.
        public bool Working { get; private set; }

        /// True only on the tick it started working, so a rising edge can be counted.
        public bool Started { get; private set; }

        /// True only on the tick it ran dry.
        public bool Stopped { get; private set; }

        /// Seconds since it last started working, which is the flicker a player reads across the room.
        public double SecondsSinceStarted { get; private set; }

        /// Draw and drain are the same for almost everything: a 50 W bulb pulls 50 W and burns 50 W.
        public Accumulator(double ratedWatts, double capacityJoules)
        {
            DrawWatts = ratedWatts;
            DrainWatts = ratedWatts;
            Capacity = capacityJoules;
        }

        /// For something that banks and holds, like a gun waiting to be fired.
        public Accumulator(double drawWatts, double capacityJoules, double drainWatts)
        {
            DrawWatts = drawWatts;
            DrainWatts = drainWatts;
            Capacity = capacityJoules;
        }

        /// How full it is, which is how brightly a lamp burns or how ready a gun is.
        public double ChargeFraction
        {
            get
            {
                if (Capacity <= 0.0) return 0.0;
                double f = Charge / Capacity;
                return f < 0.0 ? 0.0 : (f > 1.0 ? 1.0 : f);
            }
        }

        /// Nothing while it is off or already full, otherwise its full rated draw.
        public double WattsWanted => (!Enabled || Charge >= Capacity) ? 0.0 : DrawWatts;

        /// Banks what arrived, burns what it burns while working, and crosses the thresholds either way.
        public void Receive(double watts, double seconds)
        {
            Started = false;
            Stopped = false;
            Misfired = false;
            SecondsSinceStarted += seconds;

            if (Enabled) Charge += watts * seconds;
            if (Working) Charge -= DrainWatts * seconds;

            if (Charge >= Capacity)
            {
                Charge = Capacity;
                if (!Working)
                {
                    // A damaged component can spend its charge and still do nothing.
                    if (Durability != null && Durability.FailsToWork())
                    {
                        Charge = 0.0;
                        Misfired = true;
                    }
                    else
                    {
                        Working = true;
                        Started = true;
                        SecondsSinceStarted = 0.0;
                    }
                }
            }
            else if (Charge <= 0.0)
            {
                Charge = 0.0;
                if (Working)
                {
                    Working = false;
                    Stopped = true;
                }
            }
        }

        /// Spends the whole charge at once, for a gun going off.
        public void Discharge()
        {
            Charge = 0.0;
            if (!Working) return;
            Working = false;
            Stopped = true;
        }

        public override string ToString() =>
            $"{Charge:0.##}/{Capacity:0.##} J ({ChargeFraction:P0}), " +
            $"{(Working ? "working" : "dark")}, wants {WattsWanted:0.##} W{(Enabled ? "" : " (off)")}";
    }
}
