namespace SpaceScape.Power
{
    /// A reactor core. Knows nothing about the grid: it burns fuel and makes watts, and its rods are
    /// held up by magnets, so losing them drops the rods rather than letting it run away.
    public sealed class FissionCore : IPowerProducer
    {
        /// Watts one core makes with its rods fully withdrawn.
        public double OutputPerCore = 100.0;

        /// How many cores are installed. The scavenge and upgrade axis.
        public int Cores = 1;

        /// Watts of heat made per watt delivered. Roughly 15 for an RTG, 2 for fission, under 1 for
        /// fusion — so a better core is survivable, not just bigger.
        public double HeatRatio = 2.0;

        /// What it settles at with the rods fully out. Below full output it sits proportionally lower.
        public double WorkingCelsius = 400.0;

        /// Where the crew has asked the rods to be, from fully in to fully out. Only the crew writes it.
        public double TargetWithdrawal;

        /// True while something holds the rods up. Without it they fall, whatever the dial says.
        public bool Gripped = true;

        /// Where the rods actually are. Chases the target, slowly up and quickly down.
        public double Withdrawal { get; private set; }

        /// Rods are driven out by motors, so coming up is slow and deliberate.
        public double RaisePerSecond = 0.05;

        /// Rods fall under gravity, so going down is not.
        public double DropPerSecond = 0.5;

        /// Seconds of running left at full output. Stand-in until there is a rack to load.
        public double FuelSeconds = 3600.0;

        /// Watts it would make with the rods fully out.
        public double MaxOutput => OutputPerCore * (Cores < 0 ? 0 : Cores);

        public double WattsProduced => FuelSeconds <= 0.0 ? 0.0 : MaxOutput * Clamp(Withdrawal);

        /// Watts of heat it is making, which is a consequence of output and not a fault.
        public double HeatWatts => WattsProduced * HeatRatio;

        /// The temperature it should be sitting at right now, given how hard it is working.
        public double BaselineCelsius
        {
            get
            {
                double max = MaxOutput;
                if (max <= 0.0) return PowerGraph.AmbientCelsius;
                double load = WattsProduced / max;
                return PowerGraph.AmbientCelsius + load * (WorkingCelsius - PowerGraph.AmbientCelsius);
            }
        }

        /// True while it is making anything at all.
        public bool Running => WattsProduced > 0.0;

        /// Turns the dial to zero, for the crew's big red button.
        public void Scram() => TargetWithdrawal = 0.0;

        /// Burns fuel and lets the rods travel toward where they have been asked to be.
        public void ProducePower(double seconds)
        {
            double max = MaxOutput;
            if (max > 0.0 && FuelSeconds > 0.0)
            {
                FuelSeconds -= Clamp(Withdrawal) * seconds;
                if (FuelSeconds < 0.0) FuelSeconds = 0.0;
            }

            double target = Gripped ? Clamp(TargetWithdrawal) : 0.0;
            if (Withdrawal < target)
            {
                Withdrawal += RaisePerSecond * seconds;
                if (Withdrawal > target) Withdrawal = target;
            }
            else if (Withdrawal > target)
            {
                Withdrawal -= DropPerSecond * seconds;
                if (Withdrawal < target) Withdrawal = target;
            }
        }

        private static double Clamp(double v) => v < 0.0 ? 0.0 : (v > 1.0 ? 1.0 : v);

        public override string ToString() =>
            $"{WattsProduced:0} W, rods {Withdrawal:P0} of {Clamp(TargetWithdrawal):P0}{(Gripped ? "" : " NO GRIP")}, " +
            $"{BaselineCelsius:0} C nominal, fuel {FuelSeconds:0} s";
    }
}
