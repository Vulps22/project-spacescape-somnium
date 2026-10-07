namespace SomniumSpace.Worlds.SpaceScape.Ship
{
    /// Which way power may cross one face of a tile. Never both. Data is for a component's data port only: no
    /// power crosses it, and data goes both ways (data.md).
    public enum FlowDirection
    {
        None = 0,
        In,
        Out,
        Data,
    }
}
