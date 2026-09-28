namespace SomniumSpace.Worlds.SpaceScape.Ship
{
    /// State a module keeps that every player must see the same. The master's copy is written out; every
    /// other client reads it back in and carries on simulating from there. The order and count must be
    /// the same on every client, which they are, since every client has the same prefab.
    public interface INetworkedState
    {
        /// How many values this module writes.
        int StateCount { get; }

        /// Writes this module's values into the buffer, starting at the index given.
        void WriteState(float[] to, int at);

        /// Takes this module's values from the buffer, starting at the index given.
        void ReadState(float[] from, int at);
    }
}
