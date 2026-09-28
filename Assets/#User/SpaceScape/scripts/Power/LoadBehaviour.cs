namespace SomniumSpace.Worlds.SpaceScape.Power
{
    /// Anything that banks power to do its job. A bulb, a helm and a gun differ only in the numbers.
    /// Charge to the threshold, work while burning it off, stop at empty, charge again. Underfeed it
    /// and the cycle simply gets longer, which is the flicker.
    public sealed class LoadBehaviour : IPowerSink
    {
        /// What it banks into. Full is the threshold it must reach before it can start working.
        public readonly Capacitor Hold;

        /// Most it can pull at once, in watts, regardless of what the line offers.
        public double DrawWatts;

        /// Watts it burns while working. Zero means it holds its charge until something discharges it.
        public double DrainWatts;

        /// False when the module itself is switched off. It draws nothing and coasts down on what it holds.
        public bool Enabled = true;

        /// Condition of the thing this drives, used only for the chance of misfiring. Whether a
        /// wrecked component draws at all is the grid's business, not this class's.
        public Integrity Integrity;

        /// True on the tick it reached its threshold but failed to do anything with it.
        public bool Misfired { get; private set; }

        /// True while it holds enough to be doing its job.
        public bool Working { get; private set; }

        /// True only on the tick it started working, so a rising edge can be counted.
        public bool Started { get; private set; }

        /// True only on the tick it ran dry.
        public bool Stopped { get; private set; }

        /// Seconds since it last started working, which is the flicker a player reads across the room.
        public double SecondsSinceStarted { get; private set; }

        /// Draw and drain are the same for almost everything: a 50 W bulb pulls 50 W and burns 50 W.
        public LoadBehaviour(Capacitor hold, double ratedWatts) : this(hold, ratedWatts, ratedWatts) { }

        /// For something that banks and holds, like a gun waiting to be fired.
        public LoadBehaviour(Capacitor hold, double drawWatts, double drainWatts)
        {
            Hold = hold;
            DrawWatts = drawWatts;
            DrainWatts = drainWatts;
        }

        /// A load with a hold of its own, for anything not built from modules.
        public LoadBehaviour(double ratedWatts, double capacityJoules)
            : this(new Capacitor(capacityJoules), ratedWatts) { }

        /// A load with a hold of its own that burns at a different rate than it draws.
        public LoadBehaviour(double drawWatts, double capacityJoules, double drainWatts)
            : this(new Capacitor(capacityJoules), drawWatts, drainWatts) { }

        /// Joules stored right now.
        public double Charge => Hold.Charge;

        /// Joules it must hold before it can start working.
        public double Capacity => Hold.Capacity;

        /// How full it is, which is how brightly a lamp burns or how ready a gun is.
        public double ChargeFraction => Hold.Fraction;

        /// Nothing while it is off or already full, otherwise its full rated draw.
        public double WattsWanted => (!Enabled || Hold.IsFull) ? 0.0 : DrawWatts;

        /// Banks what arrived, burns what it burns while working, and crosses the thresholds either way.
        public void Receive(double watts, double seconds)
        {
            Started = false;
            Stopped = false;
            Misfired = false;
            SecondsSinceStarted += seconds;

            double net = (Enabled ? watts * seconds : 0.0) - (Working ? DrainWatts * seconds : 0.0);
            if (net > 0.0) Hold.Fill(net);
            else Hold.Draw(-net);

            if (Hold.IsFull)
            {
                if (!Working)
                {
                    // A damaged component can spend its charge and still do nothing.
                    if (Integrity != null && Integrity.FailsToWork())
                    {
                        Hold.Empty();
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
            else if (Hold.Charge <= 0.0)
            {
                if (Working)
                {
                    Working = false;
                    Stopped = true;
                }
            }
        }

        /// Sets whether it is working, for the network to bring a copy into line with the master's.
        public void CorrectWorking(bool working) => Working = working;

        /// Spends the whole charge at once, for a gun going off.
        public void Discharge()
        {
            Hold.Empty();
            if (!Working) return;
            Working = false;
            Stopped = true;
        }

        public override string ToString() =>
            $"{Hold}, {(Working ? "working" : "dark")}, wants {WattsWanted:0.##} W{(Enabled ? "" : " (off)")}";
    }
}
