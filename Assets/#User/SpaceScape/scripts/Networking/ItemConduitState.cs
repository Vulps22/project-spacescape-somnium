using Fusion;
using SomniumSpace.Network.Bridge;
using UnityEngine;

namespace SomniumSpace.Worlds.SpaceScape.Networking
{
    /// The item conduits' runs for every player, spawned once by the master (ItemConduitNetwork): a stamp the
    /// master bumps with every write (0 until the first), how many ints follow, then the runs as ItemConduits
    /// writes them.
    public sealed class ItemConduitState : MonoBehaviour
    {
        [Tooltip("Stamp, length, then the runs. 64 ints.")]
        [SerializeField] private NetworkBridgeData _data;

        /// The one in the world, or null until it has been spawned and arrived here.
        public static ItemConduitState Current { get; private set; }

        public NetworkBridgeData Data => _data;

        public NetworkObject Object => _data != null ? _data.Object : null;

        public bool Live => Object != null && Object.IsValid;

        private void OnEnable() => Current = this;

        private void OnDisable()
        {
            if (Current == this) Current = null;
        }

        private void OnValidate()
        {
            if (_data == null) _data = GetComponent<NetworkBridgeData>();
        }
    }
}
