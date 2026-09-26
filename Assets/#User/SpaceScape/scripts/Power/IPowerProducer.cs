namespace SomniumSpace.Worlds.SpaceScape.Power
{
    /// Something that generates power. It knows nothing about the grid, only about making watts.
    public interface IPowerProducer
    {
        /// Watts it is generating right now.
        double WattsProduced { get; }

        /// Advances whatever production depends on. Fuel, rods and the like belong to the implementation.
        void ProducePower(double seconds);
    }
}
