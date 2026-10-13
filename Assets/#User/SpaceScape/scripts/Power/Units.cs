namespace SomniumSpace.Worlds.SpaceScape.Power
{
    /// How every reading is written, wherever it is shown: watts, joules, degrees or anything else, scaled by kilo,
    /// mega, giga, tera (and on) to fit three digits: 450 W, 1.2 kW, 85 MJ, 1.4 kC.
    public static class Units
    {
        private static readonly string[] Prefixes = { "", "k", "M", "G", "T", "P", "E", "Z", "Y" };

        /// A reading as up to three digits and its unit, apart, for a display that shows them on separate texts.
        /// A reading under ten keeps one decimal (1.2 kW, not 1 kW; 1.5 L/s), dropped when it is whole and unscaled
        /// (2 L/s); a negative one keeps its sign.
        public static (string value, string unit) Split(double reading, string unit)
        {
            string sign = reading < 0.0 ? "-" : "";
            double size = System.Math.Abs(reading);
            int prefix = 0;
            while (prefix < Prefixes.Length - 1 && size >= 999.5)
            {
                size /= 1000.0;
                prefix++;
            }
            string value = size >= 9.95 ? size.ToString("0") : size.ToString(prefix > 0 ? "0.0" : "0.#");
            if (value == "0") sign = "";
            return (sign + value, Prefixes[prefix] + unit);
        }

        /// A reading with its unit: "1.2 MW". plus puts a + on a positive one, for a net flow.
        public static string Format(double reading, string unit, bool plus = false)
        {
            var (value, scaled) = Split(reading, unit);
            if (plus && reading > 0.0 && value != "0") value = "+" + value;
            return $"{value} {scaled}";
        }

        public static string Watts(double watts, bool plus = false) => Format(watts, "W", plus);

        public static string Joules(double joules) => Format(joules, "J");

        public static string Celsius(double celsius) => Format(celsius, "C");
    }
}
