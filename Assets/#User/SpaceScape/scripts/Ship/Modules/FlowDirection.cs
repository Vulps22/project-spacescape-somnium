namespace SomniumSpace.Worlds.SpaceScape.Ship
{
    /// Which way power may cross one face of a tile. Never both. Data is for a component's data port only: no
    /// power crosses it, and data goes both ways (data.md). Item and ItemOut are for a component's item ports
    /// only: no power crosses them; items go in at an Item port and leave by an ItemOut port, along an item
    /// conduit (loading.md). Append only: ports store these as numbers.
    public enum FlowDirection
    {
        None = 0,
        In,
        Out,
        Data,
        Item,
        ItemOut,
    }
}
