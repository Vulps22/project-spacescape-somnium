namespace SpaceScape.Power
{
    /// The grid-facing side of anything that puts power onto the grid.
    public interface IPowerSource
    {
        /// Watts it hands the grid. Given the tile it sits on, so it can ask whatever it needs to:
        /// whether there is a cable, whether the run reaches anything, whether anything wants power.
        /// A producer cannot decline and offers everything regardless; a store may.
        double WattsOffered(PowerNode node);

        /// Advances the grid-facing side, given the tile it sits on.
        void ProvidePower(PowerNode node, double seconds);
    }
}
