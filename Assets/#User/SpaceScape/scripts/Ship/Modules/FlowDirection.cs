namespace SomniumSpace.Worlds.SpaceScape.Ship
{
    /// Which way power may cross one face of a tile. Never both. Data is for a component's data port only: no
    /// power crosses it, and data goes both ways (data.md). Item is for a component's item port only: no power
    /// crosses it, and loadable items go in there (loading.md). Append only: ports store these as numbers.
    public enum FlowDirection
    {
        None = 0,
        In,
        Out,
        Data,
        Item,
    }
}
