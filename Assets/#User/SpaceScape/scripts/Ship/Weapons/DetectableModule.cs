using System.Collections.Generic;
using UnityEngine;

namespace SomniumSpace.Worlds.SpaceScape.Ship
{
    /// Marks something the radar can see (docs/radar.md). Only while it is active: a target knocked out is
    /// switched off and drops off the list until it comes back. Not a grid Module: what it marks is out in
    /// space, not a tile.
    public sealed class DetectableModule : MonoBehaviour
    {
        private static readonly List<DetectableModule> Present = new List<DetectableModule>();

        /// Everything the radar could see right now, wherever it is.
        public static IReadOnlyList<DetectableModule> All => Present;

        /// Where it is.
        public Vector3 Position => transform.position;

        /// The way it faces: its local x+.
        public Vector3 Forward => transform.right;

        /// Which contact it is, the same on every client, for a radar's cursor and selection to name. Set by
        /// whatever places it (TargetField numbers its targets); -1 until then.
        public int Id { get; set; } = -1;

        private void OnEnable()
        {
            if (!Present.Contains(this)) Present.Add(this);
        }

        private void OnDisable() => Present.Remove(this);
    }
}
