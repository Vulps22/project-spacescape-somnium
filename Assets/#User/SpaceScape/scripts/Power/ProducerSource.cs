namespace SpaceScape.Power
{
    /// Puts a producer's output onto the grid. A reaction cannot be declined, so everything it makes
    /// is offered whether the grid can take it or not, and what cannot leave becomes heat at the node.
    public sealed class ProducerSource : IPowerSource
    {
        private readonly IPowerProducer _producer;

        public ProducerSource(IPowerProducer producer)
        {
            _producer = producer;
        }

        /// The producer behind this, for anything that needs to read fuel or set a rate.
        public IPowerProducer Producer => _producer;

        /// Offers the lot regardless of whether there is anywhere for it to go.
        public double WattsOffered(PowerNode node, double seconds) => _producer.WattsProduced;

        public void ProvidePower(PowerNode node, double seconds) => _producer.ProducePower(seconds);

        public override string ToString() => $"producing {_producer.WattsProduced:0.##} W";
    }
}
