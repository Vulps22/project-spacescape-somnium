using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace SomniumSpace.Worlds.SpaceScape.Ship
{
    /// An item that can be loaded into a component through its item port (loading.md). What it is, is its type:
    /// each kind of item is a subclass (LoadableFuelRodItem), and a LoadingModule takes every item of the type it
    /// accepts. A placeholder for now; custom behaviour (a dropped rod making a radioactive zone) comes later. An
    /// item port takes one once a hand lets go of it inside the port's intake, draws it in and removes it; an
    /// open pack of its type takes one back the same way.
    public abstract class LoadableItem : Item
    {
        /// Raised when the last hand on this client lets go of a loadable item.
        public static event Action<LoadableItem> LetGo;

        /// True for the local stand-in a pack shows in its place, never spawned: a hand taking it is given a
        /// real item instead (ItemPack). Ports ignore it.
        public bool Picture { get; set; }

        protected override void OnLetGo(SelectExitEventArgs args)
        {
            base.OnLetGo(args);
            if (!Grab.isSelected) LetGo?.Invoke(this);
        }
    }
}
