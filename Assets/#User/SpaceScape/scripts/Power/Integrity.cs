using System;

namespace SomniumSpace.Worlds.SpaceScape.Power
{
    /// How much punishment a component has left. A thermal failure damages it rather than destroying
    /// it outright, but a worn component starts refusing to do its job before it is gone.
    public sealed class Integrity
    {
        /// Condition when new.
        public double Max;

        /// Condition now. At zero the component is lost.
        public double Current { get; private set; }

        /// Below this share of Max the component starts failing to work.
        public double WornBelowFraction = 0.2;

        /// Chance of failing to work at zero condition. It scales up from nothing at the worn line.
        public double FailureChanceWhenSpent = 0.5;

        private readonly Random _rng;

        public Integrity(double max, int seed = 20260917)
        {
            Max = max;
            Current = max;
            _rng = new Random(seed);
        }

        /// Condition as a share of new.
        public double Fraction => Max <= 0.0 ? 0.0 : (Current <= 0.0 ? 0.0 : Current / Max);

        /// True once it is beyond saving.
        public bool IsDestroyed => Current <= 0.0;

        /// True once it is damaged enough to be unreliable.
        public bool IsWorn => !IsDestroyed && Fraction < WornBelowFraction;

        /// Takes a hit. Returns true if it survived it.
        public bool TakeDamage(double amount)
        {
            if (amount <= 0.0) return !IsDestroyed;
            Current -= amount;
            if (Current < 0.0) Current = 0.0;
            return !IsDestroyed;
        }

        /// Repairs it, for a spanner or a test to call.
        public void Restore(double amount)
        {
            Current += amount;
            if (Current > Max) Current = Max;
        }

        /// Sets the condition outright, for the network to bring a copy into line with the master's.
        public void Correct(double current) => Current = current < 0.0 ? 0.0 : (current > Max ? Max : current);

        /// The chance each try that it fails to do its job: 1 once destroyed, 0 while sound, rising from 0 at
        /// the worn line to Failure Chance When Spent at 0 condition.
        public double FailureChance
        {
            get
            {
                if (IsDestroyed) return 1.0;
                if (!IsWorn) return 0.0;
                double howWorn = 1.0 - Fraction / WornBelowFraction;   // 0 at the worn line, 1 at spent
                return FailureChanceWhenSpent * howWorn;
            }
        }

        /// Rolls whether it fails to do its job this time. Certain once spent, impossible while sound.
        public bool FailsToWork()
        {
            if (IsDestroyed) return true;
            if (!IsWorn) return false;
            return _rng.NextDouble() < FailureChance;
        }

        public override string ToString() =>
            $"{Current:0}/{Max:0} ({Fraction:P0}){(IsDestroyed ? " DESTROYED" : IsWorn ? " WORN" : "")}";
    }
}
