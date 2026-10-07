namespace SomniumSpace.Worlds.SpaceScape.Ship
{
    /// A small, read-only record of what something shares on the data network (data.md): only this travels,
    /// never the thing that made it, so a reader cannot reach past what is sent. Each publisher has its own
    /// kind (RadarData, later BatteryData, SwitchData). Set when made and never changed, so readers holding the
    /// same one cannot change what each other sees.
    public abstract class DataObject { }

    /// Anything that shares data: a module or a conduit addon. Module and ConduitAddon both answer null
    /// (sharing nothing) until one overrides it. Typed as DataObject, since Unity cannot compile an override
    /// that narrows the return type; DataGrid.Read&lt;T&gt; filters to the kind a reader wants.
    public interface IDataSource
    {
        /// What it shares right now, or null for nothing.
        DataObject GetData();
    }
}
