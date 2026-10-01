using System;
using UnityEngine;

namespace SomniumSpace.Worlds.SpaceScape.Ship
{
    /// The addon slot on a conduit. Holds which addon is installed as a prefab, and puts it on the cable
    /// when the world starts, so nothing of it is in the scene while editing. Also holds which side it
    /// faces, how it is twisted, and whether it is parked: tucked into the cable and off, leaving plain
    /// cable. A hand changes all of it through the conduit's hologram.
    public sealed class AddonModule : Module
    {
        [Tooltip("The addon installed on this conduit, or none. A prefab carrying a ConduitAddon, like the rocker switch; it appears only in Play mode.")]
        [SerializeField] private ConduitAddon _installed;
        [Tooltip("The side of the conduit the addon faces (world). A player-installed addon faces the side nearest them.")]
        [SerializeField] private GridDirection _facing = GridDirection.ZPlus;
        [Tooltip("Quarter turns about the facing direction. 0 is upright on a wall; on a floor or ceiling, 0 has its top toward +Z.")]
        [SerializeField, Range(0, 3)] private int _turns;
        [Tooltip("Ticked: tucked into the cable and off. The conduit behaves as plain cable, and the addon stays installed.")]
        [SerializeField] private bool _parked;

        private ConduitAddon _addon;
        private Transform _mount;
        private bool _placed;

        /// Raised when an addon is installed, removed, moved, twisted, parked or unparked.
        public event Action Changed;

        /// The addon on the cable, parked or not, or null. Asking puts the installed one on first, so it is
        /// there for whichever script asks first.
        public ConduitAddon Addon
        {
            get
            {
                if (!_placed && Application.isPlaying) Place();
                return _addon;
            }
        }

        /// The prefab installed, or null.
        public ConduitAddon Installed => _installed;

        /// The addon while it is doing something: null when there is none or it is parked.
        public ConduitAddon Active => _parked ? null : Addon;

        /// The side it faces.
        public GridDirection Facing => _facing;

        /// Quarter turns about its facing.
        public int Turns => _turns;

        /// True while it is tucked into the cable and off.
        public bool Parked => _parked;

        /// True while the working addon asks for the hologram to be unlocked first. A parked addon asks for
        /// nothing, since its unlock point is tucked away with it.
        public bool ShouldBeLocked()
        {
            var addon = Active;
            return addon != null && addon.ShouldLockHologram();
        }

        /// Where a hand unlocks the hologram, or null.
        public Transform UnlockPoint => Active != null ? Active.UnlockTransform : null;

        private void Awake() => Place();

        /// Sets what is installed and how it sits, before the world starts. For the drawn layout, which
        /// builds conduits while they are inactive; does nothing once the addon is on the cable.
        public void Configure(ConduitAddon installed, GridDirection facing, int turns, bool parked)
        {
            if (_placed) return;
            _installed = installed;
            _facing = facing == GridDirection.None ? GridDirection.ZPlus : facing;
            _turns = ((turns % 4) + 4) % 4;
            _parked = parked;
        }

        /// Puts the installed addon on the cable, once.
        private void Place()
        {
            if (_placed) return;
            _placed = true;
            _mount = new GameObject("AddonMount").transform;
            _mount.SetParent(transform, false);
            if (_installed != null) _addon = Instantiate(_installed, _mount, false);
            Pose();
        }

        /// Installs an addon facing a given side, when there is none already. False when the slot is taken.
        public bool Install(ConduitAddon prefab, GridDirection facing)
        {
            if (prefab == null || Addon != null) return false;
            _installed = prefab;
            _facing = facing;
            _turns = 0;
            _parked = false;
            _addon = Instantiate(prefab, _mount, false);
            Pose();
            Changed?.Invoke();
            return true;
        }

        /// Takes the addon off the cable and returns the prefab of the item it comes out as, or null when
        /// there is nothing to take out.
        public AddonItem Remove()
        {
            var addon = Addon;
            if (addon == null || addon.Item == null) return null;
            var item = addon.Item;
            _installed = null;
            _addon = null;
            Destroy(addon.gameObject);
            Changed?.Invoke();
            return item;
        }

        private bool _hidden;

        /// True while the addon is hidden and out of reach because its conduit's hologram is open, here or for
        /// another player. Set on each client from its own view of who has the hologram open; parking and
        /// installing keep to it.
        public bool Hidden
        {
            get => _hidden;
            set
            {
                if (_hidden == value) return;
                _hidden = value;
                Pose();
            }
        }

        /// Moves, twists, parks or unparks the addon.
        public void Arrange(GridDirection facing, int turns, bool parked)
        {
            if (facing == GridDirection.None) facing = _facing;
            turns = ((turns % 4) + 4) % 4;
            if (facing == _facing && turns == _turns && parked == _parked) return;
            _facing = facing;
            _turns = turns;
            _parked = parked;
            Pose();
            Changed?.Invoke();
        }

        /// Sets the mount to face its side at its twist, and shows the addon only while it is not parked.
        private void Pose()
        {
            if (_mount == null) return;
            Vector3 forward = _facing.Vector();
            float outward = _addon != null ? _addon.Outward * GridCell.Half : 0f;
            _mount.SetPositionAndRotation(transform.position + forward * outward,
                Quaternion.LookRotation(forward, UpFor(_facing, _turns)));
            bool shown = !_parked && !_hidden;
            if (_addon != null && _addon.gameObject.activeSelf != shown) _addon.gameObject.SetActive(shown);
        }

        /// Which way is up for an addon facing a side, after some quarter turns about that side.
        public static Vector3 UpFor(GridDirection facing, int turns)
        {
            Vector3 forward = facing.Vector();
            Vector3 upright = Mathf.Abs(forward.y) > 0.5f ? Vector3.forward : Vector3.up;
            return Quaternion.AngleAxis(turns * 90f, forward) * upright;
        }

        /// The quarter turns about a side that bring its up nearest a given direction, or -1 when the
        /// direction lies along the side and says nothing about a twist.
        public static int NearestTurns(GridDirection facing, Vector3 up)
        {
            Vector3 forward = facing.Vector();
            Vector3 flat = up - forward * Vector3.Dot(up, forward);
            if (flat.sqrMagnitude < 0.04f) return -1;
            int best = 0;
            float bestDot = float.MinValue;
            for (int turns = 0; turns < 4; turns++)
            {
                float dot = Vector3.Dot(flat, UpFor(facing, turns));
                if (dot > bestDot) { bestDot = dot; best = turns; }
            }
            return best;
        }

        private void OnDrawGizmos()
        {
            if (_installed == null) return;
            Vector3 forward = _facing.Vector();
            Gizmos.matrix = Matrix4x4.TRS(transform.position + forward * (_installed.Outward * GridCell.Half),
                Quaternion.LookRotation(forward, UpFor(_facing, _turns)), Vector3.one);
            Gizmos.color = _parked ? new Color(0.5f, 0.5f, 0.5f, 0.6f) : new Color(0.3f, 1f, 0.6f, 0.8f);
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(0.1f, 0.18f, 0.02f));
            Gizmos.DrawLine(Vector3.zero, Vector3.forward * 0.08f);
            Gizmos.DrawLine(Vector3.zero, Vector3.up * 0.12f);
        }
    }
}
