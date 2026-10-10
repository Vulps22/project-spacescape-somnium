using System;
using System.Collections.Generic;

namespace SomniumSpace.Worlds.SpaceScape.Power
{
    /// A reactor core: one or more fuel chambers, each burning its own rod at its own rod position (FuelChamber).
    /// Knows nothing about the grid: it makes watts, and its rods are held up by magnets shared by every chamber, so
    /// losing them drops every rod rather than letting it run away. To the grid it is one producer: output and heat
    /// are the chambers' sum, and its working temperature comes from their combined load (reactor.md → Chambers).
    public sealed class FissionCore : IPowerProducer
    {
        private readonly List<FuelChamber> _chambers = new List<FuelChamber> { new FuelChamber() };

        /// Watts one chamber makes with its rods fully withdrawn.
        public double OutputPerChamber = 100.0;

        /// Watts of heat made per watt delivered. Roughly 15 for an RTG, 2 for fission, under 1 for
        /// fusion — so a better core is survivable, not just bigger.
        public double HeatRatio = 2.0;

        /// What it settles at with every chamber's rods fully out. Below full output it sits proportionally lower.
        public double WorkingCelsius = 400.0;

        /// True while something holds the rods up. Without it every chamber's rods fall, whatever the levers say.
        public bool Gripped = true;

        /// Rods are driven out by motors, so coming up is slow and deliberate.
        public double RaisePerSecond = 0.05;

        /// Rods fall under gravity, so going down is not.
        public double DropPerSecond = 0.5;

        /// Seconds one rod runs at full output. It burns proportionally slower with the rods part way.
        public double FuelSecondsPerRod = 3600.0;

        /// The chambers, each with its own rod and lever.
        public IReadOnlyList<FuelChamber> Chambers => _chambers;

        /// How many chambers it has, one or more. Changing it keeps the chambers already there; new ones start with
        /// a rod in.
        public int ChamberCount
        {
            get => _chambers.Count;
            set
            {
                int count = value < 1 ? 1 : value;
                while (_chambers.Count > count) _chambers.RemoveAt(_chambers.Count - 1);
                while (_chambers.Count < count) _chambers.Add(new FuelChamber());
            }
        }

        /// Where the master lever was last set. Setting it sets every chamber's lever to it, SCRAM included; a
        /// chamber's own lever can move away from it afterwards.
        public double TargetWithdrawal
        {
            get => _master;
            set
            {
                _master = FuelChamber.Clamp(value);
                foreach (var chamber in _chambers) chamber.TargetWithdrawal = _master;
            }
        }
        private double _master;

        /// Sets where the master lever stands without moving the chambers, for the network.
        public void CorrectMaster(double withdrawal) => _master = FuelChamber.Clamp(withdrawal);

        /// Where the rods are on average across the chambers, from fully in (0) to fully out (1).
        public double Withdrawal
        {
            get
            {
                double sum = 0.0;
                foreach (var chamber in _chambers) sum += chamber.Withdrawal;
                return sum / _chambers.Count;
            }
        }

        /// Watts it would make with every chamber's rods fully out.
        public double MaxOutput => OutputPerChamber * _chambers.Count;

        public double WattsProduced
        {
            get
            {
                double load = 0.0;
                foreach (var chamber in _chambers) load += chamber.Load;
                return OutputPerChamber * load;
            }
        }

        /// Watts of heat it is making, which is a consequence of output and not a fault.
        public double HeatWatts => WattsProduced * HeatRatio;

        /// The temperature it should be sitting at right now, given how hard its chambers are working together.
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

        /// Turns the master lever to zero, which takes every chamber with it: the crew's big red button.
        public void Scram() => TargetWithdrawal = 0.0;

        /// Sets every chamber's rods to one position, for the network or a test.
        public void CorrectWithdrawal(double withdrawal)
        {
            foreach (var chamber in _chambers) chamber.CorrectWithdrawal(withdrawal);
        }

        /// Burns every chamber's fuel and lets its rods travel toward where they have been asked to be.
        public void ProducePower(double seconds)
        {
            foreach (var chamber in _chambers)
                chamber.Advance(seconds, Gripped, RaisePerSecond, DropPerSecond, FuelSecondsPerRod);
        }

        public override string ToString()
        {
            string rods = _chambers.Count == 1
                ? $"rods {_chambers[0].Withdrawal:P0} of {FuelChamber.Clamp(_chambers[0].TargetWithdrawal):P0}, fuel {_chambers[0].Fuel:P0}"
                : $"{_chambers.Count} chambers, rods {Withdrawal:P0} on average";
            return $"{WattsProduced:0} W, {rods}{(Gripped ? "" : " NO GRIP")}, {BaselineCelsius:0} C nominal";
        }
    }
}
