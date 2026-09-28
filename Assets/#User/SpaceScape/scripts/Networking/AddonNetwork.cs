using System.Collections.Generic;
using Fusion;
using SomniumSpace.Network.Bridge;
using SomniumSpace.Worlds.SpaceScape.Ship;
using UnityEngine;

namespace SomniumSpace.Worlds.SpaceScape.Networking
{
    /// One installed addon's state, as its own networked object: which conduit it is on (by slot), which
    /// addon it is, how it sits, and its own state (a switch's open or closed). The master spawns it and
    /// writes it from the addon on its conduit; every other client puts the same addon on the same conduit
    /// and keeps it in line.
    ///
    /// A hand on a non-master client changes the addon here at once and asks the master. Until the master
    /// agrees, or a second passes, the incoming state is left alone, so a quick press is not undone by the
    /// value that was current before it.
    public sealed class AddonNetwork : MonoBehaviour
    {
        /// Wire ids. Append only; never insert or renumber.
        private enum AddonMessageType : byte
        {
            SetState = 0,
        }

        [Tooltip("Slot + 1, addon id, facing | turns << 3 | parked << 5, and the addon's own state. Four ints.")]
        [SerializeField] private NetworkBridgeData _data;
        [Tooltip("Seconds a press on this client is kept against the incoming state while the master catches up.")]
        [SerializeField] private float _pressHoldSeconds = 1f;

        private static readonly List<AddonNetwork> All = new List<AddonNetwork>();
        private int _claimed = -1;
        private ConduitAddon _heard;
        private int _pressed;
        private float _pressedUntil;

        private void OnEnable() => All.Add(this);

        private void OnDisable()
        {
            All.Remove(this);
            Listen(null);
        }

        private void Awake()
        {
            if (_data != null) _data.OnMessageToController += OnMessageToController;
        }

        private void OnDestroy()
        {
            if (_data != null) _data.OnMessageToController -= OnMessageToController;
        }

        public NetworkObject Object => _data != null ? _data.Object : null;

        private bool Live => Object != null && Object.IsValid;

        /// The conduit slot this addon is on, or -1 until the master has written it.
        public int Slot => _claimed >= 0 ? _claimed : Live && _data.IntArray[0] > 0 ? _data.IntArray[0] - 1 : -1;

        /// The addon object on this slot, or null.
        public static AddonNetwork Find(int slot)
        {
            foreach (var addon in All)
                if (addon.Slot == slot) return addon;
            return null;
        }

        /// Every addon object this client writes.
        public static IEnumerable<AddonNetwork> Owned()
        {
            foreach (var addon in All.ToArray())
                if (addon.Live && addon.Object.HasStateAuthority) yield return addon;
        }

        /// Marks a freshly spawned addon object as the master's, for this slot.
        public void Claim(int slot) => _claimed = slot;

        private void Update()
        {
            if (!Live) return;
            int slot = Slot;
            var tile = ConduitNetwork.TileAt(slot);
            if (tile == null || !tile.TryGetComponent<AddonModule>(out var module)) return;

            if (Object.HasStateAuthority) Write(slot, module);
            else Read(module);

            Listen(module.Addon);
        }

        private void Write(int slot, AddonModule module)
        {
            var addon = module.Addon;
            if (addon == null) return;

            var ints = _data.IntArray;
            SetIfChanged(ints, 1, ConduitNetwork.AddonId(module.Installed));
            SetIfChanged(ints, 2, ConduitNetwork.AddonPose(module));
            SetIfChanged(ints, 3, addon.NetworkState);
            SetIfChanged(ints, 0, slot + 1);   // last: marks the rest as real
        }

        private void Read(AddonModule module)
        {
            var ints = _data.IntArray;
            if (ints[0] == 0 || ConduitNetwork.IsHeld(Slot)) return;

            var addons = ConduitNetwork.Addons;
            int id = ints[1];
            var prefab = addons != null && id > 0 && id <= addons.Count ? addons[id - 1] : null;
            if (prefab == null) return;

            int pose = ints[2];
            var facing = (GridDirection)(pose & 7);
            int turns = (pose >> 3) & 3;
            bool parked = (pose & (1 << 5)) != 0;

            if (module.Installed != prefab && module.Addon != null) module.Remove();
            if (module.Addon == null) module.Install(prefab, facing);
            module.Arrange(facing, turns, parked);

            var addon = module.Addon;
            if (addon == null) return;
            int state = ints[3];
            if (Time.time < _pressedUntil && state != _pressed) return;
            _pressedUntil = 0f;
            if (addon.NetworkState != state) addon.NetworkState = state;
        }

        private void Listen(ConduitAddon addon)
        {
            if (_heard == addon) return;
            if (_heard != null) _heard.Operated -= OnOperated;
            _heard = addon;
            if (_heard != null) _heard.Operated += OnOperated;
        }

        private void OnOperated(ConduitAddon addon)
        {
            if (!Live || Object.HasStateAuthority) return;
            _pressed = addon.NetworkState;
            _pressedUntil = Time.time + _pressHoldSeconds;
            _data.RPC_SendMessageToController(MessageFrame.Pack((byte)AddonMessageType.SetState, System.BitConverter.GetBytes(_pressed)));
        }

        private void OnMessageToController(byte[] framed)
        {
            if (!Live || !Object.HasStateAuthority) return;
            if (!MessageFrame.Unpack(framed, out byte id, out byte[] data)) return;

            switch ((AddonMessageType)id)
            {
                case AddonMessageType.SetState:
                    var tile = ConduitNetwork.TileAt(Slot);
                    if (data.Length >= 4 && tile != null && tile.TryGetComponent<AddonModule>(out var module) && module.Addon != null)
                        module.Addon.NetworkState = System.BitConverter.ToInt32(data, 0);
                    break;
                default:
                    Debug.LogWarning($"AddonNetwork: unknown message {id}", this);
                    break;
            }
        }

        private static void SetIfChanged(NetworkArray<int> ints, int i, int value)
        {
            if (ints[i] != value) ints.Set(i, value);
        }

        private void OnValidate()
        {
            if (_data == null) _data = GetComponent<NetworkBridgeData>();
        }
    }
}
