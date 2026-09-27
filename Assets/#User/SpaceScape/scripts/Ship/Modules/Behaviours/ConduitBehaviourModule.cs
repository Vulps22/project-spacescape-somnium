using SomniumSpace.Worlds.SpaceScape.Power;
using UnityEngine;

namespace SomniumSpace.Worlds.SpaceScape.Ship
{
    /// A length of cable. Stores and makes nothing, so the grid carries whatever arrives straight on. An
    /// addon installed through its AddonModule adds to it; this module is still the only one that writes the tile.
    public class ConduitBehaviourModule : BehaviourModule
    {
        private const int MaxSegments = 6;

        [Tooltip("How many segments this conduit has, on faces or parked: 2 for a plain conduit. An addon may add more.")]
        [SerializeField, Range(2, MaxSegments)] private int _segments = 2;

        private AddonModule _slot;
        private ConduitAddon _heard;

        /// The addon working on this conduit, or null when there is none or it is parked.
        public ConduitAddon Addon => _slot != null ? _slot.Active : null;

        /// How many segments this conduit has, whether on a face or parked in the middle, counting its addon's.
        public int Segments
        {
            get
            {
                var addon = Addon;
                return Mathf.Min(MaxSegments, _segments + (addon != null ? addon.ExtraSegments : 0));
            }
        }

        public override IPowerSource Source => null;

        public override IPowerSink Sink => null;

        /// True when a hand must pass through an unlock point before this conduit's hologram opens. Asks the
        /// addon slot; a conduit with behaviour of its own can override it and ask base first.
        public virtual bool ShouldBeLocked() => _slot != null && _slot.ShouldBeLocked();

        /// Where the unlock cube goes while ShouldBeLocked is true.
        public virtual Transform UnlockPoint => _slot != null ? _slot.UnlockPoint : null;

        /// Starts listening to the addon slot, and to whatever addon is in it.
        private void Awake()
        {
            _slot = GetComponent<AddonModule>();
            if (_slot != null) _slot.Changed += OnAddonChanged;
            Listen();
        }

        private void OnDestroy()
        {
            if (_slot != null) _slot.Changed -= OnAddonChanged;
            if (_heard != null) _heard.Changed -= Apply;
        }

        protected override void OnEnable() => Apply();

        /// An addon went in or came out: listen to the new one and ask again.
        private void OnAddonChanged()
        {
            Listen();
            Apply();
        }

        private void Listen()
        {
            if (_heard != null) _heard.Changed -= Apply;
            _heard = Addon;
            if (_heard != null) _heard.Changed += Apply;
        }

        /// Puts the tile in the grid unless disabled or its addon stops power crossing it.
        private void Apply()
        {
            var addon = Addon;
            Tile.On = enabled && (addon == null || addon.Conducts);
        }
    }
}
