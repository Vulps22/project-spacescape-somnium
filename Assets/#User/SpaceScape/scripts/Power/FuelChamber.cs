using System;

namespace SomniumSpace.Worlds.SpaceScape.Power
{
    /// One of a reactor's fuel chambers: a rod burning in it, and that rod's own position. The chamber knows only
    /// its rod; the core it sits in decides how much one chamber is worth, whether the magnets grip, and how fast
    /// rods travel (reactor.md → Chambers).
    public sealed class FuelChamber
    {
        /// Where the crew has asked this chamber's rods to be, from fully in (0) to fully out (1). Written by its
        /// own lever, and by the master lever, which writes every chamber.
        public double TargetWithdrawal;

        /// Where its rods actually are. Chases the target, slowly up and quickly down.
        public double Withdrawal { get; private set; }

        /// How much of the rod being burnt is left, from full (1) to spent or empty (0). Starts with one rod in.
        public double Fuel = 1.0;

        /// Asked for the next rod once the one being burnt is spent: true when one was there, and it is consumed
        /// at that moment (loading.md). Null means nothing to refuel from.
        public Func<bool> Refuel;

        /// True while it has a rod with anything left in it.
        public bool HasRod => Fuel > 0.0;

        /// Its share of one chamber's full output right now: how far out its rods are, or nothing without a rod.
        public double Load => HasRod ? Clamp(Withdrawal) : 0.0;

        /// Sets where the rods are, for the network to bring a copy into line with the master's.
        public void CorrectWithdrawal(double withdrawal) => Withdrawal = Clamp(withdrawal);

        /// Burns its rod, takes the next if it is spent, and moves its rods toward the target (toward zero without
        /// grip). The core passes what it shares: grip, rod speeds and how long a rod lasts.
        public void Advance(double seconds, bool gripped, double raisePerSecond, double dropPerSecond, double secondsPerRod)
        {
            if (Fuel > 0.0)
            {
                Fuel -= secondsPerRod > 0.0 ? Clamp(Withdrawal) * seconds / secondsPerRod : Fuel;
                if (Fuel < 0.0) Fuel = 0.0;
            }

            // Spent, so it takes the next rod if there is one, and is full again.
            if (Fuel <= 0.0 && Refuel != null && Refuel()) Fuel = 1.0;

            double target = gripped ? Clamp(TargetWithdrawal) : 0.0;
            if (Withdrawal < target)
            {
                Withdrawal += raisePerSecond * seconds;
                if (Withdrawal > target) Withdrawal = target;
            }
            else if (Withdrawal > target)
            {
                Withdrawal -= dropPerSecond * seconds;
                if (Withdrawal < target) Withdrawal = target;
            }
        }

        internal static double Clamp(double v) => v < 0.0 ? 0.0 : (v > 1.0 ? 1.0 : v);

        public override string ToString() => $"rods {Withdrawal:P0} of {Clamp(TargetWithdrawal):P0}, fuel {Fuel:P0}";
    }
}
