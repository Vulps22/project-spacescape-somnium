using SomniumSpace.Worlds.SpaceScape.Power;
using UnityEngine;

namespace SomniumSpace.Worlds.SpaceScape.Ship
{
    /// What a tile does with the power it holds, and how much it lets go of. The only module that
    /// knows what the tile is.
    [RequireComponent(typeof(NodeEdgeModule))]
    public abstract class BehaviourModule : Module
    {
        /// What the grid asks to put power on. Null for anything that makes and stores nothing.
        public abstract IPowerSource Source { get; }

        /// What the grid hands power to. Null for anything that only routes.
        public abstract IPowerSink Sink { get; }

        /// The tile's hold, or null when it has none.
        protected Capacitor Hold => TryGetComponent<CapacitorModule>(out var c) ? c.Capacitor : null;

        /// The tile's condition, or null when it has none.
        protected Integrity Integrity => TryGetComponent<IntegrityModule>(out var i) ? i.Integrity : null;

        /// The tile's integrity module's debug lines on a line of their own, or nothing when it has none.
        protected string IntegrityText =>
            TryGetComponent<IntegrityModule>(out var integrity) ? "\n" + integrity.DebugText : "";

        protected virtual void OnEnable() => Tile.On = true;

        protected virtual void OnDisable() => Tile.On = false;
    }
}
