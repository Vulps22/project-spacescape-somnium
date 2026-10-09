namespace SomniumSpace.Worlds.SpaceScape.Ship
{
    /// Something a rocker switch throws: a conduit's switch addon, or a component's own switch (the loader's
    /// eject switch). It keeps its own state and shares it; the rocker only shows it and presses it.
    public interface ISwitch
    {
        /// True while it is on: a switch addon conducting, a loader ejecting.
        bool Closed { get; }

        /// Sets it by hand: takes effect here at once and is passed on to the master.
        void Press(bool closed);
    }
}
