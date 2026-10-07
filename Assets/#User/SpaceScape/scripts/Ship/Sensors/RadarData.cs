using System.Collections.Generic;
using UnityEngine;

namespace SomniumSpace.Worlds.SpaceScape.Ship
{
    /// One thing a radar sees: which contact it is and where, in the world, at the moment it was read.
    public readonly struct Contact
    {
        /// Which contact it is, the same on every client.
        public readonly int Id;

        /// Where it is.
        public readonly Vector3 Position;

        /// The way it faces.
        public readonly Vector3 Forward;

        public Contact(int id, Vector3 position, Vector3 forward)
        {
            Id = id;
            Position = position;
            Forward = forward;
        }
    }

    /// What a powered radar shares on its data network (radar.md): everything it sees, and the target the crew
    /// has selected on its screen, if that is among them. A copy, made when read.
    public sealed class RadarData : DataObject
    {
        /// Everything it sees, in id order.
        public IReadOnlyList<Contact> Contacts { get; }

        /// The selected target, or null when nothing is selected or the selection is not in sight.
        public Contact? Selected { get; }

        public RadarData(IReadOnlyList<Contact> contacts, Contact? selected)
        {
            Contacts = contacts;
            Selected = selected;
        }
    }
}
