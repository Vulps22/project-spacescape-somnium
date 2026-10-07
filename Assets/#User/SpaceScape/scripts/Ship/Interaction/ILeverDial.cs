namespace SomniumSpace.Worlds.SpaceScape.Ship
{
    /// Something a RodLever sets: a dial from 0 to 1. One thing can have several, told apart by axis: the
    /// reactor has one (its rods), the disruptor two (traverse and elevation).
    public interface ILeverDial
    {
        /// Where the dial is now, 0 to 1.
        float GetDial(int axis);

        /// True while a hand on this client holds the lever, so the network leaves the dial alone meanwhile.
        void SetDialHeld(int axis, bool held);

        /// A hand turned the dial: takes effect here at once and is passed on to whoever decides.
        void TurnDial(int axis, float value);
    }
}
