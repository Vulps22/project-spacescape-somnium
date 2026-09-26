namespace SpaceScape.Power
{
    /// A drum of coolant and a pump. Coolant carries heat away and boils off doing it, so running hot
    /// costs supplies, and the pump is a hard ceiling a reactor can be pushed past.
    public sealed class CoolantLoop
    {
        /// Degrees one litre takes with it when it boils. Real water is ~2.26 MJ/kg; this is the
        /// same idea in the grid's own units.
        public double DegreesPerLitre = 20.0;

        /// Most the pump can shift, whatever the crew asks for.
        public double MaxFlowLitresPerSecond = 2.0;

        /// What the crew has actually dialled in. Nobody turns this down for you.
        public double Flow;

        /// What is left in the drum.
        public double Litres = 200.0;

        /// Litres a second it is really moving, once the pump and the drum have had their say.
        public double ActualFlow
        {
            get
            {
                double asked = Flow < 0.0 ? 0.0 : Flow;
                if (asked > MaxFlowLitresPerSecond) asked = MaxFlowLitresPerSecond;
                return Litres <= 0.0 ? 0.0 : asked;
            }
        }

        /// True when the pump is already flat out and still being asked for more.
        public bool AtCeiling => Flow > MaxFlowLitresPerSecond && Litres > 0.0;

        /// Boils off whatever ran this tick and reports the degrees it carried away.
        public double Draw(double seconds)
        {
            double litres = ActualFlow * seconds;
            if (litres <= 0.0) return 0.0;
            if (litres > Litres) litres = Litres;

            Litres -= litres;
            return litres * DegreesPerLitre;
        }

        /// Puts a fresh drum on the pad.
        public void Refill(double litres) => Litres += litres;

        public override string ToString() =>
            $"{Litres:0} L, {ActualFlow:0.##} L/s{(AtCeiling ? " (PUMP MAXED)" : "")}";
    }
}
