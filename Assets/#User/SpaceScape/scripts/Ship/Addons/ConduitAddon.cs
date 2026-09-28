using System;
using UnityEngine;

namespace SomniumSpace.Worlds.SpaceScape.Ship
{
    /// Something installed on a conduit as a child of it, adding to the cable without replacing it. The
    /// conduit asks it questions and does the writing; an addon overrides only what it changes.
    public abstract class ConduitAddon : MonoBehaviour
    {
        [Tooltip("The item a hand takes out of the hologram's addon slot when this is removed, and puts back in to install it.")]
        [SerializeField] private AddonItem _item;
        [Tooltip("Ticked: the hologram shows a handle that turns this to face any free side of the conduit, twists it about that direction, or parks it in the middle, where it is off and the conduit is plain cable. Unlike a junction's extra segments, the handle carries no power.")]
        [SerializeField] private bool _repositionable;
        [Tooltip("How far out along its facing this sits: 0 in the middle of the cable, like a junction's box; 1 flush with the cell's face, where a wall's access panel will be, like a switch.")]
        [SerializeField, Range(0f, 1f)] private float _outward;

        [Header("Hologram")]
        [Tooltip("Ticked: a hand must pass through the unlock cube before this conduit's hologram opens. For addons with something to press, like a switch or a fuse.")]
        [SerializeField] private bool _requireHologramUnlock;
        [Tooltip("An empty GameObject where the unlock cube appears. Needed when Require Hologram Unlock is ticked; without it the conduit opens as normal.")]
        [SerializeField] private Transform _unlockTransform;

        /// Raised when an answer below has changed and the conduit should ask again.
        public event Action Changed;

        /// Raised when a hand on this client changes the addon's state, for the network to pass on.
        public event Action<ConduitAddon> Operated;

        /// The addon's own state as one number, for the network to carry: a switch's open or closed. 0 for
        /// an addon with none.
        public virtual int NetworkState { get => 0; set { } }

        /// The item this comes out as, or null when it cannot be removed.
        public AddonItem Item => _item;

        /// True when the hologram shows a handle for moving, twisting and parking this.
        public bool Repositionable => _repositionable;

        /// How far out along its facing this sits, from 0 (the middle) to 1 (flush with the cell's face).
        public float Outward => _outward;

        /// True when a hand must touch the unlock cube before the hologram opens. Asked through the conduit's
        /// ShouldBeLocked, so an addon with more to it can decide from its own state.
        public virtual bool ShouldLockHologram() => _requireHologramUnlock && _unlockTransform != null;

        /// Where the unlock cube sits, or null.
        public Transform UnlockTransform => _unlockTransform;

        /// False while this addon stops power crossing the conduit.
        public virtual bool Conducts => true;

        /// Segments this addon adds to the conduit's own.
        public virtual int ExtraSegments => 0;

        /// Tells the conduit to ask again.
        protected void RaiseChanged() => Changed?.Invoke();

        /// Says a hand on this client changed it.
        protected void RaiseOperated() => Operated?.Invoke(this);
    }
}
